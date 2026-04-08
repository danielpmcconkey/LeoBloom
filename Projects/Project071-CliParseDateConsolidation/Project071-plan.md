# Project 071 — CLI parseDate Consolidation: Plan

## Objective

Extract the duplicated `parseDate` and `parsePeriodArg` functions from individual
CLI command modules into a single shared `CliHelpers.fs` module, fix the lenient
`TryParse` bug in TransferCommands, and remove all private copies. Pure mechanical
refactor with one behavioral fix.

## Phases

### Phase 1: Create shared helpers module

**What:** Create `Src/LeoBloom.CLI/CliHelpers.fs` containing:

```fsharp
module LeoBloom.CLI.CliHelpers

open System

let parseDate (raw: string) : Result<DateOnly, string> =
    match DateOnly.TryParseExact(raw, "yyyy-MM-dd") with
    | true, d -> Ok d
    | false, _ -> Error (sprintf "Invalid date format '%s' -- expected yyyy-MM-dd" raw)

let parsePeriodArg (raw: string) : Choice<int, string> =
    match Int32.TryParse(raw) with
    | true, id -> Choice1Of2 id
    | false, _ -> Choice2Of2 raw
```

**Files:**
- **Create:** `Src/LeoBloom.CLI/CliHelpers.fs`
- **Modify:** `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` — add `<Compile Include="CliHelpers.fs" />`
  as the **first** entry in the compile ItemGroup (before `ExitCodes.fs`), so it's
  available to all command modules.

**Verification:** Project compiles with the new file in place. No command modules
reference it yet — this is additive only.

### Phase 2: Replace all parseDate copies

**What:** In each of the 7 command modules that define `parseDate`:

1. Add `open LeoBloom.CLI.CliHelpers` at the top
2. Delete the `let private parseDate ...` definition
3. All existing call sites (`parseDate someArg`) continue to work because the
   shared function has the same signature

Files to modify (remove private `parseDate`, add `open`):
- `Src/LeoBloom.CLI/AccountCommands.fs` (line 64-67)
- `Src/LeoBloom.CLI/LedgerCommands.fs` (line 95-98)
- `Src/LeoBloom.CLI/ObligationCommands.fs` (line 220-223)
- `Src/LeoBloom.CLI/PeriodCommands.fs` (line 73-76)
- `Src/LeoBloom.CLI/PortfolioCommands.fs` (line 202-205)
- `Src/LeoBloom.CLI/ReportCommands.fs` (line 143-146)
- `Src/LeoBloom.CLI/TransferCommands.fs` (line 79-82) — **also fixes AC-2**: replaces
  the lenient `TryParse` with strict `TryParseExact` via the shared function. Note:
  TransferCommands calls it `parseDateOnly`, so call sites must be updated from
  `parseDateOnly` → `parseDate`.

**Verification:** `dotnet build` succeeds. All call sites resolve to the shared function.

### Phase 3: Replace parsePeriodArg copies

**What:** In each of the 2 command modules that define `parsePeriodArg`:

1. Ensure `open LeoBloom.CLI.CliHelpers` is present (may already be added in Phase 2)
2. Delete the `let private parsePeriodArg ...` definition

Files to modify:
- `Src/LeoBloom.CLI/PeriodCommands.fs` (line 68-71)
- `Src/LeoBloom.CLI/ReportCommands.fs` (line 233-236)

**Verification:** `dotnet build` succeeds.

### Phase 4: Full test suite

**What:** Run the complete test suite to confirm AC-4: all existing tests pass
with zero changes to test code.

**Verification:** `dotnet test` — all tests pass, no test files modified.

## Acceptance Criteria

- [ ] AC-1: One `parseDate` definition exists in `CliHelpers.fs`, used by all 7 CLI command modules
- [ ] AC-2: All date parsing uses `TryParseExact("yyyy-MM-dd")` — TransferCommands bug fixed
- [ ] AC-3: One `parsePeriodArg` definition exists in `CliHelpers.fs`, used by both PeriodCommands and ReportCommands
- [ ] AC-4: All existing tests pass with zero changes to test files

## Risks

- **F# compilation order:** The new `CliHelpers.fs` must be listed before all command
  modules in the .fsproj. If it's listed after any consumer, compilation fails immediately
  with a clear error — easy to catch, easy to fix.
- **TransferCommands behavioral change (AC-2):** Switching from lenient `TryParse` to
  strict `TryParseExact("yyyy-MM-dd")` means previously-accepted date formats like
  `4/1/2026` or `2026-4-1` will now be rejected. This is intentional — the error message
  already claims strict format, and consistency across all commands is the goal.

## Out of Scope

- `InvoiceCommands.fs` `parseDateTimeOffset` — different type (`DateTimeOffset`), different
  format (ISO 8601), different function. Not a duplicate.
- `PortfolioReportService.fs` `parseDate` — lives in `LeoBloom.Portfolio` project, not CLI.
  Different layer, may have different lifecycle needs.
- Any test changes — AC-4 explicitly requires zero test modifications.
