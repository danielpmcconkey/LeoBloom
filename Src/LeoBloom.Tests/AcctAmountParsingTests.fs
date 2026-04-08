module LeoBloom.Tests.AcctAmountParsingTests

open System
open Npgsql
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

/// Parse a journal entry ID from "Journal Entry #NNN ..." output, if present.
let private parseJeId (stdout: string) : int option =
    if stdout.Contains("Journal Entry #") then
        let line = stdout.Split('\n') |> Array.tryFind (fun l -> l.Contains("Journal Entry #"))
        line |> Option.bind (fun l ->
            let hashIdx = l.IndexOf('#')
            let rest = l.Substring(hashIdx + 1).Trim()
            let parts = rest.Split([| ' '; '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
            if parts.Length > 0 then
                match Int32.TryParse(parts.[0]) with
                | true, id -> Some id
                | _ -> None
            else None)
    else None

/// Clean up a test's ledger data: optionally delete a JE, then delete accounts, fp, account type.
let private cleanup (conn: NpgsqlConnection) (jeId: int option) (acct1: int) (acct2: int) (fpId: int) (atId: int) =
    // Delete JE lines + JE if created by CLI
    match jeId with
    | Some id ->
        use c = new NpgsqlCommand("DELETE FROM ledger.journal_entry_line WHERE journal_entry_id = @id", conn)
        c.Parameters.AddWithValue("@id", id) |> ignore
        c.ExecuteNonQuery() |> ignore
        use c2 = new NpgsqlCommand("DELETE FROM ledger.journal_entry WHERE id = @id", conn)
        c2.Parameters.AddWithValue("@id", id) |> ignore
        c2.ExecuteNonQuery() |> ignore
    | None -> ()
    // Delete fiscal period
    use c3 = new NpgsqlCommand("DELETE FROM ledger.fiscal_period WHERE id = @id", conn)
    c3.Parameters.AddWithValue("@id", fpId) |> ignore
    c3.ExecuteNonQuery() |> ignore
    // Delete accounts
    use c4 = new NpgsqlCommand("DELETE FROM ledger.account WHERE id IN (@a1, @a2)", conn)
    c4.Parameters.AddWithValue("@a1", acct1) |> ignore
    c4.Parameters.AddWithValue("@a2", acct2) |> ignore
    c4.ExecuteNonQuery() |> ignore
    // Delete account type
    use c5 = new NpgsqlCommand("DELETE FROM ledger.account_type WHERE id = @id", conn)
    c5.Parameters.AddWithValue("@id", atId) |> ignore
    c5.ExecuteNonQuery() |> ignore
    conn.Dispose()

// --- Valid Formats ---

[<Fact>]
[<Trait("GherkinId", "FT-AAP-001")>]
let ``integer account ID with decimal amount parses correctly`` () =
    let conn = DataSource.openConnection()
    let atId, acct1, acct2, fpId =
        use txn = conn.BeginTransaction()
        let prefix = TestData.uniquePrefix()
        let at   = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
        let a1   = InsertHelpers.insertAccount txn (prefix + "A1") "AcctA" at true
        let a2   = InsertHelpers.insertAccount txn (prefix + "A2") "AcctB" at true
        let fp   = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        txn.Commit()
        at, a1, a2, fp
    let mutable jeId = None
    try
        let args =
            sprintf "ledger post --debit %d:1000.00 --credit %d:1000.00 --date 2026-03-15 --description \"Parse test\" --fiscal-period-id %d"
                acct1 acct2 fpId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        jeId <- parseJeId (CliRunner.stripLogLines result.Stdout)
    finally
        cleanup conn jeId acct1 acct2 fpId atId

[<Fact>]
[<Trait("GherkinId", "FT-AAP-002")>]
let ``whole number amount without decimal parses correctly`` () =
    let conn = DataSource.openConnection()
    let atId, acct1, acct2, fpId =
        use txn = conn.BeginTransaction()
        let prefix = TestData.uniquePrefix()
        let at   = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
        let a1   = InsertHelpers.insertAccount txn (prefix + "A1") "AcctA" at true
        let a2   = InsertHelpers.insertAccount txn (prefix + "A2") "AcctB" at true
        let fp   = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        txn.Commit()
        at, a1, a2, fp
    let mutable jeId = None
    try
        let args =
            sprintf "ledger post --debit %d:1000 --credit %d:1000 --date 2026-03-15 --description \"No decimal\" --fiscal-period-id %d"
                acct1 acct2 fpId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        jeId <- parseJeId (CliRunner.stripLogLines result.Stdout)
    finally
        cleanup conn jeId acct1 acct2 fpId atId

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
