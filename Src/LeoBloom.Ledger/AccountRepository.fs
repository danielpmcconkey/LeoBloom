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
          externalRef  = if reader.IsDBNull(6) then None else Some (reader.GetString(6))
          isActive     = reader.GetBoolean(7)
          createdAt    = reader.GetFieldValue<DateTimeOffset>(8)
          modifiedAt   = reader.GetFieldValue<DateTimeOffset>(9) }

    let private accountSelectSql =
        "SELECT id, code, name, account_type_id, parent_id, account_subtype,
                external_ref, is_active, created_at, modified_at
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

    /// Look up the name of an account type by ID. Returns None if not found.
    let findAccountTypeNameById (txn: NpgsqlTransaction) (accountTypeId: int) : string option =
        use sql = new NpgsqlCommand(
            "SELECT name FROM ledger.account_type WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", accountTypeId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let name = reader.GetString(0)
            reader.Close()
            Some name
        else
            reader.Close()
            None

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
        (subType: AccountSubType option)
        (externalRef: string option)
        : Account =
        // Build column/value lists dynamically to avoid combinatorial match explosion.
        let columns = System.Collections.Generic.List<string>()
        let placeholders = System.Collections.Generic.List<string>()
        columns.Add("code");        placeholders.Add("@code")
        columns.Add("name");        placeholders.Add("@name")
        columns.Add("account_type_id"); placeholders.Add("@typeId")
        if parentId.IsSome  then columns.Add("parent_id");       placeholders.Add("@parentId")
        if subType.IsSome   then columns.Add("account_subtype"); placeholders.Add("@subType")
        if externalRef.IsSome then columns.Add("external_ref"); placeholders.Add("@externalRef")
        let colList = String.concat ", " columns
        let valList = String.concat ", " placeholders
        let returning =
            "RETURNING id, code, name, account_type_id, parent_id, account_subtype,
                       external_ref, is_active, created_at, modified_at"
        let sql = sprintf "INSERT INTO ledger.account (%s) VALUES (%s) %s" colList valList returning
        use cmd = new NpgsqlCommand(sql, txn.Connection, txn)
        cmd.Parameters.AddWithValue("@code", code) |> ignore
        cmd.Parameters.AddWithValue("@name", name) |> ignore
        cmd.Parameters.AddWithValue("@typeId", accountTypeId) |> ignore
        match parentId with
        | Some pid -> cmd.Parameters.AddWithValue("@parentId", pid) |> ignore
        | None -> ()
        match subType with
        | Some st -> cmd.Parameters.AddWithValue("@subType", AccountSubType.toDbString st) |> ignore
        | None -> ()
        match externalRef with
        | Some ref -> cmd.Parameters.AddWithValue("@externalRef", ref) |> ignore
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
                       external_ref, is_active, created_at, modified_at",
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
                       external_ref, is_active, created_at, modified_at",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", accountId) |> ignore
        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let acct = readAccount reader
        reader.Close()
        acct
