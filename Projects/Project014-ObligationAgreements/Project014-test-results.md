# Project 014 -- Obligation Agreements (CRUD) -- Test Results

**Date:** 2026-04-05
**Base commit:** bd2f4ee (work is uncommitted on top of this)
**Result:** 12/13 acceptance criteria verified. 28/28 Gherkin scenarios verified.

## Test Run

```
dotnet test LeoBloom.sln --verbosity normal
Test Run Successful.
Total tests: 316
     Passed: 316
 Total time: 5.3706 Seconds
```

All 316 tests pass, including all 28 obligation agreement tests (FT-OA-001 through FT-OA-028). Zero failures, zero skipped.

## Acceptance Criteria Verification

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| 1 | `CreateObligationAgreementCommand` and `UpdateObligationAgreementCommand` exist in `Ops.fs` | Yes | Lines 157-181 of `Ops.fs`. Both types have all fields per plan. |
| 2 | Pure validation rejects: empty name, name > 100 chars, counterparty > 100 chars, amount <= 0, expected_day outside 1-31, id <= 0 on update | Yes | `ObligationAgreementValidation` module (Ops.fs lines 183-233). All validators confirmed in code. Tests FT-OA-003 through FT-OA-008 exercise each path. `validateUpdateCommand` checks `id <= 0` at line 225. |
| 3 | Pure validation collects multiple errors (not short-circuit) | Yes | `validateCreateCommand` (line 214-221) and `validateUpdateCommand` (line 223-233) use `List.collect` over all sub-validators, accumulating errors. Test FT-OA-008 confirms >= 3 errors returned for name="", amount=-10, expectedDay=0. |
| 4 | `ObligationAgreementRepository` compiles and provides: insert, findById, list, update, deactivate, hasActiveInstances | Yes | All six functions present in `ObligationAgreementRepository.fs`. All take `NpgsqlTransaction` as first arg per plan. Build succeeds. |
| 5 | `ObligationAgreementService` compiles and provides: create, getById, list, update, deactivate | Yes | All five public functions present in `ObligationAgreementService.fs`. Build succeeds (316/316 tests pass). |
| 6 | Service create validates account FKs exist and are active before insert | Yes | `create` calls `validateAccountReferences` (line 62) which uses `lookupAccount` to check existence and `is_active`. Tests FT-OA-009 (nonexistent source), FT-OA-010 (inactive source), FT-OA-011 (nonexistent dest) all pass. |
| 7 | Service update validates agreement exists and account FKs are valid before update | Yes | `update` calls `findById` (line 111) to verify existence, then `validateAccountReferences` (line 117). Tests FT-OA-020 (nonexistent) and FT-OA-024 (inactive source on update) both pass. |
| 8 | Service deactivate checks for active obligation instances and refuses if any exist | Yes | `deactivate` calls `hasActiveInstances` (line 147) and returns error if true. Test FT-OA-028 inserts an active instance and confirms the error message contains "active obligation instances". |
| 9 | All service functions return `Result<T, string list>` (except getById -> option, list -> list) | Yes | `create`: `Result<ObligationAgreement, string list>` (line 52). `getById`: `ObligationAgreement option` (line 77). `list`: `ObligationAgreement list` (line 89). `update`: `Result<ObligationAgreement, string list>` (line 101). `deactivate`: `Result<ObligationAgreement, string list>` (line 136). |
| 10 | All service functions log via `Log` module on entry and on error | **No** | `create`, `update`, and `deactivate` log on entry (Log.info) and on error (Log.warn / Log.errorExn). However, `getById` and `list` do NOT log on entry -- they only log on exception. The criterion says "all service functions" log on entry and on error. |
| 11 | `dotnet build` succeeds for the full solution | Yes | Build succeeded with 0 warnings, 0 errors. 316/316 tests pass. |
| 12 | Integration tests cover all CRUD paths and validation edge cases | Yes | 28 tests covering: create happy path (2), create pure validation (6 including Theory variants), create DB validation (3), get by ID (2), list (5), update happy path (1), update errors (5), deactivate (3). All pass. |
| 13 | No changes to existing ledger-track files | Yes | `git diff --name-only HEAD` shows only: `Ops.fs` (domain, expected), `TestHelpers.fs` (test infra), two `.fsproj` files (compile order). No ledger-specific files (Ledger.fs, JournalEntryRepository.fs, JournalEntryService.fs, FiscalPeriodRepository.fs, etc.) were touched. |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-OA-001 | Create agreement with all fields provided | Yes | Yes |
| @FT-OA-002 | Create agreement with only required fields | Yes | Yes |
| @FT-OA-003 | Create with empty name is rejected | Yes | Yes |
| @FT-OA-004 | Create with name exceeding 100 characters is rejected | Yes | Yes |
| @FT-OA-005 | Create with counterparty exceeding 100 characters is rejected | Yes | Yes |
| @FT-OA-006 | Create with non-positive amount is rejected (Theory: 0.00, -50.00) | Yes | Yes |
| @FT-OA-007 | Create with expected day outside 1-31 is rejected (Theory: 0, 32, -1) | Yes | Yes |
| @FT-OA-008 | Create with multiple validation errors collects all errors | Yes | Yes |
| @FT-OA-009 | Create with nonexistent source account is rejected | Yes | Yes |
| @FT-OA-010 | Create with inactive source account is rejected | Yes | Yes |
| @FT-OA-011 | Create with nonexistent dest account is rejected | Yes | Yes |
| @FT-OA-012 | Get agreement by ID returns the agreement | Yes | Yes |
| @FT-OA-013 | Get agreement by nonexistent ID returns none | Yes | Yes |
| @FT-OA-014 | List with default filter returns only active agreements | Yes | Yes |
| @FT-OA-015 | List filtered by obligation type | Yes | Yes |
| @FT-OA-016 | List filtered by cadence | Yes | Yes |
| @FT-OA-017 | List with no filter returns all including inactive | Yes | Yes |
| @FT-OA-018 | List returns empty when no agreements match | Yes | Yes |
| @FT-OA-019 | Update agreement with all fields | Yes | Yes |
| @FT-OA-020 | Update nonexistent agreement is rejected | Yes | Yes |
| @FT-OA-021 | Update with empty name is rejected | Yes | Yes |
| @FT-OA-022 | Update with non-positive amount is rejected | Yes | Yes |
| @FT-OA-023 | Update with expected day outside 1-31 is rejected | Yes | Yes |
| @FT-OA-024 | Update with inactive source account is rejected | Yes | Yes |
| @FT-OA-025 | Reactivate a previously deactivated agreement | Yes | Yes |
| @FT-OA-026 | Deactivate an active agreement | Yes | Yes |
| @FT-OA-027 | Deactivate a nonexistent agreement is rejected | Yes | Yes |
| @FT-OA-028 | Deactivate blocked by active obligation instances | Yes | Yes |

