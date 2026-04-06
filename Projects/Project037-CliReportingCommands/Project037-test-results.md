# Project 037 -- CLI Reporting Commands -- Test Results

**Date:** 2026-04-06
**Commit:** 6dbfeb5 (HEAD, main)
**Result:** 18/18 acceptance criteria verified

## Build & Test Summary

| Check | Result |
|---|---|
| `dotnet build` | 0 warnings, 0 errors |
| `dotnet test` (full suite) | 725 passed, 0 failed, 0 skipped |
| `dotnet test` (ReportCommandsTests only) | 68 passed, 0 failed, 0 skipped |

## Acceptance Criteria Verification

| # | Criterion | Verified | Evidence |
|---|-----------|----------|-------|
| 1 | `leobloom report trial-balance --period 7` produces human output with debit/credit columns, subtotals, grand totals, balanced status (exit 0) | Yes | Ran CLI with `--period 2026-01` (period 7 does not exist in dev DB; used valid period 26500/2026-01). Output contains `Trial Balance`, `Grand Total`, `BALANCED`. Exit 0. formatTrialBalance in OutputFormatter.fs (lines 157-176) has separate debit/credit columns, group subtotals, grand totals, balanced/unbalanced status. |
| 2 | `trial-balance --period 2026-01` (period key) also works | Yes | Ran CLI: `report trial-balance --period 2026-01`. Exit 0, human output produced. |
| 3 | `trial-balance --period 7 --json` produces valid JSON (exit 0) | Yes | Ran CLI: `report trial-balance --period 26500 --json`. Valid JSON output with `fiscalPeriodId`, `periodKey`, `groups`, `isBalanced` fields. Exit 0. |
| 4 | `balance-sheet --as-of 2026-03-31` produces human output with A=L+E, retained earnings, balanced status (exit 0) | Yes | Ran CLI. Output contains `ASSETS`, `LIABILITIES`, `EQUITY`, `Retained Earnings`, `Total Liabilities + Equity`, `BALANCED`. Exit 0. formatBalanceSheet (lines 180-219) implements A=L+E structure. |
| 5 | `balance-sheet --as-of 2026-03-31 --json` produces valid JSON (exit 0) | Yes | Ran CLI. Valid JSON with `assets`, `liabilities`, `equity`, `retainedEarnings`, `isBalanced` fields. Exit 0. |
| 6 | `income-statement --period 7` produces human output with revenue/expense sections, totals, net income (exit 0) | Yes | Ran CLI with period 26500. Output contains `REVENUE`, `EXPENSE`, `Net Income`. Exit 0. formatIncomeStatement (lines 233-242) has separate sections and net income. |
| 7 | `income-statement --period 7 --json` produces valid JSON (exit 0) | Yes | Ran CLI with period 26500 `--json`. Valid JSON with `revenue`, `expenses`, `netIncome` fields. Exit 0. |
| 8 | `pnl-subtree --account 5000 --period 7` produces human output with root account in header, income statement format (exit 0) | Yes | Ran CLI: `report pnl-subtree --account 80f3D1 --period 2026-01`. Output contains `P&L Subtree -- 80f3D1 DebitAcct`, `REVENUE`, `EXPENSE`, `Net Income`. Exit 0. formatSubtreePL (lines 246-255) shows root account code and name. |
| 9 | `pnl-subtree --account 5000 --period 7 --json` produces valid JSON (exit 0) | Yes | Ran CLI with `--json`. Valid JSON with `rootAccountCode`, `rootAccountName`, `revenue`, `expenses`, `netIncome`. Exit 0. |
| 10 | `account-balance --account 1110` defaults --as-of to today, shows normal balance type (exit 0) | Yes | Ran CLI: `report account-balance --account 80f3D1`. Output shows `As of: 2026-04-06` (today), `Normal Balance: Debit`, `Balance: 1000.00`. Exit 0. Code in handleAccountBalance (line 234) uses `DateOnly.FromDateTime(DateTime.Today)` as default. |
| 11 | `account-balance --account 1110 --as-of 2026-03-31 --json` produces valid JSON (exit 0) | Yes | Ran CLI. Valid JSON with `accountCode`, `normalBalance`, `balance`, `asOfDate`. Exit 0. |
| 12 | Missing required args prints usage to stderr, exits 2 | Yes | Tested `report trial-balance` (no --period): stderr shows usage, exit 2. Tested `report balance-sheet` (no --as-of): exit 2. Tested `report account-balance` (no --account): exit 2. |
| 13 | Invalid date values produce error to stderr, exit 1 | Yes | Tested `report balance-sheet --as-of not-a-date`: stderr shows `Invalid date format`, exit 1. Tested `report account-balance --account 80f3D1 --as-of not-a-date`: same behavior. |
| 14 | Invalid period (nonexistent) produces error to stderr, exit 1 | Yes | Tested `report trial-balance --period 999999`: stderr shows `Fiscal period with id 999999 does not exist`, exit 1. |
| 15 | Invalid account code (nonexistent) produces error to stderr, exit 1 | Yes | Tested `report account-balance --account 9999`: stderr shows `Account with code '9999' does not exist`, exit 1. |
| 16 | All 9 report subcommands appear in `leobloom report --help` | Yes | Ran `report --help`. Output lists: schedule-e, general-ledger, cash-receipts, cash-disbursements, trial-balance, balance-sheet, income-statement, pnl-subtree, account-balance. All 9 present. |
| 17 | Solution builds with zero warnings | Yes | `dotnet build` output: `0 Warning(s), 0 Error(s)` |
| 18 | All existing tests pass (no regressions) | Yes | 725/725 tests pass. 0 failures. (OverdueDetectionTests and PostObligationToLedgerTests -- flagged as known flaky -- did not fail in this run.) |

