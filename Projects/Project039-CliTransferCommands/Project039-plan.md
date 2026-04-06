# Project 039 — CLI Transfer Commands: Plan

## Objective

Expose existing transfer operations (initiate, confirm) through the CLI and
add new show/list capabilities, following the exact patterns established by
InvoiceCommands (P042). This requires additions at three layers: repository
(list query), service (show/list wrappers), and CLI (commands + formatting).

## Phases

### Phase 1: Repository — Add `list` and `ListTransfersFilter`

**What:** Add a filter type and list function to `TransferRepository.fs`.

**File:** `Src/LeoBloom.Ops/TransferRepository.fs` (modify)

**Add the filter type** above the `TransferRepository` module (same pattern
as `ListInvoicesFilter` in `InvoiceRepository.fs`):

```fsharp
type ListTransfersFilter =
    { status: TransferStatus option
      fromDate: DateOnly option
      toDate: DateOnly option }
```

**Add `list` function** at the bottom of the `TransferRepository` module.
Signature:

```fsharp
let list (txn: NpgsqlTransaction) (filter: ListTransfersFilter) : Transfer list
```

Implementation details:
- Start with `clauses = [ "is_active = true" ]` (same as invoice pattern)
- `filter.status` -> add clause `status = @status`, param is
  `TransferStatus.toString status` (it's stored as a string in the DB)
- `filter.fromDate` -> add clause `initiated_date >= @from_date`
- `filter.toDate` -> add clause `initiated_date <= @to_date`
- `ORDER BY id DESC`
- Uses existing `mapReader` and `selectColumns`

**Verification:** Project builds. Existing tests pass.

---

### Phase 2: Service — Add `show` and `list` to `TransferService`

**What:** Add two thin wrapper functions to `TransferService.fs`.

**File:** `Src/LeoBloom.Ops/TransferService.fs` (modify)

**Add `show` function** (follows `InvoiceService.showInvoice` exactly):

```fsharp
let show (id: int) : Result<Transfer, string list> =
    Log.info "Showing transfer {TransferId}" [| id :> obj |]
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let transfer = TransferRepository.findById txn id
        txn.Commit()
        match transfer with
        | None ->
            Error [ sprintf "Transfer with id %d does not exist" id ]
        | Some t ->
            Ok t
    with ex ->
        Log.errorExn ex "Failed to show transfer {TransferId}" [| id :> obj |]
        try txn.Rollback() with _ -> ()
        Error [ sprintf "Persistence error: %s" ex.Message ]
```

**Add `list` function** (follows `InvoiceService.listInvoices` exactly):

```fsharp
let list (filter: ListTransfersFilter) : Transfer list =
    Log.info "Listing transfers" [||]
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = TransferRepository.list txn filter
        txn.Commit()
        result
    with ex ->
        Log.errorExn ex "Failed to list transfers" [||]
        try txn.Rollback() with _ -> ()
        []
```

Note: `TransferService.fs` must add `open` for the `ListTransfersFilter`
type. Since it's in the same `LeoBloom.Ops` namespace as the repository,
no additional open is needed -- same as how `InvoiceService` uses
`ListInvoicesFilter` without an extra open.

**Verification:** Project builds. Existing tests pass.

---

### Phase 3: Output Formatting

**What:** Add transfer formatting functions to `OutputFormatter.fs`.

**File:** `Src/LeoBloom.CLI/OutputFormatter.fs` (modify)

**Add `formatTransfer`** (private, follows `formatInvoice` pattern):

```fsharp
let private formatTransfer (t: Transfer) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Transfer #%d" t.id)
    lines.Add(sprintf "  From Account:   %d" t.fromAccountId)
    lines.Add(sprintf "  To Account:     %d" t.toAccountId)
    lines.Add(sprintf "  Amount:         %M" t.amount)
    lines.Add(sprintf "  Status:         %s" (TransferStatus.toString t.status))
    lines.Add(sprintf "  Initiated:      %s" (t.initiatedDate.ToString("yyyy-MM-dd")))
    lines.Add(sprintf "  Exp. Settle:    %s" (t.expectedSettlement |> Option.map (fun d -> d.ToString("yyyy-MM-dd")) |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Confirmed:      %s" (t.confirmedDate |> Option.map (fun d -> d.ToString("yyyy-MM-dd")) |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Journal Entry:  %s" (t.journalEntryId |> Option.map string |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Description:    %s" (t.description |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Created:        %s" (t.createdAt.ToString("yyyy-MM-dd HH:mm:ss")))
    lines.Add(sprintf "  Modified:       %s" (t.modifiedAt.ToString("yyyy-MM-dd HH:mm:ss")))
    String.Join(Environment.NewLine, lines)
```

**Add `formatTransferList`** (private, table format):

```fsharp
let private formatTransferList (transfers: Transfer list) : string =
    if transfers.IsEmpty then ""
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-10s  %-8s  %-8s  %12s  %-12s" "ID" "Status" "From" "To" "Amount" "Initiated")
        lines.Add(sprintf "  %s  %s  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 10 "-") (String.replicate 8 "-")
            (String.replicate 8 "-") (String.replicate 12 "-") (String.replicate 12 "-"))
        for t in transfers do
            lines.Add(sprintf "  %-6d  %-10s  %-8d  %-8d  %12s  %-12s"
                t.id (TransferStatus.toString t.status) t.fromAccountId t.toAccountId
                (sprintf "%M" t.amount) (t.initiatedDate.ToString("yyyy-MM-dd")))
        String.Join(Environment.NewLine, lines)
```

**Add `writeTransferList`** (public, follows `writeInvoiceList` pattern):

```fsharp
let writeTransferList (isJson: bool) (transfers: Transfer list) : int =
    if isJson then
        let output = formatJson transfers
        Console.Out.WriteLine(output)
    else
        let output = formatTransferList transfers
        if not (String.IsNullOrEmpty output) then
            Console.Out.WriteLine(output)
    ExitCodes.success
```

**Update `formatHuman`** -- add a `Transfer` match case before the
wildcard:

```fsharp
    | :? Transfer as t -> formatTransfer t
```

Place it after the `Invoice` case, before `| _ ->`.

**Verification:** Project builds.

---

### Phase 4: CLI Commands — `TransferCommands.fs`

**What:** Create the CLI command module.

**File:** `Src/LeoBloom.CLI/TransferCommands.fs` (create)

Full structure:

```fsharp
module LeoBloom.CLI.TransferCommands

open System
open Argu
open LeoBloom.Domain.Ops
open LeoBloom.Ops
open LeoBloom.CLI.OutputFormatter
```

**Argu DU types:**

```fsharp
type TransferInitiateArgs =
    | [<Mandatory>] From_Account of int
    | [<Mandatory>] To_Account of int
    | [<Mandatory>] Amount of decimal
    | [<Mandatory>] Date of string
    | Expected_Settlement of string
    | Description of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | From_Account _ -> "Source account ID"
            | To_Account _ -> "Destination account ID"
            | Amount _ -> "Transfer amount"
            | Date _ -> "Initiation date (yyyy-MM-dd)"
            | Expected_Settlement _ -> "Expected settlement date (yyyy-MM-dd, optional)"
            | Description _ -> "Transfer description (optional)"
            | Json -> "Output in JSON format"

type TransferConfirmArgs =
    | [<MainCommand; Mandatory>] Transfer_Id of int
    | [<Mandatory>] Date of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Transfer_Id _ -> "Transfer ID to confirm"
            | Date _ -> "Confirmation date (yyyy-MM-dd)"
            | Json -> "Output in JSON format"

type TransferListArgs =
    | Status of string
    | From of string
    | To of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Status _ -> "Filter by status (initiated, confirmed)"
            | From _ -> "Filter by initiated date from (yyyy-MM-dd, inclusive)"
            | To _ -> "Filter by initiated date to (yyyy-MM-dd, inclusive)"
            | Json -> "Output in JSON format"

type TransferShowArgs =
    | [<MainCommand; Mandatory>] Transfer_Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Transfer_Id _ -> "Transfer ID to display"
            | Json -> "Output in JSON format"

type TransferArgs =
    | [<CliPrefix(CliPrefix.None)>] Initiate of ParseResults<TransferInitiateArgs>
    | [<CliPrefix(CliPrefix.None)>] Confirm of ParseResults<TransferConfirmArgs>
    | [<CliPrefix(CliPrefix.None)>] List of ParseResults<TransferListArgs>
    | [<CliPrefix(CliPrefix.None)>] Show of ParseResults<TransferShowArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Initiate _ -> "Initiate a new transfer"
            | Confirm _ -> "Confirm a pending transfer"
            | List _ -> "List transfers with optional filters"
            | Show _ -> "Show a transfer by ID"
```

**Parsing helpers:**

```fsharp
let private parseDateOnly (raw: string) : Result<DateOnly, string> =
    match DateOnly.TryParse(raw) with
    | true, d -> Ok d
    | false, _ -> Error (sprintf "Invalid date format '%s' -- expected yyyy-MM-dd" raw)
```

**Command handlers:**

`handleInitiate` -- parse args, build `InitiateTransferCommand`, call
`TransferService.initiate`, format via `write`. Must parse `Date` and
optional `Expected_Settlement` through `parseDateOnly`. On any parse
failure, return `write isJson (Error [msg])`.

`handleConfirm` -- parse `Transfer_Id` and `Date`, build
`ConfirmTransferCommand`, call `TransferService.confirm`, format via `write`.

`handleList` -- parse optional `Status`, `From`, `To`. Status parsing
uses `TransferStatus.fromString`; on failure, return error. Date parsing
uses `parseDateOnly`. Build `ListTransfersFilter`, call
`TransferService.list`, format via `writeTransferList`.

`handleShow` -- parse `Transfer_Id`, call `TransferService.show`, format
via `write`.

**Dispatch:**

```fsharp
let dispatch (isJson: bool) (args: ParseResults<TransferArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (Initiate initiateArgs) -> handleInitiate isJson initiateArgs
    | Some (Confirm confirmArgs) -> handleConfirm isJson confirmArgs
    | Some (List listArgs) -> handleList isJson listArgs
    | Some (Show showArgs) -> handleShow isJson showArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
```

**Verification:** Project builds.

---

### Phase 5: Wire into Program.fs and fsproj

**What:** Add TransferCommands to top-level dispatch and project file.

**File:** `Src/LeoBloom.CLI/Program.fs` (modify)

Add open:
```fsharp
open LeoBloom.CLI.TransferCommands
```

Add DU case to `LeoBloomArgs`:
```fsharp
    | [<CliPrefix(CliPrefix.None)>] Transfer of ParseResults<TransferArgs>
```

With usage: `"Transfer commands (initiate, confirm, show, list)"`

Add dispatch case:
```fsharp
            | Some (Transfer transferResults) ->
                TransferCommands.dispatch isJson transferResults
```

**File:** `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` (modify)

Add `TransferCommands.fs` to the `<ItemGroup>` compile list, after
`InvoiceCommands.fs` and before `Program.fs`:

```xml
    <Compile Include="TransferCommands.fs" />
```

F# compile order matters. `TransferCommands.fs` depends on
`OutputFormatter.fs` (already listed above) and must appear before
`Program.fs` (which dispatches to it).

**Verification:** `dotnet build` succeeds with no warnings. All existing
tests pass.

---

## Acceptance Criteria

From PO kickoff, mapped to implementation:

- [ ] B1: `transfer initiate` with valid args -> exit 0, prints transfer (Phase 4 handleInitiate)
- [ ] B2: `transfer initiate` with missing args -> exit 2 (Argu parse error, automatic)
- [ ] B3: `transfer confirm <id> --date DATE` -> exit 0, prints confirmed transfer (Phase 4 handleConfirm)
- [ ] B4: `transfer confirm` with bad args -> nonzero exit (Argu + service errors)
- [ ] B5: `transfer list` no filters -> all active transfers, exit 0 (Phase 1 list, Phase 4 handleList)
- [ ] B6: `transfer list --status initiated` -> filtered correctly (Phase 1 status filter)
- [ ] B7: `transfer list --from DATE --to DATE` -> filters on initiated_date (Phase 1 date filter)
- [ ] B8: `transfer show <id>` existing -> exit 0, prints detail (Phase 2 show, Phase 4 handleShow)
- [ ] B9: `transfer show <id>` nonexistent -> exit 1 with error (Phase 2 show returns Error)
- [ ] B10: All subcommands support `--json` (Phase 3 + Phase 4 isJson plumbing)
- [ ] B11: `transfer` with no subcommand -> stderr usage, exit 2 (Phase 4 dispatch None case)
- [ ] S1: `TransferCommands.fs` exists and is in fsproj (Phase 4 + Phase 5)
- [ ] S2: `TransferCommands.dispatch` wired into `Program.fs` (Phase 5)
- [ ] S3: `TransferRepository.list` filters by status + date range (Phase 1)
- [ ] S4: `TransferService.show` and `TransferService.list` exist (Phase 2)
- [ ] S5: `ListTransfersFilter` type exists (Phase 1)
- [ ] S6: `formatTransfer`, `formatTransferList`, `writeTransferList` exist (Phase 3)
- [ ] S7: `formatHuman` dispatches on `Transfer` (Phase 3)
- [ ] S8: All existing tests pass (Phase 5 verification)
- [ ] S9: Project builds with no warnings (Phase 5 verification)

## Risks

- **F# compile order in fsproj:** `TransferCommands.fs` must be listed
  after `OutputFormatter.fs` and before `Program.fs`. If the builder gets
  this wrong, the build will fail with clear errors. Low risk.
- **TransferStatus.fromString returns Result:** The `--status` flag parsing
  in `handleList` must handle the `Error` case from `fromString`. The
  builder should return a business error (exit 1) with the error message
  from `fromString` if the user passes an invalid status string.

## Out of Scope

- Transfer business logic changes (initiate/confirm validation unchanged)
- Voiding or cancelling transfers
- Obligation CLI commands (separate backlog items)
- New validation rules
- Database migrations (no schema changes needed)
