module LeoBloom.Tests.PreCloseValidationTests

open System
open System.IO
open Npgsql
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Ops
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests

// =====================================================================
// Pre-Close Validation Tests — P082
// Fiscal period year reservation: 2099
//
// Service tests: transaction-based, roll back on dispose, no cleanup.
// CLI tests:     committed data via PcvCliEnv helper, cleanup in finally.
// =====================================================================

// Standard seeded account type IDs
let private assetTypeId     = 1
let private liabilityTypeId = 2

let private logDir = "/workspace/application_logs/leobloom"

let private readLogContent () =
    if not (Directory.Exists logDir) then ""
    else
        Directory.GetFiles(logDir, "leobloom-*.log")
        |> Array.map File.ReadAllText
        |> String.concat "\n"

// =====================================================================
// Raw SQL helpers — bypass JournalEntryService to insert bad data
// =====================================================================

/// Insert a single journal entry line via raw SQL.
let private insertJeLine (txn: NpgsqlTransaction) (jeId: int) (accountId: int) (amount: decimal) (entryType: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line \
         (journal_entry_id, account_id, amount, entry_type, memo) \
         VALUES (@jeid, @acct, @amt, @et, NULL)",
        txn.Connection, txn)
    cmd.Parameters.AddWithValue("@jeid", jeId) |> ignore
    cmd.Parameters.AddWithValue("@acct", accountId) |> ignore
    cmd.Parameters.AddWithValue("@amt", amount) |> ignore
    cmd.Parameters.AddWithValue("@et", entryType) |> ignore
    cmd.ExecuteNonQuery() |> ignore

