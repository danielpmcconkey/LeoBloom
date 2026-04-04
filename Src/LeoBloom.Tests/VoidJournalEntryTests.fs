module LeoBloom.Tests.VoidJournalEntryTests

open System
open Xunit
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

let private postSetupEntry (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (prefix: string) =
    let atAsset = InsertHelpers.insertAccountType conn tracker (prefix + "_as") "debit"
    let atRev = InsertHelpers.insertAccountType conn tracker (prefix + "_rv") "credit"
    let acct1 = InsertHelpers.insertAccount conn tracker (prefix + "A1") "Debit" atAsset true
    let acct2 = InsertHelpers.insertAccount conn tracker (prefix + "A2") "Credit" atRev true
    let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Setup entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
          references = [] }
    match JournalEntryService.post cmd with
    | Ok posted ->
        TestCleanup.trackJournalEntry posted.entry.id tracker
        (posted.entry.id, fpId, acct1, acct2)
    | Error errs -> failwith (sprintf "Setup post failed: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-VJE-001")>]
let ``void an active entry successfully`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (entryId, _, _, _) = postSetupEntry conn tracker prefix
        let voidCmd = { journalEntryId = entryId; voidReason = "Duplicate posting" }
        let result = JournalEntryService.voidEntry voidCmd
        match result with
        | Ok entry ->
            Assert.True(entry.voidedAt.IsSome, "voidedAt should be Some")
            Assert.Equal(Some "Duplicate posting", entry.voidReason)
            let elapsed = DateTimeOffset.UtcNow - entry.voidedAt.Value
            Assert.True(elapsed.TotalSeconds < 30.0, sprintf "voidedAt should be recent, was %A ago" elapsed)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-VJE-002")>]
let ``voided entry remains in database`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (entryId, _, _, _) = postSetupEntry conn tracker prefix
        let voidCmd = { journalEntryId = entryId; voidReason = "Error correction" }
        let result = JournalEntryService.voidEntry voidCmd
        match result with
        | Ok _ -> ()
        | Error errs -> Assert.Fail(sprintf "Void failed: %A" errs)
        use checkCmd = new NpgsqlCommand("SELECT description, voided_at FROM ledger.journal_entry WHERE id = @id", conn)
        checkCmd.Parameters.AddWithValue("@id", entryId) |> ignore
        use reader = checkCmd.ExecuteReader()
        Assert.True(reader.Read(), "Entry should still exist in database")
        let desc = reader.GetString(0)
        let voidedAt = reader.GetValue(1)
        Assert.Equal("Setup entry", desc)
        Assert.True(voidedAt <> (box DBNull.Value), "voided_at should not be null")
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-VJE-003")>]
let ``lines and references intact after void`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atAsset = InsertHelpers.insertAccountType conn tracker (prefix + "_as") "debit"
        let atRev = InsertHelpers.insertAccountType conn tracker (prefix + "_rv") "credit"
        let acct1 = InsertHelpers.insertAccount conn tracker (prefix + "A1") "Debit" atAsset true
        let acct2 = InsertHelpers.insertAccount conn tracker (prefix + "A2") "Credit" atRev true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        let postCmd =
            { entryDate = DateOnly(2026, 3, 15)
              description = "Entry with refs"
              source = Some "manual"
              fiscalPeriodId = fpId
              lines =
                [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = None }
                  { accountId = acct2; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
              references =
                [ { referenceType = "cheque"; referenceValue = "5678" } ] }
        let entryId =
            match JournalEntryService.post postCmd with
            | Ok posted ->
                TestCleanup.trackJournalEntry posted.entry.id tracker
                posted.entry.id
            | Error errs -> failwith (sprintf "Setup post failed: %A" errs)
        let voidCmd = { journalEntryId = entryId; voidReason = "Test void" }
        match JournalEntryService.voidEntry voidCmd with
        | Ok _ -> ()
        | Error errs -> Assert.Fail(sprintf "Void failed: %A" errs)
        use lineCmd = new NpgsqlCommand("SELECT COUNT(*) FROM ledger.journal_entry_line WHERE journal_entry_id = @id", conn)
        lineCmd.Parameters.AddWithValue("@id", entryId) |> ignore
        let lineCount = lineCmd.ExecuteScalar() :?> int64
        Assert.Equal(2L, lineCount)
        use refCmd = new NpgsqlCommand("SELECT reference_type, reference_value FROM ledger.journal_entry_reference WHERE journal_entry_id = @id", conn)
        refCmd.Parameters.AddWithValue("@id", entryId) |> ignore
        use reader = refCmd.ExecuteReader()
        Assert.True(reader.Read(), "Should have a reference row")
        Assert.Equal("cheque", reader.GetString(0))
        Assert.Equal("5678", reader.GetString(1))
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-VJE-004")>]
let ``void already-voided entry is idempotent`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (entryId, _, _, _) = postSetupEntry conn tracker prefix
        let voidCmd1 = { journalEntryId = entryId; voidReason = "First void" }
        let firstVoidedAt, firstModifiedAt =
            match JournalEntryService.voidEntry voidCmd1 with
            | Ok entry -> (entry.voidedAt, entry.modifiedAt)
            | Error errs -> failwith (sprintf "First void failed: %A" errs)
        let voidCmd2 = { journalEntryId = entryId; voidReason = "Second void" }
        let result2 = JournalEntryService.voidEntry voidCmd2
        match result2 with
        | Ok entry ->
            Assert.Equal(firstVoidedAt, entry.voidedAt)
            Assert.Equal(firstModifiedAt, entry.modifiedAt)
            Assert.Equal(Some "First void", entry.voidReason)
        | Error errs -> Assert.Fail(sprintf "Second void failed unexpectedly: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-VJE-005")>]
let ``empty void reason rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (entryId, _, _, _) = postSetupEntry conn tracker prefix
        let voidCmd = { journalEntryId = entryId; voidReason = "" }
        let result = JournalEntryService.voidEntry voidCmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for empty void reason")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("reason")),
                        sprintf "Expected error containing 'reason': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-VJE-006")>]
let ``whitespace-only void reason rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (entryId, _, _, _) = postSetupEntry conn tracker prefix
        let voidCmd = { journalEntryId = entryId; voidReason = "   " }
        let result = JournalEntryService.voidEntry voidCmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for whitespace-only void reason")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("reason")),
                        sprintf "Expected error containing 'reason': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-VJE-007")>]
let ``nonexistent entry ID rejected`` () =
    let voidCmd = { journalEntryId = 999999; voidReason = "Should not exist" }
    let result = JournalEntryService.voidEntry voidCmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent entry ID")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                    sprintf "Expected error containing 'does not exist': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-VJE-008")>]
let ``void succeeds in closed fiscal period`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (entryId, fpId, _, _) = postSetupEntry conn tracker prefix
        // Close the fiscal period after posting
        use closeCmd = new NpgsqlCommand("UPDATE ledger.fiscal_period SET is_open = false WHERE id = @id", conn)
        closeCmd.Parameters.AddWithValue("@id", fpId) |> ignore
        closeCmd.ExecuteNonQuery() |> ignore
        let voidCmd = { journalEntryId = entryId; voidReason = "Void in closed period" }
        let result = JournalEntryService.voidEntry voidCmd
        match result with
        | Ok entry ->
            Assert.True(entry.voidedAt.IsSome, "voidedAt should be Some")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker
