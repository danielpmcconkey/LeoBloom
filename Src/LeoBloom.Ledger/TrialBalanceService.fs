namespace LeoBloom.Ledger

open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates trial balance queries.
module TrialBalanceService =

    let private accountTypeOrder = [ "asset"; "liability"; "equity"; "revenue"; "expense" ]

    let private buildReport (fiscalPeriodId: int) (periodKey: string) (lines: TrialBalanceAccountLine list) (disclosure: PeriodDisclosure option) : TrialBalanceReport =
        let linesByType = lines |> List.groupBy (fun l -> l.accountTypeName)
        let lineMap = linesByType |> Map.ofList

        let groups =
            accountTypeOrder
            |> List.choose (fun typeName ->
                match Map.tryFind typeName lineMap with
                | None -> None
                | Some groupLines ->
                    Some { accountTypeName = typeName
                           lines = groupLines
                           groupDebitTotal = groupLines |> List.sumBy (fun l -> l.debitTotal)
                           groupCreditTotal = groupLines |> List.sumBy (fun l -> l.creditTotal) })

        let grandTotalDebits = lines |> List.sumBy (fun l -> l.debitTotal)
        let grandTotalCredits = lines |> List.sumBy (fun l -> l.creditTotal)

        { fiscalPeriodId = fiscalPeriodId
          periodKey = periodKey
          groups = groups
          grandTotalDebits = grandTotalDebits
          grandTotalCredits = grandTotalCredits
          isBalanced = (grandTotalDebits = grandTotalCredits)
          disclosure = disclosure }

    let getByPeriodId (txn: NpgsqlTransaction) (fiscalPeriodId: int) (asOriginallyClosed: bool) : Result<TrialBalanceReport, string> =
        Log.info "Getting trial balance for fiscal period ID {FiscalPeriodId}" [| fiscalPeriodId :> obj |]
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
                        let lines = TrialBalanceRepository.getActivityByPeriodAsOfClose txn id d.closedAt.Value
                        let disclosureAoc = Some { d with asOriginallyClosed = true }
                        Ok (buildReport id periodKey lines disclosureAoc)
                else
                    let lines = TrialBalanceRepository.getActivityByPeriod txn id
                    Ok (buildReport id periodKey lines disclosure)
        with ex ->
            Log.errorExn ex "Failed to get trial balance for fiscal period ID {FiscalPeriodId}" [| fiscalPeriodId :> obj |]
            Error (sprintf "Query error: %s" ex.Message)

    let getByPeriodKey (txn: NpgsqlTransaction) (periodKey: string) (asOriginallyClosed: bool) : Result<TrialBalanceReport, string> =
        Log.info "Getting trial balance for period key {PeriodKey}" [| periodKey :> obj |]
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
                        let lines = TrialBalanceRepository.getActivityByPeriodAsOfClose txn fiscalPeriodId d.closedAt.Value
                        let disclosureAoc = Some { d with asOriginallyClosed = true }
                        Ok (buildReport fiscalPeriodId periodKey lines disclosureAoc)
                else
                    let lines = TrialBalanceRepository.getActivityByPeriod txn fiscalPeriodId
                    Ok (buildReport fiscalPeriodId periodKey lines disclosure)
        with ex ->
            Log.errorExn ex "Failed to get trial balance for period key {PeriodKey}" [| periodKey :> obj |]
            Error (sprintf "Query error: %s" ex.Message)
