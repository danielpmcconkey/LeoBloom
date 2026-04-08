namespace LeoBloom.Reporting

open System
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Reporting.ReportingTypes

/// Orchestrates general ledger detail report generation.
module GeneralLedgerReportService =

    let private validateInputs (accountCode: string) (fromDate: DateOnly) (toDate: DateOnly) : Result<unit, string list> =
        let mutable errors = []
        if String.IsNullOrWhiteSpace accountCode then
            errors <- errors @ [ "Account code is required" ]
        if fromDate > toDate then
            errors <- errors @ [ sprintf "From date %O is after to date %O" fromDate toDate ]
        if errors.IsEmpty then Ok () else Error errors

    /// Generate a general ledger detail report for a single account.
    let generate (txn: NpgsqlTransaction) (accountCode: string) (fromDate: DateOnly) (toDate: DateOnly) : Result<GeneralLedgerReport, string list> =
        Log.info "Generating general ledger report for account {AccountCode} from {From} to {To}"
            [| accountCode :> obj; fromDate :> obj; toDate :> obj |]

        match validateInputs accountCode fromDate toDate with
        | Error errs ->
            Log.warn "General ledger validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            try
                match GeneralLedgerRepository.resolveAccountByCode txn accountCode with
                | None ->
                    Error [ sprintf "Account with code '%s' does not exist" accountCode ]
                | Some (accountId, accountName, normalBalance) ->
                    let nb = match normalBalance with "credit" -> NormalBalance.Credit | _ -> NormalBalance.Debit
                    let rows = GeneralLedgerRepository.getEntriesForAccount txn accountId fromDate toDate

                    // Compute running balance respecting normal balance direction
                    // Debit-normal (asset, expense): balance = debits - credits
                    // Credit-normal (liability, equity, revenue): balance = credits - debits
                    let mutable runningBalance = 0m
                    let entries =
                        rows
                        |> List.map (fun row ->
                            let delta = resolveBalance nb row.debitAmount row.creditAmount
                            runningBalance <- runningBalance + delta
                            { date = row.date
                              journalEntryId = row.journalEntryId
                              description = row.description
                              debitAmount = row.debitAmount
                              creditAmount = row.creditAmount
                              runningBalance = runningBalance } : GeneralLedgerEntry)

                    Log.info "General ledger report generated for account {AccountCode}" [| accountCode :> obj |]

                    Ok { accountCode = accountCode
                         accountName = accountName
                         fromDate = fromDate
                         toDate = toDate
                         entries = entries
                         endingBalance = runningBalance }
            with ex ->
                Log.errorExn ex "Failed to generate general ledger for account {AccountCode}" [| accountCode :> obj |]
                Error [ sprintf "Query error: %s" ex.Message ]
