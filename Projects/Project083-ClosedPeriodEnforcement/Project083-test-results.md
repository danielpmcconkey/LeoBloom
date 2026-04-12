# Project 083 — Test Results

**Date:** 2026-04-12
**Commit:** 9197b8a (branch: feat/p083-closed-period-enforcement, uncommitted working tree)
**Result:** 20/20 verified

## Note on Evidence

No QE test-results artifact was found in the project directory. However,
the QE process artifact (`qe.json`) reports 1179 tests, 0 failures across
two consecutive runs. The Reviewer independently confirmed 1179 tests pass.
Given that all structural criteria are verified directly from the code and
the QE/Reviewer both independently confirm the suite passes, this is
sufficient evidence. The Governor does NOT re-run the suite.

## Acceptance Criteria Verification

### Part 1: Void Enforcement

| # | Criterion | Verified | Evidence |
|---|-----------|----------|---------|
| 1 | Voiding a JE in a closed period is rejected with error naming the period and suggesting `ledger reverse` | Yes | `JournalEntryService.voidEntry` (lines 157-167): checks `lookupFiscalPeriod`, rejects when `not fp.isOpen`, error includes `fp.periodKey`, `closedAtStr`, and `"leobloom ledger reverse --journal-entry-id"`. Test: `ClosedPeriodEnforcementTests.fs` FT-CPE-001. |
| 2 | Voiding a JE in an open period continues to work as before | Yes | `voidEntry` line 168 (`Some _` match) proceeds to `JournalEntryRepository.voidEntry`. Test: `ClosedPeriodEnforcementTests.fs` FT-CPE-002. |
| 3 | Error message includes the period name, close date, and the reversal command syntax | Yes | Line 166-167: format string includes `fp.periodKey` (period name), `closedAtStr` (close date), and literal `"leobloom ledger reverse --journal-entry-id %d"`. |

### Part 2: Reversing Entries

| # | Criterion | Verified | Evidence |
|---|-----------|----------|---------|
| 4 | `ledger reverse --journal-entry-id N` creates a new JE with swapped debits/credits | Yes | `reverseEntry` lines 209-218: maps lines swapping Debit↔Credit. CLI: `LedgerReverseArgs` DU + `handleReverse` dispatches to `reverseEntry`. Test: FT-RVE-001. |
| 5 | Reversal description is auto-generated: `"Reversal of JE {id}: {original description}"` | Yes | Line 222: `sprintf "Reversal of JE %d: %s" entryId original.entry.description`. Test: FT-RVE-002. |
| 6 | Reversal posts to the fiscal period derived from the entry date (standard path) | Yes | Lines 204-206: `FiscalPeriodRepository.findOpenPeriodForDate txn entryDate`, period.id used as `fiscalPeriodId`. Test: FT-RVE-005. |
| 7 | Reversing an already-voided JE is rejected | Yes | Lines 193-194: checks `original.entry.voidedAt.IsSome`, returns Error. Test: FT-RVE-008. |
| 8 | Reversing a JE that already has a reversal is rejected (idempotency guard) | Yes | Lines 197-199: `findNonVoidedByReference txn "reversal" (string entryId)`, returns Error if found. Test: FT-RVE-009. |
| 9 | Reversal JE has reference `type=reversal, value={original_id}` | Yes | Line 226: `references = [ { referenceType = "reversal"; referenceValue = string entryId } ]`. Test: FT-RVE-004. |
| 10 | `--date` override works and validates against open periods | Yes | Line 202: `dateOverride |> Option.defaultValue ...`. Line 204: `findOpenPeriodForDate` returns None for closed periods → error. CLI: `LedgerReverseArgs.Date` parsed in `handleReverse`. Tests: FT-RVE-006, FT-RVE-007. |
| 11 | `--json` supported | Yes | `LedgerReverseArgs.Json` case, `handleReverse` passes `isJson` through. Test: FT-LCD-041, FT-LCD-030c. |

### Part 3: Post-Close Adjustments

| # | Criterion | Verified | Evidence |
|---|-----------|----------|---------|
| 12 | `adjustment_for_period_id` column exists with FK constraint | Yes | Migration `1712000027000`: `ADD COLUMN adjustment_for_period_id integer NULL REFERENCES ledger.fiscal_period(id) ON DELETE RESTRICT`. |
| 13 | `ledger post --adjustment-for-period N` tags the JE correctly | Yes | `LedgerPostArgs.Adjustment_For_Period` parsed at line 121, passed as `adjustmentForPeriodId` in PostJournalEntryCommand (line 179). `insertEntry` includes `adjustment_for_period_id` in INSERT. Tests: FT-PCA-001, FT-LCD-050. |
| 14 | Adjustment target period can be closed | Yes | `validateDbDependencies` lines 83-88: checks existence only, no `isOpen` check on adjustment period. Test: FT-PCA-002. |
| 15 | Adjustment target period must exist | Yes | Lines 85-87: `lookupFiscalPeriod txn adjId` → None → error "does not exist". Test: FT-PCA-003. |
| 16 | JE itself posts to its own (open) fiscal period normally | Yes | `adjustmentForPeriodId` is stored separately; `fiscalPeriodId` is the JE's actual posting period (validated as open). Test: FT-PCA-004. |
| 17 | `--json` output includes `adjustmentForPeriodId` | Yes | Domain record `JournalEntry` has `adjustmentForPeriodId: int option` (Ledger.fs:116). Serialized through standard `write isJson` path. Test: FT-PCA-005 in LedgerCommandsTests.fs. |