## Backward Compatibility of Existing 4 Report Commands

| Command | Verified | Evidence |
|---|---|---|
| `report schedule-e --year 2026` | Yes | Exit 0, formatted output with `Schedule E`, line items, totals. Identical behavior to pre-P037. |
| `report general-ledger --account 80f3D1 --from 2026-01-01 --to 2026-03-31` | Yes | Exit 0, formatted output with `General Ledger`, date/entry/description columns, ending balance. |
| `report cash-receipts --from 2026-01-01 --to 2026-03-31` | Yes | Exit 0, formatted output with `Cash Receipts`, column headers, total. |
| `report cash-disbursements --from 2026-01-01 --to 2026-03-31` | Yes | Exit 0, formatted output with `Cash Disbursements`, column headers, total. |
| Top-level `--json` no longer errors for old commands | Yes | `--json report schedule-e --year 2026` produces human output (exit 0), not an error. Tests FT-RPT-016, FT-RPT-024, FT-RPT-033, FT-RPT-043 cover this. |

## GAAP Formatting Rules Verification

| Rule | Verified | Evidence |
|---|---|---|
| Trial Balance: separate debit/credit columns (not netted) | Yes | formatTrialBalance outputs `Debit` and `Credit` as separate columns (lines 163-169). Values are independent, not netted. |
| Trial Balance: grand totals for both columns independently | Yes | Line 173: `Grand Total` with `grandTotalDebits` and `grandTotalCredits` separately. |
| Trial Balance: balanced/unbalanced status | Yes | Lines 174-175: checks `isBalanced`, displays `BALANCED` or `UNBALANCED`. |
| Balance Sheet: A = L + E structure | Yes | Sections in order: ASSETS, LIABILITIES, EQUITY. Line 216: `Total Liabilities + Equity`. |
| Balance Sheet: retained earnings as separate line | Yes | Line 213: `Retained Earnings` as distinct line within equity, separate from equity account subtotal. |
| Balance Sheet: balanced status | Yes | Lines 217-218: `BALANCED` or `UNBALANCED`. |
| Income Statement: separate revenue/expense sections with totals | Yes | formatIncomeStatementSection renders each section with `Total {sectionName}`. Revenue and expenses are separate. |
| Income Statement: net income at bottom | Yes | Line 241: `Net Income` at bottom. |
| P&L Subtree: root account code and name in header | Yes | Line 248: header includes `rootAccountCode` and `rootAccountName`. |
| P&L Subtree: same format as income statement | Yes | Reuses formatIncomeStatementSection (lines 250-251). |
| Account Balance: displays normal balance type | Yes | Line 260: shows `Debit` or `Credit` for normalBalance. |
| Account Balance: does not suppress negative balances | Yes | Line 265: `sprintf "%M" bal.balance` -- no abs(), no conditional suppression. |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-RPT-100 | Report --help lists all 5 new subcommands | Yes (FT-RPT-100) | Yes |
| @FT-RPT-101 | Each new subcommand prints help | Yes (FT-RPT-101a-e, 5 tests) | Yes |
| @FT-RPT-110 | Trial balance by period ID, human output | Yes (FT-RPT-110) | Yes |
| @FT-RPT-111 | Trial balance by period key, human output | Yes (FT-RPT-111) | Yes |
| @FT-RPT-112 | Trial balance --json, valid JSON | Yes (FT-RPT-112) | Yes |
| @FT-RPT-113 | Trial balance, missing --period, exit 2 | Yes (FT-RPT-113) | Yes |
| @FT-RPT-114 | Trial balance, nonexistent period, exit 1 | Yes (FT-RPT-114) | Yes |
| @FT-RPT-120 | Balance sheet, human output, A=L+E | Yes (FT-RPT-120) | Yes |
| @FT-RPT-121 | Balance sheet --json, valid JSON | Yes (FT-RPT-121) | Yes |
| @FT-RPT-122 | Balance sheet, missing --as-of, exit 2 | Yes (FT-RPT-122) | Yes |
| @FT-RPT-123 | Balance sheet, invalid date, exit 1 | Yes (FT-RPT-123) | Yes |
| @FT-RPT-130 | Income statement by period ID, human output | Yes (FT-RPT-130) | Yes |
| @FT-RPT-131 | Income statement by period key, human output | Yes (FT-RPT-131) | Yes |
| @FT-RPT-132 | Income statement --json, valid JSON | Yes (FT-RPT-132) | Yes |
| @FT-RPT-133 | Income statement, missing --period, exit 2 | Yes (FT-RPT-133) | Yes |
| @FT-RPT-134 | Income statement, nonexistent period, exit 1 | Yes (FT-RPT-134) | Yes |
| @FT-RPT-140 | P&L subtree, human output with root account | Yes (FT-RPT-140) | Yes |
| @FT-RPT-141 | P&L subtree --json, valid JSON | Yes (FT-RPT-141) | Yes |
| @FT-RPT-142 | P&L subtree, missing required args | Yes (FT-RPT-142a, 142b) | Yes |
| @FT-RPT-143 | P&L subtree, nonexistent account, exit 1 | Yes (FT-RPT-143) | Yes |
| @FT-RPT-144 | P&L subtree, nonexistent period, exit 1 | Yes (FT-RPT-144) | Yes |
| @FT-RPT-150 | Account balance, explicit --as-of, human output | Yes (FT-RPT-150) | Yes |
| @FT-RPT-151 | Account balance, defaults --as-of to today | Yes (FT-RPT-151) | Yes |
| @FT-RPT-152 | Account balance --json, valid JSON | Yes (FT-RPT-152) | Yes |
| @FT-RPT-153 | Account balance, missing --account, exit 2 | Yes (FT-RPT-153) | Yes |
| @FT-RPT-154 | Account balance, invalid date, exit 1 | Yes (FT-RPT-154) | Yes |
| @FT-RPT-155 | Account balance, nonexistent account, exit 1 | Yes (FT-RPT-155) | Yes |
| @FT-RPT-160 | --json per-command produces valid JSON (5 commands) | Yes (FT-RPT-160a-e) | Yes |

All 28 Gherkin scenarios have corresponding tests. All tests pass.

## Fabrication Check

- No citations to nonexistent files.
- All CLI commands were executed live against the dev database and produced the documented output.
- Test counts match actual `dotnet test` output (725 total, 68 report-specific).
- The dev database has limited test data (many zero-balance reports), but all code paths were exercised and verified.

## Notes

- The income statement section headers display as "REVENUE" and "EXPENSE" (singular), not "EXPENSES" (plural). This is because the formatter uses `section.sectionName.ToUpper()` and the domain model's sectionName is "expense" / "revenue". The plan's sketch showed "EXPENSES" (plural). This is cosmetic and matches what the service returns -- the formatter is not misrepresenting data.
- Period ID 7 (used in plan examples) does not exist in the dev database. Tests and manual verification used period ID 26500 / key "2026-01" instead. The test file hardcodes 26500 for the same reason.

## Verdict

**APPROVED**

Every acceptance criterion is verified against the actual repo state and live CLI execution. The evidence chain is solid: code was read, tests were run, CLI commands were executed, and output was inspected. No fabrication detected.
