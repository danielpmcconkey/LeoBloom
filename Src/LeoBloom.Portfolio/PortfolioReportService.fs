namespace LeoBloom.Portfolio

open System
open Npgsql
open LeoBloom.Domain.Portfolio
open LeoBloom.Utilities

/// Orchestrates portfolio report calculations.
/// Caller is responsible for connection and transaction lifecycle.
module PortfolioReportService =

    let private validDimensions =
        Set.ofList
            [ "tax-bucket"; "account-group"; "account"
              "investment-type"; "market-cap"; "index-type"
              "sector"; "region"; "objective"; "symbol" ]

    let private parseDate (raw: string) : Result<DateOnly, string> =
        match DateOnly.TryParseExact(raw, "yyyy-MM-dd") with
        | true, d -> Ok d
        | false, _ -> Error (sprintf "Invalid date format '%s' -- expected yyyy-MM-dd" raw)

    /// Adjust percentages so they sum to exactly 100.0%.
    /// Assigns the remainder to the largest row.
    let private adjustPercentages (rows: AllocationRow list) : AllocationRow list =
        if rows.IsEmpty then []
        else
            let rawSum = rows |> List.sumBy (fun r -> r.percentage)
            let diff = 100.0m - rawSum
            // Add diff to the first (largest) row
            rows
            |> List.mapi (fun i r ->
                if i = 0 then { r with percentage = r.percentage + diff }
                else r)

    // ---------------------------------------------------------------
    // Allocation
    // ---------------------------------------------------------------

    let getAllocation (txn: NpgsqlTransaction) (dimension: string) : Result<AllocationReport, string list> =
        let dim = if String.IsNullOrWhiteSpace(dimension) then "account-group" else dimension
        if not (validDimensions.Contains(dim)) then
            Error [ sprintf "Unknown dimension '%s'. Valid values: %s"
                        dim (String.concat ", " (Set.toList validDimensions)) ]
        else
            Log.info "Getting allocation by {Dimension}" [| dim :> obj |]
            try
                let rawRows = PortfolioReportRepository.getAllocation txn dim
                let total = rawRows |> List.sumBy snd
                if total = 0m then
                    Ok { dimension = dim; rows = []; total = 0m }
                else
                    let rows =
                        rawRows
                        |> List.map (fun (cat, value) ->
                            { category     = cat
                              currentValue = value
                              percentage   = Math.Round(value / total * 100.0m, 1) })
                    let adjusted = adjustPercentages rows
                    Ok { dimension = dim; rows = adjusted; total = total }
            with ex ->
                Log.errorExn ex "Failed to get allocation by {Dimension}" [| dim :> obj |]
                Error [ sprintf "Persistence error: %s" ex.Message ]

    // ---------------------------------------------------------------
    // Portfolio Summary
    // ---------------------------------------------------------------

    let getPortfolioSummary (txn: NpgsqlTransaction) : Result<PortfolioSummary, string list> =
        Log.info "Getting portfolio summary" [||]
        try
            let rows = PortfolioReportRepository.getSummaryData txn
            if rows.IsEmpty then
                Ok { totalValue            = 0m
                     totalCostBasis        = 0m
                     unrealizedGainLoss    = 0m
                     unrealizedGainLossPct = 0m
                     taxBucketBreakdown    = []
                     topHoldings           = [] }
            else
                let totalValue     = rows |> List.sumBy (fun r -> r.currentValue)
                let totalCostBasis = rows |> List.sumBy (fun r -> r.costBasis)
                let gainLoss       = totalValue - totalCostBasis
                let gainLossPct    =
                    if totalCostBasis = 0m then 0m
                    else Math.Round(gainLoss / totalCostBasis * 100.0m, 2)

                // Tax bucket breakdown
                let bucketGroups =
                    rows
                    |> List.groupBy (fun r -> r.taxBucketName)
                    |> List.map (fun (bucket, grp) ->
                        let value = grp |> List.sumBy (fun r -> r.currentValue)
                        { category     = bucket
                          currentValue = value
                          percentage   = Math.Round(value / totalValue * 100.0m, 1) })
                    |> List.sortByDescending (fun r -> r.currentValue)

                // Top 5 holdings by current value (across all accounts, summed by symbol)
                let topHoldings =
                    rows
                    |> List.groupBy (fun r -> (r.symbol, r.fundName))
                    |> List.map (fun ((sym, name), grp) ->
                        let value = grp |> List.sumBy (fun r -> r.currentValue)
                        { category     = sprintf "%s - %s" sym name
                          currentValue = value
                          percentage   = Math.Round(value / totalValue * 100.0m, 1) })
                    |> List.sortByDescending (fun r -> r.currentValue)
                    |> List.truncate 5

                Ok { totalValue            = totalValue
                     totalCostBasis        = totalCostBasis
                     unrealizedGainLoss    = gainLoss
                     unrealizedGainLossPct = gainLossPct
                     taxBucketBreakdown    = bucketGroups
                     topHoldings           = topHoldings }
        with ex ->
            Log.errorExn ex "Failed to get portfolio summary" [||]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    // ---------------------------------------------------------------
    // Portfolio History
    // ---------------------------------------------------------------

    let getPortfolioHistory
        (txn: NpgsqlTransaction)
        (dimension: string)
        (fromRaw: string option)
        (toRaw: string option)
        : Result<PortfolioHistoryReport, string list> =
        let dim = if String.IsNullOrWhiteSpace(dimension) then "tax-bucket" else dimension
        if not (validDimensions.Contains(dim)) then
            Error [ sprintf "Unknown dimension '%s'. Valid values: %s"
                        dim (String.concat ", " (Set.toList validDimensions)) ]
        else
            let fromResult = fromRaw |> Option.map parseDate
            let toResult   = toRaw   |> Option.map parseDate
            let errors =
                [ match fromResult with Some (Error e) -> yield e | _ -> ()
                  match toResult   with Some (Error e) -> yield e | _ -> () ]
            if not errors.IsEmpty then Error errors
            else
                let startDate = fromResult |> Option.bind (function Ok d -> Some d | _ -> None)
                let endDate   = toResult   |> Option.bind (function Ok d -> Some d | _ -> None)
                Log.info "Getting portfolio history by {Dimension}" [| dim :> obj |]
                try
                    let rawRows = PortfolioReportRepository.getHistoryData txn dim startDate endDate
                    // Group raw rows by date, pivot to HistoryRow
                    let historyRows =
                        rawRows
                        |> List.groupBy (fun r -> r.positionDate)
                        |> List.map (fun (date, grp) ->
                            let cats  = grp |> List.map (fun r -> (r.category, r.totalValue))
                            let total = cats |> List.sumBy snd
                            { positionDate = date
                              categories   = cats
                              total        = total })
                        |> List.sortBy (fun r -> r.positionDate)
                    Ok { dimension = dim; rows = historyRows }
                with ex ->
                    Log.errorExn ex "Failed to get portfolio history by {Dimension}" [| dim :> obj |]
                    Error [ sprintf "Persistence error: %s" ex.Message ]

    // ---------------------------------------------------------------
    // Gains
    // ---------------------------------------------------------------

    let getGains (txn: NpgsqlTransaction) (accountId: int option) : Result<GainsReport, string list> =
        Log.info "Getting gains report" [||]
        try
            let rawRows = PortfolioReportRepository.getGainsData txn accountId
            if rawRows.IsEmpty then
                Ok { rows               = []
                     totalCostBasis     = 0m
                     totalCurrentValue  = 0m
                     totalGainLoss      = 0m
                     totalGainLossPct   = 0m }
            else
                let rows =
                    rawRows
                    |> List.map (fun r ->
                        let gl    = r.currentValue - r.costBasis
                        let glPct =
                            if r.costBasis = 0m then 0m
                            else Math.Round(gl / r.costBasis * 100.0m, 2)
                        { symbol       = r.symbol
                          fundName     = r.fundName
                          costBasis    = r.costBasis
                          currentValue = r.currentValue
                          gainLoss     = gl
                          gainLossPct  = glPct })
                let totalCB  = rows |> List.sumBy (fun r -> r.costBasis)
                let totalCV  = rows |> List.sumBy (fun r -> r.currentValue)
                let totalGL  = totalCV - totalCB
                let totalPct =
                    if totalCB = 0m then 0m
                    else Math.Round(totalGL / totalCB * 100.0m, 2)
                Ok { rows              = rows
                     totalCostBasis    = totalCB
                     totalCurrentValue = totalCV
                     totalGainLoss     = totalGL
                     totalGainLossPct  = totalPct }
        with ex ->
            Log.errorExn ex "Failed to get gains report" [||]
            Error [ sprintf "Persistence error: %s" ex.Message ]
