# Project 040 — Test Results

**Date:** 2026-04-05
**Commit:** 64dbd91 (main) + uncommitted P040 changes on working tree
**Result:** CONDITIONAL — 1 P040 test failure found

## Test Run

```
dotnet test (full suite)
Total: 608 | Passed: 602 | Failed: 6
```

### Failure Breakdown

| Test | P040? | Notes |
|---|---|---|
| PostObligationToLedgerTests (5 tests) | No | Pre-existing failures, unrelated to P040 |
| ReportingServiceTests.GeneralLedger generate for valid account with activity returns entries | **Yes** | Hardcodes account name "Rental Checking" for code 1110, but seed data has "Bank A -- Operating" (likely renamed by P052). Assertion at line 303 fails. |

The GL service test failure is a **test data assumption bug**. The test at `ReportingServiceTests.fs:303` asserts `Assert.Equal("Rental Checking", report.accountName)` but account 1110's name in the database is "Bank A -- Operating". The report service code itself is correct -- it returns whatever name the DB has. The test just has a stale expectation.

## Acceptance Criteria Verification

Since no formal acceptance criteria document was provided, I am verifying against the Gherkin scenarios in `Specs/CLI/ReportCommands.feature` and the implied deliverables from the project brief/plan.

### Structural Deliverables

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| S1 | LeoBloom.Reporting project exists | Yes | `Src/LeoBloom.Reporting/` with 9 source files |
| S2 | ReportingTypes defines Schedule E, GL, Cash Receipts, Cash Disbursements types | Yes | `ReportingTypes.fs` — all four report types with proper domain modeling |
| S3 | ScheduleEMapping provides IRS line-to-COA mapping | Yes | `ScheduleEMapping.fs` — lines 3,9,12,14,16,18,19 with sub-detail for line 19 |
| S4 | ScheduleERepository queries non-voided balances by year | Yes | `ScheduleERepository.fs` — `voided_at IS NULL` in JOIN, respects normal_balance |
| S5 | GeneralLedgerRepository queries entries with voided exclusion | Yes | `GeneralLedgerRepository.fs` — `voided_at IS NULL` filter |
| S6 | CashFlowRepository uses `account_subtype = 'Cash'` (not hardcoded codes) | Yes | `CashFlowRepository.fs:47,97` — `WHERE cash_acct.account_subtype = 'Cash'` |
| S7 | CLI ReportCommands module with Argu DU definitions | Yes | `ReportCommands.fs` — all four subcommands with dispatch |
| S8 | Program.fs integrates report command group | Yes | `Program.fs:14,38-43` — Report DU case, --json rejection, dispatch |
| S9 | No depreciation config (premature feature removed) | Yes | Zero matches for depreciation config in Reporting project |
| S10 | O(n^2) list append replaced with cons+reverse | Yes | All 4 repository files use `:: results` then `List.rev results` |

### Reviewer Fixes Verification

