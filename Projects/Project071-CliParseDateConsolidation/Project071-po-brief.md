# P071 — CLI parseDate Consolidation: PO Brief

**Card ID:** 19
**Branch:** feat/project71-cli-parsedate-consolidation
**Status:** Kickoff complete, ready for planning

## Summary

Straightforward DRY refactor. The `parseDate` function is copy-pasted across
7 CLI command modules and `parsePeriodArg` is duplicated in two. Additionally,
`TransferCommands.fs` uses lenient `DateOnly.TryParse` while claiming strict
format — that's a bug masquerading as inconsistency.

## Scope

- Extract shared `parseDate` to a new CLI helpers module
- Fix `TransferCommands.fs` to use `TryParseExact("yyyy-MM-dd")`
- Extract shared `parsePeriodArg` to same helpers module
- Remove all private copies from individual command modules
- All existing tests must pass unchanged (AC-4)

## Files Involved

parseDate found in:
- `Src/LeoBloom.CLI/AccountCommands.fs`
- `Src/LeoBloom.CLI/InvoiceCommands.fs`
- `Src/LeoBloom.CLI/LedgerCommands.fs`
- `Src/LeoBloom.CLI/ObligationCommands.fs`
- `Src/LeoBloom.CLI/PeriodCommands.fs`
- `Src/LeoBloom.CLI/PortfolioCommands.fs`
- `Src/LeoBloom.CLI/ReportCommands.fs`
- `Src/LeoBloom.CLI/TransferCommands.fs`

parsePeriodArg found in:
- `Src/LeoBloom.CLI/PeriodCommands.fs`
- `Src/LeoBloom.CLI/ReportCommands.fs`

## Acceptance Criteria

- AC-1: One `parseDate` definition, used by all CLI command modules
- AC-2: All date parsing uses `TryParseExact("yyyy-MM-dd")`
- AC-3: `parsePeriodArg` is defined once and shared
- AC-4: All existing tests pass with zero changes

## Notes for Planner

This is a pure mechanical refactor. The only tricky bit is ensuring F# module
compilation order in the .fsproj file — the new helpers module must appear
before all command modules that reference it. The TransferCommands fix (AC-2)
is the only behavioral change; everything else is structural.
