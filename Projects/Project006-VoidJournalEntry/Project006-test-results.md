# Project 006 -- Test Results

**Date:** 2026-04-03
**Commit:** ff85632
**Result:** 8/8 verified
**Test run:** 101/101 passed (all projects), 8/8 void scenarios passed
**Verdict:** APPROVED

---

## BDD to Verification Mapping

| BDD ID | Description | Verified | Scenario(s) |
|--------|-------------|----------|-------------|
| AC-001 | Valid void sets voided_at and void_reason | Yes | 1 |
| AC-002 | Voided entry remains in the database | Yes | 2 |
| AC-003 | Lines and references unaffected by void | Yes | 3 |
| AC-004 | Idempotent re-void preserves timestamps and reason | Yes | 4 |
| AC-005 | Empty/whitespace void reason rejected | Yes | 5, 6 |
| AC-006 | Nonexistent entry ID rejected | Yes | 7 |
| AC-007 | Void succeeds in a closed fiscal period | Yes | 8 |
| AC-008 | Service returns Result<JournalEntry, string list> | Yes | 1, 5, 6, 7 |

---

## AC-001: Valid void sets voided_at and void_reason

**Verdict:** VERIFIED

**Feature scenario:** "Void an active entry successfully"

**Production code evidence:**
- `JournalEntryRepository.voidEntry` (JournalEntryRepository.fs:74-107) executes
  `UPDATE ledger.journal_entry SET voided_at = now(), void_reason = @reason, modified_at = now() WHERE id = @id AND voided_at IS NULL`
  then returns the entry via SELECT.
- `JournalEntryService.voidInTransaction` (JournalEntryService.fs:130-136) validates reason,
  calls repository, wraps result as `Ok entry`.

**Test assertion evidence:**
- Step def line 198: `Assert.True(entry.voidedAt.IsSome, "Expected voided_at to be set")`
- Step def line 199: `Assert.Equal(Some expectedReason, entry.voidReason)`
- Step def line 202: Checks `voidedAt` is within 30 seconds of now (recency check)

---

## AC-002: Voided entry remains in the database (not deleted)

**Verdict:** VERIFIED

**Feature scenario:** "Voided entry remains in the database"

**Production code evidence:**
- The void path performs only an UPDATE. No DELETE statement exists anywhere in the
  void code path (repository or service).

**Test assertion evidence:**
- Step def line 212-219: Executes raw SQL `SELECT description, voided_at FROM ledger.journal_entry WHERE id = @id`,
  asserts `reader.Read()` is true (row exists), description matches "To be voided",
  and `voided_at` is not null.

---

## AC-003: Lines and references unaffected by voiding

**Verdict:** VERIFIED

**Feature scenario:** "Lines and references are intact after void"

**Production code evidence:**
- The UPDATE in `voidEntry` targets only `journal_entry` table columns
  (`voided_at`, `void_reason`, `modified_at`). No SQL touches
  `journal_entry_line` or `journal_entry_reference` during void.

**Test assertion evidence:**
- Step def line 231-234: `SELECT COUNT(*) FROM ledger.journal_entry_line WHERE journal_entry_id = @id`
  asserted equal to 2.
- Step def lines 236-247: Reads `journal_entry_reference` rows, asserts type = "cheque",
  value = "5678", and count = 1.

---

## AC-004: Idempotent re-void preserves timestamps and reason

**Verdict:** VERIFIED

**Feature scenario:** "Void an already-voided entry is idempotent"

**Production code evidence:**
- Repository SQL: `WHERE id = @id AND voided_at IS NULL`. If already voided,
  UPDATE affects 0 rows. The subsequent SELECT returns the unchanged entry.
- No `modified_at` update occurs on the no-op path.

**Test assertion evidence:**
- Given step (line 158-164) records `FirstVoidedAt` and `FirstModifiedAt` after the
  first void.
- When step voids again with reason "Second void attempt".
- Then step (line 252-261):
  - `Assert.Equal(Some expectedReason, entry.voidReason)` -- expectedReason is "First void"
  - `Assert.Equal(ctx.FirstVoidedAt, entry.voidedAt)` -- original timestamp unchanged
  - `Assert.Equal(ctx.FirstModifiedAt, Some entry.modifiedAt)` -- original modified_at unchanged

---

## AC-005: Empty/whitespace void reason rejected with descriptive error

**Verdict:** VERIFIED

**Feature scenarios:** "Empty void reason is rejected", "Whitespace-only void reason is rejected"

**Production code evidence:**
- `JournalEntryService.validateVoidCommand` (JournalEntryService.fs:124-127):
  `if String.IsNullOrWhiteSpace cmd.voidReason then Error ["Void reason is required and cannot be empty"]`
- This covers both empty string "" and whitespace-only "   " since
  `String.IsNullOrWhiteSpace` handles both cases.
- Validation runs before any DB call, so the entry is never touched on rejection.