/// Void a journal entry with NULL void_reason via raw SQL.
let private voidJeNullReason (txn: NpgsqlTransaction) (jeId: int) =
    use cmd = new NpgsqlCommand(
        "UPDATE ledger.journal_entry \
         SET voided_at = @va, void_reason = NULL WHERE id = @id",
        txn.Connection, txn)
    cmd.Parameters.AddWithValue("@va", DateTimeOffset.UtcNow) |> ignore
    cmd.Parameters.AddWithValue("@id", jeId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

// =====================================================================
// CLI env helper for committed tests
// =====================================================================

module PcvCliEnv =
    type Env =
        { PeriodId: int
          Connection: NpgsqlConnection }

    /// Delete JE lines, JEs, audit rows, and the period. Swallows cleanup errors.
    let cleanup (env: Env) =
        try
            use cmd = new NpgsqlCommand(
                "DELETE FROM ledger.journal_entry_line \
                 WHERE journal_entry_id IN \
                   (SELECT id FROM ledger.journal_entry WHERE fiscal_period_id = @fp)",
                env.Connection)
            cmd.Parameters.AddWithValue("@fp", env.PeriodId) |> ignore
            cmd.ExecuteNonQuery() |> ignore
        with ex -> eprintfn "PcvCliEnv cleanup: JE lines failed for period %d: %s" env.PeriodId ex.Message
        try
            use cmd = new NpgsqlCommand(
                "DELETE FROM ledger.journal_entry WHERE fiscal_period_id = @fp",
                env.Connection)
            cmd.Parameters.AddWithValue("@fp", env.PeriodId) |> ignore
            cmd.ExecuteNonQuery() |> ignore
        with ex -> eprintfn "PcvCliEnv cleanup: JE failed for period %d: %s" env.PeriodId ex.Message
        try
            use cmd = new NpgsqlCommand(
                "DELETE FROM ledger.fiscal_period_audit WHERE fiscal_period_id = @fp",
                env.Connection)
            cmd.Parameters.AddWithValue("@fp", env.PeriodId) |> ignore
            cmd.ExecuteNonQuery() |> ignore
        with ex -> eprintfn "PcvCliEnv cleanup: audit failed for period %d: %s" env.PeriodId ex.Message
        try
            use cmd = new NpgsqlCommand(
                "DELETE FROM ledger.fiscal_period WHERE id = @fp",
                env.Connection)
            cmd.Parameters.AddWithValue("@fp", env.PeriodId) |> ignore
            cmd.ExecuteNonQuery() |> ignore
        with ex -> eprintfn "PcvCliEnv cleanup: period failed for period %d: %s" env.PeriodId ex.Message
        env.Connection.Dispose()

    /// Create a clean open period (no JEs, no obligations) and commit it.
    let createClean () : Env =
        let conn = DataSource.openConnection()
        let prefix = TestData.uniquePrefix()
        let key = TestData.periodKey prefix
        let periodId =
            use txn = conn.BeginTransaction()
            let id = InsertHelpers.insertFiscalPeriod txn key (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
            txn.Commit()
            id
        { PeriodId = periodId; Connection = conn }

    /// Create a period with a voided JE (null void_reason) and commit it.
    let createWithVoidedJeNullReason () : Env =
        let conn = DataSource.openConnection()
        let prefix = TestData.uniquePrefix()
        let key = TestData.periodKey prefix
        let periodId =
            use txn = conn.BeginTransaction()
            let id = InsertHelpers.insertFiscalPeriod txn key (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
            let jeId = InsertHelpers.insertJournalEntry txn (DateOnly(2099, 1, 10)) "voided test je" id
            use voidCmd = new NpgsqlCommand(
                "UPDATE ledger.journal_entry SET voided_at = @va, void_reason = NULL WHERE id = @jid",
                txn.Connection, txn)
            voidCmd.Parameters.AddWithValue("@va", DateTimeOffset.UtcNow) |> ignore
            voidCmd.Parameters.AddWithValue("@jid", jeId) |> ignore
            voidCmd.ExecuteNonQuery() |> ignore
            txn.Commit()
            id
        { PeriodId = periodId; Connection = conn }

    /// Create a period with an unbalanced JE (debit=750, credit=500) and commit it.
    let createWithUnbalancedJe () : Env =
        let conn = DataSource.openConnection()
        let prefix = TestData.uniquePrefix()
        let key = TestData.periodKey prefix
        let periodId =
            use txn = conn.BeginTransaction()
            let id = InsertHelpers.insertFiscalPeriod txn key (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
            let assetAcct = InsertHelpers.insertAccount txn (prefix + "AU") "TestAsset" assetTypeId true
            let liabAcct  = InsertHelpers.insertAccount txn (prefix + "LU") "TestLiab"  liabilityTypeId true
            let jeId = InsertHelpers.insertJournalEntry txn (DateOnly(2099, 1, 15)) "unbalanced je" id
            let insertLine (et: string) (acct: int) (amt: decimal) =
                use cmd = new NpgsqlCommand(
                    "INSERT INTO ledger.journal_entry_line \
                     (journal_entry_id, account_id, amount, entry_type, memo) \
                     VALUES (@jeid, @acct, @amt, @et, NULL)",
                    txn.Connection, txn)
                cmd.Parameters.AddWithValue("@jeid", jeId) |> ignore
                cmd.Parameters.AddWithValue("@acct", acct) |> ignore
                cmd.Parameters.AddWithValue("@amt", amt) |> ignore
                cmd.Parameters.AddWithValue("@et", et) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            insertLine "debit"  assetAcct 750m
            insertLine "credit" liabAcct  500m
            txn.Commit()
            id
        { PeriodId = periodId; Connection = conn }

// =====================================================================
// @FT-PCV-001 -- Clean period passes all checks and closes
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PCV-001")>]
let ``clean period with no issues passes validation and closes`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
    let cmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "test"; note = None; force = false }
    let result = FiscalPeriodCloseService.closePeriodWithValidation txn cmd
    match result with
    | Ok closeResult ->
        Assert.False(closeResult.Period.isOpen, "period should be closed after successful validation")
        Assert.True(closeResult.ValidationResult.AllPassed, "all validation checks should have passed")
    | Error errs ->
        Assert.Fail(sprintf "Expected Ok but validation blocked close: %A" errs)

// =====================================================================
// @FT-PCV-002 -- Trial balance disequilibrium blocks close
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PCV-002")>]
let ``trial balance disequilibrium blocks close and reports debit and credit totals`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let liabAcct  = InsertHelpers.insertAccount txn (prefix + "LI") "Liab"  liabilityTypeId true
    let jeId = InsertHelpers.insertJournalEntry txn (DateOnly(2099, 1, 10)) "unbalanced je" fpId
    insertJeLine txn jeId assetAcct 100m "debit"
    insertJeLine txn jeId liabAcct   50m "credit"
    let cmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "test"; note = None; force = false }
    let result = FiscalPeriodCloseService.closePeriodWithValidation txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error: trial balance disequilibrium should block close")
    | Error errs ->
        let tbErr = errs |> List.tryFind (fun e -> e.Contains("Trial balance disequilibrium"))
        Assert.True(tbErr.IsSome,
            sprintf "Expected trial balance disequilibrium error, got: %A" errs)
        let msg = tbErr.Value
        Assert.True(msg.Contains("debits ="), sprintf "Error should show debit total: %s" msg)
        Assert.True(msg.Contains("credits ="), sprintf "Error should show credit total: %s" msg)
        Assert.True(msg.Contains("100"), sprintf "Error should show debit amount 100: %s" msg)
        Assert.True(msg.Contains("50"), sprintf "Error should show credit amount 50: %s" msg)

// =====================================================================
// @FT-PCV-003 -- Balance sheet equation failure blocks close
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PCV-003")>]
let ``balance sheet equation failure blocks close and reports asset and liabilities-plus-equity totals`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
    // Single debit-only line on an asset account — makes assets > L+E in the balance sheet
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let jeId = InsertHelpers.insertJournalEntry txn (DateOnly(2099, 1, 10)) "bs imbalance je" fpId
    insertJeLine txn jeId assetAcct 300m "debit"
    let cmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "test"; note = None; force = false }
    let result = FiscalPeriodCloseService.closePeriodWithValidation txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error: balance sheet equation failure should block close")
    | Error errs ->
        let bsErr = errs |> List.tryFind (fun e -> e.Contains("Balance sheet equation violated"))
        Assert.True(bsErr.IsSome,
            sprintf "Expected balance sheet violation error, got: %A" errs)
        let msg = bsErr.Value
        Assert.True(msg.Contains("assets ="), sprintf "Error should show asset total: %s" msg)
        Assert.True(msg.Contains("liabilities + equity ="), sprintf "Error should show L+E total: %s" msg)

// =====================================================================
// @FT-PCV-004 -- Voided JE with null void_reason blocks close
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PCV-004")>]
let ``voided JE with null void_reason blocks close and lists offending JE IDs`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (DateOnly(2099, 1, 15)) "void no reason" fpId
    voidJeNullReason txn jeId
    let cmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "test"; note = None; force = false }
    let result = FiscalPeriodCloseService.closePeriodWithValidation txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error: voided JE with null void_reason should block close")
    | Error errs ->
        let hygieneErr = errs |> List.tryFind (fun e -> e.Contains("Voided JEs with no void_reason"))
        Assert.True(hygieneErr.IsSome,
            sprintf "Expected data hygiene error for voided JE, got: %A" errs)
        let msg = hygieneErr.Value
        Assert.True(msg.Contains(string jeId),
            sprintf "Error should list the offending JE ID %d: %s" jeId msg)

// =====================================================================
// @FT-PCV-005 -- JE with entry_date outside period range blocks close
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PCV-005")>]
let ``JE with entry_date outside period range blocks close and lists offending JE IDs`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
    // JE with entry_date Feb 1 — outside the Jan period range
    let jeId = InsertHelpers.insertJournalEntry txn (DateOnly(2099, 2, 1)) "out of range je" fpId
    let cmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "test"; note = None; force = false }
    let result = FiscalPeriodCloseService.closePeriodWithValidation txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error: JE with out-of-range entry_date should block close")
    | Error errs ->
        let hygieneErr = errs |> List.tryFind (fun e -> e.Contains("JEs with entry_date outside period range"))
        Assert.True(hygieneErr.IsSome,
            sprintf "Expected data hygiene error for out-of-range entry_date, got: %A" errs)
        let msg = hygieneErr.Value
        Assert.True(msg.Contains(string jeId),
            sprintf "Error should list the offending JE ID %d: %s" jeId msg)

// =====================================================================
// @FT-PCV-006 -- In-flight obligation blocks close
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PCV-006")>]
let ``in-flight obligation instance blocks close and lists instance ID and agreement name`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
    let agrId = InsertHelpers.insertObligationAgreement txn (prefix + "_rent")
    let instanceId =
        InsertHelpers.insertObligationInstanceFull txn agrId (prefix + "_inst")
            "in_flight" (DateOnly(2099, 1, 15)) None None true
    let cmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "test"; note = None; force = false }
    let result = FiscalPeriodCloseService.closePeriodWithValidation txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error: in-flight obligation should block close")
    | Error errs ->
        let oblErr = errs |> List.tryFind (fun e -> e.Contains("In-flight obligation instances blocking close"))
        Assert.True(oblErr.IsSome,
            sprintf "Expected open obligations error, got: %A" errs)
        let msg = oblErr.Value
        Assert.True(msg.Contains(sprintf "instance %d" instanceId),
            sprintf "Error should list instance ID %d: %s" instanceId msg)
        Assert.True(msg.Contains(prefix + "_rent"),
            sprintf "Error should include agreement name '%s_rent': %s" prefix msg)

// =====================================================================
// @FT-PCV-007 -- Non-blocking obligation statuses do not prevent close
// =====================================================================

let private tryCloseWithObligationStatus (status: string) =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
    let agrId = InsertHelpers.insertObligationAgreement txn (prefix + "_agr")
    InsertHelpers.insertObligationInstanceFull txn agrId (prefix + "_inst")
        status (DateOnly(2099, 1, 15)) None None true |> ignore
    let cmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "test"; note = None; force = false }
    FiscalPeriodCloseService.closePeriodWithValidation txn cmd

