module LeoBloom.Tests.PortfolioCommandsTests

open System
open System.Text.Json
open Npgsql
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests
open LeoBloom.Tests.PortfolioTestHelpers

// =====================================================================
// Shared setup: create portfolio test environment
// =====================================================================

module PortfolioCliEnv =
    type Env =
        { TaxBucketId:              int
          AccountGroupId:           int
          Prefix:                   string
          Connection:               NpgsqlConnection
          mutable InvestmentAccountIds: int list
          mutable FundSymbols:          string list }

    let create () =
        Log.initialize()
        let conn   = DataSource.openConnection()
        let prefix = TestData.uniquePrefix()
        let tbId, agId =
            use txn = conn.BeginTransaction()
            let tb = PortfolioInsertHelpers.insertTaxBucket    txn (prefix + "_tb")
            let ag = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
            txn.Commit()
            tb, ag
        { TaxBucketId              = tbId
          AccountGroupId           = agId
          Prefix                   = prefix
          Connection               = conn
          InvestmentAccountIds     = []
          FundSymbols              = [] }

    let cleanup (env: Env) =
        // positions → investment_accounts → funds → tax_buckets → account_groups
        use cmd1 = new NpgsqlCommand(
            "DELETE FROM portfolio.position WHERE investment_account_id = ANY(@ids)",
            env.Connection)
        let idsArr = env.InvestmentAccountIds |> List.toArray
        cmd1.Parameters.AddWithValue("@ids", idsArr) |> ignore
        cmd1.ExecuteNonQuery() |> ignore

        use cmd2 = new NpgsqlCommand(
            "DELETE FROM portfolio.investment_account WHERE id = ANY(@ids)",
            env.Connection)
        cmd2.Parameters.AddWithValue("@ids", idsArr) |> ignore
        cmd2.ExecuteNonQuery() |> ignore

        use cmd3 = new NpgsqlCommand(
            "DELETE FROM portfolio.fund WHERE symbol = ANY(@syms)",
            env.Connection)
        let symsArr = env.FundSymbols |> List.toArray
        cmd3.Parameters.AddWithValue("@syms", symsArr) |> ignore
        cmd3.ExecuteNonQuery() |> ignore

        use cmd4 = new NpgsqlCommand(
            "DELETE FROM portfolio.tax_bucket WHERE id = @tb",
            env.Connection)
        cmd4.Parameters.AddWithValue("@tb", env.TaxBucketId) |> ignore
        cmd4.ExecuteNonQuery() |> ignore

        use cmd5 = new NpgsqlCommand(
            "DELETE FROM portfolio.account_group WHERE id = @ag",
            env.Connection)
        cmd5.Parameters.AddWithValue("@ag", env.AccountGroupId) |> ignore
        cmd5.ExecuteNonQuery() |> ignore

        env.Connection.Dispose()

    /// Create an investment account via CLI and return its ID.
    let createAccountViaCli (env: Env) (name: string) : int =
        let args = sprintf "portfolio account create --name \"%s\" --tax-bucket-id %d --account-group-id %d" name env.TaxBucketId env.AccountGroupId
        let result = CliRunner.run args
        if result.ExitCode <> 0 then
            failwith (sprintf "Failed to create investment account. stderr: %s" result.Stderr)
        let stdout = CliRunner.stripLogLines result.Stdout
        let line = stdout.Split('\n') |> Array.find (fun l -> l.Contains("Investment Account #"))
        let hashIdx = line.IndexOf('#')
        let rest = line.Substring(hashIdx + 1).Trim()
        let idStr = rest.Split([| ' '; '\r'; '\n'; '\u2014'; '\u2013'; '-' |], StringSplitOptions.RemoveEmptyEntries).[0]
        let id = Int32.Parse(idStr)
        env.InvestmentAccountIds <- id :: env.InvestmentAccountIds
        id

    /// Create a fund via CLI.
    let createFundViaCli (env: Env) (symbol: string) (name: string) : unit =
        let args = sprintf "portfolio fund create --symbol %s --name \"%s\"" symbol name
        let result = CliRunner.run args
        if result.ExitCode <> 0 then
            failwith (sprintf "Failed to create fund '%s'. stderr: %s" symbol result.Stderr)
        env.FundSymbols <- symbol :: env.FundSymbols

