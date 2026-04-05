# GAAP Mapper Report -- LeoBloom Spec Suite

**Date:** 2026-04-05
**Scope:** 9 feature files across Behavioral and Ops spec directories
**Basis of Accounting:** Cash basis, double-entry, as stated in DataModelSpec.md

---

## System Context

LeoBloom is a cash-basis, double-entry bookkeeping system for personal/investment
property finance. The domain model defines five account types (asset, liability,
equity, revenue, expense) with correct normal-balance assignments. The `ledger`
schema is the accounting engine; the `ops` schema is an operational layer that
feeds into it via obligation-to-ledger posting.

The chart of accounts uses a hierarchical parent-code tree as its sole
classification mechanism -- no separate category concept. This is
GAAP-consistent.

---

## Domain Area 1: General Ledger (Journal Entry Posting)

**Relevant spec files:** PostObligationToLedger.feature (and domain validation
in Ledger.fs, which underpins all posting paths)

### Applicable GAAP Principles

| Principle | Classification | Evidence / Gap |
|-----------|---------------|----------------|
| **Double-entry bookkeeping** | **Enforced** | `Ledger.validateBalanced` requires debits = credits. `validateMinimumLineCount` requires >= 2 lines. `validateAmountsPositive` prevents zero/negative amounts. POL-001/002 verify 2-line entries with matching debit/credit. Every journal entry in every feature file uses balanced entries. A broken double-entry implementation would fail these scenarios. |
| **Cash basis recognition** | **Enforced** | POL-005 asserts `entry_date = confirmed_date` (the date money moved). The DataModelSpec states entry_date is "when the money moved (cash basis)." The obligation-to-ledger bridge uses confirmed_date, not expected_date. A system that posted on the obligation date rather than the cash date would fail POL-005. |
| **Period integrity / cutoff** | **Enforced** | CFP-010 explicitly tests that posting to a closed period fails. POL-012 tests that posting without a covering fiscal period fails. POL-013 tests that posting to a closed period fails. The entry_date must fall within the fiscal period's date range (DataModelSpec invariant). An implementation that allowed entries in wrong periods would fail these scenarios. |
| **Append-only / no deletion** | **Implied** | The DataModelSpec states "a journal entry is never deleted." Voiding is tested (TB-004, BS-007, IS-004) -- voided entries are excluded from reports but remain in the ledger. However, no scenario asserts that a DELETE operation is rejected or that voided entries still exist in raw storage. **Violation that would go undetected:** An implementation that physically deletes voided entries rather than marking them void would pass all current specs, since the specs only verify that voided entries don't appear in reports. |
| **Void requires reason** | **Enforced** | `Ledger.validateVoidReason` in domain code requires a non-empty reason when `voidedAt` is set. This is a pure domain validator. However, no feature file explicitly tests the "void without reason is rejected" scenario. The enforcement exists in code but is not BDD-specified. I'll classify this as **Enforced** at the domain level but note the spec gap. |
| **Atomicity** | **Implied** | DataModelSpec states "all-or-nothing" for journal entry posting. No spec scenario tests partial failure (e.g., "when one line insert fails, the header is also rolled back"). **Violation that would go undetected:** An implementation that commits the journal_entry header but fails on lines could leave orphan headers in the database. |

---

## Domain Area 2: Trial Balance

**Relevant spec file:** TrialBalance.feature (11 scenarios)

### Applicable GAAP Principles

