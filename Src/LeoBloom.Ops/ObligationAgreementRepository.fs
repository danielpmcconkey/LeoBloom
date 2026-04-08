namespace LeoBloom.Ops

open System
open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Utilities

type ListAgreementsFilter =
    { isActive: bool option
      obligationType: ObligationDirection option
      cadence: RecurrenceCadence option }

/// Raw SQL persistence for obligation agreements. All operations run within
/// a caller-provided NpgsqlTransaction for atomicity.
module ObligationAgreementRepository =

    let private readAgreement (reader: System.Data.Common.DbDataReader) : ObligationAgreement =
        let obligationType =
            match ObligationDirection.fromString (reader.GetString(2)) with
            | Ok v -> v
            | Error msg -> failwithf "Corrupt obligation_type in DB: %s" msg
        let cadence =
            match RecurrenceCadence.fromString (reader.GetString(5)) with
            | Ok v -> v
            | Error msg -> failwithf "Corrupt cadence in DB: %s" msg
        let paymentMethod =
            if reader.IsDBNull(7) then None
            else
                match PaymentMethodType.fromString (reader.GetString(7)) with
                | Ok v -> Some v
                | Error msg -> failwithf "Corrupt payment_method in DB: %s" msg
        { id = reader.GetInt32(0)
          name = reader.GetString(1)
          obligationType = obligationType
          counterparty = if reader.IsDBNull(3) then None else Some (reader.GetString(3))
          amount = if reader.IsDBNull(4) then None else Some (reader.GetDecimal(4))
          cadence = cadence
          expectedDay = if reader.IsDBNull(6) then None else Some (reader.GetInt32(6))
          paymentMethod = paymentMethod
          sourceAccountId = if reader.IsDBNull(8) then None else Some (reader.GetInt32(8))
          destAccountId = if reader.IsDBNull(9) then None else Some (reader.GetInt32(9))
          isActive = reader.GetBoolean(10)
          notes = if reader.IsDBNull(11) then None else Some (reader.GetString(11))
          createdAt = reader.GetFieldValue<DateTimeOffset>(12)
          modifiedAt = reader.GetFieldValue<DateTimeOffset>(13) }

    let private selectColumns =
        "id, name, obligation_type, counterparty, amount, cadence, expected_day, \
         payment_method, source_account_id, dest_account_id, is_active, notes, \
         created_at, modified_at"

    let insert (txn: NpgsqlTransaction) (cmd: CreateObligationAgreementCommand) : ObligationAgreement =
        use sql = new NpgsqlCommand(
            $"INSERT INTO ops.obligation_agreement \
              (name, obligation_type, counterparty, amount, cadence, expected_day, \
               payment_method, source_account_id, dest_account_id, notes) \
              VALUES (@name, @obligation_type, @counterparty, @amount, @cadence, @expected_day, \
                      @payment_method, @source_account_id, @dest_account_id, @notes) \
              RETURNING {selectColumns}",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@name", cmd.name) |> ignore
        sql.Parameters.AddWithValue("@obligation_type", ObligationDirection.toString cmd.obligationType) |> ignore
        DataHelpers.optParam "@counterparty" (cmd.counterparty |> Option.map (fun v -> v :> obj)) sql
        DataHelpers.optParam "@amount" (cmd.amount |> Option.map (fun v -> v :> obj)) sql
        sql.Parameters.AddWithValue("@cadence", RecurrenceCadence.toString cmd.cadence) |> ignore
        DataHelpers.optParam "@expected_day" (cmd.expectedDay |> Option.map (fun v -> v :> obj)) sql
        DataHelpers.optParam "@payment_method" (cmd.paymentMethod |> Option.map (fun v -> PaymentMethodType.toString v :> obj)) sql
        DataHelpers.optParam "@source_account_id" (cmd.sourceAccountId |> Option.map (fun v -> v :> obj)) sql
        DataHelpers.optParam "@dest_account_id" (cmd.destAccountId |> Option.map (fun v -> v :> obj)) sql
        DataHelpers.optParam "@notes" (cmd.notes |> Option.map (fun v -> v :> obj)) sql

        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let result = readAgreement reader
        reader.Close()
        result

    let findById (txn: NpgsqlTransaction) (id: int) : ObligationAgreement option =
        use sql = new NpgsqlCommand(
            $"SELECT {selectColumns} FROM ops.obligation_agreement WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", id) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let result = readAgreement reader
            reader.Close()
            Some result
        else
            reader.Close()
            None

    let list (txn: NpgsqlTransaction) (filter: ListAgreementsFilter) : ObligationAgreement list =
        let mutable clauses = []
        let mutable paramList : (string * obj) list = []

        match filter.isActive with
        | Some active ->
            clauses <- clauses @ [ "is_active = @is_active" ]
            paramList <- paramList @ [ ("@is_active", active :> obj) ]
        | None -> ()

        match filter.obligationType with
        | Some ot ->
            clauses <- clauses @ [ "obligation_type = @obligation_type" ]
            paramList <- paramList @ [ ("@obligation_type", ObligationDirection.toString ot :> obj) ]
        | None -> ()

        match filter.cadence with
        | Some c ->
            clauses <- clauses @ [ "cadence = @cadence" ]
            paramList <- paramList @ [ ("@cadence", RecurrenceCadence.toString c :> obj) ]
        | None -> ()

        let whereClause =
            if clauses.IsEmpty then ""
            else " WHERE " + (clauses |> String.concat " AND ")

        use sql = new NpgsqlCommand(
            $"SELECT {selectColumns} FROM ops.obligation_agreement{whereClause} ORDER BY name",
            txn.Connection, txn)

        for (name, value) in paramList do
            sql.Parameters.AddWithValue(name, value) |> ignore

        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            results <- readAgreement reader :: results
        reader.Close()
        results |> List.rev

    let update (txn: NpgsqlTransaction) (cmd: UpdateObligationAgreementCommand) : ObligationAgreement option =
        use sql = new NpgsqlCommand(
            $"UPDATE ops.obligation_agreement SET \
              name = @name, \
              obligation_type = @obligation_type, \
              counterparty = @counterparty, \
              amount = @amount, \
              cadence = @cadence, \
              expected_day = @expected_day, \
              payment_method = @payment_method, \
              source_account_id = @source_account_id, \
              dest_account_id = @dest_account_id, \
              is_active = @is_active, \
              notes = @notes, \
              modified_at = now() \
              WHERE id = @id \
              RETURNING {selectColumns}",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", cmd.id) |> ignore
        sql.Parameters.AddWithValue("@name", cmd.name) |> ignore
        sql.Parameters.AddWithValue("@obligation_type", ObligationDirection.toString cmd.obligationType) |> ignore
        DataHelpers.optParam "@counterparty" (cmd.counterparty |> Option.map (fun v -> v :> obj)) sql
        DataHelpers.optParam "@amount" (cmd.amount |> Option.map (fun v -> v :> obj)) sql
        sql.Parameters.AddWithValue("@cadence", RecurrenceCadence.toString cmd.cadence) |> ignore
        DataHelpers.optParam "@expected_day" (cmd.expectedDay |> Option.map (fun v -> v :> obj)) sql
        DataHelpers.optParam "@payment_method" (cmd.paymentMethod |> Option.map (fun v -> PaymentMethodType.toString v :> obj)) sql
        DataHelpers.optParam "@source_account_id" (cmd.sourceAccountId |> Option.map (fun v -> v :> obj)) sql
        DataHelpers.optParam "@dest_account_id" (cmd.destAccountId |> Option.map (fun v -> v :> obj)) sql
        sql.Parameters.AddWithValue("@is_active", cmd.isActive) |> ignore
        DataHelpers.optParam "@notes" (cmd.notes |> Option.map (fun v -> v :> obj)) sql

        use reader = sql.ExecuteReader()
        if reader.Read() then
            let result = readAgreement reader
            reader.Close()
            Some result
        else
            reader.Close()
            None

    let deactivate (txn: NpgsqlTransaction) (id: int) : ObligationAgreement option =
        use sql = new NpgsqlCommand(
            $"UPDATE ops.obligation_agreement SET is_active = false, modified_at = now() \
              WHERE id = @id \
              RETURNING {selectColumns}",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", id) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let result = readAgreement reader
            reader.Close()
            Some result
        else
            reader.Close()
            None

    let hasActiveInstances (txn: NpgsqlTransaction) (agreementId: int) : bool =
        use sql = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM ops.obligation_instance \
             WHERE obligation_agreement_id = @id AND is_active = true)",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", agreementId) |> ignore
        sql.ExecuteScalar() :?> bool
