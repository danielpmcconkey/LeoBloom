# Ledger Tracer Report

**Date:** 2026-04-05
**Scope:** All Gherkin specs with financial amounts across Behavioral and Ops features
**Method:** Extract implied journal entries, build T-accounts, verify arithmetic against assertions

---

## Account Type Reference (from Ledger.fs)

| Account Type | Normal Balance | Debit increases | Credit increases |
|---|---|---|---|
| Asset | Debit | Yes | No |
| Liability | Credit | No | Yes |
| Equity | Credit | No | Yes |
| Revenue | Credit | No | Yes |
| Expense | Debit | Yes | No |

**Net balance formula:**
- Normal-debit accounts: net = debits - credits
- Normal-credit accounts: net = credits - debits

---

## Feature: Trial Balance

### @FT-TB-001 — Balanced entries produce a balanced trial balance

**Journal Entry:** "March rent" 2026-03-15

| Account | Debit | Credit |
|---|---|---|
| 1010 (asset) | 500.00 | |
| 4010 (revenue) | | 500.00 |

**Debit/credit equality:** 500.00 = 500.00. Balanced.

**T-Accounts:**

```
    1010 (Asset)              4010 (Revenue)
  Dr     |  Cr             Dr     |  Cr
 500.00  |                        | 500.00
```

**Assertions:**
- Grand total debits = 500.00. Computed: 500.00. CORRECT.
- Grand total credits = 500.00. Computed: 500.00. CORRECT.
- Report contains 2 account lines. Computed: 2 (1010, 4010). CORRECT.
- Trial balance is balanced. CORRECT.

---

### @FT-TB-002 — Report groups accounts by type with correct subtotals

**Entry 1:** "Income" 2026-03-10

| Account | Debit | Credit |
|---|---|---|
| 1010 (asset) | 600.00 | |
| 1020 (asset) | 400.00 | |
| 4010 (revenue) | | 1000.00 |

Debit/credit equality: 600.00 + 400.00 = 1000.00 = 1000.00. Balanced.

**Entry 2:** "Supplies" 2026-03-20

| Account | Debit | Credit |
|---|---|---|
| 5010 (expense) | 200.00 | |
| 1010 (asset) | | 200.00 |

Debit/credit equality: 200.00 = 200.00. Balanced.

**T-Accounts:**

```
    1010 (Asset)              1020 (Asset)
  Dr     |  Cr             Dr     |  Cr
 600.00  | 200.00          400.00 |
 --------|--------         --------|--------
 Totals: 600 Dr, 200 Cr   Totals: 400 Dr, 0 Cr

    4010 (Revenue)            5010 (Expense)
  Dr     |  Cr             Dr     |  Cr
         | 1000.00         200.00 |
 --------|--------         --------|--------
 Totals: 0 Dr, 1000 Cr    Totals: 200 Dr, 0 Cr
```

**Group subtotals:**
- Asset group: debit total = 600 + 400 = 1000.00, credit total = 200.00
- Revenue group: debit total = 0.00, credit total = 1000.00
- Expense group: debit total = 200.00, credit total = 0.00

**Assertions:**
- Asset group debit 1000.00, credit 200.00. CORRECT.
- Revenue group debit 0.00, credit 1000.00. CORRECT.
- Expense group debit 200.00, credit 0.00. CORRECT.
- Groups in order: asset, revenue, expense. (Order assertion, not arithmetic.)

---

### @FT-TB-003 — Groups with no activity are omitted

**Entry:** "Simple entry" 2026-03-15

| Account | Debit | Credit |
|---|---|---|
| 1010 (asset) | 300.00 | |
| 4010 (revenue) | | 300.00 |

Debit/credit equality: 300.00 = 300.00. Balanced.

**Assertions:**
- Exactly 2 groups (asset, revenue). Only those accounts have activity. CORRECT.
- No group for liability, equity, or expense. CORRECT.

---

### @FT-TB-004 — Voided entries excluded from trial balance

**Entry 1:** "Good entry" 2026-03-10

| Account | Debit | Credit |
|---|---|---|
| 1010 (asset) | 500.00 | |
| 4010 (revenue) | | 500.00 |

