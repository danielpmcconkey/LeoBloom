# 064 — Balance Sheet Accounting Equation Independent Verification

**Epic:** Audit Remediation (2026-04-07)
**Depends On:** None
**Status:** Not started
**Priority:** High
**Source:** Omission Hunter GAP-008

---

## Problem Statement

The balance sheet's A=L+E invariant is checked via the `isBalanced` flag in
existing tests, but no scenario independently computes and verifies that
`assets section total == liabilities section total + total equity`. If the
`isBalanced` flag computation is wrong, every balance sheet test that checks
`the balance sheet is balanced` passes silently.

## What Ships

New scenario(s) in `Specs/Behavioral/BalanceSheet.feature` that:

1. Set up a ledger with entries across assets, liabilities, equity, revenue,
   and expenses
2. Retrieve the balance sheet
3. Assert `assets.sectionTotal = liabilities.sectionTotal + equity.sectionTotal + retainedEarnings`
   as a direct arithmetic equality — NOT via the `isBalanced` flag
4. Ideally test this across multiple conditions (positive RE, negative RE,
   zero equity)

## Acceptance Criteria

- AC-1: At least one scenario asserts A = L + E as a computed equality
- AC-2: The scenario does NOT use the `isBalanced` flag — it computes the
  equation from section totals
- AC-3: A broken `isBalanced` implementation would not cause this test to
  pass
