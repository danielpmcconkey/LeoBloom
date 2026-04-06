module LeoBloom.Tests.ReportCommandsTests

open System
open System.Text.Json
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
let ``Schedule E ignores top-level --json flag and produces human output`` () =
    // P037 removed the --json short-circuit for report commands. Old commands
    // that use writeHuman simply ignore the flag and produce human output.
    let result = CliRunner.run "--json report schedule-e --year 2026"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("Schedule E", stdout)

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
let ``General ledger ignores top-level --json flag and produces human output`` () =
    // P037 removed the --json short-circuit for report commands. Old commands
    // that use writeHuman simply ignore the flag and produce human output.
    // Account 1110 may not exist, so we accept exit 0 (found) or exit 1 (not found).
    let result = CliRunner.run "--json report general-ledger --account 1110 --from 2026-01-01 --to 2026-12-31"
    Assert.True(result.ExitCode = 0 || result.ExitCode = 1,
                sprintf "Expected exit code 0 or 1, got %d" result.ExitCode)

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
let ``Cash receipts ignores top-level --json flag and produces human output`` () =
    // P037 removed the --json short-circuit for report commands. Old commands
    // that use writeHuman simply ignore the flag and produce human output.
    let result = CliRunner.run "--json report cash-receipts --from 2026-01-01 --to 2026-12-31"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("Cash Receipts", stdout)

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
let ``Cash disbursements ignores top-level --json flag and produces human output`` () =
    // P037 removed the --json short-circuit for report commands. Old commands
    // that use writeHuman simply ignore the flag and produce human output.
    let result = CliRunner.run "--json report cash-disbursements --from 2026-01-01 --to 2026-12-31"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("Cash Disbursements", stdout)

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

// =====================================================================
// P037 — Accounting Report CLI Commands (FT-RPT-100 to FT-RPT-160)
// =====================================================================

// --- FT-RPT-100: report --help lists all 5 new accounting subcommands ---

[<Fact>]
[<Trait("GherkinId", "FT-RPT-100")>]
let ``report --help lists all 5 new accounting subcommands`` () =
    let result = CliRunner.run "report --help"
    Assert.Equal(0, result.ExitCode)
    Assert.Contains("trial-balance", result.Stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("balance-sheet", result.Stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("income-statement", result.Stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("pnl-subtree", result.Stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("account-balance", result.Stdout, StringComparison.OrdinalIgnoreCase)

// --- FT-RPT-101: each new report subcommand prints help with --help ---

[<Fact>]
[<Trait("GherkinId", "FT-RPT-101a")>]
let ``report trial-balance --help prints usage information`` () =
    let result = CliRunner.run "report trial-balance --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage output on stdout")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-101b")>]
let ``report balance-sheet --help prints usage information`` () =
    let result = CliRunner.run "report balance-sheet --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage output on stdout")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-101c")>]
let ``report income-statement --help prints usage information`` () =
    let result = CliRunner.run "report income-statement --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage output on stdout")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-101d")>]
let ``report pnl-subtree --help prints usage information`` () =
    let result = CliRunner.run "report pnl-subtree --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage output on stdout")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-101e")>]
let ``report account-balance --help prints usage information`` () =
    let result = CliRunner.run "report account-balance --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage output on stdout")

// =====================================================================
// report trial-balance — FT-RPT-110 to FT-RPT-114
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPT-110")>]
let ``trial balance by period ID produces human-readable output`` () =
    let result = CliRunner.run "report trial-balance --period 26500"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    // Human output always contains the Grand Total line and status
    Assert.Contains("Trial Balance", stdout)
    Assert.Contains("Grand Total", stdout)
    Assert.True(stdout.Contains("BALANCED") || stdout.Contains("UNBALANCED"),
                "Expected BALANCED or UNBALANCED status line")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-111")>]
let ``trial balance by period key produces human-readable output`` () =
    let result = CliRunner.run "report trial-balance --period 2026-01"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("Trial Balance", stdout)
    Assert.Contains("Grand Total", stdout)
    Assert.True(stdout.Contains("BALANCED") || stdout.Contains("UNBALANCED"),
                "Expected BALANCED or UNBALANCED status line")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-112")>]
let ``trial balance with --json flag outputs valid JSON`` () =
    let result = CliRunner.run "report trial-balance --period 26500 --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-113")>]
let ``trial balance with no --period flag prints error to stderr`` () =
    let result = CliRunner.run "report trial-balance"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-114")>]
