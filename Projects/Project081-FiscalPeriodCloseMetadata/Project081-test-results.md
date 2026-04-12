# Project 081 — Test Results

**Date:** 2026-04-12
**Commit:** 1b1b2fa68d87d1a7effdfd89d14cff091c9d9af8
**Result:** 13/13 acceptance criteria verified

## Acceptance Criteria Verification

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| 1 | Migration adds `closed_at`, `closed_by`, `reopened_count` to `ledger.fiscal_period` | Yes | Confirmed in `1712000026000_FiscalPeriodCloseMetadata.sql` lines 5-8: ALTER TABLE adds all three columns with correct types and defaults |
| 2 | Migration creates `ledger.fiscal_period_audit` table with FK constraint | Yes | Confirmed in migration lines 14-21: CREATE TABLE with `REFERENCES ledger.fiscal_period(id) ON DELETE RESTRICT` |
| 3 | Backfill: currently-closed periods get `closed_at = now()`, `closed_by = 'migration'` | Yes | Confirmed in migration lines 10-12: UPDATE WHERE `is_open = false` sets both fields |
| 4 | Backfill: all periods get `reopened_count = 0` | Yes | Column DDL has `DEFAULT 0` (line 8); existing open periods get 0 by default, closed periods also get 0 since only `closed_at`/`closed_by` are set in the UPDATE |
| 5 | `fiscal-period close` sets `closed_at`, `closed_by`, writes audit row | Yes | `FiscalPeriodRepository.closePeriod` sets `closed_at = now()`, `closed_by = @actor`; `FiscalPeriodService.closePeriod` writes audit via `FiscalPeriodAuditRepository.insert`; test CFM-001 + CFM-002 cover this |
| 6 | Closing an already-closed period is idempotent, returns clear message | Yes | `FiscalPeriodService.closePeriod` returns `Ok existing` when `not existing.isOpen` with no DB update and no audit row; test CFM-003 verifies audit count stays at 1 |
| 7 | `fiscal-period reopen` clears `closed_at`, increments `reopened_count`, writes audit row | Yes | `FiscalPeriodRepository.reopenPeriod` sets `closed_at = NULL, closed_by = NULL, reopened_count = reopened_count + 1`; service writes audit row with reason as note; tests CFM-004 + CFM-005 cover this |
| 8 | `fiscal-period audit --id N` lists all close/reopen events | Yes | `PeriodCommands.handleAudit` dispatches to `FiscalPeriodService.listAudit`, which calls `FiscalPeriodAuditRepository.listByPeriod` ordered by `occurred_at ASC`; tests CFM-006, CFM-007 verify |
| 9 | `fiscal-period list` shows `closed_at`, `reopened_count` columns | Yes | `OutputFormatter.fs` line 312 includes "Closed At" and "Reopen#" headers; test CFM-009 asserts both column headers present |
| 10 | All existing fiscal period tests continue to pass | Yes | QE artifact reports 1134 passed, 0 failed, 0 skipped; Reviewer independently confirmed 1134/1134 |
| 11 | `FiscalPeriod` domain type includes new fields | Yes | `Ledger.fs` lines 89-98: `closedAt: DateTimeOffset option`, `closedBy: string option`, `reopenedCount: int` present; `FiscalPeriodAuditEntry` type at lines 100-106 |
| 12 | `--json` supported on all new/modified commands | Yes | `PeriodCloseArgs`, `PeriodReopenArgs`, `PeriodAuditArgs`, `PeriodListArgs` all have `Json` case; handlers check for it; tests CFM-010–013 parse output as valid JSON |
| 13 | Exit codes follow `ExitCodes` convention | Yes | `PeriodCommands.dispatch` returns handler results which use `write`/`writeAuditList`/`writePeriodList` (all return 0 on success); CFM-007 asserts code 0, CFM-008 asserts code 1 for nonexistent period |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-CFM-001 | Close sets closed_at and closed_by on the period | Yes | Yes |
| @FT-CFM-002 | Close writes an audit row with actor and note | Yes | Yes |
| @FT-CFM-003 | Closing an already-closed period is idempotent | Yes | Yes |
| @FT-CFM-004 | Reopen clears close metadata and increments reopened_count | Yes | Yes |
| @FT-CFM-005 | Reopen writes an audit row with actor and reason as note | Yes | Yes |
| @FT-CFM-006 | Full close-reopen-close cycle produces 3-entry audit trail | Yes | Yes |
| @FT-CFM-007 | fiscal-period audit lists all close/reopen events | Yes | Yes |
| @FT-CFM-008 | fiscal-period audit on nonexistent period exits code 1 | Yes | Yes |
| @FT-CFM-009 | fiscal-period list shows closed_at and reopened_count columns | Yes | Yes |
| @FT-CFM-010 | --json on period close (idempotent) produces valid JSON | Yes | Yes |
| @FT-CFM-011 | --json on period reopen produces valid JSON | Yes | Yes |
| @FT-CFM-012 | --json on period audit produces valid JSON | Yes | Yes |
| @FT-CFM-013 | --json on period list produces valid JSON | Yes | Yes |

## Fabrication Check

- No citations to nonexistent files detected.
- `setIsOpen` fully eliminated — grep across `Src/` returns zero results.
- `FiscalPeriodAuditRepository.fs` confirmed in `.fsproj` (line 11).
- `FiscalPeriodCloseMetadataTests.fs` confirmed in `.fsproj` (line 53).
- QE artifact claims 1134/1134 passing; Reviewer independently ran suite and confirmed same count.
- All `readPeriod` call sites in `FiscalPeriodRepository` use consistent 9-column ordinal mapping.

## Verdict

**APPROVED** — All 13 acceptance criteria verified against actual repo state. All 13 Gherkin scenarios have corresponding tests with passing results. Evidence chain is solid — no fabrication, no circular reasoning, no stale evidence detected.