**Entry 2:** "Bad entry" 2026-03-15 — VOIDED

| Account | Debit | Credit |
|---|---|---|
| 1010 (asset) | 200.00 | |
| 4010 (revenue) | | 200.00 |

Voided entry excluded from totals.

**Assertions:**
- Grand total debits = 500.00 (only "Good entry"). CORRECT.
- Grand total credits = 500.00. CORRECT.

---

### @FT-TB-005 — Empty period returns balanced report with zero totals

No entries. All zeros.

**Assertions:**
- Grand total debits = 0.00. CORRECT.
- Grand total credits = 0.00. CORRECT.
- 0 groups. CORRECT.
- Balanced (0 = 0). CORRECT.

---

### @FT-TB-006 — Closed period trial balance still works

**Entry:** "December entry" 2025-12-15

| Account | Debit | Credit |
|---|---|---|
| 1010 (asset) | 800.00 | |
| 4010 (revenue) | | 800.00 |

Period is closed. Trial balance is read-only, so closure doesn't affect it.

**Assertions:**
- Balanced. 800.00 = 800.00. CORRECT.
- Grand total debits = 800.00. CORRECT.

---

### @FT-TB-007 — Multiple entries in same period accumulate per account

**Entry 1:** "First payment" 2026-03-10

| Account | Debit | Credit |
|---|---|---|
| 1010 (asset) | 500.00 | |
| 4010 (revenue) | | 500.00 |

**Entry 2:** "Second payment" 2026-03-20

| Account | Debit | Credit |
|---|---|---|
| 1010 (asset) | 300.00 | |
| 4010 (revenue) | | 300.00 |

**T-Accounts:**

```
    1010 (Asset)              4010 (Revenue)
  Dr     |  Cr             Dr     |  Cr
 500.00  |                        | 500.00
 300.00  |                        | 300.00
 --------|--------         --------|--------
 800.00  |  0.00            0.00  | 800.00
```

**Assertions:**
- Account 1010: debit total 800.00, credit total 0.00. CORRECT.
- Account 4010: debit total 0.00, credit total 800.00. CORRECT.

---

### @FT-TB-008 — Net balance uses normal_balance formula

**Entry:** "Net test" 2026-03-15

| Account | Debit | Credit |
|---|---|---|
| 1010 (asset) | 700.00 | |
| 4010 (revenue) | | 700.00 |

**Net balances:**
- 1010 (normal-debit): net = 700.00 - 0.00 = 700.00
- 4010 (normal-credit): net = 700.00 - 0.00 = 700.00

**Assertions:**
- Account 1010 net balance 700.00 as normal-debit. CORRECT.
- Account 4010 net balance 700.00 as normal-credit. CORRECT.

---

### @FT-TB-009 — Lookup by period key returns same result as by period ID

**Entry:** "Lookup test" 2026-03-15. 250.00 Dr/Cr. Balanced. Equivalence assertion, not arithmetic.

---

### @FT-TB-010, @FT-TB-011 — Nonexistent period ID/key returns error

No financial amounts. Skipped.

---

## Feature: Close / Reopen Fiscal Period

### @FT-CFP-010 — Posting is rejected after closing a period via closePeriod

**Attempted entry:** 100.00 debit 1010 / 100.00 credit 4010. Balanced entry (100 = 100), but posting is rejected because the period is closed.

No T-accounts to build — the entry never posts. The debit/credit equality of the attempted entry is sound: 100.00 = 100.00.

All other scenarios in this feature involve no financial amounts. Skipped.

---

## Feature: Balance Sheet

### @FT-BS-001 — Balanced books produce isBalanced true

**Entry 1:** "Owner investment" 2026-03-05

| Account | Debit | Credit |
|---|---|---|
| 1010 (asset) | 5000.00 | |
| 3010 (equity) | | 5000.00 |

**Entry 2:** "Service income" 2026-03-10

| Account | Debit | Credit |
|---|---|---|
| 1010 (asset) | 1000.00 | |
| 4010 (revenue) | | 1000.00 |

**Entry 3:** "Office rent" 2026-03-20

