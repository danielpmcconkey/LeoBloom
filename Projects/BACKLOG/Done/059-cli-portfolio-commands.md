# 059 — CLI Portfolio Commands

**Epic:** L — Investment Portfolio Module
**Depends On:** 058 (investment domain and repository)
**Status:** Not started
**Priority:** High

---

## Problem Statement

The portfolio domain and repository exist (P058) but there's no way to
interact with them. This project adds CLI commands for managing investment
accounts, funds, and positions — the same consumption pattern as the existing
ledger/ops CLI commands.

## What It Does

Adds a `portfolio` command group to the LeoBloom CLI with subcommands for:

### Investment accounts

```
leobloom portfolio account list [--group <name>] [--tax-bucket <name>] [--json]
leobloom portfolio account show <id> [--json]
leobloom portfolio account create --name <name> --tax-bucket <name> --group <name>
```

### Funds

```
leobloom portfolio fund list [--sector <name>] [--region <name>] [--type <name>] [--cap <name>] [--json]
leobloom portfolio fund show <symbol> [--json]
leobloom portfolio fund create --symbol <sym> --name <name> [--type <t>] [--cap <c>] [--index-type <i>] [--sector <s>] [--region <r>] [--objective <o>]
```

### Positions

```
leobloom portfolio position record --account <id> --symbol <sym> --date <date> --price <p> --quantity <q> --value <v> --cost-basis <cb>
leobloom portfolio position list --account <id> [--from <date>] [--to <date>] [--json]
leobloom portfolio position latest [--account <id>] [--json]
```

### Dimensions (read-only reference)

```
leobloom portfolio dimensions [--json]
```

Lists all six dimension tables and their values. Useful for knowing what
values are valid when creating or classifying funds.

## Acceptance Criteria

### Behavioral (Gherkin scenarios)

| ID | Criterion | Description |
|----|-----------|-------------|
| AC-B1 | Account list | `portfolio account list` displays all investment accounts with group and tax bucket names. |
| AC-B2 | Account list with filter | `portfolio account list --group "Dan's IRAs"` shows only accounts in that group. |
| AC-B3 | Account show | `portfolio account show <id>` displays account detail including group, tax bucket, and latest position summary. |
| AC-B4 | Account create | `portfolio account create` with valid args creates an account and displays it. |
| AC-B5 | Fund list | `portfolio fund list` displays all funds with their classification dimensions. |
| AC-B6 | Fund list with filter | `portfolio fund list --sector Technology` shows only tech-sector funds. |
| AC-B7 | Fund show | `portfolio fund show FXAIX` displays fund detail including all six dimension values. |
| AC-B8 | Fund create | `portfolio fund create` with symbol and name creates a fund. |
| AC-B9 | Position record | `portfolio position record` with all required args creates a position snapshot. |
| AC-B10 | Position list | `portfolio position list --account <id>` displays positions for that account. |
| AC-B11 | Position latest | `portfolio position latest` displays the most recent position per fund per account — the current portfolio snapshot. |
| AC-B12 | Dimensions list | `portfolio dimensions` displays all six dimension tables and their values. |
| AC-B13 | JSON output | All list/show commands support `--json` flag producing valid JSON. |

### Structural

| ID | Criterion | Description |
|----|-----------|-------------|
| AC-S1 | New PortfolioCommands.fs | CLI module following the existing command pattern (Argu DUs, handlers, dispatch). |
| AC-S2 | Top-level routing | `Portfolio` case added to `LeoBloomArgs` DU in Program.fs. |
| AC-S3 | OutputFormatter extended | Portfolio formatting functions added to OutputFormatter.fs. |
| AC-S4 | Existing tests still pass | All pre-existing tests pass without modification. |

## Scope Boundaries

### In scope

- CLI commands for accounts, funds, positions, dimensions
- Human-readable and JSON output
- Test coverage

### Explicitly out of scope

- **No reporting or analytics.** P061 handles portfolio analysis.
- **No data import/migration.** P060 handles that.
- **No bulk position recording.** One position at a time via CLI. Bulk import
  is P060's concern.
