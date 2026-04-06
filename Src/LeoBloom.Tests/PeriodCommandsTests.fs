module LeoBloom.Tests.PeriodCommandsTests

open System
open System.Text.Json
open Xunit
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests

// =====================================================================
// Period CLI Commands Tests
// Covers Gherkin scenarios in Specs/CLI/PeriodCommands.feature
//
// Fiscal period year reservation: 2093 (per DSWF/qe.md table)
// Years 2091 and 2092 are taken. This file uses 2093.
// =====================================================================

// =====================================================================
// Helper: create an isolated period for close/reopen tests
// =====================================================================

module PeriodCliEnv =
    type Env =
        { PeriodId: int
          PeriodKey: string
          Tracker: TestCleanup.Tracker }

    /// Create an open period for close tests.
    let createOpen () =
        let conn = DataSource.openConnection()
        let tracker = TestCleanup.create conn
        let prefix = TestData.uniquePrefix()
        let key = TestData.periodKey prefix
        let periodId = InsertHelpers.insertFiscalPeriod conn tracker key (DateOnly(2093, 1, 1)) (DateOnly(2093, 1, 31)) true
        { PeriodId = periodId; PeriodKey = key; Tracker = tracker }

    /// Create a closed period for reopen tests.
    let createClosed () =
        let conn = DataSource.openConnection()
        let tracker = TestCleanup.create conn
        let prefix = TestData.uniquePrefix()
        let key = TestData.periodKey prefix
        let periodId = InsertHelpers.insertFiscalPeriod conn tracker key (DateOnly(2093, 2, 1)) (DateOnly(2093, 2, 28)) false
        { PeriodId = periodId; PeriodKey = key; Tracker = tracker }

    let cleanup (env: Env) =
        TestCleanup.deleteAll env.Tracker
        env.Tracker.Connection.Dispose()

// =====================================================================
// period (no subcommand)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PRD-001")>]
let ``period with no subcommand prints usage to stderr`` () =
    let result = CliRunner.run "period"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected usage information on stderr")

// =====================================================================
// period list — Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PRD-010")>]
let ``list all fiscal periods`` () =
    let result = CliRunner.run "period list"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    // Should contain table data from seed periods
    Assert.False(String.IsNullOrWhiteSpace(stdout), "Expected fiscal period rows in stdout")

[<Fact>]
[<Trait("GherkinId", "FT-PRD-011")>]
let ``list fiscal periods with --json flag outputs valid JSON`` () =
    let result = CliRunner.run "period list --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)

// =====================================================================
// period close — Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PRD-020")>]
let ``close an open period by numeric ID`` () =
    let env = PeriodCliEnv.createOpen()
    try
        let args = sprintf "period close %d" env.PeriodId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Closed", stdout)
    finally PeriodCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PRD-021")>]
let ``close an open period by period key`` () =
    let env = PeriodCliEnv.createOpen()
    try
        let args = sprintf "period close %s" env.PeriodKey
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Closed", stdout)
    finally PeriodCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PRD-022")>]
let ``close with --json flag outputs valid JSON`` () =
    let env = PeriodCliEnv.createOpen()
    try
        let args = sprintf "period close %d --json" env.PeriodId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally PeriodCliEnv.cleanup env

// =====================================================================
// period close — Error Paths
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PRD-023")>]
let ``close a nonexistent period by ID prints error to stderr`` () =
    let result = CliRunner.run "period close 999999"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-PRD-024")>]
let ``close a nonexistent period by key prints error to stderr`` () =
    let result = CliRunner.run "period close nonexistent-key"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-PRD-025")>]
let ``close with no argument prints error to stderr`` () =
    let result = CliRunner.run "period close"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// period reopen — Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PRD-030")>]
let ``reopen a closed period by ID with reason`` () =
    let env = PeriodCliEnv.createClosed()
    try
        let args = sprintf "period reopen %d --reason \"audit adjustment\"" env.PeriodId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Open", stdout)
    finally PeriodCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PRD-031")>]
let ``reopen a closed period by key with reason`` () =
    let env = PeriodCliEnv.createClosed()
    try
        let args = sprintf "period reopen %s --reason \"correction needed\"" env.PeriodKey
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Open", stdout)
    finally PeriodCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-PRD-032")>]
let ``reopen with --json flag outputs valid JSON`` () =
    let env = PeriodCliEnv.createClosed()
    try
        let args = sprintf "period reopen %d --reason \"audit\" --json" env.PeriodId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally PeriodCliEnv.cleanup env

// =====================================================================
// period reopen — Error Paths
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PRD-033")>]
let ``reopen without --reason flag prints error to stderr`` () =
    let result = CliRunner.run "period reopen 1"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-PRD-034")>]
let ``reopen a nonexistent period prints error to stderr`` () =
    let result = CliRunner.run "period reopen 999999 --reason \"test\""
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-PRD-035")>]
let ``reopen with no arguments prints error to stderr`` () =
    let result = CliRunner.run "period reopen"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// period create — Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PRD-040")>]
let ``create a new fiscal period with all required args`` () =
    let prefix = TestData.uniquePrefix()
    let key = sprintf "%sCR" prefix
    let conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let args = sprintf "period create --start 2093-05-01 --end 2093-05-31 --key \"%s\"" key
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(key, stdout)
        // Track the created period for cleanup
        let jsonResult = CliRunner.run (sprintf "period list --json")
        let cleanJson = CliRunner.stripLogLines jsonResult.Stdout
        let doc = JsonDocument.Parse(cleanJson)
        let periods = doc.RootElement.EnumerateArray()
        for p in periods do
            if p.GetProperty("periodKey").GetString() = key then
                TestCleanup.trackFiscalPeriod (p.GetProperty("id").GetInt32()) tracker
    finally
        TestCleanup.deleteAll tracker
        conn.Dispose()

