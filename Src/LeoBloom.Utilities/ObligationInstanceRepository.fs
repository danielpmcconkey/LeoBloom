namespace LeoBloom.Utilities

open System
open Npgsql
open NpgsqlTypes
open LeoBloom.Domain.Ops

/// Raw SQL persistence for obligation instances. All operations run within
/// a caller-provided NpgsqlTransaction for atomicity.
module ObligationInstanceRepository =

    let private optParam (name: string) (value: obj option) (cmd: NpgsqlCommand) =
        match value with
        | Some v -> cmd.Parameters.AddWithValue(name, v) |> ignore
        | None -> cmd.Parameters.AddWithValue(name, DBNull.Value) |> ignore

    let private selectColumns =
        "id, obligation_agreement_id, name, status, amount, expected_date, \
         confirmed_date, due_date, document_path, journal_entry_id, notes, \
         is_active, created_at, modified_at"

    let private mapReader (reader: System.Data.Common.DbDataReader) : ObligationInstance =
        let status =
            match InstanceStatus.fromString (reader.GetString(3)) with
            | Ok v -> v
            | Error msg -> failwithf "Corrupt status in DB: %s" msg
        { id = reader.GetInt32(0)
          obligationAgreementId = reader.GetInt32(1)
          name = reader.GetString(2)
          status = status
          amount = if reader.IsDBNull(4) then None else Some (reader.GetDecimal(4))
          expectedDate = reader.GetFieldValue<DateOnly>(5)
          confirmedDate = if reader.IsDBNull(6) then None else Some (reader.GetFieldValue<DateOnly>(6))
          dueDate = if reader.IsDBNull(7) then None else Some (reader.GetFieldValue<DateOnly>(7))
          documentPath = if reader.IsDBNull(8) then None else Some (reader.GetString(8))
          journalEntryId = if reader.IsDBNull(9) then None else Some (reader.GetInt32(9))
          notes = if reader.IsDBNull(10) then None else Some (reader.GetString(10))
          isActive = reader.GetBoolean(11)
          createdAt = reader.GetFieldValue<DateTimeOffset>(12)
          modifiedAt = reader.GetFieldValue<DateTimeOffset>(13) }

    let insert
        (txn: NpgsqlTransaction)
        (obligationAgreementId: int)
        (name: string)
        (status: InstanceStatus)
        (amount: decimal option)
        (expectedDate: DateOnly)
        : ObligationInstance =
        use sql = new NpgsqlCommand(
            $"INSERT INTO ops.obligation_instance \
              (obligation_agreement_id, name, status, amount, expected_date, is_active) \
              VALUES (@obligation_agreement_id, @name, @status, @amount, @expected_date, true) \
              RETURNING {selectColumns}",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@obligation_agreement_id", obligationAgreementId) |> ignore
        sql.Parameters.AddWithValue("@name", name) |> ignore
        sql.Parameters.AddWithValue("@status", InstanceStatus.toString status) |> ignore
        optParam "@amount" (amount |> Option.map (fun v -> v :> obj)) sql
        sql.Parameters.AddWithValue("@expected_date", expectedDate) |> ignore

        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let result = mapReader reader
        reader.Close()
        result

    let findExistingDates
        (txn: NpgsqlTransaction)
        (agreementId: int)
        (dates: DateOnly list)
        : DateOnly Set =
        if dates.IsEmpty then Set.empty
        else
            use sql = new NpgsqlCommand(
                "SELECT expected_date FROM ops.obligation_instance \
                 WHERE obligation_agreement_id = @id AND expected_date = ANY(@dates)",
                txn.Connection, txn)
            sql.Parameters.AddWithValue("@id", agreementId) |> ignore
            let param = NpgsqlParameter("@dates", NpgsqlDbType.Array ||| NpgsqlDbType.Date)
            param.Value <- dates |> List.map (fun d -> d :> obj) |> Array.ofList
            sql.Parameters.Add(param) |> ignore

            use reader = sql.ExecuteReader()
            let mutable result = Set.empty
            while reader.Read() do
                result <- result |> Set.add (reader.GetFieldValue<DateOnly>(0))
            reader.Close()
            result
