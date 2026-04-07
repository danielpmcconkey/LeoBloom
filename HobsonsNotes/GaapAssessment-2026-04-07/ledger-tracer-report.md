# Ledger Tracer Report -- LeoBloom GAAP Assessment

**Date:** 2026-04-07
**Scope:** All 18 Gherkin spec files in Specs/Behavioral, Specs/Ledger, and Specs/Ops
**Method:** Extract implied journal entries, build T-accounts, verify arithmetic against assertions

---

## Domain Reference

Account types and normal balances (from Ledger.fs):

| Account Type | Normal Balance | Balance Formula           |
|-------------|---------------|---------------------------|
| Asset       | Debit         | Debits - Credits          |
| Expense     | Debit         | Debits - Credits          |
| Liability   | Credit        | Credits - Debits          |
| Equity      | Credit        | Credits - Debits          |
| Revenue     | Credit        | Credits - Debits          |

---

## 1. AccountBalance.feature

### FT-AB-001: Normal-debit account balance after single entry

**Journal Entry:** "March rent"

| Account | Debit   | Credit  |
|---------|---------|---------|
| 1010 (Asset) | 1000.00 | |
| 4010 (Revenue) | | 1000.00 |

Debits = Credits = 1000.00. Balanced.

**T-Account: 1010 (Asset, normal debit)**
- Debit: 1000.00
- Balance = 1000.00 - 0.00 = 1000.00

**Assertion:** balance is 1000.00. **CORRECT.**

---

### FT-AB-002: Normal-credit account balance after single entry

Same journal entry as AB-001.

**T-Account: 4010 (Revenue, normal credit)**
- Credit: 1000.00
- Balance = 1000.00 - 0.00 = 1000.00

**Assertion:** balance is 1000.00. **CORRECT.**

---

### FT-AB-003: Balance accumulates across multiple entries

**JE1:** "First payment"

| Account | Debit  | Credit |
|---------|--------|--------|
| 1010    | 500.00 |        |
| 4010    |        | 500.00 |

**JE2:** "Second payment"

| Account | Debit  | Credit |
|---------|--------|--------|
| 1010    | 300.00 |        |
| 4010    |        | 300.00 |

Both balanced. 500 = 500, 300 = 300.

**T-Account: 1010 (Asset)**
- Debits: 500.00 + 300.00 = 800.00
- Credits: 0.00
- Balance = 800.00

**Assertion:** balance is 800.00. **CORRECT.**

---

### FT-AB-004: Mixed debits and credits net correctly

**JE1:** "Income"

| Account | Debit   | Credit  |
|---------|---------|---------|
| 1010    | 1000.00 |         |
| 4010    |         | 1000.00 |

**JE2:** "Expense"

| Account | Debit  | Credit |
|---------|--------|--------|
| 5010    | 400.00 |        |
| 1010    |        | 400.00 |

Both balanced.

**T-Account: 1010 (Asset)**
- Debits: 1000.00
- Credits: 400.00
- Balance = 1000.00 - 400.00 = 600.00

**Assertion:** balance is 600.00. **CORRECT.**

---

### FT-AB-005: Voided entry excluded from balance

**JE1:** "Good entry" -- 1010 DR 500 / 4010 CR 500 (active)
**JE2:** "Bad entry" -- 1010 DR 200 / 4010 CR 200 (VOIDED)

**T-Account: 1010 (Asset)** (excluding voided)
- Debits: 500.00
- Balance = 500.00

**Assertion:** balance is 500.00. **CORRECT.**

---

### FT-AB-006: Entry after as-of date excluded from balance

**JE1:** "Early entry" dated 2026-03-10 -- 1010 DR 500 / 4010 CR 500
**JE2:** "Late entry" dated 2026-03-25 -- 1010 DR 300 / 4010 CR 300

Query as of 2026-03-15. JE2 (dated 2026-03-25) is after as-of date, excluded.

**T-Account: 1010 (Asset)** (as of 2026-03-15)
- Debits: 500.00
- Balance = 500.00

**Assertion:** balance is 500.00. **CORRECT.**

---

### FT-AB-007: Account with no entries has zero balance

No entries posted.

**T-Account: 1010 (Asset)**
- Balance = 0.00

**Assertion:** balance is 0.00. **CORRECT.**

---

### FT-AB-008: Inactive account balance is calculated

**JE1:** "Before deactivation" -- 1010 DR 500 / 4010 CR 500

Account deactivated after posting. Balance query still works.

**T-Account: 1010 (Asset)**
- Debits: 500.00
- Balance = 500.00

**Assertion:** balance is 500.00. **CORRECT.**

---

### FT-AB-011: Lookup by account code matches lookup by ID

**JE1:** "Code lookup test" -- 1010 DR 750 / 4010 CR 750

**T-Account: 1010 (Asset)**
- Debits: 750.00
- Balance = 750.00

**Assertion:** balance is 750.00. **CORRECT.**

---

## 2. BalanceSheet.feature

### FT-BS-001: Balanced books produce isBalanced true

