# Project 031 — Foundational Logging Infrastructure: Plan

## Objective

Add structured logging to LeoBloom using Serilog with a thin F# wrapper module,
and rename `LeoBloom.Dal` to `LeoBloom.Utilities` to give the logging module a
proper home alongside the existing data access code. The rename is a mechanical
refactor with high blast radius; logging is new functionality built on top of it.

## Current State (Research Summary)

### What lives in LeoBloom.Dal today

| File | Purpose |
|------|---------|
| `DataSource.fs` | NpgsqlDataSource singleton, `openConnection()` |
| `JournalEntryRepository.fs` | Raw SQL persistence for journal entries |
| `JournalEntryService.fs` | Orchestration: validation + persistence |
| `AccountBalanceRepository.fs` | Read-only balance query |
| `AccountBalanceService.fs` | Orchestration for balance queries |

### Who references LeoBloom.Dal

**ProjectReference in .fsproj (2 projects):**
- `Src/LeoBloom.Api/LeoBloom.Api.fsproj`
- `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj`

**`open LeoBloom.Dal` in source files (7 files):**
- `Src/LeoBloom.Tests/OpsConstraintTests.fs`
- `Src/LeoBloom.Tests/DeleteRestrictionTests.fs`
- `Src/LeoBloom.Tests/DataSourceEncapsulationTests.fs`
- `Src/LeoBloom.Tests/PostJournalEntryTests.fs`
- `Src/LeoBloom.Tests/AccountBalanceTests.fs`
- `Src/LeoBloom.Tests/LedgerConstraintTests.fs`
- `Src/LeoBloom.Tests/VoidJournalEntryTests.fs`

**Solution file:**
- `LeoBloom.sln` line 14 — project entry for `LeoBloom.Dal` (GUID `{F4340D64-7DED-478A-83EA-1D9F936CF78E}`)

**Api references Dal in fsproj but never `open`s it** — the reference is needed
because Api transitively depends on DataSource initialization, but no source
file explicitly imports the namespace.

### printfn/eprintfn usage (non-Migrations)

- `Src/LeoBloom.Tests/TestHelpers.fs` lines 73, 85 — `eprintfn` in cleanup error handlers

Per Dan's decision: Migrations stays cowboy (`printfn`). TestHelpers `eprintfn`
calls should become `Log.Warning` or `Log.Error` calls once logging is available.

### Empty/dead projects in Src/

`LeoBloom.Data`, `LeoBloom.Ledger`, `LeoBloom.Ops`, `LeoBloom.Ledger.Tests`,
`LeoBloom.Ops.Tests` — all empty directories with only `obj/bin` artifacts.
No `.fsproj`, no source files. Not in the solution. Irrelevant to this project.

## Phases

### Phase 1: Rename LeoBloom.Dal to LeoBloom.Utilities

Mechanical refactor. High blast radius but zero behavioral change. Every file
touched, nothing should work differently.

**What changes:**

1. **Rename directory:** `Src/LeoBloom.Dal/` -> `Src/LeoBloom.Utilities/`

2. **Rename fsproj:** `LeoBloom.Dal.fsproj` -> `LeoBloom.Utilities.fsproj`

3. **Update namespace in all Dal source files** (5 files):
   - `DataSource.fs` — `namespace LeoBloom.Dal` -> `namespace LeoBloom.Utilities`
   - `JournalEntryRepository.fs` — same
   - `JournalEntryService.fs` — same
   - `AccountBalanceRepository.fs` — same
   - `AccountBalanceService.fs` — same

4. **Update `open` statements in test files** (7 files):
   - All `open LeoBloom.Dal` -> `open LeoBloom.Utilities`

5. **Update ProjectReference in consuming .fsproj files** (2 files):
   - `Src/LeoBloom.Api/LeoBloom.Api.fsproj` — path changes to `..\LeoBloom.Utilities\LeoBloom.Utilities.fsproj`
   - `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` — same

6. **Update solution file** `LeoBloom.sln`:
   - Line 14: project name `"LeoBloom.Dal"` -> `"LeoBloom.Utilities"`
   - Line 14: path `"Src\LeoBloom.Dal\LeoBloom.Dal.fsproj"` -> `"Src\LeoBloom.Utilities\LeoBloom.Utilities.fsproj"`
   - GUID stays the same (`{F4340D64-7DED-478A-83EA-1D9F936CF78E}`)

**Verification:**
- `dotnet build` succeeds with zero errors
- `dotnet test` passes (all existing tests)
- No remaining references to `LeoBloom.Dal` anywhere in the solution (grep confirms)

### Phase 2: Add Serilog Logging Module

New functionality. Create a thin F# module wrapping Serilog's static `Log` class.

**NuGet packages to add to `LeoBloom.Utilities.fsproj`:**
- `Serilog` — core library
- `Serilog.Sinks.Console` — console output
- `Serilog.Sinks.File` — file output
- `Serilog.Settings.Configuration` — read config from appsettings

**New file: `Src/LeoBloom.Utilities/Log.fs`**

