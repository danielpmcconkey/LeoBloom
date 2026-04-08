namespace LeoBloom.Reporting

open System
open Npgsql
open LeoBloom.Utilities
open LeoBloom.Reporting.ReportingTypes

/// Orchestrates cash receipts and cash disbursements report generation.
module CashFlowReportService =

    let private validateDateRange (fromDate: DateOnly) (toDate: DateOnly) : Result<unit, string list> =
        if fromDate > toDate then
            Error [ sprintf "From date %O is after to date %O" fromDate toDate ]
        else
            Ok ()

    /// Generate a cash receipts report (money in).
    let getReceipts (txn: NpgsqlTransaction) (fromDate: DateOnly) (toDate: DateOnly) : Result<CashReceiptsReport, string list> =
        Log.info "Generating cash receipts report from {From} to {To}" [| fromDate :> obj; toDate :> obj |]

        match validateDateRange fromDate toDate with
        | Error errs ->
            Log.warn "Cash receipts validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            try
                let rows = CashFlowRepository.getReceipts txn fromDate toDate

                let entries =
                    rows
                    |> List.map (fun row ->
                        { date = row.date
                          journalEntryId = row.journalEntryId
                          description = row.description
                          counterpartyAccount = row.counterpartyAccount
                          amount = row.amount } : CashReceiptEntry)

                let total = entries |> List.sumBy (fun e -> e.amount)

                Log.info "Cash receipts report generated: {Count} entries" [| entries.Length :> obj |]

                Ok { fromDate = fromDate
                     toDate = toDate
                     entries = entries
                     totalReceipts = total }
            with ex ->
                Log.errorExn ex "Failed to generate cash receipts report" [||]
                Error [ sprintf "Query error: %s" ex.Message ]

    /// Generate a cash disbursements report (money out).
    let getDisbursements (txn: NpgsqlTransaction) (fromDate: DateOnly) (toDate: DateOnly) : Result<CashDisbursementsReport, string list> =
        Log.info "Generating cash disbursements report from {From} to {To}" [| fromDate :> obj; toDate :> obj |]

        match validateDateRange fromDate toDate with
        | Error errs ->
            Log.warn "Cash disbursements validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            try
                let rows = CashFlowRepository.getDisbursements txn fromDate toDate

                let entries =
                    rows
                    |> List.map (fun row ->
                        { date = row.date
                          journalEntryId = row.journalEntryId
                          description = row.description
                          counterpartyAccount = row.counterpartyAccount
                          amount = row.amount } : CashDisbursementEntry)

                let total = entries |> List.sumBy (fun e -> e.amount)

                Log.info "Cash disbursements report generated: {Count} entries" [| entries.Length :> obj |]

                Ok { fromDate = fromDate
                     toDate = toDate
                     entries = entries
                     totalDisbursements = total }
            with ex ->
                Log.errorExn ex "Failed to generate cash disbursements report" [||]
                Error [ sprintf "Query error: %s" ex.Message ]
