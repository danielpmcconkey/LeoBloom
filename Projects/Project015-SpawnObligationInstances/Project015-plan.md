# Project 015 — Spawn Obligation Instances: Plan

## Objective

Given an obligation agreement and a date range, generate `obligation_instance` rows
for each occurrence based on the agreement's cadence. This is the materialization
step — turning agreement terms into trackable instances. No status transitions, no
overdue detection, no ledger posting. Just spawning.

## Design Decisions

These are load-bearing. The Builder must not deviate without PO approval.

1. **Skip-not-error on duplicate dates.** Re-running spawn for an overlapping range
   is normal workflow. Skip existing `(agreement_id, expected_date)` pairs silently.
   Return count of skipped dates alongside created instances.

2. **Batch insert in a single transaction.** All instances from one spawn call succeed
   or fail atomically.

3. **Amount pre-fill from agreement.** If `agreement.amount` is `Some`, each instance
   gets that amount. If `None` (variable), instance amount is `None`.

4. **All spawned instances start as `Expected`.** Status transitions are project 016.

5. **Annual cadence: caller's startDate anchors the month.** `expectedDay` gives the
   day within that month. If the agreement says `expectedDay = 15` and the caller
   passes `startDate = 2026-06-01`, annual instances land on June 15 of each year.
   This avoids needing a separate "expected month" field on the agreement.

