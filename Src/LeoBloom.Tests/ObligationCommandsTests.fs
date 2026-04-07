module LeoBloom.Tests.ObligationCommandsTests

open System
open System.Text.Json
open Npgsql
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests

// IMPORTANT: Post tests create fiscal periods in year 2094 to avoid colliding with
// PostObligationToLedgerTests (2091), TransferTests (2092), and TransferCommandsTests (2093).
// Each test file that exercises a findByDate code path must use a year no other file uses.

// =====================================================================
// Shared setup: "CLI-testable obligation environment"
// =====================================================================

module OblCliEnv =
    type Env =
        { AgreementId: int          // Basic receivable/monthly agreement (no accounts)
          PostableAgreementId: int  // Agreement with source/dest accounts for post tests
          FiscalPeriodId: int       // Year 2094, for post tests
          SourceAccountId: int
          DestAccountId: int
          Prefix: string
          Tracker: TestCleanup.Tracker }

    let create () =
        let conn = DataSource.openConnection()
        let tracker = TestCleanup.create conn
        let prefix = TestData.uniquePrefix()

        // Basic agreement — receivable, monthly, active
        let basicAgrId =
            InsertHelpers.insertObligationAgreementFull conn tracker $"{prefix}_agr" "receivable" "monthly" true

        // Accounts for postable agreement (post requires source + dest on agreement)
        let revTypeId = InsertHelpers.insertAccountType conn tracker $"{prefix}_rv" "credit"
        let astTypeId = InsertHelpers.insertAccountType conn tracker $"{prefix}_as" "debit"
        let srcId = InsertHelpers.insertAccount conn tracker $"{prefix}SR" $"{prefix}_src" revTypeId true
        let dstId = InsertHelpers.insertAccount conn tracker $"{prefix}DS" $"{prefix}_dst" astTypeId true

        // Postable agreement — receivable, monthly, with accounts + amount
        let postAgrId =
            use cmd = new NpgsqlCommand(
                "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, source_account_id, dest_account_id, amount) \
                 VALUES (@n, 'receivable', 'monthly', @sa, @da, 1200) RETURNING id",
                conn)
            cmd.Parameters.AddWithValue("@n", $"{prefix}_post") |> ignore
            cmd.Parameters.AddWithValue("@sa", srcId) |> ignore
            cmd.Parameters.AddWithValue("@da", dstId) |> ignore
            let id = cmd.ExecuteScalar() :?> int
            TestCleanup.trackObligationAgreement id tracker
            id

        // Fiscal period year 2094 for post tests
        let fpId =
            InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}94" (DateOnly(2094, 1, 1)) (DateOnly(2094, 12, 31)) true

        { AgreementId = basicAgrId
          PostableAgreementId = postAgrId
          FiscalPeriodId = fpId
          SourceAccountId = srcId
          DestAccountId = dstId
          Prefix = prefix
          Tracker = tracker }

    let cleanup (env: Env) =
        // Find JEs created by CLI post operations so they are deleted in FK-safe order
        try
            use cmd = new NpgsqlCommand(
                "SELECT journal_entry_id FROM ops.obligation_instance \
                 WHERE obligation_agreement_id = ANY(@aids) AND journal_entry_id IS NOT NULL",
                env.Tracker.Connection)
            cmd.Parameters.AddWithValue("@aids", [| env.AgreementId; env.PostableAgreementId |]) |> ignore
            use reader = cmd.ExecuteReader()
            while reader.Read() do
                TestCleanup.trackJournalEntry (reader.GetInt32(0)) env.Tracker
            reader.Close()
        with ex ->
            eprintfn "OblCliEnv.cleanup: failed to find JE IDs: %s" ex.Message
        TestCleanup.deleteAll env.Tracker
        env.Tracker.Connection.Dispose()

    /// Parse an obligation agreement ID from human-readable CLI output.
    /// Format: "Obligation Agreement #NNN — name"
    let parseAgreementId (stdout: string) : int =
        let line = stdout.Split('\n') |> Array.find (fun l -> l.Contains("Obligation Agreement #"))
        let hashIdx = line.IndexOf('#')
        let afterHash = line.Substring(hashIdx + 1).Trim()
        Int32.Parse(afterHash.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries).[0])

    /// Parse an obligation instance ID from human-readable CLI output.
    /// Format: "Obligation Instance #NNN — name"
    let parseInstanceId (stdout: string) : int =
        let line = stdout.Split('\n') |> Array.find (fun l -> l.Contains("Obligation Instance #"))
        let hashIdx = line.IndexOf('#')
        let afterHash = line.Substring(hashIdx + 1).Trim()
        Int32.Parse(afterHash.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries).[0])

    /// Spawn instances for the basic agreement via CLI and return the first spawned instance ID.
    let spawnInstanceViaCli (env: Env) (fromDate: string) (toDate: string) : int =
        let result = CliRunner.run (sprintf "obligation instance spawn %d --from %s --to %s" env.AgreementId fromDate toDate)
        if result.ExitCode <> 0 then
            failwithf "Spawn failed. stderr: %s" result.Stderr
        use cmd = new NpgsqlCommand(
            "SELECT id FROM ops.obligation_instance WHERE obligation_agreement_id = @aid ORDER BY id DESC LIMIT 1",
            env.Tracker.Connection)
        cmd.Parameters.AddWithValue("@aid", env.AgreementId) |> ignore
        cmd.ExecuteScalar() :?> int

    /// Create a confirmed instance linked to the postable agreement for post testing.
    /// Spawn → in_flight → confirmed (with amount=1200.00, confirmedDate=2094-01-15).
    let createConfirmedInstanceForPost (env: Env) : int =
        let spawnResult =
            CliRunner.run (sprintf "obligation instance spawn %d --from 2094-01-01 --to 2094-01-31" env.PostableAgreementId)
        if spawnResult.ExitCode <> 0 then
            failwithf "Spawn for post failed. stderr: %s" spawnResult.Stderr
        let instanceId =
            use cmd = new NpgsqlCommand(
                "SELECT id FROM ops.obligation_instance WHERE obligation_agreement_id = @aid ORDER BY id DESC LIMIT 1",
                env.Tracker.Connection)
            cmd.Parameters.AddWithValue("@aid", env.PostableAgreementId) |> ignore
            cmd.ExecuteScalar() :?> int
        let r1 = CliRunner.run (sprintf "obligation instance transition %d --to in_flight" instanceId)
        if r1.ExitCode <> 0 then
            failwithf "Transition to in_flight failed. stderr: %s" r1.Stderr
        let r2 = CliRunner.run (sprintf "obligation instance transition %d --to confirmed --amount 1200.00 --date 2094-01-15" instanceId)
        if r2.ExitCode <> 0 then
            failwithf "Transition to confirmed failed. stderr: %s" r2.Stderr
        instanceId

