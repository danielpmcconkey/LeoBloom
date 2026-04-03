---
title: "feat: Project 2 — Test Harness & Configuration"
type: feat
status: completed
date: 2026-04-03
deepened: 2026-04-03
---

# Project 2 — Test Harness & Configuration

## Enhancement Summary

**Deepened on:** 2026-04-03
**Agents used:** architecture-strategist, code-simplicity-reviewer, data-integrity-guardian, pattern-recognition-specialist, performance-oracle, security-sentinel, spec-flow-analyzer, context7-docs (TickSpec + Npgsql)

### Key Improvements
1. **Mandatory try/finally cleanup** in all Then steps — flagged by every reviewer as a correctness requirement, not optional
2. **Prod safety guard** — runtime `SELECT current_database()` check + removal of production appsettings from test project
3. **Parameterized SQL** — even in tests, to avoid establishing a string-interpolation-in-SQL pattern
4. **CopyToOutputDirectory** for appsettings — gap in original plan that would cause runtime failure
5. **Migrations invocation contract change** documented — switching from `args[0]` to `LEOBLOOM_ENV`
6. **ON DELETE RESTRICT tests added** — 18 new scenarios covering every FK, plus 1 missing nullable scenario
7. **Nullable column testing policy** documented in feature file headers

---

## Overview

Wire up executable Gherkin tests for the 88 structural constraint scenarios (69 existing + 1 missing nullable + 18 new ON DELETE RESTRICT), unify the connection string strategy across all projects, and scaffold the Domain.Tests project for Project 3.

BRD: `Projects/Project002-TestHarness/Project002-brd.md`

## Problem Statement / Motivation

Project 1 delivered 18 migration scripts and 69 Gherkin scenarios specifying structural constraints across the `ledger` and `ops` schemas. Those scenarios are currently just prose — nothing executes them. Without executable tests, schema regressions are invisible until something blows up in the application layer. Additionally, the existing scenarios only cover insert-direction constraints — every FK specifies `ON DELETE RESTRICT`, but nothing tests that delete behavior.

Additionally, the Migrations project duplicates the connection string resolution logic that already exists in `LeoBloom.Dal.ConnectionString`. Two codepaths that do the same thing will eventually diverge.

## Proposed Solution

### 1. TickSpec + xUnit in `LeoBloom.Dal.Tests`

**Packages:** `TickSpec` 2.0.4, `TickSpec.Xunit` 2.0.4 (both .NET Standard 2.0 — confirmed compatible with .NET 10).

**Feature file strategy:** The `.feature` files live in `Projects/Project001-Database/Specs/Acceptance/`. Rather than duplicating them, link them into the test project as embedded resources via MSBuild `<EmbeddedResource>` with `Link` paths. TickSpec loads features via `Assembly.GetManifestResourceStream()` — embedded resources are mandatory, not optional.

```xml
<!-- In LeoBloom.Dal.Tests.fsproj -->
<ItemGroup>
  <EmbeddedResource Include="..\..\Projects\Project001-Database\Specs\Acceptance\LedgerStructuralConstraints.feature"
                    Link="LedgerStructuralConstraints.feature" />
  <EmbeddedResource Include="..\..\Projects\Project001-Database\Specs\Acceptance\OpsStructuralConstraints.feature"
                    Link="OpsStructuralConstraints.feature" />
  <EmbeddedResource Include="..\..\Projects\Project001-Database\Specs\Acceptance\DeleteRestrictionConstraints.feature"
                    Link="DeleteRestrictionConstraints.feature" />
</ItemGroup>
```

#### Research Insights: Feature File Discovery

- The embedded resource name follows `{AssemblyName}.{LinkFileName}`, e.g. `LeoBloom.Dal.Tests.LedgerStructuralConstraints.feature`. Since the `.fsproj` doesn't set `<RootNamespace>`, it defaults to the project name. Verify early with `Assembly.GetExecutingAssembly().GetManifestResourceNames()` — a wrong resource name produces a `NullReferenceException`, not a "resource not found" error.
- Wildcard `<EmbeddedResource Include="**/*.feature" />` works but still requires a `[<Theory>]` method per feature file.

