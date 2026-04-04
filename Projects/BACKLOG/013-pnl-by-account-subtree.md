# 013 — P&L by Account Subtree

**Epic:** D — Financial Statements
**Depends On:** 011
**Status:** Not started

---

The filtered income statement. "How much did the investment property cost me?"
vs "How much did I spend personally?" This is DataModelSpec key query #7.

**Mechanics:**

Walk the `parent_code` hierarchy starting from a given root account code.
Collect all descendant accounts. Produce an income statement (Story 011) filtered
to only those accounts.

**Example:** P&L rooted at 5000 (Investment Property Expenses) shows only
accounts under the 5xxx subtree. P&L rooted at 6000 (Personal Expenses) shows
only 6xxx. P&L rooted at 4000 (Revenue) shows all rental income.

**The subtree walk:** Account 5000 has children 5100, 5200, etc. Account 5100
might have children 5110, 5120. The walk collects all descendants recursively
via `parent_code`. This is a tree traversal, not a prefix match — even though
the numbering convention happens to align, the walk uses the FK relationship,
not string matching on codes.

**Inputs:** `root_account_code` + `fiscal_period_id`.
**Output:** Income statement structure filtered to the subtree.

**Edge cases CE should address in the BRD:**

- Root account that has no children → report contains only that account's activity.
- Root account that is a revenue account → works fine, produces a revenue-only P&L.
- Can you root at a non-revenue, non-expense account (e.g., an asset)? Technically
  yes, but the output wouldn't be a meaningful P&L. The BRD should document this
  as "supported but not the intended use case."
- Depth limit? No. The COA is shallow (2-3 levels). Don't over-engineer.
