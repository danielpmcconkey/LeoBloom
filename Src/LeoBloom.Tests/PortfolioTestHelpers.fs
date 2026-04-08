module LeoBloom.Tests.PortfolioTestHelpers

open System
open Npgsql
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Portfolio insert helpers — execute within caller's transaction
// =====================================================================

module PortfolioInsertHelpers =

    let insertTaxBucket (txn: NpgsqlTransaction) (name: string) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.tax_bucket (name) VALUES (@n) RETURNING id", txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertAccountGroup (txn: NpgsqlTransaction) (name: string) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.account_group (name) VALUES (@n) RETURNING id", txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertInvestmentAccount
        (txn: NpgsqlTransaction)
        (name: string) (taxBucketId: int) (accountGroupId: int) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.investment_account (name, tax_bucket_id, account_group_id)
             VALUES (@n, @tb, @ag) RETURNING id", txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@n",  name)           |> ignore
        cmd.Parameters.AddWithValue("@tb", taxBucketId)    |> ignore
        cmd.Parameters.AddWithValue("@ag", accountGroupId) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertFund (txn: NpgsqlTransaction) (symbol: string) (name: string) : string =
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.fund (symbol, name) VALUES (@s, @n) RETURNING symbol", txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@s", symbol) |> ignore
        cmd.Parameters.AddWithValue("@n", name)   |> ignore
        cmd.ExecuteScalar() :?> string

    let insertPosition
        (txn: NpgsqlTransaction)
        (accountId: int) (symbol: string) (date: DateOnly)
        (price: decimal) (qty: decimal) (cv: decimal) (cb: decimal) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.position
               (investment_account_id, symbol, position_date, price, quantity, current_value, cost_basis)
             VALUES (@a, @s, @d, @p, @q, @cv, @cb) RETURNING id", txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@a",  accountId) |> ignore
        cmd.Parameters.AddWithValue("@s",  symbol)    |> ignore
        cmd.Parameters.AddWithValue("@d",  date)      |> ignore
        cmd.Parameters.AddWithValue("@p",  price)     |> ignore
        cmd.Parameters.AddWithValue("@q",  qty)       |> ignore
        cmd.Parameters.AddWithValue("@cv", cv)        |> ignore
        cmd.Parameters.AddWithValue("@cb", cb)        |> ignore
        cmd.ExecuteScalar() :?> int
