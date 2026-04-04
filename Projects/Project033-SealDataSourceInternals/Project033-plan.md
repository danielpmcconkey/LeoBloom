# Project 033 — Seal DataSource Internals

## Objective

Remove the public `connectionString` binding from `DataSource` and make Migrations
fully self-sufficient for its own DB access. Project 030 added `connectionString` as
a public escape hatch so Migrations could pass a raw connection string to Migrondi.
That violates the "one public function" design of DataSource. Migrations already has
its own `appsettings.{env}.json` with `search_path=migrondi` -- it just needs to
build its own connection string from that config instead of reaching into Dal.

## Current State

**DataSource** (`Src/LeoBloom.Dal/DataSource.fs`):
- `connStr` (private) -- builds connection string from appsettings + env var
- `dataSource` (private) -- NpgsqlDataSource with pooling, debug guard
- `connectionString` (public, line 60) -- exposes `connStr`; only consumer is Migrations
- `openConnection()` (public) -- the one intended public function

**Migrations** (`Src/LeoBloom.Migrations/Program.fs`):
- Line 23: `DataSource.openConnection()` -- opens a connection to create the `migrondi` schema
- Line 31: `DataSource.connectionString` -- passes raw conn string to `MigrondiConfig`
- Has its own `appsettings.Development.json` / `appsettings.Production.json` with
  `Search Path=migrondi` (distinct from Dal's `search_path=ledger,ops,public`)
- References `LeoBloom.Dal` via `ProjectReference`

**Key insight**: When Migrations calls `DataSource.openConnection()`, it gets a connection
using whatever appsettings Dal loaded (which may use a different search_path than migrondi).
This is already a latent bug or at minimum a design smell -- Migrations should be using its
own config for all DB access, not Dal's.

## Phases

### Phase 1: Make Migrations Self-Sufficient

**What:** Add a private connection-string builder to `Program.fs` that reads from
Migrations' own appsettings. Replace both `DataSource.openConnection()` and
`DataSource.connectionString` references. Add required NuGet packages to Migrations.

**Files modified:**

1. **`Src/LeoBloom.Migrations/LeoBloom.Migrations.fsproj`**
   - Add PackageReferences:
     - `Npgsql` (same version as Dal: `9.0.3`)
     - `Microsoft.Extensions.Configuration` (`10.0.0`)
     - `Microsoft.Extensions.Configuration.EnvironmentVariables` (`10.0.0`)
     - `Microsoft.Extensions.Configuration.Json` (`10.0.0`)
   - Remove ProjectReference to `LeoBloom.Dal`

2. **`Src/LeoBloom.Migrations/Program.fs`**
   - Remove `open LeoBloom.Dal`
   - Add `open Microsoft.Extensions.Configuration` and `open Npgsql`
   - Add a private `buildConnectionString()` function (same pattern as
     `DataSource.connStr` -- read env, load appsettings, substitute password).
     This is intentional duplication; Migrations' DB access is a private
     implementation detail that should not share code with Dal.
   - Replace line 23 (`DataSource.openConnection()`) with: open a new
     `NpgsqlConnection` using the locally-built connection string
   - Replace line 31 (`DataSource.connectionString`) with the local
     connection string binding

**Concrete Program.fs structure:**

```fsharp
open System
open System.IO
open Microsoft.Extensions.Configuration
open Migrondi.Core
open Npgsql

/// Migrations builds its own connection string from its own config.
/// This is intentionally not shared with DataSource -- Migrations' DB
/// access is a private implementation detail.
let private connectionString =
    let env =
        Environment.GetEnvironmentVariable "LEOBLOOM_ENV"
        |> Option.ofObj
        |> Option.defaultValue "Development"

    let config =
        ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile($"appsettings.{env}.json", optional = false)
            .AddEnvironmentVariables()
            .Build()

    let template = config["ConnectionStrings:LeoBloom"]

    if String.IsNullOrWhiteSpace template then
        failwith $"ConnectionStrings:LeoBloom not found in appsettings.{env}.json"

    let password =
        Environment.GetEnvironmentVariable "LEOBLOOM_DB_PASSWORD"
        |> Option.ofObj
        |> Option.defaultValue ""

    template.Replace("{LEOBLOOM_DB_PASSWORD}", password)

[<EntryPoint>]
let main _args =
    // env read again for the printfn (cheap, clear)
    let env =
        Environment.GetEnvironmentVariable "LEOBLOOM_ENV"
        |> Option.ofObj
        |> Option.defaultValue "Development"

    printfn "Leo Bloom Migrations — Environment: %s" env

    let migrationsDir =
        Path.Combine(AppContext.BaseDirectory, "Migrations")

    if not (Directory.Exists migrationsDir) then
        Directory.CreateDirectory migrationsDir |> ignore

    use bootstrapConn = new NpgsqlConnection(connectionString)
    bootstrapConn.Open()
    use cmd = bootstrapConn.CreateCommand()
    cmd.CommandText <- "CREATE SCHEMA IF NOT EXISTS migrondi"
    cmd.ExecuteNonQuery() |> ignore
    bootstrapConn.Close()

    let migrondiConfig = {
        MigrondiConfig.Default with
            connection = connectionString
            driver = MigrondiDriver.Postgresql
            migrations = migrationsDir
            tableName = "__migrondi_migrations"
    }
    // ... rest unchanged
```

**Verification:**
- `dotnet build Src/LeoBloom.Migrations/` compiles with no Dal reference
- `dotnet run --project Src/LeoBloom.Migrations/` runs migrations successfully
  (requires `LEOBLOOM_DB_PASSWORD` and `LEOBLOOM_ENV` set)

### Phase 2: Seal DataSource

**What:** Remove the public `connectionString` binding from DataSource. Update the
doc comment to reflect the restored "one public function" invariant.

**Files modified:**

1. **`Src/LeoBloom.Dal/DataSource.fs`**
   - Delete lines 58-60 (the `connectionString` binding and its doc comment)
   - Update the module doc comment (line 8) to remove the word "One" if we want,
     or leave it -- it's already correct once we remove the escape hatch

**Verification:**
- `dotnet build Src/LeoBloom.sln` (or equivalent) -- full solution compiles
- Grep for `DataSource.connectionString` returns zero results
- `DataSource` module exposes exactly one public binding: `openConnection`

### Phase 3: Full-Solution Smoke Test

**What:** Build entire solution, run all tests.

**Verification:**
- `dotnet build` from solution root -- clean build, no warnings related to this change
- `dotnet test` -- all existing tests pass (tests only use `DataSource.openConnection()`,
  which is untouched)

## Acceptance Criteria

- [ ] `DataSource.connectionString` binding does not exist in `DataSource.fs`
- [ ] No code outside `LeoBloom.Migrations` references `DataSource.connectionString`
- [ ] `LeoBloom.Migrations.fsproj` has no `ProjectReference` to `LeoBloom.Dal`
- [ ] `LeoBloom.Migrations/Program.fs` builds its own connection string from its own appsettings
- [ ] `dotnet build` succeeds for the full solution
- [ ] `dotnet test` passes all existing tests
- [ ] Migrations runs successfully against `leobloom_dev`

## Risks

- **Env variable duplication**: The `LEOBLOOM_ENV` / `LEOBLOOM_DB_PASSWORD` pattern
  is now duplicated between DataSource and Migrations. This is intentional per the
  design constraint (Migrations' DB access is a private implementation detail), but
  if the env var names ever change, both need updating. Acceptable tradeoff given the
  project is small and the alternative is a shared config module that re-couples them.

- **Migrations appsettings already exist and are correct**: The `search_path=migrondi`
  config is already in place. No new config files needed. If those files were missing,
  this would be a problem -- but they're already there and deployed.

## Out of Scope

- Refactoring the env-var / config pattern into a shared utility. The whole point is
  that Migrations owns its own config independently.
- Changing Migrations' appsettings content (already correct with `search_path=migrondi`).
- Touching any other consumer of `DataSource.openConnection()` (tests, Dal services, etc.).
