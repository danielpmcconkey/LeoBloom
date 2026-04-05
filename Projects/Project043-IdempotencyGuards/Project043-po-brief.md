# Project 043 -- PO Brief: Idempotency Guards for Posting Services

**Product Owner:** PO Agent
**Date:** 2026-04-05
**Backlog item:** 043-idempotency-guards.md
**Source:** Code audit SYNTHESIS.md, Tier 1 Finding #1

---

## Problem Statement

`ObligationPostingService.postToLedger` and `TransferService.confirm` execute
three-phase workflows across separate database connections/transactions:

1. Read + validate (connection 1, committed and closed)
2. Post journal entry via `JournalEntryService.post` (connection 2, committed and closed)
3. Update source record status (connection 3)

If phase 2 succeeds but phase 3 fails -- network blip, DB timeout, constraint
violation, whatever -- you get an orphaned journal entry. Money moved in the
ledger but the obligation instance / transfer record still shows its old status.
The code logs a warning claiming "retry is safe," but there is no idempotency
guard. A retry re-enters phase 2 and creates a second journal entry for the
same obligation/transfer. That is a duplicate financial record in a system
whose entire purpose is data integrity.

This was the #1 consensus finding from a 6-reviewer code audit. Five of six
reviewers flagged it explicitly.

---

## Acceptance Criteria

### Behavioral (Gherkin scenarios)

These are observable behaviors the PO cares about:

| ID | Criterion | Description |
|----|-----------|-------------|
| AC-B1 | First-time obligation posting works unchanged | Posting a confirmed obligation instance with no prior journal entry creates a journal entry, transitions the instance to posted, and returns success. No behavioral change from current code. |
| AC-B2 | Retry after partial failure skips duplicate (obligation) | If an obligation instance has a non-voided journal entry matching reference_type="obligation" / reference_value="{instanceId}", `postToLedger` skips the journal entry post, uses the existing entry's ID, and completes the status transition. Returns success. |
| AC-B3 | First-time transfer confirm works unchanged | Confirming an initiated transfer with no prior journal entry creates a journal entry, updates the transfer to confirmed status, and returns success. No behavioral change from current code. |
| AC-B4 | Retry after partial failure skips duplicate (transfer) | If a transfer has a non-voided journal entry matching reference_type="transfer" / reference_value="{transferId}", `confirm` skips the journal entry post, uses the existing entry's ID, and completes the status transition. Returns success. |
| AC-B5 | Voided prior entry does NOT trigger the guard | If the only matching journal entry has been voided (`voided_at IS NOT NULL`), the guard does not treat it as a duplicate. A new journal entry is posted normally. |
| AC-B6 | Return value is identical whether guard fires or not | The caller cannot distinguish a first-time post from a guarded retry by the return type. Both return Ok with the journal entry ID and the source record ID. |

### Structural (verified by QE/Governor, not Gherkin)

| ID | Criterion | Description |
|----|-----------|-------------|
| AC-S1 | New repository query exists | A function in `JournalEntryRepository` (or equivalent location) queries `journal_entry_reference` joined to `journal_entry` for a matching reference_type + reference_value where `journal_entry.voided_at IS NULL`. |
| AC-S2 | Guard is present in both services | Both `ObligationPostingService.postToLedger` and `TransferService.confirm` contain the idempotency check between phase 1 and phase 2. |
| AC-S3 | No new database migration required | The `journal_entry_reference` table and `journal_entry.voided_at` column already exist. The guard uses existing schema. |
| AC-S4 | Existing tests still pass | All pre-existing BDD tests pass without modification. The guard is invisible to normal (non-retry) code paths. |

---

## Scope Boundaries

### In scope

- Idempotency lookup query in `JournalEntryRepository` (new function)
- Guard logic in `ObligationPostingService.postToLedger` (before `JournalEntryService.post` call)
- Guard logic in `TransferService.confirm` (before `JournalEntryService.post` call)
- BDD scenarios covering AC-B1 through AC-B6
- The guard must filter out voided entries (only non-voided matches count)

### Explicitly out of scope

- **No single-transaction refactor.** That is audit option (a), a larger structural change. This project is option (b).
- **No TOCTOU fix.** The race window between the guard check and the journal entry post remains. At current scale (single CLI caller), this is acceptable and acknowledged.
- **No retry framework.** The guard makes retries safe; automatic retry logic is a separate concern.
- **No unique constraint on journal_entry_reference.** The table allows multiple references per entry (e.g., an entry could reference both an obligation and an invoice). A unique constraint on (reference_type, reference_value) would break legitimate future use cases. The guard is a read-before-write check, not a schema constraint.
- **No changes to JournalEntryService.post.** The guard lives in the calling services, not in the journal entry posting infrastructure.
- **No logging changes beyond the guard itself.** If the guard fires, it should log at info level that it found an existing entry and is skipping the post. That is all.

---

## Risk Notes

### For the Planner

1. **The lookup query must join through `journal_entry`.** The `voided_at` column lives on `journal_entry`, not on `journal_entry_reference`. The query needs: `journal_entry_reference` JOIN `journal_entry` ON `journal_entry_id = id` WHERE `reference_type = @rt AND reference_value = @rv AND voided_at IS NULL`. Getting this wrong (querying only the reference table) would match voided entries and incorrectly skip re-posting.

2. **Return type from the lookup.** The guard needs the journal entry ID to pass to the status transition (phase 3). The lookup function should return `int option` (the journal entry ID) or the full `JournalEntry` record. Keep it minimal -- an ID is sufficient.

3. **The guard sits between phase 1 result and phase 2 call.** In `ObligationPostingService`, that is between the `match readResult with` and the `JournalEntryService.post jeCmd` call (around line 102). In `TransferService`, between `Ok (transfer, fiscalPeriod)` and `JournalEntryService.post jeCmd` (around line 143). The guard needs its own connection/transaction for the lookup (consistent with how these services already open separate connections per phase).

### For the Builder

4. **Connection lifecycle.** The guard lookup needs a DB connection. These services open fresh connections per phase. The guard should follow the same pattern: open, query, close. Do not try to reuse the phase 1 connection (it is already closed/committed by the time the guard runs).

5. **The `PostToLedgerResult` and `Transfer` return types.** Verify that the guard can populate the same return shape regardless of whether it posted a new entry or found an existing one. For `ObligationPostingService`, the return is `Ok { journalEntryId; instanceId }` -- the guard just substitutes the found entry ID. For `TransferService`, the return goes through `TransferRepository.updateConfirm` which takes the journal entry ID as a parameter -- same substitution applies.

### For QE

6. **Test setup for retry scenarios.** To simulate a partial failure, the test needs to: (a) create the prerequisite records (obligation/transfer in the right status), (b) directly insert a journal entry + reference matching the expected reference_type/reference_value, (c) call the posting service, and (d) verify no new journal entry was created and the status transition completed. This is not a real retry -- it is simulating the state that a partial failure leaves behind.

7. **Voided entry test setup.** Same as above, but the pre-inserted journal entry must have `voided_at IS NOT NULL`. The service should then create a new entry (not skip).

---

## Verdict

This brief defines the product requirements for P043. It is ready for the Planner to pick up and produce a phased implementation plan. The fix is surgical -- roughly 10-15 lines per service plus a repository query function. No architectural changes.
