# Project 013 — P&L by Account Subtree: Test Results

**Author:** Basement Dweller (Governor role)
**Date:** 2026-04-04
**Verdict:** PASS

---

## Test Suite

269 total tests, all passing, zero warnings.
Baseline was 254 (from Projects 001-012 + structural tests).
15 new tests added (12 behavioral, 3 structural).

## Behavioral Scenarios

| ID | Scenario | Status |
|----|----------|--------|
| FT-SPL-001 | Subtree with revenue and expense children produces correct P&L | PASS |
| FT-SPL-002 | Subtree with only revenue descendants shows empty expense section | PASS |
| FT-SPL-003 | Subtree with only expense descendants shows empty revenue section | PASS |
| FT-SPL-004 | Root account with no children returns single-account P&L | PASS |
| FT-SPL-005 | Root not revenue/expense with no rev/exp descendants returns empty report | PASS |
| FT-SPL-006 | Voided entries excluded from subtree P&L | PASS |
| FT-SPL-007 | Accounts outside the subtree are excluded | PASS |
| FT-SPL-008 | Multi-level hierarchy (grandchildren) included in subtree | PASS |
| FT-SPL-009 | Lookup by period key returns same result as by period ID | PASS |
| FT-SPL-010 | Nonexistent account code returns error | PASS |
| FT-SPL-011 | Nonexistent period returns error | PASS |
| FT-SPL-012 | Empty period for subtree produces zero net income | PASS |

## Structural Tests

| Scenario | Status |
|----------|--------|
| SubtreePLReport type has required fields | PASS |
| getByAccountCodeAndPeriodId returns Result | PASS |
| getByAccountCodeAndPeriodKey returns Result | PASS |

## Acceptance Criteria

- [x] `SubtreePLReport` type exists in `Ledger.fs`
- [x] Reuses `IncomeStatementLine` and `IncomeStatementSection` (no duplication)
- [x] `SubtreePLService.getByAccountCodeAndPeriodId` works
- [x] `SubtreePLService.getByAccountCodeAndPeriodKey` works
- [x] Recursive CTE correctly resolves multi-level subtrees
- [x] Only revenue/expense accounts in the subtree appear in sections
- [x] Accounts outside the subtree are excluded even if same account type
- [x] Voided entries excluded
- [x] Nonexistent account code returns descriptive error
- [x] Nonexistent period returns descriptive error
- [x] Empty subtree activity returns zero net income with empty sections
- [x] `insertAccountWithParent` helper added to TestHelpers
- [x] All tests pass, no new warnings
- [x] Existing 254 tests still pass (regression)

## Review Notes

Self-review passed. SQL injection safe (parameterized queries). Recursive CTE
terminates naturally (tree walks downward, leaf accounts end recursion). Pattern
is consistent with IncomeStatementService/Repository. Test cleanup is safe
(single DELETE statement handles parent/child FK atomically).