**Test assertion evidence:**
- Scenario 5: When step passes reason "". Then step (line 264-272) asserts Error
  result, error message contains "reason" (case-insensitive).
- Scenario 6: When step passes reason "   ". Same Then assertion.

**Minor note:** The BDD document's scenarios 5 and 6 include "And the entry remains
un-voided in the database" but the feature file's Then step does not explicitly
re-query the DB to verify. This is structurally guaranteed (validation rejects before
any DB call), but it is not independently asserted. Accepted because the AC text says
"rejected with descriptive error" which is fully covered.

---

## AC-006: Nonexistent entry ID rejected with descriptive error

**Verdict:** VERIFIED

**Feature scenario:** "Nonexistent entry ID is rejected"

**Production code evidence:**
- `JournalEntryRepository.voidEntry` returns `None` when SELECT finds no row.
- `JournalEntryService.voidInTransaction` (line 136):
  `None -> Error [sprintf "Journal entry with id %d does not exist" cmd.journalEntryId]`

**Test assertion evidence:**
- When step (line 185-188) uses hardcoded entry ID 999999.
- Then step (line 264-272) asserts Error result contains "does not exist"
  (case-insensitive match).

---

## AC-007: Voiding in a closed fiscal period succeeds

**Verdict:** VERIFIED

**Feature scenario:** "Void succeeds in a closed fiscal period"

**Production code evidence:**
- The entire void code path (both service and repository) contains zero references
  to `is_open`, `fiscal_period`, or period status. The void only checks: (1) reason
  is non-empty, (2) entry exists, (3) entry not already voided. Closed period is
  irrelevant to the void operation by design.

**Test assertion evidence:**
- Given step posts an entry while period is open, then closes the period via
  `UPDATE ledger.fiscal_period SET is_open = false WHERE id = @id` (line 168-172).
- When step voids the entry with reason "Late correction".
- Then step (same as AC-001 scenario) asserts Ok result with voided_at set and
  void_reason = "Late correction".

---

## AC-008: Service returns Result<JournalEntry, string list>

**Verdict:** VERIFIED

**Production code evidence:**
- `voidInTransaction` signature (JournalEntryService.fs:130):
  `NpgsqlTransaction -> VoidJournalEntryCommand -> Result<JournalEntry, string list>`
- `voidEntry` signature (JournalEntryService.fs:140):
  `string -> VoidJournalEntryCommand -> Result<JournalEntry, string list>`

**Test assertion evidence:**
- Ok branch exercised by scenarios 1, 2, 3, 4, 8 -- all pattern-match on `Some (Ok entry)`.
- Error branch exercised by scenarios 5, 6, 7 -- all pattern-match on `Some (Error errs)`.
- F# type system enforces the return type at compile time. The tests would not compile
  if the service returned a different type.

---

## Cross-Artifact Traceability Spot-Checks (5/5 passed)

| # | Chain | Result |
|---|-------|--------|
| 1 | AC-004 -> BRD 4.2 -> `voidEntry` WHERE clause -> Scenario 4 step def -> Test pass | Intact |
| 2 | AC-006 -> BRD Edge Cases -> Service None branch -> Scenario 7 -> Test pass | Intact |
| 3 | AC-007 -> BRD Section 5 -> No is_open check in void -> Scenario 8 + period close -> Test pass | Intact |
| 4 | AC-003 -> BRD "Lines unchanged" -> UPDATE only header cols -> Scenario 3 asserts counts/values -> Test pass | Intact |
| 5 | AC-001 -> BRD AC-001 -> Repository UPDATE -> Scenario 1 Then assertions -> Test pass | Intact |

---

## Fabrication Detection

- **File existence:** All cited source files exist at their stated paths.
- **Test output:** 101/101 tests passed including all 8 void scenarios, confirmed by
  `dotnet test --verbosity normal` run at commit ff85632.
- **Circular evidence:** None. Each criterion verified against production code (SQL and F#
  logic) and test assertions (xUnit Assert calls) independently.
- **Stale evidence:** Test run performed at current HEAD (ff85632).
- **Omitted failures:** Zero failures. Full test output examined line by line for the
  8 void scenarios.

---

## Observations (Non-Blocking)

1. **BDD doc vs feature file fidelity on AC-005:** The BDD document includes an assertion
   that "the entry remains un-voided in the database" for scenarios 5 and 6. The feature
   file does not include this as an explicit Then step. The behavior is structurally
   guaranteed by the code (validation rejects before any DB call), and all tests run
   inside a rolled-back transaction, so there is no risk. This is a documentation gap,
   not a behavioral gap. Non-blocking.

2. **VoidJournalEntryCommand DTO:** The BRD calls for this as a deliverable. It exists
   at Ledger.fs:118-120 with fields `journalEntryId: int` and `voidReason: string`.
   Confirmed present and used by both service functions.
