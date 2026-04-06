# Project 041 -- Plan

## Objective

Expose account queries (read-only) and fiscal period management as two new
CLI subcommand groups (`account` and `period`). This requires five new
repository/service functions (account list, account show, period list, period
create, period find-by-key) plus two new CLI command files wiring into those
services and the existing close/reopen/balance services.

## PO Flag Resolutions

**F1 -- Phasing:** Repo/service first (Phase 1), then CLI/formatter (Phase 2).
Unit tests can target the service layer independently.

**F2 -- id-or-key / id-or-code resolution:** Follow the `parsePeriodArg`
pattern from `ReportCommands.fs` (line 175). The CLI does `Int32.TryParse` and
calls different service methods. No magic "accepts string, resolves internally"
service -- that would break the explicit function signature convention used
everywhere else (e.g., `getBalanceById` vs `getBalanceByCode`).

Concrete helpers to add:

```fsharp
// AccountCommands.fs
let private parseAccountArg (raw: string) : Choice<int, string> =
    match Int32.TryParse(raw) with
    | true, id -> Choice1Of2 id
    | false, _ -> Choice2Of2 raw

// PeriodCommands.fs
let private parsePeriodArg (raw: string) : Choice<int, string> =
    match Int32.TryParse(raw) with
    | true, id -> Choice1Of2 id
    | false, _ -> Choice2Of2 raw
```

**F3 -- Period create overlap validation:** Punt. GAAP allows overlapping
fiscal periods (fiscal year containing monthly periods). The DB has a UNIQUE
constraint on `period_key`, which prevents the most likely human error
(creating a duplicate month). Adding date-range overlap detection is
out of scope -- it would need a design decision about what "overlapping"
means when hierarchical periods are legitimate.

**F4 -- Account type filter values:** Confirmed from the `account_type` seed
data (migration `1712000002000`): `asset`, `liability`, `equity`, `revenue`,
`expense`. All lowercase in the DB. The CLI should accept case-insensitive
input and normalize to lowercase before querying.

## Phases

### Phase 1: Repository + Service Layer

**What:** Five new repository functions and thin service wrappers.

**Files modified:**
- `Src/LeoBloom.Ledger/AccountBalanceRepository.fs` -- add `listAccounts`, `findAccountById`, `findAccountByCode`
- `Src/LeoBloom.Ledger/AccountBalanceService.fs` -- add `listAccounts`, `showAccountById`, `showAccountByCode`
- `Src/LeoBloom.Ledger/FiscalPeriodRepository.fs` -- add `listAll`, `findByKey`, `create`
- `Src/LeoBloom.Ledger/FiscalPeriodService.fs` -- add `listPeriods`, `createPeriod`, `findPeriodByKey`

**Files modified (project):**
- No fsproj change needed for Ledger -- same files, new functions.

#### 1a. AccountBalanceRepository -- new functions

```fsharp
/// List accounts with optional type filter and inactive toggle.
/// When includeInactive=false, only is_active=true rows are returned.
/// When accountTypeName is Some, JOINs to account_type and filters by name.
let listAccounts
    (txn: NpgsqlTransaction)
    (accountTypeName: string option)
    (includeInactive: bool)
    : Account list
```

SQL sketch:
```sql
SELECT a.id, a.code, a.name, a.account_type_id, a.parent_code,
       a.account_subtype, a.is_active, a.created_at, a.modified_at
FROM ledger.account a
JOIN ledger.account_type at ON a.account_type_id = at.id
WHERE (@type_name IS NULL OR at.name = @type_name)
  AND (@include_inactive OR a.is_active = true)
ORDER BY a.code
```

