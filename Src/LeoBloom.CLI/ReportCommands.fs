module LeoBloom.CLI.ReportCommands

open System
open Argu
open LeoBloom.Reporting
open LeoBloom.Ledger
open LeoBloom.CLI.OutputFormatter

// --- Argu DU definitions for report subcommands ---

type ScheduleEArgs =
    | [<Mandatory>] Year of int
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Year _ -> "Tax year (e.g. 2026)"

type GeneralLedgerArgs =
    | [<Mandatory>] Account of string
    | [<Mandatory>] From of string
    | [<Mandatory>] To of string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Account _ -> "Account code (e.g. 1110)"
            | From _ -> "Start date (yyyy-MM-dd)"
            | To _ -> "End date (yyyy-MM-dd)"

type CashReceiptsArgs =
    | [<Mandatory>] From of string
    | [<Mandatory>] To of string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | From _ -> "Start date (yyyy-MM-dd)"
            | To _ -> "End date (yyyy-MM-dd)"

type CashDisbursementsArgs =
    | [<Mandatory>] From of string
    | [<Mandatory>] To of string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | From _ -> "Start date (yyyy-MM-dd)"
            | To _ -> "End date (yyyy-MM-dd)"

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

type ReportArgs =
    | [<CliPrefix(CliPrefix.None)>] Schedule_E of ParseResults<ScheduleEArgs>
    | [<CliPrefix(CliPrefix.None)>] General_Ledger of ParseResults<GeneralLedgerArgs>
    | [<CliPrefix(CliPrefix.None)>] Cash_Receipts of ParseResults<CashReceiptsArgs>
    | [<CliPrefix(CliPrefix.None)>] Cash_Disbursements of ParseResults<CashDisbursementsArgs>
    | [<CliPrefix(CliPrefix.None)>] Trial_Balance of ParseResults<TrialBalanceArgs>
    | [<CliPrefix(CliPrefix.None)>] Balance_Sheet of ParseResults<BalanceSheetArgs>
    | [<CliPrefix(CliPrefix.None)>] Income_Statement of ParseResults<IncomeStatementArgs>
    | [<CliPrefix(CliPrefix.None)>] Pnl_Subtree of ParseResults<PnlSubtreeArgs>
    | [<CliPrefix(CliPrefix.None)>] Account_Balance of ParseResults<AccountBalanceArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Schedule_E _ -> "Generate IRS Schedule E report"
            | General_Ledger _ -> "General ledger detail for a single account"
            | Cash_Receipts _ -> "Cash receipts (money in) report"
            | Cash_Disbursements _ -> "Cash disbursements (money out) report"
            | Trial_Balance _ -> "Trial balance for a fiscal period"
            | Balance_Sheet _ -> "Balance sheet as of a date"
            | Income_Statement _ -> "Income statement for a fiscal period"
            | Pnl_Subtree _ -> "P&L subtree for an account and period"
            | Account_Balance _ -> "Account balance as of a date"

// --- Date parsing helper ---

let private parseDate (raw: string) : Result<DateOnly, string> =
    match DateOnly.TryParseExact(raw, "yyyy-MM-dd") with
    | true, d -> Ok d
    | false, _ -> Error (sprintf "Invalid date format '%s' -- expected yyyy-MM-dd" raw)

// --- Command handlers ---

let private handleScheduleE (args: ParseResults<ScheduleEArgs>) : int =
    let year = args.GetResult ScheduleEArgs.Year
    let result = ScheduleEService.generate year
    writeHuman result

let private handleGeneralLedger (args: ParseResults<GeneralLedgerArgs>) : int =
    let accountCode = args.GetResult GeneralLedgerArgs.Account
    let fromRaw = args.GetResult GeneralLedgerArgs.From
    let toRaw = args.GetResult GeneralLedgerArgs.To

    match parseDate fromRaw, parseDate toRaw with
    | Error e1, Error e2 ->
        writeHumanErrors [ e1; e2 ]
    | Error e, _ | _, Error e ->
        writeHumanErrors [ e ]
    | Ok fromDate, Ok toDate ->
        let result = GeneralLedgerReportService.generate accountCode fromDate toDate
        writeHuman result

let private handleCashReceipts (args: ParseResults<CashReceiptsArgs>) : int =
    let fromRaw = args.GetResult CashReceiptsArgs.From
    let toRaw = args.GetResult CashReceiptsArgs.To

    match parseDate fromRaw, parseDate toRaw with
    | Error e1, Error e2 ->
        writeHumanErrors [ e1; e2 ]
    | Error e, _ | _, Error e ->
        writeHumanErrors [ e ]
    | Ok fromDate, Ok toDate ->
        let result = CashFlowReportService.getReceipts fromDate toDate
        writeHuman result

