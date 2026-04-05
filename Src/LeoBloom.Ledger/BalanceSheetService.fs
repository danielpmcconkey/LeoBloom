namespace LeoBloom.Ledger

open System
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates balance sheet queries.
module BalanceSheetService =

    let private buildSection (sectionName: string) (lines: BalanceSheetLine list) : BalanceSheetSection =
        { sectionName = sectionName
          lines = lines
          sectionTotal = lines |> List.sumBy (fun l -> l.balance) }

    let getAsOfDate (asOfDate: DateOnly) : Result<BalanceSheetReport, string> =
        Log.info "Getting balance sheet as of {AsOfDate}" [| asOfDate :> obj |]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let balances = BalanceSheetRepository.getCumulativeBalances txn asOfDate
            let retainedEarnings = BalanceSheetRepository.getRetainedEarnings txn asOfDate

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

            let report =
                { asOfDate = asOfDate
                  assets = assets
                  liabilities = liabilities
                  equity = equity
                  retainedEarnings = retainedEarnings
                  totalEquity = totalEquity
                  isBalanced = isBalanced }

            txn.Commit()
            Ok report
        with ex ->
            Log.errorExn ex "Failed to get balance sheet as of {AsOfDate}" [| asOfDate :> obj |]
            try txn.Rollback() with _ -> ()
            Error (sprintf "Query error: %s" ex.Message)
