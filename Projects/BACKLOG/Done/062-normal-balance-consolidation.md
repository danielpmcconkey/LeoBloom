# 062 — Consolidate Normal Balance Resolution Logic

**Epic:** Audit Remediation (2026-04-07)
**Depends On:** None
**Status:** Not started
**Priority:** High
**Source:** DRY Auditor Finding 15, GAAP Assessment 2026-04-07

---

## Problem Statement

The normal balance formula — "debit-normal: debits - credits;
credit-normal: credits - debits" — is implemented independently in 7+
locations across the codebase. This is the single most dangerous
duplication: if the accounting model changes (e.g., contra accounts), a
missed update to one location produces silently incorrect financial figures.

## Affected Locations

1. `LeoBloom.Ledger/TrialBalanceRepository.fs`
2. `LeoBloom.Ledger/IncomeStatementRepository.fs`
3. `LeoBloom.Ledger/BalanceSheetRepository.fs`
4. `LeoBloom.Ledger/SubtreePLRepository.fs`
5. `LeoBloom.Ledger/AccountBalanceRepository.fs`
6. `LeoBloom.Reporting/ScheduleERepository.fs`
7. `LeoBloom.Reporting/GeneralLedgerReportService.fs`

## What Ships

1. A pure function in `LeoBloom.Domain/Ledger.fs`:
   `let resolveBalance (normalBalance: NormalBalance) (debits: decimal) (credits: decimal) : decimal`
2. All 7 locations updated to call the shared function
3. Existing tests continue to pass — no behavioral change

## Acceptance Criteria

- AC-1: `resolveBalance` exists in the Domain module
- AC-2: No repository or service contains inline normal-balance arithmetic
- AC-3: All existing tests pass with zero changes to test code

## Scope Boundary

This is a pure refactor. No new tests, no behavioral changes, no new
features. The function goes in Domain because it's a core accounting rule,
not infrastructure.
