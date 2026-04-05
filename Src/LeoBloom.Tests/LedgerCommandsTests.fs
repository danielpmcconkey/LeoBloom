module LeoBloom.Tests.LedgerCommandsTests

open System
open System.Text.Json
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests

// =====================================================================
// Shared setup: create a "CLI-testable ledger environment"
// — two accounts (debit-normal + credit-normal) and an open fiscal period
// =====================================================================

module CliTestEnv =
    type Env =
        { DebitAccountId: int
          CreditAccountId: int
          FiscalPeriodId: int
          Tracker: TestCleanup.Tracker }

    let create () =
        let conn = DataSource.openConnection()
        let tracker = TestCleanup.create conn
        let prefix = TestData.uniquePrefix()
        let atDebit = InsertHelpers.insertAccountType conn tracker (prefix + "_db") "debit"
        let atCredit = InsertHelpers.insertAccountType conn tracker (prefix + "_cr") "credit"
        let acctDebit = InsertHelpers.insertAccount conn tracker (prefix + "D1") "DebitAcct" atDebit true
        let acctCredit = InsertHelpers.insertAccount conn tracker (prefix + "C1") "CreditAcct" atCredit true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        { DebitAccountId = acctDebit
          CreditAccountId = acctCredit
          FiscalPeriodId = fpId
          Tracker = tracker }

    let cleanup (env: Env) =
        TestCleanup.deleteAll env.Tracker
        env.Tracker.Connection.Dispose()

    /// Post an entry via CLI and return the entry ID parsed from stdout.
    /// Used to set up "a posted journal entry with known ID" for void/show tests.
    let postEntryViaCli (env: Env) : int =
        let args =
            sprintf "ledger post --debit %d:1000.00 --credit %d:1000.00 --date 2026-03-15 --description \"CLI test entry\" --fiscal-period-id %d"
                env.DebitAccountId env.CreditAccountId env.FiscalPeriodId
        let result = CliRunner.run args
        if result.ExitCode <> 0 then
            failwith (sprintf "Failed to post entry for test setup. stderr: %s" result.Stderr)
        // Parse entry ID from human-readable output: "Journal Entry #NNN  [POSTED]"
        let line = result.Stdout.Split('\n') |> Array.find (fun l -> l.Contains("Journal Entry #"))
        let hashIdx = line.IndexOf('#')
        let spaceIdx = line.IndexOf(' ', hashIdx)
        let idStr = line.Substring(hashIdx + 1, spaceIdx - hashIdx - 1)
        let entryId = Int32.Parse(idStr)
        TestCleanup.trackJournalEntry entryId env.Tracker
        entryId

    /// Post an entry via CLI and return the entry ID, using --json for more reliable parsing.
    let postEntryViaCliJson (env: Env) : int =
        let args =
            sprintf "--json ledger post --debit %d:1000.00 --credit %d:1000.00 --date 2026-03-15 --description \"CLI test entry\" --fiscal-period-id %d"
                env.DebitAccountId env.CreditAccountId env.FiscalPeriodId
        let result = CliRunner.run args
        if result.ExitCode <> 0 then
            failwith (sprintf "Failed to post entry for test setup. stderr: %s" result.Stderr)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        let entryId = doc.RootElement.GetProperty("entry").GetProperty("id").GetInt32()
        TestCleanup.trackJournalEntry entryId env.Tracker
        entryId


// =====================================================================
// ledger post — Tests
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LCD-001")>]
let ``post a valid journal entry via CLI`` () =
    let env = CliTestEnv.create()
    try
        let args =
            sprintf "ledger post --debit %d:1000.00 --credit %d:1000.00 --date 2026-03-15 --description \"March rent\" --fiscal-period-id %d"
                env.DebitAccountId env.CreditAccountId env.FiscalPeriodId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("Journal Entry #", result.Stdout)
        Assert.Contains("POSTED", result.Stdout)
        // Track the posted entry for cleanup
        if result.Stdout.Contains("Journal Entry #") then
            let line = result.Stdout.Split('\n') |> Array.find (fun l -> l.Contains("Journal Entry #"))
            let hashIdx = line.IndexOf('#')
            let spaceIdx = line.IndexOf(' ', hashIdx)
            let idStr = line.Substring(hashIdx + 1, spaceIdx - hashIdx - 1)
            TestCleanup.trackJournalEntry (Int32.Parse(idStr)) env.Tracker
    finally CliTestEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-LCD-002")>]
