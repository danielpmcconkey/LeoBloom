namespace LeoBloom.Portfolio

open Npgsql

/// Raw SQL persistence for portfolio dimension table lookups.
module DimensionRepository =

    let private readIdName (reader: System.Data.Common.DbDataReader) : int * string =
        (reader.GetInt32(0), reader.GetString(1))

    let private listDimension (txn: NpgsqlTransaction) (table: string) : (int * string) list =
        use sql = new NpgsqlCommand(
            sprintf "SELECT id, name FROM portfolio.%s ORDER BY id" table,
            txn.Connection, txn)
        use reader = sql.ExecuteReader()
        let results = ResizeArray<int * string>()
        while reader.Read() do
            results.Add(readIdName reader)
        reader.Close()
        results |> Seq.toList

    let listTaxBuckets        (txn: NpgsqlTransaction) = listDimension txn "tax_bucket"
    let listAccountGroups     (txn: NpgsqlTransaction) = listDimension txn "account_group"
    let listInvestmentTypes   (txn: NpgsqlTransaction) = listDimension txn "dim_investment_type"
    let listMarketCaps        (txn: NpgsqlTransaction) = listDimension txn "dim_market_cap"
    let listIndexTypes        (txn: NpgsqlTransaction) = listDimension txn "dim_index_type"
    let listSectors           (txn: NpgsqlTransaction) = listDimension txn "dim_sector"
    let listRegions           (txn: NpgsqlTransaction) = listDimension txn "dim_region"
    let listObjectives        (txn: NpgsqlTransaction) = listDimension txn "dim_objective"
