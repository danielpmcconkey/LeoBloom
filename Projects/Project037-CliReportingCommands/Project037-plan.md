# Project 037 -- CLI Reporting Commands -- Plan

## Objective

Add 5 accounting report commands (`trial-balance`, `balance-sheet`,
`income-statement`, `pnl-subtree`, `account-balance`) to the existing
`leobloom report` subcommand group. Each is a thin CLI wrapper around an
existing Ledger service. No new domain logic. The existing 4 report commands
(Schedule E, General Ledger, Cash Receipts, Cash Disbursements) stay as-is
except for wiring changes to the shared dispatch.

## PO Flag Resolution: pnl-subtree Arguments

The backlog specifies `--from DATE --to DATE` but SubtreePLService only
supports period-based lookups (`getByAccountCodeAndPeriodId` /
`getByAccountCodeAndPeriodKey`). No date-range variant exists.

**Decision:** Use `--period <id-or-key>` instead, consistent with
trial-balance and income-statement. This is not a scope change -- the
backlog was aspirational about the interface, the service API is the
constraint.

## Architecture Notes

### Pattern Differences from P039/P042

TransferCommands and InvoiceCommands are in their own files with their own
dispatch functions and `--json` threaded from `Program.fs`. The existing
ReportCommands.fs uses `writeHuman` (no `--json` support) and its dispatch
does not accept `isJson`.

The new commands need `--json` support. Two paths:

1. Add `--json` to each new Argu DU and handle it per-command (like P039/P042
   do at the subcommand level).
2. Upgrade the `Report` dispatch in `Program.fs` to pass `isJson` through
   (requires changing the existing dispatch signature and the existing 4
   commands).

**Decision:** Option 1. Add `Json` to each new command's Argu DU. The new
handlers use `write isJson result` from OutputFormatter. The existing 4
commands continue using `writeHuman` unchanged. This keeps the diff surgical
and avoids touching working commands. The block in `Program.fs` that rejects
`--json` for report commands needs to be removed -- the new commands support
it, and the existing ones simply don't declare a `Json` case so the flag has
no effect on them.

