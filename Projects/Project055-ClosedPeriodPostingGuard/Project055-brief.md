# Project 055 -- Closed Fiscal Period Posting Guard

## Priority: MEDIUM

**DESIGN DECISION REQUIRED -- Dan must weigh in before this enters the pipeline.**

## Status: Blocked (awaiting design direction)

## What's Broken

2 tests in `PostObligationToLedgerTests` fail because they expect the system
to reject posting to a closed fiscal period. The system has no such guard --
posting succeeds regardless of `is_open` status.

| Test | Gherkin ID | Failure Mode |
|------|-----------|-------------|
| `posting when fiscal period is closed returns error` | @FT-POL-013 | Expects Error containing "not open", gets Ok |
| `failed post to closed period leaves instance in confirmed status with no journal entry` | @FT-POL-017 | Expects Error for closed period, gets Ok (instance transitions to posted) |

Both tests insert a fiscal period with `is_open = false`, then call
`ObligationPostingService.postToLedger`. The service finds the period via
`findByDate`, but never checks whether it's open. Posting proceeds, and the
test's `Assert.Fail("Expected Error for closed fiscal period")` fires.

## How This Differs from P053

P053 fixed 5 failing tests. 3 were pure date-collision bugs (test periods
shadowed by seed data). The remaining 2 (POL-013 and POL-017) were _also_
colliding with seed data, but the collision was masking a deeper issue: even
after P053 moved them to 2099 dates with proper isolation, they still fail
because the feature they test -- closed-period rejection -- was never built.

The Gherkin scenarios exist (`Specs/Behavioral/PostObligationToLedger.feature`,
tags @FT-POL-013 and @FT-POL-017). The test implementations exist. The
_behavior_ doesn't.

## Why This Needs Dan's Input

This isn't a bug fix. It's a missing feature with GAAP implications and
non-trivial design choices:

1. **Hard reject vs. warning/override.** GAAP allows posting to closed periods
   in specific circumstances (adjusting entries, audit corrections). Should
   LeoBloom hard-reject all closed-period posts, or provide an override
   mechanism (e.g., a `force` flag)?

2. **Which transaction types?** Should the guard apply to:
   - Obligation posting only (what the current tests cover)?
   - Journal entry posting in general?
   - Transfers?
   - All write operations against the ledger?

3. **Reopen-then-post workflow.** An alternative to override flags: require
   the user to explicitly reopen the period (P009 already supports this),
   post, then re-close. Simpler code, stricter workflow, but more manual
   steps for legitimate adjustments.

4. **What happens to the existing Gherkin specs?** The current specs assume
   hard rejection. If Dan chooses a softer approach, the specs and tests
   need to be rewritten, not just the service code.

5. **Scope of the guard.** `PostJournalEntry` (P005) also doesn't check
   `is_open`. If we're adding the guard to obligation posting, should it
   also be added to direct journal entry posting for consistency?

## Acceptance Criteria (Preliminary -- subject to design decision)

These will be finalized after Dan provides direction:

- `ObligationPostingService.postToLedger` checks `is_open` on the resolved
  fiscal period before creating a journal entry
- Posting to a closed period returns `Error` with a message containing
  "not open" (or equivalent, per design decision)
- Instance remains in `confirmed` status with no journal entry on rejection
- @FT-POL-013 and @FT-POL-017 pass
- No regression in the rest of the test suite
- If the guard is extended beyond obligation posting (per design decision),
  corresponding specs and tests are added

## Dependencies

- P009 (Close/reopen fiscal period) -- Done. The `is_open` flag and its
  toggle mechanism already exist.
- P018 (Post obligation to ledger) -- Done. The posting service exists but
  lacks the period-status check.

## Out of Scope

- Changing the fiscal period data model
- Adding audit logging for override attempts (future backlog candidate)
- Retroactive validation of existing posted entries against period status
