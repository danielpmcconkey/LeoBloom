module LeoBloom.Tests.BalanceProjectionTests

open System
open Npgsql
open Xunit
open LeoBloom.Domain.Ops
open LeoBloom.Utilities
open LeoBloom.Ops
open LeoBloom.Ledger
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Test environment setup
// =====================================================================

/// Test environment for balance projection tests.
/// Uses two asset accounts: "checking" (the projection subject) and
/// "counter" (used as the other side of journal entries and transfers).
module ProjectionEnv =

    type Env =
        { CheckingId: int
          CheckingCode: string
          CounterId: int
          FiscalPeriodId: int
          Txn: NpgsqlTransaction
          Connection: NpgsqlConnection }

    /// Create a projection environment with the given opening balance on the checking account.
    /// Posts a journal entry: debit checking, credit counter by `balance`.
    let create (balance: decimal) =
        let conn = DataSource.openConnection()
        let txn = conn.BeginTransaction()
        let prefix = TestData.uniquePrefix()
        let assetTypeId = 1 // pre-seeded "asset" type (normal_balance = 'debit')
        let checkingId = InsertHelpers.insertAccount txn (prefix + "CK") (prefix + "_checking") assetTypeId true
        let counterId = InsertHelpers.insertAccount txn (prefix + "CT") (prefix + "_counter") assetTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31)) true
        if balance <> 0m then
            let jeCmd : LeoBloom.Domain.Ledger.PostJournalEntryCommand =
                { entryDate = DateOnly(2026, 4, 1)
                  description = "Test opening balance"
                  source = Some "test"
                  fiscalPeriodId = fpId
                  lines =
                    [ { accountId = checkingId; amount = balance; entryType = LeoBloom.Domain.Ledger.EntryType.Debit; memo = None }
                      { accountId = counterId; amount = balance; entryType = LeoBloom.Domain.Ledger.EntryType.Credit; memo = None } ]
                  references = [] }
            match JournalEntryService.post txn jeCmd with
            | Ok _ -> ()
            | Error errs -> failwith (sprintf "Failed to post opening balance entry: %A" errs)
        { CheckingId = checkingId
          CheckingCode = prefix + "CK"
          CounterId = counterId
          FiscalPeriodId = fpId
          Txn = txn
          Connection = conn }

    /// Insert a receivable obligation agreement targeting checking as dest_account_id.
    let insertReceivableAgreement (env: Env) (name: string) (amount: decimal option) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_agreement \
             (name, obligation_type, cadence, dest_account_id, is_active) \
             VALUES (@n, 'receivable', 'one_time', @dest, true) RETURNING id",
            env.Txn.Connection)
        cmd.Transaction <- env.Txn
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@dest", env.CheckingId) |> ignore
        cmd.ExecuteScalar() :?> int

    /// Insert a payable obligation agreement with checking as source_account_id.
    let insertPayableAgreement (env: Env) (name: string) (amount: decimal option) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_agreement \
             (name, obligation_type, cadence, source_account_id, is_active) \
             VALUES (@n, 'payable', 'one_time', @src, true) RETURNING id",
            env.Txn.Connection)
        cmd.Transaction <- env.Txn
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@src", env.CheckingId) |> ignore
        cmd.ExecuteScalar() :?> int

    /// Insert an obligation instance for the given agreement.
    let insertInstance (env: Env) (agreementId: int) (expectedDate: DateOnly) (amount: decimal option) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_instance \
             (obligation_agreement_id, name, status, expected_date, amount, is_active) \
             VALUES (@aid, 'Test instance', 'expected', @ed, @amt, true) RETURNING id",
            env.Txn.Connection)
        cmd.Transaction <- env.Txn
        cmd.Parameters.AddWithValue("@aid", agreementId) |> ignore
        cmd.Parameters.AddWithValue("@ed", expectedDate) |> ignore
        match amount with
        | Some a -> cmd.Parameters.AddWithValue("@amt", a) |> ignore
        | None -> cmd.Parameters.AddWithValue("@amt", DBNull.Value) |> ignore
        cmd.ExecuteScalar() :?> int

    /// Insert an initiated transfer FROM checking (outflow).
    let insertTransferOut (env: Env) (amount: decimal) (initiatedDate: DateOnly) (expectedSettlement: DateOnly option) : int =
        let cmd : InitiateTransferCommand =
            { fromAccountId = env.CheckingId
              toAccountId = env.CounterId
              amount = amount
              initiatedDate = initiatedDate
              expectedSettlement = expectedSettlement
              description = Some "Test transfer out" }
        match TransferService.initiate env.Txn cmd with
        | Ok t -> t.id
        | Error errs -> failwith (sprintf "Failed to initiate transfer out: %A" errs)

    /// Insert an initiated transfer INTO checking (inflow).
    let insertTransferIn (env: Env) (amount: decimal) (initiatedDate: DateOnly) (expectedSettlement: DateOnly option) : int =
        let cmd : InitiateTransferCommand =
            { fromAccountId = env.CounterId
              toAccountId = env.CheckingId
              amount = amount
              initiatedDate = initiatedDate
              expectedSettlement = expectedSettlement
              description = Some "Test transfer in" }
        match TransferService.initiate env.Txn cmd with
        | Ok t -> t.id
        | Error errs -> failwith (sprintf "Failed to initiate transfer in: %A" errs)

    /// Insert an obligation instance with an explicit status (for excluded-status scenarios).
    let insertInstanceWithStatus (env: Env) (agreementId: int) (expectedDate: DateOnly) (amount: decimal option) (status: string) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_instance \
             (obligation_agreement_id, name, status, expected_date, amount, is_active) \
             VALUES (@aid, 'Test instance', @status, @ed, @amt, true) RETURNING id",
            env.Txn.Connection)
        cmd.Transaction <- env.Txn
        cmd.Parameters.AddWithValue("@aid", agreementId) |> ignore
        cmd.Parameters.AddWithValue("@status", status) |> ignore
        cmd.Parameters.AddWithValue("@ed", expectedDate) |> ignore
        match amount with
        | Some a -> cmd.Parameters.AddWithValue("@amt", a) |> ignore
        | None -> cmd.Parameters.AddWithValue("@amt", DBNull.Value) |> ignore
        cmd.ExecuteScalar() :?> int

    /// Initiate and then confirm a transfer out FROM checking.
    /// Uses the test's own fiscal period (created in ProjectionEnv.create) via findByDate.
    let insertConfirmedTransferOut (env: Env) (amount: decimal) (initiatedDate: DateOnly) (expectedSettlement: DateOnly) : int =
        let initiateCmd : InitiateTransferCommand =
            { fromAccountId = env.CheckingId
              toAccountId = env.CounterId
              amount = amount
              initiatedDate = initiatedDate
              expectedSettlement = Some expectedSettlement
              description = Some "Test confirmed transfer out" }
        let transferId =
            match TransferService.initiate env.Txn initiateCmd with
            | Ok t -> t.id
            | Error errs -> failwith (sprintf "Failed to initiate transfer for confirm: %A" errs)
        let confirmCmd : ConfirmTransferCommand =
            { transferId = transferId
              confirmedDate = initiatedDate }
        match TransferService.confirm env.Txn confirmCmd with
        | Ok t -> t.id
        | Error errs -> failwith (sprintf "Failed to confirm transfer: %A" errs)

    let cleanup (env: Env) =
        env.Txn.Dispose()
        env.Connection.Dispose()

