namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger

/// Read-only query for account balance calculation.
module AccountBalanceRepository =

    let private readAccount (reader: System.Data.Common.DbDataReader) : Account =
        let subTypeStr =
            if reader.IsDBNull(5) then None
            else Some (reader.GetString(5))
        let subType =
            subTypeStr
            |> Option.bind (fun s ->
                match AccountSubType.fromDbString s with
                | Ok st -> Some st
                | Error _ -> None)
        { id = reader.GetInt32(0)
          code = reader.GetString(1)
          name = reader.GetString(2)
          accountTypeId = reader.GetInt32(3)
          parentId =
              if reader.IsDBNull(4) then None else Some (reader.GetInt32(4))
          subType = subType
          isActive = reader.GetBoolean(6)
          createdAt = reader.GetFieldValue<DateTimeOffset>(7)
          modifiedAt = reader.GetFieldValue<DateTimeOffset>(8) }

    /// List accounts with optional type filter and inactive toggle.
    /// When includeInactive=false, only is_active=true rows are returned.
    /// When accountTypeName is Some, JOINs to account_type and filters by name.
    let listAccounts
        (txn: NpgsqlTransaction)
        (accountTypeName: string option)
        (includeInactive: bool)
        : Account list =
        let baseSql =
            "SELECT a.id, a.code, a.name, a.account_type_id, a.parent_id,
                    a.account_subtype, a.is_active, a.created_at, a.modified_at
             FROM ledger.account a
             JOIN ledger.account_type at ON a.account_type_id = at.id
             WHERE 1=1"
        let typeClause =
            match accountTypeName with
            | Some _ -> " AND at.name = @type_name"
            | None -> ""
        let activeClause =
            if includeInactive then "" else " AND a.is_active = true"
        let sql = baseSql + typeClause + activeClause + " ORDER BY a.code"
        use cmd = new NpgsqlCommand(sql, txn.Connection, txn)
        match accountTypeName with
        | Some name -> cmd.Parameters.AddWithValue("@type_name", name) |> ignore
        | None -> ()
        use reader = cmd.ExecuteReader()
        let results = ResizeArray<Account>()
        while reader.Read() do
            results.Add(readAccount reader)
        reader.Close()
        results |> Seq.toList

    /// Find a single account by ID. Returns full Account record.
    let findAccountById (txn: NpgsqlTransaction) (accountId: int) : Account option =
        use cmd = new NpgsqlCommand(
            "SELECT a.id, a.code, a.name, a.account_type_id, a.parent_id,
                    a.account_subtype, a.is_active, a.created_at, a.modified_at
             FROM ledger.account a
             WHERE a.id = @id",
            txn.Connection, txn)
        cmd.Parameters.AddWithValue("@id", accountId) |> ignore
        use reader = cmd.ExecuteReader()
        if reader.Read() then
            let result = readAccount reader
            reader.Close()
            Some result
        else
            reader.Close()
            None

    /// Find a single account by code. Returns full Account record.
    let findAccountByCode (txn: NpgsqlTransaction) (code: string) : Account option =
        use cmd = new NpgsqlCommand(
            "SELECT a.id, a.code, a.name, a.account_type_id, a.parent_id,
                    a.account_subtype, a.is_active, a.created_at, a.modified_at
             FROM ledger.account a
             WHERE a.code = @code",
            txn.Connection, txn)
        cmd.Parameters.AddWithValue("@code", code) |> ignore
        use reader = cmd.ExecuteReader()
        if reader.Read() then
            let result = readAccount reader
            reader.Close()
            Some result
        else
            reader.Close()
            None

    let getBalance (txn: NpgsqlTransaction) (accountId: int) (asOfDate: DateOnly) : AccountBalance option =
        use sql = new NpgsqlCommand(
            "SELECT a.id, a.code, a.name, at.normal_balance,
                    COALESCE(SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE 0 END), 0)
                        - COALESCE(SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END), 0)
                        AS raw_balance
             FROM ledger.account a
             JOIN ledger.account_type at ON a.account_type_id = at.id
             LEFT JOIN (ledger.journal_entry_line jel
                 JOIN ledger.journal_entry je ON jel.journal_entry_id = je.id
                     AND je.voided_at IS NULL
                     AND je.entry_date <= @as_of_date)
                 ON jel.account_id = a.id
             WHERE a.id = @account_id
             GROUP BY a.id, a.code, a.name, at.normal_balance",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@account_id", accountId) |> ignore
        sql.Parameters.AddWithValue("@as_of_date", asOfDate) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let nb = match reader.GetString(3) with "debit" -> NormalBalance.Debit | _ -> NormalBalance.Credit
            let rawBalance = reader.GetDecimal(4)
            let balance = match nb with NormalBalance.Debit -> rawBalance | NormalBalance.Credit -> -rawBalance
            let result =
                Some { accountId = reader.GetInt32(0)
                       accountCode = reader.GetString(1)
                       accountName = reader.GetString(2)
                       normalBalance = nb
                       balance = balance
                       asOfDate = asOfDate }
            reader.Close()
            result
        else
            reader.Close()
            None

    let resolveAccountId (txn: NpgsqlTransaction) (code: string) : int option =
        use sql = new NpgsqlCommand(
            "SELECT id FROM ledger.account WHERE code = @code",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@code", code) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let id = reader.GetInt32(0)
            reader.Close()
            Some id
        else
            reader.Close()
            None
