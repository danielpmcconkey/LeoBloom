# Project 061 — Portfolio Allocation Reporting: Plan

## Objective

Add four portfolio analysis CLI commands (`allocation`, `portfolio-summary`,
`portfolio-history`, `gains`) under `leobloom report`. These replace the
PersonalFinance HTML charts with tabular CLI output, using the investment
portfolio schema from P057–P059.

## Architecture Decision

**New PortfolioReportCommands.fs** — ReportCommands.fs is already 286 lines with
10 subcommands (AC-S1 explicitly permits a new file). The new file follows the
exact same Argu pattern as ReportCommands.fs but is wired into the existing
`ReportArgs` DU, so the CLI surface stays under `leobloom report`.

**Service + Repository in LeoBloom.Portfolio** — The portfolio report queries are
all joins within the `portfolio` schema. Adding them to `LeoBloom.Reporting`
would require a new cross-project dependency. Keeping them in `LeoBloom.Portfolio`
matches the existing data ownership pattern.

## Phases

### Phase 1: Domain Types for Report Results

**What:** Add report result types to `LeoBloom.Domain.Portfolio`.

**Files:**
- `Src/LeoBloom.Domain/Portfolio.fs` — append new types

**New types:**
```fsharp
/// A single row in an allocation breakdown.
type AllocationRow =
    { category: string
      currentValue: decimal
      percentage: decimal }

/// Full allocation report result.
type AllocationReport =
    { dimension: string
      rows: AllocationRow list
      total: decimal }

/// Portfolio summary report.
type PortfolioSummary =
    { totalValue: decimal
      totalCostBasis: decimal
      unrealizedGainLoss: decimal
      unrealizedGainLossPct: decimal
      taxBucketBreakdown: AllocationRow list
      topHoldings: AllocationRow list }

/// A single row in the history time-series.
type HistoryRow =
    { positionDate: DateOnly
      categories: (string * decimal) list
      total: decimal }

/// Full history report result.
type PortfolioHistoryReport =
    { dimension: string
      rows: HistoryRow list }

/// Per-fund gain/loss row.
type GainLossRow =
    { symbol: string
      fundName: string
      costBasis: decimal
      currentValue: decimal
      gainLoss: decimal
      gainLossPct: decimal }

/// Full gains report.
type GainsReport =
    { rows: GainLossRow list
      totalCostBasis: decimal
      totalCurrentValue: decimal
      totalGainLoss: decimal
      totalGainLossPct: decimal }
```

**Verification:** Project compiles.

### Phase 2: Repository — Allocation Queries

**What:** New `PortfolioReportRepository.fs` in `LeoBloom.Portfolio` with SQL
queries that join position data with dimension tables.

**Files:**
- `Src/LeoBloom.Portfolio/PortfolioReportRepository.fs` — new file
- `Src/LeoBloom.Portfolio/LeoBloom.Portfolio.fsproj` — add to compile list

**Key queries:**

1. **Allocation query** — Uses `DISTINCT ON (investment_account_id, symbol)` to
   get latest positions, then JOINs to the appropriate dimension table based on
   the dimension parameter. The 10 dimensions map to different JOIN paths:
   - `tax-bucket` → `position → investment_account → tax_bucket`
   - `account-group` → `position → investment_account → account_group`
   - `account` → `position → investment_account` (group by account name)
   - `investment-type` → `position → fund → dim_investment_type`
   - `market-cap` → `position → fund → dim_market_cap`
   - `index-type` → `position → fund → dim_index_type`
   - `sector` → `position → fund → dim_sector`
   - `region` → `position → fund → dim_region`
   - `objective` → `position → fund → dim_objective`
   - `symbol` → `position → fund` (group by symbol/fund name)

   Each query returns `(category_name, SUM(current_value))` rows. Percentage
   calculation happens in the service layer (simple arithmetic, no SQL needed).

2. **Latest positions with fund+account data** — For summary and gains. Extends
   the `DISTINCT ON` pattern from `PositionRepository.latestAll` to also return
   fund name and account details via JOINs.

3. **History query** — `listByFilter`-style query but grouped by dimension and
   date. Returns `(position_date, category_name, SUM(current_value))` tuples.
   Supports optional date range filtering.