**Step definition layout:**

```
LeoBloom.Dal.Tests/
  SharedSteps.fs              -- Given/Then steps (connection lifecycle + assertions)
  LedgerStepDefinitions.fs    -- When steps for ledger schema scenarios
  OpsStepDefinitions.fs       -- When steps for ops schema scenarios
  DeleteRestrictionSteps.fs   -- When steps for ON DELETE RESTRICT scenarios
  FeatureFixture.fs           -- xUnit Theory wiring (LAST in compile order)
  appsettings.Development.json
```

F# compile order matters. The `.fsproj` `<Compile>` items must list step definitions before the fixture.

> **Note on SharedSteps.fs naming:** This file contains Given steps (connection setup) and Then steps (assertions + cleanup), not just "shared" logic. `GivenThenSteps.fs` would be more precise, but `SharedSteps.fs` is the conventional TickSpec naming. Add a module doc comment explaining the split: SharedSteps owns lifecycle + assertions, the per-schema files own the When steps.

**xUnit wiring glue** (`FeatureFixture.fs`):

```fsharp
module LeoBloom.Dal.Tests.FeatureFixture

open TickSpec.Xunit
open Xunit

let source = AssemblyStepDefinitionsSource(System.Reflection.Assembly.GetExecutingAssembly())
let scenarios resourceName =
    source.ScenariosFromEmbeddedResource resourceName
    |> MemberData.ofScenarios

[<Theory; MemberData("scenarios", "LeoBloom.Dal.Tests.LedgerStructuralConstraints.feature")>]
let ``Ledger structural constraints`` (scenario: XunitSerializableScenario) =
    source.RunScenario(scenario)

[<Theory; MemberData("scenarios", "LeoBloom.Dal.Tests.OpsStructuralConstraints.feature")>]
let ``Ops structural constraints`` (scenario: XunitSerializableScenario) =
    source.RunScenario(scenario)

[<Theory; MemberData("scenarios", "LeoBloom.Dal.Tests.DeleteRestrictionConstraints.feature")>]
let ``Delete restriction constraints`` (scenario: XunitSerializableScenario) =
    source.RunScenario(scenario)
```

Each scenario becomes an individual xUnit test case — 88 distinct pass/fail results in `dotnet test`.

### 2. State Sharing & Cleanup Strategy

**Context record** passed between steps via TickSpec's type-to-instance cache:

```fsharp
type ScenarioContext = {
    Transaction: NpgsqlTransaction
    LastException: exn option
}
```

> **Simplified from original draft.** `Connection` field removed — access via `ctx.Transaction.Connection`. One fewer field to keep in sync.

- `Given` opens a connection via `ConnectionString.resolve`, begins a transaction, returns `ScenarioContext`.
- `When` executes the test insert within the transaction, catches any `PostgresException`, returns updated context.
- `Then` asserts on the exception (or lack thereof), **always rolls back + disposes in a `finally` block**.

**Critical: TickSpec cache behavior dependency.** The `{ ctx with LastException = ex }` pattern works because TickSpec 2.x replaces the cached instance when a step returns a new value of the same type. If a When step forgets to return the updated context, the Then step receives stale state with `LastException = None` and the test falsely passes. Every When step MUST return the modified record.

**Why transactions?** Two reasons:
- **Violation tests** (the majority): the insert fails, nothing was written, but the PG transaction is in an aborted state. `ROLLBACK` always succeeds on aborted transactions — clean exit.
- **Success tests** (nullable field scenarios): the insert succeeds within the transaction. `ROLLBACK` undoes it — no test data persists.

No `DELETE` statements needed. No cleanup races.

#### Research Insights: Connection Pooling

