# Project 011-012 Plan Review -- Product Owner Gate 1

**Reviewer:** PO Agent
**Date:** 2026-04-04
**Plan reviewed:** `Project011-012-plan.md`
**Verdict:** APPROVED WITH CONDITIONS

---

## Requirement Coverage Checklist

### Project 011 -- Income Statement

| # | Backlog Requirement | Covered? | Notes |
|---|---------------------|----------|-------|
| 1 | Revenue minus expenses for a fiscal period (period activity, not cumulative) | PASS | Phase 2 SQL filters to `je.fiscal_period_id`, Phase 3 logic computes `netIncome = revenue.sectionTotal - expenses.sectionTotal` |
| 2 | Revenue positive when credits > debits | PASS | Phase 2 explicitly states revenue (normal-credit): `creditTotal - debitTotal` |
| 3 | Expense positive when debits > credits | PASS | Phase 2 explicitly states expense (normal-debit): `debitTotal - creditTotal` |
| 4 | Net Income = Total Revenue - Total Expenses | PASS | Phase 3 step 6 |
| 5 | Empty sections with zero total if no activity for that type | PASS | Phase 3 step 7 and acceptance criteria |
| 6 | Zero-activity accounts omitted | PASS | Phase 3 step 8 explains INNER JOIN handles this. Phase 6 scenario table includes it. |
| 7 | Inactive accounts with activity shown | PASS | Phase 6 scenario table includes "Inactive accounts with activity." SQL has no `a.is_active` filter, which is correct. |

### Project 012 -- Balance Sheet

| # | Backlog Requirement | Covered? | Notes |
|---|---------------------|----------|-------|
| 1 | Assets, liabilities, equity at a point in time (as_of_date, cumulative) | PASS | Phase 4 SQL uses `je.entry_date <= @as_of_date`, no period scoping |
| 2 | Retained Earnings = all-time revenue - all-time expenses (computed, no closing entries) | PASS | Phase 4 `getRetainedEarnings` function, out of scope explicitly excludes closing entries |
| 3 | Accounting equation: Assets = Liabilities + Equity (including retained earnings) | PASS | Phase 5 step 7, `isBalanced` field on `BalanceSheetReport` |
| 4 | `is_balanced` flag | PASS | `BalanceSheetReport.isBalanced` in Phase 1 type definition |
| 5 | Before any entries: all zeros, balanced | PASS | Phase 5 note, Phase 6 scenario table, acceptance criteria |
| 6 | Negative retained earnings is valid | PASS | Phase 6 scenario table and acceptance criteria |
| 7 | Inactive accounts with balances shown | PASS | Phase 6 scenario table. SQL has no `a.is_active` filter. |

---

## Plan Quality Checklist

| Check | Pass? | Notes |
|-------|-------|-------|
| Objective is clear (what and why) | PASS | Builds two reports, explains why they're bundled |
| Deliverables concrete and enumerable | PASS | File summary lists 11 file actions |
| Acceptance criteria testable (binary) | PASS | Every criterion is yes/no verifiable |
| Behavioral vs. structural criteria distinguished | PASS | Phase 6 (Gherkin) covers behavioral; Phase 7 notes structural tests separately |
| Phases ordered logically with verification | PASS | Types -> Repo -> Service -> Repo -> Service -> Specs -> Tests. Each phase has verification step. |
| Out of scope explicit | PASS | Six items listed, all reasonable |
| Dependencies accurate | PASS | Project 008 complete, pattern reuse from 007/008 verified against Ledger.fs |
| No deliverable duplicates prior work | PASS | New types, new modules, no overlap with existing |
| Consistent with backlog items | PASS | See requirement coverage above |
| Risks identified and mitigated | PASS | Five risks with mitigations, including the key retained-earnings-consistency risk |

---

## Concerns

### CONDITION 1: `HAVING SUM(jel.amount) <> 0` is wrong for the balance sheet

The Balance Sheet `getCumulativeBalances` query (Phase 4) uses `HAVING SUM(jel.amount) <> 0` to filter out zero-balance accounts. This is incorrect. `SUM(jel.amount)` sums all line amounts regardless of entry type (debit or credit). A balanced account with $500 debit and $500 credit would have `SUM(jel.amount) = 1000`, not zero, and would NOT be filtered out -- which is actually the wrong reason for the right result.

The real problem: consider an account with a single $100 debit entry. `SUM(jel.amount) = 100`, so it passes the HAVING clause. Good. Now consider an account with a $100 debit and a $100 debit (two entries, same side). `SUM(jel.amount) = 200`. Still passes. But what about the scenario the plan claims to filter: "an account where all activity nets to zero"? An account with a $100 debit and $100 credit has `SUM(jel.amount) = 200` (not zero), because `amount` is always positive in the schema and `entry_type` distinguishes debit from credit. The HAVING clause will never filter anything out.

**The fix:** The HAVING clause should compare computed debit and credit totals, not raw amounts. Something like:

```sql
HAVING SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE 0 END)
    <> SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END)
```

Or just do the filtering in F# after computing the normal-balance-adjusted balance, which is simpler and consistent with how the Income Statement handles zero-activity accounts (via INNER JOIN). The plan's own risk table flags this as a concern but the mitigation ("test an account with offsetting entries") would actually expose the bug.

**Action required:** Fix the HAVING clause or move the zero-balance filtering to the F# service layer before Builder starts.

### CONDITION 2: Income Statement query does not filter inactive accounts with zero activity correctly -- but this is actually fine

On closer reading, this is not actually a problem. The INNER JOIN already ensures only accounts with journal entry lines in the period appear. An inactive account with no activity won't have rows. An inactive account with activity will have rows. This is correct behavior. No action needed -- just confirming I checked.

### Observation (non-blocking): Cross-module dependency on TrialBalanceRepository

The plan reuses `TrialBalanceRepository.resolvePeriodId` and `periodExists` for Income Statement period lookups. The plan acknowledges this is a cross-module dependency and punts extraction to later. I agree with this call for now, but it should be noted for future cleanup. Not blocking.

### Observation (non-blocking): No scenario for Income Statement with closed period

The Trial Balance feature spec has FT-TB-006 ("Closed period trial balance still works"). The Income Statement scenario categories in Phase 6 don't include an equivalent. Since the Income Statement uses the same period resolution, it should work identically, but the Gherkin Writer should consider whether a closed-period scenario is worth including for completeness. Not a condition -- leaving this to the Gherkin Writer's judgment.

---

## Verdict: APPROVED WITH CONDITIONS

The plan is solid. The architecture follows established patterns, the requirement coverage is complete, the phasing is logical, and the risk analysis is thoughtful. One condition must be addressed before the Builder starts:

**Condition 1 (blocking):** Fix the `HAVING SUM(jel.amount) <> 0` clause in the Balance Sheet `getCumulativeBalances` query. Either correct it to compare computed debit/credit totals, or move zero-balance filtering to the F# service layer. The current clause does not do what the plan claims it does.

Once Condition 1 is addressed in the plan, this is clear to proceed to Gherkin writing and build.