[<Fact>]
[<Trait("GherkinId", "FT-PCV-007")>]
let ``expected status obligation does not block close`` () =
    match tryCloseWithObligationStatus "expected" with
    | Ok r -> Assert.False(r.Period.isOpen, "period should be closed")
    | Error errs -> Assert.Fail(sprintf "Expected close to succeed with 'expected' status: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PCV-007")>]
let ``overdue status obligation does not block close`` () =
    match tryCloseWithObligationStatus "overdue" with
    | Ok r -> Assert.False(r.Period.isOpen, "period should be closed")
    | Error errs -> Assert.Fail(sprintf "Expected close to succeed with 'overdue' status: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PCV-007")>]
let ``confirmed status obligation does not block close`` () =
    match tryCloseWithObligationStatus "confirmed" with
    | Ok r -> Assert.False(r.Period.isOpen, "period should be closed")
    | Error errs -> Assert.Fail(sprintf "Expected close to succeed with 'confirmed' status: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PCV-007")>]
let ``posted status obligation does not block close`` () =
    match tryCloseWithObligationStatus "posted" with
    | Ok r -> Assert.False(r.Period.isOpen, "period should be closed")
    | Error errs -> Assert.Fail(sprintf "Expected close to succeed with 'posted' status: %A" errs)

// =====================================================================
// @FT-PCV-008 -- --force --note bypasses failures and logs bypass in audit
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PCV-008")>]
let ``force close with note bypasses validation and records bypass in audit entry`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let liabAcct  = InsertHelpers.insertAccount txn (prefix + "LI") "Liab"  liabilityTypeId true
    let jeId = InsertHelpers.insertJournalEntry txn (DateOnly(2099, 1, 10)) "unbalanced je" fpId
    insertJeLine txn jeId assetAcct 100m "debit"
    insertJeLine txn jeId liabAcct   50m "credit"
    let cmd : CloseFiscalPeriodCommand =
        { fiscalPeriodId = fpId; actor = "test"; note = Some "CFO approved manual close"; force = true }
    let result = FiscalPeriodCloseService.closePeriodWithValidation txn cmd
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected force close to succeed: %A" errs)
    | Ok closeResult ->
        Assert.False(closeResult.Period.isOpen, "period should be closed after force close")
        let auditEntries = FiscalPeriodAuditRepository.listByPeriod txn fpId
        Assert.True(auditEntries.Length > 0, "audit trail should not be empty")
        let closeEntry = auditEntries |> List.find (fun e -> e.action = "closed")
        Assert.True(closeEntry.note.IsSome, "audit entry should have a note")
        let note = closeEntry.note.Value
        Assert.True(note.Contains("[FORCE]"),
            sprintf "Audit note should contain [FORCE] prefix, got: %s" note)
        Assert.True(note.Contains("CFO approved manual close"),
            sprintf "Audit note should contain the force note text, got: %s" note)

// =====================================================================
// @FT-PCV-009 -- --force without --note is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PCV-009")>]
let ``force close without note is rejected with clear error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
    let cmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "test"; note = None; force = true }
    let result = FiscalPeriodCloseService.closePeriodWithValidation txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error: force without note should be rejected")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("--note")),
            sprintf "Error should mention '--note', got: %A" errs)

// =====================================================================
// @FT-PCV-010 -- fiscal-period validate reports failures without closing (CLI)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PCV-010")>]
let ``fiscal-period validate on dirty period exits non-zero and reports data hygiene failure without closing`` () =
    let env = PcvCliEnv.createWithVoidedJeNullReason()
    try
        let result = CliRunner.run (sprintf "period validate %d" env.PeriodId)
        Assert.NotEqual(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.True(
            stdout.Contains("DataHygiene"),
            sprintf "Output should identify DataHygiene check: %s" stdout)
        Assert.True(
            stdout.Contains("FAIL"),
            sprintf "Output should show FAIL status: %s" stdout)
        // Period must remain open
        use qry = new NpgsqlCommand("SELECT is_open FROM ledger.fiscal_period WHERE id = @id", env.Connection)
        qry.Parameters.AddWithValue("@id", env.PeriodId) |> ignore
        let isOpen = qry.ExecuteScalar() :?> bool
        Assert.True(isOpen, "Period should remain open after validate command")
    finally PcvCliEnv.cleanup env

// =====================================================================
// @FT-PCV-010b -- fiscal-period validate on clean period exits 0 without closing
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PCV-010b")>]
let ``fiscal-period validate on clean period exits 0 and reports all checks passed without closing`` () =
    let env = PcvCliEnv.createClean()
    try
        let result = CliRunner.run (sprintf "period validate %d" env.PeriodId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.True(
            stdout.Contains("All checks passed"),
            sprintf "Output should say 'All checks passed': %s" stdout)
        // Period must remain open
        use qry = new NpgsqlCommand("SELECT is_open FROM ledger.fiscal_period WHERE id = @id", env.Connection)
        qry.Parameters.AddWithValue("@id", env.PeriodId) |> ignore
        let isOpen = qry.ExecuteScalar() :?> bool
        Assert.True(isOpen, "Period should remain open after validate command")
    finally PcvCliEnv.cleanup env

// =====================================================================
// @FT-PCV-011 -- Validation results logged even on force-close
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PCV-011")>]
let ``validation results are computed and logged even when force-closing`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let liabAcct  = InsertHelpers.insertAccount txn (prefix + "LI") "Liab"  liabilityTypeId true
    let jeId = InsertHelpers.insertJournalEntry txn (DateOnly(2099, 1, 10)) "unbalanced for log test" fpId
    insertJeLine txn jeId assetAcct 100m "debit"
    insertJeLine txn jeId liabAcct   50m "credit"
    let cmd : CloseFiscalPeriodCommand =
        { fiscalPeriodId = fpId; actor = "test"; note = Some "emergency close"; force = true }
    match FiscalPeriodCloseService.closePeriodWithValidation txn cmd with
    | Error errs -> Assert.Fail(sprintf "Expected force close to succeed: %A" errs)
    | Ok closeResult ->
        Assert.False(closeResult.Period.isOpen, "period should be closed")
        let logContent = readLogContent()
        Assert.True(
            logContent.Contains("TrialBalanceEquilibrium"),
            sprintf "Log should contain trial balance check name. Log length: %d" logContent.Length)