Npgsql has **built-in connection pooling enabled by default**. When code calls `new NpgsqlConnection(connStr)` + `Open()`, it pulls from an internal pool. `Dispose()` returns it to the pool, not closing the socket. The default pool settings (`Min Pool Size=0`, `Max Pool Size=100`) mean the first scenario opens the real TCP connection and the remaining 68 reuse it. **Do not add `Pooling=false` to the connection string.**

Estimated per-scenario overhead: ~2-5ms on localhost. Total for 69 scenarios: ~150-350ms. The bottleneck is .NET startup (~2-3 seconds), not database round-trips. **Total test suite time: ~3-5 seconds.**

**xUnit parallelism must be disabled.** Multiple scenarios hitting the same tables concurrently will cause flaky UNIQUE violation tests. Add to `SharedSteps.fs`:

```fsharp
[<assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)>]
do ()
```

> This only affects the Dal.Tests assembly. Domain.Tests (future pure logic tests) can run in parallel independently.

### 3. Step Definition Patterns

**MANDATORY: try/finally in ALL Then steps.** If an assertion throws before rollback, the connection leaks. Npgsql will detect the dirty state on next pool checkout and reset the connection, but that's relying on a safety net instead of being correct by design.

**Cleanup helper** (in SharedSteps.fs):

```fsharp
let cleanup (ctx: ScenarioContext) =
    try ctx.Transaction.Rollback() with _ -> ()
    try ctx.Transaction.Connection.Dispose() with _ -> ()
```

Three categories of scenarios, three patterns:

**NOT NULL violations** (e.g., "insert into account_type with a null name"):

```fsharp
let [<When>] ``I insert into account_type with a null name`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ledger.account_type (name, normal_balance) VALUES (NULL, 'debit')"
    { ctx with LastException = ex }
