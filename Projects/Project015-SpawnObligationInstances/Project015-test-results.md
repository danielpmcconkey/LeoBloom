# Project 015 -- Spawn Obligation Instances: Test Results

**Date:** 2026-04-05
**Commit:** 6591ab4 (Merge pull request #14 from danielpmcconkey/feat/project014-obligation-agreements)
**Result:** 16/16 criteria verified

## Test Run

```
dotnet test LeoBloom.sln --verbosity normal
Test Run Successful.
Total tests: 341
     Passed: 341
 Total time: 4.9897 Seconds
```

All 341 tests pass, zero failures, zero skipped.

## Acceptance Criteria Verification

### Behavioral Criteria

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| B1 | Pure date generation produces correct dates for Monthly, Quarterly, Annual, and OneTime cadences | Yes | Tests FT-SI-001, FT-SI-005, FT-SI-007, FT-SI-008 exercise all four cadences via `ObligationInstanceSpawning.generateExpectedDates`. All pass. |
| B2 | Day clamping works correctly (e.g., day 31 in February becomes 28/29) | Yes | FT-SI-002 verifies 31 clamps to Feb 28 in non-leap 2027. FT-SI-003 verifies 31 clamps to Feb 29 in leap 2028. `clampDay` at Ops.fs:246-247 uses `DateTime.DaysInMonth`. |
| B3 | `expectedDay` defaults to 1 when agreement has `None` | Yes | FT-SI-004 tests with effectiveDay=1 producing 1st-of-month dates. Service resolves default at ObligationInstanceService.fs:35 via `Option.defaultValue 1`. |
| B4 | Name generation produces cadence-appropriate labels | Yes | FT-SI-010 is a Theory with 8 InlineData cases covering Monthly ("Jan 2026"), Quarterly ("Q1 2026" through "Q4 2026"), Annual ("2026"), OneTime ("One-time"). All pass. |
| B5 | Spawning for an overlapping date range skips existing dates without error | Yes | FT-SI-015 pre-inserts Jan 15 and Feb 15, spawns Jan-Apr, gets 2 created and 2 skipped. FT-SI-016 pre-inserts OneTime date, gets 0 created and 1 skipped. Both pass. |
| B6 | `SpawnResult.skippedCount` accurately reflects how many dates were skipped | Yes | FT-SI-015 asserts `skippedCount = 2`, FT-SI-016 asserts `skippedCount = 1`. Service computes at ObligationInstanceService.fs:56 as `allDates.Length - newDates.Length`. |
| B7 | All spawned instances have `status = Expected` and `isActive = true` | Yes | FT-SI-011 asserts `Expected` status and `isActive = true` on each instance. FT-SI-014 asserts `isActive = true` on all 4 quarterly instances. Repository hardcodes `is_active = true` in INSERT SQL. Service passes `Expected` status. |
| B8 | Fixed-amount agreements pre-fill instance amount; variable agreements leave it `None` | Yes | FT-SI-011 asserts `amount = Some 150.00m`. FT-SI-012 creates agreement with `None` amount and asserts instance `amount.IsNone`. Service passes `agreement.amount` to repository. |
| B9 | Spawning against an inactive agreement returns an error | Yes | FT-SI-017 inserts agreement with `isActive = false`, spawns, asserts Error containing "inactive". Service checks `agreement.isActive` at ObligationInstanceService.fs:29. |
| B10 | Spawning against a nonexistent agreement returns an error | Yes | FT-SI-018 uses agreement ID 999999, asserts Error containing "does not exist". Service checks `findById` returning `None` at ObligationInstanceService.fs:23. |
| B11 | `startDate > endDate` is rejected by validation | Yes | FT-SI-009 creates command with startDate 2026-06-01 > endDate 2026-01-01, asserts Error containing "startdate". Validation at Ops.fs:306. |

### Structural Criteria

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| S1 | `SpawnObligationInstancesCommand` and `SpawnResult` types exist in `Ops.fs` | Yes | Ops.fs:235-242: both record types present with correct field names and types. |
| S2 | `ObligationInstanceRepository` can insert and read back instances with all fields mapped | Yes | ObligationInstanceRepository.fs: `insert` function (lines 42-66) INSERTs with RETURNING and maps via `mapReader` covering all 14 columns (id through modifiedAt). Integration tests (FT-SI-011 etc.) confirm round-trip works. |
| S3 | `findExistingDates` correctly identifies occupied `(agreement_id, expected_date)` pairs | Yes | ObligationInstanceRepository.fs:68-89: SQL filters on `obligation_agreement_id` and `expected_date = ANY(@dates)`. No `is_active` filter -- consistent with plan decision #11. FT-SI-015 and FT-SI-016 exercise this path. |
| S4 | `ObligationInstanceService.spawn` creates instances within a single transaction | Yes | ObligationInstanceService.fs: `conn.BeginTransaction()` at line 20, all inserts via `List.map` at lines 48-52 using the same `txn`, `txn.Commit()` at line 54, rollback in catch block at line 65. |
| S5 | All tests pass via `dotnet test` | Yes | 341/341 passed, 0 failed. Full test run output captured above. |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-SI-001 | Monthly cadence produces correct dates for full range | Yes | Yes |
| @FT-SI-002 | Monthly cadence clamps day 31 to February 28 in non-leap year | Yes | Yes |
| @FT-SI-003 | Monthly cadence clamps day 31 to February 29 in leap year | Yes | Yes |
| @FT-SI-004 | Monthly cadence defaults expectedDay to 1 when agreement has none | Yes | Yes |
| @FT-SI-005 | Quarterly cadence produces four dates for full-year range | Yes | Yes |
| @FT-SI-006 | Quarterly cadence for partial range includes only in-range dates | Yes | Yes |
| @FT-SI-007 | Annual cadence produces one date per year across multi-year range | Yes | Yes |
| @FT-SI-008 | OneTime cadence produces exactly one date at startDate | Yes | Yes |
| @FT-SI-009 | Date range where startDate is after endDate is rejected | Yes | Yes |
| @FT-SI-010 | Instance names follow cadence-specific format (8 examples) | Yes | Yes |
| @FT-SI-011 | Spawn monthly instances for a 3-month range | Yes | Yes |
| @FT-SI-012 | Spawn for variable-amount agreement leaves instance amount empty | Yes | Yes |
| @FT-SI-013 | Spawn OneTime creates a single instance | Yes | Yes |
| @FT-SI-014 | All spawned instances have isActive true | Yes | Yes |
| @FT-SI-015 | Spawn overlapping range skips existing dates and creates new ones | Yes | Yes |
| @FT-SI-016 | Spawn OneTime when instance already exists skips without error | Yes | Yes |
| @FT-SI-017 | Spawn for inactive agreement returns error | Yes | Yes |
| @FT-SI-018 | Spawn for nonexistent agreement returns error | Yes | Yes |

All 18 Gherkin scenarios have corresponding tests with matching `GherkinId` traits. All pass.

## Fabrication Check

- No citations to nonexistent files found.
- No circular evidence detected. All criteria verified against actual source code and live test output.
- Test output is from current commit 6591ab4. Tests were run in this verification session.
- 341/341 tests passed -- no omitted failures.

## Compile Order Verification

- `LeoBloom.Utilities.fsproj` (lines 27-28): `ObligationInstanceRepository.fs` followed by `ObligationInstanceService.fs`, both before `OpeningBalanceService.fs`. Matches plan.
- `LeoBloom.Tests.fsproj` (line 27): `SpawnObligationInstanceTests.fs` appears after `ObligationAgreementTests.fs`. Matches plan.
- `TestHelpers.fs`: `insertObligationAgreementForSpawn` (line 209) and `insertObligationInstanceWithDate` (line 231) are new additions. Existing helpers were not modified (verified by their presence above line 209).

## Verdict

**APPROVED**

Every acceptance criterion is met. Every Gherkin scenario has a corresponding test with the correct trait, and all tests pass. The evidence chain is solid -- verified against actual file contents and a live test run.