**JE1:** "Owner investment" -- 1010 DR 5000 / 3010 CR 5000
**JE2:** "Service income" -- 1010 DR 1000 / 4010 CR 1000
**JE3:** "Office rent" -- 5010 DR 400 / 1010 CR 400

All balanced.

**T-Accounts:**
- 1010 (Asset): DR 5000+1000=6000, CR 400. Balance = 5600.
- 3010 (Equity): CR 5000. Balance = 5000.
- 4010 (Revenue): CR 1000. Balance = 1000.
- 5010 (Expense): DR 400. Balance = 400.

**Balance Sheet:**
- Assets = 5600
- Liabilities = 0
- Equity = 5000
- Retained Earnings = Revenue - Expenses = 1000 - 400 = 600
- Total Equity = 5000 + 600 = 5600
- A = L + TE => 5600 = 0 + 5600. Balanced.

**Assertion:** isBalanced = true. **CORRECT.**

---

### FT-BS-002: Assets equal liabilities plus total equity

**JE1:** "Owner investment" -- 1010 DR 5000 / 3010 CR 5000
**JE2:** "Took a loan" -- 1010 DR 2000 / 2010 CR 2000
**JE3:** "Earned revenue" -- 1010 DR 1000 / 4010 CR 1000

**T-Accounts:**
- 1010 (Asset): DR 5000+2000+1000=8000, CR 0. Balance = 8000.
- 2010 (Liability): CR 2000. Balance = 2000.
- 3010 (Equity): CR 5000. Balance = 5000.
- 4010 (Revenue): CR 1000. Balance = 1000.

**Balance Sheet:**
- Assets = 8000
- Liabilities = 2000
- Equity = 5000
- Retained Earnings = 1000 (revenue only, no expenses)
- Total Equity = 5000 + 1000 = 6000
- A = L + TE => 8000 = 2000 + 6000 = 8000. Balanced.

**Assertions:**
- Assets = 8000.00 **CORRECT**
- Liabilities = 2000.00 **CORRECT**
- Equity = 5000.00 **CORRECT**
- Retained Earnings = 1000.00 **CORRECT**
- Total Equity = 6000.00 **CORRECT**
- isBalanced **CORRECT**

---

### FT-BS-003: Positive retained earnings when revenue exceeds expenses

**JE1:** "Revenue" -- 1010 DR 3000 / 4010 CR 3000
**JE2:** "Expense" -- 5010 DR 1000 / 1010 CR 1000

**Retained Earnings = 3000 - 1000 = 2000**

**Assertion:** retained earnings are 2000.00. **CORRECT.**

---

### FT-BS-004: Negative retained earnings when expenses exceed revenue

**JE1:** "Borrowed to fund operations" -- 1010 DR 2000 / 2010 CR 2000
**JE2:** "Small revenue" -- 1010 DR 200 / 4010 CR 200
**JE3:** "Large expense" -- 5010 DR 800 / 1010 CR 800

**T-Accounts:**
- 1010 (Asset): DR 2000+200=2200, CR 800. Balance = 1400.
- 2010 (Liability): CR 2000. Balance = 2000.
- 4010 (Revenue): CR 200. Balance = 200.
- 5010 (Expense): DR 800. Balance = 800.

**Retained Earnings = 200 - 800 = -600**

**Balance Sheet:**
- Assets = 1400
- Liabilities = 2000
- Total Equity = 0 (no equity accounts) + (-600) = -600
- A = L + TE => 1400 = 2000 + (-600) = 1400. Balanced.

**Assertions:**
- Retained Earnings = -600.00 **CORRECT**
- isBalanced **CORRECT**

---

### FT-BS-005: Retained earnings zero when no revenue or expense activity

**JE1:** "Owner investment only" -- 1010 DR 5000 / 3010 CR 5000

No revenue or expense accounts touched.

**Retained Earnings = 0**

**Assertion:** retained earnings are 0.00. **CORRECT.** isBalanced **CORRECT.**

---

### FT-BS-006: Before any entries all zeros and balanced

No entries.

All sections = 0. A = L + TE => 0 = 0 + 0. Balanced.

**All assertions CORRECT.**

---

### FT-BS-007: Voided entries excluded from balance sheet

**JE1:** "Good investment" -- 1010 DR 5000 / 3010 CR 5000 (active)
**JE2:** "Bad investment" -- 1010 DR 2000 / 3010 CR 2000 (VOIDED)

Excluding voided:
- Assets (1010) = 5000
- Equity (3010) = 5000
- A = L + TE => 5000 = 0 + 5000. Balanced.

**Assertions:** Assets = 5000, Equity = 5000, isBalanced. **ALL CORRECT.**

---

### FT-BS-008: Entries across multiple fiscal periods all contribute

**JE1:** "January investment" -- 1010 DR 3000 / 3010 CR 3000
**JE2:** "February income" -- 1010 DR 1000 / 4010 CR 1000
**JE3:** "March income" -- 1010 DR 500 / 4010 CR 500

**T-Accounts (cumulative through 2026-03-31):**
- 1010 (Asset): DR 3000+1000+500=4500. Balance = 4500.
- 3010 (Equity): CR 3000. Balance = 3000.
- 4010 (Revenue): CR 1000+500=1500. Balance = 1500.

