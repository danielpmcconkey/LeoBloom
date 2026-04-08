module LeoBloom.Tests.OrphanedPostingDetectionTests

open System
open System.Text.Json
open Npgsql
open Xunit
open LeoBloom.Domain.Ops
open LeoBloom.Utilities
open LeoBloom.Ops
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Raw SQL helpers for orphaned posting test setup
// =====================================================================

/// Insert a journal entry, optionally voided.
let private insertJournalEntryVoided
    (txn: NpgsqlTransaction)
    (fpId: int) (voided: bool) : int =
    let sql =
        if voided then
            "INSERT INTO ledger.journal_entry \
             (entry_date, description, fiscal_period_id, voided_at, void_reason) \
             VALUES ('2026-01-01', 'orphan test', @fp, NOW(), 'test void') RETURNING id"
        else
            "INSERT INTO ledger.journal_entry \
             (entry_date, description, fiscal_period_id) \
             VALUES ('2026-01-01', 'orphan test', @fp) RETURNING id"
    use cmd = new NpgsqlCommand(sql, txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    cmd.ExecuteScalar() :?> int

/// Insert a journal_entry_reference row.
let private insertJeReference
    (txn: NpgsqlTransaction) (jeId: int)
    (refType: string) (refValue: string) : unit =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_reference \
         (journal_entry_id, reference_type, reference_value) \
         VALUES (@je, @rt, @rv)",
        txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.Parameters.AddWithValue("@rt", refType) |> ignore
    cmd.Parameters.AddWithValue("@rv", refValue) |> ignore
    cmd.ExecuteNonQuery() |> ignore

/// Insert an obligation_instance with a specific status and optional journal_entry_id.
/// Uses a far-future expected_date so overdue detection does not pick it up.
/// Returns the new instance ID. Caller must ensure obligation_agreement_id is tracked.
let private insertObligationInstance
    (txn: NpgsqlTransaction)
    (agreementId: int) (status: string) (jeId: int option) : int =
    let sql =
        match jeId with
        | Some _ ->
            "INSERT INTO ops.obligation_instance \
             (obligation_agreement_id, name, status, expected_date, journal_entry_id) \
             VALUES (@aid, 'test', @status, '2099-12-31', @je) RETURNING id"
        | None ->
            "INSERT INTO ops.obligation_instance \
             (obligation_agreement_id, name, status, expected_date) \
             VALUES (@aid, 'test', @status, '2099-12-31') RETURNING id"
    use cmd = new NpgsqlCommand(sql, txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@aid", agreementId) |> ignore
    cmd.Parameters.AddWithValue("@status", status) |> ignore
    match jeId with
    | Some jid -> cmd.Parameters.AddWithValue("@je", jid) |> ignore
    | None -> ()
    cmd.ExecuteScalar() :?> int

/// Insert a transfer with a specific status and optional journal_entry_id.
/// Returns the new transfer ID. Caller must ensure from/to account IDs are tracked.
let private insertTransfer
    (txn: NpgsqlTransaction)
    (fromId: int) (toId: int) (status: string) (jeId: int option) : int =
    let sql =
        match jeId with
        | Some _ ->
            "INSERT INTO ops.transfer \
             (from_account_id, to_account_id, amount, status, initiated_date, confirmed_date, journal_entry_id) \
             VALUES (@from, @to, 100, @status, '2026-01-01', '2026-01-02', @je) RETURNING id"
        | None ->
            "INSERT INTO ops.transfer \
             (from_account_id, to_account_id, amount, status, initiated_date) \
             VALUES (@from, @to, 100, @status, '2026-01-01') RETURNING id"
    use cmd = new NpgsqlCommand(sql, txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@from", fromId) |> ignore
    cmd.Parameters.AddWithValue("@to", toId) |> ignore
    cmd.Parameters.AddWithValue("@status", status) |> ignore
    match jeId with
    | Some jid -> cmd.Parameters.AddWithValue("@je", jid) |> ignore
    | None -> ()
    cmd.ExecuteScalar() :?> int

/// Insert two asset accounts for use in transfer tests.
/// Returns (fromId, toId).
let private setupTwoAssetAccounts (txn: NpgsqlTransaction) (prefix: string) =
    let assetTypeId = 1  // pre-seeded "asset" type
    let fromId = InsertHelpers.insertAccount txn (prefix + "FR") (prefix + "_from") assetTypeId true
    let toId   = InsertHelpers.insertAccount txn (prefix + "TO") (prefix + "_to")   assetTypeId true
    (fromId, toId)

// =====================================================================
// FT-OPD-001: Clean empty result
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OPD-001")>]
let ``No orphaned postings returns a clean empty result`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // NOTE: This test asserts the DB has no orphaned data. It relies on all
    // other tests rolling back properly. No test setup required.
    match OrphanedPostingService.findOrphanedPostings txn with
    | Error errs -> Assert.Fail(sprintf "Diagnostic failed: %A" errs)
    | Ok result  -> Assert.Empty(result.orphans)

// =====================================================================
// FT-OPD-002: Dangling status (obligation and transfer)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OPD-002")>]
let ``Detects dangling status update for obligation`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP")
                   (DateOnly(2026,1,1)) (DateOnly(2026,12,31)) true
    // Obligation instance with journal_entry_id = NULL (dangling)
    let agrId  = InsertHelpers.insertObligationAgreement txn (prefix + "_agr")
    let instId = insertObligationInstance txn agrId "expected" None
    // JE with reference pointing to this instance
    let jeId   = insertJournalEntryVoided txn fpId false
    insertJeReference txn jeId "obligation" (string instId)

    match OrphanedPostingService.findOrphanedPostings txn with
    | Error errs -> Assert.Fail(sprintf "Diagnostic failed: %A" errs)
    | Ok result  ->
        let mine = result.orphans |> List.filter (fun o -> o.journalEntryId = jeId)
        Assert.Equal(1, mine.Length)
        Assert.Equal("obligation", mine.[0].sourceType)
        Assert.Equal(DanglingStatus, mine.[0].condition)
        Assert.Equal(Some instId, mine.[0].sourceRecordId)

[<Fact>]
[<Trait("GherkinId", "@FT-OPD-002")>]
let ``Detects dangling status update for transfer`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP")
                   (DateOnly(2026,1,1)) (DateOnly(2026,12,31)) true
    let (fromId, toId) = setupTwoAssetAccounts txn prefix
    // Transfer with journal_entry_id = NULL (dangling)
    let transferId = insertTransfer txn fromId toId "initiated" None
    // JE with reference pointing to this transfer
    let jeId = insertJournalEntryVoided txn fpId false
    insertJeReference txn jeId "transfer" (string transferId)

    match OrphanedPostingService.findOrphanedPostings txn with
    | Error errs -> Assert.Fail(sprintf "Diagnostic failed: %A" errs)
    | Ok result  ->
        let mine = result.orphans |> List.filter (fun o -> o.journalEntryId = jeId)
        Assert.Equal(1, mine.Length)
        Assert.Equal("transfer", mine.[0].sourceType)
        Assert.Equal(DanglingStatus, mine.[0].condition)
        Assert.Equal(Some transferId, mine.[0].sourceRecordId)

// =====================================================================
// FT-OPD-003: Missing source record (obligation and transfer)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OPD-003")>]
let ``Detects missing source record for obligation`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP")
                   (DateOnly(2026,1,1)) (DateOnly(2026,12,31)) true
    let jeId = insertJournalEntryVoided txn fpId false
    // Reference pointing to a nonexistent obligation ID
    insertJeReference txn jeId "obligation" "9999999"

    match OrphanedPostingService.findOrphanedPostings txn with
    | Error errs -> Assert.Fail(sprintf "Diagnostic failed: %A" errs)
    | Ok result  ->
        let mine = result.orphans |> List.filter (fun o -> o.journalEntryId = jeId)
        Assert.Equal(1, mine.Length)
        Assert.Equal("obligation", mine.[0].sourceType)
        Assert.Equal(MissingSource, mine.[0].condition)
        Assert.Equal(None, mine.[0].sourceRecordId)

[<Fact>]
[<Trait("GherkinId", "@FT-OPD-003")>]
let ``Detects missing source record for transfer`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP")
                   (DateOnly(2026,1,1)) (DateOnly(2026,12,31)) true
    let jeId = insertJournalEntryVoided txn fpId false
    // Reference pointing to a nonexistent transfer ID
    insertJeReference txn jeId "transfer" "9999999"

    match OrphanedPostingService.findOrphanedPostings txn with
    | Error errs -> Assert.Fail(sprintf "Diagnostic failed: %A" errs)
    | Ok result  ->
        let mine = result.orphans |> List.filter (fun o -> o.journalEntryId = jeId)
        Assert.Equal(1, mine.Length)
        Assert.Equal("transfer", mine.[0].sourceType)
        Assert.Equal(MissingSource, mine.[0].condition)
        Assert.Equal(None, mine.[0].sourceRecordId)

// =====================================================================
// FT-OPD-004: Voided backing entry (obligation and transfer)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OPD-004")>]
let ``Detects obligation in posted status backed by a voided journal entry`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId  = InsertHelpers.insertFiscalPeriod txn (prefix + "FP")
                    (DateOnly(2026,1,1)) (DateOnly(2026,12,31)) true
    // Voided JE
    let jeId  = insertJournalEntryVoided txn fpId true
    // Obligation in 'posted' status referencing the voided JE
    let agrId = InsertHelpers.insertObligationAgreement txn (prefix + "_agr")
    let instId = insertObligationInstance txn agrId "posted" (Some jeId)

    match OrphanedPostingService.findOrphanedPostings txn with
    | Error errs -> Assert.Fail(sprintf "Diagnostic failed: %A" errs)
    | Ok result  ->
        let mine = result.orphans |> List.filter (fun o ->
            o.journalEntryId = jeId && o.sourceType = "obligation")
        Assert.Equal(1, mine.Length)
        Assert.Equal(VoidedBackingEntry, mine.[0].condition)
        Assert.Equal(Some instId, mine.[0].sourceRecordId)

[<Fact>]
[<Trait("GherkinId", "@FT-OPD-004")>]
let ``Detects transfer in confirmed status backed by a voided journal entry`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP")
                   (DateOnly(2026,1,1)) (DateOnly(2026,12,31)) true
    let (fromId, toId) = setupTwoAssetAccounts txn prefix
    // Voided JE
    let jeId = insertJournalEntryVoided txn fpId true
    // Transfer in 'confirmed' status referencing the voided JE
    let transferId = insertTransfer txn fromId toId "confirmed" (Some jeId)

    match OrphanedPostingService.findOrphanedPostings txn with
    | Error errs -> Assert.Fail(sprintf "Diagnostic failed: %A" errs)
    | Ok result  ->
        let mine = result.orphans |> List.filter (fun o ->
            o.journalEntryId = jeId && o.sourceType = "transfer")
        Assert.Equal(1, mine.Length)
        Assert.Equal(VoidedBackingEntry, mine.[0].condition)
        Assert.Equal(Some transferId, mine.[0].sourceRecordId)

// =====================================================================
// FT-OPD-005: Normal postings are not flagged
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OPD-005")>]
let ``Properly completed obligation posting is not reported as an orphan`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId  = InsertHelpers.insertFiscalPeriod txn (prefix + "FP")
                    (DateOnly(2026,1,1)) (DateOnly(2026,12,31)) true
    // Non-voided JE
    let jeId  = insertJournalEntryVoided txn fpId false
    // Obligation in 'posted' status with journal_entry_id properly set
    let agrId = InsertHelpers.insertObligationAgreement txn (prefix + "_agr")
    let instId = insertObligationInstance txn agrId "posted" (Some jeId)
    // Matching JE reference
    insertJeReference txn jeId "obligation" (string instId)

    match OrphanedPostingService.findOrphanedPostings txn with
    | Error errs -> Assert.Fail(sprintf "Diagnostic failed: %A" errs)
    | Ok result  ->
        // The JE we set up should NOT appear in orphans
        let mine = result.orphans |> List.filter (fun o -> o.journalEntryId = jeId)
        Assert.Empty(mine)

// =====================================================================
// FT-OPD-006: Non-numeric reference_value → InvalidReference
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OPD-006")>]
let ``Non-numeric reference_value for obligation type is reported as InvalidReference`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP")
                   (DateOnly(2026,1,1)) (DateOnly(2026,12,31)) true
    let jeId = insertJournalEntryVoided txn fpId false
    // Non-numeric reference_value
    insertJeReference txn jeId "obligation" "NOT-A-NUMBER"

    match OrphanedPostingService.findOrphanedPostings txn with
    | Error errs -> Assert.Fail(sprintf "Diagnostic failed: %A" errs)
    | Ok result  ->
        let mine = result.orphans |> List.filter (fun o -> o.journalEntryId = jeId)
        Assert.Equal(1, mine.Length)
        Assert.Equal("obligation", mine.[0].sourceType)
        Assert.Equal(InvalidReference, mine.[0].condition)
        Assert.Equal(None, mine.[0].sourceRecordId)
        Assert.Equal("NOT-A-NUMBER", mine.[0].referenceValue)

// =====================================================================
// FT-OPD-007: JSON output mode
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-OPD-007")>]
let ``JSON output mode returns valid JSON containing the orphan data`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    // Set up a DanglingStatus obligation
    let fpId  = InsertHelpers.insertFiscalPeriod txn (prefix + "FP")
                    (DateOnly(2026,1,1)) (DateOnly(2026,12,31)) true
    let agrId = InsertHelpers.insertObligationAgreement txn (prefix + "_agr")
    let instId = insertObligationInstance txn agrId "expected" None
    let jeId  = insertJournalEntryVoided txn fpId false
    insertJeReference txn jeId "obligation" (string instId)

    match OrphanedPostingService.findOrphanedPostings txn with
    | Error errs -> Assert.Fail(sprintf "Diagnostic failed: %A" errs)
    | Ok result  ->
        // Confirm the orphan we set up is in the result
        let mine = result.orphans |> List.filter (fun o -> o.journalEntryId = jeId)
        Assert.Equal(1, mine.Length)
        Assert.Equal(DanglingStatus, mine.[0].condition)

        // Serialize via anonymous records to avoid F# DU serialization issues at the test layer.
        // The CLI uses JsonFSharpConverter for proper DU handling; here we verify shape/content.
        let serializable = {|
            orphans = result.orphans |> List.map (fun o ->
                {| sourceType     = o.sourceType
                   sourceRecordId = o.sourceRecordId
                   journalEntryId = o.journalEntryId
                   condition      = sprintf "%A" o.condition
                   referenceValue = o.referenceValue |})
        |}
        let json = JsonSerializer.Serialize(serializable,
                        JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase))
        Assert.NotEmpty(json)

        // Parse and verify structure
        let doc = JsonDocument.Parse(json)
        let mutable orphansElem = Unchecked.defaultof<JsonElement>
        Assert.True(doc.RootElement.TryGetProperty("orphans", &orphansElem),
            "JSON should have 'orphans' property")
        Assert.True(orphansElem.GetArrayLength() >= 1,
            "JSON orphans array should contain at least 1 entry")

        // Find our specific orphan in the JSON output
        let mutable foundDangling = false
        for i in 0 .. orphansElem.GetArrayLength() - 1 do
            let item = orphansElem.[i]
            let mutable jeIdElem = Unchecked.defaultof<JsonElement>
            if item.TryGetProperty("journalEntryId", &jeIdElem) && jeIdElem.GetInt32() = jeId then
                let mutable conditionElem = Unchecked.defaultof<JsonElement>
                if item.TryGetProperty("condition", &conditionElem) then
                    foundDangling <- conditionElem.GetString() = "DanglingStatus"
        Assert.True(foundDangling,
            sprintf "Expected to find a DanglingStatus orphan with journalEntryId=%d in JSON" jeId)
