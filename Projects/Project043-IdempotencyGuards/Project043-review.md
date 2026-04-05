# Project 043 -- Technical Review: Idempotency Guards

**Reviewer:** Technical Reviewer (BD)
**Date:** 2026-04-05
**Verdict:** APPROVED

---

## Summary

The implementation is clean, correct, and faithful to the plan. The Builder did
exactly what was asked -- no more, no less. The QE wrote solid tests that
actually prove the guard works. All 428 tests pass (verified independently).

---

## Spec Fidelity

Every acceptance criterion traces to working code and/or passing tests:

| AC | Status | Evidence |
|----|--------|----------|
| AC-B1 | PASS | Existing @FT-POL-001 through @FT-POL-006 pass unmodified |
| AC-B2 | PASS | @FT-POL-018 -- inserts pre-existing JE, calls service, verifies no duplicate, verifies posted status + correct JE ID |
| AC-B3 | PASS | Existing @FT-TRF-007 through @FT-TRF-012 pass unmodified |
| AC-B4 | PASS | @FT-TRF-015 -- same pattern as obligation, transfer-specific |
| AC-B5 | PASS | @FT-POL-019 + @FT-TRF-016 -- insert JE, void it, verify service creates NEW entry |
| AC-B6 | PASS | Both guard paths return identical shapes: `Ok { journalEntryId; instanceId }` (obligation) and `Ok <Transfer>` (transfer) |
| AC-S1 | PASS | `JournalEntryRepository.findNonVoidedByReference` at line 132 of JournalEntryRepository.fs |
| AC-S2 | PASS | Guard present in ObligationPostingService.fs:101-113 and TransferService.fs:143-155 |
| AC-S3 | PASS | No migration files added. Uses existing `journal_entry_reference` and `journal_entry.voided_at` |
| AC-S4 | PASS | All 424 pre-existing tests pass. 4 new tests bring total to 428 |

---

## Correctness

### Repository Query (JournalEntryRepository.fs:132-153)

SQL is correct:
- Joins `journal_entry_reference` to `journal_entry` on `journal_entry_id = id`
- Filters on `reference_type`, `reference_value`, AND `voided_at IS NULL`
- `LIMIT 1` is appropriate -- the guard only needs one match
- Parameterized queries, no injection risk
- Returns `int option` as planned

### Guard Logic (ObligationPostingService.fs:101-139, TransferService.fs:143-176)

- Guard runs between phase 1 result and phase 2 call -- correct insertion point
- Opens its own connection + transaction, consistent with existing per-phase pattern
- `try/with` handles exceptions with rollback and re-raise -- no silent swallowing
- `Some jeId` path skips `JournalEntryService.post` and goes straight to phase 3 with the existing JE ID
- `None` path executes original code unchanged (only indentation changes)
- Return shapes are identical in both paths

### Transfer Guard Path Error Handling

The guard's `| Some jeId ->` path in `TransferService.fs:163-176` drops the
"retry is safe" log that the normal path has. This is correct: if the guard
already found an existing JE, telling the user "retry is safe" is nonsensical.
The error message text itself is preserved.

---

## Edge Cases

### Guard query throws

If `findNonVoidedByReference` throws (DB timeout, connection failure), the
`with ex ->` block rolls back the guard transaction and re-raises. The exception
propagates up and the service returns nothing silently -- the caller gets the
exception. This is the correct behavior: fail loud, don't proceed with a
potentially stale or incorrect state.

### Multiple non-voided entries for the same reference

The SQL uses `LIMIT 1`, so it would return the first match. This is fine --
if there are somehow two non-voided entries for the same reference, the guard
catches the first one and skips the post. The guard's job is "don't create
another one," not "detect existing duplicates." The PO brief explicitly scoped
out unique constraints.

### Null/empty reference values

The `string instance.id` and `string transfer.id` calls produce the string
representation of an integer -- never null or empty. The reference values stored
in the DB came from the same `string <id>` pattern. No null risk here.

---

## Style Consistency

- Connection lifecycle follows existing per-phase pattern (open, use, close via `use` binding)
- Error handling matches existing try/with/rollback pattern
- Logging uses existing `Log.info` and `Log.warn` with structured parameters
- Guard log is info-level as PO brief specified
- Indentation is consistent with surrounding code

---

## Test Quality

### @FT-POL-018 and @FT-TRF-015 (Retry tests)

Strong assertions:
1. Result is `Ok` with correct JE ID matching pre-existing one
2. Count query proves no duplicate (exactly 1 non-voided reference)
3. DB cross-check confirms status transition completed
4. Cleanup tracks both pre-existing and potentially-created JE IDs

### @FT-POL-019 and @FT-TRF-016 (Voided entry tests)

Strong assertions:
1. Result is `Ok` with JE ID different from voided one
2. DB cross-check confirms status transition completed
3. Voided JE is tracked for cleanup

### Test setup is realistic

The partial-failure simulation (insert JE + reference + balanced lines directly)
correctly models the state that a real phase-2-success / phase-3-failure would
leave behind. Including balanced lines is a nice touch -- structurally valid JEs,
not just stubs.

---

## Scope Check

Production code changes are limited to exactly 3 files:
- `JournalEntryRepository.fs` -- new function appended (no existing code modified)
- `ObligationPostingService.fs` -- guard inserted, existing code indented into `| None ->`
- `TransferService.fs` -- same pattern as obligation service

Test code changes limited to 2 files, feature file changes limited to 2 files.
All match the plan. No scope creep detected.

The `| None ->` branches preserve the original code exactly (verified via diff),
with only indentation changes to nest under the new match expression.

---

## Findings

None. Implementation is correct, complete, matches spec, and follows existing
patterns. Tests are thorough with proper cleanup. No defects found.
