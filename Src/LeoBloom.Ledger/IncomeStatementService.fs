namespace LeoBloom.Ledger

open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates income statement queries.
module IncomeStatementService =

    let private buildSection (sectionName: string) (lines: IncomeStatementLine list) : IncomeStatementSection =
        { sectionName = sectionName
          lines = lines
          sectionTotal = lines |> List.sumBy (fun l -> l.balance) }

    let private buildReport (fiscalPeriodId: int) (periodKey: string) (activity: (string * IncomeStatementLine) list) : IncomeStatementReport =
        let revenueLines =
            activity
            |> List.filter (fun (typeName, _) -> typeName = "revenue")
            |> List.map snd
        let expenseLines =
            activity
            |> List.filter (fun (typeName, _) -> typeName = "expense")
            |> List.map snd
        let revenue = buildSection "revenue" revenueLines
        let expenses = buildSection "expense" expenseLines
        { fiscalPeriodId = fiscalPeriodId
          periodKey = periodKey
          revenue = revenue
          expenses = expenses
          netIncome = revenue.sectionTotal - expenses.sectionTotal }

    let getByPeriodId (txn: NpgsqlTransaction) (fiscalPeriodId: int) : Result<IncomeStatementReport, string> =
        Log.info "Getting income statement for fiscal period ID {FiscalPeriodId}" [| fiscalPeriodId :> obj |]
        try
            match TrialBalanceRepository.periodExists txn fiscalPeriodId with
            | None -> Error (sprintf "Fiscal period with id %d does not exist" fiscalPeriodId)
            | Some (id, periodKey) ->
                let activity = IncomeStatementRepository.getActivityByPeriod txn id
                Ok (buildReport id periodKey activity)
        with ex ->
            Log.errorExn ex "Failed to get income statement for fiscal period ID {FiscalPeriodId}" [| fiscalPeriodId :> obj |]
            Error (sprintf "Query error: %s" ex.Message)

    let getByPeriodKey (txn: NpgsqlTransaction) (periodKey: string) : Result<IncomeStatementReport, string> =
        Log.info "Getting income statement for period key {PeriodKey}" [| periodKey :> obj |]
        try
            match TrialBalanceRepository.resolvePeriodId txn periodKey with
            | None -> Error (sprintf "Fiscal period with key '%s' does not exist" periodKey)
            | Some fiscalPeriodId ->
                let activity = IncomeStatementRepository.getActivityByPeriod txn fiscalPeriodId
                Ok (buildReport fiscalPeriodId periodKey activity)
        with ex ->
            Log.errorExn ex "Failed to get income statement for period key {PeriodKey}" [| periodKey :> obj |]
            Error (sprintf "Query error: %s" ex.Message)
