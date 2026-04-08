module LeoBloom.CLI.TransferCommands

open System
open Argu
open LeoBloom.Domain.Ops
open LeoBloom.Ops
open LeoBloom.Utilities
open LeoBloom.CLI.OutputFormatter

// --- Argu DU definitions for transfer subcommands ---

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

// --- Parsing helpers ---

let private parseDateOnly (raw: string) : Result<DateOnly, string> =
    match DateOnly.TryParse(raw) with
    | true, d -> Ok d
    | false, _ -> Error (sprintf "Invalid date format '%s' -- expected yyyy-MM-dd" raw)

// --- Command handlers ---

let private handleInitiate (isJson: bool) (args: ParseResults<TransferInitiateArgs>) : int =
    let isJson = isJson || args.Contains TransferInitiateArgs.Json
    let fromAccount = args.GetResult TransferInitiateArgs.From_Account
    let toAccount = args.GetResult TransferInitiateArgs.To_Account
    let amount = args.GetResult TransferInitiateArgs.Amount
    let dateRaw = args.GetResult TransferInitiateArgs.Date
    let expectedSettlementRaw = args.TryGetResult TransferInitiateArgs.Expected_Settlement
    let description = args.TryGetResult TransferInitiateArgs.Description

    match parseDateOnly dateRaw with
    | Error e ->
        write isJson (Error [e])
    | Ok initiatedDate ->
        match expectedSettlementRaw with
        | Some esRaw ->
            match parseDateOnly esRaw with
            | Error e ->
                write isJson (Error [e])
            | Ok esDate ->
                use conn = DataSource.openConnection()
                use txn = conn.BeginTransaction()
                try
                    let cmd : InitiateTransferCommand =
                        { fromAccountId = fromAccount
                          toAccountId = toAccount
                          amount = amount
                          initiatedDate = initiatedDate
                          expectedSettlement = Some esDate
                          description = description }
                    let result = TransferService.initiate txn cmd
                    match result with
                    | Ok _ -> txn.Commit()
                    | Error _ -> txn.Rollback()
                    write isJson (result |> Result.map (fun v -> v :> obj))
                with ex ->
                    try txn.Rollback() with _ -> ()
                    reraise()
        | None ->
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()
            try
                let cmd : InitiateTransferCommand =
                    { fromAccountId = fromAccount
                      toAccountId = toAccount
                      amount = amount
                      initiatedDate = initiatedDate
                      expectedSettlement = None
                      description = description }
                let result = TransferService.initiate txn cmd
                match result with
                | Ok _ -> txn.Commit()
                | Error _ -> txn.Rollback()
                write isJson (result |> Result.map (fun v -> v :> obj))
            with ex ->
                try txn.Rollback() with _ -> ()
                reraise()

let private handleConfirm (isJson: bool) (args: ParseResults<TransferConfirmArgs>) : int =
    let isJson = isJson || args.Contains TransferConfirmArgs.Json
    let transferId = args.GetResult TransferConfirmArgs.Transfer_Id
    let dateRaw = args.GetResult TransferConfirmArgs.Date

    match parseDateOnly dateRaw with
    | Error e ->
        write isJson (Error [e])
    | Ok confirmedDate ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let cmd : ConfirmTransferCommand =
                { transferId = transferId
                  confirmedDate = confirmedDate }
            let result = TransferService.confirm txn cmd
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            write isJson (result |> Result.map (fun v -> v :> obj))
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleList (isJson: bool) (args: ParseResults<TransferListArgs>) : int =
    let isJson = isJson || args.Contains TransferListArgs.Json
    let statusRaw = args.TryGetResult TransferListArgs.Status
    let fromRaw = args.TryGetResult TransferListArgs.From
    let toRaw = args.TryGetResult TransferListArgs.To

    // Parse status if provided
    let statusResult =
        match statusRaw with
        | None -> Ok None
        | Some s ->
            match TransferStatus.fromString s with
            | Ok v -> Ok (Some v)
            | Error msg -> Error msg

    // Parse from date if provided
    let fromResult =
        match fromRaw with
        | None -> Ok None
        | Some s ->
            match parseDateOnly s with
            | Ok d -> Ok (Some d)
            | Error msg -> Error msg

    // Parse to date if provided
    let toResult =
        match toRaw with
        | None -> Ok None
        | Some s ->
            match parseDateOnly s with
            | Ok d -> Ok (Some d)
            | Error msg -> Error msg

    match statusResult, fromResult, toResult with
    | Error e, _, _ -> write isJson (Error [e])
    | _, Error e, _ -> write isJson (Error [e])
    | _, _, Error e -> write isJson (Error [e])
    | Ok status, Ok fromDate, Ok toDate ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let filter : ListTransfersFilter =
                { status = status
                  fromDate = fromDate
                  toDate = toDate }
            let transfers = TransferService.list txn filter
            txn.Commit()
            writeTransferList isJson transfers
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleShow (isJson: bool) (args: ParseResults<TransferShowArgs>) : int =
    let isJson = isJson || args.Contains TransferShowArgs.Json
    let transferId = args.GetResult TransferShowArgs.Transfer_Id

    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = TransferService.show txn transferId
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        write isJson (result |> Result.map (fun v -> v :> obj))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

// --- Dispatch ---

let dispatch (isJson: bool) (args: ParseResults<TransferArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (Initiate initiateArgs) -> handleInitiate isJson initiateArgs
    | Some (Confirm confirmArgs) -> handleConfirm isJson confirmArgs
    | Some (List listArgs) -> handleList isJson listArgs
    | Some (Show showArgs) -> handleShow isJson showArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
