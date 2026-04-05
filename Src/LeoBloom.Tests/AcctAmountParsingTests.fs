module LeoBloom.Tests.AcctAmountParsingTests

open System
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests

// =====================================================================
// Account:Amount Argument Parsing — AcctAmountParsing.feature
//
// Tests CLI-layer parse behavior for the --debit and --credit args.
// Per ADR-003, we do NOT re-test service validation — only arg parsing.
// =====================================================================

// --- Valid Formats ---

[<Fact>]
[<Trait("GherkinId", "FT-AAP-001")>]
let ``integer account ID with decimal amount parses correctly`` () =
    let conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker (prefix + "_at") "debit"
        let acct1 = InsertHelpers.insertAccount conn tracker (prefix + "A1") "AcctA" atId true
        let acct2 = InsertHelpers.insertAccount conn tracker (prefix + "A2") "AcctB" atId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

        let args =
            sprintf "ledger post --debit %d:1000.00 --credit %d:1000.00 --date 2026-03-15 --description \"Parse test\" --fiscal-period-id %d"
                acct1 acct2 fpId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        // Track for cleanup
        if result.Stdout.Contains("Journal Entry #") then
            let line = result.Stdout.Split('\n') |> Array.find (fun l -> l.Contains("Journal Entry #"))
            let hashIdx = line.IndexOf('#')
            let spaceIdx = line.IndexOf(' ', hashIdx)
            let idStr = line.Substring(hashIdx + 1, spaceIdx - hashIdx - 1)
            TestCleanup.trackJournalEntry (Int32.Parse(idStr)) tracker
    finally
        TestCleanup.deleteAll tracker
        conn.Dispose()

[<Fact>]
[<Trait("GherkinId", "FT-AAP-002")>]
let ``whole number amount without decimal parses correctly`` () =
    let conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker (prefix + "_at") "debit"
        let acct1 = InsertHelpers.insertAccount conn tracker (prefix + "A1") "AcctA" atId true
        let acct2 = InsertHelpers.insertAccount conn tracker (prefix + "A2") "AcctB" atId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

        let args =
            sprintf "ledger post --debit %d:1000 --credit %d:1000 --date 2026-03-15 --description \"No decimal\" --fiscal-period-id %d"
                acct1 acct2 fpId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        // Track for cleanup
        if result.Stdout.Contains("Journal Entry #") then
            let line = result.Stdout.Split('\n') |> Array.find (fun l -> l.Contains("Journal Entry #"))
            let hashIdx = line.IndexOf('#')
            let spaceIdx = line.IndexOf(' ', hashIdx)
            let idStr = line.Substring(hashIdx + 1, spaceIdx - hashIdx - 1)
            TestCleanup.trackJournalEntry (Int32.Parse(idStr)) tracker
    finally
        TestCleanup.deleteAll tracker
        conn.Dispose()

// --- Malformed acct:amount (Scenario Outline FT-AAP-003) ---

[<Fact>]
[<Trait("GherkinId", "FT-AAP-003a")>]
let ``missing colon separator is rejected`` () =
    let result = CliRunner.run "ledger post --debit 1010 --credit 4010:1000.00 --date 2026-03-15 --description \"Bad parse\" --fiscal-period-id 1"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr about invalid format")

[<Fact>]
[<Trait("GherkinId", "FT-AAP-003b")>]
let ``missing account ID is rejected`` () =
    let result = CliRunner.run "ledger post --debit :1000.00 --credit 4010:1000.00 --date 2026-03-15 --description \"Bad parse\" --fiscal-period-id 1"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr about invalid format")

[<Fact>]
[<Trait("GherkinId", "FT-AAP-003c")>]
let ``missing amount is rejected`` () =
    let result = CliRunner.run "ledger post --debit 1010: --credit 4010:1000.00 --date 2026-03-15 --description \"Bad parse\" --fiscal-period-id 1"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr about invalid format")

[<Fact>]
[<Trait("GherkinId", "FT-AAP-003d")>]
let ``non-numeric amount is rejected`` () =
    let result = CliRunner.run "ledger post --debit 1010:abc --credit 4010:1000.00 --date 2026-03-15 --description \"Bad parse\" --fiscal-period-id 1"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr about invalid format")

[<Fact>]
[<Trait("GherkinId", "FT-AAP-003e")>]
let ``non-numeric account ID is rejected`` () =
    let result = CliRunner.run "ledger post --debit abc:1000.00 --credit 4010:1000.00 --date 2026-03-15 --description \"Bad parse\" --fiscal-period-id 1"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr about invalid format")

[<Fact>]
[<Trait("GherkinId", "FT-AAP-003f")>]
let ``extra colon in acct amount is handled`` () =
    // "1010:100:00" — split on first colon gives acct=1010, amount="100:00" which fails decimal parse
    let result = CliRunner.run "ledger post --debit 1010:100:00 --credit 4010:1000.00 --date 2026-03-15 --description \"Bad parse\" --fiscal-period-id 1"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr about invalid format")

// --- Negative and zero amounts ---

[<Fact>]
[<Trait("GherkinId", "FT-AAP-004")>]
let ``negative amount in acct amount is rejected`` () =
    let result = CliRunner.run "ledger post --debit 1010:-500.00 --credit 4010:500.00 --date 2026-03-15 --description \"Negative\" --fiscal-period-id 1"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-AAP-005")>]
let ``zero amount in acct amount is rejected`` () =
    let result = CliRunner.run "ledger post --debit 1010:0.00 --credit 4010:0.00 --date 2026-03-15 --description \"Zero\" --fiscal-period-id 1"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// --- Date Parsing ---

[<Fact>]
[<Trait("GherkinId", "FT-AAP-006")>]
let ``invalid date format is rejected`` () =
    let result = CliRunner.run "ledger post --debit 1010:100.00 --credit 4010:100.00 --date not-a-date --description \"Bad date\" --fiscal-period-id 1"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-AAP-007")>]
let ``date in wrong format is rejected`` () =
    let result = CliRunner.run "ledger post --debit 1010:100.00 --credit 4010:100.00 --date 03/15/2026 --description \"US format\" --fiscal-period-id 1"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")
