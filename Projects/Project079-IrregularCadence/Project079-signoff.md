# Project 079 — Delivery Sign-off

**Date:** 2026-04-12
**Backlog item:** 079 — Add `irregular` recurrence cadence
**Commit:** feat/p079-irregular-cadence branch HEAD
**Verdict:** APPROVED

## Business Outcome

The business need is met. The CLI no longer crashes when listing or showing
obligation agreements that have non-computable schedules. Agreement 8
(HOA — Lockhart) is migrated from the unrecognized `tri_annual` value to
`irregular`, which the domain now handles correctly. The spawn command
gracefully produces zero instances for irregular agreements instead of
erroring, which is the correct behavior since Hobson's Saturday procedure
handles these manually.

## Evidence Summary

- **Governor (APPROVED):** All 7 acceptance criteria independently verified
  against repo state. No fabrication detected. Every code reference confirmed
  by direct file reads.
- **Reviewer (APPROVED):** Zero warnings, zero errors. All pattern matches
  exhaustive. Migration deviation from plan (dropping nonexistent `ops.cadence`
  INSERT) was correctly handled by Builder. No accounting equation or
  DataSource violations.
- **QE:** 1077/1077 tests passing. All 3 Gherkin behavioral scenarios
  (FT-IRC-001 list, FT-IRC-002 show, FT-IRC-003 spawn skip) implemented as
  integration tests with proper isolation. FT-IRC-003 also covered at the
  pure unit level in SpawnObligationInstanceTests.fs.
- **Gherkin coverage:** 3 scenarios covering the 3 user-visible behaviors
  (list doesn't crash, show displays "irregular", spawn produces 0 instances).
  Structural criteria (DU case count, migration existence, exhaustive matches,
  round-trip serialization) correctly left to QE/Builder verification.

## Rationale

The backlog item's intent was narrow and urgent: unblock Hobson by fixing
a CLI crash caused by an unrecognized cadence value. The delivered work
solves exactly that problem with no scope creep. The `irregular` cadence
is a legitimate domain concept (non-computable recurrence), not a hack —
it correctly models agreements where instance spawning is manual. No GAAP
implications. The minor cosmetic trait-tag inconsistency (`@FT-IRC-003`
vs `FT-IRC-003`) noted by the Reviewer does not affect test execution or
business outcomes.
