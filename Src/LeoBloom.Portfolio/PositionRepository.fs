namespace LeoBloom.Portfolio

open System
open Npgsql
open LeoBloom.Domain.Portfolio

/// Raw SQL persistence for position operations.
module PositionRepository =

    let private readPosition (reader: System.Data.Common.DbDataReader) : Position =
        { id                  = reader.GetInt32(0)
          investmentAccountId = reader.GetInt32(1)
          symbol              = reader.GetString(2)
          positionDate        = reader.GetFieldValue<DateOnly>(3)
          price               = reader.GetDecimal(4)
          quantity            = reader.GetDecimal(5)
          currentValue        = reader.GetDecimal(6)
          costBasis           = reader.GetDecimal(7) }

    /// Find a position by ID.
    let findById (txn: NpgsqlTransaction) (positionId: int) : Position option =
        use sql = new NpgsqlCommand(
            "SELECT id, investment_account_id, symbol, position_date,
                    price, quantity, current_value, cost_basis
             FROM portfolio.position WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", positionId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let p = readPosition reader
            reader.Close()
            Some p
        else
            reader.Close()
            None

    /// Insert a new position. Returns the created record.
    let create
        (txn: NpgsqlTransaction)
        (investmentAccountId: int)
        (symbol: string)
        (positionDate: DateOnly)
        (price: decimal)
        (quantity: decimal)
        (currentValue: decimal)
        (costBasis: decimal)
        : Position =
        use sql = new NpgsqlCommand(
            "INSERT INTO portfolio.position
               (investment_account_id, symbol, position_date,
                price, quantity, current_value, cost_basis)
             VALUES (@acct, @sym, @date, @price, @qty, @cv, @cb)
             RETURNING id, investment_account_id, symbol, position_date,
                       price, quantity, current_value, cost_basis",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@acct",  investmentAccountId) |> ignore
        sql.Parameters.AddWithValue("@sym",   symbol)              |> ignore
        sql.Parameters.AddWithValue("@date",  positionDate)        |> ignore
        sql.Parameters.AddWithValue("@price", price)               |> ignore
        sql.Parameters.AddWithValue("@qty",   quantity)            |> ignore
        sql.Parameters.AddWithValue("@cv",    currentValue)        |> ignore
        sql.Parameters.AddWithValue("@cb",    costBasis)           |> ignore
        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let p = readPosition reader
        reader.Close()
        p

    /// List positions matching the given filter. All filter fields are optional.
    let listByFilter (txn: NpgsqlTransaction) (filter: PositionFilter) : Position list =
        let conditions = ResizeArray<string>()
        if filter.investmentAccountId.IsSome then
            conditions.Add("investment_account_id = @acct")
        if filter.startDate.IsSome then
            conditions.Add("position_date >= @start")
        if filter.endDate.IsSome then
            conditions.Add("position_date <= @end")
        let where =
            if conditions.Count = 0 then ""
            else sprintf " WHERE %s" (String.concat " AND " conditions)
        use sql = new NpgsqlCommand(
            sprintf "SELECT id, investment_account_id, symbol, position_date,
                            price, quantity, current_value, cost_basis
                     FROM portfolio.position%s
                     ORDER BY position_date, symbol" where,
            txn.Connection, txn)
        match filter.investmentAccountId with
        | Some id -> sql.Parameters.AddWithValue("@acct", id) |> ignore
        | None -> ()
        match filter.startDate with
        | Some d -> sql.Parameters.AddWithValue("@start", d) |> ignore
        | None -> ()
        match filter.endDate with
        | Some d -> sql.Parameters.AddWithValue("@end", d) |> ignore
        | None -> ()
        use reader = sql.ExecuteReader()
        let results = ResizeArray<Position>()
        while reader.Read() do
            results.Add(readPosition reader)
        reader.Close()
        results |> Seq.toList

    /// Get the most recent position per symbol for the given account.
    let latestByAccount (txn: NpgsqlTransaction) (accountId: int) : Position list =
        use sql = new NpgsqlCommand(
            "SELECT DISTINCT ON (symbol)
                    id, investment_account_id, symbol, position_date,
                    price, quantity, current_value, cost_basis
             FROM portfolio.position
             WHERE investment_account_id = @id
             ORDER BY symbol, position_date DESC",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", accountId) |> ignore
        use reader = sql.ExecuteReader()
        let results = ResizeArray<Position>()
        while reader.Read() do
            results.Add(readPosition reader)
        reader.Close()
        results |> Seq.toList

    /// Get the most recent position per (account, symbol) pair across all accounts.
    let latestAll (txn: NpgsqlTransaction) : Position list =
        use sql = new NpgsqlCommand(
            "SELECT DISTINCT ON (investment_account_id, symbol)
                    id, investment_account_id, symbol, position_date,
                    price, quantity, current_value, cost_basis
             FROM portfolio.position
             ORDER BY investment_account_id, symbol, position_date DESC",
            txn.Connection, txn)
        use reader = sql.ExecuteReader()
        let results = ResizeArray<Position>()
        while reader.Read() do
            results.Add(readPosition reader)
        reader.Close()
        results |> Seq.toList
