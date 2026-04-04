# 008 — Trial Balance

**Epic:** B — Balance Calculation
**Depends On:** 007
**Status:** Done

---

For a fiscal period, produce a trial balance report and verify system integrity.

**The trial balance (from DataModelSpec key query #1):**

- For a given `fiscal_period_id`, find all `journal_entry` rows where
  `fiscal_period_id` matches and `voided_at IS NULL`.
- Sum all their `journal_entry_line` rows:
  - Total debits = `SUM(amount) WHERE entry_type = 'debit'`
  - Total credits = `SUM(amount) WHERE entry_type = 'credit'`
- **Total debits must equal total credits.** If they don't, the system has a bug.
  This is not a user error — it means the posting engine (Story 005) has a defect.

**The report:** Beyond the integrity check, produce a useful output:

- Each account that has activity in the period, with:
  - Account code, name, type
  - Debit total for the period
  - Credit total for the period
  - Net balance (using the normal_balance formula from Story 007)
- Grouped by account type (assets, liabilities, equity, revenue, expenses)
- Subtotals per group
- Grand total debits and credits at the bottom

**Inputs:** `fiscal_period_id` (or `period_key` like `'2026-03'`).
**Output:** The report structure above + a boolean `is_balanced`.

**Edge cases CE should address in the BRD:**

- Period with no entries → balanced (0 = 0), empty report.
- Period that is closed → still runs. Trial balance is a read operation.
- Should this include a "balance as of period end" (cumulative) or just "activity
  in this period"? **Both are useful.** Activity-in-period is the trial balance.
  Cumulative balance is the balance sheet (Story 012). The BRD should be explicit
  about which this is: **activity in the period only.**

**DataModelSpec references:** Key query #1.
