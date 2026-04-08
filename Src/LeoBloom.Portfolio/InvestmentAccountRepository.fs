namespace LeoBloom.Portfolio

open Npgsql
open LeoBloom.Domain.Portfolio

/// Raw SQL persistence for investment account operations.
module InvestmentAccountRepository =

    let private readAccount (reader: System.Data.Common.DbDataReader) : InvestmentAccount =
        { id             = reader.GetInt32(0)
          name           = reader.GetString(1)
          taxBucketId    = reader.GetInt32(2)
          accountGroupId = reader.GetInt32(3) }

    /// Find an investment account by ID.
    let findById (txn: NpgsqlTransaction) (accountId: int) : InvestmentAccount option =
        use sql = new NpgsqlCommand(
            "SELECT id, name, tax_bucket_id, account_group_id
             FROM portfolio.investment_account WHERE id = @id",
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

    /// Insert a new investment account. Returns the created record.
    let create
        (txn: NpgsqlTransaction)
        (name: string)
        (taxBucketId: int)
        (accountGroupId: int)
        : InvestmentAccount =
        use sql = new NpgsqlCommand(
            "INSERT INTO portfolio.investment_account (name, tax_bucket_id, account_group_id)
             VALUES (@name, @tb, @ag)
             RETURNING id, name, tax_bucket_id, account_group_id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@name", name) |> ignore
        sql.Parameters.AddWithValue("@tb", taxBucketId) |> ignore
        sql.Parameters.AddWithValue("@ag", accountGroupId) |> ignore
        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let acct = readAccount reader
        reader.Close()
        acct

    /// List all investment accounts, ordered by id.
    let listAll (txn: NpgsqlTransaction) : InvestmentAccount list =
        use sql = new NpgsqlCommand(
            "SELECT id, name, tax_bucket_id, account_group_id
             FROM portfolio.investment_account
             ORDER BY id",
            txn.Connection, txn)
        use reader = sql.ExecuteReader()
        let results = ResizeArray<InvestmentAccount>()
        while reader.Read() do
            results.Add(readAccount reader)
        reader.Close()
        results |> Seq.toList

    /// List investment accounts filtered by account group name (JOIN).
    let listByGroup (txn: NpgsqlTransaction) (groupName: string) : InvestmentAccount list =
        use sql = new NpgsqlCommand(
            "SELECT ia.id, ia.name, ia.tax_bucket_id, ia.account_group_id
             FROM portfolio.investment_account ia
             JOIN portfolio.account_group ag ON ia.account_group_id = ag.id
             WHERE ag.name = @name
             ORDER BY ia.id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@name", groupName) |> ignore
        use reader = sql.ExecuteReader()
        let results = ResizeArray<InvestmentAccount>()
        while reader.Read() do
            results.Add(readAccount reader)
        reader.Close()
        results |> Seq.toList

    /// List investment accounts filtered by tax bucket name (JOIN).
    let listByTaxBucket (txn: NpgsqlTransaction) (bucketName: string) : InvestmentAccount list =
        use sql = new NpgsqlCommand(
            "SELECT ia.id, ia.name, ia.tax_bucket_id, ia.account_group_id
             FROM portfolio.investment_account ia
             JOIN portfolio.tax_bucket tb ON ia.tax_bucket_id = tb.id
             WHERE tb.name = @name
             ORDER BY ia.id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@name", bucketName) |> ignore
        use reader = sql.ExecuteReader()
        let results = ResizeArray<InvestmentAccount>()
        while reader.Read() do
            results.Add(readAccount reader)
        reader.Close()
        results |> Seq.toList
