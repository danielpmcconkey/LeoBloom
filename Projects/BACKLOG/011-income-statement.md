# 011 — Income Statement

**Epic:** D — Financial Statements
**Depends On:** 008
**Status:** Not started

---

Revenue minus expenses for a fiscal period. The "did we make money?" report.

**Structure:**

```
Revenue (4xxx accounts)
  Rental Income — Jeffrey          1,000.00
  Rental Income — Alice             700.00
  ...
  ─────────────────────────────
  Total Revenue                  X,XXX.XX

Expenses (5xxx + 6xxx accounts)
  Mortgage Interest                800.00
  Property Insurance               125.00
  ...
  ─────────────────────────────
  Total Expenses                 X,XXX.XX

Net Income                       X,XXX.XX
```

**Mechanics:**
- Filter to revenue accounts (type = `'revenue'`) and expense accounts
  (type = `'expense'`).
- For each account, calculate the balance for the period (sum of activity in
  that period only, not cumulative — unlike account balance which is as-of-date).
- Revenue balances are positive when credits > debits (normal for revenue).
- Expense balances are positive when debits > credits (normal for expenses).
- Net Income = Total Revenue - Total Expenses.

**Important distinction:** This is period activity, not cumulative balance.
"Revenue in March" means credits to revenue accounts from entries in the March
fiscal period. Story 007 gives cumulative balance; this story needs
period-specific activity.

**Edge cases CE should address in the BRD:**

- Period with no revenue or no expenses → show the section with zero total.
- Accounts with zero activity in the period → omit from the report (don't clutter
  with zero rows).
- Should inactive accounts with activity appear? Yes — they had activity when
  they were active. Show them.

**DataModelSpec references:** Account types, key query #7 (P&L by subtree is
the filtered version of this).
