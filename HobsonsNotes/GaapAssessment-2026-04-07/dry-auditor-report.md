# DRY Audit Report -- LeoBloom

**Date:** 2026-04-07
**Codebase:** LeoBloom (F# / .NET 10 / PostgreSQL)
**Solution:** `LeoBloom.sln` -- 9 projects (Domain, Utilities, Ledger, Ops, Reporting, Portfolio, CLI, Migrations, Tests)

---

## Executive Summary

The codebase shows strong centralisation of some shared concerns (optParam, logging, exit codes, test cleanup), indicating prior consolidation work has already been done. The remaining duplication falls into clear clusters: (1) near-identical SQL query shapes across reporting repositories, (2) repeated date-parsing logic across every CLI command module, (3) repeated service boilerplate for open-connection/begin-transaction/try/catch/rollback, (4) replicated account-lookup queries across Ledger and Ops, and (5) duplicated `buildSection` helpers. The overall duplication level is moderate -- most of it is structural rather than semantic -- but there are several findings where a business rule change would require coordinated edits in multiple files.

---

## Findings

### Finding 1: JournalEntry Reader Duplication (3 locations)

**Classification:** Semantic

**Locations:**
- `LeoBloom.Ledger/JournalEntryRepository.fs` -- `insertEntry` (lines 27-36), `voidEntry` (lines 89-98), `getEntryById` (lines 141-150)

**What they share:** All three locations read a `JournalEntry` record from a `DbDataReader` using identical column-index-to-field mappings. The same 9-field mapping logic (id, entryDate, description, source, fiscalPeriodId, voidedAt, voidReason, createdAt, modifiedAt) is written out longhand three times.

**Where they diverge:** Nowhere meaningful. Pure copy-paste.

**What breaks if only one is updated:** If a column is added or reordered in the `journal_entry` table, all three locations must be updated. If only one is changed, the others will silently read the wrong columns. This is the classic "mapReader" extraction that other repositories (`FiscalPeriodRepository`, `ObligationAgreementRepository`, `ObligationInstanceRepository`, `TransferRepository`, `InvoiceRepository`, `AccountBalanceRepository`, `FundRepository`, `PositionRepository`, `InvestmentAccountRepository`) have already done with a private `readPeriod`/`mapReader`/`readAccount`/`readFund`/`readPosition` helper.

**Consolidation:** Straightforward. Extract a `private readEntry` function, as was done in every other repository in the codebase.

---

### Finding 2: parseDate Duplication (5+ CLI Modules)

**Classification:** Structural

**Locations:**
- `LeoBloom.CLI/LedgerCommands.fs` -- `parseDate` (line 93)
- `LeoBloom.CLI/AccountCommands.fs` -- `parseDate` (line 62)
- `LeoBloom.CLI/PeriodCommands.fs` -- `parseDate` (line 71)
- `LeoBloom.CLI/ObligationCommands.fs` -- `parseDate` (line 219)
- `LeoBloom.CLI/ReportCommands.fs` -- `parseDate` (line 142)
- `LeoBloom.CLI/PortfolioCommands.fs` -- `parseDate` (line 175)
- `LeoBloom.CLI/TransferCommands.fs` -- `parseDateOnly` (line 78) -- uses `TryParse` instead of `TryParseExact`

**What they share:** Six of seven locations are character-for-character identical: `DateOnly.TryParseExact(raw, "yyyy-MM-dd")` with the same error message format.

**Where they diverge:** `TransferCommands.fs` uses `DateOnly.TryParse` (lenient parsing) instead of `TryParseExact` (strict `yyyy-MM-dd`). This means Transfer date parsing accepts formats the other commands reject.

**What breaks if only one is updated:** If the date format is changed (e.g., to support ISO 8601 with time zones), six files need the same edit. The Transfer divergence is already a latent inconsistency.

**Consolidation:** Straightforward. Move to a shared `CliHelpers` or `ParsingHelpers` module in the CLI project.

---

### Finding 3: parsePeriodArg Duplication (2 locations)

**Classification:** Structural

**Locations:**
- `LeoBloom.CLI/PeriodCommands.fs` -- `parsePeriodArg` (line 66)
- `LeoBloom.CLI/ReportCommands.fs` -- `parsePeriodArg` (line 196)

**What they share:** Identical: `Int32.TryParse` to `Choice1Of2 id` / `Choice2Of2 raw`.

**Where they diverge:** Nowhere.

**What breaks if only one is updated:** If the id-or-key resolution logic changes (e.g., to support numeric period keys), both must be updated.

---

### Finding 4: Account Lookup Queries (3 locations)

**Classification:** Semantic

**Locations:**
- `LeoBloom.Ledger/JournalEntryService.fs` -- `lookupAccountActivity` (line 35): `SELECT id, is_active FROM ledger.account WHERE id IN (...)`
- `LeoBloom.Ops/ObligationAgreementService.fs` -- `lookupAccount` (line 11): `SELECT id, is_active FROM ledger.account WHERE id = @id`
- `LeoBloom.Ops/TransferService.fs` -- `lookupAccountInfo` (line 13): `SELECT a.is_active, at.name FROM ledger.account a JOIN ... WHERE a.id = @id`

**What they share:** All three query `ledger.account` to check existence and active status. They encode the same business rule: "accounts referenced by a write operation must exist and be active."

**Where they diverge:** JournalEntryService does a batch IN query; ObligationAgreementService does single-ID lookups; TransferService also fetches account type name (for the asset-only constraint).

**What breaks if only one is updated:** If the "active account" rule changes (e.g., to allow posting to archived accounts with a flag), three locations need updating. The ObligationAgreementService and JournalEntryService are the highest risk since they both check existence + is_active but with slightly different error messages and flow.

---

### Finding 5: Fiscal Period Lookup Queries (4 locations)

**Classification:** Semantic

**Locations:**
- `LeoBloom.Ledger/JournalEntryService.fs` -- `lookupFiscalPeriod` (line 17): `SELECT id, start_date, end_date, is_open FROM ledger.fiscal_period WHERE id = @id`
- `LeoBloom.Ledger/FiscalPeriodRepository.fs` -- `findById` (line 20): `SELECT id, period_key, start_date, end_date, is_open, created_at FROM ledger.fiscal_period WHERE id = @id`
- `LeoBloom.Ledger/TrialBalanceRepository.fs` -- `periodExists` (line 65): `SELECT id, period_key FROM ledger.fiscal_period WHERE id = @id`
- `LeoBloom.Ledger/TrialBalanceRepository.fs` -- `resolvePeriodId` (line 51): `SELECT id FROM ledger.fiscal_period WHERE period_key = @period_key`

**What they share:** All query the same `ledger.fiscal_period` table. JournalEntryService has its own private `FiscalPeriodCheck` type and query that overlaps with `FiscalPeriodRepository.findById`.

**Where they diverge:** JournalEntryService selects 4 columns (no period_key, no created_at); FiscalPeriodRepository selects all 6. TrialBalanceRepository.periodExists selects only (id, period_key). These are genuinely different projections, but the JournalEntryService one is the most concerning because it re-implements what FiscalPeriodRepository already does, with a different return type.

**What breaks if only one is updated:** If the fiscal_period schema changes (e.g., adding a `locked` flag that affects validation), JournalEntryService's private query won't pick it up. The `FiscalPeriodCheck` type in JournalEntryService is a private shadow of the domain `FiscalPeriod` type.

**Consolidation:** JournalEntryService could call `FiscalPeriodRepository.findById` and extract what it needs from the full `FiscalPeriod` domain type.

---

### Finding 6: Account Resolution Queries (3 locations)

**Classification:** Structural

**Locations:**
- `LeoBloom.Ledger/AccountBalanceRepository.fs` -- `resolveAccountId` (line 135): `SELECT id FROM ledger.account WHERE code = @code`
- `LeoBloom.Ledger/SubtreePLRepository.fs` -- `resolveAccount` (line 9): `SELECT id, code, name FROM ledger.account WHERE code = @code`
- `LeoBloom.Reporting/GeneralLedgerRepository.fs` -- `resolveAccountByCode` (line 12): `SELECT a.id, a.name, at.normal_balance FROM ledger.account a JOIN ... WHERE a.code = @code`

**What they share:** All resolve an account code to an ID (and optionally more fields).

**Where they diverge:** Each returns a different projection. AccountBalanceRepository wants just the ID; SubtreePLRepository wants (id, code, name); GeneralLedgerRepository wants (id, name, normal_balance). These are genuinely different queries serving different callers.

**What breaks:** Low risk in isolation since the divergences are intentional. Classifying as structural rather than semantic.

---

### Finding 7: buildSection Helper (3 locations)

**Classification:** Semantic

**Locations:**
- `LeoBloom.Ledger/IncomeStatementService.fs` -- `buildSection` (line 9)
- `LeoBloom.Ledger/BalanceSheetService.fs` -- `buildSection` (line 10)
- `LeoBloom.Ledger/SubtreePLService.fs` -- `buildSection` (line 9)

**What they share:** All three are identical. They build an `IncomeStatementSection` (or `BalanceSheetSection`) from a name and list of lines by summing the balance. IncomeStatementService and SubtreePLService both produce `IncomeStatementSection`. BalanceSheetService produces `BalanceSheetSection`, which is structurally identical to `IncomeStatementSection` (same fields: sectionName, lines, sectionTotal).

**Where they diverge:** The BalanceSheetService version operates on `BalanceSheetLine` instead of `IncomeStatementLine`, but these types are structurally identical: both have `{ accountId; accountCode; accountName; balance }`.

**What breaks if only one is updated:** If the section-building logic changes (e.g., adding a count or filtering zero-balance lines), three locations need the same edit.

**Consolidation:** IncomeStatementService and SubtreePLService could share one. The BalanceSheet version would require unifying the line types or using a generic helper.

---

### Finding 8: IncomeStatementLine / BalanceSheetLine Type Duplication

**Classification:** Semantic

**Locations:**
- `LeoBloom.Domain/Ledger.fs` -- `IncomeStatementLine` (line 236): `{ accountId; accountCode; accountName; balance }`
- `LeoBloom.Domain/Ledger.fs` -- `BalanceSheetLine` (line 256): `{ accountId; accountCode; accountName; balance }`

**What they share:** Field-for-field identical. Same types, same names, same shapes.

**Where they diverge:** Only the type name.

**What breaks:** If a field is added to one (e.g., `subType` for display), the other likely needs it too but won't get it automatically.

**Consolidation:** Could be unified into a single `ReportLine` type, used by both IncomeStatement and BalanceSheet sections.

---

### Finding 9: Near-Identical SQL Query Patterns in Reporting Repositories

**Classification:** Structural

**Locations:**
- `LeoBloom.Ledger/TrialBalanceRepository.fs` -- `getActivityByPeriod`
- `LeoBloom.Ledger/IncomeStatementRepository.fs` -- `getActivityByPeriod`
- `LeoBloom.Ledger/BalanceSheetRepository.fs` -- `getCumulativeBalances`
- `LeoBloom.Ledger/SubtreePLRepository.fs` -- `getSubtreeActivityByPeriod`
- `LeoBloom.Reporting/ScheduleERepository.fs` -- `getBalancesForYear`

**What they share:** All five queries follow the same shape:
```sql
SELECT a.id, a.code, a.name, at.name, at.normal_balance,
       COALESCE(SUM(CASE WHEN jel.entry_type = 'debit' ...), 0),
       COALESCE(SUM(CASE WHEN jel.entry_type = 'credit' ...), 0)
FROM ledger.journal_entry je
JOIN ledger.journal_entry_line jel ON ...
JOIN ledger.account a ON ...
JOIN ledger.account_type at ON ...
WHERE je.voided_at IS NULL AND [filter]
GROUP BY ...
ORDER BY ...
```

The debit/credit summing pattern and the normal-balance resolution (`match normalBalance with "credit" -> creditTotal - debitTotal | _ -> debitTotal - creditTotal`) are repeated in every reader loop.

**Where they diverge:** The WHERE clause filters differ (by fiscal_period_id, by date range, by account type, by subtree). The result types differ slightly. BalanceSheetRepository uses `entry_date <= @as_of_date` instead of `fiscal_period_id = @id`.

**What breaks if only one is updated:** If the debit/credit summing logic changes (e.g., to handle a new entry type), or if the voided_at filter changes, five locations need updating. The normal-balance resolution logic (debit-normal vs credit-normal) is the same business rule expressed five times.

**Consolidation:** The WHERE clause variation makes full deduplication non-trivial, but the normal-balance resolution could be extracted into a shared pure function, and the SQL fragments could share a common CTE or view.

---

### Finding 10: Service Boilerplate Pattern (open-connection/begin-txn/try-commit/catch-rollback)

**Classification:** Structural

**Locations:** Every service function across all five service projects follows this pattern:
```fsharp
use conn = DataSource.openConnection()
use txn = conn.BeginTransaction()
try
    // do work
    txn.Commit()
    result
with ex ->
    Log.errorExn ex "message" [||]
    try txn.Rollback() with _ -> ()
    Error [ sprintf "Persistence error: %s" ex.Message ]
```

Rough count: ~30+ functions across JournalEntryService, AccountBalanceService, FiscalPeriodService, TrialBalanceService, IncomeStatementService, BalanceSheetService, SubtreePLService, ObligationAgreementService, ObligationInstanceService, TransferService, InvoiceService, OrphanedPostingService, BalanceProjectionService, ScheduleEService, GeneralLedgerReportService, CashFlowReportService, FundService, InvestmentAccountService, PositionService, PortfolioReportService.

**What breaks:** This is a maintenance burden -- any cross-cutting change (e.g., adding a retry policy, changing error format, adding telemetry) requires editing 30+ functions. But each function's *business logic* is unique, so this is structural rather than semantic.

**Consolidation note:** A `withTransaction` higher-order function could eliminate the boilerplate. Common in F# codebases. Whether the team wants to pursue this is a design decision -- the boilerplate is verbose but explicit, and explicitness has value in an accounting system.

---

### Finding 11: Idempotency Guard Duplication (2 locations)

**Classification:** Semantic

**Locations:**
- `LeoBloom.Ops/ObligationPostingService.fs` -- lines 103-115 (idempotency check for obligation posting)
- `LeoBloom.Ops/TransferService.fs` -- lines 145-157 (idempotency check for transfer confirmation)

**What they share:** Both open a separate connection, call `JournalEntryRepository.findNonVoidedByReference` with a reference type and source ID, commit, and handle the result. The pattern is structurally identical:
```fsharp
let existingJeId =
    use guardConn = DataSource.openConnection()
    use guardTxn = guardConn.BeginTransaction()
    try
        let result = JournalEntryRepository.findNonVoidedByReference guardTxn "TYPE" (string id)
        guardTxn.Commit()
        result
    with ex ->
        try guardTxn.Rollback() with _ -> ()
        raise ex
```

**Where they diverge:** Only the reference type string ("obligation" vs "transfer").

**What breaks if only one is updated:** If the idempotency guard logic changes (e.g., checking additional conditions beyond non-voided), both must be updated.

**Consolidation:** Extract a shared `checkExistingJournalEntry referenceType sourceId` function.

---

### Finding 12: IncomeStatement buildReport / SubtreePL buildReport

**Classification:** Semantic

**Locations:**
- `LeoBloom.Ledger/IncomeStatementService.fs` -- `buildReport` (lines 14-29)
- `LeoBloom.Ledger/SubtreePLService.fs` -- `buildReport` (lines 14-31)

**What they share:** Near-identical logic: filter activity into revenue and expense lines, build sections, compute net income.

**Where they diverge:** SubtreePL's buildReport takes two additional parameters (rootCode, rootName) and returns a `SubtreePLReport` (which wraps the same data as `IncomeStatementReport` plus the root account info).

**What breaks if only one is updated:** If the net income calculation changes (e.g., to include "other income/expense" categories), both must be updated independently.

---

### Finding 13: OutputFormatter writeXxxList Functions (6+ near-identical functions)

**Classification:** Structural

**Locations:**
- `OutputFormatter.fs` -- `writeInvoiceList` (line 674), `writeTransferList` (line 686), `writeAccountList` (line 698), `writePeriodList` (line 708), `writeAgreementList` (line 723), `writeInstanceList` (line 731)

**What they share:** All follow the identical pattern:
```fsharp
let writeXxxList (isJson: bool) (items: Xxx list) : int =
    if isJson then
        Console.Out.WriteLine(formatJson items)
    else
        Console.Out.WriteLine(formatXxxList items)
    ExitCodes.success
```

**Where they diverge:** Only the type and the format function name.

**What breaks:** Minimal semantic risk -- these are display functions. But any change to the output convention (e.g., adding a count footer, changing the success exit code) requires editing all six.

**Consolidation:** Could be a single generic `writeList isJson formatFn items` function, though F# type erasure makes this slightly fiddly (which is why these exist in the first place, as the comments note).

---

### Finding 14: Portfolio Tracker vs Ledger Tracker (Parallel Design)

**Classification:** Coincidental

**Locations:**
- `LeoBloom.Tests/TestHelpers.fs` -- `TestCleanup.Tracker` (ledger/ops entities)
- `LeoBloom.Tests/PortfolioTestHelpers.fs` -- `PortfolioTracker` (portfolio entities)

**What they share:** Both are mutable tracker types with `tryDelete` cleanup logic and insert helpers.

**Where they diverge:** They track entirely different schemas (ledger/ops vs portfolio). The tryDelete pattern is duplicated but operates on different tables with different key types (int IDs vs string symbols for funds).

**Why this is coincidental:** These are genuinely different domains. Merging them into a single tracker would couple unrelated test concerns and make the code harder to understand. The structural similarity is an artifact of both following the same testing pattern, which is good design consistency rather than harmful duplication.

---

### Finding 15: Normal Balance Resolution Logic (5+ locations)

**Classification:** Semantic

**Locations:**
- `LeoBloom.Ledger/TrialBalanceRepository.fs` (line 32): `match reader.GetString(4) with "debit" -> NormalBalance.Debit | _ -> NormalBalance.Credit`
- `LeoBloom.Ledger/IncomeStatementRepository.fs` (lines 38-40): `match normalBalance with "credit" -> creditTotal - debitTotal | _ -> debitTotal - creditTotal`
- `LeoBloom.Ledger/BalanceSheetRepository.fs` (lines 39-41): same as above
- `LeoBloom.Ledger/SubtreePLRepository.fs` (lines 57-59): same as above
- `LeoBloom.Ledger/AccountBalanceRepository.fs` (line 119): `match reader.GetString(3) with "debit" -> NormalBalance.Debit | _ -> NormalBalance.Credit`
- `LeoBloom.Reporting/ScheduleERepository.fs` (lines 71-74): same pattern
- `LeoBloom.Reporting/GeneralLedgerReportService.fs` (lines 48-51): `match normalBalance with "credit" -> creditAmount - debitAmount | _ -> debitAmount - creditAmount`

**What they share:** All encode the same business rule: "For debit-normal accounts, balance = debits - credits. For credit-normal accounts, balance = credits - debits." This rule appears in at least 7 locations.

**What breaks if only one is updated:** This is the single most dangerous duplication in the codebase. If the accounting model changes (e.g., to support contra accounts, or a third normal balance direction), every location must be found and updated. A missed location would produce silently incorrect financial figures.

**Consolidation:** Extract a pure function into the Domain module: `let resolveBalance (normalBalance: string) (debits: decimal) (credits: decimal) : decimal`. All repositories call it.

---

## Summary Table

| # | Finding | Classification | Risk | Locations |
|---|---------|---------------|------|-----------|
| 1 | JournalEntry reader duplication | Semantic | Medium | 3 in JournalEntryRepository |
| 2 | parseDate in CLI modules | Structural | Low-Medium | 7 CLI modules |
| 3 | parsePeriodArg duplication | Structural | Low | 2 CLI modules |
| 4 | Account lookup queries | Semantic | Medium | 3 services (Ledger + Ops) |
| 5 | Fiscal period lookup queries | Semantic | Medium | 4 locations in Ledger |
| 6 | Account code resolution | Structural | Low | 3 repositories |
| 7 | buildSection helper | Semantic | Low-Medium | 3 services |
| 8 | IncomeStatementLine / BalanceSheetLine types | Semantic | Low | 2 types in Domain |
| 9 | SQL query shape in reporting repos | Structural | Medium | 5 repositories |
| 10 | Service txn boilerplate | Structural | Low | ~30 functions |
| 11 | Idempotency guard | Semantic | Medium | 2 posting services |
| 12 | Income statement / SubtreePL buildReport | Semantic | Low-Medium | 2 services |
| 13 | writeXxxList functions | Structural | Low | 6 functions in OutputFormatter |
| 14 | Test tracker patterns | Coincidental | None | 2 helper files |
| 15 | Normal balance resolution logic | **Semantic** | **High** | **7+ locations** |

---

## Priority Recommendations

**High priority (semantic duplication with real bug risk):**
1. **Finding 15** -- Normal balance resolution logic. This is the single business rule most likely to cause a silent data correctness bug if a change is made inconsistently. Extract to a single pure function.
2. **Finding 1** -- JournalEntry reader. Every other repository already has a `mapReader`/`readXxx` helper. This one was missed.

**Medium priority (semantic duplication, moderate risk):**
3. **Finding 5** -- JournalEntryService's private `FiscalPeriodCheck` shadows `FiscalPeriod`. Use the existing repository.
4. **Finding 11** -- Idempotency guard. Extract shared helper.
5. **Finding 4** -- Account lookup queries across service boundaries.

**Low priority (structural, maintenance burden but low bug risk):**
6. **Finding 2** -- CLI parseDate consolidation (and fix the Transfer divergence).
7. **Finding 7/8/12** -- buildSection and report line type deduplication.
8. **Finding 10** -- Service boilerplate (a design decision rather than a defect).

---

*Signed: DRY Auditor*
*Audit scope: Full source tree including Domain, Utilities, Ledger, Ops, Reporting, Portfolio, CLI, and Tests*
