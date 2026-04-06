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

// --- Helpers ---

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

// --- Handlers ---

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
