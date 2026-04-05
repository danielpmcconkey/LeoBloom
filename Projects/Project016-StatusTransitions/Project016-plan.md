# Project 016 — Status Transitions

## Goal

Implement the obligation instance state machine. Given an instance in status X,
allow transitions to valid successor statuses with appropriate validation and
field updates. No overdue detection (017), no ledger posting (018) — just the
state machine and its guards.

## Status Lifecycle (from DataModelSpec)

```
expected → in_flight → confirmed → posted
                ↘ overdue
expected → overdue → confirmed → posted
expected → skipped
```

## Allowed Transitions

| From       | To         | Guard                                                      |
|------------|------------|-------------------------------------------------------------|
| expected   | in_flight  | none                                                        |
| expected   | confirmed  | amount must be set (on instance or provided in command)     |
| expected   | overdue    | none (017 automates this, but manual is valid)              |
| expected   | skipped    | notes required (reason for skipping)                        |
| in_flight  | confirmed  | amount must be set (on instance or provided in command)     |
| in_flight  | overdue    | none                                                        |
| overdue    | confirmed  | amount must be set                                          |
| confirmed  | posted     | journal_entry_id required (018 provides this, but the transition gate is ours) |

Invalid transitions (anything not in the table above) return an error.

## Field Effects

- **→ in_flight**: no field changes required
- **→ confirmed**: set `confirmed_date` (required), set `amount` if provided (required if not already set)
- **→ posted**: set `journal_entry_id` (required)
- **→ overdue**: no field changes required
- **→ skipped**: set `notes` (required if not already set)

## Command Type

```fsharp
type TransitionCommand =
    { instanceId: int
      targetStatus: InstanceStatus
      amount: decimal option          // for confirmed transition
      confirmedDate: DateOnly option   // for confirmed transition
      journalEntryId: int option       // for posted transition
      notes: string option }           // for skipped transition
```

## Validation

### Pure (before DB)

1. `instanceId` > 0
2. `targetStatus` is a valid DU case
3. If `targetStatus = Posted`, `journalEntryId` must be Some
4. If `targetStatus = Skipped`, `notes` must be Some (unless instance already has notes)
5. If `targetStatus = Confirmed`, `confirmedDate` must be Some

### DB-dependent

1. Instance must exist
2. Instance must be active
3. Current status → target status must be in the allowed transitions table
4. If `targetStatus = Confirmed` and instance.amount is None, command.amount must be Some
5. If `targetStatus = Posted`, journal entry must exist in ledger.journal_entry

## Implementation

### Domain (Ops.fs)

- Add `TransitionCommand` type
- Add `StatusTransition` module with:
  - `allowedTransitions`: map of valid from→to pairs
  - `isValidTransition`: pure check
  - `validateTransitionCommand`: pure validation

### Repository (ObligationInstanceRepository.fs)

- Add `findById`: returns `ObligationInstance option` given txn + id
- Add `updateStatus`: updates status + relevant fields, returns updated instance

### Service (ObligationInstanceService.fs)

- Add `transition`: orchestrates validation, lookup, transition check, field
  updates, persistence

## Artifacts

- Gherkin: `Specs/Ops/StatusTransitions.feature`
- Tests: `Src/LeoBloom.Tests/StatusTransitionTests.fs`
- Domain: additions to `Src/LeoBloom.Domain/Ops.fs`
- Repository: additions to `Src/LeoBloom.Utilities/ObligationInstanceRepository.fs`
- Service: additions to `Src/LeoBloom.Utilities/ObligationInstanceService.fs`

## Out of Scope

- Automated overdue detection (017)
- Ledger posting (018)
- Bulk transitions
