namespace LeoBloom.Portfolio

open System
open Npgsql
open LeoBloom.Domain.Portfolio

/// Raw SQL queries for portfolio report data.
module PortfolioReportRepository =

    // ---------------------------------------------------------------
    // Internal types for enriched query results
    // ---------------------------------------------------------------

    type EnrichedPositionRow =
        { symbol: string
          fundName: string
          taxBucketName: string
          currentValue: decimal
          costBasis: decimal }

    type HistoryDataRow =
        { positionDate: DateOnly
          category: string
          totalValue: decimal }

    type GainsDataRow =
        { symbol: string
          fundName: string
          costBasis: decimal
          currentValue: decimal }

    // ---------------------------------------------------------------
    // Dimension configuration
    // ---------------------------------------------------------------

    type private DimConfig =
        { joinSql: string
          categoryExpr: string }

    let private dimensionConfig (dim: string) : DimConfig option =
        match dim with
        | "tax-bucket" ->
            Some { joinSql      = "JOIN portfolio.investment_account ia ON ia.id = l.investment_account_id\n             JOIN portfolio.tax_bucket tb ON tb.id = ia.tax_bucket_id"
                   categoryExpr = "tb.name" }
        | "account-group" ->
            Some { joinSql      = "JOIN portfolio.investment_account ia ON ia.id = l.investment_account_id\n             JOIN portfolio.account_group ag ON ag.id = ia.account_group_id"
                   categoryExpr = "ag.name" }
        | "account" ->
            Some { joinSql      = "JOIN portfolio.investment_account ia ON ia.id = l.investment_account_id"
                   categoryExpr = "ia.name" }
        | "investment-type" ->
            Some { joinSql      = "JOIN portfolio.fund f ON f.symbol = l.symbol\n             LEFT JOIN portfolio.dim_investment_type dit ON dit.id = f.investment_type_id"
                   categoryExpr = "COALESCE(dit.name, '(Unclassified)')" }
        | "market-cap" ->
            Some { joinSql      = "JOIN portfolio.fund f ON f.symbol = l.symbol\n             LEFT JOIN portfolio.dim_market_cap dmc ON dmc.id = f.market_cap_id"
                   categoryExpr = "COALESCE(dmc.name, '(Unclassified)')" }
        | "index-type" ->
            Some { joinSql      = "JOIN portfolio.fund f ON f.symbol = l.symbol\n             LEFT JOIN portfolio.dim_index_type dit ON dit.id = f.index_type_id"
                   categoryExpr = "COALESCE(dit.name, '(Unclassified)')" }
        | "sector" ->
            Some { joinSql      = "JOIN portfolio.fund f ON f.symbol = l.symbol\n             LEFT JOIN portfolio.dim_sector ds ON ds.id = f.sector_id"
                   categoryExpr = "COALESCE(ds.name, '(Unclassified)')" }
        | "region" ->
            Some { joinSql      = "JOIN portfolio.fund f ON f.symbol = l.symbol\n             LEFT JOIN portfolio.dim_region dr ON dr.id = f.region_id"
                   categoryExpr = "COALESCE(dr.name, '(Unclassified)')" }
        | "objective" ->
            Some { joinSql      = "JOIN portfolio.fund f ON f.symbol = l.symbol\n             LEFT JOIN portfolio.dim_objective dobj ON dobj.id = f.objective_id"
                   categoryExpr = "COALESCE(dobj.name, '(Unclassified)')" }
        | "symbol" ->
            Some { joinSql      = "JOIN portfolio.fund f ON f.symbol = l.symbol"
                   categoryExpr = "l.symbol" }
        | _ -> None

    // ---------------------------------------------------------------
    // Allocation query
    // ---------------------------------------------------------------

    /// Get (category, total_value) rows for the given dimension,
    /// using DISTINCT ON latest-position logic.
    let getAllocation (txn: NpgsqlTransaction) (dim: string) : (string * decimal) list =
        match dimensionConfig dim with
        | None -> []
        | Some cfg ->
            let sql =
                sprintf """WITH latest AS (
                    SELECT DISTINCT ON (investment_account_id, symbol)
                           investment_account_id, symbol, current_value
                    FROM portfolio.position
                    ORDER BY investment_account_id, symbol, position_date DESC
                )
                SELECT %s AS category, SUM(l.current_value) AS total_value
                FROM latest l
                %s
                GROUP BY %s
                ORDER BY total_value DESC""" cfg.categoryExpr cfg.joinSql cfg.categoryExpr
            use cmd = new NpgsqlCommand(sql, txn.Connection, txn)
            use reader = cmd.ExecuteReader()
            let results = ResizeArray<string * decimal>()
            while reader.Read() do
                let category =
                    if reader.IsDBNull(0) then "(Unclassified)"
                    else reader.GetString(0)
                let value = reader.GetDecimal(1)
                results.Add((category, value))
            reader.Close()
            results |> Seq.toList

    // ---------------------------------------------------------------
    // Summary query — enriched latest positions
    // ---------------------------------------------------------------

    /// Get latest positions with fund and tax-bucket info, for summary calculations.
    let getSummaryData (txn: NpgsqlTransaction) : EnrichedPositionRow list =
        use cmd = new NpgsqlCommand(
            """SELECT DISTINCT ON (p.investment_account_id, p.symbol)
                      p.symbol,
                      f.name        AS fund_name,
                      tb.name       AS tax_bucket_name,
                      p.current_value,
                      p.cost_basis
               FROM portfolio.position p
               JOIN portfolio.fund f ON f.symbol = p.symbol
               JOIN portfolio.investment_account ia ON ia.id = p.investment_account_id
               JOIN portfolio.tax_bucket tb ON tb.id = ia.tax_bucket_id
               ORDER BY p.investment_account_id, p.symbol, p.position_date DESC""",
            txn.Connection, txn)
        use reader = cmd.ExecuteReader()
        let results = ResizeArray<EnrichedPositionRow>()
        while reader.Read() do
            results.Add(
                { symbol         = reader.GetString(0)
                  fundName       = reader.GetString(1)
                  taxBucketName  = reader.GetString(2)
                  currentValue   = reader.GetDecimal(3)
                  costBasis      = reader.GetDecimal(4) })
        reader.Close()
        results |> Seq.toList

    // ---------------------------------------------------------------
    // History query — grouped by date + dimension
    // ---------------------------------------------------------------

    /// Get (position_date, category, total_value) rows for the given dimension
    /// and optional date range, across ALL position dates (not just latest).
    let getHistoryData
        (txn: NpgsqlTransaction)
        (dim: string)
        (startDate: DateOnly option)
        (endDate: DateOnly option)
        : HistoryDataRow list =
        match dimensionConfig dim with
        | None -> []
        | Some cfg ->
            // History query uses "p" alias directly (no CTE), so replace "l." references
            let histJoinSql     = cfg.joinSql.Replace("l.investment_account_id", "p.investment_account_id").Replace("l.symbol", "p.symbol")
            let histCategoryExpr = cfg.categoryExpr.Replace("l.symbol", "p.symbol")
            let conditions = ResizeArray<string>()
            if startDate.IsSome then conditions.Add("p.position_date >= @start")
            if endDate.IsSome   then conditions.Add("p.position_date <= @end")
            let where =
                if conditions.Count = 0 then ""
                else sprintf " WHERE %s" (String.concat " AND " conditions)
            let sql =
                sprintf """SELECT p.position_date, %s AS category, SUM(p.current_value) AS total_value
                           FROM portfolio.position p
                           %s%s
                           GROUP BY p.position_date, %s
                           ORDER BY p.position_date, %s"""
                    histCategoryExpr histJoinSql where histCategoryExpr histCategoryExpr
            use cmd = new NpgsqlCommand(sql, txn.Connection, txn)
            match startDate with
            | Some d -> cmd.Parameters.AddWithValue("@start", d) |> ignore
            | None   -> ()
            match endDate with
            | Some d -> cmd.Parameters.AddWithValue("@end", d) |> ignore
            | None   -> ()
            use reader = cmd.ExecuteReader()
            let results = ResizeArray<HistoryDataRow>()
            while reader.Read() do
                let category =
                    if reader.IsDBNull(1) then "(Unclassified)"
                    else reader.GetString(1)
                results.Add(
                    { positionDate = reader.GetFieldValue<DateOnly>(0)
                      category     = category
                      totalValue   = reader.GetDecimal(2) })
            reader.Close()
            results |> Seq.toList

    // ---------------------------------------------------------------
    // Gains query — latest per (account, symbol) aggregated by symbol
    // ---------------------------------------------------------------

    /// Get per-fund gain/loss data (optionally filtered by account).
    let getGainsData (txn: NpgsqlTransaction) (accountId: int option) : GainsDataRow list =
        let acctWhere =
            match accountId with
            | Some _ -> " WHERE p.investment_account_id = @acct"
            | None   -> ""
        let sql =
            sprintf """WITH latest AS (
                           SELECT DISTINCT ON (investment_account_id, symbol)
                                  symbol, cost_basis, current_value
                           FROM portfolio.position p%s
                           ORDER BY investment_account_id, symbol, position_date DESC
                       )
                       SELECT l.symbol, f.name, SUM(l.cost_basis), SUM(l.current_value)
                       FROM latest l
                       JOIN portfolio.fund f ON f.symbol = l.symbol
                       GROUP BY l.symbol, f.name
                       ORDER BY (SUM(l.current_value) - SUM(l.cost_basis)) DESC""" acctWhere
        use cmd = new NpgsqlCommand(sql, txn.Connection, txn)
        match accountId with
        | Some id -> cmd.Parameters.AddWithValue("@acct", id) |> ignore
        | None    -> ()
        use reader = cmd.ExecuteReader()
        let results = ResizeArray<GainsDataRow>()
        while reader.Read() do
            results.Add(
                { symbol       = reader.GetString(0)
                  fundName     = reader.GetString(1)
                  costBasis    = reader.GetDecimal(2)
                  currentValue = reader.GetDecimal(3) })
        reader.Close()
        results |> Seq.toList
