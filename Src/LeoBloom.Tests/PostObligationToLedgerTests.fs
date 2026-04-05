module LeoBloom.Tests.PostObligationToLedgerTests

open System
open Xunit
open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Local helpers — set up the data constellations these tests need
// =====================================================================

/// Insert an obligation agreement with source_account_id and/or dest_account_id set.
let private insertAgreementWithAccounts
    (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker)
    (name: string) (obligationType: string)
    (sourceAccountId: int option) (destAccountId: int option) : int =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, source_account_id, dest_account_id) \
         VALUES (@n, @ot, 'monthly', @sa, @da) RETURNING id",
        conn)
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.Parameters.AddWithValue("@ot", obligationType) |> ignore
    match sourceAccountId with
    | Some id -> cmd.Parameters.AddWithValue("@sa", id) |> ignore
    | None -> cmd.Parameters.AddWithValue("@sa", DBNull.Value) |> ignore
    match destAccountId with
    | Some id -> cmd.Parameters.AddWithValue("@da", id) |> ignore
    | None -> cmd.Parameters.AddWithValue("@da", DBNull.Value) |> ignore
    let id = cmd.ExecuteScalar() :?> int
    TestCleanup.trackObligationAgreement id tracker
    id

/// Create account types and accounts for a receivable: asset (dest) + revenue (source).
/// Returns (sourceAccountId, destAccountId).
let private setupReceivableAccounts (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (prefix: string) =
    let revenueTypeId = InsertHelpers.insertAccountType conn tracker $"{prefix}_rev" "credit"
    let assetTypeId = InsertHelpers.insertAccountType conn tracker $"{prefix}_ast" "debit"
    let sourceAcctId = InsertHelpers.insertAccount conn tracker $"{prefix}SR" $"{prefix}_revenue" revenueTypeId true
    let destAcctId = InsertHelpers.insertAccount conn tracker $"{prefix}DS" $"{prefix}_asset" assetTypeId true
    (sourceAcctId, destAcctId)

/// Create account types and accounts for a payable: asset (source) + expense (dest).
/// Returns (sourceAccountId, destAccountId).
let private setupPayableAccounts (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (prefix: string) =
    let assetTypeId = InsertHelpers.insertAccountType conn tracker $"{prefix}_ast" "debit"
    let expenseTypeId = InsertHelpers.insertAccountType conn tracker $"{prefix}_exp" "debit"
    let sourceAcctId = InsertHelpers.insertAccount conn tracker $"{prefix}SR" $"{prefix}_asset" assetTypeId true
    let destAcctId = InsertHelpers.insertAccount conn tracker $"{prefix}DS" $"{prefix}_expense" expenseTypeId true
    (sourceAcctId, destAcctId)

/// Create a confirmed instance by inserting in expected status and transitioning.
/// Returns the instance ID.
let private createConfirmedInstance
    (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker)
    (agreementId: int) (instanceName: string) (amount: decimal) (confirmedDate: DateOnly) : int =
    let instanceId =
        InsertHelpers.insertObligationInstance conn tracker agreementId instanceName true
    let transCmd : TransitionCommand =
        { instanceId = instanceId
          targetStatus = InstanceStatus.Confirmed
          amount = Some amount
          confirmedDate = Some confirmedDate
          journalEntryId = None
          notes = None }
    match ObligationInstanceService.transition transCmd with
    | Ok _ -> instanceId
    | Error errs -> failwithf "Failed to transition instance to confirmed: %A" errs

/// Query journal entry lines by journal_entry_id.
let private queryJournalEntryLines (conn: NpgsqlConnection) (jeId: int) =
    use cmd = new NpgsqlCommand(
        "SELECT id, journal_entry_id, account_id, amount, entry_type, memo \
         FROM ledger.journal_entry_line WHERE journal_entry_id = @jeid ORDER BY id",
        conn)
    cmd.Parameters.AddWithValue("@jeid", jeId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable lines = []
    while reader.Read() do
        lines <- lines @ [
            {| id = reader.GetInt32(0)
               journalEntryId = reader.GetInt32(1)
               accountId = reader.GetInt32(2)
               amount = reader.GetDecimal(3)
               entryType = reader.GetString(4)
               memo = if reader.IsDBNull(5) then None else Some (reader.GetString(5)) |}
        ]
    lines

/// Query journal entry by id.
let private queryJournalEntry (conn: NpgsqlConnection) (jeId: int) =
    use cmd = new NpgsqlCommand(
        "SELECT id, entry_date, description, source, fiscal_period_id \
         FROM ledger.journal_entry WHERE id = @id",
        conn)
    cmd.Parameters.AddWithValue("@id", jeId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        Some {| id = reader.GetInt32(0)
                entryDate = reader.GetFieldValue<DateOnly>(1)
                description = reader.GetString(2)
                source = if reader.IsDBNull(3) then None else Some (reader.GetString(3))
                fiscalPeriodId = reader.GetInt32(4) |}
    else None

/// Query journal entry references by journal_entry_id.
let private queryJournalEntryReferences (conn: NpgsqlConnection) (jeId: int) =
    use cmd = new NpgsqlCommand(
        "SELECT id, journal_entry_id, reference_type, reference_value \
         FROM ledger.journal_entry_reference WHERE journal_entry_id = @jeid",
        conn)
    cmd.Parameters.AddWithValue("@jeid", jeId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable refs = []
    while reader.Read() do
        refs <- refs @ [
            {| id = reader.GetInt32(0)
               journalEntryId = reader.GetInt32(1)
               referenceType = reader.GetString(2)
               referenceValue = reader.GetString(3) |}
        ]
    refs

/// Query obligation instance by id.
let private queryInstance (conn: NpgsqlConnection) (instanceId: int) =
    use cmd = new NpgsqlCommand(
        "SELECT id, status, journal_entry_id FROM ops.obligation_instance WHERE id = @id",
        conn)
    cmd.Parameters.AddWithValue("@id", instanceId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        Some {| id = reader.GetInt32(0)
                status = reader.GetString(1)
                journalEntryId = if reader.IsDBNull(2) then None else Some (reader.GetInt32(2)) |}
    else None

// =====================================================================
// Happy Path: Receivable — @FT-POL-001
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-001")>]
let ``posting a confirmed receivable instance creates correct journal entry and transitions to posted`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupReceivableAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" (Some sourceAcctId) (Some destAcctId)
        let instanceId = createConfirmedInstance conn tracker agrId $"{prefix}_inst" 1200m (DateOnly(2026, 4, 15))

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            // Assert journal entry has 2 lines
            let lines = queryJournalEntryLines conn posted.journalEntryId
            Assert.Equal(2, lines.Length)
            // Assert debit line is dest account
            let debitLine = lines |> List.find (fun l -> l.entryType = "debit")
            Assert.Equal(destAcctId, debitLine.accountId)
            Assert.Equal(1200m, debitLine.amount)
            // Assert credit line is source account
            let creditLine = lines |> List.find (fun l -> l.entryType = "credit")
            Assert.Equal(sourceAcctId, creditLine.accountId)
            Assert.Equal(1200m, creditLine.amount)
            // Assert instance status is posted
            let inst = queryInstance conn instanceId
            Assert.True(inst.IsSome)
            Assert.Equal("posted", inst.Value.status)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Happy Path: Payable — @FT-POL-002
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-002")>]
let ``posting a confirmed payable instance creates correct journal entry and transitions to posted`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupPayableAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "payable" (Some sourceAcctId) (Some destAcctId)
        let instanceId = createConfirmedInstance conn tracker agrId $"{prefix}_inst" 850m (DateOnly(2026, 4, 15))

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            let lines = queryJournalEntryLines conn posted.journalEntryId
            Assert.Equal(2, lines.Length)
            let debitLine = lines |> List.find (fun l -> l.entryType = "debit")
            Assert.Equal(destAcctId, debitLine.accountId)
            Assert.Equal(850m, debitLine.amount)
            let creditLine = lines |> List.find (fun l -> l.entryType = "credit")
            Assert.Equal(sourceAcctId, creditLine.accountId)
            Assert.Equal(850m, creditLine.amount)
            let inst = queryInstance conn instanceId
            Assert.True(inst.IsSome)
            Assert.Equal("posted", inst.Value.status)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Journal Entry Metadata — @FT-POL-003
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-003")>]
let ``journal entry description matches agreement and instance names`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupReceivableAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let agrName = $"{prefix} Brian Rent"
        let instName = $"{prefix} April 2026"
        let agrId = insertAgreementWithAccounts conn tracker agrName "receivable" (Some sourceAcctId) (Some destAcctId)
        let instanceId = createConfirmedInstance conn tracker agrId instName 1000m (DateOnly(2026, 4, 15))

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            let je = queryJournalEntry conn posted.journalEntryId
            Assert.True(je.IsSome)
            let expectedDesc = sprintf "%s \u2014 %s" agrName instName
            Assert.Equal(expectedDesc, je.Value.description)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Journal Entry Source and Reference — @FT-POL-004
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-004")>]
let ``journal entry has obligation source and reference`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupReceivableAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" (Some sourceAcctId) (Some destAcctId)
        let instanceId = createConfirmedInstance conn tracker agrId $"{prefix}_inst" 1000m (DateOnly(2026, 4, 15))

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            // Assert source = "obligation"
            let je = queryJournalEntry conn posted.journalEntryId
            Assert.True(je.IsSome)
            Assert.Equal(Some "obligation", je.Value.source)
            // Assert reference
            let refs = queryJournalEntryReferences conn posted.journalEntryId
            Assert.True(refs.Length >= 1, sprintf "Expected at least 1 reference, got %d" refs.Length)
            let oblRef = refs |> List.find (fun r -> r.referenceType = "obligation")
            Assert.Equal(string instanceId, oblRef.referenceValue)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Entry Date — @FT-POL-005
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-005")>]
let ``journal entry entry_date equals the instance confirmed_date`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupReceivableAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" (Some sourceAcctId) (Some destAcctId)
        let confirmedDate = DateOnly(2026, 4, 20)
        let instanceId = createConfirmedInstance conn tracker agrId $"{prefix}_inst" 500m confirmedDate

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            let je = queryJournalEntry conn posted.journalEntryId
            Assert.True(je.IsSome)
            Assert.Equal(confirmedDate, je.Value.entryDate)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Instance journal_entry_id — @FT-POL-006
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-006")>]
let ``instance journal_entry_id is set after posting`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupReceivableAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" (Some sourceAcctId) (Some destAcctId)
        let instanceId = createConfirmedInstance conn tracker agrId $"{prefix}_inst" 1000m (DateOnly(2026, 4, 15))

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            let inst = queryInstance conn instanceId
            Assert.True(inst.IsSome)
            Assert.Equal(Some posted.journalEntryId, inst.Value.journalEntryId)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Error: Instance not confirmed — @FT-POL-007
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-007")>]
let ``posting an instance not in confirmed status returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupReceivableAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" (Some sourceAcctId) (Some destAcctId)
        // Leave instance in expected status — do not transition
        let instanceId =
            InsertHelpers.insertObligationInstance conn tracker agrId $"{prefix}_inst" true

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            Assert.Fail("Expected Error for non-confirmed instance")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("confirmed")),
                        sprintf "Expected error containing 'confirmed': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Error: No amount — @FT-POL-008
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-008")>]
let ``posting an instance with no amount returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupReceivableAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" (Some sourceAcctId) (Some destAcctId)
        // Insert instance as confirmed but with no amount — use raw SQL to bypass transition validation
        let instanceId =
            InsertHelpers.insertObligationInstanceFull
                conn tracker agrId $"{prefix}_inst" "confirmed"
                (DateOnly(2026, 4, 1)) None None true

        // Set confirmed_date directly since insertObligationInstanceFull doesn't set it
        use updCmd = new NpgsqlCommand(
            "UPDATE ops.obligation_instance SET confirmed_date = @cd WHERE id = @id", conn)
        updCmd.Parameters.AddWithValue("@cd", DateOnly(2026, 4, 15)) |> ignore
        updCmd.Parameters.AddWithValue("@id", instanceId) |> ignore
        updCmd.ExecuteNonQuery() |> ignore

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            Assert.Fail("Expected Error for missing amount")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("amount")),
                        sprintf "Expected error containing 'amount': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Error: No confirmed_date — @FT-POL-009
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-009")>]
let ``posting an instance with no confirmed_date returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupReceivableAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" (Some sourceAcctId) (Some destAcctId)
        // Insert instance as confirmed with amount but no confirmed_date
        let instanceId =
            InsertHelpers.insertObligationInstanceFull
                conn tracker agrId $"{prefix}_inst" "confirmed"
                (DateOnly(2026, 4, 1)) (Some 1000m) None true
        // Do NOT set confirmed_date — leave it null

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            Assert.Fail("Expected Error for missing confirmed_date")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("confirmed_date")),
                        sprintf "Expected error containing 'confirmed_date': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Error: No source_account_id — @FT-POL-010
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-010")>]
let ``posting when agreement has no source_account_id returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, destAcctId) = setupReceivableAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        // No source account on agreement
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" None (Some destAcctId)
        let instanceId = createConfirmedInstance conn tracker agrId $"{prefix}_inst" 1000m (DateOnly(2026, 4, 15))

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            Assert.Fail("Expected Error for missing source_account_id")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("source_account")),
                        sprintf "Expected error containing 'source_account': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Error: No dest_account_id — @FT-POL-011
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-011")>]
let ``posting when agreement has no dest_account_id returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, _) = setupReceivableAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        // No dest account on agreement
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" (Some sourceAcctId) None
        let instanceId = createConfirmedInstance conn tracker agrId $"{prefix}_inst" 1000m (DateOnly(2026, 4, 15))

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            Assert.Fail("Expected Error for missing dest_account_id")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("dest_account")),
                        sprintf "Expected error containing 'dest_account': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Error: No fiscal period — @FT-POL-012
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-012")>]
let ``posting when no fiscal period covers confirmed_date returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupReceivableAccounts conn tracker prefix
        // No fiscal period covering July 2026
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" (Some sourceAcctId) (Some destAcctId)
        let instanceId = createConfirmedInstance conn tracker agrId $"{prefix}_inst" 1000m (DateOnly(2026, 7, 15))

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            Assert.Fail("Expected Error for missing fiscal period")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("fiscal period")),
                        sprintf "Expected error containing 'fiscal period': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Error: Closed fiscal period — @FT-POL-013
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-013")>]
let ``posting when fiscal period is closed returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupReceivableAccounts conn tracker prefix
        // Closed fiscal period
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) false
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" (Some sourceAcctId) (Some destAcctId)
        let instanceId = createConfirmedInstance conn tracker agrId $"{prefix}_inst" 1000m (DateOnly(2026, 4, 15))

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            Assert.Fail("Expected Error for closed fiscal period")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("not open")),
                        sprintf "Expected error containing 'not open': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Double-posting prevention — @FT-POL-016 (REM-001)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-016")>]