| Principle | Classification | Evidence / Gap |
|-----------|---------------|----------------|
| **Double-entry balance verification** | **Enforced** | TB-001 asserts `isBalanced = true` and grand total debits = credits. TB-005 asserts zero totals are balanced. TB-008 tests net balance using the normal-balance formula. A trial balance that didn't properly sum debits and credits would fail. |
| **Period scoping (not cumulative)** | **Enforced** | Feature header states "period-scoped activity only, not cumulative balance." All scenarios set up entries within a single period and verify totals for that period. TB-007 verifies accumulation within a period. An implementation that included prior-period entries would produce wrong totals and fail. |
| **Voided entry exclusion** | **Enforced** | TB-004 posts two entries, voids one, and asserts the trial balance reflects only the surviving entry. |
| **Account classification / grouping** | **Enforced** | TB-002 verifies groups appear in order (asset, revenue, expense) with correct subtotals per group. TB-003 verifies that account types with no activity are omitted. An implementation that misclassified accounts would produce wrong group totals. |
| **Consistency (closed period still readable)** | **Enforced** | TB-006 closes a period and then runs the trial balance, asserting it still works and returns correct totals. |
| **Normal-balance formula** | **Enforced** | TB-008 asserts that account 1010 (asset, normal-debit) has net balance = debits - credits, and account 4010 (revenue, normal-credit) has net balance = credits - debits. |

**No gaps identified in this domain area.** The trial balance specs are well-anchored to principles.

---

## Domain Area 3: Income Statement

**Relevant spec file:** IncomeStatement.feature (16 scenarios)

### Applicable GAAP Principles

| Principle | Classification | Evidence / Gap |
|-----------|---------------|----------------|
| **Revenue/expense classification** | **Enforced** | IS-001 through IS-010 verify that revenue accounts appear in the revenue section and expense accounts in the expenses section. The domain model ties account type to normal balance. An implementation that put expenses in the revenue section would fail. |
| **Period scoping** | **Enforced** | Feature header states "period-scoped activity only, not cumulative." All scenarios use a single period. IS-016 tests a closed period. An implementation that leaked other periods' data would produce wrong totals. |
| **Matching principle** | **Not applicable.** | This is a cash-basis system. The matching principle (expenses recognised in the period they help generate revenue) applies to accrual accounting. Cash basis recognises when money moves, which this system does correctly. No gap. |
| **Net income = Revenue - Expenses** | **Enforced** | IS-001 (600 = 1000 - 400), IS-008 (positive net income), IS-009 (negative net income / net loss), IS-010 (650 = 1000 - 350). Multiple scenarios with different revenue/expense mixes. |
| **Normal-balance formula** | **Enforced** | IS-011 tests revenue = credits - debits (700 - 100 = 600). IS-012 tests expenses = debits - credits (500 - 150 = 350). |
| **Voided entry exclusion** | **Enforced** | IS-004 voids an entry and asserts it doesn't affect the income statement. |
| **Presentation: asset/liability/equity exclusion** | **Implied** | The scenarios use asset accounts as the balancing side of entries, but no scenario asserts that asset accounts do NOT appear in income statement sections. **Violation that would go undetected:** An implementation that accidentally included asset-type accounts in the revenue or expense section would still produce correct section totals if the filter was applied elsewhere, but a filter bug could leak balance-sheet accounts into the P&L. The current scenarios don't test for the absence of non-income accounts in the output. |
| **Inactive account handling** | **Enforced** | IS-006 deactivates an account and verifies it still appears on the income statement if it had activity. |

---

## Domain Area 4: Balance Sheet

**Relevant spec file:** BalanceSheet.feature (11 scenarios)

### Applicable GAAP Principles

