namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger

/// Raw SQL persistence for journal entries. All operations run within
/// a caller-provided NpgsqlTransaction for atomicity.
module JournalEntryRepository =

    let private optParam (name: string) (value: string option) (cmd: NpgsqlCommand) =
        match value with
        | Some v -> cmd.Parameters.AddWithValue(name, v :> obj) |> ignore
        | None -> cmd.Parameters.AddWithValue(name, DBNull.Value) |> ignore

    let insertEntry (txn: NpgsqlTransaction) (cmd: PostJournalEntryCommand) : JournalEntry =
        use sql = new NpgsqlCommand(
            "INSERT INTO ledger.journal_entry (entry_date, description, source, fiscal_period_id)
             VALUES (@entry_date, @description, @source, @fp_id)
             RETURNING id, entry_date, description, source, fiscal_period_id,
                       voided_at, void_reason, created_at, modified_at",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@entry_date", cmd.entryDate) |> ignore
        sql.Parameters.AddWithValue("@description", cmd.description) |> ignore
        optParam "@source" cmd.source sql
        sql.Parameters.AddWithValue("@fp_id", cmd.fiscalPeriodId) |> ignore

        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let entry =
            { id = reader.GetInt32(0)
              entryDate = reader.GetFieldValue<DateOnly>(1)
              description = reader.GetString(2)
              source = if reader.IsDBNull(3) then None else Some (reader.GetString(3))
              fiscalPeriodId = reader.GetInt32(4)
              voidedAt = if reader.IsDBNull(5) then None else Some (reader.GetFieldValue<DateTimeOffset>(5))
              voidReason = if reader.IsDBNull(6) then None else Some (reader.GetString(6))
              createdAt = reader.GetFieldValue<DateTimeOffset>(7)
              modifiedAt = reader.GetFieldValue<DateTimeOffset>(8) }
        reader.Close()
        entry

    let insertLines (txn: NpgsqlTransaction) (entryId: int) (lines: PostLineCommand list) : JournalEntryLine list =
        lines
        |> List.map (fun l ->
            use sql = new NpgsqlCommand(
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type, memo)
                 VALUES (@je_id, @acct, @amt, @et, @memo)
                 RETURNING id, journal_entry_id, account_id, amount, entry_type, memo",
                txn.Connection, txn)
            sql.Parameters.AddWithValue("@je_id", entryId) |> ignore
            sql.Parameters.AddWithValue("@acct", l.accountId) |> ignore
            sql.Parameters.AddWithValue("@amt", l.amount) |> ignore
            let etStr = EntryType.toDbString l.entryType
            sql.Parameters.AddWithValue("@et", etStr) |> ignore
            optParam "@memo" l.memo sql

            use reader = sql.ExecuteReader()
            reader.Read() |> ignore
            let line =
                { id = reader.GetInt32(0)
                  journalEntryId = reader.GetInt32(1)
                  accountId = reader.GetInt32(2)
                  amount = reader.GetDecimal(3)
                  entryType =
                      match reader.GetString(4) with
                      | "debit" -> EntryType.Debit
                      | _ -> EntryType.Credit
                  memo = if reader.IsDBNull(5) then None else Some (reader.GetString(5)) }
            reader.Close()
            line)

    let voidEntry (txn: NpgsqlTransaction) (entryId: int) (reason: string) : JournalEntry option =
        // Attempt to set voided_at on non-voided entries. If already voided, affects 0 rows.
        use upd = new NpgsqlCommand(
            "UPDATE ledger.journal_entry
             SET voided_at = now(), void_reason = @reason, modified_at = now()
             WHERE id = @id AND voided_at IS NULL",
            txn.Connection, txn)
        upd.Parameters.AddWithValue("@id", entryId) |> ignore
        upd.Parameters.AddWithValue("@reason", reason) |> ignore
        upd.ExecuteNonQuery() |> ignore

        // Return current state (whether we just updated or it was already voided)
        use sel = new NpgsqlCommand(
            "SELECT id, entry_date, description, source, fiscal_period_id,
                    voided_at, void_reason, created_at, modified_at
             FROM ledger.journal_entry WHERE id = @id",
            txn.Connection, txn)
        sel.Parameters.AddWithValue("@id", entryId) |> ignore
        use reader = sel.ExecuteReader()
        if reader.Read() then
            let entry =
                { id = reader.GetInt32(0)
                  entryDate = reader.GetFieldValue<DateOnly>(1)
                  description = reader.GetString(2)
                  source = if reader.IsDBNull(3) then None else Some (reader.GetString(3))
                  fiscalPeriodId = reader.GetInt32(4)
                  voidedAt = if reader.IsDBNull(5) then None else Some (reader.GetFieldValue<DateTimeOffset>(5))
                  voidReason = if reader.IsDBNull(6) then None else Some (reader.GetString(6))
                  createdAt = reader.GetFieldValue<DateTimeOffset>(7)
                  modifiedAt = reader.GetFieldValue<DateTimeOffset>(8) }
            reader.Close()
            Some entry
        else
            reader.Close()
            None

    let insertReferences (txn: NpgsqlTransaction) (entryId: int) (refs: PostReferenceCommand list) : JournalEntryReference list =
        refs
        |> List.map (fun r ->
            use sql = new NpgsqlCommand(
                "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value)
                 VALUES (@je_id, @rt, @rv)
                 RETURNING id, journal_entry_id, reference_type, reference_value, created_at",
                txn.Connection, txn)
            sql.Parameters.AddWithValue("@je_id", entryId) |> ignore
            sql.Parameters.AddWithValue("@rt", r.referenceType) |> ignore
            sql.Parameters.AddWithValue("@rv", r.referenceValue) |> ignore

            use reader = sql.ExecuteReader()
            reader.Read() |> ignore
            let ref' =
                { id = reader.GetInt32(0)
                  journalEntryId = reader.GetInt32(1)
                  referenceType = reader.GetString(2)
                  referenceValue = reader.GetString(3)
                  createdAt = reader.GetFieldValue<DateTimeOffset>(4) }
            reader.Close()
            ref')

    let findNonVoidedByReference
        (txn: NpgsqlTransaction)
        (referenceType: string)
        (referenceValue: string)
        : int option =
        use sql = new NpgsqlCommand(
            "SELECT je.id \
             FROM ledger.journal_entry_reference jer \
             JOIN ledger.journal_entry je ON je.id = jer.journal_entry_id \
             WHERE jer.reference_type = @rt \
               AND jer.reference_value = @rv \
               AND je.voided_at IS NULL \
             LIMIT 1",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@rt", referenceType) |> ignore
        sql.Parameters.AddWithValue("@rv", referenceValue) |> ignore
        use reader = sql.ExecuteReader()
        let result =
            if reader.Read() then Some (reader.GetInt32(0))
            else None
        reader.Close()
        result
