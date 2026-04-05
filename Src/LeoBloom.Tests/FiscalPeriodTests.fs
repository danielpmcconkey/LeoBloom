module LeoBloom.Tests.FiscalPeriodTests

open System
open System.IO
open Xunit
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Helpers
// =====================================================================

let private logDir = "/workspace/application_logs/leobloom"

/// Read all log file content from the log directory.
let private readLogContent () =
    if not (Directory.Exists logDir) then ""
    else
        Directory.GetFiles(logDir, "leobloom-*.log")
        |> Array.map File.ReadAllText
        |> String.concat "\n"

// =====================================================================
// @FT-CFP-001 -- Close an open fiscal period
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-001")>]
let ``close an open period`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let cmd = { fiscalPeriodId = fpId }
        let result = FiscalPeriodService.closePeriod cmd
        match result with
        | Ok period ->
            Assert.Equal(fpId, period.id)
            Assert.False(period.isOpen, "period.isOpen should be false after close")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// @FT-CFP-002 -- Reopen a closed fiscal period with a reason
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-002")>]
let ``reopen a closed period with reason`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        // Close first
        let closeCmd = { fiscalPeriodId = fpId }
        match FiscalPeriodService.closePeriod closeCmd with
        | Ok _ -> ()
        | Error errs -> Assert.Fail(sprintf "Setup close failed: %A" errs)
        // Reopen
        let cmd = { fiscalPeriodId = fpId; reason = "Quarter-end adjustment needed" }
        let result = FiscalPeriodService.reopenPeriod cmd
        match result with
        | Ok period ->
            Assert.Equal(fpId, period.id)
            Assert.True(period.isOpen, "period.isOpen should be true after reopen")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// @FT-CFP-003 -- Closing an already-closed period is idempotent
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-003")>]
let ``close an already-closed period is idempotent`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        // Close first
        let closeCmd = { fiscalPeriodId = fpId }
        match FiscalPeriodService.closePeriod closeCmd with
        | Ok _ -> ()
        | Error errs -> Assert.Fail(sprintf "Setup close failed: %A" errs)
        // Close again (idempotent)
        let result = FiscalPeriodService.closePeriod closeCmd
        match result with
        | Ok period ->
            Assert.Equal(fpId, period.id)
            Assert.False(period.isOpen, "period.isOpen should be false")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// @FT-CFP-004 -- Reopening an already-open period is idempotent
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-004")>]
let ``reopen an already-open period is idempotent`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let cmd = { fiscalPeriodId = fpId; reason = "Already open, should still succeed" }
        let result = FiscalPeriodService.reopenPeriod cmd
        match result with
        | Ok period ->
            Assert.Equal(fpId, period.id)
            Assert.True(period.isOpen, "period.isOpen should be true")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// @FT-CFP-005 -- Reopen with invalid reason is rejected (empty string)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-005")>]
let ``reopen with empty reason rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        // Close first
        let closeCmd = { fiscalPeriodId = fpId }
        match FiscalPeriodService.closePeriod closeCmd with
        | Ok _ -> ()
        | Error errs -> Assert.Fail(sprintf "Setup close failed: %A" errs)
        // Reopen with empty reason
        let cmd = { fiscalPeriodId = fpId; reason = "" }
        let result = FiscalPeriodService.reopenPeriod cmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for empty reason")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("reason")),
                        sprintf "Expected error containing 'reason': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// @FT-CFP-005 -- Reopen with invalid reason is rejected (whitespace-only)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-005")>]
let ``reopen with whitespace-only reason rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        // Close first
        let closeCmd = { fiscalPeriodId = fpId }
        match FiscalPeriodService.closePeriod closeCmd with
        | Ok _ -> ()
        | Error errs -> Assert.Fail(sprintf "Setup close failed: %A" errs)
        // Reopen with whitespace reason
        let cmd = { fiscalPeriodId = fpId; reason = "   " }
        let result = FiscalPeriodService.reopenPeriod cmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for whitespace-only reason")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("reason")),
                        sprintf "Expected error containing 'reason': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// @FT-CFP-006 -- Close a nonexistent fiscal period is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-006")>]
let ``close nonexistent period rejected`` () =
    let cmd = { fiscalPeriodId = 999999 }
    let result = FiscalPeriodService.closePeriod cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent period")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                    sprintf "Expected error containing 'does not exist': %A" errs)