| Account | Debit | Credit |
|---|---|---|
| 5010 (expense) | 400.00 | |
| 1010 (asset) | | 400.00 |

All three entries balanced individually: 5000=5000, 1000=1000, 400=400.

**T-Accounts:**

```
    1010 (Asset)                      3010 (Equity)
  Dr      |  Cr                     Dr     |  Cr
 5000.00  |  400.00                        | 5000.00
 1000.00  |
 ---------|--------                 --------|--------
 Net: 5600.00                       Net: 5000.00

    4010 (Revenue)                    5010 (Expense)
  Dr     |  Cr                     Dr     |  Cr
         | 1000.00                 400.00  |
 --------|--------                 --------|--------
 Net: 1000.00                      Net: 400.00
```

**Balance Sheet:**
- Assets = 5600.00
- Liabilities = 0.00
- Equity (direct) = 5000.00
- Retained Earnings = Revenue - Expenses = 1000.00 - 400.00 = 600.00
- Total Equity = 5000.00 + 600.00 = 5600.00
- Assets (5600.00) = Liabilities (0.00) + Total Equity (5600.00). Balanced.

**Assertion:** Balance sheet is balanced. CORRECT.

---

### @FT-BS-002 — Assets equal liabilities plus total equity

**Entry 1:** "Owner investment" — 1010 Dr 5000 / 3010 Cr 5000
**Entry 2:** "Took a loan" — 1010 Dr 2000 / 2010 Cr 2000
**Entry 3:** "Earned revenue" — 1010 Dr 1000 / 4010 Cr 1000

All balanced (5000=5000, 2000=2000, 1000=1000).

**T-Accounts:**

```
    1010 (Asset)         2010 (Liability)     3010 (Equity)      4010 (Revenue)
  Dr      |  Cr        Dr     |  Cr        Dr     |  Cr        Dr     |  Cr
 5000.00  |                   | 2000.00           | 5000.00           | 1000.00
 2000.00  |
 1000.00  |
 ---------|---         -------|--------    -------|--------    -------|--------
 Net: 8000.00          Net: 2000.00       Net: 5000.00       Net: 1000.00
```

**Balance Sheet:**
- Assets = 8000.00
- Liabilities = 2000.00
- Equity (direct) = 5000.00
- Retained Earnings = 1000.00 - 0.00 = 1000.00
- Total Equity = 5000.00 + 1000.00 = 6000.00
- Check: 8000.00 = 2000.00 + 6000.00. Balanced.

**Assertions:**
- Assets section total 8000.00. CORRECT.
- Liabilities section total 2000.00. CORRECT.
- Equity section total 5000.00. CORRECT.
- Retained earnings 1000.00. CORRECT.
- Total equity 6000.00. CORRECT.
- Balanced. CORRECT.

---

### @FT-BS-003 — Positive retained earnings when revenue exceeds expenses

**Entry 1:** "Revenue" — 1010 Dr 3000 / 4010 Cr 3000
**Entry 2:** "Expense" — 5010 Dr 1000 / 1010 Cr 1000

**T-Accounts:**

```
    1010 (Asset)         4010 (Revenue)       5010 (Expense)
  Dr      |  Cr        Dr     |  Cr        Dr      |  Cr
 3000.00  | 1000.00           | 3000.00    1000.00  |
 ---------|--------    -------|--------    ---------|--------
 Net: 2000.00          Net: 3000.00       Net: 1000.00
```

**Balance Sheet:**
- Assets = 2000.00
- Retained Earnings = 3000.00 - 1000.00 = 2000.00
- Total Equity = 0.00 + 2000.00 = 2000.00
- Check: 2000.00 = 0.00 + 2000.00. Balanced.

**Assertion:** Retained earnings 2000.00. CORRECT.

---

### @FT-BS-004 — Negative retained earnings when expenses exceed revenue

**Entry 1:** "Borrowed to fund operations" — 1010 Dr 2000 / 2010 Cr 2000
**Entry 2:** "Small revenue" — 1010 Dr 200 / 4010 Cr 200
**Entry 3:** "Large expense" — 5010 Dr 800 / 1010 Cr 800

