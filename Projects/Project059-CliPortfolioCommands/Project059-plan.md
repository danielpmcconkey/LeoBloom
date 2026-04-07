# Project 059 — CLI Portfolio Commands: Plan

## Objective

Wire the P058 service layer (InvestmentAccountService, FundService, PositionService) into the CLI
as a `portfolio` command group with nested subcommands: `account`, `fund`, and `position`.
Follow the established Argu DU + dispatch pattern. Support `--json` output throughout.

## Key Design Decision: Nested Command Groups

This is the first nested command group in the CLI (`portfolio account list` vs flat `account list`).
Argu handles this naturally — the top-level `PortfolioArgs` DU contains sub-DUs for Account, Fund,
and Position, each of which contains their own sub-DUs for individual commands. This is the same
pattern used at the top level (e.g., `LedgerArgs` containing `Post`, `Void`, `Show`) just one level
deeper. No framework changes needed.

## Phases

### Phase 1: OutputFormatter — Portfolio Type Formatters

**What:** Add human-readable formatters and dedicated write functions for portfolio types.

**Files modified:**
- `Src/LeoBloom.CLI/OutputFormatter.fs` — add `open LeoBloom.Domain.Portfolio`, then:
  - `formatInvestmentAccount` (detail view)
  - `formatInvestmentAccountList` (table: ID, Name, TaxBucketId, AccountGroupId)
  - `formatFund` (detail view)
  - `formatFundList` (table: Symbol, Name, InvestmentTypeId, MarketCapId)
  - `formatPosition` (detail view)
  - `formatPositionList` (table: ID, AccountId, Symbol, Date, Price, Qty, Value, CostBasis)
  - `writeInvestmentAccountList`, `writeFundList`, `writePositionList` (dedicated write fns, same pattern as `writeAccountList` etc. — needed to avoid F# type erasure on generic lists)

**Verification:** Project compiles. Formatters are callable from command handlers.

### Phase 2: PortfolioCommands Module

**What:** Create `PortfolioCommands.fs` with the full Argu DU hierarchy and dispatch logic.

**File created:**
- `Src/LeoBloom.CLI/PortfolioCommands.fs`

**Argu DU structure:**
```
PortfolioAccountCreateArgs  (--name, --tax-bucket-id, --account-group-id, --json)
PortfolioAccountListArgs    (--json)
PortfolioAccountArgs        = Create | List

PortfolioFundCreateArgs     (--symbol, --name, --investment-type-id?, --market-cap-id?, --index-type-id?, --sector-id?, --region-id?, --objective-id?, --json)
PortfolioFundListArgs       (--investment-type-id?, --market-cap-id?, --index-type-id?, --sector-id?, --region-id?, --objective-id?, --json)
PortfolioFundShowArgs       (<symbol>, --json)
PortfolioFundArgs           = Create | List | Show

PortfolioPositionRecordArgs (--account-id, --symbol, --date, --price, --quantity, --value, --cost-basis, --json)
PortfolioPositionListArgs   (--account-id?, --start-date?, --end-date?, --json)
PortfolioPositionLatestArgs (--account-id?, --json)
PortfolioPositionArgs       = Record | List | Latest

PortfolioArgs               = Account | Fund | Position
```

**Handlers:** Each handler calls the corresponding service method, pipes results through OutputFormatter.
- `handleAccountCreate` → `InvestmentAccountService.createAccount`
- `handleAccountList` → `InvestmentAccountService.listAccounts`
- `handleFundCreate` → `FundService.createFund` (constructs Fund record from args, uses 0 for id-less create)
- `handleFundList` → `FundService.listFunds` or `listFundsByDimension` (based on which filter flag is present)
- `handleFundShow` → `FundService.findFundBySymbol` (error if not found)
- `handlePositionRecord` → `PositionService.recordPosition`
- `handlePositionList` → `PositionService.listPositions` (constructs PositionFilter from args)
- `handlePositionLatest` → `PositionService.latestPositionsByAccount` or `latestPositionsAll`

**Dispatch:** Nested dispatch — `dispatch` matches Account/Fund/Position, each delegates to sub-dispatch.

**Verification:** Project compiles. `leobloom portfolio --help` shows subcommands.

### Phase 3: Program.fs Registration + Project Reference

**What:** Register the portfolio command group in the top-level CLI.

**Files modified:**
- `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj`:
  - Add `<Compile Include="PortfolioCommands.fs" />` before `Program.fs`
  - Add `<ProjectReference Include="..\LeoBloom.Portfolio\LeoBloom.Portfolio.fsproj" />`
- `Src/LeoBloom.CLI/Program.fs`:
  - Add `open LeoBloom.CLI.PortfolioCommands`
  - Add `| [<CliPrefix(CliPrefix.None)>] Portfolio of ParseResults<PortfolioArgs>` to `LeoBloomArgs`
  - Add usage string: `"Portfolio commands (account, fund, position)"`
  - Add match arm: `| Some (Portfolio portfolioResults) -> PortfolioCommands.dispatch isJson portfolioResults`

**Verification:** `leobloom portfolio account list` runs without error. `leobloom --help` shows `portfolio`.

### Phase 4: CLI Integration Tests

**What:** Integration tests exercising the full CLI → service → DB round-trip.

**File created:**
- `Src/LeoBloom.Tests/PortfolioCommandsTests.fs`

**Test cases:**
- `portfolio account create` — creates account, verify stdout contains account name
- `portfolio account create` with blank name — verify exit code + error message
- `portfolio account list` — verify table output
- `portfolio account list --json` — verify JSON array
- `portfolio fund create` — creates fund, verify stdout
- `portfolio fund create` duplicate symbol — verify error
- `portfolio fund list` — verify table output
- `portfolio fund show <symbol>` — verify detail output
- `portfolio fund show` nonexistent — verify error
- `portfolio position record` — records position, verify stdout
- `portfolio position record` negative price — verify validation error
- `portfolio position record` nonexistent symbol — verify error
- `portfolio position list` — verify table output
- `portfolio position latest` — verify output
- `portfolio position latest --account-id N` — filtered output

**Setup:** Uses `PortfolioInsertHelpers` from `PortfolioTestHelpers.fs` for test data.
Tests follow the `CliRunner.run` pattern from existing CLI tests.

**Files modified:**
- `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` — add `<Compile Include="PortfolioCommandsTests.fs" />`

**Verification:** All tests pass. `dotnet test --filter PortfolioCommandsTests`.

## Acceptance Criteria

- [ ] `leobloom portfolio account create --name X --tax-bucket-id N --account-group-id N` creates an investment account and prints it
- [ ] `leobloom portfolio account list` lists all investment accounts in tabular format
- [ ] `leobloom portfolio fund create --symbol X --name Y` creates a fund and prints it
- [ ] `leobloom portfolio fund list` lists all funds; dimension filters work when provided
- [ ] `leobloom portfolio fund show <symbol>` prints fund detail or error if not found
- [ ] `leobloom portfolio position record --account-id N --symbol X --date D --price P --quantity Q --value V --cost-basis C` records a position
- [ ] `leobloom portfolio position list` lists positions; `--account-id`, `--start-date`, `--end-date` filters work
- [ ] `leobloom portfolio position latest` shows latest positions; `--account-id` filter works
- [ ] `--json` flag produces JSON output for all commands (both at top level and subcommand level)
- [ ] Validation errors (blank name, negative price, nonexistent symbol) return non-zero exit code + error on stderr
- [ ] Integration tests pass for all subcommands (happy path + key error paths)
- [ ] Project compiles with no warnings

## Risks

- **Fund create needs a Fund record with all optional dimension IDs.** The CLI must map absent flags to `None`. Straightforward but easy to mistype — tests cover this.
- **FundDimensionFilter is a single-case DU.** Only one dimension filter at a time. The CLI should error if multiple are provided, or pick the first. Plan: error if >1 filter flag is given.

## Out of Scope

- No dimension lookup commands (list tax buckets, account groups, etc.) — that's a future project.
- No position update/delete — service layer doesn't support it yet.
- No CSV/TSV export — `--json` is the only non-table format.
