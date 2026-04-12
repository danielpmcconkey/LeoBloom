# Project 079 — PO Kickoff

**Date:** 2026-04-12
**Backlog item:** 079 — Add `irregular` recurrence cadence
**Verdict:** APPROVED for planning

## Business Intent

Hobson is blocked. The CLI crashes on `obligation agreement list` because
agreement 8 (HOA — Lockhart) has a `tri_annual` cadence value that the F#
`RecurrenceCadence` DU doesn't recognize. The fix is straightforward: add an
`irregular` case to the DU, migrate the DB value, and make the spawn command
skip irregular agreements gracefully. This unblocks Hobson's Saturday
obligation review procedure.

## Acceptance Criteria Assessment

The spec's four acceptance criteria are tight and sufficient:

1. **`obligation agreement list` no longer crashes** — This is the blocking
   bug. Binary pass/fail. Good.
2. **`obligation agreement show 8` displays cadence as `irregular`** — Proves
   the migration landed and the domain round-trips correctly. Good.
3. **`obligation instance spawn` skips irregular agreements gracefully** —
   Prevents a second crash path. Irregular agreements are manually spawned
   per the Saturday procedure. Good.
4. **Existing tests still pass** — Regression guard. Standard. Good.

No missing criteria. The spec correctly scopes this as a DU extension + data
migration + spawn guard. It does NOT try to build irregular scheduling logic,
which is the right call — that's Hobson's procedure, not LeoBloom's domain.

## Scope Notes

- This is a small, well-bounded change: one DU case, one migration, one
  spawn guard, one CLI display fix.
- No GAAP implications — recurrence cadence is an operational scheduling
  concept, not an accounting one.
- The `notes` field convention for documenting irregular schedules is already
  in place (per spec). No schema changes needed beyond the cadence value.

## Rationale

Approved because: the spec solves exactly the problem it describes (Hobson
crash on agreement list), the acceptance criteria would prove the fix works
if all green, and the scope is minimal with no creep. Priority is justified
by the Hobson blocker.
