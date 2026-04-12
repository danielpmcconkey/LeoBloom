# Project079 — Irregular Recurrence Cadence Plan

## Objective

Add `Irregular` as a valid `RecurrenceCadence` discriminated union case so
that agreements with non-computable schedules (like the HOA tri-annual pattern)
don't crash the CLI. Migrate agreement 8 from `tri_annual` to `irregular`.
Ensure spawn skips irregular agreements gracefully. Unblocks Hobson's
`obligation agreement list`.

## Phases

### Phase 1: Domain — Add Irregular DU Case

**What:** Extend `RecurrenceCadence` with an `Irregular` case and wire it
through all pattern matches.

**Files modified:**

| File | Change |
|------|--------|
| `Src/LeoBloom.Domain/Ops.fs:19` | Add `\| Irregular` to `RecurrenceCadence` DU |
| `Src/LeoBloom.Domain/Ops.fs` (toString) | Add `\| Irregular -> "irregular"` |
| `Src/LeoBloom.Domain/Ops.fs` (fromString) | Add `\| "irregular" -> Ok Irregular` |
| `Src/LeoBloom.Domain/Ops.fs` (generateExpectedDates ~line 255) | Add `\| Irregular -> []` — returns no dates |
| `Src/LeoBloom.Domain/Ops.fs` (generateInstanceName ~line 292) | Add `\| Irregular -> date.ToString("yyyy-MM-dd", ...)` (fallback label if ever called) |

**Verification:** Project compiles with no incomplete match warnings.

### Phase 2: CLI — Update Help Text

**What:** Add `irregular` to the cadence option descriptions so the CLI
advertises it as valid.

**Files modified:**

| File | Change |
|------|--------|
| `Src/LeoBloom.CLI/ObligationCommands.fs` (parseCadence error message ~line 229) | Add `irregular` to the expected-cadences string |
| `Src/LeoBloom.CLI/ObligationCommands.fs` (help strings) | Add `irregular` wherever cadence options are listed |

**Verification:** `obligation agreement create --help` shows `irregular` as a
valid cadence option.

### Phase 3: Migration — Add Cadence Row and Fix Agreement 8

**What:** SQL migration to insert the `irregular` cadence into the lookup
table and update agreement 8.

**File created:**

| File | Content |
|------|---------|
| `Src/LeoBloom.Migrations/Migrations/1712000025000_AddIrregularCadence.sql` | Insert cadence row + update agreement 8 |

**Migration UP:**
```sql
INSERT INTO ops.cadence (name) VALUES ('irregular');
UPDATE ops.obligation_agreement SET cadence = 'irregular' WHERE cadence = 'tri_annual';
```

**Migration DOWN:**
```sql
UPDATE ops.obligation_agreement SET cadence = 'tri_annual' WHERE cadence = 'irregular';
DELETE FROM ops.cadence WHERE name = 'irregular';
```

**Verification:** After migration, `SELECT cadence FROM ops.obligation_agreement WHERE id = 8`
returns `'irregular'`.

### Phase 4: Tests

**What:** Add test coverage for the new cadence case.

**Files modified:**

| File | Change |
|------|--------|
| `Src/LeoBloom.Tests/DomainTests.fs` (~line 174) | Add `[<InlineData("irregular")>]` to round-trip theory |
| `Src/LeoBloom.Tests/SpawnObligationInstanceTests.fs` | Add test: `generateExpectedDates Irregular` returns `[]` |

**Verification:** `dotnet test` passes with new and existing tests green.

## Acceptance Criteria

### Behavioral (→ Gherkin scenarios)
- [ ] `obligation agreement list` no longer crashes when irregular-cadence agreements exist in DB
- [ ] `obligation agreement show 8` displays cadence as `irregular`
- [ ] `obligation instance spawn` for an irregular agreement produces 0 instances and no error

### Structural (→ Builder/QE verify directly)
- [ ] `RecurrenceCadence` DU has 5 cases: Monthly, Quarterly, Annual, OneTime, Irregular
- [ ] Migration `1712000025000_AddIrregularCadence.sql` exists with UP and DOWN sections
- [ ] No incomplete pattern-match warnings in build output
- [ ] `dotnet test` all green

## Risks

| Risk | Mitigation |
|------|-----------|
| Other agreements might also have bad cadence values in DB | Migration uses `WHERE cadence = 'tri_annual'` which covers all known bad rows. If others exist, `fromString` will still fail loudly — we catch them as they surface rather than guessing. |
| `generateInstanceName` called on Irregular unexpectedly | Returns a date-based fallback — safe, not pretty, but won't crash. |

No GAAP implications — cadence metadata doesn't affect amounts, balances, or
the accounting equation. This is scheduling/workflow only.

## Out of Scope

- Automatic schedule computation for irregular agreements (manual via Saturday procedure)
- UI/reporting changes for irregular cadence display beyond what toString provides
- Backfilling instance history for agreement 8's past due dates
