namespace LeoBloom.Ops

open System
open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Utilities

type ListInvoicesFilter =
    { tenant: string option
      fiscalPeriodId: int option }

/// Raw SQL persistence for invoice records. All operations run within
/// a caller-provided NpgsqlTransaction for atomicity.
module InvoiceRepository =

    let private mapReader (reader: System.Data.Common.DbDataReader) : Invoice =
        { id = reader.GetInt32(0)
          tenant = reader.GetString(1)
          fiscalPeriodId = reader.GetInt32(2)
          rentAmount = reader.GetDecimal(3)
          utilityShare = reader.GetDecimal(4)
          totalAmount = reader.GetDecimal(5)
          generatedAt = reader.GetFieldValue<DateTimeOffset>(6)
          documentPath = if reader.IsDBNull(7) then None else Some (reader.GetString(7))
          notes = if reader.IsDBNull(8) then None else Some (reader.GetString(8))
          isActive = reader.GetBoolean(9)
          createdAt = reader.GetFieldValue<DateTimeOffset>(10)
          modifiedAt = reader.GetFieldValue<DateTimeOffset>(11) }

    let private selectColumns =
        "id, tenant, fiscal_period_id, rent_amount, utility_share, total_amount, \
         generated_at, document_path, notes, is_active, created_at, modified_at"

    let insert (txn: NpgsqlTransaction) (cmd: RecordInvoiceCommand) : Invoice =
        use sql = new NpgsqlCommand(
            $"INSERT INTO ops.invoice \
              (tenant, fiscal_period_id, rent_amount, utility_share, total_amount, \
               generated_at, document_path, notes) \
              VALUES (@tenant, @fiscal_period_id, @rent_amount, @utility_share, @total_amount, \
                      @generated_at, @document_path, @notes) \
              RETURNING {selectColumns}",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@tenant", cmd.tenant) |> ignore
        sql.Parameters.AddWithValue("@fiscal_period_id", cmd.fiscalPeriodId) |> ignore
        sql.Parameters.AddWithValue("@rent_amount", cmd.rentAmount) |> ignore
        sql.Parameters.AddWithValue("@utility_share", cmd.utilityShare) |> ignore
        sql.Parameters.AddWithValue("@total_amount", cmd.totalAmount) |> ignore
        sql.Parameters.AddWithValue("@generated_at", cmd.generatedAt) |> ignore
        DataHelpers.optParam "@document_path" (cmd.documentPath |> Option.map (fun v -> v :> obj)) sql
        DataHelpers.optParam "@notes" (cmd.notes |> Option.map (fun v -> v :> obj)) sql

        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let result = mapReader reader
        reader.Close()
        result

    let findById (txn: NpgsqlTransaction) (id: int) : Invoice option =
        use sql = new NpgsqlCommand(
            $"SELECT {selectColumns} FROM ops.invoice WHERE id = @id",
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

    let findByTenantAndPeriod (txn: NpgsqlTransaction) (tenant: string) (fiscalPeriodId: int) : Invoice option =
        use sql = new NpgsqlCommand(
            $"SELECT {selectColumns} FROM ops.invoice \
              WHERE tenant = @tenant AND fiscal_period_id = @fiscal_period_id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@tenant", tenant) |> ignore
        sql.Parameters.AddWithValue("@fiscal_period_id", fiscalPeriodId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let result = mapReader reader
            reader.Close()
            Some result
        else
            reader.Close()
            None

    let list (txn: NpgsqlTransaction) (filter: ListInvoicesFilter) : Invoice list =
        let mutable clauses = [ "is_active = true" ]
        let mutable paramList : (string * obj) list = []

        match filter.tenant with
        | Some t ->
            clauses <- clauses @ [ "tenant = @tenant" ]
            paramList <- paramList @ [ ("@tenant", t :> obj) ]
        | None -> ()

        match filter.fiscalPeriodId with
        | Some fpId ->
            clauses <- clauses @ [ "fiscal_period_id = @fiscal_period_id" ]
            paramList <- paramList @ [ ("@fiscal_period_id", fpId :> obj) ]
        | None -> ()

        let whereClause = " WHERE " + (clauses |> String.concat " AND ")

        use sql = new NpgsqlCommand(
            $"SELECT {selectColumns} FROM ops.invoice{whereClause} ORDER BY id DESC",
            txn.Connection, txn)

        for (name, value) in paramList do
            sql.Parameters.AddWithValue(name, value) |> ignore

        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            results <- mapReader reader :: results
        reader.Close()
        results |> List.rev
