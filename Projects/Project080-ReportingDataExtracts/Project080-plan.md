# Project 080 — Reporting Data Extracts — Plan

## Objective

Implement four generic JSON data extract commands under a new `extract`
top-level CLI subcommand. These feed Hobson's report generation pipeline
with account tree, account balances, portfolio positions, and journal entry
line data. No formatting, hierarchy traversal, or normal balance adjustment
— raw data only.

## CLI Design Decision

**New top-level subcommand: `extract`** with four sub-subcommands:

```
leobloom extract account-tree
leobloom extract balances --as-of 2026-04-11
leobloom extract positions --as-of 2026-04-11
leobloom extract je-lines --fiscal-period-id 42
```

Rationale: these are data feeds for external consumption, not human-facing
reports. A dedicated `extract` namespace keeps them separate from the
existing `report` commands (which produce human-readable output). All four
commands always output JSON — the `--json` flag is accepted for consistency
but effectively a no-op. No human-readable formatter is needed.

## Phases

### Phase 1: Extract Types and Repository

**What:** Define the extract record types and the repository module with
all four SQL queries.

**Files created:**
- `Src/LeoBloom.Reporting/ExtractTypes.fs` — four record types
- `Src/LeoBloom.Reporting/ExtractRepository.fs` — four query functions

**Files modified:**
- `Src/LeoBloom.Reporting/LeoBloom.Reporting.fsproj` — add both files to
  compilation order (after `ReportingTypes.fs`, before `ScheduleEMapping.fs`)

**Extract types (all with `[<CLIMutable>]` for System.Text.Json):**

```fsharp
// AccountTreeRow
{ id: int; code: string; name: string; parentId: int option
  accountType: string; normalBalance: string; subtype: string option
  isActive: bool }

// AccountBalanceRow
{ accountId: int; code: string; name: string; balance: decimal }

// PortfolioPositionRow
{ investmentAccountId: int; investmentAccountName: string
  taxBucket: string; symbol: string; fundName: string
  positionDate: DateOnly; price: decimal; quantity: decimal
  currentValue: decimal; costBasis: decimal }

// JournalEntryLineRow
{ journalEntryId: int; entryDate: DateOnly; description: string
  source: string option; accountId: int; accountCode: string
  accountName: string; amount: decimal; entryType: string
  memo: string option }
```

**Repository queries:**

1. **`getAccountTree`** `(txn) → AccountTreeRow list`

   ```sql
   SELECT a.id, a.code, a.name, a.parent_id,
          at.name AS account_type, at.normal_balance,
          a.account_subtype, a.is_active
   FROM ledger.account a
   JOIN ledger.account_type at ON a.account_type_id = at.id
   ORDER BY a.code
   ```

2. **`getBalances`** `(txn, asOfDate) → AccountBalanceRow list`

   ```sql
   SELECT a.id AS account_id, a.code, a.name,
          SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE 0 END)
        - SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END)
            AS balance
   FROM ledger.journal_entry_line jel
   JOIN ledger.journal_entry je ON je.id = jel.journal_entry_id
   JOIN ledger.account a ON a.id = jel.account_id
   WHERE je.voided_at IS NULL
     AND je.entry_date <= @as_of_date
   GROUP BY a.id, a.code, a.name
   ORDER BY a.code
   ```

   **Critical:** INNER JOIN + WHERE for void filtering. Only accounts with
   at least one non-voided posting appear. Zero-balance accounts may be
   included (spec says "may be omitted" — we include them for completeness;
   Hobson infers zero from absence but having explicit zeros is also fine).

   Actually — re-reading the spec: "Accounts with zero balance may be
   omitted." We'll use `HAVING` to drop zero balances since the spec
   prefers it and it reduces noise:

   ```sql
   HAVING SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE 0 END)
        - SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END) <> 0
   ```