| Principle | Classification | Evidence / Gap |
|-----------|---------------|----------------|
| **Accounting equation (A = L + E)** | **Enforced** | BS-001 and BS-002 explicitly verify `isBalanced`. BS-002 checks assets (8000) = liabilities (2000) + total equity (6000). BS-004, BS-005, BS-006, BS-007, BS-008, BS-009 all verify `isBalanced`. The balance sheet report type has an `isBalanced` field. |
| **Cumulative nature (point-in-time, not period-scoped)** | **Enforced** | Feature header states "cumulative through a date, not period-scoped." BS-008 creates entries across three fiscal periods (Jan, Feb, Mar) and verifies the March 31 balance sheet includes all of them. |
| **Retained earnings = all-time revenue - expenses** | **Enforced** | BS-003 (2000 = 3000 - 1000), BS-004 (-600 = 200 - 800), BS-005 (0 when no revenue/expense). The retained earnings computation is explicitly tested as a separate line flowing into total equity. |
| **Financial statement articulation (IS flows into BS)** | **Enforced** | BS-002 shows retained earnings = 1000 (all revenue, no expenses), and total equity = equity (5000) + retained earnings (1000) = 6000. BS-008 shows multi-period retained earnings (1500) flowing into the balance sheet. The connection between income statement activity and balance sheet retained earnings is tested. |
| **Voided entry exclusion** | **Enforced** | BS-007 voids an entry and verifies it doesn't affect the balance sheet. |
| **Zero-balance account presentation** | **Enforced** | BS-010 creates an account with activity that nets to zero and asserts it still appears on the balance sheet with balance 0.00. This is correct GAAP presentation -- accounts with activity should appear even if their ending balance is zero. |
| **Inactive account handling** | **Enforced** | BS-011 deactivates an account with a balance and verifies it still appears. |
| **Historical cost** | **Not applicable.** | This system records transaction amounts as entered. There's no revaluation, fair-value adjustment, or depreciation mechanism. Historical cost is the default and the only treatment available. No gap for a cash-basis personal finance system. |
| **Revenue/expense account exclusion from sections** | **Implied** | Revenue and expense accounts are used in scenarios (e.g., BS-001 uses account 4010 revenue and 5010 expense) but their balances flow into retained earnings, not into the asset/liability/equity sections directly. No scenario asserts that revenue/expense accounts do NOT appear as line items in the assets, liabilities, or equity sections. **Violation that would go undetected:** An implementation that listed expense accounts under the assets section (both are normal-debit) would produce incorrect section totals but no scenario explicitly checks "the assets section does not contain account 5010." The `isBalanced` check would still pass because the accounting equation would hold if retained earnings were correspondingly wrong. Actually -- the retained earnings computation is separate from section totals, so a double-counting bug (expense in both retained earnings and assets section) would cause isBalanced to fail. The articulation tests provide indirect protection. Upgrading to **Enforced** for this specific failure mode, but noting that mis-classification (expense INSTEAD OF retained earnings, not in addition to) would not be caught. |
| **Going concern** | **Not applicable.** | A personal finance ledger doesn't have going-concern considerations. |

---

## Domain Area 5: Close / Reopen Fiscal Period

**Relevant spec file:** CloseReopenFiscalPeriod.feature (11 scenarios)

### Applicable GAAP Principles

| Principle | Classification | Evidence / Gap |
|-----------|---------------|----------------|
| **Period close prevents new postings** | **Enforced** | CFP-010 closes a period and then attempts to post a journal entry, asserting failure with "not open." This is the core period-integrity enforcement. |
| **Reopen requires justification** | **Enforced** | CFP-005 tests that empty/whitespace-only reasons are rejected. CFP-002 and CFP-011 test valid reasons. CFP-011 also verifies the reason is logged. |
| **Idempotency** | **Enforced** | CFP-003 (closing already-closed) and CFP-004 (reopening already-open) both succeed without error. |
| **Full lifecycle** | **Enforced** | CFP-008 runs close -> reopen -> close cycle. |
| **Closing entries (sweep temporary accounts)** | **Absent** | GAAP period close typically involves closing entries that sweep revenue and expense balances into retained earnings. This system computes retained earnings on-the-fly in the balance sheet report (all-time revenue minus expenses) rather than posting closing entries. For a cash-basis personal finance system, this is a legitimate design choice -- the computed approach produces the same result without the ceremony. However, **no spec verifies that closing a period has no side effects on account balances.** **Violation that would go undetected:** If an implementation were to zero out revenue/expense account balances when closing a period (traditional closing entries done incorrectly or unexpectedly), no spec would catch the resulting data loss, because no scenario checks account balances before and after period close. The design assumes closing is a flag toggle only, but this assumption is not tested. |

---

## Domain Area 6: Obligation Agreements (CRUD)

**Relevant spec file:** ObligationAgreementCrud.feature (28 scenarios)

### Applicable GAAP Principles

