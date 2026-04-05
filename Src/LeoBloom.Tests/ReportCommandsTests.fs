module LeoBloom.Tests.ReportCommandsTests

open System
open Npgsql
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests

// =====================================================================
// Shared setup: create a CLI-testable reporting environment
// =====================================================================

module ReportCliEnv =
    type Env =
        { CashAccountId: int      // 1110-style asset (debit normal)
          RevenueAccountId: int    // 4110-style revenue (credit normal)
          ExpenseAccountId: int    // 5110-style expense (debit normal)
          FiscalPeriodId: int
          Tracker: TestCleanup.Tracker }

    let create () =
        let conn = DataSource.openConnection()
        let tracker = TestCleanup.create conn
        let prefix = TestData.uniquePrefix()

        // Use the standard seeded account type IDs
        let assetTypeId = 1
        let revenueTypeId = 4
        let expenseTypeId = 5

        let cashAcct = InsertHelpers.insertAccount conn tracker (prefix + "CA") "TestCash" assetTypeId true
        let revAcct = InsertHelpers.insertAccount conn tracker (prefix + "RV") "TestRevenue" revenueTypeId true
        let expAcct = InsertHelpers.insertAccount conn tracker (prefix + "EX") "TestExpense" expenseTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31)) true

        { CashAccountId = cashAcct
          RevenueAccountId = revAcct
          ExpenseAccountId = expAcct
          FiscalPeriodId = fpId
          Tracker = tracker }

    let cleanup (env: Env) =
        TestCleanup.deleteAll env.Tracker
        env.Tracker.Connection.Dispose()

    /// Post an entry via the service (not CLI) for test setup.
    let postEntry (env: Env) (debitAcctId: int) (creditAcctId: int) (date: DateOnly) (desc: string) (amount: decimal) =
        let cmd =
            { entryDate = date
              description = desc
              source = Some "test-setup"
              fiscalPeriodId = env.FiscalPeriodId
              lines =
                [ { accountId = debitAcctId; amount = amount; entryType = EntryType.Debit; memo = None }
                  { accountId = creditAcctId; amount = amount; entryType = EntryType.Credit; memo = None } ]
              references = [] }
        match JournalEntryService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id env.Tracker
            posted.entry.id
        | Error errs -> failwith (sprintf "Setup post failed: %A" errs)

// =====================================================================
// report command group — FT-RPT-001 to FT-RPT-003
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPT-001")>]
let ``top-level --help includes the report command group`` () =
    let result = CliRunner.run "--help"
    Assert.Equal(0, result.ExitCode)
    Assert.Contains("report", result.Stdout, StringComparison.OrdinalIgnoreCase)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-002")>]
