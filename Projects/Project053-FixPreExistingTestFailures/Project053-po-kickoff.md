# Project 053 — Fix Pre-Existing Test Failures

**PO Kickoff**
**Date:** 2026-04-06
**Status:** In Progress
**Epic:** Foundation Cleanup
**Depends On:** None
**Blocks:** P054 (Seed Data Separation)

---

## Project Summary

5 tests in `PostObligationToLedgerTests` have been failing across multiple
projects. The root cause is a test isolation bug, not bad test data: seed
migration `1712000005000_SeedFiscalPeriods.sql` populates fiscal periods
for 2026-01 through 2028-12, and 5 tests create their own fiscal periods
for colliding date ranges. When `FiscalPeriodRepository.findByDate` runs,
the query returns the seed period (inserted first, lower ID) instead of the
test period, breaking every assertion that depends on controlling the
period's identity or `is_open` status.

Full root cause analysis is in `Project053-brief.md`.

Zero failing tests is a baseline, not a goal. Tolerating known failures
normalizes ignoring test results, which means the next real regression gets
waved away too. This ships before any new feature work.

---

## Failing Tests

| Test | Gherkin ID | Failure Mode |
|------|-----------|-------------|
| posting when no fiscal period covers confirmed_date returns error | @FT-POL-012 | Expects error, gets success (seed period exists) |
| posting when fiscal period is closed returns error | @FT-POL-013 | Expects error, gets success (seed period is open) |
| findByDate returns correct period for a date within range | @FT-POL-014 | Asserts test-period ID, gets seed-period ID |
| findByDate returns None for a date outside all periods | @FT-POL-015 | Expects None, gets seed period |
| failed post to closed period leaves instance in confirmed status | @FT-POL-017 | Expects error, gets success (seed period is open) |

---

## Acceptance Criteria

### Behavioral Criteria (Gherkin-eligible)

| # | Criterion |
|---|-----------|
| B1 | All 5 failing PostObligationToLedger tests pass |
| B2 | All other PostObligationToLedger tests continue to pass (0 regressions in the file) |
| B3 | No test in any other test file regresses |

Note: Whether these need new or updated Gherkin scenarios depends on what
the fix looks like. If the existing Gherkin specs already describe the
correct behaviors (they do -- POL-012 through POL-017 are already
specified), no new specs are needed. The Gherkin Writer should evaluate
whether existing scenarios need tag/wording adjustments and whether the
fix warrants a new scenario documenting the isolation pattern.

### Structural Criteria (verified by QE/Governor, not Gherkin)

| # | Criterion |
|---|-----------|
| S1 | No test assumes the absence of seed data fiscal periods |
| S2 | Full test suite passes: 608 passed (602 existing + 6 previously failing), 0 failed, 0 skipped |
| S3 | Root cause is documented so future test authors don't repeat the mistake |

The exact target count in S2 is approximate -- the fix may add or
reorganize tests. The hard requirement is: zero failures, zero skips,
every previously-failing test now passes.

---

## Fix Direction (for the Planner)

Two approaches identified in the brief:

1. **Fix the tests** -- use date ranges outside seed data (e.g., 2099-xx)
   or clean up overlapping seed periods within each test's setup/teardown.
   This is the primary fix.

2. **Harden `findByDate`** -- if overlapping periods shouldn't exist, add
   a uniqueness constraint or tighten the query. Defensive, but doesn't
   fix the test isolation problem by itself.

The Planner decides the mix. The PO's only constraint: the fix must not
require changing seed data, and the fix must not be brittle (i.e., don't
just shift the test dates to 2029 where seed data happens to not exist
yet -- that breaks again when someone extends the seed range).

---

## Out of Scope

- Changing the seed data content or structure (that's P054)
- Refactoring `FiscalPeriodRepository.findByDate` unless the Planner
  determines it's necessary for the fix
- Any new behavioral features
- Any changes to the Gherkin spec content for PostObligationToLedger
  scenarios beyond what the fix requires

---

## Risks

1. **Test coupling.** If other tests in PostObligationToLedgerTests also
   depend on seed data absence but happen to pass today (luck of date
   ranges), fixing only the 5 known failures could leave latent bugs. The
   Planner should audit all 19 tests in the file, not just the 5 that fail.

2. **Date range selection.** Using far-future dates (2099) avoids seed
   collisions but could interact poorly with any validation logic that
   rejects unrealistic dates. The Planner should verify.

---

## Backlog Status Update

P053 status changed from "Not started" to **In Progress** as of 2026-04-06.