All balanced (2000=2000, 200=200, 800=800).

**T-Accounts:**

```
    1010 (Asset)         2010 (Liability)     4010 (Revenue)     5010 (Expense)
  Dr      |  Cr        Dr     |  Cr        Dr     |  Cr        Dr     |  Cr
 2000.00  |  800.00           | 2000.00           | 200.00     800.00 |
  200.00  |
 ---------|--------    -------|--------    -------|--------    -------|--------
 Net: 1400.00          Net: 2000.00       Net: 200.00        Net: 800.00
```

**Balance Sheet:**
- Assets = 1400.00
- Liabilities = 2000.00
- Equity (direct) = 0.00
- Retained Earnings = 200.00 - 800.00 = -600.00
- Total Equity = 0.00 + (-600.00) = -600.00
- Check: 1400.00 = 2000.00 + (-600.00) = 1400.00. Balanced.

**Assertions:**
- Retained earnings -600.00. CORRECT.
- Balanced. CORRECT.

---

### @FT-BS-005 — Retained earnings zero when no revenue or expense activity

**Entry:** "Owner investment only" — 1010 Dr 5000 / 3010 Cr 5000

No revenue, no expense accounts touched. Retained Earnings = 0 - 0 = 0.00.

**Balance Sheet:**
- Assets = 5000.00
- Equity (direct) = 5000.00
- Retained Earnings = 0.00
- Total Equity = 5000.00
- Check: 5000.00 = 0.00 + 5000.00. Balanced.

**Assertions:**
- Retained earnings 0.00. CORRECT.
- Balanced. CORRECT.

---

### @FT-BS-006 — Before any entries all zeros and balanced

No entries. Everything is 0.00. 0 = 0 + 0. Balanced.

**Assertions:** All zeros, balanced, 0 lines in each section. CORRECT.

---

### @FT-BS-007 — Voided entries excluded from balance sheet

**Entry 1:** "Good investment" — 1010 Dr 5000 / 3010 Cr 5000
**Entry 2:** "Bad investment" — 1010 Dr 2000 / 3010 Cr 2000 — VOIDED

Only Entry 1 counts.

**Balance Sheet:**
- Assets = 5000.00
- Equity = 5000.00
- Check: 5000.00 = 5000.00. Balanced.

**Assertions:**
- Assets section total 5000.00. CORRECT.
- Equity section total 5000.00. CORRECT.
- Balanced. CORRECT.

---

### @FT-BS-008 — Entries across multiple fiscal periods all contribute

**Entry 1 (period 2026-01):** "January investment" — 1010 Dr 3000 / 3010 Cr 3000
**Entry 2 (period 2026-02):** "February income" — 1010 Dr 1000 / 4010 Cr 1000
**Entry 3 (period 2026-03):** "March income" — 1010 Dr 500 / 4010 Cr 500

**T-Accounts (cumulative through 2026-03-31):**

```
    1010 (Asset)         3010 (Equity)        4010 (Revenue)
  Dr      |  Cr        Dr     |  Cr        Dr     |  Cr
 3000.00  |                   | 3000.00           | 1000.00
 1000.00  |                                       |  500.00
  500.00  |
 ---------|--------    -------|--------    -------|--------
 Net: 4500.00          Net: 3000.00       Net: 1500.00
```

**Balance Sheet:**
- Assets = 4500.00
- Equity (direct) = 3000.00
- Retained Earnings = 1500.00 - 0.00 = 1500.00
- Total Equity = 3000.00 + 1500.00 = 4500.00
- Check: 4500.00 = 0.00 + 4500.00. Balanced.

**Assertions:**
- Assets section total 4500.00. CORRECT.
- Equity section total 3000.00. CORRECT.
- Retained earnings 1500.00. CORRECT.
- Balanced. CORRECT.

---

### @FT-BS-009 — Multiple accounts per section accumulate correctly

**Entry 1:** "Owner investment into checking" — 1010 Dr 5000 / 3010 Cr 5000
**Entry 2:** "Equipment purchase on credit" — 1020 Dr 2000 / 2010 Cr 2000
**Entry 3:** "Partner investment" — 1010 Dr 3000 / 3020 Cr 3000
**Entry 4:** "Additional loan" — 1010 Dr 1000 / 2020 Cr 1000

