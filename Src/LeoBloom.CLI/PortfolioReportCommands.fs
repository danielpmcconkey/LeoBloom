module LeoBloom.CLI.PortfolioReportCommands

open Argu
open LeoBloom.Portfolio
open LeoBloom.Utilities
open LeoBloom.CLI.OutputFormatter

// --- Argu DU definitions ---

type AllocationArgs =
    | By of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | By _   -> "Grouping dimension (default: account-group). One of: tax-bucket, account-group, account, investment-type, market-cap, index-type, sector, region, objective, symbol"
            | Json   -> "Output in JSON format"

type PortfolioSummaryArgs =
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Json -> "Output in JSON format"

type PortfolioHistoryArgs =
    | By   of string
    | From of string
    | To   of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | By _   -> "Grouping dimension (default: tax-bucket). Same options as allocation --by"
            | From _ -> "Start date (yyyy-MM-dd)"
            | To _   -> "End date (yyyy-MM-dd)"
            | Json   -> "Output in JSON format"

type GainsArgs =
    | Account of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Account _ -> "Filter to a specific investment account ID"
            | Json      -> "Output in JSON format"

// --- Handlers ---

let handleAllocation (args: ParseResults<AllocationArgs>) : int =
    let isJson = args.Contains AllocationArgs.Json
    let dim    = args.TryGetResult AllocationArgs.By |> Option.defaultValue "account-group"
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = PortfolioReportService.getAllocation txn dim
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        writeAllocationReport isJson result
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let handlePortfolioSummary (args: ParseResults<PortfolioSummaryArgs>) : int =
    let isJson = args.Contains PortfolioSummaryArgs.Json
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = PortfolioReportService.getPortfolioSummary txn
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        writePortfolioSummary isJson result
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let handlePortfolioHistory (args: ParseResults<PortfolioHistoryArgs>) : int =
    let isJson  = args.Contains PortfolioHistoryArgs.Json
    let dim     = args.TryGetResult PortfolioHistoryArgs.By   |> Option.defaultValue "tax-bucket"
    let fromRaw = args.TryGetResult PortfolioHistoryArgs.From
    let toRaw   = args.TryGetResult PortfolioHistoryArgs.To
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = PortfolioReportService.getPortfolioHistory txn dim fromRaw toRaw
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        writePortfolioHistoryReport isJson result
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let handleGains (args: ParseResults<GainsArgs>) : int =
    let isJson    = args.Contains GainsArgs.Json
    let accountId = args.TryGetResult GainsArgs.Account
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = PortfolioReportService.getGains txn accountId
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        writeGainsReport isJson result
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()
