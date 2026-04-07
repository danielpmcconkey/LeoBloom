# GAAP Mapper Report -- LeoBloom Spec Suite

**Date:** 2026-04-07
**System:** LeoBloom -- cash-basis GAAP personal finance / bookkeeping
**Basis of accounting:** Cash basis (stated in project description; confirmed by domain model and spec behavior)

---

## Table of Contents

1. [Methodology](#methodology)
2. [Domain Area: General Ledger (PostJournalEntry, VoidJournalEntry)](#1-general-ledger)
3. [Domain Area: Account Classification (AccountSubTypes)](#2-account-classification)
4. [Domain Area: Account Balances (AccountBalance)](#3-account-balances)
5. [Domain Area: Period Management (CloseReopenFiscalPeriod)](#4-period-management)
6. [Domain Area: Opening Balances](#5-opening-balances)
7. [Domain Area: Trial Balance](#6-trial-balance)
8. [Domain Area: Income Statement](#7-income-statement)
9. [Domain Area: Balance Sheet](#8-balance-sheet)
10. [Domain Area: Subtree P&L Report](#9-subtree-pl-report)
11. [Domain Area: Transfers](#10-transfers)
12. [Domain Area: Obligation Lifecycle (PostObligationToLedger, StatusTransitions, SpawnObligationInstances, OverdueDetection)](#11-obligation-lifecycle)
13. [Domain Area: Invoicing (InvoicePersistence)](#12-invoicing)
14. [Domain Area: Operational Integrity (OrphanedPostingDetection, BalanceProjection)](#13-operational-integrity)
15. [Cross-Cutting Principle Summary](#cross-cutting-principle-summary)
16. [Findings Summary Table](#findings-summary-table)

---

## Methodology

For each spec file I identify the accounting domain it covers, name the specific GAAP principles that govern that domain, then classify each as:

- **Enforced** -- A scenario exists that would fail if the principle were violated.
- **Implied** -- Scenarios are consistent with the principle but don't specifically test it; a broken implementation could still pass all scenarios.
- **Absent** -- No scenario addresses this principle in an area where it applies.

For implied and absent findings, I describe what violation would go undetected.

---

## 1. General Ledger

**Spec files:** `PostJournalEntry.feature`, `VoidJournalEntry.feature`

### Applicable Principles

#### Double-Entry Bookkeeping -- ENFORCED

FT-PJE-006 explicitly rejects an unbalanced entry ("Debits do not equal credits"). FT-PJE-009 rejects a single-line entry. The domain function `validateBalanced` is directly tested through the posting path. Every compound entry scenario (PJE-001, PJE-002) also implicitly confirms balanced posting.

**Verdict:** A system that allowed one-sided entries would fail PJE-006 and PJE-009.

#### Positive Amount Constraint -- ENFORCED

FT-PJE-007 (zero) and FT-PJE-008 (negative) reject non-positive amounts. Domain function `validateAmountsPositive` is exercised.

#### Append-Only Ledger (Non-Destruction / Audit Trail) -- ENFORCED

FT-VJE-002 explicitly confirms a voided entry "still exists in the database." FT-VJE-003 confirms lines and references are intact after void. The system voids rather than deletes. FT-VJE-001 confirms voided_at and void_reason are recorded.

**Verdict:** A system that physically deleted entries would fail VJE-002.

#### Void Reason Requirement (Audit Trail) -- ENFORCED

FT-VJE-005 (empty reason) and FT-VJE-006 (whitespace-only) are rejected. Domain function `validateVoidReason` is tested.

#### Atomicity of Posting -- ENFORCED

FT-PJE-019 confirms that a validation failure leaves no persisted rows. This tests transactional integrity.

#### Period Integrity (Entry Date Within Period) -- ENFORCED

FT-PJE-014 rejects an entry whose date falls outside the period range. FT-PJE-013 rejects posting to a closed period.

#### Inactive Account Guard -- ENFORCED

FT-PJE-015 rejects posting to an inactive account.

#### Consistency of Basis (Cash vs. Accrual) -- IMPLIED

The system is described as cash-basis. No scenario explicitly tests that an accrual-style entry (e.g., booking revenue before cash is received into an accounts receivable) is rejected. The system allows any account type in any journal entry line as long as the entry is balanced, the accounts are active, and the period is valid.

**What goes undetected:** An implementation could allow a user to debit "Accounts Receivable" (an asset) and credit "Revenue" before cash changes hands. The specs do not enforce that revenue is only recognized upon cash receipt. This is a design-level gap: on cash basis, the system should either (a) not have A/R-style accounts, or (b) have scenarios that verify revenue recognition only occurs alongside a cash-type account. Currently, the COA has no A/R account, which is the implicit guard, but the specs don't test this constraint.

#### Materiality -- ABSENT

No scenario tests materiality thresholds. This is expected for a bookkeeping engine -- materiality is a judgment-level principle, not a system rule. Noted for completeness, not a gap.

#### Going Concern -- ABSENT

Not applicable at the transaction engine level. Noted for completeness.

---

## 2. Account Classification

**Spec file:** `AccountSubTypes.feature`

### Applicable Principles

#### Chart of Accounts Classification Integrity -- ENFORCED

FT-AST-001 through AST-003 exhaustively validate which subtypes are legal for which account types. FT-AST-010 through AST-012 confirm the domain validation rejects invalid combinations. The valid-combination matrix is fully covered.

#### Equity Has No Subtypes -- ENFORCED

FT-AST-003 tests all nine subtypes against equity and confirms every one is rejected.

#### Persistence Round-Trip -- ENFORCED

FT-AST-007 through AST-009 confirm subtypes survive write/read. FT-AST-005 confirms string serialization round-trips.

#### Seed Data Correctness -- ENFORCED

FT-AST-015 through AST-017 verify the production chart of accounts has correct subtype assignments after migration.

#### Account Type Reclassification Guard -- ABSENT

No scenario tests what happens if an account's type is changed (e.g., from Asset to Expense) when it already has a subtype that is valid for the old type but invalid for the new one. The domain function `isValidSubType` exists and would reject it, but no spec exercises this path.

**What goes undetected:** An implementation that allows account type changes without re-validating the subtype could silently create an Asset account with subtype "OperatingExpense."

---

## 3. Account Balances

**Spec file:** `AccountBalance.feature`

### Applicable Principles

#### Normal Balance Convention (Debit/Credit) -- ENFORCED

FT-AB-001 tests a normal-debit account (asset). FT-AB-002 tests a normal-credit account (revenue). Both confirm the formula: normal-debit = debits minus credits, normal-credit = credits minus debits.

#### Voided Entry Exclusion -- ENFORCED

FT-AB-005 explicitly confirms a voided entry is excluded from the balance.

#### Temporal Cutoff (As-Of Date) -- ENFORCED

FT-AB-006 confirms entries after the as-of date are excluded. This is a core period cutoff principle.

#### Accumulation Across Entries -- ENFORCED

FT-AB-003 (multiple entries) and FT-AB-004 (mixed debits/credits) confirm correct accumulation.

#### Inactive Account Reporting -- ENFORCED

FT-AB-008 confirms an inactive account's balance is still calculated. This upholds the principle that deactivation does not erase history.

#### Cross-Period Accumulation -- IMPLIED

AccountBalance queries by as-of date, not by fiscal period. The scenarios all operate within a single period. No scenario creates entries in two different fiscal periods and confirms they both contribute to the as-of-date balance. (The BalanceSheet spec does this in FT-BS-008, but AccountBalance itself does not.)

**What goes undetected:** An implementation that filtered by fiscal period instead of by entry_date <= as_of_date would pass all AccountBalance scenarios but produce wrong results when entries span multiple periods.

---

## 4. Period Management

**Spec file:** `CloseReopenFiscalPeriod.feature`

### Applicable Principles

#### Period Close Prevents New Postings -- ENFORCED

FT-CFP-010 confirms posting to a closed period is rejected with "not open."

#### Period Close Does Not Alter Balances (No Closing Entries) -- ENFORCED

FT-CFP-012 records trial balance totals before and after close and confirms they are identical. This is significant: the system does not generate closing entries that sweep temporary accounts to retained earnings. On cash basis, this is acceptable -- the balance sheet computes retained earnings dynamically.

#### Reopen Requires Documented Reason (Audit Trail) -- ENFORCED

FT-CFP-005 rejects empty and whitespace-only reasons. FT-CFP-011 confirms the reason is logged at Info level.

#### Idempotency -- ENFORCED

FT-CFP-003 (re-close) and FT-CFP-004 (re-open) confirm idempotent behavior.

#### Period Close Does NOT Generate Closing Entries -- IMPLIED (Design Choice, Not Gap)

FT-CFP-012 proves balances don't change, but no scenario explicitly verifies that no closing journal entries are created. The spec tests the symptom (balances unchanged) rather than the mechanism (no entries created).

**What goes undetected:** An implementation that creates self-canceling closing entries (net zero) would pass CFP-012. This is a stretch -- the real risk is academic.

---

## 5. Opening Balances

**Spec file:** `OpeningBalances.feature`

### Applicable Principles

#### Double-Entry for Opening Balances -- ENFORCED

FT-OB-001 confirms total debits equal total credits. FT-OB-002 and OB-003 confirm the balancing entry is computed correctly in both directions. FT-OB-004 confirms a single-account entry creates a two-line journal entry.

#### Balancing Account Must Be Equity -- ENFORCED

FT-OB-012 rejects a non-equity balancing account. This enforces the GAAP principle that opening balance adjustments flow through equity.

#### Duplicate Account Prevention -- ENFORCED

FT-OB-006 rejects duplicate accounts in the entries list.

#### Zero/Empty Balance Prevention -- ENFORCED

FT-OB-008 rejects zero-balance entries. FT-OB-007 rejects an empty entries list.

#### Balancing Account Excluded From Entries -- ENFORCED

FT-OB-009 rejects the balancing account appearing in the entries list. Prevents circular booking.

#### Historical Cost -- IMPLIED

Opening balances are posted at face value. No scenario tests whether the system rejects or warns about market-value adjustments to opening balances. On cash basis for personal finance, historical cost is the natural default, but the system doesn't enforce it -- it just accepts whatever number is provided.

**What goes undetected:** A user could post opening balances at market value rather than historical cost. The system would accept it without complaint.

---

## 6. Trial Balance

**Spec file:** `TrialBalance.feature`

### Applicable Principles

#### Debits Equal Credits (Trial Balance Integrity) -- ENFORCED

FT-TB-001 confirms the trial balance is balanced and that grand total debits equal grand total credits. FT-TB-005 confirms an empty period is balanced at zero.

#### Period-Scoped Reporting -- ENFORCED

The feature description states "period-scoped activity only, not cumulative." FT-TB-001 through TB-007 all query by fiscal period.

#### Account Type Grouping -- ENFORCED

FT-TB-002 confirms accounts are grouped by type with correct subtotals and correct group order. FT-TB-003 confirms groups with no activity are omitted.

#### Voided Entry Exclusion -- ENFORCED

FT-TB-004 confirms voided entries are excluded.

#### Normal Balance Formula -- ENFORCED

FT-TB-008 confirms net balance uses the normal balance formula for both debit-normal and credit-normal accounts.

#### Cross-Period Leakage -- IMPLIED

No scenario creates entries in two periods and confirms the trial balance for one period excludes the other's entries. (The Income Statement spec does this in FT-IS-017, but the Trial Balance spec itself does not.)

**What goes undetected:** An implementation that leaked entries from adjacent periods would pass all Trial Balance scenarios if tests only ever create entries in the queried period.

---

## 7. Income Statement

**Spec file:** `IncomeStatement.feature`

### Applicable Principles

#### Revenue/Expense Matching Within Period -- ENFORCED

FT-IS-001 confirms correct net income from period-scoped revenue and expenses. FT-IS-017 explicitly confirms entries in one period are excluded from another period's income statement. This is the strongest period-cutoff test in the suite.

#### Revenue = Credits Minus Debits / Expense = Debits Minus Credits -- ENFORCED

FT-IS-011 (revenue with debit adjustment) and FT-IS-012 (expense with credit adjustment) explicitly test the normal-balance formula for income statement accounts.

#### Net Income Calculation -- ENFORCED

FT-IS-001, IS-009, IS-010 confirm net income = revenue - expenses, including net loss scenarios.

#### Voided Entry Exclusion -- ENFORCED

FT-IS-004 confirms voided entries are excluded.

#### Inactive Account Reporting -- ENFORCED

FT-IS-006 confirms inactive accounts with activity still appear on the income statement.

#### Accounts Without Activity Omitted -- ENFORCED

FT-IS-005 confirms accounts with no period activity are not shown.

#### Closed Period Still Reportable -- ENFORCED

FT-IS-016 confirms a closed period's income statement is still accessible.

#### Revenue Recognition (Cash Basis) -- IMPLIED

The scenarios record revenue when cash is debited (asset) and revenue is credited. This is consistent with cash-basis revenue recognition. However, no scenario tests that revenue booked without a corresponding cash movement is rejected. The system would accept it.

**What goes undetected:** An entry that credits Revenue and debits an A/R account (recognizing revenue before cash receipt) would be accepted by the posting engine and would appear on the income statement. No spec guards against accrual-style revenue recognition.

---

## 8. Balance Sheet

**Spec file:** `BalanceSheet.feature`

### Applicable Principles

#### Accounting Equation (A = L + E) -- ENFORCED

FT-BS-002 explicitly verifies assets = liabilities + total equity with specific numbers. FT-BS-001 confirms `isBalanced = true`. Multiple scenarios (BS-004, BS-005, BS-006, BS-008, BS-009) verify the balance sheet is balanced.

#### Retained Earnings = All-Time Revenue - All-Time Expenses -- ENFORCED

FT-BS-003 (positive), FT-BS-004 (negative), FT-BS-005 (zero) all verify retained earnings. The feature description states "retained earnings from all-time revenue and expense activity."

#### Financial Statement Articulation (IS flows into BS) -- ENFORCED

FT-BS-008 is the strongest test: entries across three fiscal periods all contribute to the balance sheet. Revenue from February and March ($1,500 combined) appears as retained earnings. This proves the income statement's net income flows into the balance sheet's retained earnings.

**This is a critical finding: articulation is tested as a structural relationship, not coincidental numbers.** The balance sheet computes retained earnings dynamically from all-time revenue/expense activity, which is the cash-basis equivalent of flowing income statement results into equity.

#### Cumulative Nature (Not Period-Scoped) -- ENFORCED

FT-BS-008 explicitly tests entries across three periods contributing to a single balance sheet date.

#### Voided Entry Exclusion -- ENFORCED

FT-BS-007 confirms voided entries are excluded.

#### Inactive Account Reporting -- ENFORCED

FT-BS-011 confirms inactive accounts with balances still appear.

#### Zero-Balance Accounts With Activity -- ENFORCED

FT-BS-010 confirms an account that had activity netting to zero still appears on the balance sheet. This upholds GAAP presentation: accounts with activity are disclosed even at zero balance.

#### Presentation: Sections (Assets, Liabilities, Equity) -- ENFORCED

FT-BS-002, BS-009 verify separate section totals. FT-BS-006 verifies zero state with correct section structure.

#### Contra-Account Presentation -- ABSENT

No scenario tests contra accounts (e.g., Accumulated Depreciation as a contra-asset, or Sales Returns as a contra-revenue). The COA does not currently have contra accounts, but the system has no spec-level guard that would enforce correct presentation if they were added.

**What goes undetected:** A contra-asset account (normal credit balance within assets) could be presented with incorrect sign or in the wrong section without any spec failing.

---

## 9. Subtree P&L Report

**Spec file:** `SubtreePLReport.feature`

### Applicable Principles

#### Hierarchical Account Aggregation -- ENFORCED

FT-SPL-008 confirms grandchild accounts are included. FT-SPL-007 confirms accounts outside the subtree are excluded. FT-SPL-004 confirms a leaf account works as its own subtree.

#### Voided Entry Exclusion -- ENFORCED

FT-SPL-006 confirms voided entries are excluded.

#### Period Scoping -- IMPLIED

Scenarios use "an open fiscal period" but no scenario creates entries in two periods and confirms only the requested period's entries appear. The subtree P&L is described as period-scoped but this is not explicitly tested with cross-period data.

**What goes undetected:** An implementation that included entries from all periods would pass SPL scenarios if tests only create entries in the queried period.

---

## 10. Transfers

**Spec file:** `Transfers.feature`

### Applicable Principles

#### Double-Entry for Transfers -- ENFORCED

FT-TRF-007 confirms a confirmed transfer creates a journal entry with debit to destination and credit to source. This is correct: the "from" account is credited (reduced) and the "to" account is debited (increased) for asset-to-asset transfers.

#### Asset-Only Transfers -- ENFORCED

FT-TRF-003 rejects a transfer involving a non-asset account. Transfers are restricted to asset accounts, which is correct for cash-basis: you don't "transfer" between revenue and expense accounts.

#### Same-Account Prevention -- ENFORCED

FT-TRF-002 rejects from_account == to_account.

#### Inactive Account Prevention -- ENFORCED

FT-TRF-004 rejects transfers involving inactive accounts.

#### Positive Amount Constraint -- ENFORCED

FT-TRF-005 (zero) and FT-TRF-006 (negative) reject invalid amounts.

#### Status State Machine -- ENFORCED

FT-TRF-013 rejects confirming a transfer not in "initiated" status.

#### Fiscal Period Requirement -- ENFORCED

FT-TRF-014 rejects confirmation when no fiscal period covers the date.

#### Idempotency Guard -- ENFORCED

FT-TRF-015 confirms retry after partial failure reuses existing journal entry. FT-TRF-016 confirms a voided prior entry does not trigger the guard.

#### Journal Entry Metadata -- ENFORCED

FT-TRF-009 through TRF-012 confirm source, reference, description, and entry_date are correct.

---

## 11. Obligation Lifecycle

**Spec files:** `PostObligationToLedger.feature`, `StatusTransitions.feature`, `SpawnObligationInstances.feature`, `OverdueDetection.feature`

### Applicable Principles

#### Obligation-to-Ledger Bridge (Double-Entry) -- ENFORCED

FT-POL-001 and POL-002 confirm posting a confirmed receivable or payable creates a 2-line journal entry with correct debit/credit allocation.

#### Status Must Be Confirmed Before Posting -- ENFORCED

FT-POL-007 rejects posting an instance not in "confirmed" status.

#### Double-Posting Prevention -- ENFORCED

FT-POL-016 confirms posting an already-posted instance is rejected and no duplicate journal entry is created.

#### Atomicity of Failed Posting -- ENFORCED

FT-POL-017 confirms a failed post leaves the instance in "confirmed" status with no journal entry.

#### Idempotency Guard (Crash Recovery) -- ENFORCED

FT-POL-018 confirms retry after partial failure reuses an existing JE. FT-POL-019 confirms a voided prior JE does not trigger the guard.

#### Fiscal Period Enforcement -- ENFORCED

FT-POL-012 (no period) and POL-013 (closed period) are tested.

#### Status State Machine Completeness -- ENFORCED

FT-ST-023 tests every invalid transition combination. FT-ST-001 through ST-009 test all valid forward transitions. The state machine is exhaustively covered.

#### Guard Conditions -- ENFORCED

FT-ST-016 (posted without JE ID), ST-017 (nonexistent JE), ST-018 (skipped without notes), ST-019 (confirmed without date), ST-011 (confirmed without amount).

#### Spawn Idempotency -- ENFORCED

FT-SI-015 and SI-016 confirm re-spawning skips existing instances.

#### Overdue Detection Precision -- ENFORCED

FT-OD-003 confirms an instance ON the reference date is NOT overdue (strict less-than). FT-OD-005 through OD-008 confirm non-eligible statuses and inactive instances are excluded.

#### Revenue Recognition Timing (Cash Basis) -- IMPLIED

The obligation lifecycle uses `confirmedDate` as the entry date for the journal entry (FT-POL-005). This implies cash-basis timing: the entry is booked when cash is confirmed as received/paid. However, no scenario tests that the `confirmedDate` must correspond to an actual cash event rather than an arbitrary date.

**What goes undetected:** A user could confirm an obligation with a `confirmedDate` that precedes actual cash receipt. The system would book it, violating cash-basis timing. The system trusts the user's `confirmedDate`.

#### Matching Principle (Accrual) -- NOT APPLICABLE

Cash basis does not use the matching principle. Obligations are recognized on confirmation, not when earned/incurred. Correct by design.

---

## 12. Invoicing

**Spec file:** `InvoicePersistence.feature`

### Applicable Principles

#### Invoice as Source Document (Not Ledger Entry) -- ENFORCED

The feature description states: "It is not a ledger posting." FT-INV-003 confirms invoices can be recorded against closed fiscal periods (which would reject ledger postings). This correctly separates source documents from journal entries.

#### Total = Rent + Utility -- ENFORCED

FT-INV-010 rejects a total that doesn't equal rent plus utility. This is a basic arithmetic integrity check.

#### Duplicate Prevention (Tenant + Period) -- ENFORCED

FT-INV-013 rejects duplicate tenant + fiscal period combinations.

#### Validation Completeness -- ENFORCED

FT-INV-006 through INV-011 cover empty tenant, negative amounts, excessive decimal places, and mismatched totals.

#### Invoice-to-Ledger Linkage -- ABSENT

No scenario tests that an invoice is linked to a journal entry, or that posting an obligation derived from an invoice references the invoice. The invoice exists as a standalone source document with no tested connection to the double-entry system.

**What goes undetected:** An invoice could be recorded and never posted to the ledger, or posted multiple times, without any spec detecting the discrepancy. The OrphanedPostingDetection spec covers JE-to-source reconciliation for obligations and transfers, but not for invoices.

---

## 13. Operational Integrity

**Spec files:** `OrphanedPostingDetection.feature`, `BalanceProjection.feature`

### Applicable Principles

#### Ledger-to-Source Reconciliation -- ENFORCED

FT-OPD-002 through OPD-004 detect three categories of inconsistency: DanglingStatus, MissingSource, and VoidedBackingEntry. FT-OPD-006 detects invalid references. This is a strong reconciliation control.

#### Projection Completeness (All Components) -- ENFORCED

FT-BP-007 tests the full projection formula: current balance + inflows - outflows - transfers out + transfers in. FT-BP-009 tests same-day aggregation with itemized breakdown.

#### Unknown Amount Disclosure (Conservatism) -- ENFORCED

FT-BP-010 and BP-011 confirm that null-amount obligations surface as "unknown" markers rather than being silently omitted or filled with guesses. This upholds the conservatism principle: don't hide uncertainty.

#### Projection Is Not Stored (Derived Data) -- IMPLIED

The feature describes projections as "computed, not stored -- every call recalculates." No scenario tests that a projection is not persisted. This is a design statement, not a tested invariant.

**What goes undetected:** An implementation that cached stale projections would pass all scenarios in isolation.

---

## Cross-Cutting Principle Summary

### Principles That Apply Across All Spec Areas

| Principle | Status | Notes |
|---|---|---|
| Double-Entry Bookkeeping | **ENFORCED** | PJE-006, OB-001, TRF-007, POL-001, TB-001 |
| Append-Only Ledger | **ENFORCED** | VJE-002, VJE-003 |
| Voided Entry Exclusion | **ENFORCED** | AB-005, BS-007, IS-004, TB-004, SPL-006 |
| Period Integrity | **ENFORCED** | PJE-013, PJE-014, CFP-010, IS-017 |
| Accounting Equation | **ENFORCED** | BS-002, BS-008 |
| Financial Statement Articulation | **ENFORCED** | BS-003/004/005 + BS-008 |
| Cash Basis Consistency | **IMPLIED** | No scenario rejects accrual-style entries |
| Historical Cost | **IMPLIED** | System accepts any value; no market-value guard |
| Contra-Account Presentation | **ABSENT** | No specs for contra accounts in any report |
| Invoice-to-Ledger Linkage | **ABSENT** | Invoices are isolated from the ledger |
| Closing Entries | **N/A** | Cash basis; retained earnings computed dynamically |
| Materiality | **N/A** | Judgment principle, not system-enforceable |
| Going Concern | **N/A** | Not applicable at engine level |

---

## Findings Summary Table

### Enforced (No Action Needed)

These principles have at least one scenario that would fail if the principle were violated:

1. Double-entry bookkeeping (balanced entries, minimum 2 lines)
2. Positive amounts only
3. Append-only ledger (void, don't delete)
4. Void reason requirement
5. Atomicity of posting
6. Period integrity (date in range, period open)
7. Inactive account guard on posting
8. Account classification integrity (subtype/type matrix)
9. Normal balance convention (debit-normal vs credit-normal)
10. Voided entry exclusion from all reports
11. Temporal cutoff (as-of date filtering)
12. Accounting equation (A = L + E)
13. Retained earnings computation
14. Financial statement articulation (IS flows to BS)
15. Period-scoped reporting (Income Statement explicitly tested cross-period)
16. Transfer asset-only constraint
17. Obligation status state machine (exhaustive coverage)
18. Double-posting prevention
19. Idempotency guards (obligations and transfers)
20. Orphaned posting detection (reconciliation control)
21. Unknown-amount disclosure (conservatism via unknown markers)
22. Invoice arithmetic integrity
23. Invoice uniqueness per tenant/period

### Implied (Pass Without Testing the Principle)

These principles are consistent with the current specs but would not be caught if violated:

| # | Principle | Area | Undetected Violation |
|---|---|---|---|
| I-1 | Cash basis consistency | PostJournalEntry, IncomeStatement | Revenue credited against a non-cash asset (e.g., A/R) would be accepted; no spec rejects accrual-style entries |
| I-2 | Cross-period accumulation | AccountBalance | All scenarios use a single period; implementation that filters by period_id instead of entry_date <= as_of_date would pass |
| I-3 | Cross-period exclusion | TrialBalance, SubtreePLReport | No cross-period entry scenario in these specs; period leakage undetected |
| I-4 | Historical cost | OpeningBalances | System accepts any numeric value; market-value opening balances undetected |
| I-5 | No closing entries mechanism | CloseReopenFiscalPeriod | CFP-012 checks balances unchanged, not that zero entries were created |
| I-6 | Cash-basis revenue timing | PostObligationToLedger | confirmedDate trusted as-is; system cannot verify it matches actual cash movement |
| I-7 | Projection non-persistence | BalanceProjection | Design doc says computed-not-stored; no spec enforces it |

### Absent (Not Addressed)

| # | Principle | Area | Undetected Violation |
|---|---|---|---|
| A-1 | Contra-account presentation | BalanceSheet, IncomeStatement | Contra accounts (accumulated depreciation, sales returns) could be presented with wrong sign or in wrong section |
| A-2 | Invoice-to-ledger linkage | InvoicePersistence | Invoice can exist without ever being posted; no reconciliation between invoices and JEs |
| A-3 | Account type reclassification guard | AccountSubTypes | Changing account type without re-validating subtype could create invalid classification |

---

## Assessment

LeoBloom's spec suite is strong where it matters most for a cash-basis personal bookkeeping system. The five pillars of double-entry bookkeeping -- balanced entries, append-only audit trail, period integrity, the accounting equation, and financial statement articulation -- are all enforced at the scenario level. The articulation between income statement and balance sheet (via retained earnings) is particularly well-done.

The primary structural gap is the **implied cash-basis consistency** (I-1). The system declares itself cash-basis but has no mechanism to prevent accrual-style entries. This is a reasonable design choice (the posting engine is basis-agnostic), but it means the cash-basis guarantee lives in operational discipline rather than system enforcement. For a single-user personal finance tool, this is acceptable. For a multi-user system, it would need tightening.

The **absent invoice-to-ledger linkage** (A-2) is worth monitoring. Currently, invoices and ledger postings are connected only through the obligation lifecycle. If invoices are ever generated independently of obligations, the lack of a reconciliation mechanism becomes a real gap.

---

*Signed: GAAP Mapper*
*Assessment date: 2026-04-07*
*Spec files reviewed: 18*
*Principles evaluated: 26*
*Classification: 23 enforced / 7 implied / 3 absent*
