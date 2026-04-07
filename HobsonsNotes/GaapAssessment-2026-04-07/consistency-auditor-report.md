# Consistency Auditor Report -- LeoBloom

**Date:** 2026-04-07
**Scope:** Full source tree across CLI, Domain, Ledger, Ops, Portfolio, Reporting, Utilities, Migrations, Tests
**Method:** Survey every repeated pattern; identify the dominant convention; catalogue deviations; classify severity

---

## Executive Summary

The codebase is remarkably consistent for a system built by parallel autonomous agents. The architectural bones -- domain types in a shared project, services owning connections, repositories taking transactions, Argu-based CLI dispatch -- are uniform everywhere. The seams that show are mostly small and localized: a handful of duplicated helper functions, one divergent date parser, one dispatch signature that breaks the convention, and inconsistent empty-list messaging. A new developer could follow the dominant pattern in every category with only minor ambiguity.

**Findings by severity:**

| Severity | Count |
|---|---|
| Confusing | 2 |
| Noisy | 6 |
| Harmless divergence | 2 |
| Consistent | 8 |

---

## 1. CLI Command Structure

### 1a. Flag Naming Convention

**Dominant pattern:** Argu DU cases use `PascalCase` with underscores for multi-word flags (e.g. `Fiscal_Period_Id`, `Due_Before`, `Expected_Day`). Argu auto-converts these to `--fiscal-period-id` on the CLI wire. This is uniform.

**One exception:** PortfolioCommands uses `[<CustomAppSettings "...">]` attributes to override the kebab-case wire names (e.g. `Tax_Bucket_Id` with `CustomAppSettings "tax-bucket-id"`). No other command file uses `CustomAppSettings`.

**Classification: Harmless divergence.** The `CustomAppSettings` attribute addresses an Argu serialization edge case. The wire names remain kebab-case, matching the rest of the CLI. A comment explaining why would prevent a future agent from "normalizing" it away.

### 1b. Argument Ordering

**Dominant pattern:** `[<MainCommand; Mandatory>]` for the positional ID/key argument, then named flags in declaration order. `Json` is always last. This is uniform across all 12+ command DU types.

**Classification: Consistent.**

### 1c. Help Text Style

**Dominant pattern:** Short sentence fragments, no trailing period. Examples: `"Debit line in acct:amount format (repeatable)"`, `"Output in JSON format"`. Parenthetical hints for optional/repeatable fields. Format hints like `"(yyyy-MM-dd)"` and `"(yyyy-MM-dd, defaults to today)"`.

**One minor divergence:** PortfolioReportCommands help text says `"Grouping dimension (default: account-group). One of: tax-bucket, account-group, ..."` -- this is the only help string that contains a full sentence with a period. Other files express defaults with `"(default: 30)"` or `"(defaults to today)"` in parentheses.

**Classification: Noisy.** Not confusing, but the full-sentence style sticks out when scanning help output.

---

## 2. CLI Dispatch Signatures

**Dominant pattern:** `let dispatch (isJson: bool) (args: ParseResults<...>) : int` -- an `isJson` parameter threaded down from Program.fs.

Used by: LedgerCommands, AccountCommands, PeriodCommands, ObligationCommands, InvoiceCommands, TransferCommands, DiagnosticCommands, PortfolioCommands (8 of 9 command modules).

**Deviation:** `ReportCommands.dispatch` takes only `(args: ParseResults<ReportArgs>) : int` -- no `isJson` parameter. Program.fs calls it as `ReportCommands.dispatch reportResults` without passing `isJson`.

Inside ReportCommands, some handlers (TrialBalance, BalanceSheet, IncomeStatement, PnlSubtree, AccountBalance) extract `isJson` locally from the sub-args. Other handlers (ScheduleE, GeneralLedger, CashReceipts, CashDisbursements, Projection) have no `--json` support at all -- they use `writeHuman` or `writeHumanErrors` directly.

**Classification: Confusing.** A new developer adding a report subcommand would look at the dispatch signature, not find `isJson`, and either (a) add it themselves (breaking the call site), or (b) skip JSON support without realizing other reports do support it. The inconsistency between "some report subcommands support --json and some don't" is invisible from the dispatch level.