// =====================================================================
// obligation (no subcommand) -- FT-OBL-001 through 003
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-001")>]
let ``obligation with no subcommand prints usage to stderr`` () =
    let result = CliRunner.run "obligation"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected usage on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-002")>]
let ``obligation agreement with no subcommand prints usage to stderr`` () =
    let result = CliRunner.run "obligation agreement"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected usage on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-003")>]
let ``obligation instance with no subcommand prints usage to stderr`` () =
    let result = CliRunner.run "obligation instance"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected usage on stderr")

// =====================================================================
// obligation agreement list -- FT-OBL-010 through 015
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-010")>]
let ``List all active agreements with no filters`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation agreement list"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Should contain table headers or agreement rows
        Assert.Contains("ID", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-011")>]
let ``List agreements filtered by type receivable`` () =
    let env = OblCliEnv.create()
    try
        // Create a payable agreement alongside the env's receivable one
        InsertHelpers.insertObligationAgreementFull
            env.Tracker.Connection env.Tracker $"{env.Prefix}_pay" "payable" "monthly" true
        |> ignore
        let result = CliRunner.run "obligation agreement list --type receivable"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("receivable", stdout)
        Assert.DoesNotContain("payable", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-012")>]
let ``List agreements filtered by cadence monthly`` () =
    let env = OblCliEnv.create()
    try
        // Create a quarterly agreement alongside the env's monthly one
        InsertHelpers.insertObligationAgreementFull
            env.Tracker.Connection env.Tracker $"{env.Prefix}_qrt" "receivable" "quarterly" true
        |> ignore
        let result = CliRunner.run "obligation agreement list --cadence monthly"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("monthly", stdout)
        Assert.DoesNotContain("quarterly", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-013")>]
let ``List agreements with --inactive includes inactive agreements`` () =
    let env = OblCliEnv.create()
    try
        let inactiveId =
            InsertHelpers.insertObligationAgreementFull
                env.Tracker.Connection env.Tracker $"{env.Prefix}_ina" "receivable" "monthly" false
        let result = CliRunner.run "obligation agreement list --inactive"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(sprintf "%d" inactiveId, stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-014")>]
let ``List agreements with --json flag outputs valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation agreement list --json"
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-015")>]
let ``List agreements with no matching results prints empty output`` () =
    let env = OblCliEnv.create()
    try
        // Filter payable + annual: our env only has receivable/monthly, so no match for our prefix
        let result = CliRunner.run "obligation agreement list --type payable --cadence annual"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Our test data prefix should not be in the output
        Assert.DoesNotContain(env.Prefix, stdout)
    finally OblCliEnv.cleanup env

// =====================================================================
// obligation agreement show -- FT-OBL-020 through 023
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-020")>]
let ``Show an existing agreement by ID`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run (sprintf "obligation agreement show %d" env.AgreementId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(sprintf "Obligation Agreement #%d" env.AgreementId, stdout)
        Assert.Contains(env.Prefix, stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-021")>]
let ``Show agreement with --json flag outputs valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run (sprintf "obligation agreement show %d --json" env.AgreementId)
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
        Assert.Equal(env.AgreementId, doc.RootElement.GetProperty("id").GetInt32())
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-022")>]
let ``Show a nonexistent agreement prints error to stderr`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation agreement show 999999"
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-023")>]
let ``Show with no ID argument prints error to stderr`` () =
    let result = CliRunner.run "obligation agreement show"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

// =====================================================================
// obligation agreement create -- FT-OBL-030 through 034
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-030")>]
let ``Create an agreement with all required args`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation agreement create --name \"Rent Receivable\" --type receivable --cadence monthly"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Obligation Agreement #", stdout)
        Assert.Contains("receivable", stdout)
        Assert.Contains("monthly", stdout)
        // Track created agreement for cleanup
        let agrId = OblCliEnv.parseAgreementId stdout
        TestCleanup.trackObligationAgreement agrId env.Tracker
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-031")>]
let ``Create an agreement with optional args included in output`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation agreement create --name \"Rent Receivable\" --type receivable --cadence monthly --counterparty \"Jeffrey\" --amount 1200.00 --notes \"Monthly rent\""
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Jeffrey", stdout)
        Assert.Contains("1200", stdout)
        let agrId = OblCliEnv.parseAgreementId stdout
        TestCleanup.trackObligationAgreement agrId env.Tracker
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-032")>]
let ``Create agreement with --json flag outputs valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation agreement create --name \"Test Agreement\" --type payable --cadence quarterly --json"
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
        let agrId = doc.RootElement.GetProperty("id").GetInt32()
        TestCleanup.trackObligationAgreement agrId env.Tracker
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-033")>]
let ``Create with no arguments prints error to stderr`` () =
    let result = CliRunner.run "obligation agreement create"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-034a")>]
let ``Create missing --name is rejected`` () =
    let result = CliRunner.run "obligation agreement create --type receivable --cadence monthly"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-034b")>]
let ``Create missing --type is rejected`` () =
    let result = CliRunner.run "obligation agreement create --name \"Test\" --cadence monthly"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-034c")>]
let ``Create missing --cadence is rejected`` () =
    let result = CliRunner.run "obligation agreement create --name \"Test\" --type receivable"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

// =====================================================================
// obligation agreement update -- FT-OBL-040 through 043
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-040")>]
let ``Update an agreement name`` () =
    let env = OblCliEnv.create()
    try
        // update requires --name, --type, --cadence all as mandatory
        let result = CliRunner.run (sprintf "obligation agreement update %d --name \"Updated Name\" --type receivable --cadence monthly" env.AgreementId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Updated Name", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-041")>]
let ``Update agreement with --json flag outputs valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run (sprintf "obligation agreement update %d --name \"Updated\" --type receivable --cadence monthly --json" env.AgreementId)
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-042")>]
let ``Update a nonexistent agreement prints error to stderr`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation agreement update 999999 --name \"Test\" --type receivable --cadence monthly"
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-043")>]
let ``Update with no ID argument prints error to stderr`` () =
    let result = CliRunner.run "obligation agreement update"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

// =====================================================================
// obligation agreement deactivate -- FT-OBL-050 through 053
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-050")>]
let ``Deactivate an active agreement`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run (sprintf "obligation agreement deactivate %d" env.AgreementId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(sprintf "Obligation Agreement #%d" env.AgreementId, stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-051")>]
let ``Deactivate with --json flag outputs valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run (sprintf "obligation agreement deactivate %d --json" env.AgreementId)
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-052")>]
let ``Deactivate a nonexistent agreement prints error to stderr`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation agreement deactivate 999999"
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-053")>]
let ``Deactivate with no ID argument prints error to stderr`` () =
    let result = CliRunner.run "obligation agreement deactivate"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

// =====================================================================
// obligation instance list -- FT-OBL-060 through 065
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-060")>]
let ``List all instances with no filters`` () =
    let env = OblCliEnv.create()
    try
        InsertHelpers.insertObligationInstance env.Tracker.Connection env.Tracker env.AgreementId $"{env.Prefix}_i1" true |> ignore
        let result = CliRunner.run "obligation instance list"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("ID", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-061")>]
let ``List instances filtered by status expected`` () =
    let env = OblCliEnv.create()
    try
        // Insert one expected and one with in_flight status
        InsertHelpers.insertObligationInstanceFull
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_exp" "expected"
            (DateOnly(2026, 6, 1)) None None true |> ignore
        InsertHelpers.insertObligationInstanceFull
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_fly" "in_flight"
            (DateOnly(2026, 6, 2)) None None true |> ignore

        let result = CliRunner.run "obligation instance list --status expected"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains($"{env.Prefix}_exp", stdout)
        Assert.DoesNotContain($"{env.Prefix}_fly", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-062")>]
let ``List instances filtered by due-before date`` () =
    let env = OblCliEnv.create()
    try
        InsertHelpers.insertObligationInstanceWithDate
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_b4" (DateOnly(2026, 4, 15)) true |> ignore
        InsertHelpers.insertObligationInstanceWithDate
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_aft" (DateOnly(2026, 5, 15)) true |> ignore

        let result = CliRunner.run "obligation instance list --due-before 2026-04-30"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains($"{env.Prefix}_b4", stdout)
        Assert.DoesNotContain($"{env.Prefix}_aft", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-063")>]
let ``List instances filtered by due-after date`` () =
    let env = OblCliEnv.create()
    try
        InsertHelpers.insertObligationInstanceWithDate
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_bef" (DateOnly(2026, 3, 15)) true |> ignore
        InsertHelpers.insertObligationInstanceWithDate
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_aft" (DateOnly(2026, 4, 15)) true |> ignore

        let result = CliRunner.run "obligation instance list --due-after 2026-04-01"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains($"{env.Prefix}_aft", stdout)
        Assert.DoesNotContain($"{env.Prefix}_bef", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-064")>]
let ``List instances with --json flag outputs valid JSON`` () =
    let env = OblCliEnv.create()
    try
        InsertHelpers.insertObligationInstance env.Tracker.Connection env.Tracker env.AgreementId $"{env.Prefix}_i1" true |> ignore
        let result = CliRunner.run "obligation instance list --json"
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-065")>]
let ``List instances with no matching results prints empty output`` () =
    let env = OblCliEnv.create()
    try
        // Filter to posted status before 2020 — genuinely no data there
        let result = CliRunner.run "obligation instance list --status posted --due-before 2020-01-01"
        Assert.Equal(0, result.ExitCode)
        // Either empty or "(no instances found)" or whitespace only — no rows for our prefix
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.DoesNotContain(env.Prefix, stdout)
    finally OblCliEnv.cleanup env

// =====================================================================
// obligation instance spawn -- FT-OBL-070 through 073
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-070")>]
let ``Spawn instances for an agreement over a date range`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run (sprintf "obligation instance spawn %d --from 2026-05-01 --to 2026-07-31" env.AgreementId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Should show "Spawned N instance(s)"
        Assert.Contains("Spawned", stdout)
        Assert.Contains("instance", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-071")>]
let ``Spawn with --json flag outputs valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run (sprintf "obligation instance spawn %d --from 2026-05-01 --to 2026-05-31 --json" env.AgreementId)
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-072")>]
let ``Spawn for a nonexistent agreement prints error to stderr`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation instance spawn 999999 --from 2026-05-01 --to 2026-05-31"
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-073a")>]
let ``Spawn missing --from is rejected`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run (sprintf "obligation instance spawn %d --to 2026-05-31" env.AgreementId)
        Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                    sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-073b")>]
let ``Spawn missing --to is rejected`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run (sprintf "obligation instance spawn %d --from 2026-05-01" env.AgreementId)
        Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                    sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-073c")>]
let ``Spawn missing agreement ID is rejected`` () =
    let result = CliRunner.run "obligation instance spawn --from 2026-05-01 --to 2026-05-31"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

// =====================================================================
// obligation instance transition -- FT-OBL-080 through 085
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-080")>]
let ``Transition an instance to a new status`` () =
    let env = OblCliEnv.create()
    try
        // Insert expected instance, transition to in_flight
        let instId =
            InsertHelpers.insertObligationInstanceFull
                env.Tracker.Connection env.Tracker
                env.AgreementId $"{env.Prefix}_t1" "expected"
                (DateOnly(2026, 6, 1)) None None true
        let result = CliRunner.run (sprintf "obligation instance transition %d --to in_flight" instId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Obligation Instance #", stdout)
        Assert.Contains("in_flight", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-081")>]
let ``Transition with optional args included in result`` () =
    let env = OblCliEnv.create()
    try
        // Insert in_flight instance; transition to confirmed with amount + date + notes
        let instId =
            InsertHelpers.insertObligationInstanceFull
                env.Tracker.Connection env.Tracker
                env.AgreementId $"{env.Prefix}_t2" "in_flight"
                (DateOnly(2026, 6, 1)) None None true
        let result = CliRunner.run (sprintf "obligation instance transition %d --to confirmed --amount 1200.00 --date 2026-04-15 --notes \"Payment received\"" instId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("1200", stdout)
        Assert.Contains("2026-04-15", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-082")>]
let ``Transition with --json flag outputs valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let instId =
            InsertHelpers.insertObligationInstanceFull
                env.Tracker.Connection env.Tracker
                env.AgreementId $"{env.Prefix}_t3" "expected"
                (DateOnly(2026, 6, 1)) None None true
        let result = CliRunner.run (sprintf "obligation instance transition %d --to in_flight --json" instId)
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-083")>]
let ``Transition a nonexistent instance prints error to stderr`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation instance transition 999999 --to in_flight"
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-084")>]
let ``Transition without --to flag prints error to stderr`` () =
    let result = CliRunner.run "obligation instance transition 1"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-085")>]
let ``Transition with no arguments prints error to stderr`` () =
    let result = CliRunner.run "obligation instance transition"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

// =====================================================================
// obligation instance post -- FT-OBL-090 through 093
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-090")>]
let ``Post a confirmed instance to ledger`` () =
    let env = OblCliEnv.create()
    try
        let instId = OblCliEnv.createConfirmedInstanceForPost env
        let result = CliRunner.run (sprintf "obligation instance post %d" instId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Output: "Posted instance NNN to journal entry NNN."
        Assert.Contains("Posted instance", stdout)
        Assert.Contains("journal entry", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-091")>]
let ``Post with --json flag outputs valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let instId = OblCliEnv.createConfirmedInstanceForPost env
        let result = CliRunner.run (sprintf "obligation instance post %d --json" instId)
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-092")>]
let ``Post a nonexistent instance prints error to stderr`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation instance post 999999"
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-093")>]
let ``Post with no ID argument prints error to stderr`` () =
    let result = CliRunner.run "obligation instance post"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

// =====================================================================
// obligation overdue -- FT-OBL-100 through 104
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-100")>]
let ``Overdue detection with no flags defaults to today`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation overdue"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Either "No overdue candidates" path or "Overdue detection complete"
        Assert.True(stdout.Contains("Overdue detection") || stdout.Contains("transitioned"),
                    sprintf "Expected overdue detection output, got: %s" stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-101")>]
let ``Overdue detection with --as-of date uses that date as reference`` () =
    let env = OblCliEnv.create()
    try
        // Create an instance expected before the as-of date
        InsertHelpers.insertObligationInstanceFull
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_od" "expected"
            (DateOnly(2026, 3, 1)) None None true |> ignore
        let result = CliRunner.run "obligation overdue --as-of 2026-04-01"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.True(stdout.Contains("transitioned") || stdout.Contains("Overdue detection"),
                    sprintf "Expected overdue detection output, got: %s" stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-102")>]
let ``Overdue detection with no eligible instances reports zero transitioned`` () =
    let env = OblCliEnv.create()
    try
        // Use a reference date far in the past so no instances are overdue
        let result = CliRunner.run "obligation overdue --as-of 2020-01-01"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("0", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-103")>]
let ``Overdue detection with --json flag outputs valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation overdue --as-of 2026-04-01 --json"
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-104")>]
let ``Overdue with invalid date format prints error to stderr`` () =
    let result = CliRunner.run "obligation overdue --as-of not-a-date"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

// =====================================================================
// obligation upcoming -- FT-OBL-110 through 118
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-110")>]
let ``Upcoming with no flags defaults to 30-day horizon`` () =
    let env = OblCliEnv.create()
    try
        let today = DateOnly.FromDateTime(DateTime.Today)
        InsertHelpers.insertObligationInstanceWithDate
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_up" (today.AddDays(15)) true |> ignore
        let result = CliRunner.run "obligation upcoming"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains($"{env.Prefix}_up", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-111")>]
let ``Upcoming with --days 7 returns only instances within 7 days`` () =
    let env = OblCliEnv.create()
    try
        let today = DateOnly.FromDateTime(DateTime.Today)
        InsertHelpers.insertObligationInstanceWithDate
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_in7" (today.AddDays(5)) true |> ignore
        InsertHelpers.insertObligationInstanceWithDate
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_out7" (today.AddDays(15)) true |> ignore
        let result = CliRunner.run "obligation upcoming --days 7"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains($"{env.Prefix}_in7", stdout)
        Assert.DoesNotContain($"{env.Prefix}_out7", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-112")>]
let ``Upcoming includes instances in expected status`` () =
    let env = OblCliEnv.create()
    try
        let today = DateOnly.FromDateTime(DateTime.Today)
        InsertHelpers.insertObligationInstanceFull
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_exp7" "expected"
            (today.AddDays(5)) None None true |> ignore
        let result = CliRunner.run "obligation upcoming --days 7"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains($"{env.Prefix}_exp7", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-113")>]
let ``Upcoming includes instances in in_flight status`` () =
    let env = OblCliEnv.create()
    try
        let today = DateOnly.FromDateTime(DateTime.Today)
        InsertHelpers.insertObligationInstanceFull
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_fly7" "in_flight"
            (today.AddDays(5)) None None true |> ignore
        let result = CliRunner.run "obligation upcoming --days 7"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains($"{env.Prefix}_fly7", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-114")>]
let ``Upcoming excludes instances beyond the horizon`` () =
    let env = OblCliEnv.create()
    try
        let today = DateOnly.FromDateTime(DateTime.Today)
        InsertHelpers.insertObligationInstanceWithDate
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_far" (today.AddDays(31)) true |> ignore
        let result = CliRunner.run "obligation upcoming --days 30"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.DoesNotContain($"{env.Prefix}_far", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-115")>]
let ``Upcoming excludes confirmed instances`` () =
    let env = OblCliEnv.create()
    try
        let today = DateOnly.FromDateTime(DateTime.Today)
        InsertHelpers.insertObligationInstanceFull
            env.Tracker.Connection env.Tracker
            env.AgreementId $"{env.Prefix}_cfm" "confirmed"
            (today.AddDays(5)) None None true |> ignore
        let result = CliRunner.run "obligation upcoming --days 7"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.DoesNotContain($"{env.Prefix}_cfm", stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-116")>]
let ``Upcoming with no matching instances returns exit code 0`` () =
    let env = OblCliEnv.create()
    try
        // Don't create any upcoming instances for our agreement
        let result = CliRunner.run "obligation upcoming --days 7"
        Assert.Equal(0, result.ExitCode)
        // Our prefix should not appear in the output
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.DoesNotContain(env.Prefix, stdout)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-117")>]
let ``Upcoming with --json flag outputs valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation upcoming --json"
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-118")>]
let ``Upcoming with invalid --days value prints error to stderr`` () =
    let result = CliRunner.run "obligation upcoming --days notanumber"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

// =====================================================================
// --json flag consistency -- FT-OBL-150
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-150a")>]
let ``--json agreement list produces valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation agreement list --json"
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-150b")>]
let ``--json agreement show produces valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run (sprintf "obligation agreement show %d --json" env.AgreementId)
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-150c")>]
let ``--json instance list produces valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation instance list --json"
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "@FT-OBL-150d")>]
let ``--json upcoming produces valid JSON`` () =
    let env = OblCliEnv.create()
    try
        let result = CliRunner.run "obligation upcoming --days 30 --json"
        Assert.Equal(0, result.ExitCode)
        let clean = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(clean)
        Assert.NotNull(doc)
    finally OblCliEnv.cleanup env

// =====================================================================
// Structural tests (no Gherkin mapping)
// =====================================================================

[<Fact>]
let ``ObligationCommands.fs appears after PeriodCommands.fs and before Program.fs in fsproj`` () =
    let fsprojPath = System.IO.Path.Combine(RepoPath.repoRoot, "Src", "LeoBloom.CLI", "LeoBloom.CLI.fsproj")
    let content = System.IO.File.ReadAllText(fsprojPath)
    let idxObl = content.IndexOf("ObligationCommands.fs")
    let idxPeriod = content.IndexOf("PeriodCommands.fs")
    let idxProgram = content.IndexOf("Program.fs")
    Assert.True(idxPeriod > 0, "PeriodCommands.fs not found in fsproj")
    Assert.True(idxObl > 0, "ObligationCommands.fs not found in fsproj")
    Assert.True(idxProgram > 0, "Program.fs not found in fsproj")
    Assert.True(idxPeriod < idxObl, "ObligationCommands.fs should come after PeriodCommands.fs")
    Assert.True(idxObl < idxProgram, "ObligationCommands.fs should come before Program.fs")

[<Fact>]
let ``OutputFormatter.fs has dedicated obligation list and result write functions`` () =
    let formatterPath = System.IO.Path.Combine(RepoPath.repoRoot, "Src", "LeoBloom.CLI", "OutputFormatter.fs")
    let content = System.IO.File.ReadAllText(formatterPath)
    Assert.Contains("writeAgreementList", content)
    Assert.Contains("writeInstanceList", content)
    Assert.Contains("writeOverdueResult", content)
    Assert.Contains("writeSpawnResult", content)
    Assert.Contains("writePostResult", content)

[<Fact>]
let ``Program.fs has Obligation case in LeoBloomArgs DU`` () =
    let programPath = System.IO.Path.Combine(RepoPath.repoRoot, "Src", "LeoBloom.CLI", "Program.fs")
    let content = System.IO.File.ReadAllText(programPath)
    Assert.Contains("Obligation of ParseResults<ObligationArgs>", content)
    Assert.Contains("ObligationCommands.dispatch", content)

[<Fact>]
let ``ObligationInstanceRepository.fs has ListInstancesFilter type`` () =
    let repoPath = System.IO.Path.Combine(RepoPath.repoRoot, "Src", "LeoBloom.Ops", "ObligationInstanceRepository.fs")
    let content = System.IO.File.ReadAllText(repoPath)
    Assert.Contains("ListInstancesFilter", content)
