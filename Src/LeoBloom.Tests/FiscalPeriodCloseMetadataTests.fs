module LeoBloom.Tests.FiscalPeriodCloseMetadataTests

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
// Fiscal Period Close Metadata Tests
// Covers Gherkin scenarios in Specs/Behavioral/FiscalPeriodCloseMetadata.feature
//
// Fiscal period year reservation: 2098 (per DSWF/qe.md table — P081)
// Service tests use transactions that roll back on dispose — no explicit cleanup.
// CLI tests commit data via CfmCliEnv and clean up in finally blocks.
// =====================================================================

// =====================================================================
// CLI Env helper for P081 tests
// =====================================================================

module CfmCliEnv =
    type Env =
        { PeriodId: int
          PeriodKey: string
          Connection: NpgsqlConnection }

    /// Create and commit an open period using 2098 year range.
    let createOpen () =
        let conn = DataSource.openConnection()
        let prefix = TestData.uniquePrefix()
        let key = TestData.periodKey prefix
        let periodId =
            use txn = conn.BeginTransaction()
            let id = InsertHelpers.insertFiscalPeriod txn key (DateOnly(2098, 1, 1)) (DateOnly(2098, 1, 31)) true
            txn.Commit()
            id
        { PeriodId = periodId; PeriodKey = key; Connection = conn }

    let cleanup (env: Env) =
        try
            use auditCmd = new NpgsqlCommand(
                "DELETE FROM ledger.fiscal_period_audit WHERE fiscal_period_id = @id",
                env.Connection)
            auditCmd.Parameters.AddWithValue("@id", env.PeriodId) |> ignore
            auditCmd.ExecuteNonQuery() |> ignore
        with ex ->
            eprintfn "Cleanup: failed to delete audit rows for period %d: %s" env.PeriodId ex.Message
        try
            use cmd = new NpgsqlCommand(
                "DELETE FROM ledger.fiscal_period WHERE id = @id",
                env.Connection)
            cmd.Parameters.AddWithValue("@id", env.PeriodId) |> ignore
            cmd.ExecuteNonQuery() |> ignore
        with ex ->
            eprintfn "Cleanup: failed to delete period %d: %s" env.PeriodId ex.Message
        env.Connection.Dispose()

// =====================================================================
// @FT-CFM-001 -- Close sets closed_at and closed_by on the period
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-001")>]
let ``close sets closed_at and closed_by on the period`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2098, 1, 1)) (DateOnly(2098, 1, 31)) true
    let cmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "alice"; note = None }
    let result = FiscalPeriodService.closePeriod txn cmd
    match result with
    | Ok period ->
        Assert.False(period.isOpen, "period.isOpen should be false after close")
        Assert.True(period.closedAt.IsSome, "period.closedAt should be set after close")
        Assert.Equal(Some "alice", period.closedBy)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-CFM-002 -- Close writes an audit row with actor and note
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-002")>]
let ``close writes audit row with actor and note`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2098, 1, 1)) (DateOnly(2098, 1, 31)) true
    let cmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "alice"; note = Some "month end" }
    match FiscalPeriodService.closePeriod txn cmd with
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    | Ok _ ->
        let entries = FiscalPeriodAuditRepository.listByPeriod txn fpId
        Assert.Equal(1, entries.Length)
        let entry = entries.[0]
        Assert.Equal("closed", entry.action)
        Assert.Equal("alice", entry.actor)
        Assert.Equal(Some "month end", entry.note)

