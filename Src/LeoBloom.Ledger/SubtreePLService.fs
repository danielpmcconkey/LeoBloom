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

    let private buildReport (rootCode: string) (rootName: string) (fiscalPeriodId: int) (periodKey: string) (activity: (string * IncomeStatementLine) list) (disclosure: PeriodDisclosure option) : SubtreePLReport =
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
          netIncome = revenue.sectionTotal - expenses.sectionTotal
          disclosure = disclosure }

    let getByAccountCodeAndPeriodId (txn: NpgsqlTransaction) (accountCode: string) (fiscalPeriodId: int) (asOriginallyClosed: bool) : Result<SubtreePLReport, string> =
        Log.info "Getting subtree P&L for account {AccountCode}, fiscal period ID {FiscalPeriodId}" [| accountCode :> obj; fiscalPeriodId :> obj |]
        try
            match SubtreePLRepository.resolveAccount txn accountCode with
            | None -> Error (sprintf "Account with code '%s' does not exist" accountCode)
            | Some (_id, rootCode, rootName) ->
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
                            let activity = SubtreePLRepository.getSubtreeActivityByPeriodAsOfClose txn rootCode id d.closedAt.Value
                            let disclosureAoc = Some { d with asOriginallyClosed = true }
                            Ok (buildReport rootCode rootName id periodKey activity disclosureAoc)
                    else
                        let activity = SubtreePLRepository.getSubtreeActivityByPeriod txn rootCode id
                        Ok (buildReport rootCode rootName id periodKey activity disclosure)
        with ex ->
            Log.errorExn ex "Failed to get subtree P&L for account {AccountCode}, fiscal period ID {FiscalPeriodId}" [| accountCode :> obj; fiscalPeriodId :> obj |]
            Error (sprintf "Query error: %s" ex.Message)

    let getByAccountCodeAndPeriodKey (txn: NpgsqlTransaction) (accountCode: string) (periodKey: string) (asOriginallyClosed: bool) : Result<SubtreePLReport, string> =
        Log.info "Getting subtree P&L for account {AccountCode}, period key {PeriodKey}" [| accountCode :> obj; periodKey :> obj |]
        try
            match SubtreePLRepository.resolveAccount txn accountCode with
            | None -> Error (sprintf "Account with code '%s' does not exist" accountCode)
            | Some (_id, rootCode, rootName) ->
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
                            let activity = SubtreePLRepository.getSubtreeActivityByPeriodAsOfClose txn rootCode fiscalPeriodId d.closedAt.Value
                            let disclosureAoc = Some { d with asOriginallyClosed = true }
                            Ok (buildReport rootCode rootName fiscalPeriodId periodKey activity disclosureAoc)
                    else
                        let activity = SubtreePLRepository.getSubtreeActivityByPeriod txn rootCode fiscalPeriodId
                        Ok (buildReport rootCode rootName fiscalPeriodId periodKey activity disclosure)
        with ex ->
            Log.errorExn ex "Failed to get subtree P&L for account {AccountCode}, period key {PeriodKey}" [| accountCode :> obj; periodKey :> obj |]
            Error (sprintf "Query error: %s" ex.Message)