[<Fact>]
[<Trait("GherkinId", "FT-PRD-041")>]
let ``create with --json flag outputs valid JSON`` () =
    let prefix = TestData.uniquePrefix()
    let key = sprintf "%sCJ" prefix
    let conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let args = sprintf "period create --start 2093-06-01 --end 2093-06-30 --key \"%s\" --json" key
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
        // Track created period for cleanup
        let createdId = doc.RootElement.GetProperty("id").GetInt32()
        TestCleanup.trackFiscalPeriod createdId tracker
    finally
        TestCleanup.deleteAll tracker
        conn.Dispose()

// =====================================================================
// period create — Missing Required Args
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PRD-042")>]
let ``create with no arguments prints error to stderr`` () =
    let result = CliRunner.run "period create"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-PRD-043a")>]
let ``create missing --start is rejected`` () =
    let result = CliRunner.run "period create --end 2093-05-31 --key \"2093-05\""
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-PRD-043b")>]
let ``create missing --end is rejected`` () =
    let result = CliRunner.run "period create --start 2093-05-01 --key \"2093-05\""
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-PRD-043c")>]
let ``create missing --key is rejected`` () =
    let result = CliRunner.run "period create --start 2093-05-01 --end 2093-05-31"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// period create — Validation Errors
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PRD-044")>]
let ``create with invalid date format prints error to stderr`` () =
    let result = CliRunner.run "period create --start not-a-date --end 2093-05-31 --key \"2093-05\""
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-PRD-045")>]
let ``create with start after end prints error to stderr`` () =
    let result = CliRunner.run "period create --start 2093-05-31 --end 2093-05-01 --key \"backwards\""
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-PRD-046")>]
let ``create with duplicate period key prints error to stderr`` () =
    let prefix = TestData.uniquePrefix()
    let key = sprintf "%sDK" prefix
    let conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        // Create a period first
        let _periodId = InsertHelpers.insertFiscalPeriod conn tracker key (DateOnly(2093, 7, 1)) (DateOnly(2093, 7, 31)) true
        // Try to create another period with the same key
        let args = sprintf "period create --start 2093-07-01 --end 2093-07-31 --key \"%s\"" key
        let result = CliRunner.run args
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")
    finally
        TestCleanup.deleteAll tracker
        conn.Dispose()

// =====================================================================
// --json flag consistency (Scenario Outline FT-PRD-050)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PRD-050a")>]
let ``--json flag produces valid JSON for period list`` () =
    let result = CliRunner.run "period list --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)

[<Fact>]
[<Trait("GherkinId", "FT-PRD-050b")>]
let ``--json flag produces valid JSON for period create`` () =
    let prefix = TestData.uniquePrefix()
    let key = sprintf "%sJF" prefix
    let conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let args = sprintf "period create --start 2093-07-01 --end 2093-07-31 --key \"%s\" --json" key
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
        // Track for cleanup
        let createdId = doc.RootElement.GetProperty("id").GetInt32()
        TestCleanup.trackFiscalPeriod createdId tracker
    finally
        TestCleanup.deleteAll tracker
        conn.Dispose()

// =====================================================================
// Service-level tests for AC7 and AC8
// (createPeriod validation: blank key, start > end, duplicate key)
// These are easier to test at the service layer directly.
// =====================================================================

[<Fact>]
[<Trait("Category", "Service")>]
let ``createPeriod rejects blank period key`` () =
    let result = FiscalPeriodService.createPeriod "" (DateOnly(2093, 8, 1)) (DateOnly(2093, 8, 31))
    match result with
    | Error errs ->
        Assert.Contains("Period key is required and cannot be blank", errs)
    | Ok _ ->
        Assert.Fail("Expected Error for blank period key, got Ok")

[<Fact>]
[<Trait("Category", "Service")>]
let ``createPeriod rejects whitespace-only period key`` () =
    let result = FiscalPeriodService.createPeriod "   " (DateOnly(2093, 8, 1)) (DateOnly(2093, 8, 31))
    match result with
    | Error errs ->
        Assert.Contains("Period key is required and cannot be blank", errs)
    | Ok _ ->
        Assert.Fail("Expected Error for whitespace period key, got Ok")

[<Fact>]
[<Trait("Category", "Service")>]
let ``createPeriod rejects start date after end date`` () =
    let result = FiscalPeriodService.createPeriod "test-key" (DateOnly(2093, 8, 31)) (DateOnly(2093, 8, 1))
    match result with
    | Error errs ->
        Assert.Contains("Start date must be on or before end date", errs)
    | Ok _ ->
        Assert.Fail("Expected Error for start > end, got Ok")

[<Fact>]
[<Trait("Category", "Service")>]
let ``createPeriod returns friendly error on duplicate key`` () =
    let prefix = TestData.uniquePrefix()
    let key = sprintf "%sSV" prefix
    let conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        // Create a period first via direct insert
        let _periodId = InsertHelpers.insertFiscalPeriod conn tracker key (DateOnly(2093, 9, 1)) (DateOnly(2093, 9, 30)) true
        // Try to create another via the service with the same key
        let result = FiscalPeriodService.createPeriod key (DateOnly(2093, 9, 1)) (DateOnly(2093, 9, 30))
        match result with
        | Error errs ->
            let hasExpectedMsg = errs |> List.exists (fun e -> e.Contains("already exists"))
            Assert.True(hasExpectedMsg, sprintf "Expected 'already exists' error, got: %A" errs)
        | Ok period ->
            // Clean up the accidentally created period
            TestCleanup.trackFiscalPeriod period.id tracker
            Assert.Fail("Expected Error for duplicate key, got Ok")
    finally
        TestCleanup.deleteAll tracker
        conn.Dispose()
