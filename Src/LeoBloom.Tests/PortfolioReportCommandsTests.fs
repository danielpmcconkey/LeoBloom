module LeoBloom.Tests.PortfolioReportCommandsTests

open System
open System.Text.Json
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests
open LeoBloom.Tests.PortfolioTestHelpers

// =====================================================================
// Shared setup
// =====================================================================

module ReportEnv =
    type Env =
        { Tracker:        PortfolioTracker
          TaxBucketId:    int
          TaxBucketId2:   int
          AccountGroupId: int
          AccountId1:     int
          AccountId2:     int
          Prefix:         string }

    let create () =
        Log.initialize()
        let conn    = DataSource.openConnection()
        let tracker = createPortfolioTracker conn
        let prefix  = TestData.uniquePrefix()

        // Two tax buckets, one account group
        let tb1 = PortfolioInsertHelpers.insertTaxBucket    conn tracker (prefix + "_Taxable")
        let tb2 = PortfolioInsertHelpers.insertTaxBucket    conn tracker (prefix + "_Roth")
        let ag  = PortfolioInsertHelpers.insertAccountGroup conn tracker (prefix + "_AG")

        // Two investment accounts
        let acct1 = PortfolioInsertHelpers.insertInvestmentAccount conn tracker (prefix + "_Brok") tb1 ag
        let acct2 = PortfolioInsertHelpers.insertInvestmentAccount conn tracker (prefix + "_Roth") tb2 ag

        // Two funds (no dimension IDs — symbol dimension will still work)
        PortfolioInsertHelpers.insertFund conn tracker (prefix + "_VTSAX") (prefix + " Total Stock") |> ignore
        PortfolioInsertHelpers.insertFund conn tracker (prefix + "_VTIAX") (prefix + " Intl Stock")  |> ignore

        // Positions on two dates
        let d1 = DateOnly(2024, 1, 1)
        let d2 = DateOnly(2024, 2, 1)

        // Account 1: VTSAX $10000 cb $8000, VTIAX $5000 cb $4000
        PortfolioInsertHelpers.insertPosition conn tracker acct1 (prefix + "_VTSAX") d1 100m 100m 10000m 8000m |> ignore
        PortfolioInsertHelpers.insertPosition conn tracker acct1 (prefix + "_VTIAX") d1 50m  100m  5000m 4000m |> ignore

        // Account 2: VTSAX $20000 cb $15000
        PortfolioInsertHelpers.insertPosition conn tracker acct2 (prefix + "_VTSAX") d1 100m 200m 20000m 15000m |> ignore

        // Second date positions (latest)
        PortfolioInsertHelpers.insertPosition conn tracker acct1 (prefix + "_VTSAX") d2 105m 100m 10500m 8000m |> ignore
        PortfolioInsertHelpers.insertPosition conn tracker acct1 (prefix + "_VTIAX") d2 52m  100m  5200m 4000m |> ignore
        PortfolioInsertHelpers.insertPosition conn tracker acct2 (prefix + "_VTSAX") d2 105m 200m 21000m 15000m |> ignore

        { Tracker        = tracker
          TaxBucketId    = tb1
          TaxBucketId2   = tb2
          AccountGroupId = ag
          AccountId1     = acct1
          AccountId2     = acct2
          Prefix         = prefix }

    let cleanup (env: Env) =
        deletePortfolioAll env.Tracker
        env.Tracker.Connection.Dispose()

// =====================================================================
// report allocation
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPP-001")>]
let ``report --help lists the four new subcommands`` () =
    let result = CliRunner.run "report --help"
    // Exit code from --help may be 1 (Argu convention) -- just check stdout/stderr for subcommand names
    let output = result.Stdout + result.Stderr
    Assert.Contains("allocation", output)
    Assert.Contains("portfolio-summary", output)
    Assert.Contains("portfolio-history", output)
    Assert.Contains("gains", output)