| # | Fix | Verified | Notes |
|---|-----|----------|-------|
| R1 | Voided entries SQL bug in ScheduleERepository | Yes | `voided_at IS NULL` in JOIN condition (line 49) |
| R2 | GL running balance respects normal_balance direction | Yes | `GeneralLedgerReportService.fs:47-50` — credit vs debit-normal branching |
| R3 | Depreciation config removed | Yes | No depreciation config anywhere in Reporting project |
| R4 | O(n^2) list append to cons+reverse | Yes | All repositories use cons+reverse pattern |
| R5 | FT-RPT-051 deleted | Yes | Not present in specs or test files |
| R6 | CashFlowRepository hardcoded codes replaced with account_subtype | Yes | `WHERE cash_acct.account_subtype = 'Cash'` in both queries |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-RPT-001 | Top-level --help includes report | Yes (ReportCommandsTests:72) | Yes |
| @FT-RPT-002 | Report --help prints subcommands | Yes (ReportCommandsTests:80) | Yes |
| @FT-RPT-003 | Report with no subcommand prints error | Yes (ReportCommandsTests:89) | Yes |
| @FT-RPT-010 | Schedule E valid year produces output | Yes (ReportCommandsTests:100) | Yes |
| @FT-RPT-011 | Schedule E Other line 19 sub-detail | Yes (ReportCommandsTests:111) | Yes |
| @FT-RPT-012 | Schedule E depreciation from account 5190 | Yes (ReportCommandsTests:121) | Yes |
| @FT-RPT-013 | Schedule E no --year flag error | Yes (ReportCommandsTests:130) | Yes |
| @FT-RPT-014 | Schedule E non-numeric year error | Yes (ReportCommandsTests:138) | Yes |
| @FT-RPT-015 | Schedule E service validation error | Yes (ReportCommandsTests:150) | Yes |
| @FT-RPT-016 | Schedule E rejects --json | Yes (ReportCommandsTests:157) | Yes |
| @FT-RPT-020 | GL valid account and date range | Yes (ReportCommandsTests:169) | Yes |
| @FT-RPT-021 | GL missing required args (3 examples) | Yes (ReportCommandsTests:191,199,207) | Yes |
| @FT-RPT-022 | GL invalid date format | Yes (ReportCommandsTests:215) | Yes |
| @FT-RPT-023 | GL nonexistent account error | Yes (ReportCommandsTests:222) | Yes |
| @FT-RPT-024 | GL rejects --json | Yes (ReportCommandsTests:229) | Yes |
| @FT-RPT-030 | Cash receipts valid date range | Yes (ReportCommandsTests:241) | Yes |
| @FT-RPT-031 | Cash receipts missing args (2 examples) | Yes (ReportCommandsTests:249,257) | Yes |
| @FT-RPT-032 | Cash receipts invalid date format | Yes (ReportCommandsTests:265) | Yes |
| @FT-RPT-033 | Cash receipts rejects --json | Yes (ReportCommandsTests:271) | Yes |
| @FT-RPT-040 | Cash disbursements valid date range | Yes (ReportCommandsTests:284) | Yes |
| @FT-RPT-041 | Cash disbursements missing args (2 examples) | Yes (ReportCommandsTests:292,300) | Yes |
| @FT-RPT-042 | Cash disbursements invalid date format | Yes (ReportCommandsTests:308) | Yes |
| @FT-RPT-043 | Cash disbursements rejects --json | Yes (ReportCommandsTests:315) | Yes |
| @FT-RPT-050 | Subcommand --help (4 examples) | Yes (ReportCommandsTests:327,334,341,348) | Yes |

**All 24 Gherkin scenarios have corresponding tests. All CLI-level tests pass.**

## Service-Level Test Coverage (ReportingServiceTests.fs)

| Test | Passes |
|---|---|
| scheduleELineMappings covers all expected IRS lines | Yes |
| allMappedAccountCodes contains all revenue and expense codes | Yes |
| line19SubDetail maps expected codes to descriptions | Yes |
| ScheduleE generate with valid year returns correct report | Yes |
| ScheduleE generate with no activity returns zeroed report | Yes |
| ScheduleE generate line 19 sub-detail multiple categories | Yes |
| ScheduleE generate rejects negative year | Yes |
| ScheduleE generate rejects year before 1900 | Yes |
| ScheduleE generate rejects year after 2100 | Yes |
| ScheduleE generate excludes voided entries | Yes |
| ScheduleE line items ordered by line number | Yes |
| **GeneralLedger generate for valid account with activity** | **No** |
| GeneralLedger generate with no activity returns empty | Yes |
| GeneralLedger generate rejects nonexistent account | Yes |
| GeneralLedger generate rejects from after to date | Yes |
| GeneralLedger generate rejects empty account code | Yes |
| GeneralLedger excludes voided entries | Yes |
| GeneralLedger entries ordered by date then entry ID | Yes |
| CashReceipts returns debits to cash accounts | Yes |
| CashDisbursements returns credits to cash accounts | Yes |
| CashReceipts includes counterparty account name | Yes |
| CashReceipts rejects from after to | Yes |
| CashDisbursements rejects from after to | Yes |
| CashReceipts with no activity returns empty | Yes |
| CashDisbursements with no activity returns empty | Yes |
| CashReceipts excludes voided entries | Yes |

**25/26 service-level tests pass. 1 failure is a stale test assertion (see below).**

## Issue Found

**ReportingServiceTests.fs line 303** — The test asserts `Assert.Equal("Rental Checking", report.accountName)` for account code 1110. The database seed data names this account "Bank A -- Operating" (likely updated by P052 account subtypes migration). The service code is correct; the test expectation is stale. Fix: change `"Rental Checking"` to `"Bank A \u2014 Operating"` on line 303.

## Verdict

**CONDITIONAL**

All Gherkin scenarios are covered by tests and pass. All 6 reviewer fixes are verified in the code. The implementation is solid. One service-level test (`GeneralLedger generate for valid account with activity`) fails due to a stale account name assertion that needs a one-line fix. This does not indicate a code defect -- the report service returns the correct data. The test expectation just needs to match the current seed data.

**Required before APPROVED:** Fix the hardcoded account name at `ReportingServiceTests.fs:303` from `"Rental Checking"` to `"Bank A \u2014 Operating"`.
