module LeoBloom.CLI.AccountCommands

open System
open Argu
open Npgsql
open LeoBloom.Ledger
open LeoBloom.Utilities
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

// --- Helpers ---

let private resolveAccount (txn: NpgsqlTransaction) (raw: string) =
    match AccountBalanceService.showAccountByCode txn raw with
    | Ok account -> Ok account
    | Error _ ->
        match Int32.TryParse(raw) with
        | true, id -> AccountBalanceService.showAccountById txn id
        | false, _ -> Error (sprintf "Account '%s' does not exist" raw)

let private parseDate (raw: string) : Result<DateOnly, string> =
    match DateOnly.TryParseExact(raw, "yyyy-MM-dd") with
    | true, d -> Ok d
    | false, _ -> Error (sprintf "Invalid date format '%s' -- expected yyyy-MM-dd" raw)

let private validAccountTypes =
    Set.ofList [ "asset"; "liability"; "equity"; "revenue"; "expense" ]

// --- Handlers ---

let private handleList (isJson: bool) (args: ParseResults<AccountListArgs>) : int =
    let isJson = isJson || args.Contains AccountListArgs.Json
    let typeRaw = args.TryGetResult AccountListArgs.Type
    let includeInactive = args.Contains AccountListArgs.Inactive

    match typeRaw with
    | Some t when not (validAccountTypes.Contains (t.ToLowerInvariant())) ->
        write isJson (Error [sprintf "Invalid account type '%s' -- valid types: asset, liability, equity, revenue, expense" t])
    | _ ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let typeName = typeRaw |> Option.map (fun t -> t.ToLowerInvariant())
            let result = AccountBalanceService.listAccounts txn typeName includeInactive
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            match result with
            | Ok accounts -> writeAccountList isJson accounts
            | Error errs -> write isJson (Error errs)
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleShow (isJson: bool) (args: ParseResults<AccountShowArgs>) : int =
    let isJson = isJson || args.Contains AccountShowArgs.Json
    let accountRaw = args.GetResult AccountShowArgs.Account

    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = resolveAccount txn accountRaw
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

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
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result =
                match AccountBalanceService.getBalanceByCode txn accountRaw asOfDate with
                | Ok _ as found -> found
                | Error _ ->
                    match Int32.TryParse(accountRaw) with
                    | true, id -> AccountBalanceService.getBalanceById txn id asOfDate
                    | false, _ -> Error (sprintf "Account '%s' does not exist" accountRaw)
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

// --- Dispatch ---

let dispatch (isJson: bool) (args: ParseResults<AccountArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (List listArgs) -> handleList isJson listArgs
    | Some (Show showArgs) -> handleShow isJson showArgs
    | Some (Balance balanceArgs) -> handleBalance isJson balanceArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
