namespace LeoBloom.Ops

open System
open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Utilities

type ListTransfersFilter =
    { status: TransferStatus option
      fromDate: DateOnly option
      toDate: DateOnly option }

/// Raw SQL persistence for transfer operations. All operations run within
/// a caller-provided NpgsqlTransaction for atomicity.
module TransferRepository =

    let private mapReader (reader: System.Data.Common.DbDataReader) : Transfer =
        let status =
            match TransferStatus.fromString (reader.GetString(4)) with
            | Ok v -> v
            | Error msg -> failwithf "Corrupt transfer status in DB: %s" msg
        { id = reader.GetInt32(0)
          fromAccountId = reader.GetInt32(1)
          toAccountId = reader.GetInt32(2)
          amount = reader.GetDecimal(3)
          status = status
          initiatedDate = reader.GetFieldValue<DateOnly>(5)
          expectedSettlement = if reader.IsDBNull(6) then None else Some (reader.GetFieldValue<DateOnly>(6))
          confirmedDate = if reader.IsDBNull(7) then None else Some (reader.GetFieldValue<DateOnly>(7))
          journalEntryId = if reader.IsDBNull(8) then None else Some (reader.GetInt32(8))
          description = if reader.IsDBNull(9) then None else Some (reader.GetString(9))
          isActive = reader.GetBoolean(10)
          createdAt = reader.GetFieldValue<DateTimeOffset>(11)
          modifiedAt = reader.GetFieldValue<DateTimeOffset>(12) }

    let private selectColumns =
        "id, from_account_id, to_account_id, amount, status, initiated_date, \
         expected_settlement, confirmed_date, journal_entry_id, description, \
         is_active, created_at, modified_at"

    let insert (txn: NpgsqlTransaction) (cmd: InitiateTransferCommand) : Transfer =
        use sql = new NpgsqlCommand(
            $"INSERT INTO ops.transfer \
              (from_account_id, to_account_id, amount, status, initiated_date, \
               expected_settlement, description) \
              VALUES (@from_account_id, @to_account_id, @amount, 'initiated', @initiated_date, \
                      @expected_settlement, @description) \
              RETURNING {selectColumns}",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@from_account_id", cmd.fromAccountId) |> ignore
        sql.Parameters.AddWithValue("@to_account_id", cmd.toAccountId) |> ignore
        sql.Parameters.AddWithValue("@amount", cmd.amount) |> ignore
        sql.Parameters.AddWithValue("@initiated_date", cmd.initiatedDate) |> ignore
        DataHelpers.optParam "@expected_settlement" (cmd.expectedSettlement |> Option.map (fun v -> v :> obj)) sql
        DataHelpers.optParam "@description" (cmd.description |> Option.map (fun v -> v :> obj)) sql

        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let result = mapReader reader
        reader.Close()
        result

    let findById (txn: NpgsqlTransaction) (id: int) : Transfer option =
        use sql = new NpgsqlCommand(
            $"SELECT {selectColumns} FROM ops.transfer WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", id) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let result = mapReader reader
            reader.Close()
            Some result
        else
            reader.Close()
            None

    let updateConfirm (txn: NpgsqlTransaction) (id: int) (confirmedDate: DateOnly) (journalEntryId: int) : Transfer =
        use sql = new NpgsqlCommand(
            $"UPDATE ops.transfer SET \
              status = 'confirmed', \
              confirmed_date = @confirmed_date, \
              journal_entry_id = @journal_entry_id, \
              modified_at = now() \
              WHERE id = @id \
              RETURNING {selectColumns}",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@confirmed_date", confirmedDate) |> ignore
        sql.Parameters.AddWithValue("@journal_entry_id", journalEntryId) |> ignore
        sql.Parameters.AddWithValue("@id", id) |> ignore

        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let result = mapReader reader
        reader.Close()
        result

    let list (txn: NpgsqlTransaction) (filter: ListTransfersFilter) : Transfer list =
        let mutable clauses = [ "is_active = true" ]
        let mutable paramList : (string * obj) list = []

        match filter.status with
        | Some s ->
            clauses <- clauses @ [ "status = @status" ]
            paramList <- paramList @ [ ("@status", TransferStatus.toString s :> obj) ]
        | None -> ()

        match filter.fromDate with
        | Some d ->
            clauses <- clauses @ [ "initiated_date >= @from_date" ]
            paramList <- paramList @ [ ("@from_date", d :> obj) ]
        | None -> ()

        match filter.toDate with
        | Some d ->
            clauses <- clauses @ [ "initiated_date <= @to_date" ]
            paramList <- paramList @ [ ("@to_date", d :> obj) ]
        | None -> ()

        let whereClause = " WHERE " + (clauses |> String.concat " AND ")

        use sql = new NpgsqlCommand(
            $"SELECT {selectColumns} FROM ops.transfer{whereClause} ORDER BY id DESC",
            txn.Connection, txn)

        for (name, value) in paramList do
            sql.Parameters.AddWithValue(name, value) |> ignore

        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            results <- mapReader reader :: results
        reader.Close()
        results |> List.rev
