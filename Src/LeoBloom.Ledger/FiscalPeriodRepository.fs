namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger

/// Raw SQL persistence for fiscal period operations. All operations run within
/// a caller-provided NpgsqlTransaction for atomicity.
module FiscalPeriodRepository =

    let private readPeriod (reader: System.Data.Common.DbDataReader) : FiscalPeriod =
        { id = reader.GetInt32(0)
          periodKey = reader.GetString(1)
          startDate = reader.GetFieldValue<DateOnly>(2)
          endDate = reader.GetFieldValue<DateOnly>(3)
          isOpen = reader.GetBoolean(4)
          createdAt = reader.GetFieldValue<DateTimeOffset>(5) }

    /// Look up a fiscal period by ID. Returns None if not found.
    let findById (txn: NpgsqlTransaction) (periodId: int) : FiscalPeriod option =
        use sql = new NpgsqlCommand(
            "SELECT id, period_key, start_date, end_date, is_open, created_at
             FROM ledger.fiscal_period WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", periodId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let period = readPeriod reader
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
            let period = readPeriod reader
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
            let period = readPeriod reader
            reader.Close()
            Some period
        else
            reader.Close()
            None

    /// List all fiscal periods, ordered by start_date.
    let listAll (txn: NpgsqlTransaction) : FiscalPeriod list =
        use sql = new NpgsqlCommand(
            "SELECT id, period_key, start_date, end_date, is_open, created_at
             FROM ledger.fiscal_period
             ORDER BY start_date",
            txn.Connection, txn)
        use reader = sql.ExecuteReader()
        let results = ResizeArray<FiscalPeriod>()
        while reader.Read() do
            results.Add(readPeriod reader)
        reader.Close()
        results |> Seq.toList

    /// Find a fiscal period by period_key. Returns None if not found.
    let findByKey (txn: NpgsqlTransaction) (periodKey: string) : FiscalPeriod option =
        use sql = new NpgsqlCommand(
            "SELECT id, period_key, start_date, end_date, is_open, created_at
             FROM ledger.fiscal_period WHERE period_key = @key",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@key", periodKey) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let period = readPeriod reader
            reader.Close()
            Some period
        else
            reader.Close()
            None

    /// Find any existing fiscal period whose date range overlaps the proposed range.
    /// Returns None if no overlap exists, Some period if a conflict is found.
    let findOverlapping (txn: NpgsqlTransaction) (proposedStart: DateOnly) (proposedEnd: DateOnly) : FiscalPeriod option =
        use sql = new NpgsqlCommand(
            "SELECT id, period_key, start_date, end_date, is_open, created_at
             FROM ledger.fiscal_period
             WHERE start_date <= @proposedEnd AND end_date >= @proposedStart
             LIMIT 1",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@proposedStart", proposedStart) |> ignore
        sql.Parameters.AddWithValue("@proposedEnd", proposedEnd) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let period = readPeriod reader
            reader.Close()
            Some period
        else
            reader.Close()
            None

    /// Insert a new fiscal period. Returns the created record.
    let create
        (txn: NpgsqlTransaction)
        (periodKey: string)
        (startDate: DateOnly)
        (endDate: DateOnly)
        : FiscalPeriod =
        use sql = new NpgsqlCommand(
            "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date)
             VALUES (@key, @start, @end)
             RETURNING id, period_key, start_date, end_date, is_open, created_at",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@key", periodKey) |> ignore
        sql.Parameters.AddWithValue("@start", startDate) |> ignore
        sql.Parameters.AddWithValue("@end", endDate) |> ignore
        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let period = readPeriod reader
        reader.Close()
        period