**Design:** One function per concern (`getAllocation`, `getSummaryData`,
`getHistory`, `getGains`). The dimension parameter is a string that maps to
the appropriate SQL JOIN clause. Build SQL dynamically based on dimension —
same pattern as `listByFilter` with conditional WHERE clauses.

**Verification:** Project compiles. (DB integration tested via CLI in Phase 5.)

### Phase 3: Service Layer

**What:** New `PortfolioReportService.fs` in `LeoBloom.Portfolio` that
orchestrates repository calls and transforms raw data into report types.

**Files:**
- `Src/LeoBloom.Portfolio/PortfolioReportService.fs` — new file
- `Src/LeoBloom.Portfolio/LeoBloom.Portfolio.fsproj` — add to compile list

**Functions:**
- `getAllocation (dimension: string) : Result<AllocationReport, string list>` —
  Validates dimension string, calls repo, calculates percentages, sorts by value
  descending. Default dimension: `account-group`.
- `getPortfolioSummary () : Result<PortfolioSummary, string list>` — Gets latest
  positions, aggregates total value/cost basis/gain, builds tax bucket breakdown
  and top-5 holdings.
- `getPortfolioHistory (dimension: string) (startDate: DateOnly option) (endDate: DateOnly option) : Result<PortfolioHistoryReport, string list>` —
  Calls repo with date filters, pivots into time-series rows. Default dimension:
  `tax-bucket`.
- `getGains (accountId: int option) : Result<GainsReport, string list>` — Gets
  latest positions (optionally filtered), computes per-fund gain/loss, sorts by
  gain descending.

**Pattern:** Follows existing service pattern exactly — `use conn`, `use txn`,
try/catch with rollback, `Result<T, string list>`.

**Verification:** Project compiles.

### Phase 4: CLI Commands + Output Formatting

**What:** Argu DU definitions, handlers, formatters, and wiring into `Program.fs`.

**Files:**
- `Src/LeoBloom.CLI/PortfolioReportCommands.fs` — new file (Argu args + dispatch)
- `Src/LeoBloom.CLI/OutputFormatter.fs` — add formatters for new report types
- `Src/LeoBloom.CLI/ReportCommands.fs` — add 4 new cases to `ReportArgs` DU,
  delegate to `PortfolioReportCommands.dispatch*`
- `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` — add `PortfolioReportCommands.fs` to
  compile list (before `ReportCommands.fs` since Report depends on it)
- `Src/LeoBloom.CLI/Program.fs` — update Report usage description string

**CLI surface:**
```
leobloom report allocation [--by <dimension>] [--json]
leobloom report portfolio-summary [--json]
leobloom report portfolio-history [--by <dimension>] [--from <date>] [--to <date>] [--json]
leobloom report gains [--account <id>] [--json]
```

**Argu structure in PortfolioReportCommands.fs:**
- `AllocationArgs` — `By` (string, optional), `Json`
- `PortfolioSummaryArgs` — `Json`
- `PortfolioHistoryArgs` — `By` (string, optional), `From` (string, optional),
  `To` (string, optional), `Json`
- `GainsArgs` — `Account` (int, optional), `Json`

**Wiring approach:** Add 4 new cases to `ReportArgs` in `ReportCommands.fs`:
```fsharp
| [<CliPrefix(CliPrefix.None)>] Allocation of ParseResults<PortfolioReportCommands.AllocationArgs>
| [<CliPrefix(CliPrefix.None)>] Portfolio_Summary of ParseResults<PortfolioReportCommands.PortfolioSummaryArgs>
| [<CliPrefix(CliPrefix.None)>] Portfolio_History of ParseResults<PortfolioReportCommands.PortfolioHistoryArgs>
| [<CliPrefix(CliPrefix.None)>] Gains of ParseResults<PortfolioReportCommands.GainsArgs>
```

And in the `dispatch` function, delegate to handlers in `PortfolioReportCommands`.

**Formatters in OutputFormatter.fs:**
- `formatAllocationReport` — table matching the backlog mockup (category, value,
  percentage columns with separator lines and total row)