// =====================================================================
// portfolio account create
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PFC-010")>]
let ``portfolio account create prints account details`` () =
    let env = PortfolioCliEnv.create()
    try
        let name = sprintf "%s_Brokerage" env.Prefix
        let args = sprintf "portfolio account create --name \"%s\" --tax-bucket-id %d --account-group-id %d" name env.TaxBucketId env.AccountGroupId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Investment Account #", stdout)
        Assert.Contains(name, stdout)
        // Track for cleanup
        let line    = stdout.Split('\n') |> Array.find (fun l -> l.Contains("Investment Account #"))
        let hashIdx = line.IndexOf('#')
        let rest    = line.Substring(hashIdx + 1).Trim()
        let idStr   = rest.Split([| ' '; '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries).[0]
        env.InvestmentAccountIds <- Int32.Parse(idStr) :: env.InvestmentAccountIds
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-011")>]
let ``portfolio account create with --json returns valid JSON`` () =
    let env = PortfolioCliEnv.create()
    try
        let name = sprintf "%s_JsonAcct" env.Prefix
        let args = sprintf "portfolio account create --name \"%s\" --tax-bucket-id %d --account-group-id %d --json" name env.TaxBucketId env.AccountGroupId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let json = CliRunner.stripLogLines result.Stdout
        let doc  = JsonDocument.Parse(json)
        let id   = doc.RootElement.GetProperty("id").GetInt32()
        Assert.True(id > 0)
        Assert.Equal(name, doc.RootElement.GetProperty("name").GetString())
        env.InvestmentAccountIds <- id :: env.InvestmentAccountIds
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-014")>]
let ``portfolio account create with blank name returns non-zero exit code`` () =
    let env = PortfolioCliEnv.create()
    try
        let args = sprintf "portfolio account create --name \"\" --tax-bucket-id %d --account-group-id %d" env.TaxBucketId env.AccountGroupId
        let result = CliRunner.run args
        Assert.NotEqual(0, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr))
    finally PortfolioCliEnv.cleanup env

// =====================================================================
// portfolio account list
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PFC-020")>]
let ``portfolio account list shows table output`` () =
    let env = PortfolioCliEnv.create()
    try
        let name = sprintf "%s_ListTest" env.Prefix
        let id   = PortfolioCliEnv.createAccountViaCli env name
        // id already tracked inside createAccountViaCli
        let result = CliRunner.run "portfolio account list"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(name, stdout)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-022")>]
let ``portfolio account list --json returns JSON array`` () =
    let env = PortfolioCliEnv.create()
    try
        let name = sprintf "%s_JsonList" env.Prefix
        let _id  = PortfolioCliEnv.createAccountViaCli env name
        let result = CliRunner.run "portfolio account list --json"
        Assert.Equal(0, result.ExitCode)
        let json = CliRunner.stripLogLines result.Stdout
        let arr  = JsonDocument.Parse(json)
        Assert.Equal(JsonValueKind.Array, arr.RootElement.ValueKind)
    finally PortfolioCliEnv.cleanup env

// =====================================================================
// portfolio fund create
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PFC-030")>]
let ``portfolio fund create prints fund details`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "T%s" (env.Prefix.ToUpperInvariant())
    try
        let args   = sprintf "portfolio fund create --symbol %s --name \"Test Fund %s\"" symbol env.Prefix
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(symbol, stdout)
        env.FundSymbols <- symbol :: env.FundSymbols
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-031")>]
let ``portfolio fund create with --json returns valid JSON`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "J%s" (env.Prefix.ToUpperInvariant())
    try
        let args   = sprintf "portfolio fund create --symbol %s --name \"Json Fund %s\" --json" symbol env.Prefix
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let json   = CliRunner.stripLogLines result.Stdout
        let doc    = JsonDocument.Parse(json)
        Assert.Equal(symbol, doc.RootElement.GetProperty("symbol").GetString())
        env.FundSymbols <- symbol :: env.FundSymbols
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-032")>]
let ``portfolio fund create duplicate symbol returns non-zero`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "D%s" (env.Prefix.ToUpperInvariant())
    try
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Fund %s" env.Prefix)
        let args   = sprintf "portfolio fund create --symbol %s --name \"Dup Fund\"" symbol
        let result = CliRunner.run args
        Assert.NotEqual(0, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr))
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-035")>]
let ``portfolio fund create with blank name returns non-zero`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "B%s" (env.Prefix.ToUpperInvariant())
    try
        let args   = sprintf "portfolio fund create --symbol %s --name \"\"" symbol
        let result = CliRunner.run args
        Assert.NotEqual(0, result.ExitCode)
    finally PortfolioCliEnv.cleanup env

// =====================================================================
// portfolio fund list
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PFC-040")>]
let ``portfolio fund list shows table output`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "L%s" (env.Prefix.ToUpperInvariant())
    try
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "List Fund %s" env.Prefix)
        let result = CliRunner.run "portfolio fund list"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(symbol, stdout)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-041")>]
let ``portfolio fund list --json returns JSON array`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "A%s" (env.Prefix.ToUpperInvariant())
    try
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Array Fund %s" env.Prefix)
        let result = CliRunner.run "portfolio fund list --json"
        Assert.Equal(0, result.ExitCode)
        let json   = CliRunner.stripLogLines result.Stdout
        let arr    = JsonDocument.Parse(json)
        Assert.Equal(JsonValueKind.Array, arr.RootElement.ValueKind)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-043")>]
let ``portfolio fund list with multiple dimension filters returns error`` () =
    let env    = PortfolioCliEnv.create()
    try
        let result = CliRunner.run "portfolio fund list --investment-type-id 1 --market-cap-id 1"
        Assert.NotEqual(0, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr))
    finally PortfolioCliEnv.cleanup env