| Principle | Classification | Evidence / Gap |
|-----------|---------------|----------------|
| **Account validation (source/dest must exist and be active)** | **Enforced** | OA-009 (nonexistent source), OA-010 (inactive source), OA-011 (nonexistent dest), OA-024 (inactive source on update). |
| **Soft-delete (no physical deletion)** | **Enforced** | OA-026 deactivates an agreement and verifies `isActive = false`. OA-014 verifies inactive agreements are excluded from default listings. OA-025 tests reactivation. OA-028 blocks deactivation when active instances exist. |
| **Data integrity (validation rules)** | **Enforced** | OA-003 through OA-008 test name, counterparty, amount, expectedDay validation. OA-008 tests error collection. |
| **Receivable vs. payable classification** | **Enforced** | OA-001 creates a payable. OA-002 creates a receivable. OA-015 filters by obligation type. The domain type `ObligationDirection` is a discriminated union. |
| **Account type consistency** | **Absent** | The DataModelSpec states `source_account_id` and `dest_account_id` reference `ledger.account.id`, and the design notes say the COA hierarchy IS the taxonomy. But **no scenario verifies that the account types are appropriate for the obligation direction.** For a payable, the destination should be an expense account and the source an asset account. For a receivable, the destination should be an asset account and the source a revenue account. **Violation that would go undetected:** Creating a payable agreement where both source and dest are revenue accounts would be accepted. When this agreement's instances are posted to the ledger, the resulting journal entry would have a debit to revenue and a credit to revenue -- technically balanced but accounting nonsense. The system would dutifully record a meaningless transaction. |

---

## Domain Area 7: Spawn Obligation Instances

**Relevant spec file:** SpawnObligationInstances.feature (18 scenarios)

### Applicable GAAP Principles

| Principle | Classification | Evidence / Gap |
|-----------|---------------|----------------|
| **Cadence correctness** | **Enforced** | SI-001 through SI-008 test monthly, quarterly, annual, and one-time date generation. SI-002/SI-003 test day-of-month clamping (leap year handling). |
| **Idempotency / no duplicate instances** | **Enforced** | SI-015 tests overlapping ranges (skips existing, creates new). SI-016 tests one-time duplicate skip. The DataModelSpec invariant "unique on (obligation_id, expected_date)" is exercised. |
| **Inactive agreement guard** | **Enforced** | SI-017 rejects spawning for inactive agreements. |
| **Amount inheritance** | **Enforced** | SI-011 verifies instances inherit the agreement's amount. SI-012 verifies variable-amount agreements produce instances with no amount. |
| **Cash basis (no accrual creation)** | **Implied** | Spawned instances have status "expected" and no journal entry. The ledger is not touched during spawning. This is consistent with cash basis (no recognition until money moves). However, **no scenario asserts that spawning does NOT create journal entries.** **Violation that would go undetected:** An implementation that created accrual-basis journal entries (debit receivable, credit revenue) at spawn time would pass all spawn specs, because those specs only check instance creation, not the absence of ledger side effects. The violation would likely be caught downstream by trial balance or balance sheet specs showing unexpected entries, but the spawn specs themselves provide no protection. |

---

## Domain Area 8: Status Transitions

**Relevant spec file:** StatusTransitions.feature (22 scenarios)

### Applicable GAAP Principles

