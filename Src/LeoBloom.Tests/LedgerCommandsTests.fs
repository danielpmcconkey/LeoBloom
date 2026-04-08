module LeoBloom.Tests.LedgerCommandsTests

open System
open System.Text.Json
open Npgsql
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
          DebitAccountTypeId: int
          CreditAccountTypeId: int
          FiscalPeriodId: int
          Connection: NpgsqlConnection
          mutable TrackedJournalEntryIds: int list }

    let create () =
        let conn = DataSource.openConnection()
        let prefix = TestData.uniquePrefix()
        let (atDebit, atCredit, acctDebit, acctCredit, fpId) =
            use txn = conn.BeginTransaction()
            let atDb = InsertHelpers.insertAccountType txn (prefix + "_db") "debit"
            let atCr = InsertHelpers.insertAccountType txn (prefix + "_cr") "credit"
            let aDb  = InsertHelpers.insertAccount txn (prefix + "D1") "DebitAcct" atDb true
            let aCr  = InsertHelpers.insertAccount txn (prefix + "C1") "CreditAcct" atCr true
            let fp   = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
            txn.Commit()
            (atDb, atCr, aDb, aCr, fp)
        { DebitAccountId = acctDebit
          CreditAccountId = acctCredit
          DebitAccountTypeId = atDebit
          CreditAccountTypeId = atCredit
          FiscalPeriodId = fpId
          Connection = conn
          TrackedJournalEntryIds = [] }

    let cleanup (env: Env) =
        // Delete journal entry lines and entries for any tracked JEs
        for jeId in env.TrackedJournalEntryIds do
            use cmd0 = new NpgsqlCommand("DELETE FROM ledger.journal_entry_reference WHERE journal_entry_id = @id", env.Connection)
            cmd0.Parameters.AddWithValue("@id", jeId) |> ignore
            cmd0.ExecuteNonQuery() |> ignore
            use cmd1 = new NpgsqlCommand("DELETE FROM ledger.journal_entry_line WHERE journal_entry_id = @id", env.Connection)
            cmd1.Parameters.AddWithValue("@id", jeId) |> ignore
            cmd1.ExecuteNonQuery() |> ignore
            use cmd2 = new NpgsqlCommand("DELETE FROM ledger.journal_entry WHERE id = @id", env.Connection)
            cmd2.Parameters.AddWithValue("@id", jeId) |> ignore
            cmd2.ExecuteNonQuery() |> ignore
        // Also catch any un-tracked JEs for our fiscal period
        use cmdRef = new NpgsqlCommand(
            "DELETE FROM ledger.journal_entry_reference WHERE journal_entry_id IN \
             (SELECT id FROM ledger.journal_entry WHERE fiscal_period_id = @fp)", env.Connection)
        cmdRef.Parameters.AddWithValue("@fp", env.FiscalPeriodId) |> ignore
        cmdRef.ExecuteNonQuery() |> ignore
        use cmd3 = new NpgsqlCommand(
            "DELETE FROM ledger.journal_entry_line WHERE journal_entry_id IN \
             (SELECT id FROM ledger.journal_entry WHERE fiscal_period_id = @fp)", env.Connection)
        cmd3.Parameters.AddWithValue("@fp", env.FiscalPeriodId) |> ignore
        cmd3.ExecuteNonQuery() |> ignore
        use cmd4 = new NpgsqlCommand("DELETE FROM ledger.journal_entry WHERE fiscal_period_id = @fp", env.Connection)
        cmd4.Parameters.AddWithValue("@fp", env.FiscalPeriodId) |> ignore
        cmd4.ExecuteNonQuery() |> ignore
        use cmd5 = new NpgsqlCommand("DELETE FROM ledger.fiscal_period WHERE id = @id", env.Connection)
        cmd5.Parameters.AddWithValue("@id", env.FiscalPeriodId) |> ignore
        cmd5.ExecuteNonQuery() |> ignore
        use cmd6 = new NpgsqlCommand("DELETE FROM ledger.account WHERE id = @id", env.Connection)
        cmd6.Parameters.AddWithValue("@id", env.DebitAccountId) |> ignore
        cmd6.ExecuteNonQuery() |> ignore
        use cmd7 = new NpgsqlCommand("DELETE FROM ledger.account WHERE id = @id", env.Connection)
        cmd7.Parameters.AddWithValue("@id", env.CreditAccountId) |> ignore
        cmd7.ExecuteNonQuery() |> ignore
        use cmd8 = new NpgsqlCommand("DELETE FROM ledger.account_type WHERE id = @id", env.Connection)
        cmd8.Parameters.AddWithValue("@id", env.DebitAccountTypeId) |> ignore
        cmd8.ExecuteNonQuery() |> ignore
        use cmd9 = new NpgsqlCommand("DELETE FROM ledger.account_type WHERE id = @id", env.Connection)
        cmd9.Parameters.AddWithValue("@id", env.CreditAccountTypeId) |> ignore
        cmd9.ExecuteNonQuery() |> ignore
        env.Connection.Dispose()

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
        env.TrackedJournalEntryIds <- entryId :: env.TrackedJournalEntryIds
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
        env.TrackedJournalEntryIds <- entryId :: env.TrackedJournalEntryIds
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
            env.TrackedJournalEntryIds <- Int32.Parse(idStr) :: env.TrackedJournalEntryIds
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
        env.TrackedJournalEntryIds <- entryId :: env.TrackedJournalEntryIds
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
            env.TrackedJournalEntryIds <- Int32.Parse(idStr) :: env.TrackedJournalEntryIds
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
            env.TrackedJournalEntryIds <- Int32.Parse(idStr) :: env.TrackedJournalEntryIds
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
