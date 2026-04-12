# P074 — Npgsql Version Alignment — Plan

## Objective

Align all projects in the LeoBloom solution to Npgsql 10.0.1. The Migrations
project was already bumped by Hobson to unblock a prod migration. The remaining
six projects are on 9.0.3, creating a version split and NU1605 warnings. This
plan bumps them all to 10.0.1 for consistency.

## Assessment: Bump All vs Split

**Decision: Bump all to 10.0.1.**

Rationale:
- Migrondi.Core 1.2.0 (used by Migrations) requires Npgsql >= 10.0.1
- Mixed 9.x/10.x across projects that share transitive references causes NU1605
  downgrade warnings and risks runtime assembly binding failures
- The codebase uses stable Npgsql APIs (NpgsqlDataSource, NpgsqlDataSourceBuilder,
  NpgsqlConnection, NpgsqlCommand, NpgsqlTransaction) — none of these have
  breaking changes in 9→10
- No timestamp remapping risk (the major Npgsql timestamp breaking change was in
  6.0, long since resolved)
- A split would add ongoing maintenance burden for zero benefit

## Breaking Change Assessment

Reviewed all Npgsql usage across the solution. The codebase uses:
- `NpgsqlDataSourceBuilder` / `NpgsqlDataSource` (DataSource.fs)
- `NpgsqlConnection` / `NpgsqlCommand` / `NpgsqlTransaction` (all repositories)
- Standard parameter binding via `cmd.Parameters.AddWithValue`
- `ConnectionStringBuilder` properties (ApplicationName, Timeout, CommandTimeout)

**Risk: None.** These APIs are stable across the 9→10 boundary. Npgsql 10.0
deprecated some legacy connection string keywords and removed some obsolete
type handler APIs, but none of those are used here.

## Phases

### Phase 1: Version Bump (Single Commit)

**What:** Change `Version="9.0.3"` → `Version="10.0.1"` for the Npgsql
PackageReference in all six .fsproj files.

**Files Modified:**
- `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj`
- `Src/LeoBloom.Ledger/LeoBloom.Ledger.fsproj`
- `Src/LeoBloom.Ops/LeoBloom.Ops.fsproj`
- `Src/LeoBloom.Portfolio/LeoBloom.Portfolio.fsproj`
- `Src/LeoBloom.Reporting/LeoBloom.Reporting.fsproj`
- `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj`

**Verification:**
1. `dotnet restore` completes without NU1605 warnings
2. `dotnet build` succeeds with no errors or Npgsql-related warnings
3. `dotnet test` — all tests pass

### Phase 2: N/A

This is a single-phase task. No code changes beyond the version bump are needed.

## Acceptance Criteria

- [ ] All seven Npgsql-referencing projects specify `Version="10.0.1"`
- [ ] `dotnet restore` produces zero NU1605 warnings
- [ ] `dotnet build` succeeds cleanly
- [ ] All tests pass (`dotnet test`)
- [ ] Decision to bump-all (vs split) is documented (this plan)

## Risks

- **Low:** A test might fail due to subtle behavioral difference in Npgsql 10's
  query execution. Mitigation: the test suite is comprehensive and will surface
  this immediately. Fix would be targeted to the specific API change.

## Out of Scope

- Upgrading Npgsql.FSharp or other Npgsql ecosystem packages (not used)
- Pinning a solution-wide Npgsql version via Directory.Build.props (nice-to-have
  but not part of this card)
- Updating Migrondi.Core version
