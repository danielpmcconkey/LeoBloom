namespace LeoBloom.Utilities

open System
open Npgsql
open LeoBloom.Domain.Ledger

/// Raw SQL persistence for fiscal period operations. All operations run within
/// a caller-provided NpgsqlTransaction for atomicity.
module FiscalPeriodRepository =

    /// Look up a fiscal period by ID. Returns None if not found.
    let findById (txn: NpgsqlTransaction) (periodId: int) : FiscalPeriod option =
        use sql = new NpgsqlCommand(
            "SELECT id, period_key, start_date, end_date, is_open, created_at
             FROM ledger.fiscal_period WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", periodId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let period =
                { id = reader.GetInt32(0)
                  periodKey = reader.GetString(1)
                  startDate = reader.GetFieldValue<DateOnly>(2)
                  endDate = reader.GetFieldValue<DateOnly>(3)
                  isOpen = reader.GetBoolean(4)
                  createdAt = reader.GetFieldValue<DateTimeOffset>(5) }
            reader.Close()
            Some period
        else
            reader.Close()
            None

    /// Look up a fiscal period by date. Returns None if no period covers the date.
    let findByDate (txn: NpgsqlTransaction) (date: DateOnly) : FiscalPeriod option =
        use sql = new NpgsqlCommand(
            "SELECT id, period_key, start_date, end_date, is_open, created_at
             FROM ledger.fiscal_period WHERE @date >= start_date AND @date <= end_date
             ORDER BY start_date LIMIT 1",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@date", date) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let period =
                { id = reader.GetInt32(0)
                  periodKey = reader.GetString(1)
                  startDate = reader.GetFieldValue<DateOnly>(2)
                  endDate = reader.GetFieldValue<DateOnly>(3)
                  isOpen = reader.GetBoolean(4)
                  createdAt = reader.GetFieldValue<DateTimeOffset>(5) }
            reader.Close()
            Some period
        else
            reader.Close()
            None

    /// Set is_open on the given period. Returns updated FiscalPeriod or None.
    let setIsOpen (txn: NpgsqlTransaction) (periodId: int) (isOpen: bool) : FiscalPeriod option =
        use sql = new NpgsqlCommand(
            "UPDATE ledger.fiscal_period SET is_open = @is_open WHERE id = @id
             RETURNING id, period_key, start_date, end_date, is_open, created_at",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@is_open", isOpen) |> ignore
        sql.Parameters.AddWithValue("@id", periodId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let period =
                { id = reader.GetInt32(0)
                  periodKey = reader.GetString(1)
                  startDate = reader.GetFieldValue<DateOnly>(2)
                  endDate = reader.GetFieldValue<DateOnly>(3)
                  isOpen = reader.GetBoolean(4)
                  createdAt = reader.GetFieldValue<DateTimeOffset>(5) }
            reader.Close()
            Some period
        else
            reader.Close()
            None
