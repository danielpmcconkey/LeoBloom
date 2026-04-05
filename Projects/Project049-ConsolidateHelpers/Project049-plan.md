# Project 049 -- Consolidate Helper Duplication: Plan

## Objective

Eliminate duplicated helper functions identified by the code audit (SYNTHESIS.md,
Tier 2 Finding #7). Three groups of duplication get consolidated into shared
locations. This is pure mechanical refactoring -- no behavior changes, no new
features. The codebase should compile identically and all tests should pass
unchanged.

## Research Findings

### optParam -- 4 copies, 2 signatures

| File | Signature | Notes |
|---|---|---|
| `JournalEntryRepository.fs:11` | `string -> string option -> NpgsqlCommand -> unit` | Narrow: only handles `string option` |
| `ObligationAgreementRepository.fs:16` | `string -> obj option -> NpgsqlCommand -> unit` | General: handles `obj option` |
| `ObligationInstanceRepository.fs:12` | `string -> obj option -> NpgsqlCommand -> unit` | Identical to OA version |
| `TransferRepository.fs:11` | `string -> obj option -> NpgsqlCommand -> unit` | Identical to OA version |

The `obj option` signature is strictly more general and subsumes the
`string option` version (a `string` upcasts to `obj`). The consolidated
version must use the `obj option` signature. Call sites in
`JournalEntryRepository.fs` already pass `string option` values, which the
compiler will auto-upcast -- but the Builder should verify this compiles
cleanly since the original was typed narrower.

### repoRoot -- 2 copies (not 4)

The backlog item says "4 identical copies" but the codebase only has 2:

| File | Line |
|---|---|
| `LogModuleStructureTests.fs:11` | `let private repoRoot = ...walkUp...` |
| `LoggingInfrastructureTests.fs:17` | `let private repoRoot = ...walkUp...` |

Both use the same fragile `walkUp` pattern from `AppContext.BaseDirectory`.
Both also define `let private srcDir = Path.Combine(repoRoot, "Src")`.

The `[<CallerFilePath>]` rewrite: a function in `TestHelpers.fs` that derives
repo root from the caller's source file path (which the compiler injects at
build time). This is stable regardless of output directory structure.

### Constraint test helpers -- duplicated between 2 files

**LedgerConstraintTests.fs** (lines 9-26) defines:
- `tryExec` -- parameterized: takes `(conn, sql, paramSetup)`
- `tryInsert` -- convenience: `tryExec conn sql ignore`
- `assertSqlState` -- parameterized: takes `(expected, ex, failMsg)`
- `assertNotNull` -- partial application of `assertSqlState "23502"`
- `assertUnique` -- partial application of `assertSqlState "23505"`
- `assertFk` -- partial application of `assertSqlState "23503"`

**OpsConstraintTests.fs** (lines 8-36) defines:
- `tryExec` -- identical implementation
- `tryInsert` -- identical implementation
- `assertNotNull` -- hardcoded failMsg, **different signature** (no failMsg param)
- `assertFk` -- hardcoded failMsg, different signature
- `assertUnique` -- hardcoded failMsg, different signature
- `assertSuccess` -- unique to Ops, not in Ledger

The Ledger version is better: parameterized `assertSqlState` is more
composable. The Ops file's call sites never pass a custom failure message
(they use the assert helpers directly), so adopting the Ledger signature
means adding a failMsg argument at each Ops call site.

**Decision:** Keep the Ledger version's `assertSqlState` as the shared
foundation. Re-derive `assertNotNull`, `assertUnique`, `assertFk` via
partial application (matching Ledger). Add `assertSuccess` (from Ops).
Ops call sites already pass their assertions without a failMsg -- the
partially-applied versions from Ledger's pattern embed a default message,
so Ops call sites remain unchanged.

Wait -- that's not quite right. Let me look at the call patterns:

- **Ledger calls:** `assertNotNull ex "Expected NOT NULL violation"` (3 args)
- **Ops calls:** `assertNotNull ex` (2 args -- the message is baked in)

The Ledger `assertNotNull` is `assertSqlState "23502"`, which is
`PostgresException option -> string -> unit` (2 remaining args). The Ops
version takes only `PostgresException option -> unit`.

To consolidate without changing call sites in both files, the shared helpers
should expose BOTH forms:
- `assertSqlState` (3-arg: sqlState, ex, failMsg) -- for Ledger-style calls
- `assertNotNull`, `assertUnique`, `assertFk` as 1-arg versions with baked-in
  messages -- for Ops-style calls. These are just `assertSqlState "23502" ex "Expected NOT NULL violation"` etc.

