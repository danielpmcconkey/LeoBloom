module LeoBloom.Tests.FiscalPeriodTests

open System
open System.IO
open Xunit
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
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
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
    let cmd = { fiscalPeriodId = fpId; actor = "test"; note = None }
    let result = FiscalPeriodService.closePeriod txn cmd
    match result with
    | Ok period ->
        Assert.Equal(fpId, period.id)
        Assert.False(period.isOpen, "period.isOpen should be false after close")
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-CFP-002 -- Reopen a closed fiscal period with a reason
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-002")>]
let ``reopen a closed period with reason`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
    // Close first
    let closeCmd = { fiscalPeriodId = fpId; actor = "test"; note = None }
    match FiscalPeriodService.closePeriod txn closeCmd with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup close failed: %A" errs)
    // Reopen
    let cmd = { fiscalPeriodId = fpId; reason = "Quarter-end adjustment needed"; actor = "test" }
    let result = FiscalPeriodService.reopenPeriod txn cmd
    match result with
    | Ok period ->
        Assert.Equal(fpId, period.id)
        Assert.True(period.isOpen, "period.isOpen should be true after reopen")
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-CFP-003 -- Closing an already-closed period is idempotent
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-003")>]
let ``close an already-closed period is idempotent`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
    // Close first
    let closeCmd = { fiscalPeriodId = fpId; actor = "test"; note = None }
    match FiscalPeriodService.closePeriod txn closeCmd with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup close failed: %A" errs)
    // Close again (idempotent)
    let result = FiscalPeriodService.closePeriod txn closeCmd
    match result with
    | Ok period ->
        Assert.Equal(fpId, period.id)
        Assert.False(period.isOpen, "period.isOpen should be false")
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-CFP-004 -- Reopening an already-open period is idempotent
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-004")>]
let ``reopen an already-open period is idempotent`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
    let cmd = { fiscalPeriodId = fpId; reason = "Already open, should still succeed"; actor = "test" }
    let result = FiscalPeriodService.reopenPeriod txn cmd
    match result with
    | Ok period ->
        Assert.Equal(fpId, period.id)
        Assert.True(period.isOpen, "period.isOpen should be true")
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-CFP-005 -- Reopen with invalid reason is rejected (empty string)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-005")>]
let ``reopen with empty reason rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
    // Close first
    let closeCmd = { fiscalPeriodId = fpId; actor = "test"; note = None }
    match FiscalPeriodService.closePeriod txn closeCmd with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup close failed: %A" errs)
    // Reopen with empty reason
    let cmd = { fiscalPeriodId = fpId; reason = ""; actor = "test" }
    let result = FiscalPeriodService.reopenPeriod txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for empty reason")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("reason")),
                    sprintf "Expected error containing 'reason': %A" errs)

// =====================================================================
// @FT-CFP-005 -- Reopen with invalid reason is rejected (whitespace-only)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-005")>]
let ``reopen with whitespace-only reason rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
    // Close first
    let closeCmd = { fiscalPeriodId = fpId; actor = "test"; note = None }
    match FiscalPeriodService.closePeriod txn closeCmd with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup close failed: %A" errs)
    // Reopen with whitespace reason
    let cmd = { fiscalPeriodId = fpId; reason = "   "; actor = "test" }
    let result = FiscalPeriodService.reopenPeriod txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for whitespace-only reason")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("reason")),
                    sprintf "Expected error containing 'reason': %A" errs)

// =====================================================================
// @FT-CFP-006 -- Close a nonexistent fiscal period is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-006")>]
let ``close nonexistent period rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let cmd = { fiscalPeriodId = 999999; actor = "test"; note = None }
    let result = FiscalPeriodService.closePeriod txn cmd
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
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let cmd = { fiscalPeriodId = 999999; reason = "Nonexistent period"; actor = "test" }
    let result = FiscalPeriodService.reopenPeriod txn cmd
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
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true

    // Close
    let closeCmd = { fiscalPeriodId = fpId; actor = "test"; note = None }
    match FiscalPeriodService.closePeriod txn closeCmd with
    | Ok p -> Assert.False(p.isOpen, "should be closed after first close")
    | Error errs -> Assert.Fail(sprintf "First close failed: %A" errs)

    // Reopen
    let reopenCmd = { fiscalPeriodId = fpId; reason = "Missed an invoice"; actor = "test" }
    match FiscalPeriodService.reopenPeriod txn reopenCmd with
    | Ok p -> Assert.True(p.isOpen, "should be open after reopen")
    | Error errs -> Assert.Fail(sprintf "Reopen failed: %A" errs)

    // Close again
    match FiscalPeriodService.closePeriod txn closeCmd with
    | Ok p -> Assert.False(p.isOpen, "should be closed after second close")
    | Error errs -> Assert.Fail(sprintf "Second close failed: %A" errs)

// CFP-009 removed (REM-014): redundant with CFP-001

// =====================================================================
// @FT-CFP-010 -- Posting is rejected after closing a period via closePeriod
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-010")>]
let ``posting rejected after close`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atAsset = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let atRev = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Debit" atAsset true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Credit" atRev true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true

    // Close the period via service
    let closeCmd = { fiscalPeriodId = fpId; actor = "test"; note = None }
    match FiscalPeriodService.closePeriod txn closeCmd with
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
    let result = JournalEntryService.post txn postCmd
    match result with
    | Ok _ ->
        Assert.Fail("Expected Error when posting to a closed period")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("not open")),
                    sprintf "Expected error containing 'not open': %A" errs)

