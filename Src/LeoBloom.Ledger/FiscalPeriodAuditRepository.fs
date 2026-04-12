namespace LeoBloom.Ledger

open Npgsql
open LeoBloom.Domain.Ledger

/// Raw SQL persistence for fiscal period audit trail. All operations run within
/// a caller-provided NpgsqlTransaction for atomicity.
module FiscalPeriodAuditRepository =

    let private readAuditEntry (reader: System.Data.Common.DbDataReader) : FiscalPeriodAuditEntry =
        { id = reader.GetInt32(0)
          fiscalPeriodId = reader.GetInt32(1)
          action = reader.GetString(2)
          actor = reader.GetString(3)
          occurredAt = reader.GetFieldValue<System.DateTimeOffset>(4)
          note = if reader.IsDBNull(5) then None else Some (reader.GetString(5)) }

    /// Insert a new audit entry. Returns the created record.
    let insert
        (txn: NpgsqlTransaction)
        (entry: {| fiscalPeriodId: int; action: string; actor: string; note: string option |})
        : FiscalPeriodAuditEntry =
        use sql = new NpgsqlCommand(
            "INSERT INTO ledger.fiscal_period_audit (fiscal_period_id, action, actor, note)
             VALUES (@fiscal_period_id, @action, @actor, @note)
             RETURNING id, fiscal_period_id, action, actor, occurred_at, note",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@fiscal_period_id", entry.fiscalPeriodId) |> ignore
        sql.Parameters.AddWithValue("@action", entry.action) |> ignore
        sql.Parameters.AddWithValue("@actor", entry.actor) |> ignore
        match entry.note with
        | Some n -> sql.Parameters.AddWithValue("@note", n) |> ignore
        | None   -> sql.Parameters.AddWithValue("@note", System.DBNull.Value) |> ignore
        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let result = readAuditEntry reader
        reader.Close()
        result

    /// List all audit entries for the given fiscal period, ordered by occurred_at ASC.
    let listByPeriod (txn: NpgsqlTransaction) (periodId: int) : FiscalPeriodAuditEntry list =
        use sql = new NpgsqlCommand(
            "SELECT id, fiscal_period_id, action, actor, occurred_at, note
             FROM ledger.fiscal_period_audit
             WHERE fiscal_period_id = @fiscal_period_id
             ORDER BY occurred_at ASC",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@fiscal_period_id", periodId) |> ignore
        use reader = sql.ExecuteReader()
        let results = System.Collections.Generic.List<FiscalPeriodAuditEntry>()
        while reader.Read() do
            results.Add(readAuditEntry reader)
        reader.Close()
        results |> Seq.toList
