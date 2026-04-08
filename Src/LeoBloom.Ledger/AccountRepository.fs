namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger

/// Raw SQL persistence for account CRUD. All operations run within
/// a caller-provided NpgsqlTransaction for atomicity.
module AccountRepository =

    let private readAccount (reader: System.Data.Common.DbDataReader) : Account =
        { id           = reader.GetInt32(0)
          code         = reader.GetString(1)
          name         = reader.GetString(2)
          accountTypeId = reader.GetInt32(3)
          parentId     = if reader.IsDBNull(4) then None else Some (reader.GetInt32(4))
          subType      = if reader.IsDBNull(5) then None
                         else
                             match AccountSubType.fromDbString (reader.GetString(5)) with
                             | Ok st -> Some st
                             | Error _ -> None
          isActive     = reader.GetBoolean(6)
          createdAt    = reader.GetFieldValue<DateTimeOffset>(7)
          modifiedAt   = reader.GetFieldValue<DateTimeOffset>(8) }

    let private accountSelectSql =
        "SELECT id, code, name, account_type_id, parent_id, account_subtype,
                is_active, created_at, modified_at
         FROM ledger.account"

    /// Look up an account by ID. Returns None if not found.
    let findById (txn: NpgsqlTransaction) (accountId: int) : Account option =
        use sql = new NpgsqlCommand(
            accountSelectSql + " WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", accountId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let acct = readAccount reader
            reader.Close()
            Some acct
        else
            reader.Close()
            None

    /// Look up an account by code. Returns None if not found.
    let findByCode (txn: NpgsqlTransaction) (code: string) : Account option =
        use sql = new NpgsqlCommand(
            accountSelectSql + " WHERE code = @code",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@code", code) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let acct = readAccount reader
            reader.Close()
            Some acct
        else
            reader.Close()
            None

    /// Check whether an account_type row with the given id exists.
    let accountTypeExists (txn: NpgsqlTransaction) (accountTypeId: int) : bool =
        use sql = new NpgsqlCommand(
            "SELECT 1 FROM ledger.account_type WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", accountTypeId) |> ignore
        use reader = sql.ExecuteReader()
        let found = reader.Read()
        reader.Close()
        found

    /// Check whether an account has active child accounts.
    let hasChildren (txn: NpgsqlTransaction) (accountId: int) : bool =
        use sql = new NpgsqlCommand(
            "SELECT 1 FROM ledger.account WHERE parent_id = @id AND is_active = true LIMIT 1",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", accountId) |> ignore
        use reader = sql.ExecuteReader()
        let found = reader.Read()
        reader.Close()
        found

    /// Check whether any journal_entry_line rows reference this account.
    let hasPostedEntries (txn: NpgsqlTransaction) (accountId: int) : bool =
        use sql = new NpgsqlCommand(
            "SELECT 1 FROM ledger.journal_entry_line WHERE account_id = @id LIMIT 1",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", accountId) |> ignore
        use reader = sql.ExecuteReader()
        let found = reader.Read()
        reader.Close()
        found

    /// Insert a new account. Returns the created record.
    /// Caller is responsible for catching 23505 (duplicate code).
    let create
        (txn: NpgsqlTransaction)
        (code: string)
        (name: string)
        (accountTypeId: int)
        (parentId: int option)
        : Account =
        let sql =
            match parentId with
            | None ->
                "INSERT INTO ledger.account (code, name, account_type_id)
                 VALUES (@code, @name, @typeId)
                 RETURNING id, code, name, account_type_id, parent_id, account_subtype,
                           is_active, created_at, modified_at"
            | Some _ ->
                "INSERT INTO ledger.account (code, name, account_type_id, parent_id)
                 VALUES (@code, @name, @typeId, @parentId)
                 RETURNING id, code, name, account_type_id, parent_id, account_subtype,
                           is_active, created_at, modified_at"
        use cmd = new NpgsqlCommand(sql, txn.Connection, txn)
        cmd.Parameters.AddWithValue("@code", code) |> ignore
        cmd.Parameters.AddWithValue("@name", name) |> ignore
        cmd.Parameters.AddWithValue("@typeId", accountTypeId) |> ignore
        match parentId with
        | Some pid -> cmd.Parameters.AddWithValue("@parentId", pid) |> ignore
        | None -> ()
        use reader = cmd.ExecuteReader()
        reader.Read() |> ignore
        let acct = readAccount reader
        reader.Close()
        acct

    /// Update an account's name. Returns the updated record.
    let updateName (txn: NpgsqlTransaction) (accountId: int) (name: string) : Account =
        use sql = new NpgsqlCommand(
            "UPDATE ledger.account SET name = @name, modified_at = now()
             WHERE id = @id
             RETURNING id, code, name, account_type_id, parent_id, account_subtype,
                       is_active, created_at, modified_at",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@name", name) |> ignore
        sql.Parameters.AddWithValue("@id", accountId) |> ignore
        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let acct = readAccount reader
        reader.Close()
        acct

    /// Set is_active = false on an account. Returns the updated record.
    let deactivate (txn: NpgsqlTransaction) (accountId: int) : Account =
        use sql = new NpgsqlCommand(
            "UPDATE ledger.account SET is_active = false, modified_at = now()
             WHERE id = @id
             RETURNING id, code, name, account_type_id, parent_id, account_subtype,
                       is_active, created_at, modified_at",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", accountId) |> ignore
        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let acct = readAccount reader
        reader.Close()
        acct