// =====================================================================
// @FT-CFM-003 -- Closing an already-closed period is idempotent
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-003")>]
let ``closing already-closed period is idempotent and produces no second audit row`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2098, 1, 1)) (DateOnly(2098, 1, 31)) true
    let aliceCmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "alice"; note = None }
    // First close (transition)
    match FiscalPeriodService.closePeriod txn aliceCmd with
    | Error errs -> Assert.Fail(sprintf "First close failed: %A" errs)
    | Ok _ ->
        // Second close with different actor — must be idempotent (no new audit row)
        let bobCmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "bob"; note = None }
        match FiscalPeriodService.closePeriod txn bobCmd with
        | Error errs -> Assert.Fail(sprintf "Second close (idempotent) failed: %A" errs)
        | Ok period ->
            Assert.False(period.isOpen, "period should still be closed")
            let entries = FiscalPeriodAuditRepository.listByPeriod txn fpId
            Assert.Equal(1, entries.Length)

// =====================================================================
// @FT-CFM-004 -- Reopen clears close metadata and increments reopened_count
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-004")>]
let ``reopen clears close metadata and increments reopened_count`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2098, 1, 1)) (DateOnly(2098, 1, 31)) true
    // Close first
    let closeCmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "alice"; note = None }
    match FiscalPeriodService.closePeriod txn closeCmd with
    | Error errs -> Assert.Fail(sprintf "Setup close failed: %A" errs)
    | Ok _ ->
        // Reopen
        let reopenCmd : ReopenFiscalPeriodCommand = { fiscalPeriodId = fpId; reason = "correction needed"; actor = "bob" }
        match FiscalPeriodService.reopenPeriod txn reopenCmd with
        | Error errs -> Assert.Fail(sprintf "Reopen failed: %A" errs)
        | Ok period ->
            Assert.True(period.isOpen, "period.isOpen should be true after reopen")
            Assert.True(period.closedAt.IsNone, "closedAt should be cleared after reopen")
            Assert.True(period.closedBy.IsNone, "closedBy should be cleared after reopen")
            Assert.Equal(1, period.reopenedCount)

// =====================================================================
// @FT-CFM-005 -- Reopen writes an audit row with actor and reason as note
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-005")>]
let ``reopen writes audit row with actor and reason as note`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2098, 1, 1)) (DateOnly(2098, 1, 31)) true
    // Close
    let closeCmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "alice"; note = None }
    match FiscalPeriodService.closePeriod txn closeCmd with
    | Error errs -> Assert.Fail(sprintf "Setup close failed: %A" errs)
    | Ok _ ->
        // Reopen
        let reopenCmd : ReopenFiscalPeriodCommand = { fiscalPeriodId = fpId; reason = "correction needed"; actor = "bob" }
        match FiscalPeriodService.reopenPeriod txn reopenCmd with
        | Error errs -> Assert.Fail(sprintf "Reopen failed: %A" errs)
        | Ok _ ->
            let entries = FiscalPeriodAuditRepository.listByPeriod txn fpId
            Assert.Equal(2, entries.Length)
            let latest = entries |> List.last
            Assert.Equal("reopened", latest.action)
            Assert.Equal("bob", latest.actor)
            Assert.Equal(Some "correction needed", latest.note)

// =====================================================================
// @FT-CFM-006 -- Full close-reopen-close cycle produces a 3-entry audit trail
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-006")>]
let ``full close-reopen-close cycle produces 3-entry audit trail in chronological order`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2098, 1, 1)) (DateOnly(2098, 1, 31)) true
    let closeCmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "alice"; note = None }
    // Close
    match FiscalPeriodService.closePeriod txn closeCmd with
    | Error errs -> Assert.Fail(sprintf "First close failed: %A" errs)
    | Ok _ ->
        // Reopen
        let reopenCmd : ReopenFiscalPeriodCommand = { fiscalPeriodId = fpId; reason = "missed invoice"; actor = "bob" }
        match FiscalPeriodService.reopenPeriod txn reopenCmd with
        | Error errs -> Assert.Fail(sprintf "Reopen failed: %A" errs)
        | Ok _ ->
            // Close again
            match FiscalPeriodService.closePeriod txn closeCmd with
            | Error errs -> Assert.Fail(sprintf "Second close failed: %A" errs)
            | Ok _ ->
                let entries = FiscalPeriodAuditRepository.listByPeriod txn fpId
                Assert.Equal(3, entries.Length)
                Assert.Equal("closed",   entries.[0].action)
                Assert.Equal("reopened", entries.[1].action)
                Assert.Equal("closed",   entries.[2].action)