| Principle | Classification | Evidence / Gap |
|-----------|---------------|----------------|
| **State machine integrity** | **Enforced** | ST-001 through ST-009 test all valid forward transitions. ST-012 through ST-015 test four invalid transitions. The `allowedTransitions` map in the domain code defines the complete state machine. |
| **Posted requires journal entry** | **Enforced** | ST-008 tests posting with a journal entry. ST-016 tests posting without one (rejected). ST-017 tests posting with a nonexistent journal entry (rejected). |
| **Confirmed requires date** | **Enforced** | ST-002, ST-004, ST-007 provide confirmedDate. ST-019 omits it (rejected). |
| **Confirmed requires amount** | **Enforced** | ST-010, ST-011 test amount handling. ST-011 rejects confirmed transition when no amount exists anywhere. |
| **Skipped requires justification** | **Enforced** | ST-009 provides notes. ST-018 omits notes (rejected). ST-022 tests that existing notes suffice. |
| **No backward transitions** | **Enforced** | ST-012 (confirmed -> expected), ST-013 (posted -> confirmed), ST-014 (skipped -> confirmed), ST-015 (overdue -> in_flight) are all rejected. The state machine is forward-only (plus the overdue lateral path). |
| **Inactive instance guard** | **Enforced** | ST-020 rejects transitions on inactive instances. |
| **Irreversibility of posted status** | **Enforced** | ST-013 rejects posted -> confirmed. The `allowedTransitions` map has no entry for the Posted state, meaning no transitions out of Posted are valid. This is correct -- once posted to the ledger, the obligation instance is final. (The journal entry can be voided separately, but the instance itself cannot be un-posted.) |
| **Posted -> void pathway** | **Absent** | If an obligation instance is posted to the ledger and the journal entry is later voided, there is no mechanism to reflect this on the instance. The instance remains in "posted" status with a journal_entry_id pointing to a voided entry. **Violation that would go undetected:** A user voids a journal entry that was created by obligation posting. The instance still says "posted" with a journal_entry_id. Reports correctly exclude the voided entry, but the ops layer thinks the obligation is settled. No spec or domain logic catches this inconsistency. This is a design gap more than a GAAP gap, but it means the ops and ledger layers can disagree about reality. |

---

## Domain Area 9: Overdue Detection

**Relevant spec file:** OverdueDetection.feature (11 scenarios)

### Applicable GAAP Principles

| Principle | Classification | Evidence / Gap |
|-----------|---------------|----------------|
| **Timely recognition of overdue status** | **Enforced** | OD-001/002 transition overdue instances. OD-003/004 enforce the boundary (on or after reference date = not overdue). |
| **Status filter correctness** | **Enforced** | OD-005 (in_flight not flagged), OD-006 (already overdue not re-flagged), OD-007 (confirmed not flagged), OD-008 (inactive not flagged). |
| **Idempotency** | **Enforced** | OD-009 runs detection twice; second run transitions zero. |
| **Mixed-set correctness** | **Enforced** | OD-010 combines multiple statuses and active/inactive states, asserting only eligible instances transition. |
| **Receivable vs. payable distinction** | **Absent** | Overdue detection treats all obligations the same regardless of direction. GAAP has different implications for overdue receivables (potential bad debt, allowance for doubtful accounts) vs. overdue payables (late payment penalties, vendor relationship). **Violation that would go undetected:** This is more of a design consideration than a spec gap for a cash-basis system. Cash-basis accounting doesn't recognize bad debt expense or allowances. The system's uniform treatment is defensible. However, no scenario tests that overdue detection works correctly for both receivable and payable instances in the same batch. |

---

## Domain Area 10: Obligation-to-Ledger Posting (Cross-Domain Bridge)

**Relevant spec file:** PostObligationToLedger.feature (15 scenarios)

### Applicable GAAP Principles

