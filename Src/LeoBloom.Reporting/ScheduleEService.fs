namespace LeoBloom.Reporting

open Npgsql
open LeoBloom.Utilities
open LeoBloom.Reporting.ReportingTypes
open LeoBloom.Reporting.ScheduleEMapping

/// Orchestrates Schedule E report generation.
module ScheduleEService =

    let private validateYear (year: int) : Result<unit, string list> =
        let mutable errors = []
        if year <= 0 then
            errors <- errors @ [ sprintf "Year must be positive, got %d" year ]
        elif year < 1900 then
            errors <- errors @ [ sprintf "Year %d is unreasonably early" year ]
        elif year > 2100 then
            errors <- errors @ [ sprintf "Year %d is unreasonably late" year ]
        if errors.IsEmpty then Ok () else Error errors

    let private buildLineItem
        (balanceMap: Map<string, decimal>)
        (mapping: ScheduleELineMapping)
        : ScheduleELineItem =

        let subDetails =
            mapping.accountCodes
            |> List.map (fun code ->
                let label =
                    match Map.tryFind code line19SubDetail with
                    | Some desc -> desc
                    | None -> code
                let amount = balanceMap |> Map.tryFind code |> Option.defaultValue 0m
                (label, amount))
            |> List.filter (fun (_, amt) -> amt <> 0m)

        let total = subDetails |> List.sumBy snd

        { lineNumber = mapping.lineNumber
          description = mapping.lineDescription
          amount = total
          subDetail =
              if mapping.lineNumber = 19 then subDetails
              else [] }

    /// Generate a Schedule E report for the given year.
    let generate (txn: NpgsqlTransaction) (year: int) : Result<ScheduleEReport, string list> =
        Log.info "Generating Schedule E report for year {Year}" [| year :> obj |]
        match validateYear year with
        | Error errs ->
            Log.warn "Schedule E validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            try
                let balances = ScheduleERepository.getBalancesForYear txn allMappedAccountCodes year

                let balanceMap =
                    balances
                    |> List.map (fun b -> b.accountCode, b.netBalance)
                    |> Map.ofList

                let lineItems =
                    scheduleELineMappings
                    |> List.map (buildLineItem balanceMap)

                let revenueTotal =
                    lineItems
                    |> List.filter (fun l -> l.lineNumber = 3)
                    |> List.sumBy (fun l -> l.amount)

                let expenseTotal =
                    lineItems
                    |> List.filter (fun l -> l.lineNumber <> 3)
                    |> List.sumBy (fun l -> l.amount)

                Log.info "Schedule E report generated for year {Year}" [| year :> obj |]

                Ok { year = year
                     lineItems = lineItems
                     totalExpenses = expenseTotal
                     netRentalIncome = revenueTotal - expenseTotal }
            with ex ->
                Log.errorExn ex "Failed to generate Schedule E for year {Year}" [| year :> obj |]
                Error [ sprintf "Query error: %s" ex.Message ]