Wait -- that won't work cleanly. The `Program.fs` block currently short-circuits
*all* report commands when `--json` is passed at the top level. The fix:
remove that short-circuit entirely. The new commands get `Json` in their own
DUs. The existing commands don't -- they just ignore it. Top-level `--json`
is not propagated to report dispatch (it's already not, since `dispatch`
doesn't take `isJson`). New commands pick up `--json` from their own args.

### Period Argument Parsing

Three commands accept `--period <id-or-key>`: trial-balance,
income-statement, pnl-subtree. The value is a string that could be either
an integer period ID or a string period key (e.g., "2026-01"). The handler
tries `Int32.TryParse` -- if it parses, call `getByPeriodId`; if not, call
`getByPeriodKey`. This is the same dual-path pattern used in the services.

**Helper function:**

```fsharp
let private parsePeriodArg (raw: string) : Choice<int, string> =
    match Int32.TryParse(raw) with
    | true, id -> Choice1Of2 id
    | false, _ -> Choice2Of2 raw
```

## Phases

### Phase 1: Argu DUs and Handlers in ReportCommands.fs

**What:** Add 5 new Argu DU types, 5 handler functions, wire them into
the existing `ReportArgs` DU and `dispatch` function. Add the
`parsePeriodArg` helper.

**Files modified:**
- `Src/LeoBloom.CLI/ReportCommands.fs`

**Argu DU Definitions:**

```fsharp
type TrialBalanceArgs =
    | [<Mandatory>] Period of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Period _ -> "Fiscal period ID or period key (e.g. 7 or 2026-01)"
            | Json -> "Output in JSON format"

type BalanceSheetArgs =
    | [<Mandatory>] As_Of of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | As_Of _ -> "As-of date (yyyy-MM-dd)"
            | Json -> "Output in JSON format"

type IncomeStatementArgs =
    | [<Mandatory>] Period of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Period _ -> "Fiscal period ID or period key (e.g. 7 or 2026-01)"
            | Json -> "Output in JSON format"

type PnlSubtreeArgs =
    | [<Mandatory>] Account of string
    | [<Mandatory>] Period of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Account _ -> "Root account code (e.g. 5000)"
            | Period _ -> "Fiscal period ID or period key (e.g. 7 or 2026-01)"
            | Json -> "Output in JSON format"

type AccountBalanceArgs =
    | [<Mandatory>] Account of string
    | As_Of of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Account _ -> "Account code (e.g. 1110)"
            | As_Of _ -> "As-of date (yyyy-MM-dd, defaults to today)"
            | Json -> "Output in JSON format"
```

**New cases in ReportArgs DU:**

```fsharp
| [<CliPrefix(CliPrefix.None)>] Trial_Balance of ParseResults<TrialBalanceArgs>
| [<CliPrefix(CliPrefix.None)>] Balance_Sheet of ParseResults<BalanceSheetArgs>
| [<CliPrefix(CliPrefix.None)>] Income_Statement of ParseResults<IncomeStatementArgs>
| [<CliPrefix(CliPrefix.None)>] Pnl_Subtree of ParseResults<PnlSubtreeArgs>
| [<CliPrefix(CliPrefix.None)>] Account_Balance of ParseResults<AccountBalanceArgs>
```

**Handler Signatures:**

```fsharp
let private handleTrialBalance (args: ParseResults<TrialBalanceArgs>) : int
let private handleBalanceSheet (args: ParseResults<BalanceSheetArgs>) : int
let private handleIncomeStatement (args: ParseResults<IncomeStatementArgs>) : int
let private handlePnlSubtree (args: ParseResults<PnlSubtreeArgs>) : int
let private handleAccountBalance (args: ParseResults<AccountBalanceArgs>) : int
```

**Handler Logic Sketches:**

`handleTrialBalance`:
- Extract `isJson` from args
- Get `period` string, run through `parsePeriodArg`
- `Choice1Of2 id` -> `TrialBalanceService.getByPeriodId id`
- `Choice2Of2 key` -> `TrialBalanceService.getByPeriodKey key`
- `write isJson (result |> Result.map (fun v -> v :> obj))`

`handleBalanceSheet`:
- Extract `isJson` from args
- Parse `--as-of` through existing `parseDate` helper
- `BalanceSheetService.getAsOfDate asOfDate`
- `write isJson (result |> Result.map (fun v -> v :> obj))`

`handleIncomeStatement`:
- Same pattern as trial-balance but calls `IncomeStatementService`

`handlePnlSubtree`:
- Extract `isJson`, `account`, and `period` from args
- Parse period with `parsePeriodArg`
- `Choice1Of2 id` -> `SubtreePLService.getByAccountCodeAndPeriodId account id`
- `Choice2Of2 key` -> `SubtreePLService.getByAccountCodeAndPeriodKey account key`
- `write isJson (result |> Result.map (fun v -> v :> obj))`

`handleAccountBalance`:
- Extract `isJson`, `account` from args
- `--as-of` is optional; default to `DateOnly.FromDateTime(DateTime.Today)`
- Parse date if provided, or use default
- `AccountBalanceService.getBalanceByCode account asOfDate`
- `write isJson (result |> Result.map (fun v -> v :> obj))`

**New opens needed at top of ReportCommands.fs:**

```fsharp
open LeoBloom.Ledger        // TrialBalanceService, BalanceSheetService, etc.
open LeoBloom.CLI.OutputFormatter  // already there
```

Note: The existing file opens `LeoBloom.Reporting` (for ScheduleEService, etc.).
The new commands use `LeoBloom.Ledger` (TrialBalanceService, BalanceSheetService,
IncomeStatementService, SubtreePLService, AccountBalanceService). Add the open.

**Dispatch additions:**

```fsharp
| Some (Trial_Balance tbArgs) -> handleTrialBalance tbArgs
| Some (Balance_Sheet bsArgs) -> handleBalanceSheet bsArgs
| Some (Income_Statement isArgs) -> handleIncomeStatement isArgs
| Some (Pnl_Subtree plArgs) -> handlePnlSubtree plArgs
| Some (Account_Balance abArgs) -> handleAccountBalance abArgs
```

**Verification:** `dotnet build` succeeds. `leobloom report --help` shows all
9 subcommands. Each new command's `--help` shows the correct arguments.

### Phase 2: Human-Readable Formatters in OutputFormatter.fs

**What:** Add `formatHuman` match cases for the 5 new report types and the
private formatting functions they dispatch to.

**Files modified:**
- `Src/LeoBloom.CLI/OutputFormatter.fs`

**New open needed:**

`LeoBloom.Domain.Ledger` is already opened. The 5 report types
(`TrialBalanceReport`, `BalanceSheetReport`, `IncomeStatementReport`,
`SubtreePLReport`, `AccountBalance`) are all defined there. No new open needed.

**Formatter Output Sketches (GAAP-compliant):**

#### formatTrialBalance

```
Trial Balance -- Period 2026-01 (ID: 7)

  ASSETS
  Code    Account Name                      Debit         Credit
  ------  --------------------------------  ------------  ------------
  1110    Operating Checking                    5,000.00
  1120    Savings Account                       2,000.00
  ------  --------------------------------  ------------  ------------
  Subtotal                                      7,000.00          0.00

  LIABILITIES
  ...

  Grand Total                                  10,000.00     10,000.00
  Status: BALANCED
```

Key rules:
- Separate debit/credit columns (not netted) per GAAP
- Group by account type with subtotals
- Grand totals for both columns independently
- Display balanced/unbalanced status

```fsharp
let private formatTrialBalance (report: TrialBalanceReport) : string
```

#### formatBalanceSheet

```
Balance Sheet -- As of 2026-03-31

  ASSETS
  Code    Account Name                         Balance
  ------  --------------------------------  ------------
  1110    Operating Checking                    5,000.00
  ------  --------------------------------  ------------
  Total Assets                                  7,000.00

  LIABILITIES
  ...
  Total Liabilities                             2,000.00

  EQUITY
  ...
  Subtotal Equity Accounts                      3,000.00
  Retained Earnings                             2,000.00
  Total Equity                                  5,000.00

  Total Liabilities + Equity                    7,000.00
  Status: BALANCED
```

Key rules:
- A = L + E structure
- Retained earnings as separate line within equity (not lumped)
- Display balanced/unbalanced status

```fsharp
let private formatBalanceSheet (report: BalanceSheetReport) : string
```

#### formatIncomeStatement

```
Income Statement -- Period 2026-01 (ID: 7)

  REVENUE
  Code    Account Name                         Amount
  ------  --------------------------------  ------------
  4100    Rental Income                         1,500.00
  ------  --------------------------------  ------------
  Total Revenue                                 1,500.00

  EXPENSES
  Code    Account Name                         Amount
  ------  --------------------------------  ------------
  5100    Property Tax                            500.00
  ------  --------------------------------  ------------
  Total Expenses                                  500.00

  Net Income                                    1,000.00
```

Key rules:
- Separate revenue/expense sections with totals
- Net income at bottom
- Amounts positive (normal balance convention)

```fsharp
let private formatIncomeStatement (report: IncomeStatementReport) : string
```

#### formatSubtreePL

```
P&L Subtree -- 5000 Property Expenses -- Period 2026-01 (ID: 7)

  REVENUE
  ...

  EXPENSES
  ...

  Net Income                                   -1,200.00
```

Key rules:
- Same as income statement, scoped to subtree
- Display root account code and name in header

```fsharp
let private formatSubtreePL (report: SubtreePLReport) : string
```

#### formatAccountBalance

```
Account Balance -- 1110 Operating Checking
  As of:          2026-03-31
  Normal Balance: Debit
  Balance:        5,000.00
```

Key rules:
- Show normal balance type so sign is interpretable
- Don't suppress or abs() negative balances

```fsharp
let private formatAccountBalance (bal: AccountBalance) : string
```

**formatHuman additions:**

```fsharp
| :? TrialBalanceReport as r -> formatTrialBalance r
| :? BalanceSheetReport as r -> formatBalanceSheet r
| :? IncomeStatementReport as r -> formatIncomeStatement r
| :? SubtreePLReport as r -> formatSubtreePL r
| :? AccountBalance as b -> formatAccountBalance b
```

**Verification:** Build succeeds. Running each command with test data produces
correctly formatted output matching the sketches above.

### Phase 3: Program.fs Wiring

**What:** Remove the `--json` short-circuit for report commands and update
the Report usage description.

**Files modified:**
- `Src/LeoBloom.CLI/Program.fs`

**Current code (lines 45-49):**

```fsharp
| Some (Report reportResults) ->
    if isJson then
        Console.Error.WriteLine("Error: --json is not supported for report commands")
        ExitCodes.businessError
    else
        ReportCommands.dispatch reportResults
```

**New code:**

```fsharp
| Some (Report reportResults) ->
    ReportCommands.dispatch reportResults
```

The `--json` guard must be removed because the new commands support it.
The existing commands (Schedule E, GL, Cash Receipts, Cash Disbursements) use
`writeHuman` which ignores JSON -- they simply won't produce JSON output even
if `--json` is passed at the top level, because they don't read it from their
args (they don't have a `Json` case). This is acceptable -- if someone passes
`leobloom --json report schedule-e --year 2026`, they get human output. The
correct invocation for the new commands is
`leobloom report trial-balance --period 7 --json` (per-command flag).

**Also update** the Report usage string in `LeoBloomArgs`:

```fsharp
| Report _ -> "Report commands (schedule-e, general-ledger, cash-receipts, cash-disbursements, trial-balance, balance-sheet, income-statement, pnl-subtree, account-balance)"
```

**Verification:** `leobloom report trial-balance --period 7 --json` produces
JSON. `leobloom report schedule-e --year 2026` still works identically to
before.

## File Change Summary

| File | Action | What |
|---|---|---|
| `Src/LeoBloom.CLI/ReportCommands.fs` | Modify | Add 5 Argu DUs, `parsePeriodArg`, 5 handlers, expand `ReportArgs` DU and dispatch, add `open LeoBloom.Ledger` |
| `Src/LeoBloom.CLI/OutputFormatter.fs` | Modify | Add 5 private format functions, 5 `formatHuman` match cases |
| `Src/LeoBloom.CLI/Program.fs` | Modify | Remove `--json` short-circuit for reports, update usage string |

No new files. No deleted files. No migrations. No domain changes.

## Acceptance Criteria

- [ ] `leobloom report trial-balance --period 7` produces human-readable tabular output with separate debit/credit columns, group subtotals, grand totals, and balanced/unbalanced status (exit 0)
- [ ] `leobloom report trial-balance --period 2026-01` (period key) also works
- [ ] `leobloom report trial-balance --period 7 --json` produces valid JSON (exit 0)
- [ ] `leobloom report balance-sheet --as-of 2026-03-31` produces human output with A=L+E structure, retained earnings as separate line, balanced status (exit 0)
- [ ] `leobloom report balance-sheet --as-of 2026-03-31 --json` produces valid JSON (exit 0)
- [ ] `leobloom report income-statement --period 7` produces human output with revenue/expense sections, section totals, net income (exit 0)
- [ ] `leobloom report income-statement --period 7 --json` produces valid JSON (exit 0)
- [ ] `leobloom report pnl-subtree --account 5000 --period 7` produces human output with root account in header, same format as income statement (exit 0)
- [ ] `leobloom report pnl-subtree --account 5000 --period 7 --json` produces valid JSON (exit 0)
- [ ] `leobloom report account-balance --account 1110` defaults `--as-of` to today, shows normal balance type, exit 0
- [ ] `leobloom report account-balance --account 1110 --as-of 2026-03-31 --json` produces valid JSON (exit 0)
- [ ] Missing required args for any new command prints usage to stderr and exits 2 (Argu's behavior)
- [ ] Invalid date values produce error message to stderr, exit 1
- [ ] Invalid period (nonexistent) produces error message to stderr, exit 1
- [ ] Invalid account code (nonexistent) produces error message to stderr, exit 1
- [ ] All 9 report subcommands appear in `leobloom report --help`
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass (no regressions)
- [ ] Existing Schedule E, General Ledger, Cash Receipts, Cash Disbursements commands behave identically to before

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Argu `Pnl_Subtree` generates CLI name `pnl-subtree` (hyphenated) | Low -- Argu converts underscores to hyphens by convention | Verify in `--help` output |
| `formatHuman` match order matters -- new types could shadow existing matches | Very low -- different types, no inheritance hierarchy | New cases go before the `_ -> sprintf "%A"` fallback |
| Top-level `--json` no longer errors for old report commands (silent ignore) | Low impact -- old commands never read it, output is unchanged | Documented in plan. Not a behavioral regression. |
| Decimal formatting inconsistency (`%M` vs comma-formatted) | Medium -- existing formatters use `%M` which omits commas | Follow existing convention (`%M`). If Dan wants commas, that's a separate formatting pass across all reports. |

## Out of Scope

- Modifying the existing 4 report commands (Schedule E, GL, Cash Receipts, Cash Disbursements)
- Adding `--json` support to the existing 4 report commands (they keep using `writeHuman`)
- Date-range P&L subtree (would require new service method -- separate project)
- Comma-formatted currency display (cosmetic, applies to all reports, separate project)
- New domain logic, repositories, or SQL
- Interactive prompts (per ADR-003)