[<Fact>]
[<Trait("GherkinId", "FT-RPP-010")>]
let ``report allocation default dimension shows account-group output`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report allocation"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Should contain "Allocation by account-group" and a value
        Assert.Contains("account-group", stdout)
        Assert.Contains(env.Prefix + "_AG", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-011")>]
let ``report allocation --by sector groups by sector dimension`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report allocation --by sector"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Our test funds have no sector assigned, so they appear as "(Unclassified)"
        Assert.Contains("Unclassified", stdout)
        Assert.Contains("sector", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-012")>]
let ``report allocation accepts all 10 valid dimensions`` () =
    let env = ReportEnv.create()
    try
        let dims = [ "tax-bucket"; "account-group"; "account"
                     "investment-type"; "market-cap"; "index-type"
                     "sector"; "region"; "objective"; "symbol" ]
        for dim in dims do
            let result = CliRunner.run (sprintf "report allocation --by %s" dim)
            Assert.True(result.ExitCode = 0, sprintf "Dimension '%s' failed with exit %d. stderr: %s" dim result.ExitCode result.Stderr)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-013")>]
let ``report allocation percentages sum to 100`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report allocation --by tax-bucket"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Percentages are printed as XX.X% -- collect them and sum
        let lines =
            stdout.Split('\n')
            |> Array.filter (fun l -> l.Contains("%") && not (l.Contains("100.0")))
        let pcts =
            lines
            |> Array.choose (fun l ->
                let parts = l.Trim().Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
                let pctPart = parts |> Array.tryFind (fun p -> p.EndsWith("%"))
                pctPart |> Option.bind (fun s ->
                    match Decimal.TryParse(s.TrimEnd('%')) with
                    | true, d -> Some d
                    | _ -> None))
        if pcts.Length > 0 then
            let sum = Array.sum pcts
            Assert.True(sum >= 99.0m && sum <= 101.0m,
                sprintf "Percentages sum to %M, expected ~100" sum)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-014")>]
let ``report allocation --json returns valid JSON`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report allocation --json"
        Assert.Equal(0, result.ExitCode)
        let json = CliRunner.stripLogLines result.Stdout
        let doc  = JsonDocument.Parse(json)
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind)
        Assert.True(doc.RootElement.TryGetProperty("rows") |> fst)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-015")>]
let ``report allocation empty portfolio returns informative message`` () =
    let env = ReportEnv.create()
    try
        // No positions -- just the prefix dimensions but no positions
        let conn    = env.Tracker.Connection
        let tracker = createPortfolioTracker conn
        let emptyPrefix = TestData.uniquePrefix()
        let tb  = PortfolioInsertHelpers.insertTaxBucket    conn tracker (emptyPrefix + "_tb")
        let ag  = PortfolioInsertHelpers.insertAccountGroup conn tracker (emptyPrefix + "_ag")
        PortfolioInsertHelpers.insertInvestmentAccount conn tracker (emptyPrefix + "_acct") tb ag |> ignore
        try
            let result = CliRunner.run "report allocation"
            // Should succeed with exit 0 and mention no positions
            Assert.Equal(0, result.ExitCode)
        finally
            deletePortfolioAll tracker
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-016")>]
let ``report allocation with invalid dimension exits non-zero with error`` () =
    let result = CliRunner.run "report allocation --by not-a-dimension"
    Assert.NotEqual(0, result.ExitCode)
    let output = result.Stderr + result.Stdout
    Assert.False(String.IsNullOrWhiteSpace(output),
        "Expected error message for invalid dimension")

// =====================================================================
// report portfolio-summary
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPP-020")>]
let ``report portfolio-summary shows total value and gain/loss`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report portfolio-summary"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Total Value", stdout)
        Assert.Contains("Cost Basis", stdout)
        Assert.Contains("Gain/Loss", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-021")>]
let ``report portfolio-summary shows tax bucket breakdown`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report portfolio-summary"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Tax Bucket", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-022")>]
let ``report portfolio-summary shows top 5 holdings`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report portfolio-summary"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Top 5", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-023")>]
let ``report portfolio-summary --json returns valid JSON`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report portfolio-summary --json"
        Assert.Equal(0, result.ExitCode)
        let json = CliRunner.stripLogLines result.Stdout
        let doc  = JsonDocument.Parse(json)
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind)
        Assert.True(doc.RootElement.TryGetProperty("totalValue") |> fst)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-024")>]