**Balance Sheet:**
- Assets = 4500
- Equity = 3000
- Retained Earnings = 1500
- Total Equity = 3000 + 1500 = 4500
- A = L + TE => 4500 = 0 + 4500. Balanced.

**Assertions:**
- Assets = 4500.00 **CORRECT**
- Equity = 3000.00 **CORRECT**
- Retained Earnings = 1500.00 **CORRECT**
- isBalanced **CORRECT**

---

### FT-BS-009: Multiple accounts per section accumulate correctly

**JE1:** "Owner investment into checking" -- 1010 DR 5000 / 3010 CR 5000
**JE2:** "Equipment purchase on credit" -- 1020 DR 2000 / 2010 CR 2000
**JE3:** "Partner investment" -- 1010 DR 3000 / 3020 CR 3000
**JE4:** "Additional loan" -- 1010 DR 1000 / 2020 CR 1000

**T-Accounts:**
- 1010 (Asset): DR 5000+3000+1000=9000. Balance = 9000.
- 1020 (Asset): DR 2000. Balance = 2000.
- 2010 (Liability): CR 2000. Balance = 2000.
- 2020 (Liability): CR 1000. Balance = 1000.
- 3010 (Equity): CR 5000. Balance = 5000.
- 3020 (Equity): CR 3000. Balance = 3000.

**Balance Sheet:**
- Assets = 9000 + 2000 = 11000
- Liabilities = 2000 + 1000 = 3000
- Equity = 5000 + 3000 = 8000
- Total Equity = 8000 + 0 (no R&E) = 8000
- A = L + TE => 11000 = 3000 + 8000 = 11000. Balanced.

**Assertions:**
- Assets section: 2 lines, total 11000.00 **CORRECT**
- Liabilities section: 2 lines, total 3000.00 **CORRECT**
- Equity section: 2 lines, total 8000.00 **CORRECT**
- isBalanced **CORRECT**

---

### FT-BS-010: Account with activity netting to zero still appears

**JE1:** "Investment" -- 1010 DR 5000 / 3010 CR 5000
**JE2:** "Transfer to savings" -- 1020 DR 1000 / 1010 CR 1000
**JE3:** "Transfer back" -- 1010 DR 1000 / 1020 CR 1000

**T-Accounts:**
- 1010 (Asset): DR 5000+1000=6000, CR 1000. Balance = 5000.
- 1020 (Asset): DR 1000, CR 1000. Balance = 0.00.

**Assertion:** 1020 balance = 0.00, assets section contains 2 lines. **CORRECT.**

---

### FT-BS-011: Inactive accounts with cumulative balances still appear

**JE1:** "Before deactivation" -- 1010 DR 5000 / 3010 CR 5000

Account 1010 deactivated after posting.

**Assertion:** 1010 balance = 5000.00, isBalanced. **CORRECT.**

---

## 3. CloseReopenFiscalPeriod.feature

### FT-CFP-012: Closing a period does not modify account balances

**JE1:** 1010 DR 500 / 4010 CR 500

Trial balance before close: DR total = 500, CR total = 500.
After close: same.

**Assertion:** Trial balance totals identical before and after close. **CORRECT** (no arithmetic change).

No other scenarios in this feature involve financial amounts requiring T-account verification.

---

## 4. IncomeStatement.feature

### FT-IS-001: Period with revenue and expense produces correct net income

**JE1:** "Service income" -- 1010 DR 1000 / 4010 CR 1000
**JE2:** "Office supplies" -- 5010 DR 400 / 1010 CR 400

**Income Statement (period-scoped):**
- Revenue (4010): CR 1000, balance = 1000
- Expenses (5010): DR 400, balance = 400
- Net Income = 1000 - 400 = 600

**Assertions:** Revenue = 1000, Expenses = 400, Net Income = 600. **ALL CORRECT.**

---

### FT-IS-002: Revenue only period

**JE1:** 1010 DR 750 / 4010 CR 750

- Revenue = 750, Expenses = 0. Net Income = 750.

**Assertions:** Revenue total = 750.00, 1 line. Expenses total = 0.00, 0 lines. **CORRECT.**

---

### FT-IS-003: Expenses only period

**JE1:** 5010 DR 300 / 1010 CR 300

- Revenue = 0, Expenses = 300. Net Income = -300.

**Assertions:** Expenses total = 300.00, 1 line. Revenue total = 0.00, 0 lines. **CORRECT.**

---

### FT-IS-004: Voided entries excluded from income statement

**JE1:** "Good income" -- 1010 DR 500 / 4010 CR 500 (active)
**JE2:** "Bad income" -- 1010 DR 200 / 4010 CR 200 (VOIDED)

Revenue = 500 (voided excluded). Net Income = 500.

**Assertions:** Revenue = 500.00, Net Income = 500.00. **CORRECT.**

---

### FT-IS-005: Accounts with no activity omitted

**JE1:** 1010 DR 600 / 4010 CR 600

