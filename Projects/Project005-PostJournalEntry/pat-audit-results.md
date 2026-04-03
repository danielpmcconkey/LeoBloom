# Pat's Audit Report — Project 005

## Verdict: CLEAN

## Summary
The team didn't cheat. Every Then step asserts something meaningful that would fail if the code under test were a no-op. The validation chain is complete — all 6 pure validators fire, both DB checks query real data, and the service gates persistence behind both validation phases. The feature file has exactly 21 scenarios matching the BDD doc, every one maps to a Then step, and all 21 pass against a real PostgreSQL database inside a transaction. Dan can sleep easy.

## Audit 1: Assertion Integrity

### Then: `the post succeeds with N lines and valid id and timestamps`
- Asserts `id > 0`, `createdAt > MinValue`, `modifiedAt > MinValue`, line count matches, each line has `id > 0`, correct `journalEntryId`, positive `amount`.
- Fails on Error or None result.
- **Verdict: SOLID.** A no-op would produce None and hit `Assert.Fail`. A fake Ok with zero-id or wrong line count would fail assertions.

### Then: `the post succeeds with N lines`
- Asserts `List.length posted.lines = int count`.
- Fails on Error or None.
- **Verdict: SOLID.** Line count is verified against the expected value.

### Then: `the post succeeds with N references`
- Asserts `List.length posted.references = int count`.
- Fails on Error or None.
- **Verdict: SOLID.** Reference count is verified.

### Then: `the post succeeds with null source`
- Asserts `posted.entry.source.IsNone`.
- Fails on Error or None.
- **Verdict: SOLID.** Explicitly checks the nullable field came back None.

### Then: `the post succeeds with memo values on all lines`
- Iterates all lines, asserts `line.memo.IsSome` for each.
- Fails on Error or None.
- **Verdict: SOLID.** Checks every line, not just the first.

### Then: `the post succeeds` (bare)
- Only asserts result is Ok (not Error or None).
- **Verdict: ACCEPTABLE.** Used for edge cases (duplicate refs, future date) where the point is "this doesn't blow up." The setup is the real test — the Given steps create real DB state, the When posts through the full service. If the service returned Error, Assert.Fail fires.

### Then: `the post fails with error containing "X"`
- Extracts error list, joins with semicolons, calls `Assert.Contains(substring, joined, OrdinalIgnoreCase)`.
- Fails on Ok or None.
- **Verdict: SOLID.** This is the workhorse for all validation scenarios. It verifies the *specific* error message substring, not just "some error happened." A no-op returning Ok would hit `Assert.Fail("Expected Error but got Ok")`.

### Then: `the post fails and no rows persisted for "X"`
- Asserts result is Error, then queries `ledger.journal_entry WHERE description = @d` and asserts count = 0.
- Fails on Ok or None.
- **Verdict: SOLID.** Actually hits the database to verify no orphaned rows. This is real atomicity verification.

### Then: `the entry_type parse result is Error`
- Asserts `EntryTypeParseResult` is `Some (Error _)`.
- Fails on Ok or None.
- **Verdict: SOLID.** Tests the parse boundary directly.

**Hollow assertion count: 0**

## Audit 2: Validation Chain

### `validateCommand` (Ledger.fs, line 155)
Calls these validators on the command:

1. **`validateMinimumLineCount`** — checks `List.length lines >= 2`, errors with "at least 2 lines" message. Verified: single-line input returns Error.
2. **`validateAmountsPositive`** — filters lines where `amount <= 0m`, returns error per offending line. Verified: zero and negative both caught.
3. **`validateBalanced`** — sums debits and credits separately, compares for equality. Verified: unequal amounts return Error with "do not equal".
4. **`validateDescription`** — `String.IsNullOrWhiteSpace` check. Verified: empty string returns Error with "Description is required".
5. **`validateSource`** — None is Ok, Some with whitespace-only is Error. Verified: empty string wrapped in Some returns Error with "Source cannot be empty".
6. **`validateReferences`** — iterates all refs, checks both `referenceType` and `referenceValue` for `IsNullOrWhiteSpace`. Verified: empty type or value returns specific error.

All 6 validators are called. Errors are collected (not short-circuited) — `headerErrors @ lineErrors`. No path bypasses any validator.

### `validateDbDependencies` (JournalEntryService.fs, line 53)
- Queries `ledger.fiscal_period` by ID. If not found: error. If found but `not isOpen`: error. If `entryDate` outside `startDate..endDate`: error.
- Queries `ledger.account` for all distinct account IDs. Missing IDs: error per missing. Inactive accounts: error per inactive.
- All errors collected in mutable list, returned as `Error errors` if non-empty.
- **No path returns Ok without checking.** The only Ok is when `errors.IsEmpty` at line 79.

### `postInTransaction` (JournalEntryService.fs, line 110)
```
validateCommand -> Error? return Error
                -> Ok?
    validateDbDependencies -> Error? return Error
                           -> Ok?
        insertEntry -> insertLines -> insertReferences -> Ok result
```
- `validateCommand` called first (line 111).
- `validateDbDependencies` called second (line 114).
- Persistence only happens inside the `Ok ()` branch of both (line 116-120).
- **No shortcut. No path where inserts happen without validation.**

