namespace LeoBloom.Reporting

open System
open Npgsql
open ReportingTypes

module NetWorthReportService =

    let generate (txn: NpgsqlTransaction) (asOfDate: DateOnly) : NetWorthReport =
        let investmentPositions = NetWorthRepository.getInvestmentPositions txn asOfDate
        let cashBalances = NetWorthRepository.getCashBalances txn asOfDate
        let liabilityBalances = NetWorthRepository.getLiabilityBalances txn asOfDate
        let frozenAssetBalances = NetWorthRepository.getFrozenAssetBalances txn asOfDate

        let securities =
            investmentPositions
            |> List.filter (fun p -> p.investmentTypeName <> "Real estate")

        let realEstate =
            investmentPositions
            |> List.filter (fun p -> p.investmentTypeName = "Real estate")

        let securitiesTotal = securities |> List.sumBy (fun p -> p.currentValue)
        let realEstateTotal = realEstate |> List.sumBy (fun p -> p.currentValue)
        let cashTotal = cashBalances |> List.sumBy (fun b -> b.balance)
        let frozenTotal = frozenAssetBalances |> List.sumBy (fun b -> b.balance)
        let assetsTotal = securitiesTotal + realEstateTotal + cashTotal + frozenTotal
        let liabilitiesTotal = liabilityBalances |> List.sumBy (fun b -> b.balance)
        let netWorth = assetsTotal - liabilitiesTotal

        let mutable lines = []
        let mutable ordinal = 0

        let addLine label amount level =
            ordinal <- ordinal + 1
            lines <- { ordinal = ordinal; label = label; amount = amount; level = level } :: lines

        addLine "Total Net Worth" netWorth 0
        addLine "Assets" assetsTotal 1

        if not (List.isEmpty securities) then
            addLine "Securities" securitiesTotal 2
            securities
            |> List.groupBy (fun p -> p.taxBucketName)
            |> List.sortBy fst
            |> List.iter (fun (bucket, bucketPositions) ->
                let bucketTotal = bucketPositions |> List.sumBy (fun p -> p.currentValue)
                addLine bucket bucketTotal 3
                bucketPositions
                |> List.sortByDescending (fun p -> p.currentValue)
                |> List.iter (fun p ->
                    addLine (sprintf "%s — %s" p.symbol p.fundName) p.currentValue 4
                )
            )

        if not (List.isEmpty realEstate) then
            addLine "Real Estate" realEstateTotal 2
            realEstate
            |> List.sortByDescending (fun p -> p.currentValue)
            |> List.iter (fun p ->
                addLine p.investmentAccountName p.currentValue 3
            )

        if not (List.isEmpty cashBalances) then
            addLine "Cash" cashTotal 2
            cashBalances
            |> List.sortByDescending (fun b -> b.balance)
            |> List.iter (fun b ->
                addLine b.accountName b.balance 3
            )

        if not (List.isEmpty frozenAssetBalances) then
            addLine "Frozen" frozenTotal 2
            frozenAssetBalances
            |> List.sortByDescending (fun b -> b.balance)
            |> List.iter (fun b ->
                addLine b.accountName b.balance 3
            )

        addLine "Liabilities" liabilitiesTotal 1
        liabilityBalances
        |> List.sortByDescending (fun b -> b.balance)
        |> List.iter (fun b ->
            addLine b.accountName b.balance 2
        )

        { asOfDate = asOfDate
          lines = List.rev lines }