// =====================================================================
// portfolio fund show
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PFC-050")>]
let ``portfolio fund show prints fund detail`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "S%s" (env.Prefix.ToUpperInvariant())
    try
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Show Fund %s" env.Prefix)
        let result = CliRunner.run (sprintf "portfolio fund show %s" symbol)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(symbol, stdout)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-052")>]
let ``portfolio fund show nonexistent symbol returns non-zero`` () =
    let env = PortfolioCliEnv.create()
    try
        let result = CliRunner.run "portfolio fund show ZZZZNOTREAL"
        Assert.NotEqual(0, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr))
    finally PortfolioCliEnv.cleanup env

// =====================================================================
// portfolio position record
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PFC-060")>]
let ``portfolio position record creates a position and prints it`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "R%s" (env.Prefix.ToUpperInvariant())
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_Acct" env.Prefix)
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Rec Fund %s" env.Prefix)
        let args = sprintf "portfolio position record --account-id %d --symbol %s --date 2026-01-15 --price 100.00 --quantity 10 --value 1000.00 --cost-basis 950.00" acctId symbol
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Position #", stdout)
        Assert.Contains(symbol, stdout)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-061")>]
let ``portfolio position record with --json returns valid JSON`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "Q%s" (env.Prefix.ToUpperInvariant())
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_JAcct" env.Prefix)
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Json Rec Fund %s" env.Prefix)
        let args = sprintf "portfolio position record --account-id %d --symbol %s --date 2026-01-15 --price 100.00 --quantity 10 --value 1000.00 --cost-basis 950.00 --json" acctId symbol
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let json = CliRunner.stripLogLines result.Stdout
        let doc  = JsonDocument.Parse(json)
        Assert.True(doc.RootElement.GetProperty("id").GetInt32() > 0)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-064")>]
let ``portfolio position record with negative price returns non-zero`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "N%s" (env.Prefix.ToUpperInvariant())
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_NAcct" env.Prefix)
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Neg Fund %s" env.Prefix)
        let args = sprintf "portfolio position record --account-id %d --symbol %s --date 2026-01-15 --price -1.00 --quantity 10 --value 1000.00 --cost-basis 950.00" acctId symbol
        let result = CliRunner.run args
        Assert.NotEqual(0, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr))
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-065")>]
let ``portfolio position record with nonexistent symbol returns non-zero`` () =
    let env = PortfolioCliEnv.create()
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_XAcct" env.Prefix)
        let args = sprintf "portfolio position record --account-id %d --symbol ZZNOEXIST --date 2026-01-15 --price 10.00 --quantity 5 --value 50.00 --cost-basis 45.00" acctId
        let result = CliRunner.run args
        Assert.NotEqual(0, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr))
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-066")>]
let ``portfolio position record with invalid date returns non-zero`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "I%s" (env.Prefix.ToUpperInvariant())
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_IAcct" env.Prefix)
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Inv Date Fund %s" env.Prefix)
        let args = sprintf "portfolio position record --account-id %d --symbol %s --date notadate --price 10.00 --quantity 5 --value 50.00 --cost-basis 45.00" acctId symbol
        let result = CliRunner.run args
        Assert.NotEqual(0, result.ExitCode)
    finally PortfolioCliEnv.cleanup env

// =====================================================================
// portfolio position list
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PFC-070")>]
let ``portfolio position list shows table output`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "P%s" (env.Prefix.ToUpperInvariant())
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_PLAcct" env.Prefix)
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "PosList Fund %s" env.Prefix)
        let recArgs = sprintf "portfolio position record --account-id %d --symbol %s --date 2026-02-01 --price 50.00 --quantity 20 --value 1000.00 --cost-basis 980.00" acctId symbol
        let recResult = CliRunner.run recArgs
        Assert.Equal(0, recResult.ExitCode)
        let result = CliRunner.run "portfolio position list"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(symbol, stdout)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-071")>]
