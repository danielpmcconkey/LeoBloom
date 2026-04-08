namespace LeoBloom.Ops

open System
open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Utilities
open LeoBloom.Ledger

/// Orchestrates balance projection computation for a single account.
/// Pure computation — no persistence, no new tables.
/// Formula: projected_balance(date) = current_balance
///   + expected_inflows (receivable obligation instances)
///   - expected_outflows (payable obligation instances)
///   - in_flight_transfers_out
///   + in_flight_transfers_in
module BalanceProjectionService =

    let project
        (txn: NpgsqlTransaction)
        (accountCode: string)
        (projectionEndDate: DateOnly)
        : Result<BalanceProjection, string list> =
        Log.info "Computing balance projection for account {AccountCode} through {EndDate}"
            [| accountCode :> obj; projectionEndDate :> obj |]

        let today = DateOnly.FromDateTime(DateTime.Today)

        if projectionEndDate <= today then
            Log.warn "Projection end date {EndDate} is not strictly in the future (today={Today})"
                [| projectionEndDate :> obj; today :> obj |]
            Error [ "Projection end date must be in the future (past or today dates are not allowed)" ]
        else
            match AccountBalanceService.showAccountByCode txn accountCode with
            | Error msg -> Error [ msg ]
            | Ok account ->
                match AccountBalanceService.getBalanceById txn account.id today with
                | Error msg -> Error [ msg ]
                | Ok balanceRecord ->
                    let currentBalance = balanceRecord.balance

                    try
                        let obligationItems =
                            BalanceProjectionRepository.getProjectedObligationItems
                                txn account.id today projectionEndDate
                        let transferItems =
                            BalanceProjectionRepository.getProjectedTransferItems
                                txn account.id today projectionEndDate

                        let allItems = obligationItems @ transferItems
                        let dayCount = projectionEndDate.DayNumber - today.DayNumber + 1
                        let mutable runningBalance = currentBalance
                        let days =
                            [ for i in 0 .. dayCount - 1 do
                                let date = today.AddDays(i)
                                let dayItems =
                                    allItems |> List.filter (fun item -> item.date = date)
                                let knownChange =
                                    dayItems |> List.sumBy (fun item ->
                                        match item.amount with
                                        | None -> 0m
                                        | Some a ->
                                            if item.direction = Inflow then a else -a)
                                let opening = runningBalance
                                let closing = opening + knownChange
                                runningBalance <- closing
                                yield
                                    { date = date
                                      openingBalance = opening
                                      items = dayItems
                                      knownNetChange = knownChange
                                      closingBalance = closing
                                      hasUnknownAmounts =
                                          dayItems |> List.exists (fun i -> i.amount.IsNone) } ]

                        Log.info
                            "Balance projection computed for {AccountCode}: {DayCount} days, {ItemCount} projection items"
                            [| accountCode :> obj; dayCount :> obj; allItems.Length :> obj |]
                        Ok { accountId = account.id
                             accountCode = account.code
                             accountName = account.name
                             asOfDate = today
                             projectionEndDate = projectionEndDate
                             currentBalance = currentBalance
                             days = days }
                    with ex ->
                        Log.errorExn ex
                            "Failed to compute balance projection for account {AccountCode}"
                            [| accountCode :> obj |]
                        Error [ sprintf "Persistence error: %s" ex.Message ]
