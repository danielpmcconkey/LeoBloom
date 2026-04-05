# Project 052 — Test Results

**Date:** 2026-04-05
**Branch:** feature/P052-account-subtypes
**Base commit:** dbda298 (P052 changes are uncommitted on working tree)
**Result:** 10/10 acceptance criteria verified (2 partially N/A by design)

## Test Execution

```
dotnet test --verbosity normal
Total tests: 551
     Passed: 546
     Failed: 5
```

All 5 failures are pre-existing in `PostObligationToLedgerTests` (unrelated to P052):
- posting when no fiscal period covers confirmed_date returns error
- failed post to closed period leaves instance in confirmed status with no journal entry
- posting when fiscal period is closed returns error
- findByDate returns correct period for a date within range
- findByDate returns None for a date outside all periods

P052-specific tests (AccountSubTypeTests): **89/89 passed, 0 failed.**

## Acceptance Criteria Verification

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| 1 | `account_subtype` column exists on `ledger.account` (nullable varchar) | Yes | Confirmed via `\d ledger.account`: `character varying(25)`, nullable. Migration `1712000021000_AddAccountSubType.sql` adds column and sets values on existing seed accounts. |
| 2 | `AccountSubType` DU exists in Domain with 9 cases | Yes | `Ledger.fs` lines 18-27: DU with Cash, FixedAsset, Investment, CurrentLiability, LongTermLiability, OperatingRevenue, OtherRevenue, OperatingExpense, OtherExpense. Exactly 9 cases. |
| 3 | `toDbString` / `fromDbString` round-trip correctly for all 9 cases | Yes | Module `AccountSubType` (lines 29-53) implements both functions. FT-AST-005 tests all 9 round-trips (9 test cases, all passing). FT-AST-006 verifies `fromDbString` rejects "Bogus". |
| 4 | `isValidSubType` rejects invalid combinations | Yes | `isValidSubType` (lines 67-70) delegates to `validSubTypesForAccountType`. FT-AST-002 tests 8 invalid combos, FT-AST-003 tests all 9 subtypes rejected for Equity. FT-AST-010/011/012 add targeted cases. All pass. |
| 5 | `Account` domain type includes `subType: AccountSubType option` | Yes | `Ledger.fs` line 78: `subType: AccountSubType option` field on the `Account` record type. |
| 6 | All account read paths populate the subtype field | Yes (N/A) | No existing code in LeoBloom.Ledger or LeoBloom.Ops constructs the `Account` domain type. Grep for `Account {`, `Account.`, `: Account`, and `subType` in both projects returned zero matches. The DB column exists and is read correctly by raw SQL in tests (FT-AST-007/008/009/015/016/017). Domain type is ready for future CRUD. |
| 7 | All account write paths persist and validate the subtype field | Yes (N/A) | Same as AC #6: no service-layer write paths exist. The DB column accepts writes (FT-AST-007/008/009/013/014 prove INSERT and UPDATE). Domain validation function (`isValidSubType`) is implemented and tested. Future CRUD services will use both. |
| 8 | Seed data sets correct subtypes on all leaf accounts | Yes | Verified in two ways: (1) `1712000006000_SeedChartOfAccounts.sql` includes `account_subtype` in all INSERT statements with correct values per plan. (2) FT-AST-015 tests 14 leaf accounts against expected subtypes, FT-AST-016 tests 15 headers for null, FT-AST-017 tests 3 equity leaves for null. All 32 test cases pass. |
| 9 | All existing tests pass with no regressions | Yes | 546/551 pass. The 5 failures are pre-existing `PostObligationToLedgerTests` failures, present before P052 work. Zero new failures introduced. |
| 10 | Fresh DB setup (seed migration) includes subtypes | Yes | `1712000006000_SeedChartOfAccounts.sql` now includes `account_subtype` in all INSERT column lists. A fresh DB created from seed migrations will have subtypes on all leaf accounts. FT-AST-015/016/017 implicitly validate this (tests run against the migrated DB). |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes | Test Cases |
|---|---|---|---|---|
| @FT-AST-001 | Valid subtype for account type is accepted | Yes | Yes | 9 |
| @FT-AST-002 | Invalid subtype for account type is rejected | Yes | Yes | 8 |
| @FT-AST-003 | Equity accepts no subtypes | Yes | Yes | 9 |
| @FT-AST-004 | Null subtype is valid for any account type | Yes | Yes | 5 |
| @FT-AST-005 | toDbString and fromDbString round-trip all subtypes | Yes | Yes | 9 |
| @FT-AST-006 | fromDbString rejects an unrecognized string | Yes | Yes | 1 |
| @FT-AST-007 | Account with subtype persists and reads back correctly | Yes | Yes | 1 |
| @FT-AST-008 | Account with null subtype persists and reads back correctly | Yes | Yes | 1 |
| @FT-AST-009 | Subtypes round-trip through write and read paths | Yes | Yes | 9 |
| @FT-AST-010 | Invalid subtype for account type is rejected by domain validation | Yes | Yes | 1 |
| @FT-AST-011 | Any subtype on equity is rejected by domain validation | Yes | Yes | 1 |
| @FT-AST-012 | Invalid subtype change is rejected by domain validation | Yes | Yes | 1 |
| @FT-AST-013 | Updating an account to a valid subtype succeeds | Yes | Yes | 1 |
| @FT-AST-014 | Updating an account to null subtype succeeds | Yes | Yes | 1 |
| @FT-AST-015 | Seed accounts have expected subtypes after migration | Yes | Yes | 14 |
| @FT-AST-016 | Header accounts have null subtype after migration | Yes | Yes | 15 |
| @FT-AST-017 | Equity leaf accounts have null subtype after migration | Yes | Yes | 3 |

**Total: 17/17 scenarios covered, 89/89 test cases passing.**

## Fabrication Check

- All cited files exist and contain the claimed code.
- Test output was produced by this Governor session (`dotnet test` run on 2026-04-05).
- The "Phases 3 & 4 skipped" claim is accurate: grep of LeoBloom.Ledger and LeoBloom.Ops for Account domain type usage returned zero results.
- No circular evidence detected. All claims trace to actual file contents or test execution.

## Verdict

**APPROVED.** All 10 acceptance criteria verified. Evidence chain is solid. The two "N/A" criteria (AC #6, #7) are genuinely not applicable -- no existing code uses the Account domain type, so there are no read/write paths to update. The domain type and DB column are correctly wired for future CRUD work. Zero regressions introduced.
