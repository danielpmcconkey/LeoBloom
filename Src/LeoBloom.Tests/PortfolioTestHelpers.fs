module LeoBloom.Tests.PortfolioTestHelpers

open System
open Npgsql
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Portfolio-specific tracker extension
// =====================================================================

type PortfolioTracker =
    { mutable PositionIds: int list
      mutable InvestmentAccountIds: int list
      mutable FundSymbols: string list
      mutable TaxBucketIds: int list
      mutable AccountGroupIds: int list
      Connection: NpgsqlConnection }

let createPortfolioTracker (conn: NpgsqlConnection) =
    { PositionIds = []
      InvestmentAccountIds = []
      FundSymbols = []
      TaxBucketIds = []
      AccountGroupIds = []
      Connection = conn }

let trackPosition id (t: PortfolioTracker) =
    t.PositionIds <- id :: t.PositionIds

let trackInvestmentAccount id (t: PortfolioTracker) =
    t.InvestmentAccountIds <- id :: t.InvestmentAccountIds

let trackFund symbol (t: PortfolioTracker) =
    t.FundSymbols <- symbol :: t.FundSymbols

let trackTaxBucket id (t: PortfolioTracker) =
    t.TaxBucketIds <- id :: t.TaxBucketIds

let trackAccountGroup id (t: PortfolioTracker) =
    t.AccountGroupIds <- id :: t.AccountGroupIds

/// Delete all tracked portfolio rows in FK-safe order.
let deletePortfolioAll (t: PortfolioTracker) =
    let tryDelete (table: string) (idColumn: string) (ids: int list) =
        if not ids.IsEmpty then
            try
                use cmd = new NpgsqlCommand(
                    sprintf "DELETE FROM %s WHERE %s = ANY(@ids)" table idColumn,
                    t.Connection)
                cmd.Parameters.AddWithValue("@ids", ids |> List.toArray) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex ->
                LeoBloom.Utilities.Log.errorExn ex "PortfolioTestCleanup failed to clean {Table}" [| table :> obj |]
    let tryDeleteStrings (table: string) (idColumn: string) (ids: string list) =
        if not ids.IsEmpty then
            try
                use cmd = new NpgsqlCommand(
                    sprintf "DELETE FROM %s WHERE %s = ANY(@ids)" table idColumn,
                    t.Connection)
                cmd.Parameters.AddWithValue("@ids", ids |> List.toArray) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex ->
                LeoBloom.Utilities.Log.errorExn ex "PortfolioTestCleanup failed to clean {Table}" [| table :> obj |]

    // FK-safe order: positions -> investment_accounts -> funds -> lookup tables
    tryDelete         "portfolio.position"           "id"            t.PositionIds
    // Also clean positions by account (in case position was created outside tracker)
    tryDelete         "portfolio.position"           "investment_account_id" t.InvestmentAccountIds
    tryDelete         "portfolio.investment_account" "id"            t.InvestmentAccountIds
    tryDeleteStrings  "portfolio.fund"               "symbol"        t.FundSymbols
    tryDelete         "portfolio.tax_bucket"         "id"            t.TaxBucketIds
    tryDelete         "portfolio.account_group"      "id"            t.AccountGroupIds

// =====================================================================
// Portfolio insert helpers
// =====================================================================

module PortfolioInsertHelpers =

    let insertTaxBucket (conn: NpgsqlConnection) (t: PortfolioTracker) (name: string) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.tax_bucket (name) VALUES (@n) RETURNING id", conn)
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        let id = cmd.ExecuteScalar() :?> int
        trackTaxBucket id t
        id

    let insertAccountGroup (conn: NpgsqlConnection) (t: PortfolioTracker) (name: string) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.account_group (name) VALUES (@n) RETURNING id", conn)
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        let id = cmd.ExecuteScalar() :?> int
        trackAccountGroup id t
        id

    let insertInvestmentAccount
        (conn: NpgsqlConnection) (t: PortfolioTracker)
        (name: string) (taxBucketId: int) (accountGroupId: int) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.investment_account (name, tax_bucket_id, account_group_id)
             VALUES (@n, @tb, @ag) RETURNING id", conn)
        cmd.Parameters.AddWithValue("@n",  name)           |> ignore
        cmd.Parameters.AddWithValue("@tb", taxBucketId)    |> ignore
        cmd.Parameters.AddWithValue("@ag", accountGroupId) |> ignore
        let id = cmd.ExecuteScalar() :?> int
        trackInvestmentAccount id t
        id

    let insertFund (conn: NpgsqlConnection) (t: PortfolioTracker) (symbol: string) (name: string) : string =
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.fund (symbol, name) VALUES (@s, @n) RETURNING symbol", conn)
        cmd.Parameters.AddWithValue("@s", symbol) |> ignore
        cmd.Parameters.AddWithValue("@n", name)   |> ignore
        let sym = cmd.ExecuteScalar() :?> string
        trackFund sym t
        sym

    let insertPosition
        (conn: NpgsqlConnection) (t: PortfolioTracker)
        (accountId: int) (symbol: string) (date: DateOnly)
        (price: decimal) (qty: decimal) (cv: decimal) (cb: decimal) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.position
               (investment_account_id, symbol, position_date, price, quantity, current_value, cost_basis)
             VALUES (@a, @s, @d, @p, @q, @cv, @cb) RETURNING id", conn)
        cmd.Parameters.AddWithValue("@a",  accountId) |> ignore
        cmd.Parameters.AddWithValue("@s",  symbol)    |> ignore
        cmd.Parameters.AddWithValue("@d",  date)      |> ignore
        cmd.Parameters.AddWithValue("@p",  price)     |> ignore
        cmd.Parameters.AddWithValue("@q",  qty)       |> ignore
        cmd.Parameters.AddWithValue("@cv", cv)        |> ignore
        cmd.Parameters.AddWithValue("@cb", cb)        |> ignore
        let id = cmd.ExecuteScalar() :?> int
        trackPosition id t
        id