let ``posting an already-posted instance is rejected with no duplicate journal entry`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupReceivableAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" (Some sourceAcctId) (Some destAcctId)
        let instanceId = createConfirmedInstance conn tracker agrId $"{prefix}_inst" 1000m (DateOnly(2026, 4, 15))

        // First post — should succeed
        let firstResult = ObligationPostingService.postToLedger { instanceId = instanceId }
        match firstResult with
        | Error errs -> Assert.Fail(sprintf "First post expected to succeed: %A" errs)
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            let originalJeId = posted.journalEntryId

            // Second post — should fail
            let secondResult = ObligationPostingService.postToLedger { instanceId = instanceId }
            match secondResult with
            | Ok _ -> Assert.Fail("Expected Error for double-posting")
            | Error errs ->
                Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("confirmed")),
                            sprintf "Expected error containing 'confirmed': %A" errs)

            // Verify no second journal entry: instance still points to original
            let inst = queryInstance conn instanceId
            Assert.True(inst.IsSome)
            Assert.Equal(Some originalJeId, inst.Value.journalEntryId)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Atomicity of failed posting — @FT-POL-017 (REM-007)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-017")>]
let ``failed post to closed period leaves instance in confirmed status with no journal entry`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (sourceAcctId, destAcctId) = setupReceivableAccounts conn tracker prefix
        // Closed fiscal period
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) false
        let agrId = insertAgreementWithAccounts conn tracker $"{prefix}_agr" "receivable" (Some sourceAcctId) (Some destAcctId)
        let instanceId = createConfirmedInstance conn tracker agrId $"{prefix}_inst" 1000m (DateOnly(2026, 4, 15))

        let result = ObligationPostingService.postToLedger { instanceId = instanceId }
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.journalEntryId tracker
            Assert.Fail("Expected Error for closed fiscal period")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("not open")),
                        sprintf "Expected error containing 'not open': %A" errs)

        // Verify instance is still confirmed with no journal entry
        let inst = queryInstance conn instanceId
        Assert.True(inst.IsSome)
        Assert.Equal("confirmed", inst.Value.status)
        Assert.True(inst.Value.journalEntryId.IsNone, "Expected journal_entry_id to remain null")
    finally TestCleanup.deleteAll tracker

// =====================================================================
// FiscalPeriodRepository.findByDate — @FT-POL-014
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-014")>]
let ``findByDate returns correct period for a date within range`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let startDate = DateOnly(2026, 5, 1)
        let endDate = DateOnly(2026, 5, 31)
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" startDate endDate true

        use txn = conn.BeginTransaction()
        let result = FiscalPeriodRepository.findByDate txn (DateOnly(2026, 5, 15))
        txn.Commit()

        Assert.True(result.IsSome, "Expected a fiscal period to be found")
        Assert.Equal(fpId, result.Value.id)
        Assert.Equal(startDate, result.Value.startDate)
        Assert.Equal(endDate, result.Value.endDate)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// FiscalPeriodRepository.findByDate — @FT-POL-015
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-POL-015")>]
let ``findByDate returns None for a date outside all periods`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2026, 5, 1)) (DateOnly(2026, 5, 31)) true

        use txn = conn.BeginTransaction()
        let result = FiscalPeriodRepository.findByDate txn (DateOnly(2026, 8, 15))
        txn.Commit()

        Assert.True(result.IsNone, "Expected no fiscal period for date outside range")
    finally TestCleanup.deleteAll tracker