let ``post with --json flag outputs JSON to stdout`` () =
    let env = CliTestEnv.create()
    try
        let args =
            sprintf "--json ledger post --debit %d:1000.00 --credit %d:1000.00 --date 2026-03-15 --description \"March rent\" --fiscal-period-id %d"
                env.DebitAccountId env.CreditAccountId env.FiscalPeriodId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        // Verify it's valid JSON
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
        // Track for cleanup
        let entryId = doc.RootElement.GetProperty("entry").GetProperty("id").GetInt32()
        TestCleanup.trackJournalEntry entryId env.Tracker
    finally CliTestEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-LCD-003")>]
let ``post with optional --source flag includes source in output`` () =
    let env = CliTestEnv.create()
    try
        let args =
            sprintf "ledger post --debit %d:1000.00 --credit %d:1000.00 --date 2026-03-15 --description \"Manual adj\" --source \"manual\" --fiscal-period-id %d"
                env.DebitAccountId env.CreditAccountId env.FiscalPeriodId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("manual", result.Stdout)
        // Track for cleanup
        if result.Stdout.Contains("Journal Entry #") then
            let line = result.Stdout.Split('\n') |> Array.find (fun l -> l.Contains("Journal Entry #"))
            let hashIdx = line.IndexOf('#')
            let spaceIdx = line.IndexOf(' ', hashIdx)
            let idStr = line.Substring(hashIdx + 1, spaceIdx - hashIdx - 1)
            TestCleanup.trackJournalEntry (Int32.Parse(idStr)) env.Tracker
    finally CliTestEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-LCD-004")>]
let ``post with --ref flag includes references in output`` () =
    let env = CliTestEnv.create()
    try
        let args =
            sprintf "ledger post --debit %d:1000.00 --credit %d:1000.00 --date 2026-03-15 --description \"With ref\" --fiscal-period-id %d --ref cheque:1234"
                env.DebitAccountId env.CreditAccountId env.FiscalPeriodId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("cheque", result.Stdout)
        Assert.Contains("1234", result.Stdout)
        // Track for cleanup
        if result.Stdout.Contains("Journal Entry #") then
            let line = result.Stdout.Split('\n') |> Array.find (fun l -> l.Contains("Journal Entry #"))
            let hashIdx = line.IndexOf('#')
            let spaceIdx = line.IndexOf(' ', hashIdx)
            let idStr = line.Substring(hashIdx + 1, spaceIdx - hashIdx - 1)
            TestCleanup.trackJournalEntry (Int32.Parse(idStr)) env.Tracker
    finally CliTestEnv.cleanup env

// --- Missing Required Args ---

[<Fact>]
[<Trait("GherkinId", "FT-LCD-005")>]
let ``post with no arguments prints error to stderr`` () =
    let result = CliRunner.run "ledger post"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-LCD-006a")>]
let ``post missing --debit is rejected`` () =
    let result = CliRunner.run "ledger post --credit 4010:1000.00 --date 2026-03-15 --description \"X\" --fiscal-period-id 1"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-LCD-006b")>]
let ``post missing --credit is rejected`` () =
    let result = CliRunner.run "ledger post --debit 1010:1000.00 --date 2026-03-15 --description \"X\" --fiscal-period-id 1"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-LCD-006c")>]
let ``post missing --date is rejected`` () =
    let result = CliRunner.run "ledger post --debit 1010:1000.00 --credit 4010:1000.00 --description \"X\" --fiscal-period-id 1"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-LCD-006d")>]
