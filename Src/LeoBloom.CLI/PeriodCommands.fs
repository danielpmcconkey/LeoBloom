module LeoBloom.CLI.PeriodCommands

open System
open Argu
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Ledger
open LeoBloom.Utilities
open LeoBloom.CLI.OutputFormatter
open LeoBloom.CLI.CliHelpers

// --- Argu DU definitions ---

type PeriodListArgs =
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Json -> "Output in JSON format"

type PeriodCloseArgs =
    | [<MainCommand; Mandatory>] Period of string
    | Actor of string
    | Note of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Period _ -> "Fiscal period ID or period key"
            | Actor _ -> "Actor identifier (default: dan)"
            | Note _  -> "Optional note for the close action"
            | Json -> "Output in JSON format"

type PeriodReopenArgs =
    | [<MainCommand; Mandatory>] Period of string
    | [<Mandatory>] Reason of string
    | Actor of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Period _ -> "Fiscal period ID or period key"
            | Reason _ -> "Reason for reopening the period"
            | Actor _ -> "Actor identifier (default: dan)"
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

type PeriodAuditArgs =
    | [<MainCommand; Mandatory>] Period of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Period _ -> "Fiscal period ID or period key"
            | Json -> "Output in JSON format"

type PeriodArgs =
    | [<CliPrefix(CliPrefix.None)>] List of ParseResults<PeriodListArgs>
    | [<CliPrefix(CliPrefix.None)>] Close of ParseResults<PeriodCloseArgs>
    | [<CliPrefix(CliPrefix.None)>] Reopen of ParseResults<PeriodReopenArgs>
    | [<CliPrefix(CliPrefix.None)>] Create of ParseResults<PeriodCreateArgs>
    | [<CliPrefix(CliPrefix.None)>] Audit of ParseResults<PeriodAuditArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | List _ -> "List all fiscal periods"
            | Close _ -> "Close a fiscal period"
            | Reopen _ -> "Reopen a closed fiscal period"
            | Create _ -> "Create a new fiscal period"
            | Audit _ -> "List close/reopen audit trail for a fiscal period"

// --- Helpers ---

/// Resolves a period arg (id or key) to a period ID.
/// On key input, calls findPeriodByKey. On id input, passes through.
let private resolvePeriodId (txn: NpgsqlTransaction) (raw: string) : Result<int, string list> =
    match parsePeriodArg raw with
    | Choice1Of2 id -> Ok id
    | Choice2Of2 key ->
        match FiscalPeriodService.findPeriodByKey txn key with
        | Ok period -> Ok period.id
        | Error errs -> Error errs

// --- Handlers ---

let private handleList (isJson: bool) (args: ParseResults<PeriodListArgs>) : int =
    let isJson = isJson || args.Contains PeriodListArgs.Json
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = FiscalPeriodService.listPeriods txn ()
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        match result with
        | Ok periods -> writePeriodList isJson periods
        | Error errs -> write isJson (Error errs)
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private handleClose (isJson: bool) (args: ParseResults<PeriodCloseArgs>) : int =
    let isJson = isJson || args.Contains PeriodCloseArgs.Json
    let periodRaw = args.GetResult PeriodCloseArgs.Period
    let actor = args.TryGetResult PeriodCloseArgs.Actor |> Option.defaultValue "dan"
    let note = args.TryGetResult PeriodCloseArgs.Note

    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        match resolvePeriodId txn periodRaw with
        | Error errs ->
            txn.Rollback()
            write isJson (Error errs)
        | Ok periodId ->
            let cmd : CloseFiscalPeriodCommand =
                { fiscalPeriodId = periodId; actor = actor; note = note }
            let result = FiscalPeriodService.closePeriod txn cmd
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            write isJson (result |> Result.map (fun v -> v :> obj))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private handleReopen (isJson: bool) (args: ParseResults<PeriodReopenArgs>) : int =
    let isJson = isJson || args.Contains PeriodReopenArgs.Json
    let periodRaw = args.GetResult PeriodReopenArgs.Period
    let reason = args.GetResult PeriodReopenArgs.Reason
    let actor = args.TryGetResult PeriodReopenArgs.Actor |> Option.defaultValue "dan"

    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        match resolvePeriodId txn periodRaw with
        | Error errs ->
            txn.Rollback()
            write isJson (Error errs)
        | Ok periodId ->
            let cmd : ReopenFiscalPeriodCommand =
                { fiscalPeriodId = periodId; reason = reason; actor = actor }
            let result = FiscalPeriodService.reopenPeriod txn cmd
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            write isJson (result |> Result.map (fun v -> v :> obj))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

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
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = FiscalPeriodService.createPeriod txn key startDate endDate
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            write isJson (result |> Result.map (fun v -> v :> obj))
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleAudit (isJson: bool) (args: ParseResults<PeriodAuditArgs>) : int =
    let isJson = isJson || args.Contains PeriodAuditArgs.Json
    let periodRaw = args.GetResult PeriodAuditArgs.Period

    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        match resolvePeriodId txn periodRaw with
        | Error errs ->
            txn.Rollback()
            write isJson (Error errs)
        | Ok periodId ->
            let result = FiscalPeriodService.listAudit txn periodId
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            match result with
            | Ok entries -> writeAuditList isJson entries
            | Error errs -> write isJson (Error errs)
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

// --- Dispatch ---

let dispatch (isJson: bool) (args: ParseResults<PeriodArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (List listArgs) -> handleList isJson listArgs
    | Some (Close closeArgs) -> handleClose isJson closeArgs
    | Some (Reopen reopenArgs) -> handleReopen isJson reopenArgs
    | Some (Create createArgs) -> handleCreate isJson createArgs
    | Some (Audit auditArgs) -> handleAudit isJson auditArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