- `formatPortfolioSummary` — key-value pairs for totals, then tax bucket table
  and top-5 table
- `formatPortfolioHistory` — date column + one column per category
- `formatGainsReport` — symbol, cost basis, value, gain/loss $, gain/loss %
- Dedicated `write*` functions for each (same pattern as `writePositionList` etc.)

**Empty portfolio handling (AC-B11):** Each handler checks for empty data and
outputs an informative message like "(no positions found)" with exit code 0.

**Verification:** Project compiles. `leobloom report --help` shows the new
subcommands.

### Phase 5: Tests

**What:** Integration tests exercising all four commands via CLI runner.

**Files:**
- `Src/LeoBloom.Tests/PortfolioReportCommandsTests.fs` — new file
- `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` — add to compile list

**Test strategy:** Uses existing `PortfolioCliEnv` pattern from
`PortfolioCommandsTests.fs` — sets up test data (tax bucket, account group,
accounts, funds with dimensions, positions at multiple dates), then runs CLI
commands and asserts on stdout content and exit codes.

**Test cases:**
- AC-B1: `report allocation` (default dimension) — verify output contains account
  group names, values, percentages
- AC-B2: `report allocation --by sector` — verify sector grouping
- AC-B3: All 10 `--by` values produce exit code 0
- AC-B4: Percentages sum to 100% (parse from output, assert within tolerance)
- AC-B5: `report portfolio-summary` — verify total value, cost basis, gain/loss,
  tax bucket breakdown, top 5
- AC-B6: `report portfolio-history` — verify date rows present
- AC-B7: `report portfolio-history --from X --to Y` — verify date range filtering
- AC-B8: `report gains` — verify per-fund gain/loss output
- AC-B9: `report gains --account N` — verify account filtering
- AC-B10: `--json` on all four commands produces valid JSON
- AC-B11: Empty portfolio — all commands exit 0 with informative message
- AC-S3: Existing test suite still passes (verified by running full test suite)

**Verification:** All new tests pass. All pre-existing tests pass.

## Acceptance Criteria

- [ ] AC-B1: `report allocation` shows allocation by account group with values and percentages
- [ ] AC-B2: `report allocation --by sector` shows allocation grouped by sector
- [ ] AC-B3: Each of the 10 `--by` values produces output without error
- [ ] AC-B4: Allocation percentages sum to 100.0% (within rounding tolerance)
- [ ] AC-B5: `report portfolio-summary` shows total value, cost basis, unrealized gain, tax bucket breakdown, top 5 holdings
- [ ] AC-B6: `report portfolio-history` shows time-series data with one row per position date
- [ ] AC-B7: `--from` and `--to` restrict the date range of history output
- [ ] AC-B8: `report gains` shows per-fund unrealized gain/loss with dollar and percentage values
- [ ] AC-B9: `report gains --account <id>` shows only positions for that account
- [ ] AC-B10: All commands support `--json` flag producing valid JSON
- [ ] AC-B11: All commands handle empty portfolio gracefully (informative message, exit 0)
- [ ] AC-S1: Portfolio report commands in new `PortfolioReportCommands.fs`, wired into existing `ReportArgs`
- [ ] AC-S2: Allocation and summary reports use latest-position-per-fund-per-account logic
- [ ] AC-S3: All pre-existing tests pass without modification

## Risks

- **Dimension SQL complexity:** The 10-dimension allocation query needs dynamic
  SQL with different JOIN paths. Mitigate by building a dimension→(join clause,
  group column) lookup table and constructing SQL from it. This keeps the logic
  in one function rather than 10 separate queries.
- **History query performance:** Grouping all positions by date and dimension
  could return large result sets. Not a real risk at Dan's data scale, but the
  query should use date range filters when provided.
- **Percentage rounding:** Summing individually rounded percentages may not hit
  exactly 100.0%. The test should allow ±0.1% tolerance, and the formatter can
  adjust the largest category's percentage to force the sum to 100.0%.

## Out of Scope

- HTML or chart rendering (Leo is CLI-only)
- Monte Carlo projections
- Benchmark comparison
- Real-time price fetching
- New database migrations (all tables exist from P057)
