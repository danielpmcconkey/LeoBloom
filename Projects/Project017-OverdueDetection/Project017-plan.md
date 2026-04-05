# Project 017 — Overdue Detection

## Goal

Batch operation that identifies obligation instances past their expected date
and transitions them to overdue. This is the automation layer on top of the
manual overdue transition built in 016.

## Behavior

Given a reference date (typically "today"), find all active obligation instances
where:
- `status = 'expected'` (in_flight instances are not overdue — they're in progress)
- `expected_date < referenceDate` (strictly before, not on)
- `is_active = true`

For each matching instance, transition to `overdue` using the existing
`StatusTransition` machinery from 016.

Return a summary: how many detected, how many transitioned, any errors.

## Design Decisions

- **Reference date as parameter**, not hardcoded `today`. Makes testing
  deterministic and allows "what if we ran this yesterday" scenarios.
- **Only `expected` → `overdue`**, not `in_flight` → `overdue`. An in-flight
  payment that's past due date is still in progress — the user should manually
  decide if it's actually overdue. This matches the DataModelSpec lifecycle
  where both transitions exist but serve different intents.
- **Batch, not per-instance**. Single query to find candidates, then transition
  each. If one fails, continue with the rest (collect errors, don't abort).
- **Idempotent**. Running twice produces the same result — already-overdue
  instances are skipped by the `status = 'expected'` filter.

## Implementation

### Domain (Ops.fs)

- `OverdueDetectionResult` type: `{ transitioned: int; errors: (int * string) list }`

### Repository (ObligationInstanceRepository.fs)

- `findOverdueCandidates`: query for active instances with status = expected
  and expected_date < referenceDate. Returns `ObligationInstance list`.

### Service (ObligationInstanceService.fs)

- `detectOverdue (referenceDate: DateOnly) : OverdueDetectionResult`
  - Calls `findOverdueCandidates` to get the list
  - For each, calls the existing `transition` function with targetStatus = Overdue
  - Collects successes and errors into the result

## Artifacts

- Gherkin: `Specs/Ops/OverdueDetection.feature`
- Tests: `Src/LeoBloom.Tests/OverdueDetectionTests.fs`
- Domain: additions to `Src/LeoBloom.Domain/Ops.fs`
- Repository: additions to `Src/LeoBloom.Utilities/ObligationInstanceRepository.fs`
- Service: additions to `Src/LeoBloom.Utilities/ObligationInstanceService.fs`

## Out of Scope

- Scheduling / cron. This is a function you call, not a daemon.
- Notifications / alerts (future nagging agent).
- In-flight → overdue automation.