let ``post missing --description is rejected`` () =
    let result = CliRunner.run "ledger post --debit 1010:1000.00 --credit 4010:1000.00 --date 2026-03-15 --fiscal-period-id 1"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-LCD-006e")>]
let ``post missing --fiscal-period-id is rejected`` () =
    let result = CliRunner.run "ledger post --debit 1010:1000.00 --credit 4010:1000.00 --date 2026-03-15 --description \"X\""
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// --- Service Error Surfacing ---

[<Fact>]
[<Trait("GherkinId", "FT-LCD-007")>]
let ``post that triggers a service validation error surfaces it to stderr`` () =
    let env = CliTestEnv.create()
    try
        // Unbalanced: debit 1000, credit 500
        let args =
            sprintf "ledger post --debit %d:1000.00 --credit %d:500.00 --date 2026-03-15 --description \"Unbalanced\" --fiscal-period-id %d"
                env.DebitAccountId env.CreditAccountId env.FiscalPeriodId
        let result = CliRunner.run args
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")
    finally CliTestEnv.cleanup env

// =====================================================================
// ledger void — Tests
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LCD-010")>]
let ``void an existing entry via CLI`` () =
    let env = CliTestEnv.create()
    try
        let entryId = CliTestEnv.postEntryViaCli env
        let args = sprintf "ledger void %d --reason \"Duplicate posting\"" entryId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("VOIDED", result.Stdout)
    finally CliTestEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-LCD-011")>]
let ``void with --json flag outputs JSON to stdout`` () =
    let env = CliTestEnv.create()
    try
        let entryId = CliTestEnv.postEntryViaCliJson env
        let args = sprintf "--json ledger void %d --reason \"Duplicate\"" entryId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally CliTestEnv.cleanup env

// --- Void Error Paths ---

[<Fact>]
[<Trait("GherkinId", "FT-LCD-012")>]
let ``void a nonexistent entry prints error to stderr`` () =
    let result = CliRunner.run "ledger void 999999 --reason \"Does not exist\""
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-LCD-013")>]
let ``void with no arguments prints error to stderr`` () =
    let result = CliRunner.run "ledger void"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// ledger show — Tests
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LCD-020")>]
let ``show an existing entry via CLI`` () =
    let env = CliTestEnv.create()
    try
        let entryId = CliTestEnv.postEntryViaCli env
        let args = sprintf "ledger show %d" entryId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("Journal Entry #", result.Stdout)
        Assert.Contains("Lines:", result.Stdout)
    finally CliTestEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-LCD-021")>]
let ``show with --json flag outputs JSON to stdout`` () =
    let env = CliTestEnv.create()
    try
        let entryId = CliTestEnv.postEntryViaCliJson env
        let args = sprintf "--json ledger show %d" entryId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally CliTestEnv.cleanup env

// --- Show Error Paths ---

[<Fact>]
[<Trait("GherkinId", "FT-LCD-022")>]
let ``show a nonexistent entry prints error to stderr`` () =
    let result = CliRunner.run "ledger show 999999"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-LCD-023")>]
let ``show with no entry ID prints error to stderr`` () =
    let result = CliRunner.run "ledger show"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// --json flag consistency (Scenario Outline FT-LCD-030)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LCD-030a")>]
let ``--json flag produces valid JSON for show`` () =
    let env = CliTestEnv.create()
    try
        let entryId = CliTestEnv.postEntryViaCliJson env
        let args = sprintf "--json ledger show %d" entryId
        let result = CliRunner.run args
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally CliTestEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-LCD-030b")>]
let ``--json flag produces valid JSON for void`` () =
    let env = CliTestEnv.create()
    try
        let entryId = CliTestEnv.postEntryViaCliJson env
        let args = sprintf "--json ledger void %d --reason \"JSON test\"" entryId
        let result = CliRunner.run args
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally CliTestEnv.cleanup env