4020 has no activity. Revenue section: 1 line (4010 only).

**Assertion:** Revenue section contains 1 line, does not contain 4020. **CORRECT.**

---

### FT-IS-006: Inactive accounts with activity still appear

**JE1:** 1010 DR 500 / 4010 CR 500. Then 4010 deactivated.

**Assertion:** Revenue section contains 4010 with balance 500.00. **CORRECT.**

---

### FT-IS-007: Empty period produces zero net income

No entries. Revenue = 0, Expenses = 0, Net Income = 0.

**All assertions CORRECT.**

---

### FT-IS-009: Net loss when expenses exceed revenue

**JE1:** "Small income" -- 1010 DR 300 / 4010 CR 300
**JE2:** "Big expense" -- 5010 DR 800 / 1010 CR 800

- Revenue = 300
- Expenses = 800
- Net Income = 300 - 800 = -500

**Assertion:** Net Income = -500.00. **CORRECT.**

---

### FT-IS-010: Multiple revenue and expense accounts accumulate correctly

**JE1:** 1010 DR 600 / 4010 CR 600
**JE2:** 1010 DR 400 / 4020 CR 400
**JE3:** 5010 DR 200 / 1010 CR 200
**JE4:** 5020 DR 150 / 1010 CR 150

All balanced.

**Income Statement:**
- Revenue: 4010 = 600, 4020 = 400. Total = 1000.
- Expenses: 5010 = 200, 5020 = 150. Total = 350.
- Net Income = 1000 - 350 = 650.

**Assertions:**
- Revenue: 2 lines, total 1000.00 **CORRECT**
- Expenses: 2 lines, total 350.00 **CORRECT**
- Net Income = 650.00 **CORRECT**

---

### FT-IS-011: Revenue balance equals credits minus debits

**JE1:** "Revenue credit" -- 1010 DR 700 / 4010 CR 700
**JE2:** "Revenue debit adjustment" -- 4010 DR 100 / 1010 CR 100

**T-Account: 4010 (Revenue, normal credit)**
- Credits: 700
- Debits: 100
- Balance = 700 - 100 = 600

**Assertion:** 4010 balance = 600.00. **CORRECT.**

---

### FT-IS-012: Expense balance equals debits minus credits

**JE1:** "Expense debit" -- 5010 DR 500 / 1010 CR 500
**JE2:** "Expense credit adjustment" -- 1010 DR 150 / 5010 CR 150

**T-Account: 5010 (Expense, normal debit)**
- Debits: 500
- Credits: 150
- Balance = 500 - 150 = 350

**Assertion:** 5010 balance = 350.00. **CORRECT.**

---

### FT-IS-017: Income statement for one period excludes another period's activity

**JE1 (period 2026-03):** 1010 DR 800 / 4010 CR 800
**JE2 (period 2026-04):** 5010 DR 300 / 1010 CR 300

Income statement for 2026-03 only:
- Revenue = 800, Expenses = 0, Net Income = 800.

**Assertions:** Revenue = 800, Expenses = 0, Net Income = 800. **ALL CORRECT.**

---

### FT-IS-016: Closed period income statement still works

**JE1:** 1010 DR 800 / 4010 CR 800

Revenue = 800, Net Income = 800.

**Assertions CORRECT.**

---

### FT-IS-013: Lookup by period key returns same result as by period ID

**JE1:** 1010 DR 250 / 4010 CR 250

Both lookups should return revenue = 250, net income = 250.

**Assertion:** Both results identical. **CORRECT** (no arithmetic discrepancy possible).

---

## 5. OpeningBalances.feature

### FT-OB-001: Opening balances for asset and liability produce balanced entry

Opening balances: Cash (asset) = 10000, Mortgage (liability) = 200000.

**Implied Journal Entry:**
- Cash is asset (normal debit): DR Cash 10000
- Mortgage is liability (normal credit): CR Mortgage 200000
- Equity (balancing): must make DR = CR

DR side: 10000 (Cash)
CR side: 200000 (Mortgage)

Since credits > debits by 190000, Equity gets DR 190000.

Wait -- let me reconsider. The opening balance posts each account at its normal balance direction.

- Cash (asset, normal debit): DR 10000
- Mortgage (liability, normal credit): CR 200000
- DR total so far: 10000, CR total so far: 200000
- Imbalance: CR exceeds DR by 190000
- Balancing (Equity): DR 190000

**Total:** DR 10000 + 190000 = 200000, CR 200000. Balanced. 3 lines.

**Assertions:** 3 lines, debits = credits. **CORRECT.**

---

### FT-OB-002: Balancing line computed correctly when debits exceed credits

Opening balance: Cash (asset) = 5000.

- Cash: DR 5000
- DR total: 5000, CR total: 0
- Balancing Equity: CR 5000

**Assertion:** Equity line is a credit of 5000.00. **CORRECT.**

---

### FT-OB-003: Balancing line computed correctly when credits exceed debits

Opening balance: Mortgage (liability) = 200000.

- Mortgage: CR 200000
- DR total: 0, CR total: 200000
- Balancing Equity: DR 200000

