# 012 — Balance Sheet

**Epic:** D — Financial Statements
**Depends On:** 008
**Status:** Not started

---

Assets, liabilities, and equity at a point in time. The "what do we own and
owe?" report.

**Structure:**

```
Assets (1xxx accounts)
  Fidelity CMA                  XX,XXX.XX
  Ally Checking                   X,XXX.XX
  ...
  ─────────────────────────────
  Total Assets                  XX,XXX.XX

Liabilities (2xxx accounts)
  Mortgage Principal            XXX,XXX.XX
  ...
  ─────────────────────────────
  Total Liabilities             XX,XXX.XX

Equity (3xxx accounts)
  Opening Balance Equity         XX,XXX.XX
  Retained Earnings              XX,XXX.XX  ← computed
  ─────────────────────────────
  Total Equity                  XX,XXX.XX

Total Liabilities + Equity      XX,XXX.XX
```

**The accounting equation:** Assets = Liabilities + Equity. If this doesn't
hold, the system has a bug.

**Retained Earnings:** This is the tricky part. In a formal system, revenue and
expense accounts are "closed" to retained earnings at period-end via closing
entries. We are NOT doing closing entries — that's unnecessary ceremony for this
use case. Instead, **compute retained earnings dynamically:**

`Retained Earnings = All-time revenue account balances - All-time expense account balances`

This is the cumulative net income since inception. It appears as a line in the
equity section. The balance sheet then balances because retained earnings
absorbs the revenue/expense activity.

**Inputs:** `as_of_date`. This is a point-in-time snapshot, not a period report.
**Output:** The report structure above + a boolean `is_balanced` (Assets = L + E).

**Edge cases CE should address in the BRD:**

- Balance sheet as of a date before any entries → all zeros, balanced.
- Retained earnings is negative (expenses > revenue) → show as negative. This is
  normal for a property that's still in early months with startup costs.
- Include inactive accounts with balances? Yes — they still represent real
  assets/liabilities.