let ``portfolio position list filtered by account-id`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "F%s" (env.Prefix.ToUpperInvariant())
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_FAcct" env.Prefix)
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Filter Fund %s" env.Prefix)
        let recArgs = sprintf "portfolio position record --account-id %d --symbol %s --date 2026-02-01 --price 55.00 --quantity 10 --value 550.00 --cost-basis 520.00" acctId symbol
        CliRunner.run recArgs |> ignore
        let result = CliRunner.run (sprintf "portfolio position list --account-id %d" acctId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(symbol, stdout)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-072")>]
let ``portfolio position list --json returns JSON array`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "G%s" (env.Prefix.ToUpperInvariant())
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_GAcct" env.Prefix)
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Json PList Fund %s" env.Prefix)
        let recArgs = sprintf "portfolio position record --account-id %d --symbol %s --date 2026-03-01 --price 60.00 --quantity 5 --value 300.00 --cost-basis 280.00" acctId symbol
        CliRunner.run recArgs |> ignore
        let result = CliRunner.run (sprintf "portfolio position list --account-id %d --json" acctId)
        Assert.Equal(0, result.ExitCode)
        let json = CliRunner.stripLogLines result.Stdout
        let arr  = JsonDocument.Parse(json)
        Assert.Equal(JsonValueKind.Array, arr.RootElement.ValueKind)
    finally PortfolioCliEnv.cleanup env

// =====================================================================
// portfolio position latest
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PFC-080")>]
let ``portfolio position latest shows output`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "M%s" (env.Prefix.ToUpperInvariant())
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_MAcct" env.Prefix)
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Latest Fund %s" env.Prefix)
        let recArgs = sprintf "portfolio position record --account-id %d --symbol %s --date 2026-03-15 --price 75.00 --quantity 8 --value 600.00 --cost-basis 570.00" acctId symbol
        let recResult = CliRunner.run recArgs
        Assert.Equal(0, recResult.ExitCode)
        let result = CliRunner.run "portfolio position latest"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(symbol, stdout)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-083")>]
let ``portfolio position latest filtered by account-id`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "K%s" (env.Prefix.ToUpperInvariant())
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_KAcct" env.Prefix)
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Lat Filt Fund %s" env.Prefix)
        let recArgs = sprintf "portfolio position record --account-id %d --symbol %s --date 2026-04-01 --price 80.00 --quantity 3 --value 240.00 --cost-basis 225.00" acctId symbol
        CliRunner.run recArgs |> ignore
        let result = CliRunner.run (sprintf "portfolio position latest --account-id %d" acctId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(symbol, stdout)
    finally PortfolioCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PFC-082")>]
let ``portfolio position latest --json returns JSON array`` () =
    let env    = PortfolioCliEnv.create()
    let symbol = sprintf "V%s" (env.Prefix.ToUpperInvariant())
    try
        let acctId = PortfolioCliEnv.createAccountViaCli env (sprintf "%s_VAcct" env.Prefix)
        PortfolioCliEnv.createFundViaCli env symbol (sprintf "Json Latest Fund %s" env.Prefix)
        let recArgs = sprintf "portfolio position record --account-id %d --symbol %s --date 2026-04-01 --price 90.00 --quantity 2 --value 180.00 --cost-basis 170.00" acctId symbol
        CliRunner.run recArgs |> ignore
        let result = CliRunner.run (sprintf "portfolio position latest --account-id %d --json" acctId)
        Assert.Equal(0, result.ExitCode)
        let json = CliRunner.stripLogLines result.Stdout
        let arr  = JsonDocument.Parse(json)
        Assert.Equal(JsonValueKind.Array, arr.RootElement.ValueKind)
    finally PortfolioCliEnv.cleanup env