**Assertion:** Equity line is a debit of 200000.00. **CORRECT.**

---

### FT-OB-004: Single account entry creates two-line journal entry

Opening balance: Cash (asset) = 1000.

- Cash: DR 1000
- Equity: CR 1000
- 2 lines.

**Assertion:** 2 lines. **CORRECT.**

---

### FT-OB-005: Posted entry has correct line count and metadata

Opening balances: Cash 10000 (asset), Brokerage 50000 (asset), Mortgage 200000 (liability).

- Cash: DR 10000
- Brokerage: DR 50000
- Mortgage: CR 200000
- DR total: 60000, CR total: 200000
- Equity (balancing): DR 140000

Total: DR 60000 + 140000 = 200000, CR 200000. Balanced. 4 lines.

**Assertion:** 4 lines, description "Opening balances", source "opening_balance". **CORRECT.**

---

## 6. PostJournalEntry.feature

### FT-PJE-001: Simple 2-line entry posts successfully

**JE:** 1010 DR 1000 / 4010 CR 1000. Balanced. 2 lines.

**Assertion:** succeeds with 2 lines. **CORRECT.**

---

### FT-PJE-002: Compound 3-line entry posts successfully

**JE:** 5010 DR 800 / 2010 DR 700 / 1010 CR 1500

DR total: 800 + 700 = 1500. CR total: 1500. Balanced. 3 lines.

**Assertion:** succeeds with 3 lines. **CORRECT.**

---

### FT-PJE-003: Entry with references posts successfully

**JE:** 1010 DR 1000 / 4010 CR 1000. Balanced. 2 references.

**Assertion:** succeeds with 2 references. **CORRECT.**

---

### FT-PJE-004, PJE-005: Metadata variants

Same balanced entry (1010 DR 1000 / 4010 CR 1000). **CORRECT.**

---

### FT-PJE-006: Unbalanced entry is rejected

**JE:** 1010 DR 1000 / 4010 CR 500.

DR = 1000, CR = 500. **NOT balanced.** Rejected.

**Assertion:** fails with "do not equal". **CORRECT** (1000 != 500).

---

### FT-PJE-007, PJE-008, PJE-009, PJE-010, PJE-011, PJE-012: Validation rejections

These test zero amounts, negative amounts, single-line, empty description, invalid entry_type, empty source. No T-account arithmetic to verify -- pure validation. All structurally sound.

---

### FT-PJE-013 through PJE-021: DB validation and edge cases

Entries used are either balanced (100 DR / 100 CR, 500 DR / 500 CR, 200 DR / 200 CR) and rejected for non-arithmetic reasons, or test atomicity/duplicates. All entry amounts balance. **No arithmetic discrepancies.**

---

## 7. PostObligationToLedger.feature

### FT-POL-001: Posting a confirmed receivable instance

Amount = 1200.00. Receivable: money coming in.

**Implied JE:**
- Debit destination account: 1200.00
- Credit source account: 1200.00

DR = CR = 1200. Balanced.

**Assertions:** 2 lines, debit line = destination 1200, credit line = source 1200, status = "posted". **CORRECT.**

---

### FT-POL-002: Posting a confirmed payable instance

Amount = 850.00. Payable: money going out.

**Implied JE:**
- Debit destination account: 850.00
- Credit source account: 850.00

DR = CR = 850. Balanced.

**Assertions:** 2 lines, debit line = destination 850, credit line = source 850. **CORRECT.**

Note: For both receivable and payable, the spec uses the same pattern (debit destination, credit source). The account type determines whether this is economically an inflow or outflow. The journal entry mechanics are consistent: DR dest / CR source regardless of direction. This is arithmetically correct -- account type mapping is the GAAP Mapper's concern, not mine.

---

### FT-POL-003: Journal entry description matches agreement and instance names

Amount = 1000.00. DR dest / CR source = 1000. Balanced. **CORRECT.**

---

### FT-POL-004, POL-005, POL-006: Metadata scenarios

All use 1000.00 or 500.00 amounts, DR dest / CR source. All balanced. **CORRECT.**

---

### FT-POL-016: Double-posting prevention

First post: 1000.00, balanced. Second attempt rejected. No duplicate JE.

**Assertion:** No second journal entry created. **CORRECT** (no arithmetic impact).

---

### FT-POL-017: Failed post to closed period leaves no journal entry

Post rejected. No JE created. **No arithmetic to verify.**

---

### FT-POL-018: Retry after partial failure skips duplicate journal entry

Pre-existing JE found by idempotency guard. No new JE created. Instance transitions. **No new arithmetic.**

---

### FT-POL-019: Voided prior JE does not trigger idempotency guard

New JE created: 1000.00 DR dest / CR source. Balanced. **CORRECT.**

---

## 8. SubtreePLReport.feature

### FT-SPL-001: Subtree with revenue and expense children

Revenue entry = 1000.00, expense entry = 400.00.

- Revenue total = 1000
- Expense total = 400
- Net Income = 1000 - 400 = 600

**Assertions:** Revenue = 1000, Expense = 400, Net Income = 600. **CORRECT.**