// =====================================================================
// Scenario helpers
// =====================================================================

let private getDay (projection: BalanceProjection) (date: DateOnly) =
    projection.days |> List.find (fun d -> d.date = date)

let private today = DateOnly.FromDateTime(DateTime.Today)
let private tomorrow = today.AddDays(1)
let private dayAfter = today.AddDays(2)
let private day3 = today.AddDays(3)

// =====================================================================
// FT-BP-001: Flat line — no obligations or transfers
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-001")>]
let ``flat line when no future obligations or transfers`` () =
    let env = ProjectionEnv.create 5000m
    try
        let result = BalanceProjectionService.project env.Txn env.CheckingCode day3
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            Assert.Equal(5000m, projection.currentBalance)
            Assert.Equal(4, projection.days.Length)
            for day in projection.days do
                Assert.Equal(5000m, day.closingBalance)
            // Series spans today through day3
            let dates = projection.days |> List.map (fun d -> d.date)
            Assert.Contains(today, dates)
            Assert.Contains(day3, dates)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-002: Expected receivable inflow
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-002")>]
let ``receivable inflow increases projected balance on expected_date`` () =
    let env = ProjectionEnv.create 1000m
    try
        let agId = ProjectionEnv.insertReceivableAgreement env "Rent" None
        ProjectionEnv.insertInstance env agId dayAfter (Some 500m) |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode day3
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            Assert.Equal(1000m, (getDay projection tomorrow).closingBalance)
            Assert.Equal(1500m, (getDay projection dayAfter).closingBalance)
            Assert.Equal(1500m, (getDay projection day3).closingBalance)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-003: Expected payable outflow
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-003")>]
let ``payable outflow decreases projected balance on expected_date`` () =
    let env = ProjectionEnv.create 2000m
    try
        let agId = ProjectionEnv.insertPayableAgreement env "Mortgage" None
        ProjectionEnv.insertInstance env agId tomorrow (Some 400m) |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode day3
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            Assert.Equal(2000m, (getDay projection today).closingBalance)
            Assert.Equal(1600m, (getDay projection tomorrow).closingBalance)
            Assert.Equal(1600m, (getDay projection day3).closingBalance)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-004: Transfer out on settlement date
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-004")>]
let ``initiated transfer out decreases projected balance on settlement date`` () =
    let env = ProjectionEnv.create 3000m
    try
        ProjectionEnv.insertTransferOut env 800m today (Some dayAfter) |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode day3
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            Assert.Equal(3000m, (getDay projection tomorrow).closingBalance)
            Assert.Equal(2200m, (getDay projection dayAfter).closingBalance)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-005: Transfer in on settlement date
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-005")>]
let ``initiated transfer in increases projected balance on settlement date`` () =
    let env = ProjectionEnv.create 1000m
    try
        ProjectionEnv.insertTransferIn env 600m today (Some tomorrow) |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode day3
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            Assert.Equal(1000m, (getDay projection today).closingBalance)
            Assert.Equal(1600m, (getDay projection tomorrow).closingBalance)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-006: Transfer with no expected_settlement falls back to initiated_date
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-006")>]
let ``transfer with no expected_settlement uses initiated_date as projection date`` () =
    let env = ProjectionEnv.create 2000m
    try
        ProjectionEnv.insertTransferOut env 300m dayAfter None |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode day3
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            Assert.Equal(2000m, (getDay projection tomorrow).closingBalance)
            Assert.Equal(1700m, (getDay projection dayAfter).closingBalance)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-007: All components combine correctly
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-007")>]
let ``all projection components combine correctly`` () =
    let env = ProjectionEnv.create 5000m
    try
        let rcvAg = ProjectionEnv.insertReceivableAgreement env "Rent rcv" None
        let payAg = ProjectionEnv.insertPayableAgreement env "Expense" None
        ProjectionEnv.insertInstance env rcvAg dayAfter (Some 1000m) |> ignore
        ProjectionEnv.insertInstance env payAg dayAfter (Some 400m) |> ignore
        ProjectionEnv.insertTransferOut env 200m today (Some dayAfter) |> ignore
        ProjectionEnv.insertTransferIn env 100m today (Some dayAfter) |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode dayAfter
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            // 5000 + 1000 - 400 - 200 + 100 = 5500
            Assert.Equal(5500m, (getDay projection dayAfter).closingBalance)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-008: Series includes every day from today through projection_date
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-008")>]
let ``projection series includes every day from today through projection_date`` () =
    let env = ProjectionEnv.create 1000m
    try
        let result = BalanceProjectionService.project env.Txn env.CheckingCode day3
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            let dates = projection.days |> List.map (fun d -> d.date) |> Set.ofList
            Assert.Contains(today, dates)
            Assert.Contains(tomorrow, dates)
            Assert.Contains(dayAfter, dates)
            Assert.Contains(day3, dates)
            Assert.Equal(4, projection.days.Length)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-009: Same-day obligations summed with itemized breakdown
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-009")>]
let ``multiple obligations on same day are summed with itemized breakdown`` () =
    let env = ProjectionEnv.create 3000m
    try
        let rcvAg = ProjectionEnv.insertReceivableAgreement env "Rcv Apr8" None
        let pay1Ag = ProjectionEnv.insertPayableAgreement env "Pay1 Apr8" None
        let pay2Ag = ProjectionEnv.insertPayableAgreement env "Pay2 Apr8" None
        ProjectionEnv.insertInstance env rcvAg tomorrow (Some 500m) |> ignore
        ProjectionEnv.insertInstance env pay1Ag tomorrow (Some 200m) |> ignore
        ProjectionEnv.insertInstance env pay2Ag tomorrow (Some 150m) |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode day3
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            let day = getDay projection tomorrow
            Assert.Equal(3, day.items.Length)
            // net = +500 - 200 - 150 = +150
            Assert.Equal(150m, day.knownNetChange)
            Assert.Equal(3150m, day.closingBalance)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-010: Null-amount payable surfaces as unknown outflow
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-010")>]
let ``null-amount payable obligation surfaces as unknown outflow`` () =
    let env = ProjectionEnv.create 2000m
    try
        let agId = ProjectionEnv.insertPayableAgreement env "Mystery expense" None
        ProjectionEnv.insertInstance env agId dayAfter None |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode day3
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            let day = getDay projection dayAfter
            Assert.True(day.hasUnknownAmounts)
            Assert.Equal(1, day.items.Length)
            Assert.Equal(None, day.items.[0].amount)
            Assert.Equal(Outflow, day.items.[0].direction)
            // Balance is NOT reduced — unknown amount does not affect closing balance
            Assert.Equal(2000m, day.closingBalance)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-011: Null-amount receivable surfaces as unknown inflow
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-011")>]
let ``null-amount receivable obligation surfaces as unknown inflow`` () =
    let env = ProjectionEnv.create 2000m
    try
        let agId = ProjectionEnv.insertReceivableAgreement env "Mystery income" None
        ProjectionEnv.insertInstance env agId dayAfter None |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode day3
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            let day = getDay projection dayAfter
            Assert.True(day.hasUnknownAmounts)
            Assert.Equal(1, day.items.Length)
            Assert.Equal(None, day.items.[0].amount)
            Assert.Equal(Inflow, day.items.[0].direction)
            // Balance is NOT increased — unknown amount does not affect closing balance
            Assert.Equal(2000m, day.closingBalance)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-012: Past projection date rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-012")>]