// =====================================================================
// @FT-CFP-011 -- Reopen reason is logged at Info level
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-011")>]
let ``reopen reason is logged at info level`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
    // Close first
    let closeCmd = { fiscalPeriodId = fpId; actor = "test"; note = None }
    match FiscalPeriodService.closePeriod txn closeCmd with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup close failed: %A" errs)
    // Reopen with a distinctive reason
    let reason = "Correcting prior-period error"
    let cmd = { fiscalPeriodId = fpId; reason = reason; actor = "test" }
    let result = FiscalPeriodService.reopenPeriod txn cmd
    match result with
    | Ok period ->
        Assert.True(period.isOpen, "period.isOpen should be true after reopen")
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    // Verify the reason appears in the log output
    let logContent = readLogContent()
    Assert.True(logContent.Contains(reason),
        sprintf "Log should contain the reopen reason '%s'. Log content length: %d" reason logContent.Length)

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
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true

    // Close
    match FiscalPeriodService.closePeriod txn { fiscalPeriodId = fpId; actor = "test"; note = None } with
    | Ok p -> Assert.False(p.isOpen)
    | Error errs -> Assert.Fail(sprintf "Close failed: %A" errs)

    // Verify DB state directly
    use qry = new NpgsqlCommand("SELECT is_open FROM ledger.fiscal_period WHERE id = @id", txn.Connection)
    qry.Transaction <- txn
    qry.Parameters.AddWithValue("@id", fpId) |> ignore
    let isOpenAfterClose = qry.ExecuteScalar() :?> bool
    Assert.False(isOpenAfterClose, "DB should show is_open = false after close")

    // Reopen
    match FiscalPeriodService.reopenPeriod txn { fiscalPeriodId = fpId; reason = "Symmetry check"; actor = "test" } with
    | Ok p -> Assert.True(p.isOpen)
    | Error errs -> Assert.Fail(sprintf "Reopen failed: %A" errs)

    // Verify DB state directly
    use qry2 = new NpgsqlCommand("SELECT is_open FROM ledger.fiscal_period WHERE id = @id", txn.Connection)
    qry2.Transaction <- txn
    qry2.Parameters.AddWithValue("@id", fpId) |> ignore
    let isOpenAfterReopen = qry2.ExecuteScalar() :?> bool
    Assert.True(isOpenAfterReopen, "DB should show is_open = true after reopen")

// =====================================================================
// Side Effects — @FT-CFP-012 (REM-011)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CFP-012")>]
let ``closing a period does not modify account balances`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetTypeId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let revenueTypeId = InsertHelpers.insertAccountType txn (prefix + "_rt") "credit"
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true

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
    match JournalEntryService.post txn jeCmd with
    | Ok _ -> ()
    | Error errs -> failwithf "Setup post failed: %A" errs

    // Record trial balance before close
    let beforeReport =
        match TrialBalanceService.getByPeriodId txn fpId with
        | Ok r -> r
        | Error e -> failwithf "Trial balance before close failed: %s" e

    // Close the period
    match FiscalPeriodService.closePeriod txn { fiscalPeriodId = fpId; actor = "test"; note = None } with
    | Ok _ -> ()
    | Error errs -> failwithf "Close failed: %A" errs

    // Record trial balance after close
    let afterReport =
        match TrialBalanceService.getByPeriodId txn fpId with
        | Ok r -> r
        | Error e -> failwithf "Trial balance after close failed: %s" e

    // Totals must be identical
    Assert.Equal(beforeReport.grandTotalDebits, afterReport.grandTotalDebits)
    Assert.Equal(beforeReport.grandTotalCredits, afterReport.grandTotalCredits)

[<Fact>]
let ``reopenPeriod validates reason before hitting the database`` () =
    // Empty reason should fail fast without touching the DB.
    // We verify this by passing a nonexistent period ID with an empty reason --
    // the error should be about the reason, not about the period.
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let cmd = { fiscalPeriodId = 999998; reason = ""; actor = "test" }
    let result = FiscalPeriodService.reopenPeriod txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for empty reason")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("reason")),
                    sprintf "Should fail on reason validation, not period lookup: %A" errs)
        Assert.False(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                     sprintf "Should NOT contain 'does not exist' -- reason validation should short-circuit: %A" errs)
