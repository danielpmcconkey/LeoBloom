# Project 017 — Test Results

## Summary

- **Total tests:** 374 (363 existing + 11 new)
- **Passed:** 374
- **Failed:** 0
- **Skipped:** 0

## New Tests (11)

| GherkinId   | Test | Result |
|-------------|------|--------|
| FT-OD-001   | single overdue instance is transitioned | Pass |
| FT-OD-002   | multiple overdue instances are all transitioned | Pass |
| FT-OD-003   | instance on the reference date is not overdue | Pass |
| FT-OD-004   | instance after the reference date is not overdue | Pass |
| FT-OD-005   | in_flight instances are not flagged as overdue | Pass |
| FT-OD-006   | already overdue instances are not re-transitioned | Pass |
| FT-OD-007   | confirmed instances are not flagged as overdue | Pass |
| FT-OD-008   | inactive instances are not flagged as overdue | Pass |
| FT-OD-009   | running detection twice produces same result | Pass |
| FT-OD-010   | only eligible instances among a mixed set are transitioned | Pass |
| FT-OD-011   | no overdue instances returns zero transitioned | Pass |

## Reviewer Findings

1. **Silent failure on candidate query** — `detectOverdue` was catching query
   exceptions and returning an empty result indistinguishable from "nothing to
   do." Fixed: added `queryFailed` boolean to `OverdueDetectionResult` so
   callers can distinguish.

2. **Plan typo** — `findOverdueCandiates` → `findOverdueCandidates`. Fixed.

3. **Test isolation with global queries (acknowledged, not fixed)** —
   `detectOverdue` queries all instances globally, so tests asserting exact
   transition counts could theoretically flake if orphaned data exists from
   failed prior runs. Accepted: the risk is low (cleanup in finally blocks,
   specific reference dates), and scoping the batch query by test prefix would
   mean adding test-only parameters to production code.

## Regression

All 363 pre-existing tests pass unchanged.