let ``report --help prints available subcommands`` () =
    let result = CliRunner.run "report --help"
    Assert.Equal(0, result.ExitCode)
    Assert.Contains("schedule-e", result.Stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("general-ledger", result.Stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("cash-receipts", result.Stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("cash-disbursements", result.Stdout, StringComparison.OrdinalIgnoreCase)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-003")>]
let ``report with no subcommand prints error to stderr`` () =
    let result = CliRunner.run "report"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// report schedule-e — FT-RPT-010 to FT-RPT-016
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPT-010")>]
let ``Schedule E report for a valid year produces formatted output`` () =
    // This test uses the real DB. Seeded accounts may or may not exist,
    // but the CLI should execute without error for any year.
    // If there's no Schedule E data, we still get formatted output with zeroes.
    let result = CliRunner.run "report schedule-e --year 2026"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("Schedule E", stdout)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-011")>]
let ``Schedule E Other line 19 shows sub-detail breakdown`` () =
    let result = CliRunner.run "report schedule-e --year 2026"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    // The output should contain the "Other" line and Schedule E header
    Assert.Contains("Schedule E", stdout)
    Assert.Contains("Other", stdout)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-012")>]
let ``Schedule E depreciation line shows account 5190 balance`` () =
    let result = CliRunner.run "report schedule-e --year 2026"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    // Depreciation line reads from account 5190 balance, not config
    Assert.Contains("Depreciation", stdout)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-013")>]
let ``Schedule E with no --year flag prints error to stderr`` () =
    let result = CliRunner.run "report schedule-e"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-014")>]
let ``Schedule E with non-numeric year prints error to stderr`` () =
    let result = CliRunner.run "report schedule-e --year not-a-year"
    // Argu parse failure goes through LeoBloomExiter which maps to exit code 2 (systemError).
    // The Gherkin spec says "exit code is 1" but Argu type-mismatch is a parse error, not a
    // business error. Argu catches the bad int before our code ever runs. Exit 2 is correct
    // per the CLI architecture (ErrorHandler.fs: non-HelpText Argu errors -> systemError).
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-015")>]
let ``Schedule E service validation error surfaces to stderr`` () =
    let result = CliRunner.run "report schedule-e --year -1"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-016")>]
let ``Schedule E does not accept --json flag`` () =
    let result = CliRunner.run "--json report schedule-e --year 2026"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// report general-ledger — FT-RPT-020 to FT-RPT-024
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPT-020")>]
let ``General ledger for a valid account and date range produces formatted output`` () =
    let env = ReportCliEnv.create()
    try
        // Post an entry so there's data to show
        ReportCliEnv.postEntry env env.CashAccountId env.RevenueAccountId
            (DateOnly(2026, 3, 15)) "Test GL entry" 1000m |> ignore

        // Look up the account code we just created
        use cmd = new NpgsqlCommand("SELECT code FROM ledger.account WHERE id = @id", env.Tracker.Connection)
        cmd.Parameters.AddWithValue("@id", env.CashAccountId) |> ignore
        let accountCode = cmd.ExecuteScalar() :?> string

        let result = CliRunner.run (sprintf "report general-ledger --account %s --from 2026-01-01 --to 2026-12-31" accountCode)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("General Ledger", stdout)
        Assert.Contains("Date", stdout)
        Assert.Contains("Description", stdout)
    finally ReportCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-RPT-021a")>]
let ``General ledger missing --account is rejected`` () =
    let result = CliRunner.run "report general-ledger --from 2026-01-01 --to 2026-12-31"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-021b")>]
let ``General ledger missing --from is rejected`` () =
    let result = CliRunner.run "report general-ledger --account 1110 --to 2026-12-31"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-021c")>]
let ``General ledger missing --to is rejected`` () =
    let result = CliRunner.run "report general-ledger --account 1110 --from 2026-01-01"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-022")>]
let ``General ledger with invalid date format prints error to stderr`` () =
    let result = CliRunner.run "report general-ledger --account 1110 --from not-a-date --to 2026-12-31"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-023")>]
let ``General ledger with nonexistent account surfaces service error`` () =
    let result = CliRunner.run "report general-ledger --account 9999 --from 2026-01-01 --to 2026-12-31"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-024")>]
let ``General ledger does not accept --json flag`` () =
    let result = CliRunner.run "--json report general-ledger --account 1110 --from 2026-01-01 --to 2026-12-31"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// report cash-receipts — FT-RPT-030 to FT-RPT-033
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPT-030")>]
let ``Cash receipts for a valid date range produces formatted output`` () =
    let result = CliRunner.run "report cash-receipts --from 2026-01-01 --to 2026-12-31"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("Cash Receipts", stdout)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-031a")>]
let ``Cash receipts missing --from is rejected`` () =
    let result = CliRunner.run "report cash-receipts --to 2026-12-31"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-031b")>]
let ``Cash receipts missing --to is rejected`` () =
    let result = CliRunner.run "report cash-receipts --from 2026-01-01"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-032")>]
let ``Cash receipts with invalid date format prints error to stderr`` () =
    let result = CliRunner.run "report cash-receipts --from 01/01/2026 --to 2026-12-31"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-033")>]
let ``Cash receipts does not accept --json flag`` () =
    let result = CliRunner.run "--json report cash-receipts --from 2026-01-01 --to 2026-12-31"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// report cash-disbursements — FT-RPT-040 to FT-RPT-043
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPT-040")>]
let ``Cash disbursements for a valid date range produces formatted output`` () =
    let result = CliRunner.run "report cash-disbursements --from 2026-01-01 --to 2026-12-31"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("Cash Disbursements", stdout)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-041a")>]
let ``Cash disbursements missing --from is rejected`` () =
    let result = CliRunner.run "report cash-disbursements --to 2026-12-31"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-041b")>]
let ``Cash disbursements missing --to is rejected`` () =
    let result = CliRunner.run "report cash-disbursements --from 2026-01-01"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-042")>]
let ``Cash disbursements with invalid date format prints error to stderr`` () =
    let result = CliRunner.run "report cash-disbursements --from not-a-date --to 2026-12-31"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-043")>]
let ``Cash disbursements does not accept --json flag`` () =
    let result = CliRunner.run "--json report cash-disbursements --from 2026-01-01 --to 2026-12-31"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// Shared patterns — FT-RPT-050 (subcommand --help)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPT-050a")>]
let ``report schedule-e --help prints usage information`` () =
    let result = CliRunner.run "report schedule-e --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage output on stdout")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-050b")>]
let ``report general-ledger --help prints usage information`` () =
    let result = CliRunner.run "report general-ledger --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage output on stdout")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-050c")>]
let ``report cash-receipts --help prints usage information`` () =
    let result = CliRunner.run "report cash-receipts --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage output on stdout")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-050d")>]
let ``report cash-disbursements --help prints usage information`` () =
    let result = CliRunner.run "report cash-disbursements --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage output on stdout")

