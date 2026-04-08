module LeoBloom.CLI.ReportCommands

open System
open Argu
open LeoBloom.Reporting
open LeoBloom.Ledger
open LeoBloom.Ops
open LeoBloom.Portfolio
open LeoBloom.Utilities
open LeoBloom.CLI.OutputFormatter
open LeoBloom.CLI.CliHelpers

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

type ProjectionArgs =
    | [<Mandatory>] Account of string
    | [<Mandatory>] Through of string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Account _ -> "Account code (e.g. 1110)"
            | Through _ -> "Projection end date (yyyy-MM-dd, must be in the future)"

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
    | [<CliPrefix(CliPrefix.None)>] Projection of ParseResults<ProjectionArgs>
    | [<CliPrefix(CliPrefix.None)>] Allocation of ParseResults<PortfolioReportCommands.AllocationArgs>
    | [<CliPrefix(CliPrefix.None)>] Portfolio_Summary of ParseResults<PortfolioReportCommands.PortfolioSummaryArgs>
    | [<CliPrefix(CliPrefix.None)>] Portfolio_History of ParseResults<PortfolioReportCommands.PortfolioHistoryArgs>
    | [<CliPrefix(CliPrefix.None)>] Gains of ParseResults<PortfolioReportCommands.GainsArgs>
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
            | Projection _ -> "Balance projection through a future date"
            | Allocation _        -> "Allocation breakdown by dimension"
            | Portfolio_Summary _ -> "Portfolio value and gain/loss summary"
            | Portfolio_History _ -> "Historical portfolio value time-series"
            | Gains _             -> "Per-fund unrealized gain/loss report"

// --- Command handlers ---

let private handleScheduleE (args: ParseResults<ScheduleEArgs>) : int =
    let year = args.GetResult ScheduleEArgs.Year
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = ScheduleEService.generate txn year
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        writeHuman result
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

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
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = GeneralLedgerReportService.generate txn accountCode fromDate toDate
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            writeHuman result
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleCashReceipts (args: ParseResults<CashReceiptsArgs>) : int =
    let fromRaw = args.GetResult CashReceiptsArgs.From
    let toRaw = args.GetResult CashReceiptsArgs.To

    match parseDate fromRaw, parseDate toRaw with
    | Error e1, Error e2 ->
        writeHumanErrors [ e1; e2 ]
    | Error e, _ | _, Error e ->
        writeHumanErrors [ e ]
    | Ok fromDate, Ok toDate ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = CashFlowReportService.getReceipts txn fromDate toDate
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            writeHuman result
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleCashDisbursements (args: ParseResults<CashDisbursementsArgs>) : int =
    let fromRaw = args.GetResult CashDisbursementsArgs.From
    let toRaw = args.GetResult CashDisbursementsArgs.To

    match parseDate fromRaw, parseDate toRaw with
    | Error e1, Error e2 ->
        writeHumanErrors [ e1; e2 ]
    | Error e, _ | _, Error e ->
        writeHumanErrors [ e ]
    | Ok fromDate, Ok toDate ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = CashFlowReportService.getDisbursements txn fromDate toDate
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            writeHuman result
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

// --- New accounting report handlers ---

let private handleTrialBalance (args: ParseResults<TrialBalanceArgs>) : int =
    let isJson = args.Contains TrialBalanceArgs.Json
    let periodRaw = args.GetResult TrialBalanceArgs.Period

    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result =
            match parsePeriodArg periodRaw with
            | Choice1Of2 id -> TrialBalanceService.getByPeriodId txn id
            | Choice2Of2 key -> TrialBalanceService.getByPeriodKey txn key
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private handleBalanceSheet (args: ParseResults<BalanceSheetArgs>) : int =
    let isJson = args.Contains BalanceSheetArgs.Json
    let asOfRaw = args.GetResult BalanceSheetArgs.As_Of

    match parseDate asOfRaw with
    | Error e ->
        write isJson (Error [e])
    | Ok asOfDate ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = BalanceSheetService.getAsOfDate txn asOfDate
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleIncomeStatement (args: ParseResults<IncomeStatementArgs>) : int =
    let isJson = args.Contains IncomeStatementArgs.Json
    let periodRaw = args.GetResult IncomeStatementArgs.Period

    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result =
            match parsePeriodArg periodRaw with
            | Choice1Of2 id -> IncomeStatementService.getByPeriodId txn id
            | Choice2Of2 key -> IncomeStatementService.getByPeriodKey txn key
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private handlePnlSubtree (args: ParseResults<PnlSubtreeArgs>) : int =
    let isJson = args.Contains PnlSubtreeArgs.Json
    let account = args.GetResult PnlSubtreeArgs.Account
    let periodRaw = args.GetResult PnlSubtreeArgs.Period

    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result =
            match parsePeriodArg periodRaw with
            | Choice1Of2 id -> SubtreePLService.getByAccountCodeAndPeriodId txn account id
            | Choice2Of2 key -> SubtreePLService.getByAccountCodeAndPeriodKey txn account key
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private handleProjection (args: ParseResults<ProjectionArgs>) : int =
    let account = args.GetResult ProjectionArgs.Account
    let throughRaw = args.GetResult ProjectionArgs.Through

    match parseDate throughRaw with
    | Error e ->
        writeHumanErrors [ e ]
    | Ok throughDate ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = BalanceProjectionService.project txn account throughDate
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            match result with
            | Error errs -> writeHumanErrors errs
            | Ok projection -> writeBalanceProjection projection
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

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
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = AccountBalanceService.getBalanceByCode txn account asOfDate
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            write isJson (result |> Result.map (fun v -> v :> obj) |> Result.mapError (fun e -> [e]))
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

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
    | Some (Projection projArgs) -> handleProjection projArgs
    | Some (Allocation allocArgs)         -> PortfolioReportCommands.handleAllocation allocArgs
    | Some (Portfolio_Summary psArgs)     -> PortfolioReportCommands.handlePortfolioSummary psArgs
    | Some (Portfolio_History phArgs)     -> PortfolioReportCommands.handlePortfolioHistory phArgs
    | Some (Gains gainsArgs)              -> PortfolioReportCommands.handleGains gainsArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