let private handleCashDisbursements (args: ParseResults<CashDisbursementsArgs>) : int =
    let fromRaw = args.GetResult CashDisbursementsArgs.From
    let toRaw = args.GetResult CashDisbursementsArgs.To

    match parseDate fromRaw, parseDate toRaw with
    | Error e1, Error e2 ->
        writeHumanErrors [ e1; e2 ]
    | Error e, _ | _, Error e ->
        writeHumanErrors [ e ]
    | Ok fromDate, Ok toDate ->
        let result = CashFlowReportService.getDisbursements fromDate toDate
        writeHuman result

// --- Period argument parsing helper ---

let private parsePeriodArg (raw: string) : Choice<int, string> =
    match Int32.TryParse(raw) with
    | true, id -> Choice1Of2 id
    | false, _ -> Choice2Of2 raw

// --- New accounting report handlers ---

let private handleTrialBalance (args: ParseResults<TrialBalanceArgs>) : int =
    let isJson = args.Contains TrialBalanceArgs.Json
    let periodRaw = args.GetResult TrialBalanceArgs.Period

    let result =
        match parsePeriodArg periodRaw with
        | Choice1Of2 id -> TrialBalanceService.getByPeriodId id
        | Choice2Of2 key -> TrialBalanceService.getByPeriodKey key

    write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))

let private handleBalanceSheet (args: ParseResults<BalanceSheetArgs>) : int =
    let isJson = args.Contains BalanceSheetArgs.Json
    let asOfRaw = args.GetResult BalanceSheetArgs.As_Of

    match parseDate asOfRaw with
    | Error e ->
        write isJson (Error [e])
    | Ok asOfDate ->
        let result = BalanceSheetService.getAsOfDate asOfDate
        write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))

let private handleIncomeStatement (args: ParseResults<IncomeStatementArgs>) : int =
    let isJson = args.Contains IncomeStatementArgs.Json
    let periodRaw = args.GetResult IncomeStatementArgs.Period

    let result =
        match parsePeriodArg periodRaw with
        | Choice1Of2 id -> IncomeStatementService.getByPeriodId id
        | Choice2Of2 key -> IncomeStatementService.getByPeriodKey key

    write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))

let private handlePnlSubtree (args: ParseResults<PnlSubtreeArgs>) : int =
    let isJson = args.Contains PnlSubtreeArgs.Json
    let account = args.GetResult PnlSubtreeArgs.Account
    let periodRaw = args.GetResult PnlSubtreeArgs.Period

    let result =
        match parsePeriodArg periodRaw with
        | Choice1Of2 id -> SubtreePLService.getByAccountCodeAndPeriodId account id
        | Choice2Of2 key -> SubtreePLService.getByAccountCodeAndPeriodKey account key

    write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))

let private handleAccountBalance (args: ParseResults<AccountBalanceArgs>) : int =
    let isJson = args.Contains AccountBalanceArgs.Json
    let account = args.GetResult AccountBalanceArgs.Account
    let asOfRaw = args.TryGetResult AccountBalanceArgs.As_Of

    let asOfResult =
        match asOfRaw with
        | None -> Ok (DateOnly.FromDateTime(DateTime.Today))
        | Some raw -> parseDate raw

    match asOfResult with
    | Error e ->
        write isJson (Error [e])
    | Ok asOfDate ->
        let result = AccountBalanceService.getBalanceByCode account asOfDate
        write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))

// --- Dispatch ---

let dispatch (args: ParseResults<ReportArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (Schedule_E scheduleEArgs) -> handleScheduleE scheduleEArgs
    | Some (General_Ledger glArgs) -> handleGeneralLedger glArgs
    | Some (Cash_Receipts crArgs) -> handleCashReceipts crArgs
    | Some (Cash_Disbursements cdArgs) -> handleCashDisbursements cdArgs
    | Some (Trial_Balance tbArgs) -> handleTrialBalance tbArgs
    | Some (Balance_Sheet bsArgs) -> handleBalanceSheet bsArgs
    | Some (Income_Statement isArgs) -> handleIncomeStatement isArgs
    | Some (Pnl_Subtree plArgs) -> handlePnlSubtree plArgs
    | Some (Account_Balance abArgs) -> handleAccountBalance abArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