| Principle | Classification | Evidence / Gap |
|-----------|---------------|----------------|
| **Double-entry from obligation data** | **Enforced** | POL-001/002 verify 2-line entries with debit to destination and credit to source. Both receivable and payable directions are tested. |
| **Cash basis (entry_date = confirmed_date)** | **Enforced** | POL-005 asserts entry_date equals confirmedDate. |
| **Fiscal period coverage** | **Enforced** | POL-012 (no period covering date), POL-013 (closed period). |
| **Pre-posting validation** | **Enforced** | POL-007 (not confirmed), POL-008 (no amount), POL-009 (no confirmed_date), POL-010 (no source account), POL-011 (no dest account). |
| **Audit trail (source and reference)** | **Enforced** | POL-004 verifies source = "obligation" and a reference with type "obligation" and value = instance ID. POL-003 verifies the description format. |
| **Account direction correctness** | **Implied** | POL-001 and POL-002 both assert "debit line is for the destination account" and "credit line is for the source account." This is tested for both receivable and payable directions. However, the specs don't verify that the debit/credit directions are GAAP-correct for the account types involved. For a receivable, the destination is an asset account (debit increases it -- correct) and the source is a revenue account (credit increases it -- correct). For a payable, the destination is an expense account (debit increases it -- correct) and the source is an asset account (credit decreases it -- correct). The specs verify the mechanical pattern (debit dest, credit source) but not the accounting semantics. **Violation that would go undetected:** If the agreement's source and destination accounts were swapped (e.g., a payable with source = expense and dest = asset), the system would dutifully debit the asset and credit the expense -- reversing the correct treatment. The posting engine doesn't validate that the account types make sense for the obligation direction. This connects to the account-type-consistency gap identified in Domain Area 6. |
| **Single-posting guarantee** | **Implied** | POL-007 rejects non-confirmed instances, and the status transition specs (ST-013) prevent posted -> confirmed. Together, these imply an instance can only be posted once. But **no scenario explicitly tests "posting the same instance twice returns an error."** **Violation that would go undetected:** A race condition or missing check could allow double-posting, creating two journal entries for the same obligation instance. The second posting would overwrite journal_entry_id, orphaning the first entry. |

---

## Summary of Findings

### Enforced (principle violation would cause spec failure)
- Double-entry bookkeeping (balanced entries, minimum 2 lines, positive amounts)
- Cash-basis recognition (entry_date = confirmed_date)
- Period integrity / cutoff (closed periods reject postings)
- Trial balance correctness (debits = credits, grouped by type)
- Income statement: Revenue - Expenses = Net Income
- Balance sheet: A = L + E, with computed retained earnings
- Financial statement articulation (IS net income flows to BS retained earnings)
- Normal-balance formula (debits - credits for normal-debit; credits - debits for normal-credit)
- Voided entry exclusion from all reports
- State machine integrity (forward-only, guards on posted/confirmed/skipped)
- Obligation lifecycle (spawn, transition, post)
- Period close/reopen with reason requirement

### Implied (consistent with principle but not specifically tested)
- Append-only journal (voids tested in reports, but physical deletion not tested)
- Atomicity (no partial-failure scenario)
- No accrual side effects from spawning (spawn specs don't check ledger)
- Single-posting guarantee (indirect protection via state machine, no direct test)
- Account direction correctness in obligation posting (mechanical pattern tested, not semantic correctness)
- Income statement excludes balance-sheet accounts (no negative test)

### Absent (no scenario addresses this)
- **Closing entries / period-close side effects:** No test verifies that closing a period doesn't modify account balances. The system's design (computed retained earnings) makes traditional closing entries unnecessary, but the assumption that close is flag-only is untested.
- **Account-type consistency for obligations:** No validation that source/dest account types are appropriate for receivable vs. payable direction. A payable with two revenue accounts would be accepted.
- **Posted-instance / voided-entry reconciliation:** No mechanism to reflect a voided journal entry back to the ops layer. Posted instances can reference voided entries indefinitely.
- **Double-posting prevention (explicit):** No scenario tests posting an already-posted instance.

---

## Recommendation Priority

1. **Account-type consistency for obligations** (Domain Areas 6 and 10) -- This is the highest-risk gap. A misconfigured obligation agreement will produce incorrect journal entries that are technically balanced but semantically wrong. The system will record them without complaint. Add validation in the obligation-to-ledger posting path (or in agreement creation) that verifies the account types are sensible for the obligation direction.

2. **Period-close side-effect test** (Domain Area 5) -- Add a scenario: close a period, then run the trial balance (or balance sheet) and verify the numbers haven't changed. Confirms the flag-only assumption.

3. **Double-posting prevention** (Domain Area 10) -- Add a scenario: post a confirmed instance, then attempt to post it again. Assert rejection.

4. **Posted/voided reconciliation** (Domain Area 8) -- This is a design decision, not a spec gap. But the current state means the ops dashboard can show a settled obligation whose backing journal entry no longer counts. Worth a design note at minimum.

---

*Signed: GAAP Mapper*