Must be compiled **before** `DataSource.fs` in the fsproj (F# compilation order
matters — DataSource and everything downstream can then use it).

```fsharp
namespace LeoBloom.Utilities

open System
open Serilog
open Microsoft.Extensions.Configuration

/// Thin wrapper over Serilog's static Log class.
/// Module-level functions callable from anywhere. No DI, no ILogger<T>.
/// Call Log.initialize() once at process startup before any logging.
module Log =

    /// Initialize Serilog from appsettings.{env}.json.
    /// Creates one log file per process execution.
    /// Must be called once at startup (Program.fs / test init).
    let initialize () =
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

        let timestamp = DateTime.Now.ToString("yyyyMMdd.HH.mm.ss")

        let baseDir =
            config["Logging:FileBasePath"]
            |> Option.ofObj
            |> Option.defaultValue "/workspace/application_logs/leobloom"

        let logPath = System.IO.Path.Combine(baseDir, $"leobloom-{timestamp}.log")

        Serilog.Log.Logger <-
            LoggerConfiguration()
                .ReadFrom.Configuration(config)
                .WriteTo.Console()
                .WriteTo.File(logPath)
                .CreateLogger()

        Serilog.Log.Information("LeoBloom logging initialized. File: {LogPath}", logPath)

    /// Flush and close. Call at process shutdown.
    let closeAndFlush () =
        Serilog.Log.CloseAndFlush()

    // --- Thin wrappers (skip Debug entirely per design decision) ---

    let info (messageTemplate: string) ([<ParamArray>] args: obj array) =
        Serilog.Log.Information(messageTemplate, args)

    let warn (messageTemplate: string) ([<ParamArray>] args: obj array) =
        Serilog.Log.Warning(messageTemplate, args)

    let error (messageTemplate: string) ([<ParamArray>] args: obj array) =
        Serilog.Log.Error(messageTemplate, args)

    let fatal (messageTemplate: string) ([<ParamArray>] args: obj array) =
        Serilog.Log.Fatal(messageTemplate, args)

    let errorExn (ex: exn) (messageTemplate: string) ([<ParamArray>] args: obj array) =
        Serilog.Log.Error(ex, messageTemplate, args)
```

**Design notes on the wrapper:**
- `ParamArray` gives call sites the same structured-logging template syntax
  Serilog uses natively: `Log.info "Posted journal entry {Id}" [| entryId |]`
- No `debug` function — skip Debug level entirely per design decision
- `errorExn` variant captures the exception object for stack traces in the log
- `initialize()` reads the same `LEOBLOOM_ENV` + appsettings pattern that
  `DataSource` already uses — consistent, no new config mechanism
- Minimum log level is controlled by Serilog's `ReadFrom.Configuration()`,
  which reads from the `Serilog:MinimumLevel` key in appsettings

**Update fsproj compilation order:**

```xml
<Compile Include="Log.fs" />          <!-- NEW — first -->
<Compile Include="DataSource.fs" />
<Compile Include="JournalEntryRepository.fs" />
...
```

**Verification:**
- `dotnet build` succeeds
- New `Log` module is accessible from any project that references `LeoBloom.Utilities`

### Phase 3: Wire Up Configuration

Add Serilog config to appsettings files and call `Log.initialize()` at
entry points.

**appsettings.Development.json** (in Api, Tests, and Dal/Utilities output):

Add Serilog section:
```json
{
  "ConnectionStrings": { ... },
  "Serilog": {
    "MinimumLevel": "Information"
  },
  "Logging": {
    "FileBasePath": "/workspace/application_logs/leobloom"
  }
}
```

The `Serilog:MinimumLevel` key is what `ReadFrom.Configuration()` reads.
`Logging:FileBasePath` is our custom key for the file sink base directory.

**Entry points to wire up:**

1. **`Src/LeoBloom.Api/Program.fs`** — add `Log.initialize()` before
   `WebApplication.CreateBuilder()`, add `Log.closeAndFlush()` at shutdown.
   Add `open LeoBloom.Utilities`.

2. **`Src/LeoBloom.Tests/TestHelpers.fs`** — tricky. Tests don't have a single
   `Program.fs` entry point. Options:
   - Add a module-level `do` binding in TestHelpers.fs that calls
     `Log.initialize()`. Since TestHelpers.fs is the first compiled file in the
     test project, the static initializer runs before any test. This is the
     simplest approach and matches the "no DI" philosophy.
   - The test project already copies `appsettings.Development.json` to output.

**Note:** `LeoBloom.Migrations` is explicitly excluded. No logging changes there.

**Verification:**
- `dotnet build` succeeds
- `dotnet test` passes
- Running tests produces a log file in `/workspace/application_logs/leobloom/`
- Console output includes Serilog-formatted log lines

### Phase 4: Replace Existing printfn/eprintfn with Log Calls

Convert the two `eprintfn` calls in TestHelpers.fs to use the new logging module.

**`Src/LeoBloom.Tests/TestHelpers.fs`:**

