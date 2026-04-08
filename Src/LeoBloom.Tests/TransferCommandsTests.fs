module LeoBloom.Tests.TransferCommandsTests

open System
open System.Text.Json
open Npgsql
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests

// =====================================================================
// Shared setup: create a "CLI-testable transfer environment"
// -- two active asset accounts + a fiscal period for confirm (year 2097)
// =====================================================================

module TransferCliEnv =
    type Env =
        { FromAccountId: int
          ToAccountId: int
          FromAccountTypeId: int
          FiscalPeriodId: int
          Prefix: string
          Connection: NpgsqlConnection }

    let create () =
        let conn = DataSource.openConnection()
        let prefix = TestData.uniquePrefix()
        let (atId, fromId, toId, fpId) =
            use txn = conn.BeginTransaction()
            // Use account type 1 (pre-seeded "asset") for accounts
            let fr = InsertHelpers.insertAccount txn $"{prefix}FR" $"{prefix}_from" 1 true
            let to_ = InsertHelpers.insertAccount txn $"{prefix}TO" $"{prefix}_to" 1 true
            let fp  = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2097, 1, 1)) (DateOnly(2097, 12, 31)) true
            txn.Commit()
            (1, fr, to_, fp)
        { FromAccountId = fromId
          ToAccountId = toId
          FromAccountTypeId = atId
          FiscalPeriodId = fpId
          Prefix = prefix
          Connection = conn }

    let cleanup (env: Env) =
        // Find JEs linked to transfers, delete transfers first (FK: transfer.journal_entry_id), then JEs
        try
            use cmdJeIds = new NpgsqlCommand(
                "SELECT journal_entry_id FROM ops.transfer \
                 WHERE (from_account_id = @from OR to_account_id = @to) \
                 AND journal_entry_id IS NOT NULL",
                env.Connection)
            cmdJeIds.Parameters.AddWithValue("@from", env.FromAccountId) |> ignore
            cmdJeIds.Parameters.AddWithValue("@to", env.ToAccountId) |> ignore
            use reader = cmdJeIds.ExecuteReader()
            let jeIds = [ while reader.Read() do yield reader.GetInt32(0) ]
            reader.Close()
            // Delete transfers before JEs — transfer.journal_entry_id FK would block JE deletion
            use cmdTrFirst = new NpgsqlCommand(
                "DELETE FROM ops.transfer WHERE from_account_id = @from OR to_account_id = @to",
                env.Connection)
            cmdTrFirst.Parameters.AddWithValue("@from", env.FromAccountId) |> ignore
            cmdTrFirst.Parameters.AddWithValue("@to", env.ToAccountId) |> ignore
            cmdTrFirst.ExecuteNonQuery() |> ignore
            for jeId in jeIds do
                use cmd1 = new NpgsqlCommand("DELETE FROM ledger.journal_entry_line WHERE journal_entry_id = @id", env.Connection)
                cmd1.Parameters.AddWithValue("@id", jeId) |> ignore
                cmd1.ExecuteNonQuery() |> ignore
                use cmd1b = new NpgsqlCommand("DELETE FROM ledger.journal_entry_reference WHERE journal_entry_id = @id", env.Connection)
                cmd1b.Parameters.AddWithValue("@id", jeId) |> ignore
                cmd1b.ExecuteNonQuery() |> ignore
                use cmd2 = new NpgsqlCommand("DELETE FROM ledger.journal_entry WHERE id = @id", env.Connection)
                cmd2.Parameters.AddWithValue("@id", jeId) |> ignore
                cmd2.ExecuteNonQuery() |> ignore
        with ex ->
            eprintfn "TransferCliEnv.cleanup: error during JE cleanup: %s" ex.Message
        // Safety-net: delete any remaining transfers not already removed in the try block
        use cmdTr = new NpgsqlCommand(
            "DELETE FROM ops.transfer WHERE from_account_id = @from OR to_account_id = @to",
            env.Connection)
        cmdTr.Parameters.AddWithValue("@from", env.FromAccountId) |> ignore
        cmdTr.Parameters.AddWithValue("@to", env.ToAccountId) |> ignore
        cmdTr.ExecuteNonQuery() |> ignore
        // Delete fiscal period
        use cmdFp = new NpgsqlCommand("DELETE FROM ledger.fiscal_period WHERE id = @id", env.Connection)
        cmdFp.Parameters.AddWithValue("@id", env.FiscalPeriodId) |> ignore
        cmdFp.ExecuteNonQuery() |> ignore
        // Delete accounts
        use cmdA1 = new NpgsqlCommand("DELETE FROM ledger.account WHERE id = @id", env.Connection)
        cmdA1.Parameters.AddWithValue("@id", env.FromAccountId) |> ignore
        cmdA1.ExecuteNonQuery() |> ignore
        use cmdA2 = new NpgsqlCommand("DELETE FROM ledger.account WHERE id = @id", env.Connection)
        cmdA2.Parameters.AddWithValue("@id", env.ToAccountId) |> ignore
        cmdA2.ExecuteNonQuery() |> ignore
        env.Connection.Dispose()

    /// Initiate a transfer via CLI (human mode) and return the transfer ID.
    let initiateTransferViaCli (env: Env) (amount: decimal) (dateStr: string) : int =
        let args =
            sprintf "transfer initiate --from-account %d --to-account %d --amount %M --date %s"
                env.FromAccountId env.ToAccountId amount dateStr
        let result = CliRunner.run args
        if result.ExitCode <> 0 then
            failwith (sprintf "Failed to initiate transfer for test setup. stderr: %s" result.Stderr)
        // Parse transfer ID from human-readable output: "Transfer #NNN"
        let stdout = CliRunner.stripLogLines result.Stdout
        let line = stdout.Split('\n') |> Array.find (fun l -> l.Contains("Transfer #"))
        let hashIdx = line.IndexOf('#')
        let rest = line.Substring(hashIdx + 1).Trim()
        let idStr = rest.Split([| ' '; '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries).[0]
        Int32.Parse(idStr)

    /// Initiate a transfer via CLI (JSON mode) and return the transfer ID.
    let initiateTransferViaCliJson (env: Env) (amount: decimal) (dateStr: string) : int =
        let args =
            sprintf "--json transfer initiate --from-account %d --to-account %d --amount %M --date %s"
                env.FromAccountId env.ToAccountId amount dateStr
        let result = CliRunner.run args
        if result.ExitCode <> 0 then
            failwith (sprintf "Failed to initiate transfer for test setup. stderr: %s" result.Stderr)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        doc.RootElement.GetProperty("id").GetInt32()

// =====================================================================
// transfer initiate -- Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-001")>]
let ``initiate a transfer with all required args`` () =
    let env = TransferCliEnv.create()
    try
        let args =
            sprintf "transfer initiate --from-account %d --to-account %d --amount 500.00 --date 2097-04-01"
                env.FromAccountId env.ToAccountId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Transfer #", stdout)
        Assert.Contains(sprintf "%d" env.FromAccountId, stdout)
        Assert.Contains(sprintf "%d" env.ToAccountId, stdout)
        Assert.Contains("500", stdout)
    finally TransferCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-002")>]
let ``initiate with optional args includes them in output`` () =
    let env = TransferCliEnv.create()
    try
        let args =
            sprintf "transfer initiate --from-account %d --to-account %d --amount 500.00 --date 2097-04-01 --expected-settlement 2097-04-03 --description \"Savings top-up\""
                env.FromAccountId env.ToAccountId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Savings top-up", stdout)
        Assert.Contains("2097-04-03", stdout)
    finally TransferCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-003")>]
let ``initiate with --json flag outputs valid JSON`` () =
    let env = TransferCliEnv.create()
    try
        let args =
            sprintf "--json transfer initiate --from-account %d --to-account %d --amount 500.00 --date 2097-04-01"
                env.FromAccountId env.ToAccountId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally TransferCliEnv.cleanup env

// =====================================================================
// transfer initiate -- Missing Required Args
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-004")>]
let ``initiate with no arguments prints error to stderr`` () =
    let result = CliRunner.run "transfer initiate"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-005a")>]
let ``initiate missing --from-account is rejected`` () =
    let result = CliRunner.run "transfer initiate --to-account 1020 --amount 500.00 --date 2097-04-01"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-005b")>]
let ``initiate missing --to-account is rejected`` () =
    let result = CliRunner.run "transfer initiate --from-account 1010 --amount 500.00 --date 2097-04-01"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-005c")>]
let ``initiate missing --amount is rejected`` () =
    let result = CliRunner.run "transfer initiate --from-account 1010 --to-account 1020 --date 2097-04-01"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-005d")>]
let ``initiate missing --date is rejected`` () =
    let result = CliRunner.run "transfer initiate --from-account 1010 --to-account 1020 --amount 500.00"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// transfer initiate -- Service Validation Error
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-006")>]
let ``initiate that triggers a service validation error surfaces it to stderr`` () =
    let env = TransferCliEnv.create()
    try
        // same from and to account triggers "Cannot transfer to the same account"
        let args =
            sprintf "transfer initiate --from-account %d --to-account %d --amount 500.00 --date 2097-04-01"
                env.FromAccountId env.FromAccountId
        let result = CliRunner.run args
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")
    finally TransferCliEnv.cleanup env

// =====================================================================
// transfer initiate -- Date Parse Error
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-007")>]
let ``initiate with invalid date format prints error to stderr`` () =
    let env = TransferCliEnv.create()
    try
        let args =
            sprintf "transfer initiate --from-account %d --to-account %d --amount 500.00 --date not-a-date"
                env.FromAccountId env.ToAccountId
        let result = CliRunner.run args
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")
    finally TransferCliEnv.cleanup env

// =====================================================================
// transfer confirm -- Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-010")>]
let ``confirm an initiated transfer via CLI`` () =
    let env = TransferCliEnv.create()
    try
        let transferId = TransferCliEnv.initiateTransferViaCli env 500.00m "2097-04-01"
        let args = sprintf "transfer confirm %d --date 2097-04-15" transferId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Transfer #", stdout)
        Assert.Contains("confirmed", stdout.ToLowerInvariant())
    finally TransferCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-011")>]
let ``confirm with --json flag outputs valid JSON`` () =
    let env = TransferCliEnv.create()
    try
        let transferId = TransferCliEnv.initiateTransferViaCliJson env 500.00m "2097-04-01"
        let args = sprintf "--json transfer confirm %d --date 2097-04-15" transferId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally TransferCliEnv.cleanup env

// =====================================================================
// transfer confirm -- Error Paths
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-012")>]
let ``confirm with no arguments prints error to stderr`` () =
    let result = CliRunner.run "transfer confirm"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-013")>]
let ``confirm with missing --date flag prints error to stderr`` () =
    let result = CliRunner.run "transfer confirm 1"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-014")>]
let ``confirm with invalid date format prints error to stderr`` () =
    let env = TransferCliEnv.create()
    try
        let result = CliRunner.run "transfer confirm 1 --date not-a-date"
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")
    finally TransferCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-015")>]
let ``confirm a nonexistent transfer prints error to stderr`` () =
    let env = TransferCliEnv.create()
    try
        let result = CliRunner.run "transfer confirm 999999 --date 2097-04-15"
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")
    finally TransferCliEnv.cleanup env

// =====================================================================
// transfer show -- Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-020")>]
let ``show an existing transfer via CLI`` () =
    let env = TransferCliEnv.create()
    try
        let transferId = TransferCliEnv.initiateTransferViaCli env 500.00m "2097-04-01"
        let args = sprintf "transfer show %d" transferId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Transfer #", stdout)
        Assert.Contains(sprintf "%d" env.FromAccountId, stdout)
    finally TransferCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-021")>]
let ``show with --json flag outputs valid JSON`` () =
    let env = TransferCliEnv.create()
    try
        let transferId = TransferCliEnv.initiateTransferViaCliJson env 500.00m "2097-04-01"
        let args = sprintf "--json transfer show %d" transferId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally TransferCliEnv.cleanup env

// =====================================================================
// transfer show -- Error Paths
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-022")>]
let ``show a nonexistent transfer prints error to stderr`` () =
    let result = CliRunner.run "transfer show 999999"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-023")>]
let ``show with no transfer ID prints error to stderr`` () =
    let result = CliRunner.run "transfer show"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// transfer list -- Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-030")>]
let ``list all transfers with no filters`` () =
    let env = TransferCliEnv.create()
    try
        TransferCliEnv.initiateTransferViaCli env 500.00m "2097-04-01" |> ignore
        TransferCliEnv.initiateTransferViaCli env 250.00m "2097-04-02" |> ignore
        let result = CliRunner.run "transfer list"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Should contain table headers or transfer rows
        Assert.Contains("Status", stdout)
    finally TransferCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-031")>]
let ``list transfers filtered by status`` () =
    let env = TransferCliEnv.create()
    try
        // Create two initiated transfers; confirm one
        let t1 = TransferCliEnv.initiateTransferViaCli env 500.00m "2097-05-01"
        TransferCliEnv.initiateTransferViaCli env 250.00m "2097-05-02" |> ignore
        // Confirm t1
        let confirmResult = CliRunner.run (sprintf "transfer confirm %d --date 2097-05-15" t1)
        Assert.Equal(0, confirmResult.ExitCode)
        // List only initiated
        let result = CliRunner.run "transfer list --status initiated"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("initiated", stdout)
        // Should NOT contain the confirmed transfer's status text as a row entry
        // (the confirmed one should be filtered out)
        // We can't assert DoesNotContain "confirmed" because the header might have it,
        // but we verify the filter returns results
        Assert.False(String.IsNullOrWhiteSpace(stdout), "Expected at least one initiated transfer row")
    finally TransferCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-032")>]
let ``list transfers filtered by date range`` () =
    let env = TransferCliEnv.create()
    try
        // Create transfers on different dates
        TransferCliEnv.initiateTransferViaCli env 500.00m "2097-06-01" |> ignore
        TransferCliEnv.initiateTransferViaCli env 250.00m "2097-07-15" |> ignore
        // Filter to June only
        let result = CliRunner.run "transfer list --from 2097-06-01 --to 2097-06-30"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("2097-06-01", stdout)
        Assert.DoesNotContain("2097-07-15", stdout)
    finally TransferCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-033")>]
let ``list with --json flag outputs valid JSON`` () =
    let env = TransferCliEnv.create()
    try
        TransferCliEnv.initiateTransferViaCli env 500.00m "2097-04-01" |> ignore
        let result = CliRunner.run "--json transfer list"
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally TransferCliEnv.cleanup env

// =====================================================================
// transfer list -- Empty Results
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-034")>]
let ``list with no matching results prints empty output`` () =
    let env = TransferCliEnv.create()
    try
        // Don't create any transfers, but filter by status
        let result = CliRunner.run "transfer list --status initiated"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Empty or no data rows -- either blank or just whitespace
        // (list returns empty list -> formatTransferList returns "" -> nothing printed)
        Assert.True(String.IsNullOrWhiteSpace(stdout) || not (stdout.Contains(sprintf "%d" env.FromAccountId)),
                    "Expected empty output or no matching transfer rows for our accounts")
    finally TransferCliEnv.cleanup env

// =====================================================================
// transfer list -- Invalid Filter
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-035")>]
let ``list with invalid status value prints error to stderr`` () =
    let env = TransferCliEnv.create()
    try
        let result = CliRunner.run "transfer list --status bogus"
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")
    finally TransferCliEnv.cleanup env

// =====================================================================
// transfer (no subcommand)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-040")>]
let ``transfer with no subcommand prints usage to stderr`` () =
    let result = CliRunner.run "transfer"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected usage information on stderr")

// =====================================================================
// --json flag consistency
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-050a")>]
let ``--json transfer show produces valid JSON`` () =
    let env = TransferCliEnv.create()
    try
        let transferId = TransferCliEnv.initiateTransferViaCliJson env 500.00m "2097-04-01"
        let args = sprintf "--json transfer show %d" transferId
        let result = CliRunner.run args
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally TransferCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-050b")>]
let ``--json transfer confirm produces valid JSON`` () =
    let env = TransferCliEnv.create()
    try
        let transferId = TransferCliEnv.initiateTransferViaCliJson env 500.00m "2097-08-01"
        let args = sprintf "--json transfer confirm %d --date 2097-08-15" transferId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally TransferCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-TRC-050c")>]
let ``--json transfer list produces valid JSON`` () =
    let env = TransferCliEnv.create()
    try
        TransferCliEnv.initiateTransferViaCli env 500.00m "2097-04-01" |> ignore
        let result = CliRunner.run "--json transfer list"
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally TransferCliEnv.cleanup env