// =====================================================================
// @FT-CFM-007 -- fiscal-period audit lists all close/reopen events
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-007")>]
let ``fiscal-period audit lists all close-reopen events for a period`` () =
    let env = CfmCliEnv.createOpen()
    try
        // Close via CLI
        let closeResult = CliRunner.run (sprintf "period close %d --actor alice" env.PeriodId)
        Assert.Equal(0, closeResult.ExitCode)
        // Reopen via CLI
        let reopenResult = CliRunner.run (sprintf "period reopen %d --reason \"missed invoice\" --actor bob" env.PeriodId)
        Assert.Equal(0, reopenResult.ExitCode)
        // Audit
        let auditResult = CliRunner.run (sprintf "period audit %d" env.PeriodId)
        Assert.Equal(0, auditResult.ExitCode)
        let stdout = CliRunner.stripLogLines auditResult.Stdout
        // Verify both entries appear in the output table
        Assert.Contains("closed", stdout, StringComparison.OrdinalIgnoreCase)
        Assert.Contains("alice", stdout)
        Assert.Contains("reopened", stdout, StringComparison.OrdinalIgnoreCase)
        Assert.Contains("bob", stdout)
    finally CfmCliEnv.cleanup env

// =====================================================================
// @FT-CFM-008 -- fiscal-period audit on a nonexistent period exits with code 1
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-008")>]
let ``fiscal-period audit on nonexistent period exits with code 1`` () =
    let result = CliRunner.run "period audit 999999"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// @FT-CFM-009 -- fiscal-period list shows closed_at and reopened_count columns
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-009")>]
let ``fiscal-period list shows Closed At and Reopen# column headers`` () =
    let env = CfmCliEnv.createOpen()
    try
        // Close so the period appears with a closed_at value
        let closeResult = CliRunner.run (sprintf "period close %d --actor alice" env.PeriodId)
        Assert.Equal(0, closeResult.ExitCode)
        // List
        let listResult = CliRunner.run "period list"
        Assert.Equal(0, listResult.ExitCode)
        let stdout = CliRunner.stripLogLines listResult.Stdout
        Assert.Contains("Closed At", stdout, StringComparison.OrdinalIgnoreCase)
        Assert.Contains("Reopen#", stdout, StringComparison.OrdinalIgnoreCase)
    finally CfmCliEnv.cleanup env

// =====================================================================
// @FT-CFM-010 -- --json on period close (idempotent) produces valid JSON
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-010")>]
let ``--json on period close idempotent produces valid JSON`` () =
    let env = CfmCliEnv.createOpen()
    try
        // Close once so the period is already closed
        CliRunner.run (sprintf "period close %d --actor alice" env.PeriodId) |> ignore
        // Idempotent close with --json
        let result = CliRunner.run (sprintf "period close %d --json" env.PeriodId)
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally CfmCliEnv.cleanup env

// =====================================================================
// @FT-CFM-011 -- --json on period reopen produces valid JSON
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-011")>]
let ``--json on period reopen produces valid JSON`` () =
    let env = CfmCliEnv.createOpen()
    try
        // Close first
        CliRunner.run (sprintf "period close %d --actor alice" env.PeriodId) |> ignore
        // Reopen with --json
        let result = CliRunner.run (sprintf "period reopen %d --reason \"correction\" --actor dan --json" env.PeriodId)
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally CfmCliEnv.cleanup env

// =====================================================================
// @FT-CFM-012 -- --json on period audit produces valid JSON
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-012")>]
let ``--json on period audit produces valid JSON`` () =
    let env = CfmCliEnv.createOpen()
    try
        // Close so there is at least one audit entry
        CliRunner.run (sprintf "period close %d --actor alice" env.PeriodId) |> ignore
        // Audit with --json
        let result = CliRunner.run (sprintf "period audit %d --json" env.PeriodId)
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally CfmCliEnv.cleanup env

// =====================================================================
// @FT-CFM-013 -- --json on period list produces valid JSON
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFM-013")>]
let ``--json on period list produces valid JSON`` () =
    let result = CliRunner.run "period list --json"
    Assert.Equal(0, result.ExitCode)
    let cleanStdout = CliRunner.stripLogLines result.Stdout
    let doc = JsonDocument.Parse(cleanStdout)
    Assert.NotNull(doc)
