module LeoBloom.Tests.VoidJournalEntryTests

open System
open Xunit
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Tests.TestHelpers

let private postSetupEntry (txn: NpgsqlTransaction) (prefix: string) =
    let atAsset = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let atRev = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Debit" atAsset true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Credit" atRev true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Setup entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    match JournalEntryService.post txn cmd with
    | Ok posted -> (posted.entry.id, fpId, acct1, acct2)
    | Error errs -> failwith (sprintf "Setup post failed: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-VJE-001")>]
let ``void an active entry successfully`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (entryId, _, _, _) = postSetupEntry txn prefix
    let voidCmd = { journalEntryId = entryId; voidReason = "Duplicate posting" }
    let result = JournalEntryService.voidEntry txn voidCmd
    match result with
    | Ok entry ->
        Assert.True(entry.voidedAt.IsSome, "voidedAt should be Some")
        Assert.Equal(Some "Duplicate posting", entry.voidReason)
        let elapsed = DateTimeOffset.UtcNow - entry.voidedAt.Value
        Assert.True(elapsed.TotalSeconds < 30.0, sprintf "voidedAt should be recent, was %A ago" elapsed)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-VJE-002")>]
let ``voided entry remains in database`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (entryId, _, _, _) = postSetupEntry txn prefix
    let voidCmd = { journalEntryId = entryId; voidReason = "Error correction" }
    let result = JournalEntryService.voidEntry txn voidCmd
    match result with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Void failed: %A" errs)
    use checkCmd = new NpgsqlCommand("SELECT description, voided_at FROM ledger.journal_entry WHERE id = @id", txn.Connection)
    checkCmd.Transaction <- txn
    checkCmd.Parameters.AddWithValue("@id", entryId) |> ignore
    use reader = checkCmd.ExecuteReader()
    Assert.True(reader.Read(), "Entry should still exist in database")
    let desc = reader.GetString(0)
    let voidedAt = reader.GetValue(1)
    Assert.Equal("Setup entry", desc)
    Assert.True(voidedAt <> (box DBNull.Value), "voided_at should not be null")

[<Fact>]
[<Trait("GherkinId", "FT-VJE-003")>]
let ``lines and references intact after void`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atAsset = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let atRev = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Debit" atAsset true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Credit" atRev true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let postCmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Entry with refs"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
          references =
            [ { referenceType = "cheque"; referenceValue = "5678" } ]
          adjustmentForPeriodId = None }
    let entryId =
        match JournalEntryService.post txn postCmd with
        | Ok posted -> posted.entry.id
        | Error errs -> failwith (sprintf "Setup post failed: %A" errs)
    let voidCmd = { journalEntryId = entryId; voidReason = "Test void" }
    match JournalEntryService.voidEntry txn voidCmd with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Void failed: %A" errs)
    use lineCmd = new NpgsqlCommand("SELECT COUNT(*) FROM ledger.journal_entry_line WHERE journal_entry_id = @id", txn.Connection)
    lineCmd.Transaction <- txn
    lineCmd.Parameters.AddWithValue("@id", entryId) |> ignore
    let lineCount = lineCmd.ExecuteScalar() :?> int64
    Assert.Equal(2L, lineCount)
    use refCmd = new NpgsqlCommand("SELECT reference_type, reference_value FROM ledger.journal_entry_reference WHERE journal_entry_id = @id", txn.Connection)
    refCmd.Transaction <- txn
    refCmd.Parameters.AddWithValue("@id", entryId) |> ignore
    use reader = refCmd.ExecuteReader()
    Assert.True(reader.Read(), "Should have a reference row")
    Assert.Equal("cheque", reader.GetString(0))
    Assert.Equal("5678", reader.GetString(1))

[<Fact>]
[<Trait("GherkinId", "FT-VJE-004")>]
let ``void already-voided entry is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (entryId, _, _, _) = postSetupEntry txn prefix
    let voidCmd1 = { journalEntryId = entryId; voidReason = "First void" }
    match JournalEntryService.voidEntry txn voidCmd1 with
    | Ok _ -> ()
    | Error errs -> failwith (sprintf "First void failed: %A" errs)
    let voidCmd2 = { journalEntryId = entryId; voidReason = "Second void" }
    let result2 = JournalEntryService.voidEntry txn voidCmd2
    match result2 with
    | Ok _ -> Assert.Fail("Expected Error: second void of same entry should be rejected")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("already voided")),
                    sprintf "Expected error containing 'already voided': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-VJE-005")>]
let ``empty void reason rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (entryId, _, _, _) = postSetupEntry txn prefix
    let voidCmd = { journalEntryId = entryId; voidReason = "" }
    let result = JournalEntryService.voidEntry txn voidCmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for empty void reason")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("reason")),
                    sprintf "Expected error containing 'reason': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-VJE-006")>]
let ``whitespace-only void reason rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (entryId, _, _, _) = postSetupEntry txn prefix
    let voidCmd = { journalEntryId = entryId; voidReason = "   " }
    let result = JournalEntryService.voidEntry txn voidCmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for whitespace-only void reason")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("reason")),
                    sprintf "Expected error containing 'reason': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-VJE-007")>]
let ``nonexistent entry ID rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let voidCmd = { journalEntryId = 999999; voidReason = "Should not exist" }
    let result = JournalEntryService.voidEntry txn voidCmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent entry ID")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                    sprintf "Expected error containing 'does not exist': %A" errs)

// FT-VJE-008 replaced by FT-CPE-001: void is now blocked on closed periods (P083).
[<Fact>]
[<Trait("GherkinId", "FT-CPE-001")>]
let ``void is rejected when fiscal period is closed`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (entryId, fpId, _, _) = postSetupEntry txn prefix
    // Close the fiscal period after posting
    use closeCmd = new NpgsqlCommand("UPDATE ledger.fiscal_period SET is_open = false WHERE id = @id", txn.Connection)
    closeCmd.Transaction <- txn
    closeCmd.Parameters.AddWithValue("@id", fpId) |> ignore
    closeCmd.ExecuteNonQuery() |> ignore
    let voidCmd = { journalEntryId = entryId; voidReason = "Should be rejected" }
    let result = JournalEntryService.voidEntry txn voidCmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error: void in closed period should be rejected")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("closed period")),
                    sprintf "Expected error containing 'closed period': %A" errs)
        Assert.True(errs |> List.exists (fun e -> e.Contains("leobloom ledger reverse")),
                    sprintf "Expected error to suggest 'leobloom ledger reverse': %A" errs)
