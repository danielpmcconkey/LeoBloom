# Project 040 — CLI Tax Reports — Plan

## Objective

Add four tax report commands (`schedule-e`, `general-ledger`,
`cash-receipts`, `cash-disbursements`) to the LeoBloom CLI under a new
`report` command group. The business logic lives in a new
`LeoBloom.Reporting` F# class library. The CLI layer is a thin
parse-call-format shell per ADR-003.

## COA-to-Schedule E Mapping Analysis

Verified against the seed data in
`Src/LeoBloom.Migrations/Migrations/1712000006000_SeedChartOfAccounts.sql`.

The property-related COA accounts map cleanly to IRS Schedule E lines:

| Schedule E Line | IRS Description | COA Account(s) |
|---|---|---|
| 3 | Rents received | `4110` Rental Income - Tenant A, `4120` Rental Income - Tenant B, `4130`/`4140`/`4150` Utility Reimbursements |
| 9 | Insurance | `5150` Homeowners Insurance - Rental |
| 12 | Mortgage interest | `5110` Mortgage Interest - Rental |
| 14 | Repairs | `5170` Repairs & Maintenance |
| 16 | Taxes | `5140` Property Tax - Rental |
| 18 | Depreciation | `5190` Depreciation (config-sourced annual amount) |
| 19 | Other (HOA) | `5160` HOA Dues |
| 19 | Other (Utilities) | `5120` Water & Electric, `5130` Gas |
| 19 | Other (Lawn) | `5200` Lawn Care |
| 19 | Other (Pest) | `5210` Pest Control |
| 19 | Other (Supplies) | `5180` Supplies |

The mapping is feasible. All property revenue lives under `4100` (parent
code) and all property expenses live under `5100` (parent code). The
"Other" line 19 will need sub-detail since it aggregates several accounts.

**Depreciation config:** The service reads depreciation from a config
structure (cost basis, in-service date, useful life = 27.5 years,
method = straight-line). For the initial implementation, this can live in
`appsettings.{env}.json` under a `Depreciation` section. The service
computes the annual amount from config rather than hardcoding it.

---

## Phases

### Phase 1: Domain Types and Reporting Project Scaffold

**What:** Create the `LeoBloom.Reporting` project with domain types for
all four reports. Add to solution. Define the COA-to-Schedule E mapping
as a pure data structure.