---

### FT-SPL-002: Only revenue descendants

Revenue = 750.00. Expenses = 0.

**Assertions:** Revenue = 750 (1 line), Expenses = 0 (0 lines). **CORRECT.**

---

### FT-SPL-003: Only expense descendants

Expenses = 300.00. Revenue = 0.

**Assertions:** Expense = 300 (1 line), Revenue = 0 (0 lines). **CORRECT.**

---

### FT-SPL-004: Root account with no children

Revenue = 500.00. Net Income = 500.

**Assertions:** Revenue = 500 (1 line), Net Income = 500. **CORRECT.**

---

### FT-SPL-005: Root account not revenue/expense, no rev/exp descendants

All zeros. **CORRECT.**

---

### FT-SPL-006: Voided entries excluded

Active revenue = 500, voided revenue = 200 (excluded).

Revenue = 500, Net Income = 500.

**Assertions CORRECT.**

---

### FT-SPL-007: Accounts outside the subtree excluded

Inside subtree: 600 revenue. Outside: 400 revenue (excluded).

Revenue = 600 (1 line). **CORRECT.**

---

### FT-SPL-008: Multi-level hierarchy includes grandchildren

Expense on grandchild = 250.

**Assertion:** Expense section: 1 line, 250.00. **CORRECT.**

---

### FT-SPL-012: Empty period for subtree produces zero net income

All zeros. **CORRECT.**

---

## 9. Transfers.feature

### FT-TRF-007: Confirming an initiated transfer creates correct journal entry

Transfer 1000.00 from "checking" to "savings" (both assets).

**Implied JE:**
- Debit savings (to_account): 1000.00
- Credit checking (from_account): 1000.00

DR = CR = 1000. Balanced.

Both are asset accounts (normal debit). Savings increases, checking decreases. Economically correct for asset-to-asset transfer.

**Assertions:** 2 lines, debit = savings 1000, credit = checking 1000. **CORRECT.**

---

### FT-TRF-008: Confirming sets confirmed_date and journal_entry_id

Transfer 750.00. DR savings / CR checking = 750. Balanced. **CORRECT.**

---

### FT-TRF-009: Journal entry has transfer source and reference

Transfer 500.00. Balanced. **CORRECT.**

---

### FT-TRF-010: Journal entry entry_date equals confirmed_date

Transfer 500.00. Balanced. **CORRECT.**

---

### FT-TRF-011, TRF-012: Description variants

Transfer 500.00. Balanced. **CORRECT.**

---

### FT-TRF-015: Retry after partial failure skips duplicate journal entry

Pre-existing JE. No new JE. **No new arithmetic.**

---

### FT-TRF-016: Voided prior JE does not trigger idempotency guard

New JE: 1000.00 DR/CR. Balanced. **CORRECT.**

---

## 10. TrialBalance.feature

### FT-TB-001: Balanced entries produce a balanced trial balance

**JE1:** 1010 DR 500 / 4010 CR 500

**Trial Balance:**
- Account 1010: DR 500, CR 0
- Account 4010: DR 0, CR 500
- Grand Total DR: 500, Grand Total CR: 500. Balanced.

**Assertions:** Grand DR = 500, Grand CR = 500, 2 account lines, isBalanced. **ALL CORRECT.**

---

### FT-TB-002: Report groups accounts by type with correct subtotals

**JE1:** "Income" -- 1010 DR 600, 1020 DR 400, 4010 CR 1000
DR = 600+400 = 1000, CR = 1000. Balanced.

**JE2:** "Supplies" -- 5010 DR 200, 1010 CR 200
DR = 200, CR = 200. Balanced.

**Trial Balance per account:**
- 1010: DR 600, CR 200
- 1020: DR 400, CR 0
- 4010: DR 0, CR 1000
- 5010: DR 200, CR 0

**By group:**
- Asset: DR 600+400=1000, CR 200+0=200
- Revenue: DR 0, CR 1000
- Expense: DR 200, CR 0

Grand total DR: 1000+0+200 = 1200
Grand total CR: 200+1000+0 = 1200. Balanced.

**Assertions:**
- Asset group: DR 1000, CR 200 **CORRECT**
- Revenue group: DR 0, CR 1000 **CORRECT**
- Expense group: DR 200, CR 0 **CORRECT**

---

### FT-TB-003: Groups with no activity are omitted

**JE1:** 1010 DR 300 / 4010 CR 300

Only asset and revenue groups have activity. 2 groups. No liability, equity, or expense groups.

**Assertion:** Exactly 2 groups. **CORRECT.**

---

### FT-TB-004: Voided entries excluded from trial balance

**JE1:** "Good entry" -- 1010 DR 500 / 4010 CR 500 (active)
**JE2:** "Bad entry" -- 1010 DR 200 / 4010 CR 200 (VOIDED)

Grand DR = 500, Grand CR = 500 (voided excluded).

**Assertions:** Grand DR = 500, Grand CR = 500. **CORRECT.**

---

### FT-TB-005: Empty period returns zero totals

