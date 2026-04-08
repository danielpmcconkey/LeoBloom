module LeoBloom.Tests.TransferTests

open System
open Xunit
open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Ops
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Local helpers
// =====================================================================

/// Look up the pre-existing "asset" account type (id=1) and create two active asset accounts.
/// Returns (fromAccountId, toAccountId).
let private setupTwoAssetAccounts (txn: NpgsqlTransaction) (prefix: string) =
    let assetTypeId = 1  // pre-seeded "asset" type — name must be exactly "asset" for transfer validation
    let fromId = InsertHelpers.insertAccount txn $"{prefix}FR" $"{prefix}_from" assetTypeId true
    let toId = InsertHelpers.insertAccount txn $"{prefix}TO" $"{prefix}_to" assetTypeId true
    (fromId, toId)

/// Query a transfer by id directly from the DB.
let private queryTransfer (txn: NpgsqlTransaction) (transferId: int) =
    use cmd = new NpgsqlCommand(
        "SELECT id, from_account_id, to_account_id, amount, status, initiated_date, \
         confirmed_date, journal_entry_id, description, is_active \
         FROM ops.transfer WHERE id = @id",
        txn.Connection)
    cmd.Transaction <- txn
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
let private queryJournalEntry (txn: NpgsqlTransaction) (jeId: int) =
    use cmd = new NpgsqlCommand(
        "SELECT id, entry_date, description, source, fiscal_period_id \
         FROM ledger.journal_entry WHERE id = @id",
        txn.Connection)
    cmd.Transaction <- txn
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
let private queryJournalEntryLines (txn: NpgsqlTransaction) (jeId: int) =
    use cmd = new NpgsqlCommand(
        "SELECT account_id, amount, entry_type \
         FROM ledger.journal_entry_line WHERE journal_entry_id = @jeid ORDER BY id",
        txn.Connection)
    cmd.Transaction <- txn
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
let private queryJournalEntryReferences (txn: NpgsqlTransaction) (jeId: int) =
    use cmd = new NpgsqlCommand(
        "SELECT reference_type, reference_value \
         FROM ledger.journal_entry_reference WHERE journal_entry_id = @jeid",
        txn.Connection)
    cmd.Transaction <- txn
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
let private initiateTransfer
    (txn: NpgsqlTransaction)
    (fromId: int) (toId: int) (amount: decimal)
    (description: string option) : Transfer =
    let cmd : InitiateTransferCommand =
        { fromAccountId = fromId
          toAccountId = toId
          amount = amount
          initiatedDate = DateOnly(2092, 1, 1)
          expectedSettlement = None
          description = description }
    match TransferService.initiate txn cmd with
    | Ok t -> t
    | Error errs -> failwithf "Setup failed — could not initiate transfer: %A" errs