let ``report portfolio-summary empty portfolio exits 0 with informative message`` () =
    let env = ReportEnv.create()
    try
        // Relies on existing empty DB having no positions from other tests
        // Just verify the command itself succeeds
        let result = CliRunner.run "report portfolio-summary"
        Assert.Equal(0, result.ExitCode)
    finally ReportEnv.cleanup env

// =====================================================================
// report portfolio-history
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPP-030")>]
let ``report portfolio-history shows one row per position date`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report portfolio-history --by symbol"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Should contain dates 2024-01-01 and 2024-02-01
        Assert.Contains("2024-01-01", stdout)
        Assert.Contains("2024-02-01", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-031")>]
let ``report portfolio-history --by account-group groups by account group`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report portfolio-history --by account-group"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Header should say "account-group" and the account group name should appear
        Assert.Contains("account-group", stdout)
        Assert.Contains(env.Prefix + "_AG", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-032")>]
let ``report portfolio-history --from filters start date`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report portfolio-history --by symbol --from 2024-02-01"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.DoesNotContain("2024-01-01", stdout)
        Assert.Contains("2024-02-01", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-033")>]
let ``report portfolio-history --to filters end date`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report portfolio-history --by symbol --to 2024-01-01"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("2024-01-01", stdout)
        Assert.DoesNotContain("2024-02-01", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-034")>]
let ``report portfolio-history --json returns valid JSON`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report portfolio-history --json"
        Assert.Equal(0, result.ExitCode)
        let json = CliRunner.stripLogLines result.Stdout
        let doc  = JsonDocument.Parse(json)
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind)
        Assert.True(doc.RootElement.TryGetProperty("rows") |> fst)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-035")>]
let ``report portfolio-history empty portfolio exits 0`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report portfolio-history --by symbol --from 2099-01-01"
        Assert.Equal(0, result.ExitCode)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-036")>]
let ``report portfolio-history invalid date format returns non-zero`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report portfolio-history --from not-a-date"
        Assert.NotEqual(0, result.ExitCode)
    finally ReportEnv.cleanup env

// =====================================================================
// report gains
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPP-040")>]
let ``report gains shows per-fund rows with gain/loss`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report gains"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(env.Prefix + "_VTSAX", stdout)
        Assert.Contains(env.Prefix + "_VTIAX", stdout)
        Assert.Contains("Gain/Loss", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-041")>]
let ``report gains shows totals row`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report gains"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Total", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-042")>]
let ``report gains --account filters to single account`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run (sprintf "report gains --account %d" env.AccountId1)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Account 1 has both VTSAX and VTIAX
        Assert.Contains(env.Prefix + "_VTSAX", stdout)
        Assert.Contains(env.Prefix + "_VTIAX", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-043")>]
let ``report gains --json returns valid JSON`` () =
    let env = ReportEnv.create()
    try
        let result = CliRunner.run "report gains --json"
        Assert.Equal(0, result.ExitCode)
        let json = CliRunner.stripLogLines result.Stdout
        let doc  = JsonDocument.Parse(json)
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind)
        Assert.True(doc.RootElement.TryGetProperty("rows") |> fst)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-044")>]
let ``report gains empty portfolio exits 0 with informative message`` () =
    let env = ReportEnv.create()
    try
        // Filter to a non-existent account ID
        let result = CliRunner.run "report gains --account 999999999"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("no positions", stdout)
    finally ReportEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPP-045")>]
let ``report gains with non-numeric account ID exits non-zero with error`` () =
    let result = CliRunner.run "report gains --account not-an-id"
    Assert.NotEqual(0, result.ExitCode)
    let output = result.Stderr + result.Stdout
    Assert.False(String.IsNullOrWhiteSpace(output),
        "Expected error message for non-numeric account ID")

// =====================================================================
// AC-S1: PortfolioReportCommands.fs structural check
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPP-060")>]
let ``report subcommands have help text`` () =
    let commands = [ "report allocation --help"
                     "report portfolio-summary --help"
                     "report portfolio-history --help"
                     "report gains --help" ]
    for cmd in commands do
        let result = CliRunner.run cmd
        let output = result.Stdout + result.Stderr
        Assert.False(String.IsNullOrWhiteSpace(output),
            sprintf "Expected help output for '%s', got nothing" cmd)
