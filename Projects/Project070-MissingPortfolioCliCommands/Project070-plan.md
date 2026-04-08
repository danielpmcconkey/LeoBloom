# Project 070 — Missing Portfolio CLI Commands — Plan

## Objective

Implement four missing CLI commands from the P059 spec that were never built:
`portfolio account show`, account list filters (`--group`, `--tax-bucket`),
and `portfolio dimensions`. Each gets Gherkin scenarios, test implementations,
CLI arg parsing, service/repository support, and OutputFormatter coverage.

## Architecture Summary

The existing portfolio CLI follows a clean layered pattern:
- **Argu DUs** (PortfolioCommands.fs) → parse CLI args
- **Handler functions** (same file) → open conn/txn, call service, format output
- **Service layer** (LeoBloom.Portfolio) → validation + orchestration
- **Repository layer** (LeoBloom.Portfolio) → raw SQL
- **OutputFormatter.fs** → human-readable + JSON formatting
- **Tests** (PortfolioCommandsTests.fs) → integration tests using CliFrameworkTests helpers

All new code follows these exact patterns. No new files needed except a
DimensionRepository.fs in the Portfolio project.

## Phases

### Phase 1: Service & Repository Layer

**What:** Add the data access and service functions that the CLI handlers will call.

**Files modified:**
- `Src/LeoBloom.Portfolio/InvestmentAccountRepository.fs` — add `findById` already exists; add `listByGroup`, `listByTaxBucket` (filter by name via JOIN)
- `Src/LeoBloom.Portfolio/InvestmentAccountService.fs` — add `getAccount` (by ID, returns `InvestmentAccount option`), `listAccountsByGroup`, `listAccountsByTaxBucket`
- `Src/LeoBloom.Portfolio/DimensionRepository.fs` — **NEW** — 8 functions, one per dimension table, each returns `{id; name} list`. Simple `SELECT id, name FROM portfolio.X ORDER BY id`.
- `Src/LeoBloom.Portfolio/DimensionService.fs` — **NEW** — `listAllDimensions` returning a record/map of all 8 tables' contents.
- `Src/LeoBloom.Portfolio/LeoBloom.Portfolio.fsproj` — add `DimensionRepository.fs` and `DimensionService.fs` to compile order (before existing services).

**Notes:**
- The `--group <name>` and `--tax-bucket <name>` filters use **names** not IDs (per backlog). Repository queries JOIN `investment_account` with `account_group`/`tax_bucket` and filter by `name = @name`.
- `account show <id>` needs the account record plus latest positions. Compose `InvestmentAccountRepository.findById` + `PositionService.latestPositionsByAccount`.
- The backlog says "all 6 dimension tables" but the schema has **8**: tax_bucket, account_group, dim_investment_type, dim_market_cap, dim_index_type, dim_sector, dim_region, dim_objective. We list all 8.

**Verification:** `dotnet build` passes for LeoBloom.Portfolio project.

### Phase 2: CLI Layer (Argu DUs + Handlers)

**What:** Wire up Argu argument types and handler functions for all 4 commands.

**Files modified:**
- `Src/LeoBloom.CLI/PortfolioCommands.fs`:
  - Add `Show` case to `PortfolioAccountArgs` DU with `PortfolioAccountShowArgs` (MainCommand `Id` of int, `Json` flag)
  - Add `Group` and `Tax_Bucket` filter args to `PortfolioAccountListArgs`
  - Add `Dimensions` case to top-level `PortfolioArgs` DU with `PortfolioDimensionsArgs` (`Json` flag)
  - Add `handleAccountShow` handler: lookup account by ID, get latest positions, format composite output
  - Update `handleAccountList` to apply group/tax-bucket filters when provided
  - Add `handleDimensions` handler: call `DimensionService.listAllDimensions`, format output
  - Update `dispatchAccount` to handle `Show` case
  - Update `dispatch` to handle `Dimensions` case

**Verification:** `dotnet build` passes for LeoBloom.CLI project.

### Phase 3: OutputFormatter

**What:** Add human-readable and JSON formatting for the new outputs.

