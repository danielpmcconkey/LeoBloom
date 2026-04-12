namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates balance sheet queries.
module BalanceSheetService =

    let private buildSection (sectionName: string) (lines: BalanceSheetLine list) : BalanceSheetSection =
        { sectionName = sectionName
          lines = lines
          sectionTotal = lines |> List.sumBy (fun l -> l.balance) }

    let private buildReport (asOfDate: DateOnly) (balances: (string * BalanceSheetLine) list) (retainedEarnings: decimal) (disclosure: PeriodDisclosure option) : BalanceSheetReport =
        let assetLines =
            balances
            |> List.filter (fun (typeName, _) -> typeName = "asset")
            |> List.map snd
        let liabilityLines =
            balances
            |> List.filter (fun (typeName, _) -> typeName = "liability")
            |> List.map snd
        let equityLines =
            balances
            |> List.filter (fun (typeName, _) -> typeName = "equity")
            |> List.map snd

        let assets = buildSection "asset" assetLines
        let liabilities = buildSection "liability" liabilityLines
        let equity = buildSection "equity" equityLines
        let totalEquity = equity.sectionTotal + retainedEarnings
        let isBalanced = (assets.sectionTotal = liabilities.sectionTotal + totalEquity)

        { asOfDate = asOfDate
          assets = assets
          liabilities = liabilities
          equity = equity
          retainedEarnings = retainedEarnings
          totalEquity = totalEquity
          isBalanced = isBalanced
          disclosure = disclosure }

    let getAsOfDate (txn: NpgsqlTransaction) (asOfDate: DateOnly) : Result<BalanceSheetReport, string> =
        Log.info "Getting balance sheet as of {AsOfDate}" [| asOfDate :> obj |]
        try
            let balances = BalanceSheetRepository.getCumulativeBalances txn asOfDate
            let retainedEarnings = BalanceSheetRepository.getRetainedEarnings txn asOfDate
            Ok (buildReport asOfDate balances retainedEarnings None)
        with ex ->
            Log.errorExn ex "Failed to get balance sheet as of {AsOfDate}" [| asOfDate :> obj |]
            Error (sprintf "Query error: %s" ex.Message)

    let getAsOfDateWithPeriod (txn: NpgsqlTransaction) (asOfDate: DateOnly) (fiscalPeriodId: int) (asOriginallyClosed: bool) : Result<BalanceSheetReport, string> =
        Log.info "Getting balance sheet as of {AsOfDate} with period {FiscalPeriodId}" [| asOfDate :> obj; fiscalPeriodId :> obj |]
        try
            let disclosure = PeriodDisclosureRepository.getDisclosure txn fiscalPeriodId
            if asOriginallyClosed then
                match disclosure with
                | None -> Error (sprintf "Fiscal period with id %d does not exist" fiscalPeriodId)
                | Some d when d.isOpen -> Error "Cannot use --as-originally-closed on an open period"
                | Some d when d.closedAt.IsNone -> Error "Period has no close timestamp"
                | Some d ->
                    let balances = BalanceSheetRepository.getCumulativeBalancesAsOfClose txn asOfDate fiscalPeriodId d.closedAt.Value
                    let retainedEarnings = BalanceSheetRepository.getRetainedEarningsAsOfClose txn asOfDate fiscalPeriodId d.closedAt.Value
                    let disclosureAoc = Some { d with asOriginallyClosed = true }
                    Ok (buildReport asOfDate balances retainedEarnings disclosureAoc)
            else
                let balances = BalanceSheetRepository.getCumulativeBalances txn asOfDate
                let retainedEarnings = BalanceSheetRepository.getRetainedEarnings txn asOfDate
                Ok (buildReport asOfDate balances retainedEarnings disclosure)
        with ex ->
            Log.errorExn ex "Failed to get balance sheet as of {AsOfDate} with period {FiscalPeriodId}" [| asOfDate :> obj; fiscalPeriodId :> obj |]
            Error (sprintf "Query error: %s" ex.Message)
