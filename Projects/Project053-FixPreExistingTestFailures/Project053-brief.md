# Project 053 — Fix Pre-Existing Test Failures

## Priority: HIGH

Must be completed before any new feature work. Zero failing tests is the
baseline, not a goal.

## What Is Failing

5 tests in `PostObligationToLedgerTests` fail every run:

| Test | GherkinId | Failure Mode |
|------|-----------|-------------|
| `posting when no fiscal period covers confirmed_date returns error` | @FT-POL-012 | Expects error, gets success |
| `posting when fiscal period is closed returns error` | @FT-POL-013 | Expects error, gets success |
| `findByDate returns correct period for a date within range` | @FT-POL-014 | Asserts test-period ID, gets seed-period ID |
| `findByDate returns None for a date outside all periods` | @FT-POL-015 | Expects None, gets seed period |
| `failed post to closed period leaves instance in confirmed status with no journal entry` | @FT-POL-017 | Expects error, gets success |

## Root Cause

Seed data collision with test data on `ledger.fiscal_period`.

Migration `1712000005000_SeedFiscalPeriods.sql` populates fiscal periods for
every month from 2026-01 through 2028-12 with `is_open = true` (the column
default). These are legitimate production periods — they belong in the
database.

The failing tests insert their own fiscal periods for date ranges that
**overlap** with seed data (April 2026, May 2026, July 2026, August 2026).
When service code calls `FiscalPeriodRepository.findByDate`, the query:

```sql
SELECT ... FROM ledger.fiscal_period
WHERE @date >= start_date AND @date <= end_date
ORDER BY start_date LIMIT 1
```

returns the **seed period** (inserted first, lower ID) instead of the
**test period**. This breaks every test that depends on controlling the
fiscal period's identity or `is_open` status.

Specific breakdowns:

- **POL-012**: Uses July 2026 and expects no period to exist. Seed data
  has `2026-07`. Query returns a hit.
- **POL-013**: Inserts a *closed* period for April 2026. Seed data has an
  *open* period for April 2026. Query returns the open seed period. Service
  proceeds instead of erroring.
- **POL-014**: Inserts a test period for May 2026, asserts `findByDate`
  returns that period's ID. Query returns seed May 2026 (different ID).
- **POL-015**: Expects `findByDate` returns None for August 2026. Seed data
  has `2026-08`. Query returns a hit.
- **POL-017**: Same mechanics as POL-013. Closed test period shadowed by
  open seed period.

## Why This Matters

- **Broken tests erode trust.** If 5 known failures are "fine," the 6th
  failure that catches a real bug gets ignored too.
- **The excuse doesn't hold up.** "Bad test data" implies the seed data is
  wrong. The seed data is correct — it's production data. The tests are
  wrong: they assume they're the only fiscal periods in the database.
- **This has been tolerated across multiple projects.** That's how
  normalization of deviance works. Stop it here.

## Fix Direction (for the Planner)

Two approaches, not mutually exclusive:

1. **Fix the tests:** Use date ranges outside seed data (e.g., 2099-xx) or
   clean up / temporarily delete overlapping seed periods within each test's
   try/finally block. The test harness already has `TestCleanup` for this.

2. **Fix `findByDate` query semantics:** If the intent is "find the period
   for this date," and there should only ever be one non-overlapping period
   for a given date, add a uniqueness constraint or tighten the query. But
   this doesn't fix the test isolation problem — tests that insert duplicate
   periods for the same date range will still collide.

The right call is probably (1) with a dash of (2) as defensive hardening.
The Planner decides.

## Acceptance Criteria

- All 19 `PostObligationToLedgerTests` pass (0 failures, 0 skips)
- No test depends on the absence of seed data fiscal periods
- No other test suite regresses
- Root cause is documented so future test authors don't repeat it

## Dependencies

None. This is a standalone fix.

## Out of Scope

- Changing the seed data
- Refactoring `findByDate` query logic (unless the Planner determines it's
  part of the fix)
- Any new behavioral features