// =====================================================================
// @FT-CFP-007 -- Reopen a nonexistent fiscal period is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-007")>]
let ``reopen nonexistent period rejected`` () =
    let cmd = { fiscalPeriodId = 999999; reason = "Nonexistent period" }
    let result = FiscalPeriodService.reopenPeriod cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent period")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                    sprintf "Expected error containing 'does not exist': %A" errs)

// =====================================================================
// @FT-CFP-008 -- Full close-reopen-close cycle completes without error
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-008")>]
let ``close then reopen then close again full cycle`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true

        // Close
        let closeCmd = { fiscalPeriodId = fpId }
        match FiscalPeriodService.closePeriod closeCmd with
        | Ok p -> Assert.False(p.isOpen, "should be closed after first close")
        | Error errs -> Assert.Fail(sprintf "First close failed: %A" errs)

        // Reopen
        let reopenCmd = { fiscalPeriodId = fpId; reason = "Missed an invoice" }
        match FiscalPeriodService.reopenPeriod reopenCmd with
        | Ok p -> Assert.True(p.isOpen, "should be open after reopen")
        | Error errs -> Assert.Fail(sprintf "Reopen failed: %A" errs)

        // Close again
        match FiscalPeriodService.closePeriod closeCmd with
        | Ok p -> Assert.False(p.isOpen, "should be closed after second close")
        | Error errs -> Assert.Fail(sprintf "Second close failed: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// @FT-CFP-009 -- Close a period with no journal entries
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-009")>]
let ``close an empty period with no entries`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 5, 1)) (DateOnly(2026, 5, 31)) true
        let cmd = { fiscalPeriodId = fpId }
        let result = FiscalPeriodService.closePeriod cmd
        match result with
        | Ok period ->
            Assert.Equal(fpId, period.id)
            Assert.False(period.isOpen, "Empty period should close successfully")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// @FT-CFP-010 -- Posting is rejected after closing a period via closePeriod
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-010")>]
let ``posting rejected after close`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atAsset = InsertHelpers.insertAccountType conn tracker (prefix + "_as") "debit"
        let atRev = InsertHelpers.insertAccountType conn tracker (prefix + "_rv") "credit"
        let acct1 = InsertHelpers.insertAccount conn tracker (prefix + "A1") "Debit" atAsset true
        let acct2 = InsertHelpers.insertAccount conn tracker (prefix + "A2") "Credit" atRev true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true

        // Close the period via service
        let closeCmd = { fiscalPeriodId = fpId }
        match FiscalPeriodService.closePeriod closeCmd with
        | Ok _ -> ()
        | Error errs -> Assert.Fail(sprintf "Close failed: %A" errs)

        // Attempt to post a journal entry to the closed period
        let postCmd =
            { entryDate = DateOnly(2026, 4, 15)
              description = "Late entry"
              source = Some "manual"
              fiscalPeriodId = fpId
              lines =
                [ { accountId = acct1; amount = 100m; entryType = EntryType.Debit; memo = None }
                  { accountId = acct2; amount = 100m; entryType = EntryType.Credit; memo = None } ]
              references = [] }
        let result = JournalEntryService.post postCmd
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            Assert.Fail("Expected Error when posting to a closed period")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("not open")),
                        sprintf "Expected error containing 'not open': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// @FT-CFP-011 -- Reopen reason is logged at Info level
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-011")>]
let ``reopen reason is logged at info level`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        // Close first
        let closeCmd = { fiscalPeriodId = fpId }
        match FiscalPeriodService.closePeriod closeCmd with
        | Ok _ -> ()
        | Error errs -> Assert.Fail(sprintf "Setup close failed: %A" errs)
        // Reopen with a distinctive reason
        let reason = "Correcting prior-period error"
        let cmd = { fiscalPeriodId = fpId; reason = reason }
        let result = FiscalPeriodService.reopenPeriod cmd
        match result with
        | Ok period ->
            Assert.True(period.isOpen, "period.isOpen should be true after reopen")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        // Verify the reason appears in the log output
        let logContent = readLogContent()
        Assert.True(logContent.Contains(reason),
            sprintf "Log should contain the reopen reason '%s'. Log content length: %d" reason logContent.Length)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Structural tests (QE-owned, no Gherkin mapping)
// =====================================================================

