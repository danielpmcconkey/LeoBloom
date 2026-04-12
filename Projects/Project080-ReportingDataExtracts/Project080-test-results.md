# Project 080 — Reporting Data Extracts — Test Results

**Date:** 2026-04-12
**Commit:** 6b37845ae29a3c1f6431ba9b39af35ab4cceaee0
**Result:** 12/12 verified (7 behavioral + 5 structural)

## Acceptance Criteria Verification

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| B1 | `extract account-tree` returns JSON with id, code, name, parent_id, account_type, normal_balance, subtype, is_active | Yes | ExtractTypes.fs:11-19 defines AccountTreeRow with all fields + `[<JsonPropertyName>]`. ExtractRepository.fs:14-35 returns them. |
| B2 | `extract balances --as-of` excludes voided entries via INNER JOIN + WHERE | Yes | ExtractRepository.fs:44-48 uses `JOIN` + `WHERE je.voided_at IS NULL`. Grep confirms 0 LEFT JOIN occurrences in file. |
| B3 | `extract balances` returns raw debit-minus-credit (no normal balance adjustment) | Yes | ExtractRepository.fs:42-43 uses `SUM(debit) - SUM(credit)`. No normalBalance reference in balance computation. |
| B4 | `extract positions --as-of` returns latest per (account, symbol) | Yes | ExtractRepository.fs:74-81 subquery with `DISTINCT ON (investment_account_id, symbol)` + `position_date DESC`. |
| B5 | `extract positions` excludes current_value = 0 | Yes | ExtractRepository.fs:85 `WHERE latest.current_value <> 0` on outer query (post DISTINCT ON). Builder's deviation from plan SQL is correct — filters after latest-snapshot selection. |
| B6 | `extract je-lines --fiscal-period-id` returns non-voided lines in period | Yes | ExtractRepository.fs:113-118 INNER JOIN + `WHERE je.voided_at IS NULL AND fp.id = @fiscal_period_id`. |
| B7 | `extract je-lines` orders by account_code ASC, entry_date ASC, journal_entry_id ASC | Yes | ExtractRepository.fs:119 `ORDER BY a.code, je.entry_date, je.id`. |
| S1 | ExtractTypes.fs defines four record types with correct fields | Yes | AccountTreeRow, AccountBalanceRow, PortfolioPositionRow, JournalEntryLineRow all present with `[<CLIMutable>]` and `[<JsonPropertyName>]`. |
| S2 | ExtractRepository.fs uses INNER JOIN + WHERE (never LEFT JOIN) | Yes | Grep confirms 0 LEFT JOIN matches. All 4 queries use `JOIN` (INNER). |
| S3 | ExtractCommands.fs registered in Program.fs dispatch | Yes | Program.fs:30 `Extract of ParseResults<ExtractArgs>`, lines 78-79 dispatch to `ExtractCommands.dispatch`. |
| S4 | All four commands produce valid JSON to stdout | Yes | CLI tests parse output with `JsonDocument.Parse()` — FT-EXT-010, 020, 030, 040. |
| S5 | Commands use DataSource.openConnection() (env-controlled) | Yes | 4 call sites in ExtractCommands.fs (lines 62, 80, 101, 114). No raw NpgsqlConnection construction. |

## Gherkin Coverage