3. **`getPositions`** `(txn, asOfDate) → PortfolioPositionRow list`

   ```sql
   SELECT DISTINCT ON (p.investment_account_id, p.symbol)
          p.investment_account_id, ia.name AS investment_account_name,
          tb.name AS tax_bucket, p.symbol, f.name AS fund_name,
          p.position_date, p.price, p.quantity,
          p.current_value, p.cost_basis
   FROM portfolio.position p
   JOIN portfolio.investment_account ia ON ia.id = p.investment_account_id
   JOIN portfolio.tax_bucket tb ON tb.id = ia.tax_bucket_id
   JOIN portfolio.fund f ON f.symbol = p.symbol
   WHERE p.position_date <= @as_of_date
     AND p.current_value <> 0
   ORDER BY p.investment_account_id, p.symbol, p.position_date DESC
   ```

   **Critical:** `current_value <> 0` excludes fully liquidated positions.
   `DISTINCT ON` with `position_date DESC` picks the latest snapshot per
   (account, symbol) pair.

4. **`getJournalEntryLines`** `(txn, fiscalPeriodId) → JournalEntryLineRow list`

   ```sql
   SELECT je.id AS journal_entry_id, je.entry_date, je.description,
          je.source, jel.account_id, a.code AS account_code,
          a.name AS account_name, jel.amount,
          jel.entry_type, jel.memo
   FROM ledger.journal_entry_line jel
   JOIN ledger.journal_entry je ON je.id = jel.journal_entry_id
   JOIN ledger.account a ON a.id = jel.account_id
   JOIN ledger.fiscal_period fp ON fp.id = je.fiscal_period_id
   WHERE je.voided_at IS NULL
     AND fp.id = @fiscal_period_id
   ORDER BY a.code, je.entry_date, je.id
   ```

   **Note:** Filtering by `fiscal_period_id` FK directly, not by date
   range. The spec says "entry_date must fall within the fiscal period's
   start_date and end_date" — using the FK is equivalent and simpler since
   JEs already have a `fiscal_period_id` column. If a JE's entry_date is
   somehow outside its assigned period (data integrity issue), the FK
   approach is still correct — the JE was assigned to that period.

**Verification:** Unit tests compile. Queries return expected types.

### Phase 2: CLI Commands and Wiring

**What:** Add the `extract` subcommand to the CLI with four sub-subcommands,
wire them to the repository, and output JSON.

**Files created:**
- `Src/LeoBloom.CLI/ExtractCommands.fs` — Argu DU definitions + dispatch +
  four handler functions

**Files modified:**
- `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` — add `ExtractCommands.fs` before
  `Program.fs`
- `Src/LeoBloom.CLI/Program.fs` — add `Extract` case to `LeoBloomArgs` DU,
  wire dispatch

**Argu structure:**

```fsharp
type ExtractAccountTreeArgs =
    | Json
    interface IArgParserTemplate

type ExtractBalancesArgs =
    | [<Mandatory>] As_Of of string
    | Json
    interface IArgParserTemplate

type ExtractPositionsArgs =
    | As_Of of string   // optional, defaults to today
    | Json
    interface IArgParserTemplate

type ExtractJeLinesArgs =
    | [<Mandatory>] Fiscal_Period_Id of int
    | Json
    interface IArgParserTemplate

type ExtractArgs =
    | [<CliPrefix(CliPrefix.None)>] Account_Tree of ParseResults<ExtractAccountTreeArgs>
    | [<CliPrefix(CliPrefix.None)>] Balances of ParseResults<ExtractBalancesArgs>
    | [<CliPrefix(CliPrefix.None)>] Positions of ParseResults<ExtractPositionsArgs>
    | [<CliPrefix(CliPrefix.None)>] Je_Lines of ParseResults<ExtractJeLinesArgs>
    interface IArgParserTemplate
```

**Handler pattern** (same as existing commands):

```fsharp
let private handleBalances (isJson: bool) (args: ParseResults<ExtractBalancesArgs>) : int =
    let isJson = isJson || args.Contains ExtractBalancesArgs.Json
    let asOfRaw = args.GetResult ExtractBalancesArgs.As_Of
    match parseDate asOfRaw with
    | Error msg -> write isJson (Error [msg])
    | Ok asOf ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let rows = ExtractRepository.getBalances txn asOf
            txn.Commit()
            // Always JSON — formatJson + stdout
            Console.Out.WriteLine(formatJson rows)
            ExitCodes.success
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()
```