let ``projection date in the past is rejected`` () =
    let env = ProjectionEnv.create 1000m
    try
        let pastDate = today.AddDays(-1)
        let result = BalanceProjectionService.project env.Txn env.CheckingCode pastDate
        match result with
        | Ok _ -> Assert.Fail("Expected Error for past projection date")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLower().Contains("past")),
                sprintf "Expected error containing 'past', got: %A" errs)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-013: Today's date rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-013")>]
let ``projection date equal to today is rejected`` () =
    let env = ProjectionEnv.create 1000m
    try
        let result = BalanceProjectionService.project env.Txn env.CheckingCode today
        match result with
        | Ok _ -> Assert.Fail("Expected Error for today as projection date")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLower().Contains("past")),
                sprintf "Expected error containing 'past', got: %A" errs)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-014: Nonexistent account returns error
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-014")>]
let ``nonexistent account returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result = BalanceProjectionService.project txn "ZZZZ_NONEXISTENT" day3
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent account")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLower().Contains("does not exist")),
            sprintf "Expected error containing 'does not exist', got: %A" errs)

// =====================================================================
// FT-BP-015: Confirmed obligation instance excluded from projection
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-015")>]
let ``confirmed obligation instance is excluded from balance projection`` () =
    let env = ProjectionEnv.create 5000m
    try
        let agExpected = ProjectionEnv.insertReceivableAgreement env "Rcv expected BP015" None
        let agConfirmed = ProjectionEnv.insertReceivableAgreement env "Rcv confirmed BP015" None
        ProjectionEnv.insertInstance env agExpected (dayAfter) (Some 800m) |> ignore
        ProjectionEnv.insertInstanceWithStatus env agConfirmed (dayAfter) (Some 600m) "confirmed" |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode (day3)
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            let day = getDay projection (dayAfter)
            // 5000 + 800 (expected receivable) = 5800; confirmed 600 excluded
            Assert.Equal(5800m, day.closingBalance)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-016: Posted obligation instance excluded from projection
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-016")>]
let ``posted obligation instance is excluded from balance projection`` () =
    let env = ProjectionEnv.create 3000m
    try
        let agExpected = ProjectionEnv.insertPayableAgreement env "Pay expected BP016" None
        let agPosted = ProjectionEnv.insertPayableAgreement env "Pay posted BP016" None
        ProjectionEnv.insertInstance env agExpected (dayAfter) (Some 400m) |> ignore
        ProjectionEnv.insertInstanceWithStatus env agPosted (dayAfter) (Some 250m) "posted" |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode (day3)
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            let day = getDay projection (dayAfter)
            // 3000 - 400 (expected payable) = 2600; posted 250 excluded
            Assert.Equal(2600m, day.closingBalance)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-017: Confirmed transfer excluded from projection
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-017")>]
let ``confirmed transfer is excluded from balance projection`` () =
    // Start at 4300 so that after confirming the 300 transfer (which posts a JE),
    // the effective current balance is 4000 — matching the Gherkin scenario.
    let env = ProjectionEnv.create 4300m
    try
        ProjectionEnv.insertConfirmedTransferOut env 300m (today) (dayAfter) |> ignore
        ProjectionEnv.insertTransferOut env 500m (today) (Some (dayAfter)) |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode (day3)
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            let day = getDay projection (dayAfter)
            // Effective current balance: 4300 - 300 (confirmed JE) = 4000
            // Projected: 4000 - 500 (initiated transfer out) = 3500; confirmed not a future item
            Assert.Equal(3500m, day.closingBalance)
    finally ProjectionEnv.cleanup env

// =====================================================================
// FT-BP-018: Skipped obligation instance excluded from projection
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-BP-018")>]
let ``skipped obligation instance is excluded from balance projection`` () =
    let env = ProjectionEnv.create 2000m
    try
        let agExpected = ProjectionEnv.insertReceivableAgreement env "Rcv expected BP018" None
        let agSkipped = ProjectionEnv.insertPayableAgreement env "Pay skipped BP018" None
        ProjectionEnv.insertInstance env agExpected (dayAfter) (Some 1000m) |> ignore
        ProjectionEnv.insertInstanceWithStatus env agSkipped (dayAfter) (Some 350m) "skipped" |> ignore
        let result = BalanceProjectionService.project env.Txn env.CheckingCode (day3)
        match result with
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
        | Ok projection ->
            let day = getDay projection (dayAfter)
            // 2000 + 1000 (expected receivable) = 3000; skipped 350 excluded
            Assert.Equal(3000m, day.closingBalance)
    finally ProjectionEnv.cleanup env
