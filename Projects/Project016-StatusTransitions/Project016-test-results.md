# Project 016 — Test Results

## Summary

- **Total tests:** 363 (341 existing + 22 new)
- **Passed:** 363
- **Failed:** 0
- **Skipped:** 0

## New Tests (22)

| GherkinId   | Test | Result |
|-------------|------|--------|
| FT-ST-001   | transition from expected to in_flight succeeds | Pass |
| FT-ST-002   | transition from expected to confirmed with amount and date | Pass |
| FT-ST-003   | transition from expected to confirmed providing amount in command | Pass |
| FT-ST-004   | transition from in_flight to confirmed | Pass |
| FT-ST-005   | transition from expected to overdue | Pass |
| FT-ST-006   | transition from in_flight to overdue | Pass |
| FT-ST-007   | transition from overdue to confirmed | Pass |
| FT-ST-008   | transition from confirmed to posted with journal entry | Pass |
| FT-ST-009   | transition from expected to skipped with notes | Pass |
| FT-ST-010   | confirmed transition updates amount when provided even if already set | Pass |
| FT-ST-011   | confirmed transition fails when no amount on instance or in command | Pass |
| FT-ST-012   | transition from confirmed to expected is rejected | Pass |
| FT-ST-013   | transition from posted to confirmed is rejected | Pass |
| FT-ST-014   | transition from skipped to confirmed is rejected | Pass |
| FT-ST-015   | transition from overdue to in_flight is rejected | Pass |
| FT-ST-016   | transition to posted without journal entry ID fails | Pass |
| FT-ST-017   | transition to posted with nonexistent journal entry fails | Pass |
| FT-ST-018   | transition to skipped without notes fails when instance has no notes | Pass |
| FT-ST-019   | transition to confirmed without confirmedDate fails | Pass |
| FT-ST-020   | transition on inactive instance fails | Pass |
| FT-ST-021   | transition on nonexistent instance fails | Pass |
| FT-ST-022   | transition to skipped succeeds when instance already has notes | Pass |

## Reviewer Findings (resolved)

1. **Field leakage on non-target transitions** — `journalEntryId`, `confirmedDate`, and `notes` were passed through to the DB unconditionally. Fixed: each field is now gated by target status (mirrors the `amountToSet` pattern). `journalEntryId` only set for `Posted`, `confirmedDate` only for `Confirmed`, `notes` only for `Skipped`.

2. **Finding 4 (not addressed)** — Amount invariant for `posted` status not re-checked at transition time. Low severity defense-in-depth concern. Accepted: the only path to `posted` is through `confirmed`, which enforces amount. A future direct DB edit breaking this is out of scope.

## Regression

All 341 pre-existing tests pass unchanged.
