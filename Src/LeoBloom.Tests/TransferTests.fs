module LeoBloom.Tests.TransferTests

open System
open Xunit
open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Local helpers
// =====================================================================

/// Look up the pre-existing "asset" account type (id=1) and create two active asset accounts.
/// Returns (fromAccountId, toAccountId).
let private setupTwoAssetAccounts (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (prefix: string) =
    let assetTypeId = 1  // pre-seeded "asset" type — name must be exactly "asset" for transfer validation
    let fromId = InsertHelpers.insertAccount conn tracker $"{prefix}FR" $"{prefix}_from" assetTypeId true
    let toId = InsertHelpers.insertAccount conn tracker $"{prefix}TO" $"{prefix}_to" assetTypeId true
    (fromId, toId)

/// Query a transfer by id directly from the DB.
let private queryTransfer (conn: NpgsqlConnection) (transferId: int) =
    use cmd = new NpgsqlCommand(
        "SELECT id, from_account_id, to_account_id, amount, status, initiated_date, \
         confirmed_date, journal_entry_id, description, is_active \
         FROM ops.transfer WHERE id = @id",
        conn)
    cmd.Parameters.AddWithValue("@id", transferId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        Some {| id = reader.GetInt32(0)
                fromAccountId = reader.GetInt32(1)
                toAccountId = reader.GetInt32(2)
                amount = reader.GetDecimal(3)
                status = reader.GetString(4)
                initiatedDate = reader.GetFieldValue<DateOnly>(5)
                confirmedDate = if reader.IsDBNull(6) then None else Some (reader.GetFieldValue<DateOnly>(6))
                journalEntryId = if reader.IsDBNull(7) then None else Some (reader.GetInt32(7))
                description = if reader.IsDBNull(8) then None else Some (reader.GetString(8))
                isActive = reader.GetBoolean(9) |}
    else None

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

/// Query journal entry lines by journal_entry_id.
let private queryJournalEntryLines (conn: NpgsqlConnection) (jeId: int) =
    use cmd = new NpgsqlCommand(
        "SELECT account_id, amount, entry_type \
         FROM ledger.journal_entry_line WHERE journal_entry_id = @jeid ORDER BY id",
        conn)
    cmd.Parameters.AddWithValue("@jeid", jeId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable lines = []
    while reader.Read() do
        lines <- lines @ [
            {| accountId = reader.GetInt32(0)
               amount = reader.GetDecimal(1)
               entryType = reader.GetString(2) |}
        ]
    lines

/// Query journal entry references by journal_entry_id.
let private queryJournalEntryReferences (conn: NpgsqlConnection) (jeId: int) =
    use cmd = new NpgsqlCommand(
        "SELECT reference_type, reference_value \
         FROM ledger.journal_entry_reference WHERE journal_entry_id = @jeid",
        conn)
    cmd.Parameters.AddWithValue("@jeid", jeId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable refs = []
    while reader.Read() do
        refs <- refs @ [
            {| referenceType = reader.GetString(0)
               referenceValue = reader.GetString(1) |}
        ]
    refs

/// Helper: initiate a transfer through the service and return the Transfer.
/// Tracks the journal entry if confirm creates one later.
let private initiateTransfer
    (tracker: TestCleanup.Tracker)
    (fromId: int) (toId: int) (amount: decimal)
    (description: string option) : Transfer =
    let cmd : InitiateTransferCommand =
        { fromAccountId = fromId
          toAccountId = toId
          amount = amount
          initiatedDate = DateOnly(2099, 1, 1)
          expectedSettlement = None
          description = description }
    match TransferService.initiate cmd with
    | Ok t -> t
    | Error errs -> failwithf "Setup failed — could not initiate transfer: %A" errs

// =====================================================================
// Initiate: Happy Path — @FT-TRF-001
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-001")>]
let ``Initiating a transfer between two active asset accounts succeeds`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (fromId, toId) = setupTwoAssetAccounts conn tracker prefix

        let cmd : InitiateTransferCommand =
            { fromAccountId = fromId
              toAccountId = toId
              amount = 500.00m
              initiatedDate = DateOnly(2099, 1, 1)
              expectedSettlement = None
              description = None }

        let result = TransferService.initiate cmd
        match result with
        | Ok transfer ->
            Assert.Equal(fromId, transfer.fromAccountId)
            Assert.Equal(toId, transfer.toAccountId)
            Assert.Equal(500.00m, transfer.amount)
            Assert.Equal(TransferStatus.Initiated, transfer.status)
            Assert.Equal(DateOnly(2099, 1, 1), transfer.initiatedDate)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Initiate: from = to — @FT-TRF-002
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-002")>]
let ``Initiating with from_account equal to to_account returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let assetTypeId = 1  // pre-seeded "asset" type
        let acctId = InsertHelpers.insertAccount conn tracker $"{prefix}CK" $"{prefix}_checking" assetTypeId true

        let cmd : InitiateTransferCommand =
            { fromAccountId = acctId
              toAccountId = acctId
              amount = 500.00m
              initiatedDate = DateOnly(2099, 1, 1)
              expectedSettlement = None
              description = None }

        let result = TransferService.initiate cmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for same account")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("same")),
                        sprintf "Expected error containing 'same': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Initiate: Non-asset account — @FT-TRF-003
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-003")>]
let ``Initiating with a non-asset account type returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let assetTypeId = 1  // pre-seeded "asset" type
        let expenseTypeId = InsertHelpers.insertAccountType conn tracker $"{prefix}_exp" "debit"
        let checkingId = InsertHelpers.insertAccount conn tracker $"{prefix}CK" $"{prefix}_checking" assetTypeId true
        let rentId = InsertHelpers.insertAccount conn tracker $"{prefix}RN" $"{prefix}_rent" expenseTypeId true

        let cmd : InitiateTransferCommand =
            { fromAccountId = checkingId
              toAccountId = rentId
              amount = 500.00m
              initiatedDate = DateOnly(2099, 1, 1)
              expectedSettlement = None
              description = None }

        let result = TransferService.initiate cmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for non-asset account")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("asset")),
                        sprintf "Expected error containing 'asset': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Initiate: Inactive account — @FT-TRF-004
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-004")>]
let ``Initiating with an inactive account returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let assetTypeId = 1  // pre-seeded "asset" type
        let checkingId = InsertHelpers.insertAccount conn tracker $"{prefix}CK" $"{prefix}_checking" assetTypeId true
        let closedId = InsertHelpers.insertAccount conn tracker $"{prefix}CL" $"{prefix}_closed" assetTypeId false

        let cmd : InitiateTransferCommand =
            { fromAccountId = checkingId
              toAccountId = closedId
              amount = 500.00m
              initiatedDate = DateOnly(2099, 1, 1)
              expectedSettlement = None
              description = None }

        let result = TransferService.initiate cmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for inactive account")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("active")),
                        sprintf "Expected error containing 'active': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Initiate: Zero amount — @FT-TRF-005
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-005")>]
let ``Initiating with amount zero returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (fromId, toId) = setupTwoAssetAccounts conn tracker prefix

        let cmd : InitiateTransferCommand =
            { fromAccountId = fromId
              toAccountId = toId
              amount = 0.00m
              initiatedDate = DateOnly(2099, 1, 1)
              expectedSettlement = None
              description = None }

        let result = TransferService.initiate cmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for zero amount")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("amount")),
                        sprintf "Expected error containing 'amount': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Initiate: Negative amount — @FT-TRF-006
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-006")>]
let ``Initiating with negative amount returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (fromId, toId) = setupTwoAssetAccounts conn tracker prefix

        let cmd : InitiateTransferCommand =
            { fromAccountId = fromId
              toAccountId = toId
              amount = -100.00m
              initiatedDate = DateOnly(2099, 1, 1)
              expectedSettlement = None
              description = None }

        let result = TransferService.initiate cmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for negative amount")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("amount")),
                        sprintf "Expected error containing 'amount': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Confirm: Happy Path — @FT-TRF-007
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-007")>]
let ``Confirming an initiated transfer creates correct journal entry and sets status confirmed`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (fromId, toId) = setupTwoAssetAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2099, 3, 1)) (DateOnly(2099, 3, 31)) true
        let transfer = initiateTransfer tracker fromId toId 1000.00m None

        let confirmCmd : ConfirmTransferCommand =
            { transferId = transfer.id
              confirmedDate = DateOnly(2099, 3, 15) }

        let result = TransferService.confirm confirmCmd
        match result with
        | Ok confirmed ->
            // Track the JE for cleanup
            match confirmed.journalEntryId with
            | Some jeId -> TestCleanup.trackJournalEntry jeId tracker
            | None -> ()

            Assert.Equal(TransferStatus.Confirmed, confirmed.status)

            // Verify journal entry lines
            let jeId = confirmed.journalEntryId.Value
            let lines = queryJournalEntryLines conn jeId
            Assert.Equal(2, lines.Length)

            let debitLine = lines |> List.find (fun l -> l.entryType = "debit")
            Assert.Equal(toId, debitLine.accountId)
            Assert.Equal(1000.00m, debitLine.amount)

            let creditLine = lines |> List.find (fun l -> l.entryType = "credit")
            Assert.Equal(fromId, creditLine.accountId)
            Assert.Equal(1000.00m, creditLine.amount)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Confirm: confirmed_date and journal_entry_id — @FT-TRF-008
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-008")>]
let ``Confirming sets confirmed_date and journal_entry_id on the transfer`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (fromId, toId) = setupTwoAssetAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2099, 4, 1)) (DateOnly(2099, 4, 30)) true
        let transfer = initiateTransfer tracker fromId toId 750.00m None

        let confirmCmd : ConfirmTransferCommand =
            { transferId = transfer.id
              confirmedDate = DateOnly(2099, 4, 15) }

        let result = TransferService.confirm confirmCmd
        match result with
        | Ok confirmed ->
            match confirmed.journalEntryId with
            | Some jeId -> TestCleanup.trackJournalEntry jeId tracker
            | None -> ()

            Assert.Equal(Some (DateOnly(2099, 4, 15)), confirmed.confirmedDate)
            Assert.True(confirmed.journalEntryId.IsSome, "Expected journal_entry_id to be set")

            // Cross-check with DB
            let dbTransfer = queryTransfer conn confirmed.id
            Assert.True(dbTransfer.IsSome)
            Assert.Equal(Some (DateOnly(2099, 4, 15)), dbTransfer.Value.confirmedDate)
            Assert.Equal(confirmed.journalEntryId, dbTransfer.Value.journalEntryId)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Confirm: JE source and reference — @FT-TRF-009
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-009")>]
let ``Journal entry has transfer source and reference`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (fromId, toId) = setupTwoAssetAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2099, 5, 1)) (DateOnly(2099, 5, 31)) true
        let transfer = initiateTransfer tracker fromId toId 500.00m None

        let confirmCmd : ConfirmTransferCommand =
            { transferId = transfer.id
              confirmedDate = DateOnly(2099, 5, 15) }

        let result = TransferService.confirm confirmCmd
        match result with
        | Ok confirmed ->
            let jeId = confirmed.journalEntryId.Value
            TestCleanup.trackJournalEntry jeId tracker

            // Assert source = "transfer"
            let je = queryJournalEntry conn jeId
            Assert.True(je.IsSome)
            Assert.Equal(Some "transfer", je.Value.source)

            // Assert reference
            let refs = queryJournalEntryReferences conn jeId
            Assert.True(refs.Length >= 1, sprintf "Expected at least 1 reference, got %d" refs.Length)
            let trfRef = refs |> List.find (fun r -> r.referenceType = "transfer")
            Assert.Equal(string transfer.id, trfRef.referenceValue)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Confirm: JE entry_date = confirmed_date — @FT-TRF-010
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-010")>]
let ``Journal entry entry_date equals confirmed_date`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (fromId, toId) = setupTwoAssetAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2099, 6, 1)) (DateOnly(2099, 6, 30)) true
        let transfer = initiateTransfer tracker fromId toId 500.00m None

        let confirmedDate = DateOnly(2099, 6, 20)
        let confirmCmd : ConfirmTransferCommand =
            { transferId = transfer.id
              confirmedDate = confirmedDate }

        let result = TransferService.confirm confirmCmd
        match result with
        | Ok confirmed ->
            let jeId = confirmed.journalEntryId.Value
            TestCleanup.trackJournalEntry jeId tracker

            let je = queryJournalEntry conn jeId
            Assert.True(je.IsSome)
            Assert.Equal(confirmedDate, je.Value.entryDate)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Confirm: JE uses transfer description — @FT-TRF-011
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-011")>]
let ``Journal entry uses transfer description when present`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (fromId, toId) = setupTwoAssetAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2099, 7, 1)) (DateOnly(2099, 7, 31)) true
        let transfer = initiateTransfer tracker fromId toId 500.00m (Some "Savings top-up")

        let confirmCmd : ConfirmTransferCommand =
            { transferId = transfer.id
              confirmedDate = DateOnly(2099, 7, 15) }

        let result = TransferService.confirm confirmCmd
        match result with
        | Ok confirmed ->
            let jeId = confirmed.journalEntryId.Value
            TestCleanup.trackJournalEntry jeId tracker

            let je = queryJournalEntry conn jeId
            Assert.True(je.IsSome)
            Assert.Equal("Savings top-up", je.Value.description)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Confirm: JE auto-generates description — @FT-TRF-012
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-012")>]
let ``Journal entry auto-generates description when transfer has no description`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (fromId, toId) = setupTwoAssetAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2099, 8, 1)) (DateOnly(2099, 8, 31)) true
        let transfer = initiateTransfer tracker fromId toId 500.00m None

        let confirmCmd : ConfirmTransferCommand =
            { transferId = transfer.id
              confirmedDate = DateOnly(2099, 8, 15) }

        let result = TransferService.confirm confirmCmd
        match result with
        | Ok confirmed ->
            let jeId = confirmed.journalEntryId.Value
            TestCleanup.trackJournalEntry jeId tracker

            let je = queryJournalEntry conn jeId
            Assert.True(je.IsSome)
            Assert.StartsWith("Transfer", je.Value.description)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Confirm: Non-initiated status — @FT-TRF-013
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-013")>]
let ``Confirming a transfer not in initiated status returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (fromId, toId) = setupTwoAssetAccounts conn tracker prefix
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}FP" (DateOnly(2099, 9, 1)) (DateOnly(2099, 9, 30)) true

        // Initiate and then confirm to get a confirmed transfer
        let transfer = initiateTransfer tracker fromId toId 500.00m None
        let firstConfirm = TransferService.confirm { transferId = transfer.id; confirmedDate = DateOnly(2099, 9, 15) }
        match firstConfirm with
        | Ok confirmed ->
            match confirmed.journalEntryId with
            | Some jeId -> TestCleanup.trackJournalEntry jeId tracker
            | None -> ()
        | Error errs -> failwithf "Setup failed — first confirm should succeed: %A" errs

        // Now try to confirm again — should fail because status is already confirmed
        let result = TransferService.confirm { transferId = transfer.id; confirmedDate = DateOnly(2099, 9, 15) }
        match result with
        | Ok secondConfirmed ->
            match secondConfirmed.journalEntryId with
            | Some jeId -> TestCleanup.trackJournalEntry jeId tracker
            | None -> ()
            Assert.Fail("Expected Error for non-initiated status")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("initiated")),
                        sprintf "Expected error containing 'initiated': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Confirm: No fiscal period — @FT-TRF-014
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-014")>]
let ``Confirming when no fiscal period covers confirmed_date returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (fromId, toId) = setupTwoAssetAccounts conn tracker prefix
        // No fiscal period created for the target date
        let transfer = initiateTransfer tracker fromId toId 500.00m None

        let confirmCmd : ConfirmTransferCommand =
            { transferId = transfer.id
              confirmedDate = DateOnly(2099, 12, 15) }

        let result = TransferService.confirm confirmCmd
        match result with
        | Ok confirmed ->
            match confirmed.journalEntryId with
            | Some jeId -> TestCleanup.trackJournalEntry jeId tracker
            | None -> ()
            Assert.Fail("Expected Error for missing fiscal period")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("fiscal period")),
                        sprintf "Expected error containing 'fiscal period': %A" errs)
    finally TestCleanup.deleteAll tracker