All balanced (5000=5000, 2000=2000, 3000=3000, 1000=1000).

**T-Accounts:**

```
    1010 (Asset)         1020 (Asset)         2010 (Liability)
  Dr      |  Cr        Dr      |  Cr        Dr     |  Cr
 5000.00  |            2000.00 |                   | 2000.00
 3000.00  |
 1000.00  |
 ---------|--------    --------|--------    -------|--------
 Net: 9000.00          Net: 2000.00        Net: 2000.00

    2020 (Liability)     3010 (Equity)        3020 (Equity)
  Dr     |  Cr        Dr     |  Cr        Dr     |  Cr
         | 1000.00           | 5000.00           | 3000.00
 -------|--------    -------|--------    -------|--------
 Net: 1000.00        Net: 5000.00        Net: 3000.00
```

**Balance Sheet:**
- Assets = 9000.00 + 2000.00 = 11000.00
- Liabilities = 2000.00 + 1000.00 = 3000.00
- Equity (direct) = 5000.00 + 3000.00 = 8000.00
- Retained Earnings = 0.00 (no revenue/expense)
- Total Equity = 8000.00 + 0.00 = 8000.00
- Check: 11000.00 = 3000.00 + 8000.00 = 11000.00. Balanced.

**Assertions:**
- Assets section: 2 lines, total 11000.00. CORRECT.
- Liabilities section: 2 lines, total 3000.00. CORRECT.
- Equity section: 2 lines, total 8000.00. CORRECT.
- Balanced. CORRECT.

---

### @FT-BS-010 — Account with activity netting to zero still appears with balance zero

**Entry 1:** "Investment" — 1010 Dr 5000 / 3010 Cr 5000
**Entry 2:** "Transfer to savings" — 1020 Dr 1000 / 1010 Cr 1000
**Entry 3:** "Transfer back" — 1010 Dr 1000 / 1020 Cr 1000

**T-Accounts:**

```
    1010 (Asset)         1020 (Asset)         3010 (Equity)
  Dr      |  Cr        Dr      |  Cr        Dr     |  Cr
 5000.00  | 1000.00    1000.00 | 1000.00           | 5000.00
 1000.00  |
 ---------|--------    --------|--------    -------|--------
 Net: 5000.00          Net: 0.00           Net: 5000.00
```

**Assertions:**
- Account 1020 balance 0.00. Computed: 1000 - 1000 = 0.00. CORRECT.
- Assets section contains 2 lines (1010 and 1020, because 1020 had activity). CORRECT.

---

### @FT-BS-011 — Inactive accounts with cumulative balances still appear

**Entry:** "Before deactivation" — 1010 Dr 5000 / 3010 Cr 5000

Account 1010 deactivated after entry posted. Balance still = 5000.00.

**Assertions:**
- Account 1010 balance 5000.00. CORRECT.
- Balanced: 5000 = 5000. CORRECT.

---

## Feature: Income Statement

### @FT-IS-001 — Period with revenue and expense activity produces correct net income

**Entry 1:** "Service income" — 1010 Dr 1000 / 4010 Cr 1000
**Entry 2:** "Office supplies" — 5010 Dr 400 / 1010 Cr 400

**T-Accounts (revenue/expense only):**

```
    4010 (Revenue)       5010 (Expense)
  Dr     |  Cr        Dr     |  Cr
         | 1000.00    400.00 |
 -------|--------    -------|--------
 Net: 1000.00        Net: 400.00
```

- Revenue total = 1000.00
- Expense total = 400.00
- Net Income = 1000.00 - 400.00 = 600.00

**Assertions:**
- Revenue section total 1000.00. CORRECT.
- Expenses section total 400.00. CORRECT.
- Net income 600.00. CORRECT.

---

### @FT-IS-002 — Revenue only period

**Entry:** "Revenue only" — 1010 Dr 750 / 4010 Cr 750

Revenue = 750.00. Expenses = 0.00. Net income = 750.00.