### Behavioral — Specs/Behavioral/DataExtracts.feature

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-EXT-100 | Account tree returns all accounts with required fields | Yes (ExtractRepositoryTests.fs, 2 tests) | Yes |
| @FT-EXT-101 | Account tree includes active and inactive accounts | Yes | Yes |
| @FT-EXT-102 | Account tree ordered by code | Yes | Yes |
| @FT-EXT-110 | Balances respects as-of date cutoff | Yes | Yes |
| @FT-EXT-111 | Balances includes entries on as-of date itself | Yes | Yes |
| @FT-EXT-112 | Balances excludes voided entries | Yes | Yes |
| @FT-EXT-113 | Fully-voided account absent from balances | Yes | Yes |
| @FT-EXT-114 | Balance is raw debit-minus-credit | Yes | Yes |
| @FT-EXT-115 | Net-zero balance account omitted | Yes | Yes |
| @FT-EXT-120 | Positions returns latest snapshot per pair | Yes | Yes |
| @FT-EXT-121 | Positions as-of excludes post-cutoff snapshots | Yes | Yes |
| @FT-EXT-122 | Positions one row per distinct pair across accounts | Yes | Yes |
| @FT-EXT-123 | Zero current_value excluded | Yes | Yes |
| @FT-EXT-124 | Latest-snapshot zero excludes even with earlier value | Yes | Yes |
| @FT-EXT-130 | JE lines scoped to requested fiscal period | Yes | Yes |
| @FT-EXT-131 | Empty period returns empty list | Yes | Yes |
| @FT-EXT-132 | JE lines excludes voided entries | Yes | Yes |
| @FT-EXT-133 | JE lines ordered by code/date/id | Yes | Yes |

### CLI — Specs/CLI/ExtractCommands.feature

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-EXT-001 | Top-level --help includes extract | Yes | Yes |
| @FT-EXT-002 | Extract --help lists 4 subcommands | Yes | Yes |
| @FT-EXT-003 | Extract no subcommand → stderr + exit 2 | Yes | Yes |
| @FT-EXT-010 | Account tree no flags → valid JSON | Yes | Yes |
| @FT-EXT-011 | Account tree --json → valid JSON | Yes | Yes |
| @FT-EXT-012 | Account tree --help | Yes | Yes |
| @FT-EXT-020 | Balances valid --as-of → valid JSON | Yes | Yes |
| @FT-EXT-021 | Balances --json → valid JSON | Yes | Yes |
| @FT-EXT-022 | Balances missing --as-of → exit 2 | Yes | Yes |
| @FT-EXT-023 | Balances invalid date → exit 1 | Yes | Yes |
| @FT-EXT-024 | Balances --help | Yes | Yes |
| @FT-EXT-030 | Positions valid --as-of → valid JSON | Yes | Yes |
| @FT-EXT-031 | Positions no --as-of defaults today | Yes | Yes |
| @FT-EXT-032 | Positions --json → valid JSON | Yes | Yes |
| @FT-EXT-033 | Positions invalid date → exit 1 | Yes | Yes |
| @FT-EXT-034 | Positions --help | Yes | Yes |
| @FT-EXT-040 | JE lines valid ID → valid JSON | Yes | Yes |
| @FT-EXT-041 | JE lines --json → valid JSON | Yes | Yes |
| @FT-EXT-042 | JE lines missing ID → exit 2 | Yes | Yes |
| @FT-EXT-043 | JE lines non-numeric ID → exit 1/2 | Yes | Yes |
| @FT-EXT-044 | JE lines --help | Yes | Yes |
| @FT-EXT-050 | Account tree snake_case fields | Yes | Yes |
| @FT-EXT-051 | Balances snake_case fields | Yes | Yes |
| @FT-EXT-052 | Positions snake_case fields | Yes | Yes |
| @FT-EXT-053 | JE lines snake_case fields | Yes | Yes |

## QE Artifact Verification

- QE reports 1121/1121 tests pass, 0 failed, 0 skipped
- Commit hash in QE artifact: 6b37845ae29a3c1f6431ba9b39af35ab4cceaee0 — matches current HEAD
- Reviewer independently confirmed 1121/1121

## Fabrication Check

- All cited files exist and contain the claimed code patterns
- No circular evidence — every criterion verified against actual source files
- No stale evidence — commit hash matches HEAD
- No omitted failures — QE and Reviewer both report 1121/1121
- Builder's getPositions SQL deviation documented and confirmed correct by Governor (subquery filters after DISTINCT ON, not before)

## Verdict

**APPROVED** — Every acceptance criterion verified. Evidence chain is solid. Implementation is correct and complete.