**Files created:**
- `Src/LeoBloom.Reporting/LeoBloom.Reporting.fsproj` — F# class library,
  refs Domain, Utilities, Ledger. Does NOT ref Ops (analysis shows none of
  the four reports need Ops types -- they're all ledger reads).
- `Src/LeoBloom.Reporting/ReportingTypes.fs` — domain types for all four
  reports (ScheduleEReport, ScheduleELineItem, GeneralLedgerReport,
  GeneralLedgerEntry, CashReceiptsReport, CashReceiptEntry,
  CashDisbursementsReport, CashDisbursementEntry)
- `Src/LeoBloom.Reporting/ScheduleEMapping.fs` — pure data: the
  account-code-to-Schedule-E-line mapping, depreciation config type

**Files modified:**
- `LeoBloom.sln` — add LeoBloom.Reporting project

**Verification:** Solution builds. New project compiles with no warnings.

**Note on Ops dependency:** The brief says Reporting refs Ops, but looking
at what these four reports actually need, they're all pure ledger reads.
Schedule E reads account balances. General ledger reads journal entry
lines. Cash receipts/disbursements read journal entry lines filtered by
asset accounts (cash). No obligation or transfer data is needed. The
Builder should NOT add an Ops reference unless a specific report requires
it. If future reports (P037 accounting reports) need Ops, that ref gets
added then.

### Phase 2: Report Repositories

**What:** SQL queries backing each report. Follow the existing pattern:
repositories take an `NpgsqlTransaction`, return typed results.

**Files created:**
- `Src/LeoBloom.Reporting/ScheduleERepository.fs` — queries account
  balances for property accounts within a calendar year date range
  (Jan 1 - Dec 31 of YYYY). Filters to non-voided entries. Groups by
  account, returns code + name + net balance.
- `Src/LeoBloom.Reporting/GeneralLedgerRepository.fs` — queries journal
  entry lines for a single account within a date range. Joins to
  journal_entry for date, description, voided status. Excludes voided.
  Returns date, entry ID, description, debit/credit amounts, running
  balance.
- `Src/LeoBloom.Reporting/CashFlowRepository.fs` — shared repository
  for cash receipts and disbursements. Queries journal entry lines where
  one side hits a cash/bank account (asset accounts under `1100` parent).
  Receipts = debits to cash accounts (money in). Disbursements = credits
  to cash accounts (money out). Joins to get counterparty account name,
  entry description, date.

**Files modified:**
- `Src/LeoBloom.Reporting/LeoBloom.Reporting.fsproj` — add Compile entries

**Verification:** Project compiles.

### Phase 3: Report Services

**What:** Service modules that orchestrate validation, repo calls, and
report assembly. Follow the existing service pattern: open own connection,
begin transaction, call repo, commit, return `Result<T, string list>`.

**Files created:**
- `Src/LeoBloom.Reporting/ScheduleEService.fs` — takes year (int),
  validates (year > 0, reasonable range), reads depreciation config from
  appsettings, queries balances via repo, maps to Schedule E lines using
  the mapping from Phase 1, computes depreciation from config, assembles
  report.
- `Src/LeoBloom.Reporting/GeneralLedgerReportService.fs` — takes account
  code (string), from/to dates. Validates account exists, date range is
  valid. Queries via repo, assembles report.
- `Src/LeoBloom.Reporting/CashFlowReportService.fs` — takes from/to
  dates. Validates date range. Queries via repo for receipts or
  disbursements, assembles report.

**Files modified:**
- `Src/LeoBloom.Reporting/LeoBloom.Reporting.fsproj` — add Compile entries
- `Src/LeoBloom.CLI/appsettings.Development.json` — add `Depreciation`
  config section (costBasis, inServiceDate, usefulLifeYears)
- `Src/LeoBloom.Tests/appsettings.Development.json` — same config

**Verification:** Project compiles. Services are callable from test code.

**Service return type convention:** All four services return
`Result<ReportType, string list>` matching the codebase convention
(JournalEntryService, IncomeStatementService, etc. all use this pattern).

### Phase 4: CLI Report Command Group

**What:** Create the `report` command group in the CLI with four
subcommands. Follow the LedgerCommands.fs pattern exactly.

**Files created:**
- `Src/LeoBloom.CLI/ReportCommands.fs` — Argu DU definitions for
  `ReportArgs` (with subcommands `Schedule_E`, `General_Ledger`,
  `Cash_Receipts`, `Cash_Disbursements`), each with their own args DU.
  Handler functions dispatch to Reporting services. No `--json` flag on
  these commands (per brief).

**Files modified:**
- `Src/LeoBloom.CLI/OutputFormatter.fs` — add `formatHuman` cases for
  each report type (table formatting for Schedule E, transaction list for
  general ledger, receipt/disbursement tables)
- `Src/LeoBloom.CLI/Program.fs` — add `Report` case to `LeoBloomArgs`
  DU, dispatch to `ReportCommands.dispatch`
- `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` — add Compile entry for
  ReportCommands.fs (before Program.fs), add ProjectReference to
  LeoBloom.Reporting

**Verification:** CLI builds. `leobloom report --help` shows four
subcommands. Each subcommand's `--help` shows correct flags.

### Phase 5: Test Project Wiring

**What:** Add LeoBloom.Reporting reference to test project so QE can
write tests in the next pipeline step.

**Files modified:**
- `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` — add ProjectReference to
  LeoBloom.Reporting

**Verification:** Test project compiles. Existing tests still pass.

---

## Acceptance Criteria

- [ ] `LeoBloom.Reporting` project exists, compiles, is in the solution
- [ ] `leobloom report schedule-e --year 2026` executes without error
  against a database with seeded data and returns formatted Schedule E
  output with correct line item mapping
- [ ] `leobloom report general-ledger --account 1110 --from 2026-01-01 --to 2026-12-31`
  returns transaction detail for the specified account
- [ ] `leobloom report cash-receipts --from 2026-01-01 --to 2026-12-31`
  returns all cash inflows with counterparty and date
- [ ] `leobloom report cash-disbursements --from 2026-01-01 --to 2026-12-31`
  returns all cash outflows with counterparty and date
- [ ] Schedule E report includes depreciation line from config (not DB)
- [ ] Schedule E "Other" line 19 shows sub-detail breakdown
- [ ] Invalid args (missing --year, bad date format, nonexistent account)
  produce errors on stderr with exit code 1
- [ ] No `--json` flag on any report subcommand
- [ ] No business logic in CLI layer — all report assembly is in
  LeoBloom.Reporting services
- [ ] All existing tests pass after changes
- [ ] Dependency graph: Reporting refs Domain + Utilities + Ledger only
  (not Ops)

## Risks

- **Depreciation config location:** appsettings.json works for one
  property. If Dan acquires more properties, this becomes a DB table.
  The service should read config through an abstraction (a function
  parameter or config reader) so swapping the source later doesn't
  require rewriting the service. Mitigation: design the service to take
  a depreciation config record, not read appsettings directly. A thin
  config-reading wrapper feeds it in.

- **Cash account identification:** Cash receipts/disbursements need to
  know which accounts are "cash" accounts. The COA has bank accounts
  under parent `1100` (Property Assets) and `1200` (Personal Assets).
  The report should include all asset accounts that represent actual
  bank/cash accounts (`1110`, `1120`, `1210`, `1220`). The mapping
  needs to be explicit, not inferred from account type alone (asset
  type includes the property itself, which isn't cash). Mitigation: the
  cash flow repository queries by explicit account codes or uses the
  leaf-level bank accounts under `1100` and `1200`.

- **Account resolution by code vs ID:** The CLI takes account codes
  (strings like "1110"), but the DB uses integer IDs internally. The
  general-ledger report service needs to resolve code to ID. Existing
  services don't have this lookup -- the repo will need a
  code-to-ID resolution query.

## Out of Scope

- Accounting reports (trial balance, balance sheet, income statement) --
  that's P037
- `--json` output for tax reports
- Fixed asset module / depreciation computation from DB
- Modifications to Ledger or Ops domain projects
- Multi-property support
- PDF/export formatting