**Assertions:**
- Revenue section total 750.00. CORRECT.
- Revenue section 1 line. CORRECT.
- Expenses section total 0.00. CORRECT.
- Expenses section 0 lines. CORRECT.

---

### @FT-IS-003 — Expenses only period

**Entry:** "Expense only" — 5010 Dr 300 / 1010 Cr 300

Revenue = 0.00. Expenses = 300.00. Net income = -300.00.

**Assertions:**
- Expenses section total 300.00. CORRECT.
- Expenses section 1 line. CORRECT.
- Revenue section total 0.00. CORRECT.
- Revenue section 0 lines. CORRECT.

(Note: net income not asserted here, but would be -300.00.)

---

### @FT-IS-004 — Voided entries excluded from income statement

**Entry 1:** "Good income" — 1010 Dr 500 / 4010 Cr 500
**Entry 2:** "Bad income" — 1010 Dr 200 / 4010 Cr 200 — VOIDED

Only Entry 1 counts. Revenue = 500.00.

**Assertions:**
- Revenue section total 500.00. CORRECT.
- Net income 500.00. CORRECT.

---

### @FT-IS-005 — Accounts with no activity omitted from sections

**Entry:** "One revenue only" — 1010 Dr 600 / 4010 Cr 600

Account 4020 (revenue) has no activity. Only 4010 appears.

**Assertions:**
- Revenue section contains 1 line. CORRECT.
- Revenue section does not contain 4020. CORRECT.

---

### @FT-IS-006 — Inactive accounts with activity still appear

**Entry:** "Before deactivation" — 1010 Dr 500 / 4010 Cr 500

Account 4010 deactivated after posting. Still has 500.00 revenue in the period.

**Assertion:** Revenue section contains 4010 with balance 500.00. CORRECT.

---

### @FT-IS-007 — Empty period produces zero net income

No entries. Revenue = 0, Expenses = 0, Net Income = 0.

**Assertions:** All zeros, 0 lines. CORRECT.

---

### @FT-IS-008 — Net income is positive when revenue exceeds expenses

**Entry 1:** "Big income" — 1010 Dr 2000 / 4010 Cr 2000
**Entry 2:** "Small expense" — 5010 Dr 500 / 1010 Cr 500

Net Income = 2000.00 - 500.00 = 1500.00.

**Assertion:** Net income 1500.00. CORRECT.

---

### @FT-IS-009 — Net loss when expenses exceed revenue

**Entry 1:** "Small income" — 1010 Dr 300 / 4010 Cr 300
**Entry 2:** "Big expense" — 5010 Dr 800 / 1010 Cr 800

Net Income = 300.00 - 800.00 = -500.00.

**Assertion:** Net income -500.00. CORRECT.

---

### @FT-IS-010 — Multiple revenue and expense accounts accumulate correctly

**Entry 1:** "Sales revenue" — 1010 Dr 600 / 4010 Cr 600
**Entry 2:** "Service revenue" — 1010 Dr 400 / 4020 Cr 400
**Entry 3:** "Rent expense" — 5010 Dr 200 / 1010 Cr 200
**Entry 4:** "Utility expense" — 5020 Dr 150 / 1010 Cr 150

**T-Accounts (revenue/expense):**

```
    4010 (Revenue)       4020 (Revenue)       5010 (Expense)       5020 (Expense)
  Dr     |  Cr        Dr     |  Cr        Dr     |  Cr        Dr     |  Cr
         | 600.00            | 400.00     200.00 |             150.00 |
 -------|--------    -------|--------    -------|--------    -------|--------
 Net: 600.00         Net: 400.00        Net: 200.00        Net: 150.00
```

- Revenue total = 600 + 400 = 1000.00
- Expense total = 200 + 150 = 350.00
- Net Income = 1000.00 - 350.00 = 650.00

**Assertions:**
- Revenue section: 2 lines, total 1000.00. CORRECT.
- Expenses section: 2 lines, total 350.00. CORRECT.
- Net income 650.00. CORRECT.

---

### @FT-IS-011 — Revenue balance equals credits minus debits