Note: The `account_subtype` column (nullable varchar, added in migration
`1712000021000`) must be read and mapped via `AccountSubType.fromDbString`.
If the DB value is `NULL`, map to `None`. If it's a valid string, map to
`Some subType`. If it's an unrecognized string, log a warning and map to
`None` (defensive -- don't blow up list queries over bad seed data).

```fsharp
/// Find a single account by ID. Returns full Account record.
let findAccountById (txn: NpgsqlTransaction) (accountId: int) : Account option
```

SQL sketch:
```sql
SELECT a.id, a.code, a.name, a.account_type_id, a.parent_code,
       a.account_subtype, a.is_active, a.created_at, a.modified_at
FROM ledger.account a
WHERE a.id = @id
```

```fsharp
/// Find a single account by code. Returns full Account record.
let findAccountByCode (txn: NpgsqlTransaction) (code: string) : Account option
```

Same SQL, but `WHERE a.code = @code`.

**Reader helper:** Since all three functions read the same column set into
an `Account` record, extract a private `readAccount` helper:

```fsharp
let private readAccount (reader: NpgsqlDataReader) : Account =
    let subTypeStr =
        if reader.IsDBNull(5) then None
        else Some (reader.GetString(5))
    let subType =
        subTypeStr
        |> Option.bind (fun s ->
            match AccountSubType.fromDbString s with
            | Ok st -> Some st
            | Error _ -> None)
    { id = reader.GetInt32(0)
      code = reader.GetString(1)
      name = reader.GetString(2)
      accountTypeId = reader.GetInt32(3)
      parentCode =
          if reader.IsDBNull(4) then None else Some (reader.GetString(4))
      subType = subType
      isActive = reader.GetBoolean(6)
      createdAt = reader.GetFieldValue<DateTimeOffset>(7)
      modifiedAt = reader.GetFieldValue<DateTimeOffset>(8) }
```

#### 1b. AccountBalanceService -- new functions

Follow the same conn/txn/try pattern as `getBalanceById`.

```fsharp
/// List accounts. No business logic -- straight pass-through to repo.
let listAccounts
    (accountTypeName: string option)
    (includeInactive: bool)
    : Result<Account list, string list>
```

```fsharp
/// Show a single account by ID.
let showAccountById (accountId: int) : Result<Account, string>
```

Returns `Error "Account with id N does not exist"` on None.

```fsharp
/// Show a single account by code.
let showAccountByCode (code: string) : Result<Account, string>
```

Returns `Error "Account with code 'X' does not exist"` on None.

Note: `listAccounts` returns `Result<_, string list>` for consistency with
the error-list pattern used by `closePeriod`/`reopenPeriod`. The single-item
functions return `Result<_, string>` matching `getBalanceById`/`getBalanceByCode`.

#### 1c. FiscalPeriodRepository -- new functions

```fsharp
/// List all fiscal periods, ordered by start_date.
let listAll (txn: NpgsqlTransaction) : FiscalPeriod list
```

SQL sketch:
```sql
SELECT id, period_key, start_date, end_date, is_open, created_at
FROM ledger.fiscal_period
ORDER BY start_date
```

**Reader helper:** Extract a private `readPeriod` from the existing `findById`
code (all three existing functions + the new ones read the same 6 columns).

```fsharp
/// Find a fiscal period by period_key. Returns None if not found.
let findByKey (txn: NpgsqlTransaction) (periodKey: string) : FiscalPeriod option
```

SQL: same SELECT, `WHERE period_key = @key`.

```fsharp
/// Insert a new fiscal period. Returns the created record.
let create
    (txn: NpgsqlTransaction)
    (periodKey: string)
    (startDate: DateOnly)
    (endDate: DateOnly)
    : FiscalPeriod
```

SQL sketch:
```sql
INSERT INTO ledger.fiscal_period (period_key, start_date, end_date)
VALUES (@key, @start, @end)
RETURNING id, period_key, start_date, end_date, is_open, created_at
```

New periods default to `is_open = true` (the DB default).

#### 1d. FiscalPeriodService -- new functions

```fsharp
/// List all fiscal periods.
let listPeriods () : Result<FiscalPeriod list, string list>
```

Thin wrapper: open conn, begin txn, call `listAll`, commit, return `Ok`.
Catches exceptions and returns `Error ["Persistence error: ..."]`.

```fsharp
/// Create a new fiscal period. Validates key/dates are non-empty,
/// start <= end. Does NOT check for date overlaps (see F3 resolution).
let createPeriod
    (periodKey: string)
    (startDate: DateOnly)
    (endDate: DateOnly)
    : Result<FiscalPeriod, string list>
```

Validation before DB call:
- `periodKey` must not be blank
- `startDate` must be <= `endDate`

On constraint violation (duplicate `period_key`), catch the Npgsql exception
and return `Error ["A fiscal period with key 'X' already exists"]`.

```fsharp
/// Find a fiscal period by key. Used by CLI for id-or-key resolution.
let findPeriodByKey (key: string) : Result<FiscalPeriod, string list>
```

Returns `Error ["Fiscal period with key 'X' does not exist"]` on None.

**Verification:** All existing tests pass. New functions compile. Can be
smoke-tested with `dotnet build`.

---

### Phase 2: CLI Commands + Formatters

**What:** Two new command files, Program.fs wiring, OutputFormatter additions.

**Files created:**
- `Src/LeoBloom.CLI/AccountCommands.fs`
- `Src/LeoBloom.CLI/PeriodCommands.fs`

**Files modified:**
- `Src/LeoBloom.CLI/OutputFormatter.fs` -- add formatters
- `Src/LeoBloom.CLI/Program.fs` -- add DU cases + dispatch
- `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` -- add new .fs files to compile order

#### 2a. fsproj file order

Insert new files before `Program.fs`:

```xml
<Compile Include="AccountCommands.fs" />
<Compile Include="PeriodCommands.fs" />
<Compile Include="Program.fs" />
```

#### 2b. AccountCommands.fs

```fsharp
module LeoBloom.CLI.AccountCommands

open System
open Argu
open LeoBloom.Domain.Ledger
open LeoBloom.Ledger
open LeoBloom.CLI.OutputFormatter

// --- Argu DU definitions ---

type AccountListArgs =
    | Type of string
    | Inactive
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Type _ -> "Filter by account type (asset, liability, equity, revenue, expense)"
            | Inactive -> "Include inactive accounts"
            | Json -> "Output in JSON format"

type AccountShowArgs =
    | [<MainCommand; Mandatory>] Account of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Account _ -> "Account ID or code"
            | Json -> "Output in JSON format"

type AccountBalanceCmdArgs =
    | [<MainCommand; Mandatory>] Account of string
    | As_Of of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Account _ -> "Account ID or code"
            | As_Of _ -> "As-of date (yyyy-MM-dd, defaults to today)"
            | Json -> "Output in JSON format"

type AccountArgs =
    | [<CliPrefix(CliPrefix.None)>] List of ParseResults<AccountListArgs>
    | [<CliPrefix(CliPrefix.None)>] Show of ParseResults<AccountShowArgs>
    | [<CliPrefix(CliPrefix.None)>] Balance of ParseResults<AccountBalanceCmdArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | List _ -> "List accounts with optional filters"
            | Show _ -> "Show account details"
            | Balance _ -> "Show account balance"
```

Note on naming: `AccountBalanceCmdArgs` avoids collision with the existing
`ReportCommands.AccountBalanceArgs`. The `account balance` subcommand is
functionally identical to `report account-balance` but lives under the
`account` group for discoverability. Both call the same service.

**Handlers:**

```fsharp
let private parseAccountArg (raw: string) : Choice<int, string> =
    match Int32.TryParse(raw) with
    | true, id -> Choice1Of2 id
    | false, _ -> Choice2Of2 raw

let private parseDate (raw: string) : Result<DateOnly, string> =
    match DateOnly.TryParseExact(raw, "yyyy-MM-dd") with
    | true, d -> Ok d
    | false, _ -> Error (sprintf "Invalid date format '%s' -- expected yyyy-MM-dd" raw)

let private validAccountTypes =
    Set.ofList [ "asset"; "liability"; "equity"; "revenue"; "expense" ]

let private handleList (isJson: bool) (args: ParseResults<AccountListArgs>) : int =
    let isJson = isJson || args.Contains AccountListArgs.Json
    let typeRaw = args.TryGetResult AccountListArgs.Type
    let includeInactive = args.Contains AccountListArgs.Inactive

    // Validate type if provided
    match typeRaw with
    | Some t when not (validAccountTypes.Contains (t.ToLowerInvariant())) ->
        write isJson (Error [sprintf "Invalid account type '%s' -- valid types: asset, liability, equity, revenue, expense" t])
    | _ ->
        let typeName = typeRaw |> Option.map (fun t -> t.ToLowerInvariant())
        let result = AccountBalanceService.listAccounts typeName includeInactive
        writeAccountList isJson (result |> Result.defaultValue [])
        // If result is Error, use write with the error
        // (but listAccounts only fails on DB errors, so handle both)

// ... (see full handler pattern below)
```

Actually, cleaner approach matching the Invoice/Transfer list pattern:

```fsharp
let private handleList (isJson: bool) (args: ParseResults<AccountListArgs>) : int =
    let isJson = isJson || args.Contains AccountListArgs.Json
    let typeRaw = args.TryGetResult AccountListArgs.Type
    let includeInactive = args.Contains AccountListArgs.Inactive

    match typeRaw with
    | Some t when not (validAccountTypes.Contains (t.ToLowerInvariant())) ->
        write isJson (Error [sprintf "Invalid account type '%s' -- valid types: asset, liability, equity, revenue, expense" t])
    | _ ->
        let typeName = typeRaw |> Option.map (fun t -> t.ToLowerInvariant())
        match AccountBalanceService.listAccounts typeName includeInactive with
        | Ok accounts -> writeAccountList isJson accounts
        | Error errs -> write isJson (Error errs)

let private handleShow (isJson: bool) (args: ParseResults<AccountShowArgs>) : int =
    let isJson = isJson || args.Contains AccountShowArgs.Json
    let accountRaw = args.GetResult AccountShowArgs.Account

    let result =
        match parseAccountArg accountRaw with
        | Choice1Of2 id -> AccountBalanceService.showAccountById id
        | Choice2Of2 code -> AccountBalanceService.showAccountByCode code

    write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))

let private handleBalance (isJson: bool) (args: ParseResults<AccountBalanceCmdArgs>) : int =
    let isJson = isJson || args.Contains AccountBalanceCmdArgs.Json
    let accountRaw = args.GetResult AccountBalanceCmdArgs.Account
    let asOfRaw = args.TryGetResult AccountBalanceCmdArgs.As_Of

    let asOfResult =
        match asOfRaw with
        | None -> Ok (DateOnly.FromDateTime(DateTime.Today))
        | Some raw -> parseDate raw

    match asOfResult with
    | Error e -> write isJson (Error [e])
    | Ok asOfDate ->
        let result =
            match parseAccountArg accountRaw with
            | Choice1Of2 id -> AccountBalanceService.getBalanceById id asOfDate
            | Choice2Of2 code -> AccountBalanceService.getBalanceByCode code asOfDate
        write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))

// --- Dispatch ---

let dispatch (isJson: bool) (args: ParseResults<AccountArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (List listArgs) -> handleList isJson listArgs
    | Some (Show showArgs) -> handleShow isJson showArgs
    | Some (Balance balanceArgs) -> handleBalance isJson balanceArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
```

#### 2c. PeriodCommands.fs

```fsharp
module LeoBloom.CLI.PeriodCommands

open System
open Argu
open LeoBloom.Domain.Ledger
open LeoBloom.Ledger
open LeoBloom.CLI.OutputFormatter

// --- Argu DU definitions ---

type PeriodListArgs =
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Json -> "Output in JSON format"

type PeriodCloseArgs =
    | [<MainCommand; Mandatory>] Period of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Period _ -> "Fiscal period ID or period key"
            | Json -> "Output in JSON format"

type PeriodReopenArgs =
    | [<MainCommand; Mandatory>] Period of string
    | [<Mandatory>] Reason of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Period _ -> "Fiscal period ID or period key"
            | Reason _ -> "Reason for reopening the period"
            | Json -> "Output in JSON format"

type PeriodCreateArgs =
    | [<Mandatory>] Start of string
    | [<Mandatory>] End of string
    | [<Mandatory>] Key of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Start _ -> "Period start date (yyyy-MM-dd)"
            | End _ -> "Period end date (yyyy-MM-dd)"
            | Key _ -> "Period key (e.g. 2026-05)"
            | Json -> "Output in JSON format"

type PeriodArgs =
    | [<CliPrefix(CliPrefix.None)>] List of ParseResults<PeriodListArgs>
    | [<CliPrefix(CliPrefix.None)>] Close of ParseResults<PeriodCloseArgs>
    | [<CliPrefix(CliPrefix.None)>] Reopen of ParseResults<PeriodReopenArgs>
    | [<CliPrefix(CliPrefix.None)>] Create of ParseResults<PeriodCreateArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | List _ -> "List all fiscal periods"
            | Close _ -> "Close a fiscal period"
            | Reopen _ -> "Reopen a closed fiscal period"
            | Create _ -> "Create a new fiscal period"
```

**Handlers:**

```fsharp
let private parsePeriodArg (raw: string) : Choice<int, string> =
    match Int32.TryParse(raw) with
    | true, id -> Choice1Of2 id
    | false, _ -> Choice2Of2 raw

let private parseDate (raw: string) : Result<DateOnly, string> =
    match DateOnly.TryParseExact(raw, "yyyy-MM-dd") with
    | true, d -> Ok d
    | false, _ -> Error (sprintf "Invalid date format '%s' -- expected yyyy-MM-dd" raw)

/// Resolves a period arg (id or key) to a period ID.
/// On key input, calls findPeriodByKey. On id input, passes through.
let private resolvePeriodId (raw: string) : Result<int, string list> =
    match parsePeriodArg raw with
    | Choice1Of2 id -> Ok id
    | Choice2Of2 key ->
        match FiscalPeriodService.findPeriodByKey key with
        | Ok period -> Ok period.id
        | Error errs -> Error errs

let private handleList (isJson: bool) (args: ParseResults<PeriodListArgs>) : int =
    let isJson = isJson || args.Contains PeriodListArgs.Json
    match FiscalPeriodService.listPeriods () with
    | Ok periods -> writePeriodList isJson periods
    | Error errs -> write isJson (Error errs)

let private handleClose (isJson: bool) (args: ParseResults<PeriodCloseArgs>) : int =
    let isJson = isJson || args.Contains PeriodCloseArgs.Json
    let periodRaw = args.GetResult PeriodCloseArgs.Period

    match resolvePeriodId periodRaw with
    | Error errs -> write isJson (Error errs)
    | Ok periodId ->
        let cmd : CloseFiscalPeriodCommand = { fiscalPeriodId = periodId }
        let result = FiscalPeriodService.closePeriod cmd
        write isJson (result |> Result.map (fun v -> v :> obj))

let private handleReopen (isJson: bool) (args: ParseResults<PeriodReopenArgs>) : int =
    let isJson = isJson || args.Contains PeriodReopenArgs.Json
    let periodRaw = args.GetResult PeriodReopenArgs.Period
    let reason = args.GetResult PeriodReopenArgs.Reason

    match resolvePeriodId periodRaw with
    | Error errs -> write isJson (Error errs)
    | Ok periodId ->
        let cmd : ReopenFiscalPeriodCommand =
            { fiscalPeriodId = periodId; reason = reason }
        let result = FiscalPeriodService.reopenPeriod cmd
        write isJson (result |> Result.map (fun v -> v :> obj))

let private handleCreate (isJson: bool) (args: ParseResults<PeriodCreateArgs>) : int =
    let isJson = isJson || args.Contains PeriodCreateArgs.Json
    let startRaw = args.GetResult PeriodCreateArgs.Start
    let endRaw = args.GetResult PeriodCreateArgs.End
    let key = args.GetResult PeriodCreateArgs.Key

    match parseDate startRaw, parseDate endRaw with
    | Error e1, Error e2 ->
        write isJson (Error [e1; e2])
    | Error e, _ | _, Error e ->
        write isJson (Error [e])
    | Ok startDate, Ok endDate ->
        let result = FiscalPeriodService.createPeriod key startDate endDate
        write isJson (result |> Result.map (fun v -> v :> obj))

// --- Dispatch ---

let dispatch (isJson: bool) (args: ParseResults<PeriodArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (List listArgs) -> handleList isJson listArgs
    | Some (Close closeArgs) -> handleClose isJson closeArgs
    | Some (Reopen reopenArgs) -> handleReopen isJson reopenArgs
    | Some (Create createArgs) -> handleCreate isJson createArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
```

#### 2d. OutputFormatter.fs additions

**Account detail formatter:**

```fsharp
let private formatAccount (a: Account) : string =
    let subTypeStr =
        match a.subType with
        | Some st -> AccountSubType.toDbString st
        | None -> "(none)"
    let activeStr = if a.isActive then "Active" else "Inactive"
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Account %s -- %s" a.code a.name)
    lines.Add(sprintf "  ID:            %d" a.id)
    lines.Add(sprintf "  Type ID:       %d" a.accountTypeId)
    lines.Add(sprintf "  Sub-Type:      %s" subTypeStr)
    lines.Add(sprintf "  Parent Code:   %s" (a.parentCode |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Status:        %s" activeStr)
    lines.Add(sprintf "  Created:       %s" (a.createdAt.ToString("yyyy-MM-dd HH:mm:ss")))
    lines.Add(sprintf "  Modified:      %s" (a.modifiedAt.ToString("yyyy-MM-dd HH:mm:ss")))
    String.Join(Environment.NewLine, lines)
```

**Account list formatter:**

```fsharp
let private formatAccountList (accounts: Account list) : string =
    if accounts.IsEmpty then "(no accounts found)"
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-10s  %-35s  %-8s  %-8s" "ID" "Code" "Name" "Type ID" "Active")
        lines.Add(sprintf "  %s  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 10 "-")
            (String.replicate 35 "-") (String.replicate 8 "-") (String.replicate 8 "-"))
        for a in accounts do
            let name = if a.name.Length > 35 then a.name.Substring(0, 32) + "..." else a.name
            let active = if a.isActive then "Yes" else "No"
            lines.Add(sprintf "  %-6d  %-10s  %-35s  %-8d  %-8s"
                a.id a.code name a.accountTypeId active)
        String.Join(Environment.NewLine, lines)
```

**Fiscal period detail formatter:**

```fsharp
let private formatFiscalPeriod (fp: FiscalPeriod) : string =
    let statusStr = if fp.isOpen then "Open" else "Closed"
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Fiscal Period %s (ID: %d)" fp.periodKey fp.id)
    lines.Add(sprintf "  Start Date:    %s" (fp.startDate.ToString("yyyy-MM-dd")))
    lines.Add(sprintf "  End Date:      %s" (fp.endDate.ToString("yyyy-MM-dd")))
    lines.Add(sprintf "  Status:        %s" statusStr)
    lines.Add(sprintf "  Created:       %s" (fp.createdAt.ToString("yyyy-MM-dd HH:mm:ss")))
    String.Join(Environment.NewLine, lines)
```

**Fiscal period list formatter:**

```fsharp
let private formatFiscalPeriodList (periods: FiscalPeriod list) : string =
    if periods.IsEmpty then "(no fiscal periods found)"
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-10s  %-12s  %-12s  %-8s" "ID" "Key" "Start" "End" "Status")
        lines.Add(sprintf "  %s  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 10 "-")
            (String.replicate 12 "-") (String.replicate 12 "-") (String.replicate 8 "-"))
        for fp in periods do
            let status = if fp.isOpen then "Open" else "Closed"
            lines.Add(sprintf "  %-6d  %-10s  %-12s  %-12s  %-8s"
                fp.id fp.periodKey
                (fp.startDate.ToString("yyyy-MM-dd"))
                (fp.endDate.ToString("yyyy-MM-dd"))
                status)
        String.Join(Environment.NewLine, lines)
```

**`formatHuman` additions:** Add two new cases before the `| _ ->` fallback:

```fsharp
| :? Account as a -> formatAccount a
| :? FiscalPeriod as fp -> formatFiscalPeriod fp
```

**Dedicated list write functions** (following the `writeInvoiceList`/
`writeTransferList` pattern to avoid F# type erasure):

```fsharp
/// Dedicated write function for Account list.
let writeAccountList (isJson: bool) (accounts: Account list) : int =
    if isJson then
        let output = formatJson accounts
        Console.Out.WriteLine(output)
    else
        let output = formatAccountList accounts
        Console.Out.WriteLine(output)
    ExitCodes.success

/// Dedicated write function for FiscalPeriod list.
let writePeriodList (isJson: bool) (periods: FiscalPeriod list) : int =
    if isJson then
        let output = formatJson periods
        Console.Out.WriteLine(output)
    else
        let output = formatFiscalPeriodList periods
        Console.Out.WriteLine(output)
    ExitCodes.success
```

#### 2e. Program.fs changes

Add to the `LeoBloomArgs` DU:

```fsharp
| [<CliPrefix(CliPrefix.None)>] Account of ParseResults<AccountArgs>
| [<CliPrefix(CliPrefix.None)>] Period of ParseResults<PeriodArgs>
```

Add opens:

```fsharp
open LeoBloom.CLI.AccountCommands
open LeoBloom.CLI.PeriodCommands
```

Add to `Usage`:

```fsharp
| Account _ -> "Account commands (list, show, balance)"
| Period _ -> "Period commands (list, close, reopen, create)"
```

Add dispatch cases:

```fsharp
| Some (Account accountResults) ->
    AccountCommands.dispatch isJson accountResults
| Some (Period periodResults) ->
    PeriodCommands.dispatch isJson periodResults
```

**Verification:** `dotnet build` succeeds. All 7 commands dispatch correctly.
Existing tests still pass.

---

## Acceptance Criteria

- [ ] AC1: `AccountBalanceRepository.listAccounts` returns accounts filtered by type and inactive flag
- [ ] AC2: `AccountBalanceRepository.findAccountById` returns `Some Account` for existing ID, `None` otherwise
- [ ] AC3: `AccountBalanceRepository.findAccountByCode` returns `Some Account` for existing code, `None` otherwise
- [ ] AC4: `FiscalPeriodRepository.listAll` returns all periods ordered by start_date
- [ ] AC5: `FiscalPeriodRepository.findByKey` returns `Some FiscalPeriod` for existing key, `None` otherwise
- [ ] AC6: `FiscalPeriodRepository.create` inserts a row and returns the new `FiscalPeriod` record
- [ ] AC7: `FiscalPeriodService.createPeriod` rejects blank key or start > end
- [ ] AC8: `FiscalPeriodService.createPeriod` returns friendly error on duplicate key
- [ ] AC9: `account list` with no flags returns active accounts, exit 0
- [ ] AC10: `account list --type asset` filters to asset accounts only
- [ ] AC11: `account list --inactive` includes inactive accounts
- [ ] AC12: `account show <id>` / `account show <code>` returns account detail
- [ ] AC13: `account show <nonexistent>` writes error to stderr, exit 1
- [ ] AC14: `account balance <id-or-code>` returns balance (delegates to existing service)
- [ ] AC15: `period list` returns all periods, exit 0
- [ ] AC16: `period close <id>` / `period close <key>` closes period, exit 0
- [ ] AC17: `period reopen <id-or-key> --reason TEXT` reopens period, exit 0
- [ ] AC18: `period reopen` without --reason exits with error (Argu handles this)
- [ ] AC19: `period create --start DATE --end DATE --key TEXT` creates period, exit 0
- [ ] AC20: All commands support `--json` for machine-readable output
- [ ] AC21: All existing tests still pass (no regressions)

## Risks

- **account_subtype nullable read:** The Account record has a `subType: AccountSubType option` field. The new repo functions must handle `NULL` DB values defensively. Mitigation: the `readAccount` helper handles this explicitly.
- **Argu DU naming collisions:** `AccountBalanceArgs` already exists in `ReportCommands.fs`. The new `AccountBalanceCmdArgs` avoids the collision. Builder must be careful with opens.
- **F# compile order:** New CLI files must be listed in the fsproj before `Program.fs` but after `OutputFormatter.fs` (since they reference it). The ordering in the plan is correct.

## Out of Scope

- Account creation/deactivation (migrations, not CLI)
- Account property modification
- Fiscal period deletion
- Period date-overlap validation (see F3 resolution)
- Changes to existing balance calculation or close/reopen logic
- Batch operations
- Deprecating `report account-balance` in favor of `account balance` (both can coexist)
