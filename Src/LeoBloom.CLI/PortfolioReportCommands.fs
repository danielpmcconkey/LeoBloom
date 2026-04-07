module LeoBloom.CLI.PortfolioReportCommands

open Argu
open LeoBloom.Portfolio
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
    let result = PortfolioReportService.getAllocation dim
    writeAllocationReport isJson result

let handlePortfolioSummary (args: ParseResults<PortfolioSummaryArgs>) : int =
    let isJson = args.Contains PortfolioSummaryArgs.Json
    let result = PortfolioReportService.getPortfolioSummary ()
    writePortfolioSummary isJson result

let handlePortfolioHistory (args: ParseResults<PortfolioHistoryArgs>) : int =
    let isJson  = args.Contains PortfolioHistoryArgs.Json
    let dim     = args.TryGetResult PortfolioHistoryArgs.By   |> Option.defaultValue "tax-bucket"
    let fromRaw = args.TryGetResult PortfolioHistoryArgs.From
    let toRaw   = args.TryGetResult PortfolioHistoryArgs.To
    let result  = PortfolioReportService.getPortfolioHistory dim fromRaw toRaw
    writePortfolioHistoryReport isJson result

let handleGains (args: ParseResults<GainsArgs>) : int =
    let isJson    = args.Contains GainsArgs.Json
    let accountId = args.TryGetResult GainsArgs.Account
    let result    = PortfolioReportService.getGains accountId
    writeGainsReport isJson result