**Entry 1:** "Revenue credit" — 1010 Dr 700 / 4010 Cr 700
**Entry 2:** "Revenue debit adjustment" — 4010 Dr 100 / 1010 Cr 100

**T-Account for 4010 (Revenue, normal-credit):**

```
    4010 (Revenue)
  Dr     |  Cr
 100.00  | 700.00
 --------|--------
 Net (Cr - Dr) = 700.00 - 100.00 = 600.00
```

**Assertion:** Account 4010 balance 600.00. CORRECT.

---

### @FT-IS-012 — Expense balance equals debits minus credits

**Entry 1:** "Expense debit" — 5010 Dr 500 / 1010 Cr 500
**Entry 2:** "Expense credit adjustment" — 1010 Dr 150 / 5010 Cr 150

**T-Account for 5010 (Expense, normal-debit):**

```
    5010 (Expense)
  Dr     |  Cr
 500.00  | 150.00
 --------|--------
 Net (Dr - Cr) = 500.00 - 150.00 = 350.00
```

**Assertion:** Account 5010 balance 350.00. CORRECT.

---

### @FT-IS-013 — Lookup equivalence

250.00 Dr/Cr entry. Equivalence assertion. No arithmetic to verify beyond the entry being balanced (250 = 250).

---

### @FT-IS-014, @FT-IS-015 — Nonexistent period errors

No financial amounts. Skipped.

---

### @FT-IS-016 — Closed period income statement still works

**Entry:** "December income" — 1010 Dr 800 / 4010 Cr 800

Revenue = 800.00. Net income = 800.00.

**Assertions:**
- Revenue section total 800.00. CORRECT.
- Net income 800.00. CORRECT.

---

## Feature: Post Obligation to Ledger

### @FT-POL-001 — Posting a confirmed receivable creates correct journal entry

Agreement: receivable, with source and destination accounts.
Instance: amount 1200.00, confirmedDate 2026-04-15.

**Implied journal entry:**

| Account | Debit | Credit |
|---|---|---|
| Destination (asset, e.g. checking) | 1200.00 | |
| Source (revenue, e.g. rent income) | | 1200.00 |

Debit/credit equality: 1200.00 = 1200.00. Balanced.

**Assertions:**
- Journal entry has 2 lines. CORRECT.
- Debit line = destination account, 1200.00. CORRECT.
- Credit line = source account, 1200.00. CORRECT.
- Instance status = "posted". (State transition, not arithmetic.)

---

### @FT-POL-002 — Posting a confirmed payable creates correct journal entry

Agreement: payable, with source and destination accounts.
Instance: amount 850.00, confirmedDate 2026-04-15.

**Implied journal entry:**

| Account | Debit | Credit |
|---|---|---|
| Destination (expense) | 850.00 | |
| Source (asset, e.g. checking) | | 850.00 |

Debit/credit equality: 850.00 = 850.00. Balanced.

**Assertions:**
- Journal entry has 2 lines. CORRECT.
- Debit line = destination, 850.00. CORRECT.
- Credit line = source, 850.00. CORRECT.

---

### @FT-POL-003 — Journal entry description matches agreement and instance names

Amount 1000.00. Balanced (1000 = 1000). Metadata assertion, not arithmetic.

---

### @FT-POL-004 — Journal entry has obligation source and reference

Amount 1000.00. Balanced (1000 = 1000). Metadata assertion, not arithmetic.

---

### @FT-POL-005 — Journal entry entry_date equals confirmed_date

Amount 500.00. Balanced (500 = 500). Date assertion, not arithmetic.

---

### @FT-POL-006 — Instance journal_entry_id is set after posting

Amount 1000.00. Balanced (1000 = 1000). Reference assertion, not arithmetic.

---

### @FT-POL-007 through @FT-POL-015 — Validation and error scenarios

These scenarios test validation guards (wrong status, missing amount, missing confirmed_date, missing accounts, wrong fiscal period). Where amounts appear (1000.00, 500.00), the entries never actually post. No T-accounts to build. The debit/credit equality of the would-be entries is sound in each case (single-amount, two-line entry).

---

## Feature: Obligation Agreement CRUD

### @FT-OA-001 — Create agreement with all fields provided