## Fabrication Check

- No fabricated citations. All file paths and line numbers verified against actual repo state.
- No circular evidence. Test results come from `dotnet test` run, not from Builder/Reviewer claims.
- No omitted failures. 316/316 tests pass including all 28 OA tests.
- GherkinId traits on every test match the feature file tags exactly.
- Theory tests (FT-OA-006, FT-OA-007) cover all example rows from the Gherkin Scenario Outlines.

## Finding: AC #10 Partial Gap

Acceptance criterion #10 states: "All service functions log via Log module on entry and on error."

`getById` (ObligationAgreementService.fs line 77) and `list` (line 89) do not log on entry. They only log via `Log.errorExn` in the exception handler. The three mutating functions (`create`, `update`, `deactivate`) all log properly on entry and on error.

This is a minor gap -- read-only functions are less critical to log on entry -- but the criterion as written says "all."

## Verdict

**CONDITIONAL**

28/28 Gherkin scenarios have passing tests with correct GherkinId traits. 12/13 acceptance criteria fully verified. The single gap is AC #10 where `getById` and `list` lack entry-level logging. The fix is trivial (two `Log.info` lines), but the Governor does not modify code. Everything else is solid -- the evidence chain is clean, tests are real integration tests hitting the database, and no ledger-track files were touched.
