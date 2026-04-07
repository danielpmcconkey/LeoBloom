# 061 — Portfolio Allocation Reporting

**Epic:** L — Investment Portfolio Module
**Depends On:** 058 (investment domain and repository)
**Status:** Not started
**Priority:** Medium

---

## Problem Statement

The PersonalFinance app generates an HTML report with 10 portfolio charts
showing allocation breakdowns across six classification dimensions, plus
time-series views by tax bucket and by symbol. Dan uses these charts to
understand concentration risk, diversification, and portfolio composition.

This project brings that analytical capability into LeoBloom as CLI report
commands, replacing the PersonalFinance HTML output. The data is the same;
the interface is Leo's CLI.

## What It Does

Adds portfolio analysis commands under `leobloom report`:

### Current allocation (pie chart equivalents)

```
leobloom report allocation [--by <dimension>] [--json]
```

Dimensions: `tax-bucket`, `account-group`, `account`, `investment-type`,
`market-cap`, `index-type`, `sector`, `region`, `objective`, `symbol`.

Default (no `--by`): shows allocation by account group.

Output: a table showing each category, its total current value, and its
percentage of total portfolio value. Sorted by value descending.

```
Allocation by Sector
────────────────────────────────────────────────────
  Technology          $432,156.78     38.2%
  Blend               $312,445.12     27.6%
  Finance              $89,234.56      7.9%
  ...
────────────────────────────────────────────────────
  Total             $1,131,234.56    100.0%
```

### Portfolio summary

```
leobloom report portfolio-summary [--json]
```

One-screen overview:
- Total portfolio value
- Total cost basis
- Total unrealized gain/loss (value and percentage)
- Breakdown by tax bucket (value and percentage each)
- Top 5 holdings by value

### Historical value (time-series, area chart equivalent)

```
leobloom report portfolio-history [--by <dimension>] [--from <date>] [--to <date>] [--json]
```

Shows portfolio value over time, grouped by the selected dimension. Each
row is a position date; columns are the category values. This is the
data that powered the stacked area charts in PersonalFinance.

Default dimension: `tax-bucket`. Default date range: all available history.

### Gain/loss by holding

```
leobloom report gains [--account <id>] [--json]
```

Per-fund unrealized gain/loss: symbol, cost basis, current value, gain/loss
(dollar and percentage). Sorted by gain descending. Optionally filtered by
account.

## Acceptance Criteria

### Behavioral (Gherkin scenarios)

| ID | Criterion | Description |
|----|-----------|-------------|
| AC-B1 | Allocation by default dimension | `report allocation` shows allocation by account group with values and percentages. |
| AC-B2 | Allocation by specific dimension | `report allocation --by sector` shows allocation grouped by sector. |
| AC-B3 | All 10 dimensions supported | Each of the 10 `--by` values produces output without error. |
| AC-B4 | Allocation percentages sum to 100 | The percentage column sums to 100.0% (within rounding tolerance). |
| AC-B5 | Portfolio summary | `report portfolio-summary` shows total value, cost basis, unrealized gain, tax bucket breakdown, top 5 holdings. |
| AC-B6 | Portfolio history | `report portfolio-history` shows time-series data with one row per position date. |
| AC-B7 | Portfolio history date filter | `--from` and `--to` restrict the date range of history output. |
| AC-B8 | Gains report | `report gains` shows per-fund unrealized gain/loss with dollar and percentage values. |
| AC-B9 | Gains filtered by account | `report gains --account <id>` shows only positions for that account. |
| AC-B10 | JSON output | All commands support `--json` flag producing valid JSON. |
| AC-B11 | Empty portfolio | All commands handle an empty portfolio gracefully (informative message, exit 0). |

### Structural

| ID | Criterion | Description |
|----|-----------|-------------|
| AC-S1 | New ReportCommands entries | Portfolio report commands added to existing ReportCommands.fs (or a new PortfolioReportCommands.fs if the file is getting large). |
| AC-S2 | Uses latest-position logic | Allocation and summary reports use the "latest position per fund per account" query from P058, not raw position rows. |
| AC-S3 | Existing tests still pass | All pre-existing tests pass without modification. |

## Scope Boundaries

### In scope

- CLI report commands for allocation, summary, history, and gains
- Human-readable tabular output and JSON
- Test coverage

### Explicitly out of scope

- **No HTML or chart rendering.** Leo is CLI-only. If Dan wants charts later,
  that's a UI project.
- **No Monte Carlo projections.** Future work.
- **No benchmark comparison** ("what if I'd put everything in the S&P 500").
  Noted in PersonalFinance's Program.cs as a todo. Future work.
- **No real-time price fetching.** Reports use the most recent snapshot in
  the database. Importing new snapshots is a separate concern.
