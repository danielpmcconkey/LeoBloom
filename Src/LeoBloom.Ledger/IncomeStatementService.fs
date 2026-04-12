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

    let private buildReport (fiscalPeriodId: int) (periodKey: string) (activity: (string * IncomeStatementLine) list) (disclosure: PeriodDisclosure option) : IncomeStatementReport =
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
          netIncome = revenue.sectionTotal - expenses.sectionTotal
          disclosure = disclosure }

    let getByPeriodId (txn: NpgsqlTransaction) (fiscalPeriodId: int) (asOriginallyClosed: bool) : Result<IncomeStatementReport, string> =
        Log.info "Getting income statement for fiscal period ID {FiscalPeriodId}" [| fiscalPeriodId :> obj |]
        try
            match TrialBalanceRepository.periodExists txn fiscalPeriodId with
            | None -> Error (sprintf "Fiscal period with id %d does not exist" fiscalPeriodId)
            | Some (id, periodKey) ->
                let disclosure = PeriodDisclosureRepository.getDisclosure txn id
                if asOriginallyClosed then
                    match disclosure with
                    | None -> Error (sprintf "Fiscal period with id %d does not exist" id)
                    | Some d when d.isOpen -> Error "Cannot use --as-originally-closed on an open period"
                    | Some d when d.closedAt.IsNone -> Error "Period has no close timestamp"
                    | Some d ->
                        let activity = IncomeStatementRepository.getActivityByPeriodAsOfClose txn id d.closedAt.Value
                        let disclosureAoc = Some { d with asOriginallyClosed = true }
                        Ok (buildReport id periodKey activity disclosureAoc)
                else
                    let activity = IncomeStatementRepository.getActivityByPeriod txn id
                    Ok (buildReport id periodKey activity disclosure)
        with ex ->
            Log.errorExn ex "Failed to get income statement for fiscal period ID {FiscalPeriodId}" [| fiscalPeriodId :> obj |]
            Error (sprintf "Query error: %s" ex.Message)

    let getByPeriodKey (txn: NpgsqlTransaction) (periodKey: string) (asOriginallyClosed: bool) : Result<IncomeStatementReport, string> =
        Log.info "Getting income statement for period key {PeriodKey}" [| periodKey :> obj |]
        try
            match TrialBalanceRepository.resolvePeriodId txn periodKey with
            | None -> Error (sprintf "Fiscal period with key '%s' does not exist" periodKey)
            | Some fiscalPeriodId ->
                let disclosure = PeriodDisclosureRepository.getDisclosure txn fiscalPeriodId
                if asOriginallyClosed then
                    match disclosure with
                    | None -> Error (sprintf "Fiscal period with key '%s' does not exist" periodKey)
                    | Some d when d.isOpen -> Error "Cannot use --as-originally-closed on an open period"
                    | Some d when d.closedAt.IsNone -> Error "Period has no close timestamp"
                    | Some d ->
                        let activity = IncomeStatementRepository.getActivityByPeriodAsOfClose txn fiscalPeriodId d.closedAt.Value
                        let disclosureAoc = Some { d with asOriginallyClosed = true }
                        Ok (buildReport fiscalPeriodId periodKey activity disclosureAoc)
                else
                    let activity = IncomeStatementRepository.getActivityByPeriod txn fiscalPeriodId
                    Ok (buildReport fiscalPeriodId periodKey activity disclosure)
        with ex ->
            Log.errorExn ex "Failed to get income statement for period key {PeriodKey}" [| periodKey :> obj |]
            Error (sprintf "Query error: %s" ex.Message)