Line 73:
```fsharp
// Before:
eprintfn "[TestCleanup] FAILED to clean %s: %s" table ex.Message
// After:
Log.errorExn ex "TestCleanup failed to clean {Table}" [| table :> obj |]
```

Line 85:
```fsharp
// Before:
eprintfn "[TestCleanup] FAILED to clean %s.%s: %s" table col ex.Message
// After:
Log.errorExn ex "TestCleanup failed to clean {Table}.{Column}" [| table :> obj; col :> obj |]
```

Add `open LeoBloom.Utilities` to TestHelpers.fs.

**Verification:**
- `dotnet test` passes
- Cleanup failures (if any) appear in structured log output, not raw stderr
- No remaining `eprintfn` calls outside of Migrations

### Phase 5: Add Foundational Log Statements to Service Layer

Add Info-level logging at major workflow steps in the service layer. This is
light — just enough to trace what happened when something goes wrong.

**`JournalEntryService.fs`:**
- `post`: Log.info at entry ("Posting journal entry...") and at success/failure
- `voidEntry`: Log.info at entry and at success/failure
- Error paths: Log.warn for validation failures, Log.error for persistence exceptions

**`AccountBalanceService.fs`:**
- `getBalanceById`: Log.info at entry
- `getBalanceByCode`: Log.info at entry
- Error paths: Log.error for query exceptions

**`DataSource.fs`:**
- Log.info after successful DataSource initialization ("DataSource initialized, connected to {Database}")
- Log.fatal if initialization fails (before the `failwith`)

Note: DataSource initializes eagerly at module load. `Log.initialize()` must be
called before DataSource is first accessed, which is guaranteed by compilation
order (Log.fs compiles before DataSource.fs) and the `do` binding in the
consuming entry point running before any DataSource access.

**Verification:**
- `dotnet test` passes
- Log output shows structured entries for test operations
- Intentionally failing a test produces visible error/warn log entries

## Acceptance Criteria

- [ ] `LeoBloom.Dal` directory, namespace, and all references no longer exist anywhere in the solution
- [ ] `LeoBloom.Utilities` project builds and contains all files that were in Dal
- [ ] `dotnet build` succeeds for the entire solution with zero warnings related to the rename
- [ ] `dotnet test` passes with zero failures (same pass count as before)
- [ ] Serilog packages (`Serilog`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`, `Serilog.Settings.Configuration`) are referenced in `LeoBloom.Utilities.fsproj`
- [ ] `Log.fs` exists in `LeoBloom.Utilities` with `initialize`, `closeAndFlush`, `info`, `warn`, `error`, `fatal`, `errorExn` functions
- [ ] No `debug` or `Debug`-level function exists in the Log module
- [ ] `Log.initialize()` is called at startup in Api `Program.fs` and test infrastructure
- [ ] Running tests creates a log file at `/workspace/application_logs/leobloom/leobloom-{timestamp}.log`
- [ ] Log filename follows format `leobloom-yyyyMMdd.HH.mm.ss.log`
- [ ] Minimum log level is configurable via `Serilog:MinimumLevel` in appsettings
- [ ] File sink base path is configurable via `Logging:FileBasePath` in appsettings
- [ ] `eprintfn` calls in TestHelpers.fs are replaced with `Log.errorExn` calls
- [ ] No `printfn`/`eprintfn` in any Src/ project except LeoBloom.Migrations
- [ ] Service layer functions (`post`, `voidEntry`, `getBalanceById`, `getBalanceByCode`) emit Info-level log entries
- [ ] LeoBloom.Migrations has zero changes (no new references, no logging)

## Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Rename breaks something subtle (casing, path separator) | Low | Phase 1 is isolated — build + test before proceeding. Git mv handles the rename cleanly. |
| F# compilation order issue (Log.fs must come before DataSource.fs) | Low | Explicitly place Log.fs first in the fsproj Compile list. Build will catch ordering errors immediately. |
| Log.initialize() not called before DataSource triggers module load | Medium | DataSource is lazy until first `openConnection()` call. In tests, the `do` binding in TestHelpers runs first. In Api, `Log.initialize()` is called before builder setup. Verify with a smoke test. |
| Serilog.Settings.Configuration version incompatibility with .NET 10 | Low | Serilog has been tracking .NET releases closely. If there's an issue, fall back to programmatic config only (remove `ReadFrom.Configuration`, hardcode minimum level). |
| Test parallelism + single log file | Low | Each test run gets its own file (timestamp-based). Parallel tests within a single run write to the same file, which Serilog handles via thread-safe sinks. |

## Out of Scope

- **LeoBloom.Migrations** — no changes, stays cowboy `printfn`
- **OpenTelemetry, enrichers, correlation IDs** — not needed yet
- **Per-domain-event logging** — future per-epic work
- **Centralized log aggregation** — no prod environment yet
- **Application metrics/telemetry** — separate concern
- **Log rotation, size limits, archival** — one run, one file, keep it simple
- **Cleaning up empty projects** (LeoBloom.Data, LeoBloom.Ledger, etc.) — separate chore, not this project