// =====================================================================
// @FT-PCV-012 -- Multiple validation failures all reported together
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PCV-012")>]
let ``multiple validation failures are all reported together not fail-fast`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
    // 1. Unbalanced JE — causes trial balance failure
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let liabAcct  = InsertHelpers.insertAccount txn (prefix + "LI") "Liab"  liabilityTypeId true
    let jeId1 = InsertHelpers.insertJournalEntry txn (DateOnly(2099, 1, 10)) "unbalanced je" fpId
    insertJeLine txn jeId1 assetAcct 100m "debit"
    insertJeLine txn jeId1 liabAcct   50m "credit"
    // 2. Voided JE with null void_reason — causes data hygiene failure
    let jeId2 = InsertHelpers.insertJournalEntry txn (DateOnly(2099, 1, 15)) "voided no reason" fpId
    voidJeNullReason txn jeId2
    // 3. In-flight obligation — causes open obligations failure
    let agrId = InsertHelpers.insertObligationAgreement txn (prefix + "_agr")
    InsertHelpers.insertObligationInstanceFull txn agrId (prefix + "_inst")
        "in_flight" (DateOnly(2099, 1, 15)) None None true |> ignore
    let cmd : CloseFiscalPeriodCommand = { fiscalPeriodId = fpId; actor = "test"; note = None; force = false }
    let result = FiscalPeriodCloseService.closePeriodWithValidation txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error: multiple validation failures should block close")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("Trial balance")),
            sprintf "Errors should include trial balance failure: %A" errs)
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("Voided JEs")),
            sprintf "Errors should include data hygiene failure: %A" errs)
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("In-flight")),
            sprintf "Errors should include open obligations failure: %A" errs)
        Assert.True(errs.Length >= 3,
            sprintf "Should report at least 3 failures but got %d: %A" errs.Length errs)