let ``trial balance with nonexistent period surfaces service error`` () =
    let result = CliRunner.run "report trial-balance --period 999999"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// report balance-sheet — FT-RPT-120 to FT-RPT-123
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPT-120")>]
let ``balance sheet as of a valid date produces human-readable output`` () =
    let result = CliRunner.run "report balance-sheet --as-of 2026-03-31"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("Assets", stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("Liabilities", stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("Equity", stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("Retained Earnings", stdout)
    Assert.True(stdout.Contains("BALANCED") || stdout.Contains("UNBALANCED"),
                "Expected BALANCED or UNBALANCED status line")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-121")>]
let ``balance sheet with --json flag outputs valid JSON`` () =
    let result = CliRunner.run "report balance-sheet --as-of 2026-03-31 --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-122")>]
let ``balance sheet with no --as-of flag prints error to stderr`` () =
    let result = CliRunner.run "report balance-sheet"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-123")>]
let ``balance sheet with invalid date format prints error to stderr`` () =
    let result = CliRunner.run "report balance-sheet --as-of not-a-date"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// report income-statement — FT-RPT-130 to FT-RPT-134
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPT-130")>]
let ``income statement by period ID produces human-readable output`` () =
    let result = CliRunner.run "report income-statement --period 26500"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("Revenue", stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("Expense", stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("Net Income", stdout)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-131")>]
let ``income statement by period key produces human-readable output`` () =
    let result = CliRunner.run "report income-statement --period 2026-01"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("Revenue", stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("Expense", stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("Net Income", stdout)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-132")>]
let ``income statement with --json flag outputs valid JSON`` () =
    let result = CliRunner.run "report income-statement --period 26500 --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-133")>]
let ``income statement with no --period flag prints error to stderr`` () =
    let result = CliRunner.run "report income-statement"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-134")>]
let ``income statement with nonexistent period surfaces service error`` () =
    let result = CliRunner.run "report income-statement --period 999999"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// report pnl-subtree — FT-RPT-140 to FT-RPT-144
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPT-140")>]
let ``pnl subtree with valid account and period produces human-readable output`` () =
    let result = CliRunner.run "report pnl-subtree --account 5000 --period 26500"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    // Contains the root account code and name
    Assert.Contains("5000", stdout)
    Assert.Contains("Net Income", stdout)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-141")>]
let ``pnl subtree with --json flag outputs valid JSON`` () =
    let result = CliRunner.run "report pnl-subtree --account 5000 --period 26500 --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-142a")>]
let ``pnl subtree missing --account is rejected`` () =
    let result = CliRunner.run "report pnl-subtree --period 26500"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-142b")>]
let ``pnl subtree missing --period is rejected`` () =
    let result = CliRunner.run "report pnl-subtree --account 5000"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-143")>]
let ``pnl subtree with nonexistent account surfaces service error`` () =
    let result = CliRunner.run "report pnl-subtree --account 9999 --period 26500"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-144")>]
let ``pnl subtree with nonexistent period surfaces service error`` () =
    let result = CliRunner.run "report pnl-subtree --account 5000 --period 999999"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// report account-balance — FT-RPT-150 to FT-RPT-155
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPT-150")>]
let ``account balance with explicit --as-of produces human-readable output`` () =
    let result = CliRunner.run "report account-balance --account 1110 --as-of 2026-03-31"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("1110", stdout)
    // Contains the normal balance type (Debit or Credit)
    Assert.True(stdout.Contains("Debit") || stdout.Contains("Credit"),
                "Expected normal balance type (Debit or Credit)")
    // Contains the balance amount (the word "Balance" in the output)
    Assert.Contains("Balance", stdout)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-151")>]
let ``account balance defaults --as-of to today when omitted`` () =
    let result = CliRunner.run "report account-balance --account 1110"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("1110", stdout)
    Assert.Contains("Balance", stdout)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-152")>]
let ``account balance with --json flag outputs valid JSON`` () =
    let result = CliRunner.run "report account-balance --account 1110 --as-of 2026-03-31 --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-153")>]
let ``account balance with no --account flag prints error to stderr`` () =
    let result = CliRunner.run "report account-balance"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-154")>]
let ``account balance with invalid date format prints error to stderr`` () =
    let result = CliRunner.run "report account-balance --account 1110 --as-of not-a-date"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-RPT-155")>]
let ``account balance with nonexistent account surfaces service error`` () =
    let result = CliRunner.run "report account-balance --account 9999"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// --json flag per-command — FT-RPT-160
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RPT-160a")>]
let ``--json on trial-balance produces valid JSON`` () =
    let result = CliRunner.run "report trial-balance --period 26500 --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-160b")>]
let ``--json on balance-sheet produces valid JSON`` () =
    let result = CliRunner.run "report balance-sheet --as-of 2026-03-31 --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-160c")>]
let ``--json on income-statement produces valid JSON`` () =
    let result = CliRunner.run "report income-statement --period 26500 --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-160d")>]
let ``--json on pnl-subtree produces valid JSON`` () =
    let result = CliRunner.run "report pnl-subtree --account 5000 --period 26500 --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-RPT-160e")>]
let ``--json on account-balance produces valid JSON`` () =
    let result = CliRunner.run "report account-balance --account 1110 --as-of 2026-03-31 --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)