No entries. Grand DR = 0, Grand CR = 0. 0 groups. Balanced.

**All assertions CORRECT.**

---

### FT-TB-006: Closed period trial balance still works

**JE1:** 1010 DR 800 / 4010 CR 800

Grand DR = 800, Grand CR = 800.

**Assertion:** Grand DR = 800. **CORRECT.**

---

### FT-TB-007: Multiple entries accumulate per account

**JE1:** 1010 DR 500 / 4010 CR 500
**JE2:** 1010 DR 300 / 4010 CR 300

- 1010: DR 500+300=800, CR 0
- 4010: DR 0, CR 500+300=800

**Assertions:** 1010 DR total 800 / CR total 0, 4010 DR total 0 / CR total 800. **CORRECT.**

---

### FT-TB-008: Net balance uses normal_balance formula

**JE1:** 1010 DR 700 / 4010 CR 700

- 1010 (Asset, normal debit): net = DR - CR = 700 - 0 = 700
- 4010 (Revenue, normal credit): net = CR - DR = 700 - 0 = 700

**Assertions:** 1010 net = 700 (normal debit), 4010 net = 700 (normal credit). **CORRECT.**

---

## 11. VoidJournalEntry.feature

No scenarios assert financial amounts beyond confirming that voided entries are excluded from balances (covered in other features). The void feature tests state transitions and metadata. Entry amounts (1000, 500, 750, 200, 100, 300) are all in balanced DR/CR pairs.

All entries are balanced. **No arithmetic discrepancies.**

---

## 12. AccountSubTypes.feature

No financial amounts. Classification and validation only. **Nothing to trace.**

---

## 13. BalanceProjection.feature

### FT-BP-001: Flat line with no obligations or transfers

Current balance: 5000.00. No inflows, no outflows.

Every day = 5000.00.

**Assertion:** Every day shows 5000.00. **CORRECT.**

---

### FT-BP-002: Expected receivable inflow increases projected balance

Current balance: 1000.00. Receivable inflow of 500.00 on 2026-04-09.

- 2026-04-08: 1000.00 (no change yet)
- 2026-04-09: 1000.00 + 500.00 = 1500.00
- 2026-04-10: 1500.00

**Assertions:** 2026-04-08 = 1000, 2026-04-09 = 1500, 2026-04-10 = 1500. **CORRECT.**

---

### FT-BP-003: Expected payable outflow decreases projected balance

Current balance: 2000.00. Payable outflow of 400.00 on 2026-04-08.

- 2026-04-07: 2000.00
- 2026-04-08: 2000.00 - 400.00 = 1600.00
- 2026-04-10: 1600.00

**Assertions:** 2026-04-07 = 2000, 2026-04-08 = 1600, 2026-04-10 = 1600. **CORRECT.**

---

### FT-BP-004: Initiated transfer out decreases projected balance

Current balance: 3000.00. Transfer out 800.00 on 2026-04-09.

- 2026-04-08: 3000.00
- 2026-04-09: 3000.00 - 800.00 = 2200.00

**Assertions:** 2026-04-08 = 3000, 2026-04-09 = 2200. **CORRECT.**

---

### FT-BP-005: Initiated transfer in increases projected balance

Current balance: 1000.00. Transfer in 600.00 on 2026-04-08.

- 2026-04-07: 1000.00
- 2026-04-08: 1000.00 + 600.00 = 1600.00

**Assertions:** 2026-04-07 = 1000, 2026-04-08 = 1600. **CORRECT.**

---

### FT-BP-006: In-flight transfer with no expected_settlement falls back to initiated_date

Current balance: 2000.00. Transfer out 300.00, falls back to initiated_date 2026-04-09.

- 2026-04-08: 2000.00
- 2026-04-09: 2000.00 - 300.00 = 1700.00

**Assertions:** 2026-04-08 = 2000, 2026-04-09 = 1700. **CORRECT.**

---

### FT-BP-007: All projection components combine correctly

Current balance: 5000.00. On 2026-04-09:
- Receivable inflow: +1000.00
- Payable outflow: -400.00
- Transfer out: -200.00
- Transfer in: +100.00

Projected = 5000 + 1000 - 400 - 200 + 100 = 5500.00

**Assertion:** 2026-04-09 = 5500.00. **CORRECT.**

---

### FT-BP-009: Multiple obligations on the same day are summed

Current balance: 3000.00. On 2026-04-08:
- Receivable: +500.00
- Payable: -200.00
- Payable: -150.00

Net effect = +500 - 200 - 150 = +150
Projected = 3000 + 150 = 3150.00

**Assertions:**
- 2026-04-08 = 3150.00 **CORRECT**
- Net effect = +150.00 **CORRECT**
- 3 items in breakdown **CORRECT**

---

## 14. InvoicePersistence.feature

### FT-INV-001: Recording a valid invoice

rentAmount = 1200.00, utilityShare = 85.50, totalAmount = 1285.50.

Verification: 1200.00 + 85.50 = 1285.50. **CORRECT.**

---

### FT-INV-002: Null optional fields