**Program.fs changes:**

```fsharp
// Add to LeoBloomArgs DU:
| [<CliPrefix(CliPrefix.None)>] Extract of ParseResults<ExtractArgs>

// Add to dispatch match:
| Some (Extract extractResults) ->
    ExtractCommands.dispatch isJson extractResults
```

**Verification:** `dotnet build` succeeds. Manual smoke test of each
command against `leobloom_dev`.

### Phase 3: Tests

**What:** Behavioral tests proving the four extracts return correct data
with proper filtering.

**Files created:**
- `Src/LeoBloom.Tests/ExtractRepositoryTests.fs` — integration tests
  hitting `leobloom_test` database

**Files modified:**
- `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` — add test file

**Test cases:**

1. **Account tree returns all accounts with correct fields** —
   verify result is non-empty, every row has non-null code/name/accountType,
   parent_id references a valid id in the result set (or is null for roots).

2. **Balances exclude voided entries** — post a JE, void it, run extract,
   confirm the voided amounts are not in the balance.

3. **Balances respect as-of date** — post JEs on different dates, run
   extract with an as-of between them, confirm only earlier entries
   contribute to balances.

4. **Portfolio positions use latest snapshot** — insert two position
   snapshots for the same (account, symbol) with different dates, confirm
   only the later one (≤ as-of) appears.

5. **Portfolio positions exclude zero-value** — insert a position with
   `current_value = 0`, confirm it's absent from results.

6. **JE lines filter by fiscal period** — post JEs in two different
   periods, run extract for one period, confirm only that period's lines
   appear.

7. **JE lines exclude voided entries** — post a JE, void it, run extract,
   confirm voided lines are absent.

**Verification:** `dotnet test` passes.

## Acceptance Criteria

### Behavioral (→ Gherkin scenarios)

- [ ] `extract account-tree` returns JSON array with id, code, name, parent_id, account_type, normal_balance, subtype, is_active for every account
- [ ] `extract balances --as-of <date>` returns only non-voided postings up to the given date (void filtering: INNER JOIN + WHERE, not LEFT JOIN)
- [ ] `extract balances --as-of <date>` returns raw debit-minus-credit balance (no normal balance adjustment)
- [ ] `extract positions --as-of <date>` returns latest position per (account, symbol) as of the given date
- [ ] `extract positions` excludes positions where current_value = 0
- [ ] `extract je-lines --fiscal-period-id <id>` returns all non-voided JE lines within the fiscal period
- [ ] `extract je-lines` orders output by account_code ASC, entry_date ASC, journal_entry_id ASC

### Structural (→ Builder/QE verify)

- [ ] `ExtractTypes.fs` defines four record types with correct field names/types
- [ ] `ExtractRepository.fs` uses INNER JOIN + WHERE for void filtering (never LEFT JOIN)
- [ ] `ExtractCommands.fs` registered in Program.fs dispatch
- [ ] All four commands produce valid JSON to stdout
- [ ] Commands work against both `leobloom_dev` and `leobloom_prod` (env-controlled)

## Risks

- **`DISTINCT ON` with `current_value <> 0` filter:** If the latest
  snapshot has `current_value = 0` but an earlier one doesn't, the position
  is correctly excluded (we want current state, not historical). This
  matches the spec's intent — "fully sold or rolled over."

- **Fiscal period FK vs date range:** Using `je.fiscal_period_id = @id`
  instead of date-range filtering. This is simpler and correct assuming JEs
  are assigned to the right period. If there's ever a mismatch, that's a
  data integrity bug to fix separately, not a reporting concern.

- **JSON field naming:** `JsonNamingPolicy.CamelCase` will convert F#
  PascalCase record fields to camelCase. The spec examples use snake_case
  (`account_id`, `entry_type`). We'll use `[<JsonPropertyName("...")>]`
  attributes on the record types to match the spec's snake_case contract
  exactly. This is important — Hobson's scripts will parse these field names.

## Out of Scope

- PDF report generation (Hobson's domain)
- Normal balance adjustment on balances
- Account hierarchy traversal / tree building
- Aggregation or roll-up of JE lines
- Human-readable formatting for extract commands