Actually, simplest approach: make the shared versions match the Ledger
signature (partial application of `assertSqlState`), then update OPS call
sites to pass the failure message. The Ops tests have ~20 call sites total.
This is consistent and avoids maintaining two calling conventions.

**Final decision:** Shared helpers use Ledger's pattern. Ops call sites get
updated to pass a failure message string. This is a few more characters per
call but removes the "two conventions" problem permanently.

## Phases

### Phase 1: Create DataHelpers.fs in LeoBloom.Utilities

**What:**
- Create `Src/LeoBloom.Utilities/DataHelpers.fs` with the `obj option`
  version of `optParam`
- Add `DataHelpers.fs` to `LeoBloom.Utilities.fsproj` (after `DataSource.fs`)
- The function should be public, in a module `DataHelpers` under namespace
  `LeoBloom.Utilities`

**Files:**
- CREATE: `Src/LeoBloom.Utilities/DataHelpers.fs`
- MODIFY: `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj`

**Verification:** `dotnet build Src/LeoBloom.Utilities/`

### Phase 2: Replace optParam in repository files

**What:**
- Remove the `let private optParam` definition from all 4 repository files
- Add `open LeoBloom.Utilities` (if not already present) to each file
- Replace `optParam` calls with `DataHelpers.optParam`
- For `JournalEntryRepository.fs`: the original typed `value` as
  `string option` -- the call sites pass `cmd.source` (which is
  `string option`). Switching to `obj option` means the call sites need
  `Option.map (fun v -> v :> obj)` wrapping, OR the existing call sites
  already work because F# auto-upcasts `string` to `obj` in the `Some`
  branch. The Builder must verify compilation.

**Files modified:**
- `Src/LeoBloom.Ledger/JournalEntryRepository.fs` -- remove optParam def, update calls
- `Src/LeoBloom.Ops/ObligationAgreementRepository.fs` -- remove optParam def, update calls
- `Src/LeoBloom.Ops/ObligationInstanceRepository.fs` -- remove optParam def, update calls
- `Src/LeoBloom.Ops/TransferRepository.fs` -- remove optParam def, update calls

**Verification:** `dotnet build Src/LeoBloom.Ledger/ && dotnet build Src/LeoBloom.Ops/`

### Phase 3: Add constraint test helpers and repoRoot to TestHelpers.fs

**What:**
- Add a `ConstraintAssert` submodule to `TestHelpers.fs` containing:
  - `tryExec`
  - `tryInsert`
  - `assertSqlState`
  - `assertNotNull` (partial application: `assertSqlState "23502"`)
  - `assertUnique` (partial application: `assertSqlState "23505"`)
  - `assertFk` (partial application: `assertSqlState "23503"`)
  - `assertSuccess` (from OpsConstraintTests)
- Add a `RepoPath` submodule to `TestHelpers.fs` containing:
  - `repoRoot` using `[<CallerFilePath>]` -- a function that derives repo
    root from the caller's file path by walking up to the directory
    containing `LeoBloom.sln`
  - `srcDir` convenience function

**The `[<CallerFilePath>]` approach:**
```fsharp
open System.Runtime.CompilerServices

module RepoPath =
    let repoRoot ([<CallerFilePath>] ?callerPath: string) : string =
        let callerFile = defaultArg callerPath ""
        let rec walkUp (dir: string) =
            if File.Exists(Path.Combine(dir, "LeoBloom.sln")) then dir
            else
                let parent = Directory.GetParent(dir)
                if parent = null then failwith "Could not find repo root from CallerFilePath"
                walkUp parent.FullName
        walkUp (Path.GetDirectoryName(callerFile))

    let srcDir ([<CallerFilePath>] ?callerPath: string) : string =
        Path.Combine(repoRoot(?callerPath = callerPath), "Src")
```

This is stable: it walks up from the *source file location* (known at compile
time), not from the runtime assembly output directory. It works regardless of
build configuration, output path, or `dotnet test` working directory.

**Files modified:**
- `Src/LeoBloom.Tests/TestHelpers.fs` -- add ConstraintAssert and RepoPath modules

**Verification:** `dotnet build Src/LeoBloom.Tests/`

### Phase 4: Update consumers of duplicated helpers