**Recommendation:** The dominant pattern should win. Add `isJson` to `ReportCommands.dispatch` and thread it to handlers that support JSON. Handlers without JSON support can ignore it (consistent with how DiagnosticCommands handles it). ~1 file to update.

---

## 3. Date Parsing

### 3a. Function Name and Implementation

**Dominant pattern:** `let private parseDate (raw: string) : Result<DateOnly, string>` using `DateOnly.TryParseExact(raw, "yyyy-MM-dd")`. This exact function is copy-pasted into 6 CLI command files: LedgerCommands, AccountCommands, PeriodCommands, ObligationCommands, ReportCommands, PortfolioCommands.

**Deviation:** TransferCommands defines `let private parseDateOnly (raw: string) : Result<DateOnly, string>` using `DateOnly.TryParse(raw)` -- the lenient parser, not the exact one.

This means TransferCommands accepts dates in formats like `3/15/2026` or `15-Mar-2026`, while every other command module requires strict `yyyy-MM-dd`. The error message still says `"expected yyyy-MM-dd"`, which would be misleading if the lenient parser accepted something else.

**Classification: Confusing.** A user who learns the date format from one command and uses it everywhere will hit inconsistent acceptance. Worse, the error message lies about what the parser actually accepts.

**Recommendation:** Replace `DateOnly.TryParse` with `DateOnly.TryParseExact(raw, "yyyy-MM-dd")` in TransferCommands. Rename `parseDateOnly` to `parseDate` for consistency. ~1 file, ~2 lines.

### 3b. Duplication

The `parseDate` function is identically defined in 7 CLI files. There is also a `parseDate` in `PortfolioReportService.fs` (a non-CLI file).

**Classification: Noisy.** Not confusing, but a DRY violation that will compound as more commands are added. Consider extracting to a shared `CliHelpers` module. ~7 files to update if consolidated.

---

## 4. Output Formatting

### 4a. Empty-List Messaging

**Dominant pattern:** Most formatters return a descriptive empty message: `"(no accounts found)"`, `"(no agreements found)"`, `"(no instances found)"`, `"(no investment accounts found)"`, `"(no funds found)"`, `"(no positions found)"`.

**Deviations:**
- `formatInvoiceList` returns `""` (empty string) when the list is empty.
- `formatTransferList` returns `""` (empty string) when the list is empty.
- The `writeInvoiceList` and `writeTransferList` functions check for empty output and skip printing, so the user sees nothing at all -- no feedback that the query succeeded with zero results.

**Classification: Noisy.** The user experience differs: some list commands print "(no X found)" and others print nothing. This is a minor UX inconsistency but it breaks the expectation set by the dominant pattern.

**Recommendation:** Return `"(no invoices found)"` and `"(no transfers found)"` respectively. ~2 files, ~2 lines each.

### 4b. Dedicated Write Functions vs. Generic `write`