### `post` (production path, JournalEntryService.fs, line 83)
- Same validation order as `postInTransaction`.
- Opens connection, begins transaction.
- On `validateDbDependencies` Error: explicit `txn.Rollback()`.
- On success: `txn.Commit()`.
- On exception: `try txn.Rollback() with _ -> ()` in catch block.
- **Transaction integrity: confirmed.** Rollback on every failure path.

### Could a bad entry slip through?
No. Both `post` and `postInTransaction` gate on `validateCommand` then `validateDbDependencies` before any insert call. The validators cover: line count, amounts, balance, description, source, references (pure) and period existence/open/date-range, account existence/active (DB). The only theoretical gap is `validateVoidReason` is not called in `validateCommand` — but void fields aren't part of the `PostJournalEntryCommand` DTO, so that's correct by design (voids are a separate operation).

## Audit 3: Test-to-Feature Mapping

| # | Scenario | Then Step | Verdict |
|---|----------|-----------|---------|
| 1 | Simple 2-line entry posts successfully | `the post succeeds with 2 lines and valid id and timestamps` | CORRECT — checks line count, IDs, timestamps |
| 2 | Compound 3-line entry posts successfully | `the post succeeds with 3 lines` | CORRECT — checks 3 lines returned |
| 3 | Entry with references posts successfully | `the post succeeds with 2 references` | CORRECT — checks reference count |
| 4 | Entry with null source posts successfully | `the post succeeds with null source` | CORRECT — checks source.IsNone |
| 5 | Entry with memo on lines posts successfully | `the post succeeds with memo values on all lines` | CORRECT — checks memo.IsSome on every line |
| 6 | Unbalanced entry is rejected | `the post fails with error containing "do not equal"` | CORRECT — unbalanced amounts, error checked |
| 7 | Zero amount is rejected | `the post fails with error containing "non-positive amount"` | CORRECT — 0.00 amounts, error checked |
| 8 | Negative amount is rejected | `the post fails with error containing "non-positive amount"` | CORRECT — -100.00 amounts, error checked |
| 9 | Single-line entry is rejected | `the post fails with error containing "at least 2 lines"` | CORRECT — 1 line only, error checked |
| 10 | Empty description is rejected | `the post fails with error containing "Description"` | CORRECT — empty string description, error checked |
| 11 | Invalid entry_type is rejected | `the entry_type parse result is Error` | CORRECT — "foo" parsed, Error verified |
| 12 | Empty source string is rejected | `the post fails with error containing "Source"` | CORRECT — source="" wrapped in Some, error checked |
| 13 | Closed fiscal period is rejected | `the post fails with error containing "not open"` | CORRECT — Given inserts period with `isOpen=false`, error checked |
| 14 | Entry date outside period range is rejected | `the post fails with error containing "outside"` | CORRECT — date 2026-04-15 vs period ending 2026-03-31, error checked |
| 15 | Inactive account is rejected | `the post fails with error containing "inactive"` | CORRECT — Given inserts account with `isActive=false`, error checked |
| 16 | Nonexistent fiscal period is rejected | `the post fails with error containing "does not exist"` | CORRECT — period ID 99999 never inserted, error checked |
| 17 | Empty reference type is rejected | `the post fails with error containing "reference_type"` | CORRECT — empty reference_type in table, error checked |
| 18 | Empty reference value is rejected | `the post fails with error containing "reference_value"` | CORRECT — empty reference_value in table, error checked |
| 19 | Validation failure leaves no persisted rows | `the post fails and no rows persisted for "Bad entry"` | CORRECT — queries DB to verify zero rows |
| 20 | Duplicate references across entries are allowed | `the post succeeds` | CORRECT — pre-existing entry with same ref created in Given, second post succeeds |
| 21 | Future entry_date with valid open period succeeds | `the post succeeds` | CORRECT — June 2026 period and date, post goes through |

**Mismatches: 0**

## Audit 4: Scenario Count

| Source | Count |
|--------|-------|
| BDD doc (Project005-bdd.md) scenario index | 21 |
| Feature file (PostJournalEntry.feature) scenarios | 21 |
| Distinct Then step definitions | 9 (reusable steps covering all 21 scenarios) |
| Test execution count | 21 |

The BDD doc lists scenarios 1-21. The feature file has exactly 21 `Scenario:` blocks. The 9 Then step definitions are parameterized/reusable (e.g., `the post fails with error containing "X"` handles 10 scenarios). All 21 scenarios map to exactly one Then step each. **Counts match.**

## Audit 5: Test Execution

```
Test Run Successful.
Total tests: 21
     Passed: 21
 Total time: 1.4092 Seconds
```

All 21 tests pass. Zero warnings, zero errors. Tests run against `leobloom_dev` PostgreSQL database (safety check at line 32 of step definitions verifies database name). Each test runs in its own transaction that gets rolled back in the `finally` block, leaving no test pollution.

## Fatal Findings

None.

## Concerns

None. The implementation is straightforward and honest. The tests exercise real database operations through the full service layer. Assertions are specific and would catch regressions. The validation chain is complete with no bypass paths. The one deviation from the BDD doc's Gherkin syntax (BDD doc used multi-step When/Then, feature file consolidated into single steps) is a reasonable implementation choice that doesn't weaken coverage.

## Signed
-- Pat
