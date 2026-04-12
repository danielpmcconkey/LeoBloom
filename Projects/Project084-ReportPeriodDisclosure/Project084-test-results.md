# Project 084 — Test Results

**Date:** 2026-04-12
**Commit:** e7e153c (uncommitted working tree — P084 changes staged/unstaged)
**Result:** 17/17 verified (10 behavioral + 7 structural)

## Acceptance Criteria Verification

### Behavioral

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| 1 | Income statement, balance sheet (with --period), P&L subtree, and trial balance show period provenance header | Yes | `formatDisclosureHeader` integrated into all 4 formatters at OutputFormatter.fs:200,229,288,307. Gherkin FT-RPD-001 covers all 4 via Scenario Outline. |
| 2 | Header shows period name, date range, open/closed status, close timestamp + actor, and reopened count | Yes | `formatDisclosureHeader` at OutputFormatter.fs:158 renders all fields from `PeriodDisclosure` type. FT-RPD-001, FT-RPD-002, FT-RPD-010 cover these. |
| 3 | Header shows adjustment count and net impact when adjustments exist | Yes | FT-RPD-004 tests this. `formatDisclosureHeader` conditionally shows adjustment summary at OutputFormatter.fs:172-175. |
| 4 | Open period header shows "OPEN as of report generation" status | Yes | FT-RPD-003 covers all 4 reports via Scenario Outline. `isOpen` field drives status text. |
| 5 | Footer lists individual adjustment JEs with ID, date, description, and net amount | Yes | `formatDisclosureFooter` at OutputFormatter.fs:181 renders detail rows. FT-RPD-006 tests this. |
| 6 | --as-originally-closed filters to JEs created at or before closed_at | Yes | `getActivityByPeriodAsOfClose` variants added to all 4 repositories. FT-RPD-011 covers 4 reports via Scenario Outline. FT-RPD-012 confirms pre-close adjustment JEs included. |
| 7 | --as-originally-closed on open period returns clear error | Yes | FT-RPD-014 covers all 4 reports via Scenario Outline. Service validation logic returns `Error` for open periods. |
| 8 | je-lines extract includes adjustment JEs by default | Yes | ExtractRepository.fs:111-112 uses OR clause for `adjustment_for_period_id` when `includeAdjustments=true`. FT-RPD-016 tests this. |
| 9 | je-lines with --exclude-adjustments omits adjustment JEs | Yes | ExtractRepository.fs:113-114 uses only `fiscal_period_id` filter (reviewer fix confirmed — no `IS NULL` over-exclusion). FT-RPD-017 tests this. |
| 10 | JSON output includes period metadata in response envelope | Yes | `PeriodMetadataEnvelope` at ExtractTypes.fs:55 with `JsonPropertyName` attributes. FT-RPD-019 (4 reports), FT-RPD-020 (extract) test this. |

### Structural

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| 1 | `PeriodDisclosure` type exists in Ledger.fs | Yes | Ledger.fs:274 — record type with all specified fields |
| 2 | `PeriodDisclosureRepository.fs` exists and compiles | Yes | File exists at Src/LeoBloom.Ledger/PeriodDisclosureRepository.fs. QE reports 0 build errors. |
| 3 | `PeriodDisclosureRepository.fs` in .fsproj compile order | Yes | LeoBloom.Ledger.fsproj:12 — after FiscalPeriodAuditRepository.fs |
| 4 | All four report types have `disclosure: PeriodDisclosure option` | Yes | Ledger.fs lines 295, 316, 339, 351 — TrialBalanceReport, IncomeStatementReport, BalanceSheetReport, SubtreePLReport |
| 5 | `PeriodMetadataEnvelope` type exists in ExtractTypes.fs | Yes | ExtractTypes.fs:55 with snake_case JSON annotations |
| 6 | `--as-originally-closed` flag on all period-based report commands | Yes | ReportCommands.fs lines 53, 65, 77, 89 — TrialBalance, BalanceSheet, IncomeStatement, PnlSubtree args. ExtractCommands.fs:41 for je-lines. |
| 7 | `--exclude-adjustments` flag on je-lines extract command | Yes | ExtractCommands.fs:40 |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-RPD-001 | Closed-period provenance header (4 reports) | Yes (4 tests) | Yes (QE: 1224/1224) |
| @FT-RPD-002 | Header includes report generation timestamp | Yes | Yes |
| @FT-RPD-003 | Open-period OPEN status (4 reports) | Yes (4 tests) | Yes |
| @FT-RPD-004 | Adjustment count and net impact in header | Yes | Yes |
| @FT-RPD-005 | No adjustment summary when none exist | Yes | Yes |
| @FT-RPD-006 | Footer lists adjustment JE details | Yes | Yes |
| @FT-RPD-007 | No footer when no adjustments | Yes | Yes |
| @FT-RPD-008 | Balance sheet without --period has no disclosure | Yes | Yes |
| @FT-RPD-009 | Balance sheet with --period gets disclosure | Yes | Yes |
| @FT-RPD-010 | Reopened period header shows updated close info | Yes | Yes |
| @FT-RPD-011 | --as-originally-closed excludes post-close JEs (4 reports) | Yes (4 tests) | Yes |
| @FT-RPD-012 | --as-originally-closed includes pre-close adjustment JEs | Yes | Yes |
| @FT-RPD-013 | --as-originally-closed header indicator | Yes | Yes |
| @FT-RPD-014 | --as-originally-closed on open period errors (4 reports) | Yes (4 tests) | Yes |
| @FT-RPD-015 | --as-originally-closed without --period on BS errors | Yes | Yes |
| @FT-RPD-016 | je-lines includes adjustment JEs by default | Yes | Yes |
| @FT-RPD-017 | --exclude-adjustments omits adjustment JEs | Yes | Yes |
| @FT-RPD-018 | je-lines with no adjustments works correctly | Yes | Yes |
| @FT-RPD-019 | JSON output includes disclosure object (4 reports) | Yes (4 tests) | Yes |
| @FT-RPD-020 | je-lines extract JSON includes period metadata | Yes | Yes |
| @FT-RPD-021 | Balance sheet JSON without --period has no disclosure | Yes | Yes |

## QE Artifact

QE's test-results artifact is at `/workspace/Nightshift/artifacts/31/process/qe.json` (process system, not project directory markdown). Outcome: SUCCESS. Suite: 1224/1224 pass, 0 failures, 0 skips. No separate markdown artifact was produced by QE — this is a process gap, not a code gap.

## Fabrication Check

- All file paths cited by Builder confirmed to exist
- Code patterns match claimed changes (grep-verified)
- Reviewer's conditional fix confirmed applied (no `IS NULL` in exclude branch)
- QE's test count (1224/1224) is plausible given 45 new tests added to prior ~1179
- No circular evidence detected — each claim verified against actual file contents

## Verdict

**APPROVED**

All 10 behavioral and 7 structural acceptance criteria verified against actual repo state. All 21 Gherkin scenarios have corresponding tests. QE reports full suite passing. Reviewer's conditional fix confirmed applied. No fabrication detected.