**Files modified:**
- `Src/LeoBloom.CLI/OutputFormatter.fs`:
  - Add `formatAccountDetail` — account info + position summary table (reuses existing `formatInvestmentAccount` header + `formatPositionList` body)
  - Add `writeAccountDetail` — isJson dispatcher for account show
  - Add `formatDimensionTable` — table name + id/name rows
  - Add `formatAllDimensions` — iterates all 8 tables
  - Add `writeDimensions` — isJson dispatcher for dimensions command

**Verification:** `dotnet build` passes for LeoBloom.CLI project.

### Phase 4: Gherkin Scenarios

**What:** Add feature scenarios for all 4 missing commands.

**Files created:**
- `Specs/CLI/PortfolioMissingCommands.feature` — separate feature file to avoid bloating the existing 461-line feature. Covers:
  - `portfolio account show <id>` — happy path, with positions, `--json`, nonexistent ID error
  - `portfolio account list --group <name>` — happy path, no matches, `--json`, invalid group name
  - `portfolio account list --tax-bucket <name>` — happy path, no matches, `--json`, invalid bucket name
  - `portfolio dimensions` — happy path with populated tables, `--json`, empty tables
  - Scenario tags follow existing pattern: `@FT-PFC-1xx` range

**Verification:** Feature file parses (no syntax errors in Gherkin).

### Phase 5: Test Implementations

**What:** Integration tests matching the Gherkin scenarios.

**Files modified:**
- `Src/LeoBloom.Tests/PortfolioCommandsTests.fs` — add test functions for all new commands. Follow existing `PortfolioCliEnv` pattern:
  - `account show` tests: create account + positions, verify show output, verify JSON, verify nonexistent ID error
  - `account list --group` tests: create accounts in different groups, verify filter, verify no-match, verify JSON
  - `account list --tax-bucket` tests: same pattern with tax buckets
  - `portfolio dimensions` tests: verify dimensions listed, verify JSON, verify empty tables behavior

**Files modified (helpers):**
- `Src/LeoBloom.Tests/PortfolioTestHelpers.fs` — add dimension insert helpers if needed (insertDimInvestmentType, etc.)

**Verification:** `dotnet test --filter PortfolioCommandsTests` passes.

## Domain Types

A new domain type is needed for the dimensions command output:

```fsharp
type DimensionTable = { tableName: string; values: (int * string) list }
type AllDimensions  = { tables: DimensionTable list }
```

Add to `Src/LeoBloom.Domain/Portfolio.fs` in Phase 1.

## Acceptance Criteria

- [ ] AC-1: `portfolio account show <id>` displays account detail with latest position summary, returns exit 0
- [ ] AC-2: `portfolio account list --group <name>` filters accounts by group name, returns exit 0
- [ ] AC-3: `portfolio account list --tax-bucket <name>` filters accounts by tax bucket name, returns exit 0
- [ ] AC-4: `portfolio dimensions` lists all 8 dimension tables with their id/name values, returns exit 0
- [ ] AC-5: All four commands support `--json` flag producing valid JSON output
- [ ] AC-6: Error cases tested: nonexistent account ID (show), invalid group/tax-bucket names (filters return empty, not error), empty dimension tables (dimensions shows empty)

## Risks

- **Backlog says "6 dimension tables" but schema has 8.** Mitigation: list all 8 — the backlog count was approximate. This is strictly more complete.
- **`--group <name>` / `--tax-bucket <name>` use names not IDs.** This is intentional per backlog but differs from other filter patterns (fund list uses IDs). Names that don't match any row should return an empty list, not an error — consistent with how fund list handles non-matching dimension IDs.
- **`account show` composites account + positions.** This is a new pattern (no other command combines two service calls). Keep it simple: two sequential calls in the handler, not a new composite service method.
- **F# compile order.** New files (DimensionRepository.fs, DimensionService.fs) must be added to fsproj in correct order: DimensionRepository before DimensionService, both before existing services that might reference them.

## Out of Scope

- Modifying existing P059 commands or tests
- Adding pagination to any list command
- Adding sorting options to account list filters
- Creating a dedicated `DimensionCommands.fs` — dimensions lives under portfolio since these are portfolio dimension tables
