# 035 — Orphaned Posting Detection

**Epic:** K — Code Audit Remediation
**Depends On:** 043 (idempotency guards), 036 (CLI framework)
**Status:** Not started
**Priority:** Low

---

## Problem Statement

`ObligationPostingService.postToLedger` and `TransferService.confirm` used to
commit journal entries in one transaction, then update the source record
(obligation instance / transfer) in a separate transaction. If the second
write failed — network blip, DB timeout, process kill — the result was an
**orphaned journal entry**: money moved in the ledger with no corresponding
status update on the source record.

P043 (Idempotency Guards) closed the hole going forward. Retries now detect
existing journal entries and skip the duplicate post. But any orphans created
**before P043 landed** are still sitting in the database undetected.

This project is a **one-time forensic diagnostic** that also serves as an
**ongoing health check** for the nagging agent or manual reconciliation.

## What It Does

A read-only diagnostic query, exposed as a CLI command, that finds:

1. **Dangling status updates** — Journal entries in `journal_entry_reference`
   with `reference_type` = `obligation` or `transfer`, where the source
   record's `journal_entry_id` is NULL. This means the journal entry posted
   but the source record was never updated to reflect it.

2. **Missing source records** — Journal entries in `journal_entry_reference`
   pointing to a `reference_value` (obligation instance ID or transfer ID)
   that does not exist in the corresponding table at all.

3. **Posted with voided backing entry** — Obligation instances in `posted`
   status or transfers in `confirmed` status whose `journal_entry_id`
   references a journal entry where `voided_at IS NOT NULL`. Per REM-015
   design intent, this is not an error — the instance retains its historical
   reference — but it is a condition that requires human attention.

**The query is read-only. No mutations. No fixes.** If orphans are found,
Dan decides what to do about them manually.

## CLI Command

```
leobloom diagnostic orphaned-postings [--json]
```

This introduces a new `diagnostic` command group in the CLI. This is the
first command in the group. Future diagnostics (accounting equation check,
trial balance reconciliation) would live here too.

**Output (human-readable):** A summary line ("N orphaned postings found" or
"No orphaned postings found"), followed by a table of results if any exist.
Each row shows: source type (obligation/transfer), source record ID, journal
entry ID, and the specific condition detected (dangling status, missing
source, voided backing entry).

**Output (--json):** An array of result objects with the same fields.

**Exit codes:** 0 = ran successfully (regardless of whether orphans were
found), 1 = validation error, 2 = system error.

## Acceptance Criteria

### Behavioral (Gherkin scenarios)

| ID | Criterion | Description |
|----|-----------|-------------|
| AC-B1 | No orphans returns clean result | When no orphaned postings exist, the command outputs "No orphaned postings found" and exits 0. |
| AC-B2 | Detects dangling obligation status | Given a journal entry with reference_type="obligation" and reference_value pointing to an obligation instance whose journal_entry_id is NULL, the diagnostic reports it. |
| AC-B3 | Detects dangling transfer status | Given a journal entry with reference_type="transfer" and reference_value pointing to a transfer whose journal_entry_id is NULL, the diagnostic reports it. |
| AC-B4 | Detects missing obligation source | Given a journal entry with reference_type="obligation" and reference_value pointing to a nonexistent obligation instance ID, the diagnostic reports it. |
| AC-B5 | Detects missing transfer source | Given a journal entry with reference_type="transfer" and reference_value pointing to a nonexistent transfer ID, the diagnostic reports it. |
| AC-B6 | Detects posted obligation with voided JE | Given an obligation instance in "posted" status whose journal_entry_id references a voided journal entry, the diagnostic reports it. |
| AC-B7 | Detects confirmed transfer with voided JE | Given a transfer in "confirmed" status whose journal_entry_id references a voided journal entry, the diagnostic reports it. |
| AC-B8 | Normal postings are not flagged | Given a properly completed obligation posting (source record has journal_entry_id set, matching reference exists, JE not voided), the diagnostic does not report it. |
| AC-B9 | JSON output mode | With --json flag, the output is valid JSON containing the same data as the human-readable output. |

### Structural (verified by QE/Governor, not Gherkin)

| ID | Criterion | Description |
|----|-----------|-------------|
| AC-S1 | New `diagnostic` command group | A `Diagnostic` case exists in the top-level `LeoBloomArgs` DU, routing to a new `DiagnosticCommands.fs` module. |
| AC-S2 | Repository query is read-only | The diagnostic query uses SELECT only. No INSERT, UPDATE, DELETE. |
| AC-S3 | No new migrations | The diagnostic uses existing schema (`journal_entry_reference`, `journal_entry`, `obligation_instance`, `transfer`). |
| AC-S4 | Existing tests still pass | All pre-existing BDD tests pass without modification. |

## Scope Boundaries

### In scope

- New `DiagnosticCommands.fs` in `LeoBloom.CLI` with the `orphaned-postings`
  subcommand
- Repository query (or queries) to detect the three orphan conditions
- Human-readable and JSON output formatting
- BDD scenarios covering AC-B1 through AC-B9

### Explicitly out of scope

- **No automatic remediation.** The diagnostic reports; Dan decides.
- **No scheduled execution.** The nagging agent calling this on a cron is
  a future concern, not this project's.
- **No obligation commands.** P038 (CLI obligation commands) is separate.
- **No new indexes.** The existing indexes from P044 should be sufficient
  for this query. If the planner disagrees, they can add one, but the
  default assumption is no migration needed.
- **No alerting or notification.** Just CLI output.

## Design Notes

- The query needs to join `journal_entry_reference` to `journal_entry`
  (for `voided_at`), then left-join to `ops.obligation_instance` and
  `ops.transfer` based on `reference_type` and `reference_value`. The
  reference_value is stored as varchar; it will need to be cast to integer
  for the join.
- The "posted with voided JE" check goes the other direction: start from
  obligation instances / transfers that have a non-null `journal_entry_id`,
  join to `journal_entry`, and check `voided_at IS NOT NULL`.
- This could be one query or two (one starting from references, one starting
  from source records). The planner decides.

## Risk Notes

- **reference_value casting:** `reference_value` is `varchar(200)`. If any
  non-numeric values exist for obligation/transfer reference types, the
  cast to integer will fail. The query should handle this gracefully (skip
  or report as a separate anomaly).
- **Performance:** At current data volume this is trivial. The query touches
  small tables. No concern.

## Source

- Code audit SYNTHESIS.md (2026-04-05)
- REM-012 cancellation note (pattern detection belongs in diagnostic, not
  hard gate)
- REM-015 / S8 design decision (posted with voided JE is a reportable
  condition)