Amount 150.00 is a field value on the agreement, not a journal entry. No ledger impact. No T-accounts.

### @FT-OA-006 — Create with non-positive amount rejected

Amounts 0.00 and -50.00 tested as invalid. Validation, not ledger. No T-accounts.

### @FT-OA-019 — Update agreement with all fields

Amounts 100.00 and 200.00 are agreement field values. No ledger impact.

All other scenarios in this feature are CRUD/validation. No journal entries implied.

---

## Feature: Overdue Detection

No journal entries in any scenario. Status transitions only (expected -> overdue). No financial amounts that produce ledger entries. Skipped.

---

## Feature: Spawn Obligation Instances

### @FT-SI-011 — Spawn monthly instances for a 3-month range

Amount 150.00 is stamped on each spawned instance. No journal entry created at spawn time. No T-accounts.

### @FT-SI-012 — Spawn for variable-amount agreement

No amount. No ledger impact.

### @FT-SI-013 — Spawn OneTime creates a single instance

Amount 500.00 on the instance. No journal entry.

### @FT-SI-014 — All spawned instances have isActive true

Amount 300.00 on each instance. No journal entry.

### @FT-SI-015 — Spawn overlapping range

Amount 100.00 on instances. No journal entry.

### @FT-SI-016 — Spawn OneTime when instance already exists

Amount 500.00. No journal entry.

All spawning scenarios create obligation instances only — no ledger posting, no journal entries, no T-accounts to build.

---

## Feature: Obligation Instance Status Transitions

### @FT-ST-002 — expected to confirmed with amount and date

Amount 500.00 on the instance. Status transition only. No journal entry.

### @FT-ST-003 — expected to confirmed providing amount in command

Amount 750.00 provided in command. Status transition only. No journal entry.

### @FT-ST-004 — in_flight to confirmed

Amount 300.00. Status transition. No journal entry.

### @FT-ST-007 — overdue to confirmed

Amount 500.00. Status transition. No journal entry.

### @FT-ST-008 — confirmed to posted with journal entry

Amount 500.00. This transition links to an existing journal entry, but the entry itself is a Given (pre-existing), not created by the transition under test. The arithmetic of that pre-existing entry is outside the scope of this scenario.

### @FT-ST-010 — Confirmed transition updates amount when provided

Amounts 500.00 and 525.00. Field update, not a journal entry.

### @FT-ST-011 — Confirmed transition fails when no amount

Validation. No journal entry.

All other scenarios are state-machine validation with no financial amounts producing ledger entries.

---

## Summary

| Feature | Scenarios Traced | Discrepancies Found |
|---|---|---|
| Trial Balance | 8 | 0 |
| Close/Reopen Fiscal Period | 1 | 0 |
| Balance Sheet | 11 | 0 |
| Income Statement | 12 | 0 |
| Post Obligation to Ledger | 6 | 0 |
| Obligation Agreement CRUD | 0 (no ledger entries) | 0 |
| Overdue Detection | 0 (no ledger entries) | 0 |
| Spawn Obligation Instances | 0 (no ledger entries) | 0 |
| Status Transitions | 0 (no ledger entries) | 0 |
| **TOTAL** | **38** | **0** |

**Findings:**

Every scenario with financial amounts that produce or reference journal entries was traced through T-accounts. In all 38 cases:

1. **Debit/credit equality** holds for every journal entry.
2. **Balance direction** is consistent with the normal-balance convention (assets/expenses carry debit balances, liabilities/equity/revenue carry credit balances).
3. **Running totals** accumulate correctly across multi-entry scenarios.
4. **Void mechanics** correctly exclude voided entries from all reports (TB-004, BS-007, IS-004).
5. **The accounting equation** (A = L + E + Retained Earnings) holds in every balance sheet scenario.
6. **Net income** (Revenue - Expenses) is correctly computed in every income statement scenario, including negative cases.
7. **Retained earnings** flow correctly from all-time revenue minus all-time expenses into the balance sheet.

No arithmetic discrepancies found. The specs are clean.

---

*Signed,*
**Ledger Tracer**
*Arithmetic verification auditor*
