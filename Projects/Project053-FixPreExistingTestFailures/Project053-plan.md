# Project 053 — Plan

## Objective

Fix 5 failing tests in `PostObligationToLedgerTests` caused by test fiscal
periods colliding with seed data from `1712000005000_SeedFiscalPeriods.sql`.
Audit all 19 tests in the file to eliminate any silent dependency on seed
data absence, even for currently-passing tests.

## Root Cause (Summary)

Seed migration inserts fiscal periods for every month 2026-01 through 2028-12.
Tests that insert their own periods for dates within that range get shadowed
by `findByDate`'s `ORDER BY start_date LIMIT 1` returning the seed row (lower
ID). Tests that expect "no period found" for dates in the seed range also fail.

## Fix Strategy

**Primary fix: move all test fiscal periods to 2099-xx date ranges.**

This is the right call because:

1. It completely eliminates collision with any realistic seed data range.
2. There is zero date validation in the codebase that would reject 2099 dates.
3. Existing precedent: `DeleteRestrictionTests`, `ConsolidatedHelpersTests`,
   and `LedgerConstraintTests` already use 2099 dates for exactly this reason.
4. It satisfies the PO's constraint: "don't just shift to 2029 where seed data
   happens to not exist *yet*." 2099 is far enough out that nobody is extending
   seed data there.

**No changes to `findByDate` query or seed data.** The query is correct for
production (there should never be overlapping periods for real data). The seed
data is correct. The tests were wrong.

## Phases

### Phase 1: Audit All 19 Tests

**What:** Classify every test in `PostObligationToLedgerTests.fs` by whether
its fiscal period dates collide with seed data and whether it would break
if seed data existed for those dates.

**Audit results (pre-computed from code review):**

| Test | Gherkin ID | Fiscal Period Dates | Collides | Currently Fails | Needs Fix |
|------|-----------|-------------------|---------|----------------|----------|
| posting receivable happy path | @FT-POL-001 | Apr 2026 | YES | No* | YES |
| posting payable happy path | @FT-POL-002 | Apr 2026 | YES | No* | YES |
| journal entry description | @FT-POL-003 | Apr 2026 | YES | No* | YES |
| journal entry source/reference | @FT-POL-004 | Apr 2026 | YES | No* | YES |
| entry_date equals confirmed_date | @FT-POL-005 | Apr 2026 | YES | No* | YES |
| instance journal_entry_id set | @FT-POL-006 | Apr 2026 | YES | No* | YES |
| not confirmed error | @FT-POL-007 | Apr 2026 | YES | No* | YES |
| no amount error | @FT-POL-008 | Apr 2026 | YES | No* | YES |
| no confirmed_date error | @FT-POL-009 | Apr 2026 | YES | No* | YES |
| no source_account error | @FT-POL-010 | Apr 2026 | YES | No* | YES |
| no dest_account error | @FT-POL-011 | Apr 2026 | YES | No* | YES |
| no fiscal period error | @FT-POL-012 | Jul 2026 (expects none) | YES | **YES** | YES |
| closed fiscal period error | @FT-POL-013 | Apr 2026 (closed) | YES | **YES** | YES |
| findByDate correct period | @FT-POL-014 | May 2026 | YES | **YES** | YES |
| findByDate returns None | @FT-POL-015 | May 2026 + Aug 2026 lookup | YES | **YES** | YES |
| double-posting prevention | @FT-POL-016 | Apr 2026 | YES | No* | YES |
| failed post atomicity | @FT-POL-017 | Apr 2026 (closed) | YES | **YES** | YES |
| idempotency guard retry | @FT-POL-018 | Apr 2026 | YES | No* | YES |
| voided entry bypass | @FT-POL-019 | Apr 2026 | YES | No* | YES |

*"No*" = currently passes by luck. These tests insert periods that collide
with seed data, but `findByDate` happens to return the seed period which is
also open, and the test doesn't assert on the period's identity. They'd break
if someone closed or deleted the seed period for April 2026. They are still
isolation bugs and must be fixed.

**Verdict: all 19 tests need date changes.** Every single test in this file
uses dates in the 2026-01 through 2028-12 seed range.

**Verification:** Code review of the audit table against the source file.

### Phase 2: Fix All 19 Tests