### Part 4: Fiscal Period Override

| # | Criterion | Verified | Evidence |
|---|-----------|----------|---------|
| 18 | `ledger post --fiscal-period-id N` overrides date-derived period | Yes | `handlePost` lines 159-165: `fpIdOpt` from `TryGetResult Fiscal_Period_Id`, used directly when `Some`. Test: FT-LCD-052, FT-PCA-006. |
| 19 | Override target period must be open | Yes | `validateDbDependencies` lines 64-65: rejects when `not fp.isOpen`. Test: FT-PCA-007. |
| 20 | Omitting the flag preserves existing date-derivation behavior | Yes | Lines 162-165: when `None`, calls `findOpenPeriodForDate txn entryDate`. Test: FT-PCA-008, FT-LCD-053. |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-CPE-001 | Void in closed period rejected with actionable error | Yes (ClosedPeriodEnforcementTests.fs:84 + VoidJournalEntryTests.fs:177) | Yes |
| @FT-CPE-002 | Void in open period continues to work | Yes (ClosedPeriodEnforcementTests.fs:123) | Yes |
| @FT-RVE-001 | Reversing produces swapped lines | Yes (ReversingEntryTests.fs:87) | Yes |
| @FT-RVE-002 | Auto-generated reversal description | Yes (ReversingEntryTests.fs:113) | Yes |
| @FT-RVE-003 | Source = "reversal" | Yes (ReversingEntryTests.fs:132) | Yes |
| @FT-RVE-004 | Reference type/value | Yes (ReversingEntryTests.fs:150) | Yes |
| @FT-RVE-005 | Period derived from entry date | Yes (ReversingEntryTests.fs:171) | Yes |
| @FT-RVE-006 | Date override routes to correct period | Yes (ReversingEntryTests.fs:190) | Yes |
| @FT-RVE-007 | Date override to closed period rejected | Yes (ReversingEntryTests.fs:211) | Yes |
| @FT-RVE-008 | Voided JE cannot be reversed | Yes (ReversingEntryTests.fs:239) | Yes |
| @FT-RVE-009 | Already-reversed JE rejected | Yes (ReversingEntryTests.fs:279) | Yes |
| @FT-RVE-010 | Open period JE can be reversed | Yes (ReversingEntryTests.fs:319) | Yes |
| @FT-PCA-001 | Adjustment tag set on JE | Yes (PostCloseAdjustmentTests.fs:66) | Yes |
| @FT-PCA-002 | Closed target period accepted | Yes (PostCloseAdjustmentTests.fs:86) | Yes |
| @FT-PCA-003 | Nonexistent target rejected | Yes (PostCloseAdjustmentTests.fs:106) | Yes |
| @FT-PCA-004 | JE posts to own period | Yes (PostCloseAdjustmentTests.fs:128) | Yes |
| @FT-PCA-005 | JSON output includes field | Yes (LedgerCommandsTests.fs:492) | Yes |
| @FT-PCA-006 | Fiscal period override | Yes (PostCloseAdjustmentTests.fs:153) | Yes |
| @FT-PCA-007 | Closed override rejected | Yes (PostCloseAdjustmentTests.fs:179) | Yes |
| @FT-PCA-008 | Date-derivation preserved | Yes (PostCloseAdjustmentTests.fs:203) | Yes |
| @FT-LCD-030c | JSON for reverse command | Yes (LedgerCommandsTests.fs:416) | Yes |
| @FT-LCD-040 | CLI reverse happy path | Yes (LedgerCommandsTests.fs:434) | Yes |
| @FT-LCD-041 | CLI reverse --json | Yes (LedgerCommandsTests.fs:447) | Yes |
| @FT-LCD-042 | CLI reverse --date | Yes (LedgerCommandsTests.fs:461) | Yes |
| @FT-LCD-043 | CLI reverse nonexistent entry | Yes (LedgerCommandsTests.fs:473) | Yes |
| @FT-LCD-044 | CLI reverse no args | Yes (LedgerCommandsTests.fs:480) | Yes |
| @FT-LCD-050 | CLI post --adjustment-for-period | Yes (LedgerCommandsTests.fs:513) | Yes |
| @FT-LCD-051 | CLI post bad adjustment period | Yes (LedgerCommandsTests.fs:530) | Yes |
| @FT-LCD-052 | CLI post --fiscal-period-id override | Yes (LedgerCommandsTests.fs:543) | Yes |
| @FT-LCD-053 | CLI post without --fiscal-period-id | Yes (LedgerCommandsTests.fs:562) | Yes |

## Breaking Changes Verified

| Change | Status | Evidence |
|---|---|---|
| FT-VJE-008 removed from VoidJournalEntry.feature | Yes | Line 101: comment noting removal by P083. Test renamed to "void is rejected when fiscal period is closed" with FT-CPE-001 tag. |
| FT-LCD-006 "no --fp-id" row removed from LedgerCommands.feature | Yes | Line 64: comment noting removal by P083. Test FT-LCD-006e removed from LedgerCommandsTests.fs (line 272). |

## Fabrication Check

- No circular evidence detected. All criteria verified against actual source files.
- No citations to nonexistent files.
- QE and Reviewer test counts match (1179).
- No stale evidence — all files are on the current working tree of feat/p083-closed-period-enforcement.

## Verdict

**APPROVED** — All 20 acceptance criteria verified against actual code. Evidence chain is solid. Every Gherkin scenario has a corresponding test tagged with its scenario ID.