// =====================================================================
// Initiate: Happy Path — @FT-TRF-001
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-001")>]
let ``Initiating a transfer between two active asset accounts succeeds`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix

    let cmd : InitiateTransferCommand =
        { fromAccountId = fromId
          toAccountId = toId
          amount = 500.00m
          initiatedDate = DateOnly(2092, 1, 1)
          expectedSettlement = None
          description = None }

    let result = TransferService.initiate txn cmd
    match result with
    | Ok transfer ->
        Assert.Equal(fromId, transfer.fromAccountId)
        Assert.Equal(toId, transfer.toAccountId)
        Assert.Equal(500.00m, transfer.amount)
        Assert.Equal(TransferStatus.Initiated, transfer.status)
        Assert.Equal(DateOnly(2092, 1, 1), transfer.initiatedDate)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// Initiate: from = to — @FT-TRF-002
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-002")>]
let ``Initiating with from_account equal to to_account returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetTypeId = 1  // pre-seeded "asset" type
    let acctId = InsertHelpers.insertAccount txn $"{prefix}CK" $"{prefix}_checking" assetTypeId true

    let cmd : InitiateTransferCommand =
        { fromAccountId = acctId
          toAccountId = acctId
          amount = 500.00m
          initiatedDate = DateOnly(2092, 1, 1)
          expectedSettlement = None
          description = None }

    let result = TransferService.initiate txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for same account")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("same")),
                    sprintf "Expected error containing 'same': %A" errs)

// =====================================================================
// Initiate: Non-asset account — @FT-TRF-003
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-003")>]
let ``Initiating with a non-asset account type returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetTypeId = 1  // pre-seeded "asset" type
    let expenseTypeId = InsertHelpers.insertAccountType txn $"{prefix}_exp" "debit"
    let checkingId = InsertHelpers.insertAccount txn $"{prefix}CK" $"{prefix}_checking" assetTypeId true
    let rentId = InsertHelpers.insertAccount txn $"{prefix}RN" $"{prefix}_rent" expenseTypeId true

    let cmd : InitiateTransferCommand =
        { fromAccountId = checkingId
          toAccountId = rentId
          amount = 500.00m
          initiatedDate = DateOnly(2092, 1, 1)
          expectedSettlement = None
          description = None }

    let result = TransferService.initiate txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for non-asset account")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("asset")),
                    sprintf "Expected error containing 'asset': %A" errs)

// =====================================================================
// Initiate: Inactive account — @FT-TRF-004
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-004")>]
let ``Initiating with an inactive account returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetTypeId = 1  // pre-seeded "asset" type
    let checkingId = InsertHelpers.insertAccount txn $"{prefix}CK" $"{prefix}_checking" assetTypeId true
    let closedId = InsertHelpers.insertAccount txn $"{prefix}CL" $"{prefix}_closed" assetTypeId false

    let cmd : InitiateTransferCommand =
        { fromAccountId = checkingId
          toAccountId = closedId
          amount = 500.00m
          initiatedDate = DateOnly(2092, 1, 1)
          expectedSettlement = None
          description = None }

    let result = TransferService.initiate txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for inactive account")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("active")),
                    sprintf "Expected error containing 'active': %A" errs)

// =====================================================================
// Initiate: Zero amount — @FT-TRF-005
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-005")>]
let ``Initiating with amount zero returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix

    let cmd : InitiateTransferCommand =
        { fromAccountId = fromId
          toAccountId = toId
          amount = 0.00m
          initiatedDate = DateOnly(2092, 1, 1)
          expectedSettlement = None
          description = None }

    let result = TransferService.initiate txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for zero amount")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("amount")),
                    sprintf "Expected error containing 'amount': %A" errs)

// =====================================================================
// Initiate: Negative amount — @FT-TRF-006
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-006")>]
let ``Initiating with negative amount returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix

    let cmd : InitiateTransferCommand =
        { fromAccountId = fromId
          toAccountId = toId
          amount = -100.00m
          initiatedDate = DateOnly(2092, 1, 1)
          expectedSettlement = None
          description = None }

    let result = TransferService.initiate txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for negative amount")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("amount")),
                    sprintf "Expected error containing 'amount': %A" errs)

// =====================================================================
// Confirm: Happy Path — @FT-TRF-007
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-007")>]
let ``Confirming an initiated transfer creates correct journal entry and sets status confirmed`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}FP" (DateOnly(2092, 3, 1)) (DateOnly(2092, 3, 31)) true
    let transfer = initiateTransfer txn fromId toId 1000.00m None

    let confirmCmd : ConfirmTransferCommand =
        { transferId = transfer.id
          confirmedDate = DateOnly(2092, 3, 15) }

    let result = TransferService.confirm txn confirmCmd
    match result with
    | Ok confirmed ->
        Assert.Equal(TransferStatus.Confirmed, confirmed.status)

        // Verify journal entry lines
        let jeId = confirmed.journalEntryId.Value
        let lines = queryJournalEntryLines txn jeId
        Assert.Equal(2, lines.Length)

        let debitLine = lines |> List.find (fun l -> l.entryType = "debit")
        Assert.Equal(toId, debitLine.accountId)
        Assert.Equal(1000.00m, debitLine.amount)

        let creditLine = lines |> List.find (fun l -> l.entryType = "credit")
        Assert.Equal(fromId, creditLine.accountId)
        Assert.Equal(1000.00m, creditLine.amount)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// Confirm: confirmed_date and journal_entry_id — @FT-TRF-008
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-008")>]
let ``Confirming sets confirmed_date and journal_entry_id on the transfer`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}FP" (DateOnly(2092, 4, 1)) (DateOnly(2092, 4, 30)) true
    let transfer = initiateTransfer txn fromId toId 750.00m None

    let confirmCmd : ConfirmTransferCommand =
        { transferId = transfer.id
          confirmedDate = DateOnly(2092, 4, 15) }

    let result = TransferService.confirm txn confirmCmd
    match result with
    | Ok confirmed ->
        Assert.Equal(Some (DateOnly(2092, 4, 15)), confirmed.confirmedDate)
        Assert.True(confirmed.journalEntryId.IsSome, "Expected journal_entry_id to be set")

        // Cross-check with DB
        let dbTransfer = queryTransfer txn confirmed.id
        Assert.True(dbTransfer.IsSome)
        Assert.Equal(Some (DateOnly(2092, 4, 15)), dbTransfer.Value.confirmedDate)
        Assert.Equal(confirmed.journalEntryId, dbTransfer.Value.journalEntryId)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// Confirm: JE source and reference — @FT-TRF-009
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-009")>]
let ``Journal entry has transfer source and reference`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}FP" (DateOnly(2092, 5, 1)) (DateOnly(2092, 5, 31)) true
    let transfer = initiateTransfer txn fromId toId 500.00m None

    let confirmCmd : ConfirmTransferCommand =
        { transferId = transfer.id
          confirmedDate = DateOnly(2092, 5, 15) }

    let result = TransferService.confirm txn confirmCmd
    match result with
    | Ok confirmed ->
        let jeId = confirmed.journalEntryId.Value

        // Assert source = "transfer"
        let je = queryJournalEntry txn jeId
        Assert.True(je.IsSome)
        Assert.Equal(Some "transfer", je.Value.source)

        // Assert reference
        let refs = queryJournalEntryReferences txn jeId
        Assert.True(refs.Length >= 1, sprintf "Expected at least 1 reference, got %d" refs.Length)
        let trfRef = refs |> List.find (fun r -> r.referenceType = "transfer")
        Assert.Equal(string transfer.id, trfRef.referenceValue)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// Confirm: JE entry_date = confirmed_date — @FT-TRF-010
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-010")>]
let ``Journal entry entry_date equals confirmed_date`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}FP" (DateOnly(2092, 6, 1)) (DateOnly(2092, 6, 30)) true
    let transfer = initiateTransfer txn fromId toId 500.00m None

    let confirmedDate = DateOnly(2092, 6, 20)
    let confirmCmd : ConfirmTransferCommand =
        { transferId = transfer.id
          confirmedDate = confirmedDate }

    let result = TransferService.confirm txn confirmCmd
    match result with
    | Ok confirmed ->
        let jeId = confirmed.journalEntryId.Value

        let je = queryJournalEntry txn jeId
        Assert.True(je.IsSome)
        Assert.Equal(confirmedDate, je.Value.entryDate)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// Confirm: JE uses transfer description — @FT-TRF-011
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-011")>]
let ``Journal entry uses transfer description when present`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}FP" (DateOnly(2092, 7, 1)) (DateOnly(2092, 7, 31)) true
    let transfer = initiateTransfer txn fromId toId 500.00m (Some "Savings top-up")

    let confirmCmd : ConfirmTransferCommand =
        { transferId = transfer.id
          confirmedDate = DateOnly(2092, 7, 15) }

    let result = TransferService.confirm txn confirmCmd
    match result with
    | Ok confirmed ->
        let jeId = confirmed.journalEntryId.Value

        let je = queryJournalEntry txn jeId
        Assert.True(je.IsSome)
        Assert.Equal("Savings top-up", je.Value.description)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// Confirm: JE auto-generates description — @FT-TRF-012
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-012")>]
let ``Journal entry auto-generates description when transfer has no description`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}FP" (DateOnly(2092, 8, 1)) (DateOnly(2092, 8, 31)) true
    let transfer = initiateTransfer txn fromId toId 500.00m None

    let confirmCmd : ConfirmTransferCommand =
        { transferId = transfer.id
          confirmedDate = DateOnly(2092, 8, 15) }

    let result = TransferService.confirm txn confirmCmd
    match result with
    | Ok confirmed ->
        let jeId = confirmed.journalEntryId.Value

        let je = queryJournalEntry txn jeId
        Assert.True(je.IsSome)
        Assert.StartsWith("Transfer", je.Value.description)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// Confirm: Non-initiated status — @FT-TRF-013
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-013")>]
let ``Confirming a transfer not in initiated status returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}FP" (DateOnly(2092, 9, 1)) (DateOnly(2092, 9, 30)) true

    // Initiate and then confirm to get a confirmed transfer
    let transfer = initiateTransfer txn fromId toId 500.00m None
    let firstConfirm = TransferService.confirm txn { transferId = transfer.id; confirmedDate = DateOnly(2092, 9, 15) }
    match firstConfirm with
    | Ok _ -> ()
    | Error errs -> failwithf "Setup failed — first confirm should succeed: %A" errs

    // Now try to confirm again — should fail because status is already confirmed
    let result = TransferService.confirm txn { transferId = transfer.id; confirmedDate = DateOnly(2092, 9, 15) }
    match result with
    | Ok _ ->
        Assert.Fail("Expected Error for non-initiated status")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("initiated")),
                    sprintf "Expected error containing 'initiated': %A" errs)

// =====================================================================
// Confirm: No fiscal period — @FT-TRF-014
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-014")>]
let ``Confirming when no fiscal period covers confirmed_date returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix
    // No fiscal period created for the target date
    let transfer = initiateTransfer txn fromId toId 500.00m None

    let confirmCmd : ConfirmTransferCommand =
        { transferId = transfer.id
          confirmedDate = DateOnly(2092, 12, 15) }

    let result = TransferService.confirm txn confirmCmd
    match result with
    | Ok _ ->
        Assert.Fail("Expected Error for missing fiscal period")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("fiscal period")),
                    sprintf "Expected error containing 'fiscal period': %A" errs)

// =====================================================================
// Idempotency Guard: Retry skips duplicate — @FT-TRF-015 (P043)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-015")>]
let ``Retry after partial failure skips duplicate journal entry`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}FP" (DateOnly(2092, 10, 1)) (DateOnly(2092, 10, 31)) true
    let transfer = initiateTransfer txn fromId toId 1000.00m None

    // Simulate partial failure: insert a journal entry + reference as if phase 2
    // succeeded but phase 3 (updateConfirm) failed on a prior attempt
    let preExistingJeId =
        use jeCmd = new NpgsqlCommand(
            "INSERT INTO ledger.journal_entry (entry_date, description, source, fiscal_period_id) \
             VALUES (@d, @desc, 'transfer', @fp) RETURNING id", txn.Connection)
        jeCmd.Transaction <- txn
        jeCmd.Parameters.AddWithValue("@d", DateOnly(2092, 10, 15)) |> ignore
        jeCmd.Parameters.AddWithValue("@desc", "simulated partial failure") |> ignore
        jeCmd.Parameters.AddWithValue("@fp", fpId) |> ignore
        jeCmd.ExecuteScalar() :?> int

    // Insert balanced lines
    use debitCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) \
         VALUES (@je, @acct, @amt, 'debit')", txn.Connection)
    debitCmd.Transaction <- txn
    debitCmd.Parameters.AddWithValue("@je", preExistingJeId) |> ignore
    debitCmd.Parameters.AddWithValue("@acct", toId) |> ignore
    debitCmd.Parameters.AddWithValue("@amt", 1000.00m) |> ignore
    debitCmd.ExecuteNonQuery() |> ignore

    use creditCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) \
         VALUES (@je, @acct, @amt, 'credit')", txn.Connection)
    creditCmd.Transaction <- txn
    creditCmd.Parameters.AddWithValue("@je", preExistingJeId) |> ignore
    creditCmd.Parameters.AddWithValue("@acct", fromId) |> ignore
    creditCmd.Parameters.AddWithValue("@amt", 1000.00m) |> ignore
    creditCmd.ExecuteNonQuery() |> ignore

    // Insert the reference that the idempotency guard will find
    use refCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) \
         VALUES (@je, 'transfer', @rv)", txn.Connection)
    refCmd.Transaction <- txn
    refCmd.Parameters.AddWithValue("@je", preExistingJeId) |> ignore
    refCmd.Parameters.AddWithValue("@rv", string transfer.id) |> ignore
    refCmd.ExecuteNonQuery() |> ignore

    // Act: call confirm — guard should find the existing JE and skip phase 2
    let confirmCmd : ConfirmTransferCommand =
        { transferId = transfer.id
          confirmedDate = DateOnly(2092, 10, 15) }

    let result = TransferService.confirm txn confirmCmd

    match result with
    | Ok confirmed ->
        // Assert: result uses the pre-existing JE, not a new one
        Assert.Equal(Some preExistingJeId, confirmed.journalEntryId)
        Assert.Equal(TransferStatus.Confirmed, confirmed.status)

        // Assert: no duplicate — count non-voided references for this transfer
        use countCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM ledger.journal_entry_reference jer \
             JOIN ledger.journal_entry je ON je.id = jer.journal_entry_id \
             WHERE jer.reference_type = 'transfer' AND jer.reference_value = @rv \
             AND je.voided_at IS NULL", txn.Connection)
        countCmd.Transaction <- txn
        countCmd.Parameters.AddWithValue("@rv", string transfer.id) |> ignore
        let count = countCmd.ExecuteScalar() :?> int64
        Assert.Equal(1L, count)

        // Assert: transfer is confirmed with correct JE ID in DB
        let dbTransfer = queryTransfer txn transfer.id
        Assert.True(dbTransfer.IsSome)
        Assert.Equal("confirmed", dbTransfer.Value.status)
        Assert.Equal(Some preExistingJeId, dbTransfer.Value.journalEntryId)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// Idempotency Guard: Voided entry doesn't trigger guard — @FT-TRF-016 (P043)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-TRF-016")>]