6. **OneTime: exactly one instance at startDate.** `expectedDay` is ignored for
   OneTime cadence. The instance lands on `startDate`. If one already exists for
   that date, it gets skipped (not error — consistent with decision #1).

7. **Name generation is pure.** No DB involvement. Format by cadence:
   - Monthly: `"Jan 2026"`, `"Feb 2026"`
   - Quarterly: `"Q1 2026"`, `"Q2 2026"`
   - Annual: `"2026"`, `"2027"`
   - OneTime: `"One-time"`

8. **`expectedDay` defaults to 1 when null.** Monthly and Quarterly cadences need a
   day. If the agreement doesn't specify one, use the 1st. Day clamping applies:
   `expectedDay = 31` in February becomes Feb 28 (or 29 in leap years).

9. **Quarterly: instances land on `expectedDay` of Jan, Apr, Jul, Oct.** These are
   calendar quarters, not fiscal. `expectedDay` applies the same way as Monthly —
   e.g., `expectedDay = 15` produces Jan 15, Apr 15, Jul 15, Oct 15. A date is
   included if it falls within `[startDate, endDate]` inclusive. So
   `startDate = 2026-03-15, endDate = 2026-09-30` produces Apr 15, Jul 15 (not
   Oct 15 — it's outside the range).

10. **No DB unique constraint added.** Uniqueness on `(obligation_agreement_id,
    expected_date)` is enforced at the application layer via `findExistingDates`.
    Acceptable for a single-user system. If multi-user becomes relevant, add a DB
    constraint.

11. **`findExistingDates` ignores `is_active`.** Soft-deleted instances still occupy
    their date slot. Re-spawning does NOT fill in deactivated dates. If a
    deactivated instance needs to be replaced, the caller must hard-delete the row
    first (or a future CRUD project for obligation instances can handle this).

## Phases

### Phase 1: Domain Types and Pure Logic

**What:** Add the `SpawnObligationInstancesCommand` and `SpawnResult` types to
`Ops.fs`. Add a pure module `ObligationInstanceSpawning` (also in `Ops.fs`) that
contains the date generation and name generation functions with zero DB dependencies.

**Files modified:**
- `Src/LeoBloom.Domain/Ops.fs` — append new types and pure module after
  `ObligationAgreementValidation`

**New types:**
```fsharp
type SpawnObligationInstancesCommand =
    { obligationAgreementId: int
      startDate: DateOnly
      endDate: DateOnly }

type SpawnResult =
    { created: ObligationInstance list
      skippedCount: int }
```

**Pure functions in `ObligationInstanceSpawning` module:**
- `generateExpectedDates : RecurrenceCadence -> int -> DateOnly -> DateOnly -> DateOnly list`
  - Takes cadence, effectiveDay (defaulted from expectedDay), start, end
  - Returns sorted list of expected dates within range (inclusive)
  - Monthly: iterate months, clamp day to month length
  - Quarterly: generate `expectedDay` of Jan, Apr, Jul, Oct for each year in range; include date if within `[start, end]`; clamp day to month length
  - Annual: one per year at anchor month + clamped day
  - OneTime: just `[startDate]`
- `generateInstanceName : RecurrenceCadence -> DateOnly -> string`
  - Pure date-to-label mapping per cadence
- `validateSpawnCommand : SpawnObligationInstancesCommand -> Result<unit, string list>`
  - `startDate <= endDate`
  - `obligationAgreementId > 0`

**Verification:** Unit tests for pure date generation cover all cadences, day
clamping (Feb 29/30/31), quarter boundaries, annual wrapping, and OneTime. These
tests need no database — they exercise pure functions only.

### Phase 2: Repository

**What:** Create `ObligationInstanceRepository.fs` in `LeoBloom.Utilities` with raw
SQL for obligation_instance persistence.

**Files created:**
- `Src/LeoBloom.Utilities/ObligationInstanceRepository.fs`

**Files modified:**
- `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` — add `<Compile>` entry after
  `ObligationAgreementService.fs`, before `OpeningBalanceService.fs`

**Repository functions:**
- `mapReader : DbDataReader -> ObligationInstance` — map all 14 columns
- `insert : NpgsqlTransaction -> ObligationAgreementId:int -> name:string -> status:InstanceStatus -> amount:decimal option -> expectedDate:DateOnly -> ObligationInstance`
  - INSERT with `is_active = true`, `status = 'expected'`, RETURNING *
- `findExistingDates : NpgsqlTransaction -> agreementId:int -> DateOnly list -> DateOnly Set`
  - `SELECT expected_date FROM ops.obligation_instance WHERE obligation_agreement_id = @id AND expected_date = ANY(@dates)`
  - Returns set of dates that already have instances (regardless of is_active —
    the uniqueness constraint is on the pair, not filtered by active status)

**Compile order in fsproj:**
```xml
<Compile Include="ObligationAgreementService.fs" />
<Compile Include="ObligationInstanceRepository.fs" />
<Compile Include="OpeningBalanceService.fs" />
```

**Verification:** Integration test that inserts an instance via repository, reads it
back, confirms all fields map correctly.

### Phase 3: Service

**What:** Create `ObligationInstanceService.fs` in `LeoBloom.Utilities` with the
spawn orchestration.

**Files created:**
- `Src/LeoBloom.Utilities/ObligationInstanceService.fs`

**Files modified:**
- `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` — add `<Compile>` entry after
  `ObligationInstanceRepository.fs`, before `OpeningBalanceService.fs`

**Service function:**
```
spawn : SpawnObligationInstancesCommand -> Result<SpawnResult, string list>
```

**Orchestration flow:**
1. Validate command (pure validation from Phase 1)
2. Open connection + transaction
3. Look up agreement by ID via `ObligationAgreementRepository.findById`
4. Validate: agreement exists, is active
5. Resolve effective day: `agreement.expectedDay |> Option.defaultValue 1`
6. Call pure `generateExpectedDates` with cadence, effective day, date range
7. Call `findExistingDates` to get already-occupied dates
8. Filter to new dates only
9. For each new date: generate name, insert via repository
10. Commit transaction
11. Return `SpawnResult` with created list and skipped count

**Compile order in fsproj:**
```xml
<Compile Include="ObligationInstanceRepository.fs" />
<Compile Include="ObligationInstanceService.fs" />
<Compile Include="OpeningBalanceService.fs" />
```

**Verification:** Integration tests covering the full spawn flow (see Phase 4).

### Phase 4: Tests

**What:** Full test suite for spawning. Split into pure unit tests (no DB) and
integration tests (hit the database).

**Files created:**
- `Src/LeoBloom.Tests/SpawnObligationInstanceTests.fs`

**Files modified:**
- `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` — add `<Compile>` entry after
  `ObligationAgreementTests.fs`
- `Src/LeoBloom.Tests/TestHelpers.fs` — add a new helper
  `insertObligationAgreementForSpawn` that takes cadence, expectedDay, and amount
  parameters. Do NOT modify existing helpers — other tests depend on them.

**Test categories:**

*Pure date generation tests (no DB):*
- Monthly: Jan-Jun 2026 with expectedDay=15 produces 6 dates
- Monthly: expectedDay=31 in Feb clamps to 28 (non-leap) / 29 (leap year)
- Monthly: expectedDay=None defaults to 1st
- Quarterly: Jan-Dec 2026 produces 4 dates (Jan, Apr, Jul, Oct)
- Quarterly: Q2-Q3 range produces 2 dates
- Annual: 2026-2028 range produces 3 dates
- OneTime: always produces exactly 1 date (the startDate)
- startDate > endDate rejected

*Pure name generation tests (no DB):*
- Monthly dates produce "Jan 2026" format
- Quarterly dates produce "Q1 2026" format
- Annual produces "2026" format
- OneTime produces "One-time"

*Integration tests (DB):*
- Spawn monthly instances for a 3-month range, verify 3 instances created with correct dates, names, status=Expected, amount pre-filled
- Spawn for variable-amount agreement (amount=None), verify instance amount is None
- Spawn overlapping range, verify existing dates skipped, new dates created, skippedCount accurate
- Spawn for inactive agreement returns error
- Spawn for nonexistent agreement returns error
- Spawn OneTime, verify single instance created
- Spawn OneTime when instance already exists, verify skippedCount=1, created=empty

**Verification:** `dotnet test` passes. All tests use the existing TestCleanup
pattern with tracker registration.

## Acceptance Criteria

**Behavioral (Gherkin scenarios):**
- [ ] Pure date generation produces correct dates for Monthly, Quarterly, Annual, and OneTime cadences
- [ ] Day clamping works correctly (e.g., day 31 in February becomes 28/29)
- [ ] `expectedDay` defaults to 1 when agreement has `None`
- [ ] Name generation produces cadence-appropriate labels
- [ ] Spawning for an overlapping date range skips existing dates without error
- [ ] `SpawnResult.skippedCount` accurately reflects how many dates were skipped
- [ ] All spawned instances have `status = Expected` and `isActive = true`
- [ ] Fixed-amount agreements pre-fill instance amount; variable agreements leave it `None`
- [ ] Spawning against an inactive agreement returns an error
- [ ] Spawning against a nonexistent agreement returns an error
- [ ] `startDate > endDate` is rejected by validation

**Structural (Builder/QE verification):**
- [ ] `SpawnObligationInstancesCommand` and `SpawnResult` types exist in `Ops.fs`
- [ ] `ObligationInstanceRepository` can insert and read back instances with all fields mapped
- [ ] `findExistingDates` correctly identifies occupied `(agreement_id, expected_date)` pairs
- [ ] `ObligationInstanceService.spawn` creates instances within a single transaction
- [ ] All tests pass via `dotnet test`

## Risks

- **Quarterly date alignment:** Does "Q1" always mean January, or does it depend on
  the fiscal year? The spec says Jan/Apr/Jul/Oct. If Dan's fiscal year starts in a
  different month, this needs revisiting. Mitigation: hardcode calendar quarters for
  now, flag as a potential future change.

- **Annual cadence month anchoring:** Using `startDate`'s month as the anchor is a
  design decision, not a spec mandate. If the caller passes different start dates on
  different spawn calls for the same annual agreement, they'd get instances in
  different months. Mitigation: document that the caller is responsible for
  consistent start dates. A future project could add `expectedMonth` to the agreement
  if this becomes a problem.

- **No DB unique constraint.** The uniqueness on `(agreement_id, expected_date)` is
  app-enforced only. Race conditions between concurrent spawn calls could create
  duplicates. Mitigation: acceptable for a single-user system. If multi-user becomes
  relevant, add a DB constraint.

## Out of Scope

- Status transitions (project 016)
- Overdue detection (project 017)
- Ledger posting (project 018)
- CRUD operations for individual obligation instances (separate concern)
- CLI integration (future project)
- Fiscal-year-aware quarter definitions