**What:** Change all fiscal period date ranges and confirmed dates to 2099-xx
equivalents. Also update the "no period found" tests to use 2099 dates that
genuinely have no coverage.

**File:** `Src/LeoBloom.Tests/PostObligationToLedgerTests.fs`

**Specific changes per test pattern:**

1. **Tests using April 2026 (POL-001 through POL-011, POL-016, POL-017, POL-018, POL-019):**
   - `DateOnly(2026, 4, 1)` -> `DateOnly(2099, 4, 1)`
   - `DateOnly(2026, 4, 30)` -> `DateOnly(2099, 4, 30)`
   - `DateOnly(2026, 4, 15)` -> `DateOnly(2099, 4, 15)`
   - `DateOnly(2026, 4, 20)` -> `DateOnly(2099, 4, 20)`

2. **POL-012 (no fiscal period for date):**
   - Change `DateOnly(2026, 7, 15)` -> `DateOnly(2099, 7, 15)`
   - No fiscal period is inserted. Test expects `findByDate` returns nothing.
     With 2099, no seed data exists. Correct.

3. **POL-014 (findByDate returns correct period):**
   - `DateOnly(2026, 5, 1)` -> `DateOnly(2099, 5, 1)`
   - `DateOnly(2026, 5, 31)` -> `DateOnly(2099, 5, 31)`
   - `DateOnly(2026, 5, 15)` -> `DateOnly(2099, 5, 15)`

4. **POL-015 (findByDate returns None for out-of-range date):**
   - `DateOnly(2026, 5, 1)` -> `DateOnly(2099, 5, 1)`
   - `DateOnly(2026, 5, 31)` -> `DateOnly(2099, 5, 31)`
   - `DateOnly(2026, 8, 15)` -> `DateOnly(2099, 8, 15)`

**What does NOT change:**
- Helper functions at the top of the file (they take dates as parameters)
- Any other test file (out of scope per PO -- they use `fpId` directly, not
  `findByDate`, so the collision is harmless for them today)
- Seed migration
- `FiscalPeriodRepository.findByDate` query

**Verification:** `dotnet test` -- all 19 PostObligationToLedgerTests pass.
Full suite passes with 0 failures, 0 skips.

### Phase 3: Document the Pattern

**What:** Add a brief comment block at the top of `PostObligationToLedgerTests.fs`
(below the module declaration and opens) explaining why 2099 dates are used and
warning future test authors not to use dates in the seed range.

Something like:

```fsharp
// IMPORTANT: All fiscal period dates in this file use 2099-xx to avoid
// colliding with seed data (1712000005000_SeedFiscalPeriods.sql populates
// 2026-01 through 2028-12). Tests that need findByDate to return THEIR
// period — or to find NO period — will break if dates overlap with seed data.
// See Project053 for the full root cause analysis.
```

**File:** `Src/LeoBloom.Tests/PostObligationToLedgerTests.fs`

**Verification:** Comment exists and is accurate.

## Acceptance Criteria

- [ ] All 5 previously-failing tests pass (POL-012, POL-013, POL-014, POL-015, POL-017)
- [ ] All 14 other PostObligationToLedgerTests continue to pass
- [ ] No test in any other test file regresses
- [ ] No test assumes the absence of seed data fiscal periods
- [ ] Root cause is documented in the test file for future authors
- [ ] Zero failures, zero skips in full test suite

## Risks

1. **Other test files using colliding dates.** SubtreePLTests (March 2026),
   OpeningBalanceTests (January 2026) also insert periods in the seed range.
   They currently pass because they pass `fpId` directly as a FK and never
   call `findByDate`. This is technically an isolation smell but not a failure.
   Fixing them is out of scope for P053 -- the PO noted P054 (Seed Data
   Separation) as the follow-on for broader cleanup.

2. **2099 breaking something unexpected.** No date validation exists in the
   codebase that would reject it. Multiple test files already use 2099.
   Risk is near zero.

## Out of Scope

- Changing seed data (P054)
- Fixing fiscal period date collisions in other test files (P054)
- Refactoring `findByDate` query semantics
- Adding a uniqueness constraint on fiscal period date ranges
- New Gherkin specs (existing specs POL-012 through POL-017 already describe
  the correct behaviors; only the test implementations were wrong)