let ``Voided prior journal entry does not trigger the idempotency guard`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (fromId, toId) = setupTwoAssetAccounts txn prefix
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}FP" (DateOnly(2092, 11, 1)) (DateOnly(2092, 11, 30)) true
    let transfer = initiateTransfer txn fromId toId 1000.00m None

    // Insert a journal entry + reference, then void it
    let voidedJeId =
        use jeCmd = new NpgsqlCommand(
            "INSERT INTO ledger.journal_entry (entry_date, description, source, fiscal_period_id) \
             VALUES (@d, @desc, 'transfer', @fp) RETURNING id", txn.Connection)
        jeCmd.Transaction <- txn
        jeCmd.Parameters.AddWithValue("@d", DateOnly(2092, 11, 15)) |> ignore
        jeCmd.Parameters.AddWithValue("@desc", "will be voided") |> ignore
        jeCmd.Parameters.AddWithValue("@fp", fpId) |> ignore
        jeCmd.ExecuteScalar() :?> int

    // Insert balanced lines
    use debitCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) \
         VALUES (@je, @acct, @amt, 'debit')", txn.Connection)
    debitCmd.Transaction <- txn
    debitCmd.Parameters.AddWithValue("@je", voidedJeId) |> ignore
    debitCmd.Parameters.AddWithValue("@acct", toId) |> ignore
    debitCmd.Parameters.AddWithValue("@amt", 1000.00m) |> ignore
    debitCmd.ExecuteNonQuery() |> ignore

    use creditCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) \
         VALUES (@je, @acct, @amt, 'credit')", txn.Connection)
    creditCmd.Transaction <- txn
    creditCmd.Parameters.AddWithValue("@je", voidedJeId) |> ignore
    creditCmd.Parameters.AddWithValue("@acct", fromId) |> ignore
    creditCmd.Parameters.AddWithValue("@amt", 1000.00m) |> ignore
    creditCmd.ExecuteNonQuery() |> ignore

    // Insert the reference
    use refCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) \
         VALUES (@je, 'transfer', @rv)", txn.Connection)
    refCmd.Transaction <- txn
    refCmd.Parameters.AddWithValue("@je", voidedJeId) |> ignore
    refCmd.Parameters.AddWithValue("@rv", string transfer.id) |> ignore
    refCmd.ExecuteNonQuery() |> ignore

    // Void the journal entry
    use voidCmd = new NpgsqlCommand(
        "UPDATE ledger.journal_entry SET voided_at = now(), void_reason = 'test void' \
         WHERE id = @id", txn.Connection)
    voidCmd.Transaction <- txn
    voidCmd.Parameters.AddWithValue("@id", voidedJeId) |> ignore
    voidCmd.ExecuteNonQuery() |> ignore

    // Act: call confirm — guard should NOT find voided entry, should create new
    let confirmCmd : ConfirmTransferCommand =
        { transferId = transfer.id
          confirmedDate = DateOnly(2092, 11, 15) }

    let result = TransferService.confirm txn confirmCmd

    match result with
    | Ok confirmed ->
        // Assert: a NEW journal entry was created, not the voided one
        Assert.True(confirmed.journalEntryId.IsSome, "Expected journal_entry_id to be set")
        Assert.NotEqual(Some voidedJeId, confirmed.journalEntryId)
        Assert.Equal(TransferStatus.Confirmed, confirmed.status)

        // Assert: transfer is confirmed in DB
        let dbTransfer = queryTransfer txn transfer.id
        Assert.True(dbTransfer.IsSome)
        Assert.Equal("confirmed", dbTransfer.Value.status)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
