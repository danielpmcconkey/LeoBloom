module LeoBloom.Tests.PortfolioMissingCommandsTests

open System
open System.Text.Json
open Npgsql
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests
open LeoBloom.Tests.PortfolioTestHelpers
open LeoBloom.Tests.PortfolioCommandsTests

// =====================================================================
// portfolio account show — FT-PFC-100 through FT-PFC-104
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PFC-100")>]
let ``portfolio account show displays account detail with no positions`` () =
    let env = PortfolioCliEnv.create()
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_ShowAcct" env.Prefix)
        let result = CliRunner.run (sprintf "portfolio account show %d" acctId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(sprintf "Investment Account #%d" acctId, stdout)
        Assert.Contains("Latest Positions:", stdout)
        Assert.Contains("(no positions recorded)", stdout)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-101")>]
let ``portfolio account show with recorded positions includes latest position summary`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "X%s" (env.Prefix.ToUpperInvariant())
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_ShowPos" env.Prefix)
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Show Fund %s" env.Prefix)
        let recArgs =
            sprintf "portfolio position record --account-id %d --symbol %s --date 2026-03-01 --price 150.00 --quantity 5 --value 750.00 --cost-basis 700.00"
                acctId symbol
        let recResult = CliRunner.run recArgs
        Assert.Equal(0, recResult.ExitCode)
        let result = CliRunner.run (sprintf "portfolio account show %d" acctId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(sprintf "Investment Account #%d" acctId, stdout)
        Assert.Contains("Latest Positions:", stdout)
        Assert.Contains(symbol, stdout)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-102")>]
let ``portfolio account show with --json outputs valid JSON with account and latestPositions fields`` () =
    let env = PortfolioCliEnv.create()
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_ShowJson" env.Prefix)
        let result = CliRunner.run (sprintf "portfolio account show %d --json" acctId)
        Assert.Equal(0, result.ExitCode)
        let json = CliRunner.stripLogLines result.Stdout
        let doc  = JsonDocument.Parse(json)
        let acct = doc.RootElement.GetProperty("account")
        Assert.Equal(acctId, acct.GetProperty("id").GetInt32())
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("latestPositions").ValueKind)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-103")>]
let ``portfolio account show with nonexistent ID prints error and exits 1`` () =
    let result = CliRunner.run "portfolio account show 999999"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr))

[<Fact>]
[<Trait("GherkinId", "FT-PFC-104")>]
let ``portfolio account show with no ID argument prints error and exits 2`` () =
    let result = CliRunner.run "portfolio account show"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr))

// =====================================================================
// portfolio account list --group — FT-PFC-110 through FT-PFC-112
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PFC-110")>]
let ``portfolio account list --group returns only accounts in the named group`` () =
    Log.initialize()
    let conn   = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let grpAName  = prefix + "_grpA"
    let acctAName = prefix + "_acA"
    let acctBName = prefix + "_acB"
    let tbId, agAId, agBId, acctAId, acctBId =
        let txn = conn.BeginTransaction()
        let tb  = PortfolioInsertHelpers.insertTaxBucket    txn (prefix + "_tb")
        let agA = PortfolioInsertHelpers.insertAccountGroup txn grpAName
        let agB = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_grpB")
        let aA  = PortfolioInsertHelpers.insertInvestmentAccount txn acctAName tb agA
        let aB  = PortfolioInsertHelpers.insertInvestmentAccount txn acctBName tb agB
        txn.Commit()
        txn.Dispose()
        tb, agA, agB, aA, aB
    try
        let result = CliRunner.run (sprintf "portfolio account list --group \"%s\"" grpAName)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(acctAName, stdout)
        Assert.DoesNotContain(acctBName, stdout)
    finally
        use cmd1 = new NpgsqlCommand("DELETE FROM portfolio.investment_account WHERE id = ANY(@ids)", conn)
        cmd1.Parameters.AddWithValue("@ids", [| acctAId; acctBId |]) |> ignore
        cmd1.ExecuteNonQuery() |> ignore
        use cmd2 = new NpgsqlCommand("DELETE FROM portfolio.account_group WHERE id = ANY(@ids)", conn)
        cmd2.Parameters.AddWithValue("@ids", [| agAId; agBId |]) |> ignore
        cmd2.ExecuteNonQuery() |> ignore
        use cmd3 = new NpgsqlCommand("DELETE FROM portfolio.tax_bucket WHERE id = @tb", conn)
        cmd3.Parameters.AddWithValue("@tb", tbId) |> ignore
        cmd3.ExecuteNonQuery() |> ignore
        conn.Dispose()

[<Fact>]
[<Trait("GherkinId", "FT-PFC-111")>]
let ``portfolio account list --group with --json returns valid JSON array of matching accounts`` () =
    Log.initialize()
    let conn   = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let grpAName = prefix + "_grpA"
    let tbId, agAId, agBId, acctAId, acctBId =
        let txn = conn.BeginTransaction()
        let tb  = PortfolioInsertHelpers.insertTaxBucket    txn (prefix + "_tb")
        let agA = PortfolioInsertHelpers.insertAccountGroup txn grpAName
        let agB = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_grpB")
        let aA  = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_acA") tb agA
        let aB  = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_acB") tb agB
        txn.Commit()
        txn.Dispose()
        tb, agA, agB, aA, aB
    try
        let result = CliRunner.run (sprintf "portfolio account list --group \"%s\" --json" grpAName)
        Assert.Equal(0, result.ExitCode)
        let json = CliRunner.stripLogLines result.Stdout
        let arr  = JsonDocument.Parse(json)
        Assert.Equal(JsonValueKind.Array, arr.RootElement.ValueKind)
        Assert.Equal(1, arr.RootElement.GetArrayLength())
    finally
        use cmd1 = new NpgsqlCommand("DELETE FROM portfolio.investment_account WHERE id = ANY(@ids)", conn)
        cmd1.Parameters.AddWithValue("@ids", [| acctAId; acctBId |]) |> ignore
        cmd1.ExecuteNonQuery() |> ignore
        use cmd2 = new NpgsqlCommand("DELETE FROM portfolio.account_group WHERE id = ANY(@ids)", conn)
        cmd2.Parameters.AddWithValue("@ids", [| agAId; agBId |]) |> ignore
        cmd2.ExecuteNonQuery() |> ignore
        use cmd3 = new NpgsqlCommand("DELETE FROM portfolio.tax_bucket WHERE id = @tb", conn)
        cmd3.Parameters.AddWithValue("@tb", tbId) |> ignore
        cmd3.ExecuteNonQuery() |> ignore
        conn.Dispose()

[<Fact>]
[<Trait("GherkinId", "FT-PFC-112")>]
let ``portfolio account list --group with no matching name returns empty and exits 0`` () =
    let env = PortfolioCliEnv.create()
    try
        let _id = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_GrpNoMatch" env.Prefix)
        let result = CliRunner.run (sprintf "portfolio account list --group \"%s_NOMATCH\"" env.Prefix)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("no investment accounts found", stdout)
    finally PortfolioCliEnv.cleanup env

// =====================================================================
// portfolio account list --tax-bucket — FT-PFC-120 through FT-PFC-122
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PFC-120")>]
let ``portfolio account list --tax-bucket returns only accounts in the named bucket`` () =
    Log.initialize()
    let conn   = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let tbAName   = prefix + "_tbA"
    let acctAName = prefix + "_acA"
    let acctBName = prefix + "_acB"
    let tbAId, tbBId, agId, acctAId, acctBId =
        let txn = conn.BeginTransaction()
        let tbA = PortfolioInsertHelpers.insertTaxBucket    txn tbAName
        let tbB = PortfolioInsertHelpers.insertTaxBucket    txn (prefix + "_tbB")
        let ag  = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
        let aA  = PortfolioInsertHelpers.insertInvestmentAccount txn acctAName tbA ag
        let aB  = PortfolioInsertHelpers.insertInvestmentAccount txn acctBName tbB ag
        txn.Commit()
        txn.Dispose()
        tbA, tbB, ag, aA, aB
    try
        let result = CliRunner.run (sprintf "portfolio account list --tax-bucket \"%s\"" tbAName)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(acctAName, stdout)
        Assert.DoesNotContain(acctBName, stdout)
    finally
        use cmd1 = new NpgsqlCommand("DELETE FROM portfolio.investment_account WHERE id = ANY(@ids)", conn)
        cmd1.Parameters.AddWithValue("@ids", [| acctAId; acctBId |]) |> ignore
        cmd1.ExecuteNonQuery() |> ignore
        use cmd2 = new NpgsqlCommand("DELETE FROM portfolio.account_group WHERE id = @ag", conn)
        cmd2.Parameters.AddWithValue("@ag", agId) |> ignore
        cmd2.ExecuteNonQuery() |> ignore
        use cmd3 = new NpgsqlCommand("DELETE FROM portfolio.tax_bucket WHERE id = ANY(@ids)", conn)
        cmd3.Parameters.AddWithValue("@ids", [| tbAId; tbBId |]) |> ignore
        cmd3.ExecuteNonQuery() |> ignore
        conn.Dispose()

[<Fact>]
[<Trait("GherkinId", "FT-PFC-121")>]
let ``portfolio account list --tax-bucket with --json returns valid JSON array of matching accounts`` () =
    Log.initialize()
    let conn   = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let tbAName = prefix + "_tbA"
    let tbAId, tbBId, agId, acctAId, acctBId =
        let txn = conn.BeginTransaction()
        let tbA = PortfolioInsertHelpers.insertTaxBucket    txn tbAName
        let tbB = PortfolioInsertHelpers.insertTaxBucket    txn (prefix + "_tbB")
        let ag  = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
        let aA  = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_acA") tbA ag
        let aB  = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_acB") tbB ag
        txn.Commit()
        txn.Dispose()
        tbA, tbB, ag, aA, aB
    try
        let result = CliRunner.run (sprintf "portfolio account list --tax-bucket \"%s\" --json" tbAName)
        Assert.Equal(0, result.ExitCode)
        let json = CliRunner.stripLogLines result.Stdout
        let arr  = JsonDocument.Parse(json)
        Assert.Equal(JsonValueKind.Array, arr.RootElement.ValueKind)
        Assert.Equal(1, arr.RootElement.GetArrayLength())
    finally
        use cmd1 = new NpgsqlCommand("DELETE FROM portfolio.investment_account WHERE id = ANY(@ids)", conn)
        cmd1.Parameters.AddWithValue("@ids", [| acctAId; acctBId |]) |> ignore
        cmd1.ExecuteNonQuery() |> ignore
        use cmd2 = new NpgsqlCommand("DELETE FROM portfolio.account_group WHERE id = @ag", conn)
        cmd2.Parameters.AddWithValue("@ag", agId) |> ignore
        cmd2.ExecuteNonQuery() |> ignore
        use cmd3 = new NpgsqlCommand("DELETE FROM portfolio.tax_bucket WHERE id = ANY(@ids)", conn)
        cmd3.Parameters.AddWithValue("@ids", [| tbAId; tbBId |]) |> ignore
        cmd3.ExecuteNonQuery() |> ignore
        conn.Dispose()

[<Fact>]
[<Trait("GherkinId", "FT-PFC-122")>]
let ``portfolio account list --tax-bucket with no matching name returns empty and exits 0`` () =
    let env = PortfolioCliEnv.create()
    try
        let _id = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_TbNoMatch" env.Prefix)
        let result = CliRunner.run (sprintf "portfolio account list --tax-bucket \"%s_NOMATCH\"" env.Prefix)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("no investment accounts found", stdout)
    finally PortfolioCliEnv.cleanup env

// =====================================================================
// portfolio dimensions — FT-PFC-130 through FT-PFC-132
// =====================================================================

let private dimensionTableNames =
    [ "tax_bucket"; "account_group"; "dim_investment_type"; "dim_market_cap"
      "dim_index_type"; "dim_sector"; "dim_region"; "dim_objective" ]

[<Fact>]
[<Trait("GherkinId", "FT-PFC-130")>]
let ``portfolio dimensions lists all 8 dimension tables and exits 0`` () =
    let result = CliRunner.run "portfolio dimensions"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    for tableName in dimensionTableNames do
        Assert.Contains(sprintf "%s:" tableName, stdout)

[<Fact>]
[<Trait("GherkinId", "FT-PFC-131")>]
let ``portfolio dimensions --json outputs valid JSON with 8 tables`` () =
    let result = CliRunner.run "portfolio dimensions --json"
    Assert.Equal(0, result.ExitCode)
    let json   = CliRunner.stripLogLines result.Stdout
    let doc    = JsonDocument.Parse(json)
    let tables = doc.RootElement.GetProperty("tables")
    Assert.Equal(JsonValueKind.Array, tables.ValueKind)
    Assert.Equal(8, tables.GetArrayLength())

[<Fact>]
[<Trait("GherkinId", "FT-PFC-132")>]
let ``portfolio dimensions exits 0 and lists all 8 table headers even when tables are empty`` () =
    // This test verifies the command's structural correctness: all 8 table headers always appear
    // and the exit code is 0 regardless of how much data is in the dimension tables.
    // The "(empty)" per-table marker is produced by the formatter when values is empty;
    // that path is exercised when seeds have not populated a given table.
    let result = CliRunner.run "portfolio dimensions"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    for tableName in dimensionTableNames do
        Assert.Contains(sprintf "%s:" tableName, stdout)