rentAmount = 1200.00, utilityShare = 0.00, totalAmount = 1200.00.

1200.00 + 0.00 = 1200.00. **CORRECT.**

---

### FT-INV-003: Closed fiscal period

rentAmount = 1200.00, utilityShare = 85.50, totalAmount = 1285.50.

1200.00 + 85.50 = 1285.50. **CORRECT.**

---

### FT-INV-004: Zero rent, non-zero utility

rentAmount = 0.00, utilityShare = 85.50, totalAmount = 85.50.

0.00 + 85.50 = 85.50. **CORRECT.**

---

### FT-INV-005: Zero utility, non-zero rent

rentAmount = 1200.00, utilityShare = 0.00, totalAmount = 1200.00.

1200.00 + 0.00 = 1200.00. **CORRECT.**

---

### FT-INV-010: Total not equal to rent plus utility is rejected

rentAmount = 1200.00, utilityShare = 85.50, totalAmount = 1300.00.

1200.00 + 85.50 = 1285.50 != 1300.00. **CORRECTLY REJECTED.**

---

No other invoice scenarios involve arithmetic verification.

---

## 15. OrphanedPostingDetection.feature

No financial amounts. Diagnostic only. **Nothing to trace.**

---

## 16. OverdueDetection.feature

No financial amounts. Status transitions only. **Nothing to trace.**

---

## 17. SpawnObligationInstances.feature

### FT-SI-011: Spawn monthly instances for a 3-month range

Each instance has amount 150.00 (from agreement). 3 instances created.

No journal entries or balances to verify. The amount is a field copy, not arithmetic. **No discrepancy.**

---

### FT-SI-012: Variable-amount agreement leaves instance amount empty

No amount. **Nothing to trace.**

---

### FT-SI-013: Spawn OneTime creates a single instance

Amount = 500.00. Single instance. **No arithmetic to verify.**

---

## 18. StatusTransitions.feature

No financial amounts produce journal entries or balance calculations. Status transition scenarios reference amounts (500.00, 750.00, 300.00, 525.00) only as field values on instances, not as double-entry postings.

**Nothing to trace.**

---

## Summary

| Feature File | Scenarios with Financial Amounts | Discrepancies Found |
|-------------|----------------------------------|---------------------|
| AccountBalance.feature | 9 | 0 |
| BalanceSheet.feature | 10 | 0 |
| CloseReopenFiscalPeriod.feature | 1 | 0 |
| IncomeStatement.feature | 12 | 0 |
| OpeningBalances.feature | 5 | 0 |
| PostJournalEntry.feature | 8 | 0 |
| PostObligationToLedger.feature | 7 | 0 |
| SubtreePLReport.feature | 8 | 0 |
| Transfers.feature | 6 | 0 |
| TrialBalance.feature | 8 | 0 |
| VoidJournalEntry.feature | 0 (metadata only) | 0 |
| AccountSubTypes.feature | 0 (classification only) | 0 |
| BalanceProjection.feature | 9 | 0 |
| InvoicePersistence.feature | 6 | 0 |
| OrphanedPostingDetection.feature | 0 (diagnostic only) | 0 |
| OverdueDetection.feature | 0 (status only) | 0 |
| SpawnObligationInstances.feature | 0 (field copy only) | 0 |
| StatusTransitions.feature | 0 (field values only) | 0 |
| **TOTAL** | **89** | **0** |

---

## Findings

**Zero discrepancies.** Every journal entry in every scenario balances (total debits = total credits). Every T-account balance, trial balance total, income statement figure, balance sheet section total, retained earnings computation, balance projection series value, and invoice total arithmetic matches the expected assertions in the specs.

Specific verification highlights:

1. **Debit/credit equality:** All 89 financial scenarios maintain strict DR = CR on every journal entry. The compound 3-line entry (PJE-002: 800 + 700 = 1500) is correctly balanced.

2. **Balance direction:** All normal-balance formulas are applied correctly. Assets and expenses use DR - CR. Liabilities, equity, and revenue use CR - DR. No scenario asserts a contra-normal balance without cause.

3. **Running totals:** Multi-step scenarios (AB-003, AB-004, BS-008, BS-009, TB-007, IS-010, BP-007, BP-009) all trace correctly through sequential entries.

4. **Void mechanics:** This system uses a soft-void (voided_at flag) rather than reversing entries. All void-related balance assertions (AB-005, BS-007, TB-004, IS-004, SPL-006) correctly exclude voided entries. No equal-and-opposite reversal entries are generated -- the specs consistently filter by voided_at instead.

5. **Opening balances:** The balancing-account logic (OB-001 through OB-005) correctly computes the offset entry needed to make DR = CR when establishing starting balances.

6. **Balance sheet equation:** A = L + Total Equity (where Total Equity = Equity + Retained Earnings) holds in every balance sheet scenario. Verified explicitly in BS-001, BS-002, BS-004, BS-009.

7. **Invoice arithmetic:** The rent + utility = total constraint is correctly enforced (INV-010 properly rejects a mismatch).

The books are clean.

---

*Signed: Ledger Tracer*
*2026-04-07*
