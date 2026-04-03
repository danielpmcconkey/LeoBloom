# BRD-006: Void Journal Entry

**Project:** 006 — Void Journal Entry
**Author:** Basement Dweller (BA role)
**Date:** 2026-04-03
**Status:** Draft — Awaiting PO Approval

---

## 1. Business Objective

Deliver the ability to mark an existing journal entry as void. Voided entries
remain in the ledger (append-only) but are excluded from all balance and report
calculations. This is essential for correcting errors without destroying the
audit trail — the fundamental reason double-entry systems use voids instead of
deletes.

This is the second write operation in the ledger, building directly on 005's
persistence and service infrastructure.

## 2. Scope

### In Scope

1. **Void command DTO** — `VoidJournalEntryCommand` carrying the journal entry
   `id` and a `voidReason` string. Minimal surface: the caller identifies what
   to void and explains why.

2. **Pure validation** — `validateVoidReason` already exists in
   `LeoBloom.Domain/Ledger.fs` and enforces non-empty, non-whitespace
   `void_reason` when voiding. 006 reuses it.

3. **Repository layer** — a new function in `JournalEntryRepository` that:
   - Looks up the entry by `id`
   - If already voided (`voided_at IS NOT NULL`), returns the existing entry
     unchanged (idempotent no-op)
   - If not voided, sets `voided_at = now()`, `void_reason = @reason`,
     `modified_at = now()` via `UPDATE ... RETURNING *`
   - Returns `Option<JournalEntry>` — `None` if the entry ID doesn't exist

4. **Service layer** — a `void` function in `JournalEntryService` that:
   - Validates `voidReason` (non-empty, non-whitespace)
   - Calls the repository
   - Returns `Result<JournalEntry, string list>` — the voided entry or errors
   - Error cases: empty/whitespace reason, entry not found

5. **Closed period behavior** — Voiding an entry in a closed fiscal period is
   **allowed**. Rationale: voiding is a metadata update on an existing record,
   not a new posting. The `is_open` gate prevents new entries from being posted
   into the period; it does not prevent administrative corrections to existing
   entries. This is consistent with standard accounting practice where voids and
   adjustments to existing entries are permitted after period close.

6. **BDD tests** — integration tests in `LeoBloom.Dal.Tests` exercising:
   - Happy path: void an active entry, verify `voided_at` and `void_reason` set
   - Idempotent: void an already-voided entry, verify timestamps unchanged
   - Rejection: empty void reason
   - Rejection: whitespace-only void reason
   - Rejection: entry ID does not exist
   - Closed period: void succeeds in a closed period
   - State verification: voided entry still readable (not deleted)
   - State verification: lines and references remain intact after void

### Out of Scope

- **Unvoiding / reversing a void** — not in backlog. If needed, post a
  correcting entry.
- **Auto-creating reversing entries** — explicitly excluded per backlog.
- **Cascade to ops** — `obligation_instance.journal_entry_id` or
  `transfer.journal_entry_id` pointing to a voided entry is the ops layer's
  problem (Epic F/G).
- **Balance query changes** — all existing balance queries already filter on
  `voided_at IS NULL` per DataModelSpec. No query changes needed.
- **API endpoints** — no HTTP layer. Service + Dal only.

## 3. Dependencies

| Dependency | Status | Notes |
|-----------|--------|-------|
| 001 — Database schema | Done | `voided_at` and `void_reason` columns exist on `journal_entry` |
| 005 — Post journal entry | Done | Service + repository infrastructure; entries must exist to void them |
| Domain `validateVoidReason` | Done | Already in `Ledger.fs`, tested in Domain.Tests |

## 4. Technical Approach

### 4.1 Project Structure

- `LeoBloom.Domain/Ledger.fs` — add `VoidJournalEntryCommand` DTO
- `LeoBloom.Dal/JournalEntryRepository.fs` — add `voidEntry` function
- `LeoBloom.Dal/JournalEntryService.fs` — add `void` function
- `LeoBloom.Dal.Tests/` — new feature file and step definitions for 006

### 4.2 Idempotency

Voiding an already-voided entry returns success with the existing entry
unchanged. The repository checks `voided_at IS NOT NULL` before updating. This
means:

- No error on double-void
- Original `voided_at` timestamp preserved
- Original `void_reason` preserved
- `modified_at` NOT updated on the no-op path

This is a deliberate design choice: the first void is the one that matters.
Re-voiding is a no-op, not an overwrite.

### 4.3 SQL Approach

Single `UPDATE` with a `WHERE` clause that includes `voided_at IS NULL`. If
the entry is already voided, `UPDATE` affects 0 rows. Then `SELECT` to return
the current state. This avoids race conditions — the `WHERE` clause is the
concurrency guard.

Alternative considered: `SELECT` then conditional `UPDATE`. Rejected because
it's two round trips and has a TOCTOU window. The single-`UPDATE` approach is
both simpler and safer.

### 4.4 Transaction Semantics

The void operation is a single `UPDATE` on one row. No multi-table writes. A
transaction is still used for consistency with the 005 pattern and to support
the `postInTransaction`-style test-friendly variant.

## 5. Edge Cases (per Backlog)

| Case | Behavior |
|------|----------|
| Entry already voided | Return success, no changes (idempotent) |
| Entry in closed period | Allow — metadata update, not a new posting |
| Entry ID doesn't exist | Error: "Journal entry with id X does not exist" |
| Empty void reason | Error: validation rejection |
| Whitespace-only void reason | Error: validation rejection |
| Voided entry is only entry in period | Fine — period has zero activity |
| Lines/references after void | Unchanged — void only touches header |
| Minimum reason length | None. Non-empty is sufficient. "Error" is valid. |

## 6. Acceptance Criteria Summary

See BDD document (Project006-bdd.md) for full Gherkin scenarios. The BRD
criteria are:

- AC-001: A valid void sets `voided_at` and `void_reason` on the entry
- AC-002: Voided entry remains in the database (not deleted)
- AC-003: Lines and references are unaffected by voiding
- AC-004: Idempotent void returns success without modifying timestamps or reason
- AC-005: Empty/whitespace void reason is rejected with descriptive error
- AC-006: Nonexistent entry ID is rejected with descriptive error
- AC-007: Voiding in a closed fiscal period succeeds
- AC-008: Service returns `Result<JournalEntry, string list>`