[<Fact>]
let ``fiscal period is_open column exists and defaults to true`` () =
    use conn = DataSource.openConnection()
    use cmd = new NpgsqlCommand(
        "SELECT column_default FROM information_schema.columns
         WHERE table_schema = 'ledger' AND table_name = 'fiscal_period' AND column_name = 'is_open'",
        conn)
    use reader = cmd.ExecuteReader()
    Assert.True(reader.Read(), "is_open column should exist in ledger.fiscal_period")
    let defaultVal = reader.GetString(0)
    Assert.True(defaultVal.Contains("true"), sprintf "is_open should default to true, got: %s" defaultVal)

[<Fact>]
let ``closePeriod and reopenPeriod are symmetric on the same period`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true

        // Close
        match FiscalPeriodService.closePeriod { fiscalPeriodId = fpId } with
        | Ok p -> Assert.False(p.isOpen)
        | Error errs -> Assert.Fail(sprintf "Close failed: %A" errs)

        // Verify DB state directly
        use qry = new NpgsqlCommand("SELECT is_open FROM ledger.fiscal_period WHERE id = @id", conn)
        qry.Parameters.AddWithValue("@id", fpId) |> ignore
        let isOpenAfterClose = qry.ExecuteScalar() :?> bool
        Assert.False(isOpenAfterClose, "DB should show is_open = false after close")

        // Reopen
        match FiscalPeriodService.reopenPeriod { fiscalPeriodId = fpId; reason = "Symmetry check" } with
        | Ok p -> Assert.True(p.isOpen)
        | Error errs -> Assert.Fail(sprintf "Reopen failed: %A" errs)

        // Verify DB state directly
        use qry2 = new NpgsqlCommand("SELECT is_open FROM ledger.fiscal_period WHERE id = @id", conn)
        qry2.Parameters.AddWithValue("@id", fpId) |> ignore
        let isOpenAfterReopen = qry2.ExecuteScalar() :?> bool
        Assert.True(isOpenAfterReopen, "DB should show is_open = true after reopen")
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Side Effects — @FT-CFP-012 (REM-011)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-012")>]
let ``closing a period does not modify account balances`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let assetTypeId = InsertHelpers.insertAccountType conn tracker (prefix + "_at") "debit"
        let revenueTypeId = InsertHelpers.insertAccountType conn tracker (prefix + "_rt") "credit"
        let assetAcct = InsertHelpers.insertAccount conn tracker (prefix + "AS") "Asset" assetTypeId true
        let revAcct = InsertHelpers.insertAccount conn tracker (prefix + "RV") "Revenue" revenueTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true

        // Post an entry
        let jeCmd : PostJournalEntryCommand =
            { entryDate = DateOnly(2026, 4, 15)
              description = "Test entry"
              source = Some "manual"
              fiscalPeriodId = fpId
              lines =
                [ { accountId = assetAcct; amount = 500m; entryType = EntryType.Debit; memo = None }
                  { accountId = revAcct; amount = 500m; entryType = EntryType.Credit; memo = None } ]
              references = [] }
        match JournalEntryService.post jeCmd with
        | Ok posted -> TestCleanup.trackJournalEntry posted.entry.id tracker
        | Error errs -> failwithf "Setup post failed: %A" errs

        // Record trial balance before close
        let beforeResult = TrialBalanceService.getByPeriodId fpId
        let beforeReport =
            match beforeResult with
            | Ok r -> r
            | Error e -> failwithf "Trial balance before close failed: %s" e

        // Close the period
        match FiscalPeriodService.closePeriod { fiscalPeriodId = fpId } with
        | Ok _ -> ()
        | Error errs -> failwithf "Close failed: %A" errs

        // Record trial balance after close
        let afterResult = TrialBalanceService.getByPeriodId fpId
        let afterReport =
            match afterResult with
            | Ok r -> r
            | Error e -> failwithf "Trial balance after close failed: %s" e

        // Totals must be identical
        Assert.Equal(beforeReport.grandTotalDebits, afterReport.grandTotalDebits)
        Assert.Equal(beforeReport.grandTotalCredits, afterReport.grandTotalCredits)
    finally TestCleanup.deleteAll tracker

[<Fact>]
let ``reopenPeriod validates reason before hitting the database`` () =
    // Empty reason should fail fast without touching the DB.
    // We verify this by passing a nonexistent period ID with an empty reason --
    // the error should be about the reason, not about the period.
    let cmd = { fiscalPeriodId = 999998; reason = "" }
    let result = FiscalPeriodService.reopenPeriod cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for empty reason")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("reason")),
                    sprintf "Should fail on reason validation, not period lookup: %A" errs)
        Assert.False(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                     sprintf "Should NOT contain 'does not exist' -- reason validation should short-circuit: %A" errs)
