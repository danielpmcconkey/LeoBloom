namespace LeoBloom.Ledger

open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates subtree P&L queries.
module SubtreePLService =

    let private buildSection (sectionName: string) (lines: IncomeStatementLine list) : IncomeStatementSection =
        { sectionName = sectionName
          lines = lines
          sectionTotal = lines |> List.sumBy (fun l -> l.balance) }

    let private buildReport (rootCode: string) (rootName: string) (fiscalPeriodId: int) (periodKey: string) (activity: (string * IncomeStatementLine) list) : SubtreePLReport =
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
        { rootAccountCode = rootCode
          rootAccountName = rootName
          fiscalPeriodId = fiscalPeriodId
          periodKey = periodKey
          revenue = revenue
          expenses = expenses
          netIncome = revenue.sectionTotal - expenses.sectionTotal }

    let getByAccountCodeAndPeriodId (txn: NpgsqlTransaction) (accountCode: string) (fiscalPeriodId: int) : Result<SubtreePLReport, string> =
        Log.info "Getting subtree P&L for account {AccountCode}, fiscal period ID {FiscalPeriodId}" [| accountCode :> obj; fiscalPeriodId :> obj |]
        try
            match SubtreePLRepository.resolveAccount txn accountCode with
            | None -> Error (sprintf "Account with code '%s' does not exist" accountCode)
            | Some (_id, rootCode, rootName) ->
                match TrialBalanceRepository.periodExists txn fiscalPeriodId with
                | None -> Error (sprintf "Fiscal period with id %d does not exist" fiscalPeriodId)
                | Some (id, periodKey) ->
                    let activity = SubtreePLRepository.getSubtreeActivityByPeriod txn rootCode id
                    Ok (buildReport rootCode rootName id periodKey activity)
        with ex ->
            Log.errorExn ex "Failed to get subtree P&L for account {AccountCode}, fiscal period ID {FiscalPeriodId}" [| accountCode :> obj; fiscalPeriodId :> obj |]
            Error (sprintf "Query error: %s" ex.Message)

    let getByAccountCodeAndPeriodKey (txn: NpgsqlTransaction) (accountCode: string) (periodKey: string) : Result<SubtreePLReport, string> =
        Log.info "Getting subtree P&L for account {AccountCode}, period key {PeriodKey}" [| accountCode :> obj; periodKey :> obj |]
        try
            match SubtreePLRepository.resolveAccount txn accountCode with
            | None -> Error (sprintf "Account with code '%s' does not exist" accountCode)
            | Some (_id, rootCode, rootName) ->
                match TrialBalanceRepository.resolvePeriodId txn periodKey with
                | None -> Error (sprintf "Fiscal period with key '%s' does not exist" periodKey)
                | Some fiscalPeriodId ->
                    let activity = SubtreePLRepository.getSubtreeActivityByPeriod txn rootCode fiscalPeriodId
                    Ok (buildReport rootCode rootName fiscalPeriodId periodKey activity)
        with ex ->
            Log.errorExn ex "Failed to get subtree P&L for account {AccountCode}, period key {PeriodKey}" [| accountCode :> obj; periodKey :> obj |]
            Error (sprintf "Query error: %s" ex.Message)
