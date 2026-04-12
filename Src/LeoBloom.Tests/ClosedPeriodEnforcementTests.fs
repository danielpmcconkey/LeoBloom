module LeoBloom.Tests.ClosedPeriodEnforcementTests

// =====================================================================
// Closed Period Enforcement Tests
// Covers Gherkin scenarios in Specs/Behavioral/ClosedPeriodEnforcement.feature
//
// Fiscal period year reservation: 2100 (per DSWF/qe.md table — P083)
// All tests use a single NpgsqlTransaction that is rolled back on dispose.
// No explicit cleanup needed for transactional tests.
// =====================================================================

open System
open Npgsql
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Tests.TestHelpers

/// Insert a fiscal period and immediately close it (set is_open=false, closed_at=now()).
let private insertClosedFiscalPeriod (txn: NpgsqlTransaction) (periodKey: string) (startDate: DateOnly) (endDate: DateOnly) : int =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date, is_open, closed_at) \
         VALUES (@k, @s, @e, false, now()) RETURNING id",
        txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@k", periodKey) |> ignore
    cmd.Parameters.AddWithValue("@s", startDate) |> ignore
    cmd.Parameters.AddWithValue("@e", endDate) |> ignore
    cmd.ExecuteScalar() :?> int

let private postTestEntry
    (txn: NpgsqlTransaction)
    (acct1: int) (acct2: int)
    (fpId: int)
    (entryDate: DateOnly)
    (description: string)
    (amount: decimal) : int =
    let cmd : PostJournalEntryCommand =
        { entryDate = entryDate
          description = description
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = amount; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = amount; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    // Must insert directly to bypass closed-period check in JournalEntryService.post
    use insertCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id, source, created_at) \
         VALUES (@d, @desc, @fp, @src, now()) RETURNING id",
        txn.Connection)
    insertCmd.Transaction <- txn
    insertCmd.Parameters.AddWithValue("@d", entryDate) |> ignore
    insertCmd.Parameters.AddWithValue("@desc", description) |> ignore
    insertCmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    insertCmd.Parameters.AddWithValue("@src", "manual" :> obj) |> ignore
    let jeId = insertCmd.ExecuteScalar() :?> int
    // Insert lines
    use lineCmd1 = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) \
         VALUES (@je, @a, @amt, 'debit')",
        txn.Connection)
    lineCmd1.Transaction <- txn
    lineCmd1.Parameters.AddWithValue("@je", jeId) |> ignore
    lineCmd1.Parameters.AddWithValue("@a", acct1) |> ignore
    lineCmd1.Parameters.AddWithValue("@amt", amount) |> ignore
    lineCmd1.ExecuteNonQuery() |> ignore
    use lineCmd2 = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) \
         VALUES (@je, @a, @amt, 'credit')",
        txn.Connection)
    lineCmd2.Transaction <- txn
    lineCmd2.Parameters.AddWithValue("@je", jeId) |> ignore
    lineCmd2.Parameters.AddWithValue("@a", acct2) |> ignore
    lineCmd2.Parameters.AddWithValue("@amt", amount) |> ignore
    lineCmd2.ExecuteNonQuery() |> ignore
    jeId

// --- FT-CPE-001: Voiding a JE in a closed period is rejected with an actionable error ---

[<Fact>]
[<Trait("GherkinId", "FT-CPE-001")>]
let ``void in closed period is rejected with actionable error naming period and close date`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atAsset = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let atRev   = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let acct1   = InsertHelpers.insertAccount txn (prefix + "A1") "Asset" atAsset true
    let acct2   = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" atRev true
    // Create a period named "March 2100" and close it
    let fpId = insertClosedFiscalPeriod txn (prefix + "FP") (DateOnly(2100, 3, 1)) (DateOnly(2100, 3, 31))
    let jeId = postTestEntry txn acct1 acct2 fpId (DateOnly(2100, 3, 15)) "March rent" 1000m
    let voidCmd = { journalEntryId = jeId; voidReason = "Error correction" }
    let result = JournalEntryService.voidEntry txn voidCmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error: void in closed period should be rejected")
    | Error errs ->
        // Error names the period key
        Assert.True(
            errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("closed")),
            sprintf "Expected error mentioning 'closed': %A" errs)
        // Error includes a close date (not "unknown" — we set closed_at)
        Assert.True(
            errs |> List.exists (fun e ->
                e.Contains("2100-") || e.Contains("closed") || e.Contains("closed_at")),
            sprintf "Expected error to include close date info: %A" errs)
        // Error does NOT say "unknown" for the close date
        Assert.False(
            errs |> List.forall (fun e -> e.Contains("unknown")),
            sprintf "Close date should not be 'unknown' when closed_at is set: %A" errs)
        // Error suggests reversal command
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("ledger reverse")),
            sprintf "Expected error to suggest 'ledger reverse': %A" errs)

// --- FT-CPE-002: Voiding a JE in an open period continues to work ---

[<Fact>]
[<Trait("GherkinId", "FT-CPE-002")>]
let ``void in open period succeeds with voided_at set and void reason preserved`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atAsset = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let atRev   = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let acct1   = InsertHelpers.insertAccount txn (prefix + "A1") "Asset" atAsset true
    let acct2   = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" atRev true
    let fpId    = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2100, 4, 1)) (DateOnly(2100, 4, 30)) true
    // Post the entry using service (open period — this should succeed)
    let postCmd : PostJournalEntryCommand =
        { entryDate = DateOnly(2100, 4, 15)
          description = "April rent"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 800m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 800m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let jeId =
        match JournalEntryService.post txn postCmd with
        | Ok posted -> posted.entry.id
        | Error errs -> failwith (sprintf "Setup post failed: %A" errs)
    let voidCmd = { journalEntryId = jeId; voidReason = "Duplicate posting" }
    let result = JournalEntryService.voidEntry txn voidCmd
    match result with
    | Ok entry ->
        Assert.True(entry.voidedAt.IsSome, "voidedAt should be Some after void")
        Assert.Equal(Some "Duplicate posting", entry.voidReason)
    | Error errs -> Assert.Fail(sprintf "Expected void to succeed: %A" errs)
