module LeoBloom.CLI.ExtractCommands

open System
open Argu
open LeoBloom.Reporting.ExtractRepository
open LeoBloom.Utilities
open LeoBloom.CLI.OutputFormatter
open LeoBloom.CLI.CliHelpers

// --- Argu DU definitions for extract subcommands ---

type ExtractAccountTreeArgs =
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Json -> "Output in JSON format (default; flag accepted for consistency)"

type ExtractBalancesArgs =
    | [<Mandatory>] As_Of of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | As_Of _ -> "As-of date (yyyy-MM-dd)"
            | Json -> "Output in JSON format (default; flag accepted for consistency)"

type ExtractPositionsArgs =
    | As_Of of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | As_Of _ -> "As-of date (yyyy-MM-dd, defaults to today)"
            | Json -> "Output in JSON format (default; flag accepted for consistency)"

type ExtractJeLinesArgs =
    | [<Mandatory>] Fiscal_Period_Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Fiscal_Period_Id _ -> "Fiscal period ID"
            | Json -> "Output in JSON format (default; flag accepted for consistency)"

type ExtractArgs =
    | [<CliPrefix(CliPrefix.None)>] Account_Tree of ParseResults<ExtractAccountTreeArgs>
    | [<CliPrefix(CliPrefix.None)>] Balances of ParseResults<ExtractBalancesArgs>
    | [<CliPrefix(CliPrefix.None)>] Positions of ParseResults<ExtractPositionsArgs>
    | [<CliPrefix(CliPrefix.None)>] Je_Lines of ParseResults<ExtractJeLinesArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Account_Tree _ -> "Extract full account tree as JSON"
            | Balances _ -> "Extract account balances as of a date as JSON"
            | Positions _ -> "Extract portfolio positions as of a date as JSON"
            | Je_Lines _ -> "Extract journal entry lines for a fiscal period as JSON"

// --- Command handlers ---

let private handleAccountTree (_args: ParseResults<ExtractAccountTreeArgs>) : int =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let rows = getAccountTree txn
        txn.Commit()
        Console.Out.WriteLine(formatJson (box rows))
        ExitCodes.success
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private handleBalances (_isJson: bool) (args: ParseResults<ExtractBalancesArgs>) : int =
    let asOfRaw = args.GetResult ExtractBalancesArgs.As_Of
    match parseDate asOfRaw with
    | Error msg ->
        Console.Error.WriteLine(sprintf "Error: %s" msg)
        ExitCodes.businessError
    | Ok asOf ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let rows = getBalances txn asOf
            txn.Commit()
            Console.Out.WriteLine(formatJson (box rows))
            ExitCodes.success
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handlePositions (_isJson: bool) (args: ParseResults<ExtractPositionsArgs>) : int =
    let asOfResult =
        match args.TryGetResult ExtractPositionsArgs.As_Of with
        | None -> Ok (DateOnly.FromDateTime(DateTime.Today))
        | Some raw -> parseDate raw
    match asOfResult with
    | Error msg ->
        Console.Error.WriteLine(sprintf "Error: %s" msg)
        ExitCodes.businessError
    | Ok asOf ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let rows = getPositions txn asOf
            txn.Commit()
            Console.Out.WriteLine(formatJson (box rows))
            ExitCodes.success
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleJeLines (_isJson: bool) (args: ParseResults<ExtractJeLinesArgs>) : int =
    let fiscalPeriodId = args.GetResult ExtractJeLinesArgs.Fiscal_Period_Id
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let rows = getJournalEntryLines txn fiscalPeriodId
        txn.Commit()
        Console.Out.WriteLine(formatJson (box rows))
        ExitCodes.success
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

// --- Dispatch ---

let dispatch (isJson: bool) (args: ParseResults<ExtractArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (Account_Tree atArgs) -> handleAccountTree atArgs
    | Some (Balances balArgs) -> handleBalances isJson balArgs
    | Some (Positions posArgs) -> handlePositions isJson posArgs
    | Some (Je_Lines jlArgs) -> handleJeLines isJson jlArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
