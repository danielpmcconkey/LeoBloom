# Project 038 — CLI Obligation Commands — Plan

## Objective

Expose obligation agreement and instance services through the CLI, following the
established parse-call-format-exit pattern (ADR-003). One new backend query
(`upcoming`) provides the nagging agent's primary feed. Everything else wraps
existing service methods.

## Inventory of New Work

### Backend (LeoBloom.Ops)

The instance side is missing `list` and `upcoming` — both the repo and service
need new methods. Agreements already have full CRUD.

| Method | Layer | Exists? | Notes |
|--------|-------|---------|-------|
| `ObligationInstanceRepository.list` | Repo | **No** | Filtered query: status, due-before, due-after |
| `ObligationInstanceService.list` | Service | **No** | Thin wrapper around repo list |
| `ObligationInstanceRepository.findUpcoming` | Repo | **No** | status IN (expected, in_flight) AND expected_date <= ref+N |
| `ObligationInstanceService.findUpcoming` | Service | **No** | Thin wrapper, default N=30 |

### CLI (LeoBloom.CLI)

| File | Exists? | Notes |
|------|---------|-------|
| `ObligationCommands.fs` | **No** | Three-level DU: `obligation agreement ...`, `obligation instance ...`, `obligation overdue`, `obligation upcoming` |
| `OutputFormatter.fs` | Yes | Add formatters for `ObligationAgreement`, `ObligationInstance`, and their list forms |
| `Program.fs` | Yes | Wire `Obligation` subcommand |
| `LeoBloom.CLI.fsproj` | Yes | Add `ObligationCommands.fs` to compile order |

---

## Phases

### Phase 1: Backend — Instance List & Upcoming

**What:**
- Add `ListInstancesFilter` type to top of `ObligationInstanceRepository.fs`
  (follows `ListAgreementsFilter` pattern)
  ```fsharp
  type ListInstancesFilter =
      { status: InstanceStatus option
        dueBefore: DateOnly option
        dueAfter: DateOnly option }
  ```
- Add `ObligationInstanceRepository.list` — parameterized WHERE with status,
  `expected_date <= @due_before`, `expected_date >= @due_after`, ordered by
  `expected_date`. Only returns active instances (unless we add a flag later).
- Add `ObligationInstanceService.list` — open conn/txn, delegate to repo, same
  pattern as `ObligationAgreementService.list`.
- Add `ObligationInstanceRepository.findUpcoming` — query:
  ```sql
  SELECT ... FROM ops.obligation_instance
  WHERE status IN ('expected', 'in_flight')
    AND is_active = true
    AND expected_date <= @horizon
    AND expected_date >= @today
  ORDER BY expected_date
  ```
- Add `ObligationInstanceService.findUpcoming (today: DateOnly) (days: int)` —
  computes horizon = today.AddDays(days), delegates to repo.

**Files modified:**
- `Src/LeoBloom.Ops/ObligationInstanceRepository.fs` (add `ListInstancesFilter`, `list`, `findUpcoming`)
- `Src/LeoBloom.Ops/ObligationInstanceService.fs` (add `list`, `findUpcoming`)

**Verification:** `dotnet build Src/LeoBloom.Ops/LeoBloom.Ops.fsproj` succeeds.

---

### Phase 2: Output Formatting

**What:** Add human-readable and list formatters for obligation types to
`OutputFormatter.fs`. Follow existing patterns exactly.

