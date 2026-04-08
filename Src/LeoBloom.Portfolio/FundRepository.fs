namespace LeoBloom.Portfolio

open System
open Npgsql
open LeoBloom.Domain.Portfolio
open LeoBloom.Utilities

/// Raw SQL persistence for fund operations.
module FundRepository =

    let private readFund (reader: System.Data.Common.DbDataReader) : Fund =
        let optInt (col: int) =
            if reader.IsDBNull(col) then None
            else Some (reader.GetInt32(col))
        { symbol           = reader.GetString(0)
          name             = reader.GetString(1)
          investmentTypeId = optInt 2
          marketCapId      = optInt 3
          indexTypeId      = optInt 4
          sectorId         = optInt 5
          regionId         = optInt 6
          objectiveId      = optInt 7 }

    /// Find a fund by symbol.
    let findBySymbol (txn: NpgsqlTransaction) (symbol: string) : Fund option =
        use sql = new NpgsqlCommand(
            "SELECT symbol, name, investment_type_id, market_cap_id,
                    index_type_id, sector_id, region_id, objective_id
             FROM portfolio.fund WHERE symbol = @symbol",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@symbol", symbol) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let fund = readFund reader
            reader.Close()
            Some fund
        else
            reader.Close()
            None

    /// Insert a new fund. Returns the created record (symbol is the PK — no serial).
    let create (txn: NpgsqlTransaction) (fund: Fund) : Fund =
        use sql = new NpgsqlCommand(
            "INSERT INTO portfolio.fund
               (symbol, name, investment_type_id, market_cap_id,
                index_type_id, sector_id, region_id, objective_id)
             VALUES (@symbol, @name, @it, @mc, @idx, @sec, @reg, @obj)
             RETURNING symbol, name, investment_type_id, market_cap_id,
                       index_type_id, sector_id, region_id, objective_id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@symbol", fund.symbol) |> ignore
        sql.Parameters.AddWithValue("@name",   fund.name)   |> ignore
        DataHelpers.optParam "@it"  (fund.investmentTypeId |> Option.map box) sql
        DataHelpers.optParam "@mc"  (fund.marketCapId      |> Option.map box) sql
        DataHelpers.optParam "@idx" (fund.indexTypeId      |> Option.map box) sql
        DataHelpers.optParam "@sec" (fund.sectorId         |> Option.map box) sql
        DataHelpers.optParam "@reg" (fund.regionId         |> Option.map box) sql
        DataHelpers.optParam "@obj" (fund.objectiveId      |> Option.map box) sql
        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let f = readFund reader
        reader.Close()
        f

    /// List all funds, ordered by symbol.
    let listAll (txn: NpgsqlTransaction) : Fund list =
        use sql = new NpgsqlCommand(
            "SELECT symbol, name, investment_type_id, market_cap_id,
                    index_type_id, sector_id, region_id, objective_id
             FROM portfolio.fund
             ORDER BY symbol",
            txn.Connection, txn)
        use reader = sql.ExecuteReader()
        let results = ResizeArray<Fund>()
        while reader.Read() do
            results.Add(readFund reader)
        reader.Close()
        results |> Seq.toList

    /// List funds matching a dimension filter.
    let listByDimension (txn: NpgsqlTransaction) (filter: FundDimensionFilter) : Fund list =
        let (col, value) =
            match filter with
            | ByInvestmentType id -> ("investment_type_id", id)
            | ByMarketCap      id -> ("market_cap_id",      id)
            | ByIndexType      id -> ("index_type_id",      id)
            | BySector         id -> ("sector_id",          id)
            | ByRegion         id -> ("region_id",          id)
            | ByObjective      id -> ("objective_id",       id)
        use sql = new NpgsqlCommand(
            sprintf "SELECT symbol, name, investment_type_id, market_cap_id,
                            index_type_id, sector_id, region_id, objective_id
                     FROM portfolio.fund
                     WHERE %s = @value
                     ORDER BY symbol" col,
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@value", value) |> ignore
        use reader = sql.ExecuteReader()
        let results = ResizeArray<Fund>()
        while reader.Read() do
            results.Add(readFund reader)
        reader.Close()
        results |> Seq.toList