```

**FK violations** (e.g., "insert into account with account_type_id 9999"):

```fsharp
let [<When>] ``I insert into account with account_type_id 9999`` (ctx: ScenarioContext) =
    use cmd = new NpgsqlCommand("INSERT INTO ledger.account (code, name, account_type_id) VALUES ('XXXX', 'Test', @id)", ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@id", 9999) |> ignore
    let ex = try cmd.ExecuteNonQuery() |> ignore; None with :? PostgresException as e -> Some (e :> exn)
    { ctx with LastException = ex }
```

> **Parameterized SQL.** The original draft used string interpolation (`$"... {id}"`). Even though the value is an int from a Gherkin regex, string interpolation in SQL is a pattern that gets copy-pasted into application code. Use `NpgsqlParameter` from the start so the precedent is clean. Since every FK test uses a literal bogus ID (9999), the step name can be a literal match instead of a regex capture.

**UNIQUE violations** (e.g., "Given an account_type 'asset' exists" + "insert another with name 'asset'"):
The seeded data from migrations already provides the existing rows (account_type has asset/liability/equity/revenue/expense). For non-seeded tables, the `Given` step inserts a row within the transaction first.

**Success tests** (e.g., "insert a valid account with a null parent_code"):

```fsharp
let [<Then>] ``the insert succeeds`` (ctx: ScenarioContext) =
    try
        match ctx.LastException with
        | None -> () // pass
        | Some ex -> Assert.Fail($"Expected insert to succeed but got: {ex.Message}")
    finally
        cleanup ctx
```

**Shared Then steps** — the assertion steps are reusable across both features:

| Step | PostgreSQL SqlState | Npgsql Property |
|---|---|---|
| `the insert is rejected with a NOT NULL violation` | `23502` | `PostgresException.SqlState` |
| `the insert is rejected with a UNIQUE violation` | `23505` | `PostgresException.SqlState` |
| `the insert is rejected with a FK violation` | `23503` | `PostgresException.SqlState` |
| `the insert succeeds` | (no exception) | — |

```fsharp
let [<Then>] ``the insert is rejected with a NOT NULL violation`` (ctx: ScenarioContext) =
    try
        match ctx.LastException with
        | Some (:? PostgresException as pgEx) ->
            Assert.Equal("23502", pgEx.SqlState)
        | Some ex ->
            Assert.Fail($"Expected PostgresException but got: {ex.GetType().Name}")
        | None ->
            Assert.Fail("Expected NOT NULL violation but insert succeeded")
    finally
        cleanup ctx
```

#### Research Insights: PostgresException Properties

Beyond `SqlState`, `PostgresException` exposes `ConstraintName`, `TableName`, `SchemaName`, and `ColumnName`. For more precise assertions in the future, you could assert on `ConstraintName` (e.g., `account_type_name_key` for the UNIQUE constraint on name). Not needed for this project but available.

### 4. Prod Safety

**Defense in depth — three layers:**

1. **PostgreSQL role restriction.** The `claude` role has no permissions on `leobloom_prod`. This is the primary gate.
2. **Environment default.** `LEOBLOOM_ENV` defaults to `Development` if unset. Prod is opt-in.
3. **Runtime database name guard.** Add to the `Given` step that opens connections:

```fsharp
// In SharedSteps.fs, after opening connection
use dbCheck = ctx.Transaction.Connection.CreateCommand()
dbCheck.CommandText <- "SELECT current_database()"
dbCheck.Transaction <- ctx.Transaction
let dbName = dbCheck.ExecuteScalar() :?> string
if dbName <> "leobloom_dev" then
    failwith $"SAFETY: Tests connected to '{dbName}' instead of 'leobloom_dev'. Aborting."
```

**No `appsettings.Production.json` in Dal.Tests.** Nobody runs structural constraint tests against prod. Creating this file is YAGNI and a security surface — it would contain prod infrastructure details in source control for no reason. If `LEOBLOOM_ENV` is somehow set to `Production`, the tests should fail fast with "file not found" rather than silently connect to prod.

### 5. Refactor Migrations to Use `Dal.ConnectionString.resolve`

**Current state:** `Src/LeoBloom.Migrations/Program.fs` lines 9-36 duplicate the config loading logic from `Dal.ConnectionString.resolve`. It also takes the environment from `args[0]` instead of `LEOBLOOM_ENV`.

**Target state:**
- Add `<ProjectReference>` to `LeoBloom.Dal` in the Migrations `.fsproj`.
- Replace the inline config loading with `LeoBloom.Dal.ConnectionString.resolve AppContext.BaseDirectory`.
- Remove the `args`-based environment selection — `LEOBLOOM_ENV` is the sole mechanism.
- Remove the `Microsoft.Extensions.Configuration.*` package references from Migrations (no longer used directly after the refactor).

> **Invocation contract change:** This changes how Migrations selects the environment. Anyone calling `dotnet run -- Production` must switch to `LEOBLOOM_ENV=Production dotnet run`. If there are scripts or CI configs that pass args, they'll silently fall back to Development. Document this in the PR description.

> **Preserve the migrondi schema bootstrap.** Lines 44-51 of `Program.fs` create the `migrondi` schema before Migrondi runs. This code stays — only the connection string resolution changes. Don't accidentally delete it when ripping out the old config code.

**Migrations still needs its own `appsettings.*.json`** because its connection string has `Search Path=migrondi` (for the Migrondi journal table), which differs from the Dal tests' search path.

> **Search path is per-consumer by design.** Add a comment to `ConnectionString.fs` noting that search path lives in each project's appsettings, not in the shared resolver.

### 6. `LeoBloom.Domain.Tests` Scaffolding

Already exists and builds. **Leave it alone.** The BRD says to add TickSpec packages "ready for Project 3," but `dotnet add package` takes 5 seconds when Project 3 actually starts. Adding a dependency now that does nothing risks version drift by the time it's needed. The existing placeholder test (`Assert.True(true)`) keeps `dotnet test` happy.

### 7. `appsettings.Development.json` for Dal.Tests

```json
{
  "ConnectionStrings": {
    "LeoBloom": "Host=172.18.0.1;Port=5432;Database=leobloom_dev;Username=claude;Password={LEOBLOOM_DB_PASSWORD};Search Path=ledger,ops,public"
  }
}
```

Note `Search Path=ledger,ops,public` — the tests need both schemas visible for inserts and FK resolution.

**CRITICAL: Must add CopyToOutputDirectory to the .fsproj:**

```xml
<ItemGroup>
  <None Include="appsettings.Development.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Without this, `ConnectionString.resolve AppContext.BaseDirectory` will throw "ConnectionStrings:LeoBloom not found" at runtime because the file isn't in the test output directory. The Migrations project already does this correctly.

## Technical Considerations

- **Npgsql PostgresException**: The `SqlState` property gives the 5-character SQLSTATE code. This is the assertion target, not exception messages (which vary by locale/version).
- **Seeded data dependencies**: Several UNIQUE violation tests depend on seed data from migrations (account_type names, fiscal_period keys, obligation_status names, etc.). Tests assume migrations have run against `leobloom_dev`. This is a prerequisite, not something the tests set up.
- **Cross-schema FKs**: `ops.obligation_agreement` references `ledger.account(id)`, `ops.obligation_instance` references `ledger.journal_entry(id)`, etc. The test connection needs visibility into both schemas.
- **TickSpec resource naming gotcha**: If the embedded resource name in `MemberData` doesn't match exactly, you get a `NullReferenceException` — not a helpful error. Verify with `GetManifestResourceNames()` during initial setup.
- **Connection disposal**: TickSpec does NOT call `IDisposable.Dispose()` on cached context instances. The `cleanup` helper in every Then step's `finally` block handles this.
- **Module style note**: `ConnectionString.fs` uses namespace-scoped modules (`namespace LeoBloom.Dal` + `module ConnectionString =`). The test files use top-level module declarations (`module LeoBloom.Dal.Tests.FeatureFixture`). Both are valid F#. Top-level modules are the right choice for test files since TickSpec discovers steps via assembly scanning, not namespace.

### Research Insights: Fail-Fast on Missing Password

`ConnectionString.resolve` currently defaults to empty string when `LEOBLOOM_DB_PASSWORD` is unset. This silently produces a connection string with `Password=;` which fails with a confusing Npgsql auth error. Consider changing to fail-fast:

```fsharp
let password =
    match Environment.GetEnvironmentVariable "LEOBLOOM_DB_PASSWORD" with
    | null | "" -> failwith "LEOBLOOM_DB_PASSWORD environment variable is required"
    | p -> p
```

This is a minor improvement to `ConnectionString.fs` that benefits all consumers. Not strictly a Project 2 deliverable, but it's a 3-line change while we're in the file's neighborhood.

## System-Wide Impact

- **Interaction graph**: Test execution → `ConnectionString.resolve` → `appsettings.{env}.json` → env vars → Npgsql → PostgreSQL. No callbacks, no middleware.
- **Error propagation**: PostgresException bubbles from Npgsql → caught in When step → asserted in Then step. Uncaught exceptions fail the xUnit test with a stack trace.
- **State lifecycle risks**: ~~If a Then step throws before rollback, the connection leaks.~~ **Mitigated.** All Then steps use `try/finally` with the `cleanup` helper. Rollback + dispose happens regardless of assertion outcome.
- **API surface parity**: No API changes. This is test infrastructure only.
- **Integration test scenarios**: These ARE the integration tests — 88 scenarios hitting the real database.
- **Cross-assembly note**: Disabling parallelism in Dal.Tests does not affect Domain.Tests. If Domain.Tests eventually has its own DB tests, cross-assembly concurrency would need its own solution.

## Spec Coverage — Resolved

### New feature file: `Projects/Project001-Database/Specs/Acceptance/DeleteRestrictionConstraints.feature`

Every FK uses `ON DELETE RESTRICT`. Each gets its own scenario — a migration could change any single one to CASCADE independently. Test pattern: insert parent + child within transaction, DELETE parent, assert FK violation (23503).

**18 scenarios organized by parent table:**

```
Ledger schema (13 FKs):

  account_type as parent:
    1. Delete account_type with dependent account → restricted

  account as parent:
    2. Delete account with dependent child account (parent_code) → restricted
    3. Delete account with dependent journal_entry_line → restricted
    4. Delete account with dependent obligation_agreement (source) → restricted
    5. Delete account with dependent obligation_agreement (dest) → restricted
    6. Delete account with dependent transfer (from) → restricted
    7. Delete account with dependent transfer (to) → restricted

  fiscal_period as parent:
    8. Delete fiscal_period with dependent journal_entry → restricted
    9. Delete fiscal_period with dependent invoice → restricted

  journal_entry as parent:
    10. Delete journal_entry with dependent reference → restricted
    11. Delete journal_entry with dependent line → restricted
    12. Delete journal_entry with dependent obligation_instance → restricted
    13. Delete journal_entry with dependent transfer → restricted

Ops schema (5 FKs):

  obligation_type as parent:
    14. Delete obligation_type with dependent agreement → restricted

  cadence as parent:
    15. Delete cadence with dependent agreement → restricted

  payment_method as parent:
    16. Delete payment_method with dependent agreement → restricted

  obligation_status as parent:
    17. Delete obligation_status with dependent instance → restricted

  obligation_agreement as parent:
    18. Delete obligation_agreement with dependent instance → restricted
```

Step definitions go in `DeleteRestrictionSteps.fs`. Each scenario inserts the full parent→child chain within a transaction, attempts the DELETE, asserts the FK violation, and rolls back. Some parent tables already have seed data with children (e.g., account_type → accounts from chart of accounts), but for consistency and isolation, all RESTRICT tests should insert their own test data within the transaction.

### Added: transfer.journal_entry_id nullable scenario

Added to `OpsStructuralConstraints.feature`:

```gherkin
Scenario: transfer journal_entry_id is nullable
  Given the ops schema exists
  When I insert a valid transfer with null journal_entry_id
  Then the insert succeeds
```

This brings OpsStructuralConstraints to 41 scenarios, fixing the inconsistency where every other nullable FK had a success test.

### Nullable column testing policy

Each feature file gets a header comment documenting scope:

```gherkin
# Scope: NOT NULL constraints (user-defined, not DEFAULT-backed), UNIQUE constraints,
# FK constraints (insert direction), and nullable FK/business-significant fields.
# Intentionally untested: freeform nullable text/date columns (memo, notes, source,
# description, etc.) and NOT NULL + DEFAULT columns (is_active, created_at, modified_at).
```

### Final scenario count: 88

| Feature file | Scenarios |
|---|---|
| LedgerStructuralConstraints.feature | 29 |
| OpsStructuralConstraints.feature | 41 (+1 transfer nullable) |
| DeleteRestrictionConstraints.feature | 18 (new) |
| **Total** | **88** |

## Acceptance Criteria

- [x]`dotnet test` from solution root runs all 88 Gherkin scenarios against `leobloom_dev` — all pass
- [x]`LeoBloom.Migrations` uses `Dal.ConnectionString.resolve` — no duplicated config loading
- [x]`LEOBLOOM_ENV` is the sole mechanism for environment selection across all projects
- [x]No test data persists in the database after a test run (transaction rollback)
- [x]Solution builds clean with zero warnings
- [x]`LeoBloom.Domain.Tests` builds and runs (placeholder test passes, unchanged)
- [x]Feature files remain in `Projects/Project001-Database/Specs/Acceptance/` (single source of truth, linked into test project)
- [x]All Then steps use try/finally for cleanup — no connection leaks on assertion failure
- [x]No `appsettings.Production.json` in Dal.Tests
- [x]Runtime database name guard prevents accidental prod execution

## Success Metrics

- 88/88 scenarios passing in `dotnet test`
- Zero rows inserted/left behind in `leobloom_dev` after test run
- Migrations project has zero direct `ConfigurationBuilder` usage

## Dependencies & Risks

**Dependencies:**
- Project 1 migrations applied to `leobloom_dev` (prerequisite — seed data must exist)
- `LEOBLOOM_ENV=Development` set in container
- `LEOBLOOM_DB_PASSWORD=claude` set in container
- .NET 10 SDK available at `/home/sandbox/.dotnet`

**Risks:**
- **TickSpec .NET 10 compatibility**: Mitigated — targets .NET Standard 2.0, confirmed compatible. Package updated January 2026.
- **Embedded resource naming**: Easy to get wrong, produces unhelpful errors. Verify with `GetManifestResourceNames()` early.
- **Seeded data assumptions**: If someone re-runs migrations from scratch or the seed data changes, UNIQUE violation tests could break. Low risk — seed data is stable.
- **Migrations invocation contract change**: Switching from `args[0]` to `LEOBLOOM_ENV`. Any scripts calling `dotnet run -- Production` will silently default to Development.
- **TickSpec context cache**: When steps must return the modified `ScenarioContext` record. A forgotten return silently breaks downstream assertions.

## Files Modified / Created

| File | Action | Notes |
|---|---|---|
| `Src/LeoBloom.Dal.Tests/LeoBloom.Dal.Tests.fsproj` | Modified | Add TickSpec + Npgsql packages, embedded resource links, appsettings copy, compile order |
| `Src/LeoBloom.Dal.Tests/SharedSteps.fs` | Created | Given/Then steps, cleanup helper, parallelism disable, prod guard |
| `Src/LeoBloom.Dal.Tests/LedgerStepDefinitions.fs` | Created | Ledger When step definitions |
| `Src/LeoBloom.Dal.Tests/OpsStepDefinitions.fs` | Created | Ops When step definitions |
| `Src/LeoBloom.Dal.Tests/DeleteRestrictionSteps.fs` | Created | ON DELETE RESTRICT When steps (18 scenarios) |
| `Src/LeoBloom.Dal.Tests/FeatureFixture.fs` | Created | xUnit Theory wiring (last in compile order) |
| `Src/LeoBloom.Dal.Tests/appsettings.Development.json` | Created | Connection string with `Search Path=ledger,ops,public` |
| `Src/LeoBloom.Dal.Tests/Tests.fs` | Deleted | Placeholder replaced by real tests |
| `Projects/Project001-Database/Specs/Acceptance/OpsStructuralConstraints.feature` | Modified | Add transfer.journal_entry_id nullable scenario |
| `Projects/Project001-Database/Specs/Acceptance/DeleteRestrictionConstraints.feature` | Created | 18 ON DELETE RESTRICT scenarios |
| `Projects/Project001-Database/Specs/Acceptance/LedgerStructuralConstraints.feature` | Modified | Add scope comment header |
| `Src/LeoBloom.Migrations/Program.fs` | Modified | Use `Dal.ConnectionString.resolve`, preserve migrondi schema bootstrap |
| `Src/LeoBloom.Migrations/LeoBloom.Migrations.fsproj` | Modified | Add ProjectReference to Dal, remove config packages |
| `Src/LeoBloom.Dal/ConnectionString.fs` | Modified (optional) | Add search path comment, consider fail-fast on missing password |

## Sources & References

- BRD: `Projects/Project002-TestHarness/Project002-brd.md`
- Existing resolver: `Src/LeoBloom.Dal/ConnectionString.fs`
- [TickSpec GitHub](https://github.com/fsprojects/TickSpec)
- [TickSpec NuGet 2.0.4](https://www.nuget.org/packages/TickSpec)
- [TickSpec.Xunit NuGet 2.0.4](https://www.nuget.org/packages/TickSpec.xUnit)
- [TickSpec xUnit example](https://github.com/fsprojects/TickSpec/tree/master/Examples/ByFramework/xUnit/FSharp.xUnit)
- [Npgsql docs (Context7)](https://www.npgsql.org/doc/)
- PostgreSQL SQLSTATE codes: `23502` (NOT NULL), `23503` (FK), `23505` (UNIQUE)