- `formatObligationAgreement` — single agreement detail display
- `formatObligationAgreementList` — tabular list
- `formatObligationInstance` — single instance detail display
- `formatObligationInstanceList` — tabular list
- `formatOverdueResult` — transitioned count + error list (custom, not via `formatHuman`)
- `formatSpawnResult` — created count + skipped count + instance list (custom)
- `formatPostToLedgerResult` — journal entry ID + instance ID (custom)
- `formatUpcomingList` — reuse instance list formatter (or alias)
- Add `ObligationAgreement` and `ObligationInstance` cases to `formatHuman` dispatch
- Add `writeAgreementList`, `writeInstanceList` dedicated list writers (follows
  `writeTransferList`/`writeAccountList` pattern to avoid F# type erasure)
- Add `writeOverdueResult`, `writeSpawnResult`, `writePostResult` dedicated writers
  for non-entity result types

**Files modified:**
- `Src/LeoBloom.CLI/OutputFormatter.fs`

**Verification:** `dotnet build Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` succeeds.

---

### Phase 3: CLI Command Module

**What:** Create `ObligationCommands.fs` with the three-level Argu DU structure.

**Argu DU hierarchy:**
```
ObligationArgs
  ├── Agreement (ParseResults<AgreementArgs>)
  │     ├── List (ParseResults<AgreementListArgs>)
  │     ├── Show (ParseResults<AgreementShowArgs>)
  │     ├── Create (ParseResults<AgreementCreateArgs>)
  │     ├── Update (ParseResults<AgreementUpdateArgs>)
  │     └── Deactivate (ParseResults<AgreementDeactivateArgs>)
  ├── Instance (ParseResults<InstanceArgs>)
  │     ├── List (ParseResults<InstanceListArgs>)
  │     ├── Spawn (ParseResults<InstanceSpawnArgs>)
  │     ├── Transition (ParseResults<InstanceTransitionArgs>)
  │     └── Post (ParseResults<InstancePostArgs>)
  ├── Overdue (ParseResults<OverdueArgs>)
  └── Upcoming (ParseResults<UpcomingArgs>)
```

**Command → Service mapping:**

| Command | Service Call |
|---------|-------------|
| `agreement list` | `ObligationAgreementService.list` with `ListAgreementsFilter` |
| `agreement show <id>` | `ObligationAgreementService.getById` |
| `agreement create` | `ObligationAgreementService.create` |
| `agreement update <id>` | `ObligationAgreementService.update` |
| `agreement deactivate <id>` | `ObligationAgreementService.deactivate` |
| `instance list` | `ObligationInstanceService.list` with `ListInstancesFilter` |
| `instance spawn <agreement-id>` | `ObligationInstanceService.spawn` |
| `instance transition <id>` | `ObligationInstanceService.transition` |
| `instance post <id>` | `ObligationPostingService.postToLedger` |
| `overdue` | `ObligationInstanceService.detectOverdue` |
| `upcoming` | `ObligationInstanceService.findUpcoming` |

**Create args (all mandatory except noted):**
- `--name`, `--type` (receivable|payable), `--cadence` (monthly|quarterly|annual|one_time)
- Optional: `--counterparty`, `--amount`, `--expected-day`, `--payment-method`, `--source-account`, `--dest-account`, `--notes`

**Update args:** Same as create plus mandatory `<id>` main command, plus `--active/--inactive`.

**Transition args:**
- `<instance-id>` main command, `--to STATUS` mandatory
- Optional: `--amount`, `--date` (confirmed date), `--notes`, `--journal-entry-id`

**Files created:**
- `Src/LeoBloom.CLI/ObligationCommands.fs`

**Files modified:**
- `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` (add `ObligationCommands.fs` before `Program.fs`)
- `Src/LeoBloom.CLI/Program.fs` (add `Obligation` case to `LeoBloomArgs` DU and dispatch)

**Verification:** `dotnet build Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` succeeds.
`dotnet run --project Src/LeoBloom.CLI -- obligation --help` prints usage.

---

## Acceptance Criteria

- [ ] `leobloom obligation agreement list` returns agreements (tabular or JSON)
- [ ] `leobloom obligation agreement list --type receivable` filters by type
- [ ] `leobloom obligation agreement list --cadence monthly` filters by cadence
- [ ] `leobloom obligation agreement list --inactive` includes inactive agreements
- [ ] `leobloom obligation agreement show <id>` returns agreement detail
- [ ] `leobloom obligation agreement create --name ... --type ... --cadence ...` creates agreement
- [ ] `leobloom obligation agreement update <id> --name ... --type ... --cadence ...` updates agreement
- [ ] `leobloom obligation agreement deactivate <id>` deactivates agreement
- [ ] `leobloom obligation instance list` returns instances (tabular or JSON)
- [ ] `leobloom obligation instance list --status expected` filters by status
- [ ] `leobloom obligation instance list --due-before DATE` filters by date
- [ ] `leobloom obligation instance list --due-after DATE` filters by date
- [ ] `leobloom obligation instance spawn <agreement-id> --from DATE --to DATE` spawns instances
- [ ] `leobloom obligation instance transition <id> --to STATUS` transitions instance
- [ ] `leobloom obligation instance post <id>` posts instance to ledger
- [ ] `leobloom obligation overdue` runs overdue detection (defaults to today)
- [ ] `leobloom obligation overdue --as-of DATE` runs overdue detection for given date
- [ ] `leobloom obligation upcoming` returns upcoming instances (default 30 days)
- [ ] `leobloom obligation upcoming --days 7` returns upcoming instances within 7 days
- [ ] All commands support `--json` flag for JSON output
- [ ] Exit codes follow `ExitCodes` convention (0 success, 1 business error, 2 system error)
- [ ] No business logic in CLI layer — all logic delegated to services

## Risks

- **Three-level Argu nesting** (`obligation agreement list`): Argu handles this
  fine with `CliPrefix.None` on intermediate DUs. Same pattern as existing commands
  but one level deeper. Test the help output.
- **`create` has many args**: Some mandatory, some optional. Argu handles this
  natively with `Mandatory` attribute. No custom validation needed in CLI.
- **Overdue detection returns a result struct, not an entity**: Need a custom
  formatter for `OverdueDetectionResult` — it's not a domain record that fits
  `formatHuman`'s existing pattern. Write a dedicated format function.
- **Type erasure on list formatters**: F# erases generic list types at runtime.
  Follow the existing `writeTransferList`/`writeAccountList` pattern with
  dedicated write functions rather than relying on `formatHuman` dispatch.

## Out of Scope

- Service-level tests for `upcoming` (P038 is CLI-only per ADR-003; the new
  service method is tested as part of CLI integration)
- Gherkin specs (Gherkin Writer's job)
- Any changes to obligation business logic
- `instance show <id>` — not in the spec, don't add it
