module LeoBloom.CLI.ReportCommands

open System
open Argu
open LeoBloom.Reporting
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

type ReportArgs =
    | [<CliPrefix(CliPrefix.None)>] Schedule_E of ParseResults<ScheduleEArgs>
    | [<CliPrefix(CliPrefix.None)>] General_Ledger of ParseResults<GeneralLedgerArgs>
    | [<CliPrefix(CliPrefix.None)>] Cash_Receipts of ParseResults<CashReceiptsArgs>
    | [<CliPrefix(CliPrefix.None)>] Cash_Disbursements of ParseResults<CashDisbursementsArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Schedule_E _ -> "Generate IRS Schedule E report"
            | General_Ledger _ -> "General ledger detail for a single account"
            | Cash_Receipts _ -> "Cash receipts (money in) report"
            | Cash_Disbursements _ -> "Cash disbursements (money out) report"

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

// --- Dispatch ---

let dispatch (args: ParseResults<ReportArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (Schedule_E scheduleEArgs) -> handleScheduleE scheduleEArgs
    | Some (General_Ledger glArgs) -> handleGeneralLedger glArgs
    | Some (Cash_Receipts crArgs) -> handleCashReceipts crArgs
    | Some (Cash_Disbursements cdArgs) -> handleCashDisbursements cdArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