**What:**
- **LedgerConstraintTests.fs:** Remove local `tryExec`, `tryInsert`,
  `assertSqlState`, `assertNotNull`, `assertUnique`, `assertFk` definitions.
  Add `open LeoBloom.Tests.TestHelpers` (already present). Prefix calls with
  `ConstraintAssert.` (e.g., `ConstraintAssert.tryExec`,
  `ConstraintAssert.assertNotNull`).
- **OpsConstraintTests.fs:** Remove local `tryExec`, `tryInsert`,
  `assertNotNull`, `assertFk`, `assertUnique`, `assertSuccess` definitions.
  Prefix calls with `ConstraintAssert.`. Update call sites to pass a failure
  message string (matching Ledger's signature).
- **LogModuleStructureTests.fs:** Remove local `repoRoot` and `srcDir`
  definitions. Replace with `RepoPath.repoRoot()` and `RepoPath.srcDir()`
  calls. Add `open LeoBloom.Tests.TestHelpers` if not present (currently it
  does NOT open TestHelpers).
- **LoggingInfrastructureTests.fs:** Remove local `repoRoot` and `srcDir`
  definitions. Replace with `RepoPath.repoRoot()` and `RepoPath.srcDir()`
  calls. (Already opens TestHelpers.)

**Files modified:**
- `Src/LeoBloom.Tests/LedgerConstraintTests.fs`
- `Src/LeoBloom.Tests/OpsConstraintTests.fs`
- `Src/LeoBloom.Tests/LogModuleStructureTests.fs`
- `Src/LeoBloom.Tests/LoggingInfrastructureTests.fs`

**Verification:** `dotnet build Src/LeoBloom.Tests/ && dotnet test Src/LeoBloom.Tests/`

## Acceptance Criteria

- [ ] `optParam` exists exactly once in the entire `Src/` tree (in `DataHelpers.fs`)
- [ ] `DataHelpers.fs` is in `LeoBloom.Utilities` project, compiled after `DataSource.fs`
- [ ] No `let private optParam` definitions remain in any repository file
- [ ] All 4 repository files call `DataHelpers.optParam` instead of local copies
- [ ] `repoRoot` logic exists exactly once in `TestHelpers.fs` (in `RepoPath` module)
- [ ] `repoRoot` uses `[<CallerFilePath>]` attribute, not `AppContext.BaseDirectory` walkup
- [ ] No `let private repoRoot` definitions remain in `LogModuleStructureTests.fs` or `LoggingInfrastructureTests.fs`
- [ ] Constraint test helpers (`tryExec`, `tryInsert`, `assertSqlState`, `assertNotNull`, `assertUnique`, `assertFk`, `assertSuccess`) exist exactly once in `TestHelpers.fs`
- [ ] No duplicate constraint helper definitions remain in `LedgerConstraintTests.fs` or `OpsConstraintTests.fs`
- [ ] `dotnet build` succeeds with zero warnings for all projects
- [ ] `dotnet test Src/LeoBloom.Tests/` passes all existing tests (zero regressions)

## Risks

- **F# auto-upcast for `string option` to `obj option`:** The
  `JournalEntryRepository.fs` call sites pass `string option` values to
  `optParam`, but the consolidated version expects `obj option`. F# does NOT
  auto-upcast option types -- `Some "hello"` is `string option`, not
  `obj option`. The Builder will need to add explicit `Option.map box` at
  JournalEntryRepository call sites (there are 2: `@source` and `@memo`).
  This is the one non-trivial part of the refactoring.

- **CallerFilePath in F#:** The `[<CallerFilePath>]` attribute works with
  optional parameters in F#. The compiler injects the source file path at
  the call site. This is well-supported in .NET and F# but the Builder
  should verify the exact syntax compiles. If it doesn't work with optional
  params in F#, fall back to a version that takes an explicit `__SOURCE_FILE__`
  or just hardcodes the path derivation from `__SOURCE_DIRECTORY__`.

- **Module qualification at call sites:** Moving helpers into submodules
  (`ConstraintAssert.tryExec` instead of just `tryExec`) changes every call
  site. This is mechanical but there are ~40+ call sites across the two
  constraint test files. Builder should do a find-and-replace, not manual
  edits.

## Out of Scope

- `buildSection` consolidation (3 copies in reporting services) -- waits for
  reporting refactor
- `lookupAccount` variant consolidation (4 services) -- subtly different
  queries, premature extraction adds coupling
- Any behavioral changes to the helpers themselves
- Adding new tests -- this is refactoring, existing tests validate correctness
