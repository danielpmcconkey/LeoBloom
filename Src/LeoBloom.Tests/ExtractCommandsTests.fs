module LeoBloom.Tests.ExtractCommandsTests

open System
open System.Text.Json
open Npgsql
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests
open LeoBloom.Tests.PortfolioTestHelpers

// =====================================================================
// ExtractCliEnv — committed data for CLI tests that need populated results
// (FT-EXT-051, 052, 053)
// =====================================================================

module ExtractCliEnv =
    type Env =
        { DebitAccountId:     int
          CreditAccountId:    int
          FiscalPeriodId:     int
          InvestmentAccountId: int
          TaxBucketId:        int
          AccountGroupId:     int
          Symbol:             string
          Connection:         NpgsqlConnection }

    let create () =
        Log.initialize()
        let conn   = DataSource.openConnection()
        let prefix = TestData.uniquePrefix()

        // Use seeded account type IDs
        let assetTypeId   = 1
        let revenueTypeId = 4

        let debitAcctId, creditAcctId, fpId =
            use txn = conn.BeginTransaction()
            let dAcct = InsertHelpers.insertAccount txn (prefix + "D1") ("ExtD " + prefix) assetTypeId   true
            let cAcct = InsertHelpers.insertAccount txn (prefix + "C1") ("ExtC " + prefix) revenueTypeId true
            let fp    = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31)) true
            // Post a journal entry so balances / je-lines have data
            let cmd =
                { entryDate       = DateOnly(2026, 3, 15)
                  description     = "Extract CLI test entry"
                  source          = None
                  fiscalPeriodId  = fp
                  lines =
                    [ { accountId = dAcct; amount = 1000m; entryType = EntryType.Debit;  memo = None }
                      { accountId = cAcct; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
                  references = []
                  adjustmentForPeriodId = None }
            match JournalEntryService.post txn cmd with
            | Ok _    -> ()
            | Error e -> failwith (sprintf "ExtractCliEnv setup failed: %A" e)
            txn.Commit()
            (dAcct, cAcct, fp)

        let tbId, agId, iaId, sym =
            use txn = conn.BeginTransaction()
            let tb  = PortfolioInsertHelpers.insertTaxBucket       txn (prefix + "_tb")
            let ag  = PortfolioInsertHelpers.insertAccountGroup    txn (prefix + "_ag")
            let ia  = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_ia") tb ag
            let sym = PortfolioInsertHelpers.insertFund txn (prefix + "SY") (prefix + " Fund")
            PortfolioInsertHelpers.insertPosition txn ia sym (DateOnly(2026, 3, 31)) 100m 10m 24500m 20000m |> ignore
            txn.Commit()
            (tb, ag, ia, sym)

        { DebitAccountId      = debitAcctId
          CreditAccountId     = creditAcctId
          FiscalPeriodId      = fpId
          InvestmentAccountId = iaId
          TaxBucketId         = tbId
          AccountGroupId      = agId
          Symbol              = sym
          Connection          = conn }

    let cleanup (env: Env) =
        // Ledger: JE lines → journal entries → fiscal period → accounts
        use cmd1 = new NpgsqlCommand(
            "DELETE FROM ledger.journal_entry_line WHERE journal_entry_id IN \
             (SELECT id FROM ledger.journal_entry WHERE fiscal_period_id = @fp)", env.Connection)
        cmd1.Parameters.AddWithValue("@fp", env.FiscalPeriodId) |> ignore
        cmd1.ExecuteNonQuery() |> ignore
        use cmd2 = new NpgsqlCommand("DELETE FROM ledger.journal_entry WHERE fiscal_period_id = @fp", env.Connection)
        cmd2.Parameters.AddWithValue("@fp", env.FiscalPeriodId) |> ignore
        cmd2.ExecuteNonQuery() |> ignore
        use cmd3 = new NpgsqlCommand("DELETE FROM ledger.fiscal_period WHERE id = @id", env.Connection)
        cmd3.Parameters.AddWithValue("@id", env.FiscalPeriodId) |> ignore
        cmd3.ExecuteNonQuery() |> ignore
        use cmd4 = new NpgsqlCommand("DELETE FROM ledger.account WHERE id = @id", env.Connection)
        cmd4.Parameters.AddWithValue("@id", env.DebitAccountId) |> ignore
        cmd4.ExecuteNonQuery() |> ignore
        use cmd5 = new NpgsqlCommand("DELETE FROM ledger.account WHERE id = @id", env.Connection)
        cmd5.Parameters.AddWithValue("@id", env.CreditAccountId) |> ignore
        cmd5.ExecuteNonQuery() |> ignore
        // Portfolio: positions → investment_account → fund → tax_bucket, account_group
        use cmd6 = new NpgsqlCommand("DELETE FROM portfolio.position WHERE investment_account_id = @ia", env.Connection)
        cmd6.Parameters.AddWithValue("@ia", env.InvestmentAccountId) |> ignore
        cmd6.ExecuteNonQuery() |> ignore
        use cmd7 = new NpgsqlCommand("DELETE FROM portfolio.investment_account WHERE id = @id", env.Connection)
        cmd7.Parameters.AddWithValue("@id", env.InvestmentAccountId) |> ignore
        cmd7.ExecuteNonQuery() |> ignore
        use cmd8 = new NpgsqlCommand("DELETE FROM portfolio.fund WHERE symbol = @s", env.Connection)
        cmd8.Parameters.AddWithValue("@s", env.Symbol) |> ignore
        cmd8.ExecuteNonQuery() |> ignore
        use cmd9 = new NpgsqlCommand("DELETE FROM portfolio.tax_bucket WHERE id = @id", env.Connection)
        cmd9.Parameters.AddWithValue("@id", env.TaxBucketId) |> ignore
        cmd9.ExecuteNonQuery() |> ignore
        use cmd10 = new NpgsqlCommand("DELETE FROM portfolio.account_group WHERE id = @id", env.Connection)
        cmd10.Parameters.AddWithValue("@id", env.AccountGroupId) |> ignore
        cmd10.ExecuteNonQuery() |> ignore
        env.Connection.Dispose()

// =====================================================================
// extract --help and routing — FT-EXT-001, 002, 003
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-001")>]
let ``top-level --help includes the extract command group`` () =
    let result = CliRunner.run "--help"
    Assert.Equal(0, result.ExitCode)
    Assert.Contains("extract", result.Stdout, StringComparison.OrdinalIgnoreCase)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-002")>]
let ``extract --help lists all four subcommands`` () =
    let result = CliRunner.run "extract --help"
    Assert.Equal(0, result.ExitCode)
    Assert.Contains("account-tree", result.Stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("balances",     result.Stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("positions",    result.Stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("je-lines",     result.Stdout, StringComparison.OrdinalIgnoreCase)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-003")>]
let ``extract with no subcommand prints error to stderr and exits 2`` () =
    let result = CliRunner.run "extract"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// extract account-tree — FT-EXT-010, 011, 012
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-010")>]
let ``account-tree with no flags outputs valid JSON`` () =
    let result = CliRunner.run "extract account-tree"
    Assert.Equal(0, result.ExitCode)
    let clean = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(clean)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-011")>]
let ``account-tree with --json flag still outputs valid JSON`` () =
    let result = CliRunner.run "extract account-tree --json"
    Assert.Equal(0, result.ExitCode)
    let clean = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(clean)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-012")>]
let ``account-tree --help prints usage information`` () =
    let result = CliRunner.run "extract account-tree --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage information on stdout")

// =====================================================================
// extract balances — FT-EXT-020, 021, 022, 023, 024
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-020")>]
let ``balances with a valid --as-of date outputs valid JSON`` () =
    let result = CliRunner.run "extract balances --as-of 2026-04-11"
    Assert.Equal(0, result.ExitCode)
    let clean = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(clean)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-021")>]
let ``balances with --json flag still outputs valid JSON`` () =
    let result = CliRunner.run "extract balances --as-of 2026-04-11 --json"
    Assert.Equal(0, result.ExitCode)
    let clean = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(clean)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-022")>]
let ``balances with no --as-of flag prints error to stderr and exits 2`` () =
    let result = CliRunner.run "extract balances"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-EXT-023")>]
let ``balances with invalid date format prints error to stderr and exits 1`` () =
    let result = CliRunner.run "extract balances --as-of not-a-date"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-EXT-024")>]
let ``balances --help prints usage information`` () =
    let result = CliRunner.run "extract balances --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage information on stdout")

// =====================================================================
// extract positions — FT-EXT-030, 031, 032, 033, 034
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-030")>]
let ``positions with a valid --as-of date outputs valid JSON`` () =
    let result = CliRunner.run "extract positions --as-of 2026-04-11"
    Assert.Equal(0, result.ExitCode)
    let clean = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(clean)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-031")>]
let ``positions with no --as-of flag defaults to today and outputs valid JSON`` () =
    let result = CliRunner.run "extract positions"
    Assert.Equal(0, result.ExitCode)
    let clean = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(clean)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-032")>]
let ``positions with --json flag still outputs valid JSON`` () =
    let result = CliRunner.run "extract positions --as-of 2026-04-11 --json"
    Assert.Equal(0, result.ExitCode)
    let clean = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(clean)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-033")>]
let ``positions with invalid date format prints error to stderr and exits 1`` () =
    let result = CliRunner.run "extract positions --as-of 04/11/2026"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-EXT-034")>]
let ``positions --help prints usage information`` () =
    let result = CliRunner.run "extract positions --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage information on stdout")

// =====================================================================
// extract je-lines — FT-EXT-040, 041, 042, 043, 044
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-040")>]
let ``je-lines with a valid --fiscal-period-id outputs valid JSON`` () =
    // Uses a non-existent period ID — returns empty array, still valid JSON + exit 0
    let result = CliRunner.run "extract je-lines --fiscal-period-id 999999"
    Assert.Equal(0, result.ExitCode)
    let clean = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(clean)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-041")>]
let ``je-lines with --json flag still outputs valid JSON`` () =
    let result = CliRunner.run "extract je-lines --fiscal-period-id 999999 --json"
    Assert.Equal(0, result.ExitCode)
    let clean = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(clean)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-042")>]
let ``je-lines with no --fiscal-period-id prints error to stderr and exits 2`` () =
    let result = CliRunner.run "extract je-lines"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-EXT-043")>]
let ``je-lines with non-numeric fiscal period ID prints error to stderr`` () =
    let result = CliRunner.run "extract je-lines --fiscal-period-id not-an-id"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-EXT-044")>]
let ``je-lines --help prints usage information`` () =
    let result = CliRunner.run "extract je-lines --help"
    Assert.Equal(0, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stdout), "Expected usage information on stdout")

// =====================================================================
// JSON snake_case field contracts — FT-EXT-050, 051, 052, 053
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-050")>]
let ``account-tree JSON output uses snake_case field names`` () =
    // account-tree always returns data (seed accounts exist)
    let result = CliRunner.run "extract account-tree"
    Assert.Equal(0, result.ExitCode)
    let clean = CliRunner.stripLogLines result.Stdout
    Assert.Contains("account_type",   clean)
    Assert.Contains("normal_balance", clean)
    Assert.Contains("is_active",      clean)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-051")>]
let ``balances JSON output uses snake_case field names`` () =
    let env = ExtractCliEnv.create()
    try
        let result = CliRunner.run (sprintf "extract balances --as-of 2026-12-31")
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        Assert.Contains("account_id", clean)
    finally ExtractCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-EXT-052")>]
let ``positions JSON output uses snake_case field names`` () =
    let env = ExtractCliEnv.create()
    try
        let result = CliRunner.run "extract positions --as-of 2026-12-31"
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        Assert.Contains("investment_account_id", clean)
        Assert.Contains("tax_bucket",            clean)
        Assert.Contains("current_value",         clean)
    finally ExtractCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-EXT-053")>]
let ``je-lines JSON output uses snake_case field names`` () =
    let env = ExtractCliEnv.create()
    try
        let result = CliRunner.run (sprintf "extract je-lines --fiscal-period-id %d" env.FiscalPeriodId)
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        Assert.Contains("journal_entry_id", clean)
        Assert.Contains("entry_type",       clean)
        Assert.Contains("account_code",     clean)
    finally ExtractCliEnv.cleanup env