**Dominant pattern:** For list operations, dedicated `write*List` functions handle the type erasure problem (F# generics and `obj` boxing). For single-entity operations, the generic `write isJson (result |> Result.map (fun v -> v :> obj))` pattern is used.

This pattern is applied uniformly. Every list has a dedicated writer. Every single entity goes through `write`.

**Classification: Consistent.** The pattern exists for a sound technical reason and is applied without exception.

### 4c. Human vs. JSON Output Support

**Dominant pattern:** Most commands support `--json` output (all commands that thread `isJson`).

**Deviations:** ScheduleE, GeneralLedger, CashReceipts, CashDisbursements, and Projection reports have no `--json` support. They use `writeHuman` / `writeHumanErrors` / `writeBalanceProjection` which have no JSON path.

**Classification: Harmless divergence.** These are human-readable narrative reports where the tabular format IS the value. Adding JSON would require defining serializable DTOs for report types that are currently formatting-only. The `ReportingTypes` module already defines proper types, but Projection uses `BalanceProjection` from the Ops domain which does not appear in `formatHuman`. This is a deliberate design choice per domain, not an oversight.

---

## 5. Repository Method Naming and Signatures

### 5a. Reader Helper Pattern

**Dominant pattern:** Repositories define a `private readX` or `private mapReader` function that takes a `DbDataReader` and returns the domain type. This is used in FiscalPeriodRepository (`readPeriod`), ObligationAgreementRepository (`mapReader`), FundRepository (`readFund`), InvestmentAccountRepository (`readAccount`), PositionRepository (`readPosition`).

**Deviation:** JournalEntryRepository has no shared reader helper. Each method (insertEntry, voidEntry, getEntryById) inline-constructs the `JournalEntry` record from the reader. The mapping logic is duplicated 3 times.

**Classification: Noisy.** Functionally correct but visually jarring when compared to the rest of the codebase. ~1 file to refactor.

### 5b. Reader Helper Naming

**Mixed naming:** `readPeriod`, `mapReader`, `readFund`, `readAccount`, `readPosition`. The dominant convention is `read{EntityName}` (4 of 5). The outlier is `mapReader` in ObligationAgreementRepository.

**Classification: Noisy.** A trivial rename. ~1 file, ~1 line.

### 5c. Method Naming Conventions

**Dominant pattern for CRUD:**
- `insert` / `create` for new records
- `findById` for single-entity lookup
- `list` / `listAll` / `listByFilter` for multi-entity queries
- `update` for modifications
- `deactivate` for soft-delete

These are consistent across ObligationAgreementRepository, InvoiceRepository, TransferRepository, FundRepository, InvestmentAccountRepository, PositionRepository, FiscalPeriodRepository.

**Classification: Consistent.**

### 5d. Nullable Parameter Handling

**Dominant pattern:** `DataHelpers.optParam` is used in JournalEntryRepository and ObligationAgreementRepository for nullable parameters.

**Deviation:** FundRepository defines its own local `addOpt` helper that does the same thing with a slightly different signature (`(param: string) (v: int option)` taking the option directly, not a boxed `obj option`).

**Classification: Noisy.** The local helper is more type-safe but breaks the convention. ~1 file to consolidate.

---

## 6. Service Method Naming and Signatures

### 6a. Connection/Transaction Ownership

**Dominant pattern:** Every service method opens its own connection + transaction via `use conn = DataSource.openConnection()` / `use txn = conn.BeginTransaction()`. This is universal across all 14+ service modules.

**Classification: Consistent.**

### 6b. Error Handling in Services

**Dominant pattern:** Write operations return `Result<T, string list>`. Read operations also return `Result<T, string list>` for most services.

**Deviations -- raw return types for list/query operations:**
- `ObligationAgreementService.list` returns `ObligationAgreement list` (not Result)
- `ObligationAgreementService.getById` returns `ObligationAgreement option` (not Result)
- `ObligationInstanceService.list` returns `ObligationInstance list` (not Result)
- `ObligationInstanceService.findUpcoming` returns `ObligationInstance list` (not Result)
- `InvoiceService.listInvoices` returns `Invoice list` (not Result)
- `TransferService.list` returns `Transfer list` (not Result)

These services silently return empty lists on error (swallowing the exception after logging).

By contrast:
- `AccountBalanceService.listAccounts` returns `Result<Account list, string list>`
- `FiscalPeriodService.listPeriods` returns `Result<FiscalPeriod list, string list>`
- `PositionService.listPositions` returns `Result<Position list, string list>`
- `FundService.listFunds` returns `Result<Fund list, string list>`
- `InvestmentAccountService.listAccounts` returns `Result<InvestmentAccount list, string list>`

**Classification: Confusing.** There are two conventions and a new developer cannot predict which one applies. The Ledger and Portfolio domains wrap list operations in `Result`. The Ops domain (for list/query operations) returns raw types and swallows errors. This means the CLI cannot distinguish "no results" from "query failed" for Ops list operations.

**Recommendation:** The `Result<T list, string list>` pattern should win -- it's used by 5 of the 11 list/query service methods and provides strictly more information to the caller. The 6 Ops methods that return raw types should be migrated. ~4 files, ~6 methods to update, plus CLI callers.

---

## 7. Domain Type Conventions

### 7a. Record Field Naming

**Dominant pattern:** `camelCase` for all record fields. This is 100% consistent across Ledger, Ops, and Portfolio domain types.

**Classification: Consistent.**

### 7b. DU String Conversion Modules

**Dominant pattern:** Companion modules with `toString` and `fromString` functions for each discriminated union that maps to a DB string. Used by: `ObligationDirection`, `InstanceStatus`, `RecurrenceCadence`, `PaymentMethodType`, `TransferStatus`, `EntryType`.

**Minor inconsistency in function style:** `ObligationDirection.toString` and `PaymentMethodType.toString` and `RecurrenceCadence.toString` use point-free `function` syntax (e.g. `let toString = function | Receivable -> ...`). `InstanceStatus.toString` uses explicit parameter `let toString (s: InstanceStatus) = match s with ...`. `EntryType.toDbString` uses explicit parameter AND a different name (`toDbString` instead of `toString`).

Similarly, `AccountSubType` uses `toDbString` / `fromDbString` while all Ops DUs use `toString` / `fromString`.

**Classification: Noisy.** The naming split (`toString` vs `toDbString`) falls along domain lines: Ledger uses `toDbString`, Ops uses `toString`. A new developer might wonder if there's a semantic difference. There isn't.

### 7c. Option vs. Nullable

**Dominant pattern:** F# `option` for all nullable fields. No use of `Nullable<T>` anywhere. This is 100% consistent.

**Classification: Consistent.**

---

## 8. SQL Style

### 8a. Migration Naming

**Dominant pattern:** `{timestamp}_{Action}{Entity}.sql` with PascalCase action+entity. Examples: `CreateAccount`, `SeedFiscalPeriods`, `CreatePortfolioSchema`, `EliminateLookupTables`, `AddSecondaryIndexes`, `ReplaceParentCodeWithParentId`.

**Classification: Consistent.** All 23 migrations follow this pattern.

### 8b. DDL Conventions

**Dominant pattern in migrations:**
- `serial PRIMARY KEY` for integer PKs
- `varchar(N)` for bounded strings, `text` for unbounded (though `text` is not used)
- `timestamptz NOT NULL DEFAULT now()` for timestamps
- `boolean NOT NULL DEFAULT true` for active flags
- `REFERENCES ... ON DELETE RESTRICT` for foreign keys
- All lowercase for column names, underscores between words

**Classification: Consistent.** The Portfolio schema migration (023) follows the exact same conventions as the Ledger/Ops migrations.

### 8c. SQL in Repositories

**Dominant pattern:** Multi-line SQL strings using standard F# string literals. SQL keywords in UPPERCASE (`SELECT`, `INSERT INTO`, `WHERE`, `ORDER BY`). Table-qualified names (`ledger.account`, `ops.obligation_instance`, `portfolio.fund`). Parameter names prefixed with `@`.

**Deviation:** Some Ops repositories use `$"..."` interpolated strings (ObligationAgreementRepository) while Ledger repositories use plain string literals. Portfolio repositories use `sprintf` for dynamic SQL. The SQL content is correct in all cases, but the string construction mechanism varies.

**Classification: Noisy.** Three different string construction approaches (`"..."`, `$"..."`, `sprintf`) for SQL. Functionally equivalent but visually inconsistent.

---

## 9. Error Handling Patterns

### 9a. Error Message Style

**Dominant pattern:** Error messages are full sentences with context. `"Fiscal period with id %d does not exist"`, `"Account with code '%s' does not exist"`, `"Transfer amount must be greater than zero"`.

**Classification: Consistent.** Error messages across all domains follow this pattern.

### 9b. Persistence Error Wrapping

**Dominant pattern:** `sprintf "Persistence error: %s" ex.Message` for unexpected database errors. Used by JournalEntryService, FiscalPeriodService, ObligationAgreementService, InvoiceService, TransferService, FundService, InvestmentAccountService, PositionService.

**One deviation:** `AccountBalanceService` uses `sprintf "Query error: %s" ex.Message` instead of `"Persistence error: %s"`.

**Classification: Noisy.** A user seeing "Query error" from one command and "Persistence error" from another might wonder if they mean different things. They don't. ~1 file, ~4 occurrences.

---

## 10. Test Naming and Structure

### 10a. Test Naming Convention

**Dominant pattern:** Gherkin-tagged integration tests use F# double-backtick identifiers with natural language: `` [<Fact>] [<Trait("GherkinId", "FT-PJE-001")>] let ``simple two-line entry posts successfully`` () = ``

Pure domain tests (DomainTests.fs) use the same style without Gherkin tags: `` let ``balanced two-line entry passes`` () = ``

**Classification: Consistent.**

### 10b. Test Structure -- Setup/Cleanup Pattern

**Dominant pattern:** Integration tests use `TestCleanup.create`, `TestData.uniquePrefix()`, `InsertHelpers` for setup, and `TestCleanup.deleteAll` in a `try/finally` block. This is uniform across PostJournalEntryTests, BalanceSheetTests, ObligationAgreementTests, ReportingServiceTests, and others.

**Classification: Consistent.**

### 10c. Test Assertion Style

**Dominant pattern:** `Assert.True` / `Assert.Equal` / `Assert.Fail` from xUnit. Result matching uses `match result with | Ok _ -> ... | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)`.

**Classification: Consistent.**

---

## 11. Logging

### 11a. Structured Logging

**Dominant pattern:** Serilog structured logging with `Log.info`, `Log.warn`, `Log.errorExn`. Template parameters use PascalCase placeholders: `{EntryId}`, `{AccountCode}`, `{FiscalPeriodId}`.

**Classification: Consistent.** Every service module uses the same thin wrapper and naming style.

---

## Summary of Actionable Findings

### Must Fix (Confusing)

| # | Finding | Files | Effort |
|---|---------|-------|--------|
| C1 | `ReportCommands.dispatch` missing `isJson` parameter, breaking the universal dispatch signature | 1 file (ReportCommands.fs) + 1 call site (Program.fs) | Small |
| C2 | `TransferCommands.parseDateOnly` uses lenient `DateOnly.TryParse` instead of `TryParseExact("yyyy-MM-dd")`, accepting formats the error message claims are invalid | 1 file (TransferCommands.fs) | Trivial |

### Should Fix (Noisy)

| # | Finding | Files | Effort |
|---|---------|-------|--------|
| N1 | `parseDate` function duplicated identically across 7 CLI files | 7 files | Small (extract to shared module) |
| N2 | Empty-list display: invoices and transfers return `""` instead of `"(no X found)"` | 1 file (OutputFormatter.fs) | Trivial |
| N3 | JournalEntryRepository inlines reader mapping 3 times instead of using a `readEntry` helper | 1 file (JournalEntryRepository.fs) | Small |
| N4 | `mapReader` in ObligationAgreementRepository vs. `read*` everywhere else | 1 file | Trivial rename |
| N5 | `AccountBalanceService` uses `"Query error:"` while all others use `"Persistence error:"` | 1 file (AccountBalanceService.fs) | Trivial |
| N6 | FundRepository defines local `addOpt` instead of using `DataHelpers.optParam` | 1 file (FundRepository.fs) | Trivial |

### Awareness Only (Harmless Divergence)

| # | Finding | Reason |
|---|---------|--------|
| H1 | PortfolioCommands uses `[<CustomAppSettings>]` attributes that no other command file uses | Addresses an Argu serialization edge case for hyphenated flag names |
| H2 | Some report subcommands lack `--json` support | Narrative tabular reports where human formatting IS the value |

---

## Verdict

The codebase is in good shape. The two confusing findings (C1, C2) are real risks -- the dispatch signature gap will cause the next Report subcommand to be written incorrectly, and the lenient date parser is a bug. The noisy findings are minor polish. The fundamental architecture, naming conventions, and patterns are impressively uniform given the multi-agent authorship model.

---

*Signed: Consistency Auditor*
*Assessment date: 2026-04-07*
