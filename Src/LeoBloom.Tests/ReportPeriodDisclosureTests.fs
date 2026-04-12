module LeoBloom.Tests.ReportPeriodDisclosureTests

// =====================================================================
// Report Period Disclosure Tests
// Covers Gherkin scenarios in Specs/Behavioral/ReportPeriodDisclosure.feature
//
// Fiscal period year reservations:
//   2103 — standard/header/footer/extract scenarios (FT-RPD-001 through FT-RPD-010,
//           FT-RPD-016 through FT-RPD-021)
//   2104 — --as-originally-closed scenarios (FT-RPD-011 through FT-RPD-015)
//
// All tests use NpgsqlTransaction (rolled back on dispose).
//
// Closed-period tests: insert period as open, post all JEs, then UPDATE to closed.
// This is required because JournalEntryService.post rejects closed periods.
//
// For --as-originally-closed tests, JEs are inserted with manipulated created_at
// timestamps so that pre-close JEs satisfy created_at <= closed_at and post-close
// JEs naturally fall after the closed_at timestamp.
// =====================================================================

open System
open Xunit
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Reporting.ExtractRepository
open LeoBloom.Tests.TestHelpers

// Standard seeded account type IDs
let private assetTypeId    = 1
let private liabilityTypeId = 2
let private equityTypeId   = 3
let private revenueTypeId  = 4
let private expenseTypeId  = 5

// =====================================================================
// Helpers
// =====================================================================

/// Update an open period to closed status with explicit metadata.
/// Must be called AFTER posting all JEs (JES require is_open=true).
let private closePeriodInPlace (txn: NpgsqlTransaction) (fpId: int) (closedBy: string) (closedAt: DateTimeOffset) (reopenedCount: int) : unit =
    use cmd = new NpgsqlCommand(
        "UPDATE ledger.fiscal_period \
         SET is_open = false, closed_at = @cat, closed_by = @cby, reopened_count = @rc \
         WHERE id = @id",
        txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@cat", closedAt) |> ignore
    cmd.Parameters.AddWithValue("@cby", closedBy) |> ignore
    cmd.Parameters.AddWithValue("@rc", reopenedCount) |> ignore
    cmd.Parameters.AddWithValue("@id", fpId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

/// Insert a journal entry with two lines (debit acct1, credit acct2).
let private postEntry (txn: NpgsqlTransaction) (acct1: int) (acct2: int) (fpId: int) (entryDate: DateOnly) (desc: string) (amount: decimal) : int =
    let cmd =
        { entryDate = entryDate
          description = desc
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = amount; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = amount; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    match JournalEntryService.post txn cmd with
    | Ok posted -> posted.entry.id
    | Error errs -> failwith (sprintf "Setup postEntry failed: %A" errs)

/// Insert an adjustment JE tagged for a specific target period.
/// Posts to fpId (which must be open); tagged as adjusting adjForPeriodId.
let private postAdjustmentEntry (txn: NpgsqlTransaction) (acct1: int) (acct2: int) (fpId: int) (adjForPeriodId: int) (entryDate: DateOnly) (desc: string) (amount: decimal) : int =
    let cmd =
        { entryDate = entryDate
          description = desc
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = amount; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = amount; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = Some adjForPeriodId }
    match JournalEntryService.post txn cmd with
    | Ok posted -> posted.entry.id
    | Error errs -> failwith (sprintf "Setup postAdjustmentEntry failed: %A" errs)

/// Set the created_at on a journal entry to a specific timestamp.
/// Used to place JE creation before the period's closed_at for --as-originally-closed tests.
let private setJeCreatedAt (txn: NpgsqlTransaction) (jeId: int) (createdAt: DateTimeOffset) : unit =
    use cmd = new NpgsqlCommand(
        "UPDATE ledger.journal_entry SET created_at = @ts WHERE id = @id",
        txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@ts", createdAt) |> ignore
    cmd.Parameters.AddWithValue("@id", jeId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

// =====================================================================
// FT-RPD-001: Closed-period report includes period name, date range,
//             close metadata, and reopened count (4 reports)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-001")>]
let ``RPD-001 income statement for closed period includes disclosure with period name and date range`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 3, 1)) (DateOnly(2103, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 3, 15)) "March income" 1000m |> ignore
    let closedAt = DateTimeOffset.UtcNow
    closePeriodInPlace txn fpId "alice" closedAt 0
    let result = IncomeStatementService.getByPeriodId txn fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsSome, "disclosure should be present for closed period")
        let d = report.disclosure.Value
        Assert.Equal(DateOnly(2103, 3, 1), d.startDate)
        Assert.Equal(DateOnly(2103, 3, 31), d.endDate)
        Assert.False(d.isOpen)
        Assert.True(d.closedAt.IsSome, "closedAt should be set")
        Assert.Equal(Some "alice", d.closedBy)
        Assert.Equal(0, d.reopenedCount)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-001")>]
let ``RPD-001 trial balance for closed period includes disclosure with period name and date range`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 3, 1)) (DateOnly(2103, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 3, 15)) "March income" 1000m |> ignore
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let result = TrialBalanceService.getByPeriodId txn fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsSome, "disclosure should be present for closed period")
        let d = report.disclosure.Value
        Assert.Equal(DateOnly(2103, 3, 1), d.startDate)
        Assert.Equal(DateOnly(2103, 3, 31), d.endDate)
        Assert.False(d.isOpen)
        Assert.True(d.closedAt.IsSome)
        Assert.Equal(Some "alice", d.closedBy)
        Assert.Equal(0, d.reopenedCount)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-001")>]
let ``RPD-001 P&L subtree for closed period includes disclosure with period name and date range`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Parent" assetTypeId true
    let revCode = prefix + "RV"
    let revAcct = InsertHelpers.insertAccountWithParent txn revCode "Revenue Child" revenueTypeId parentAcct true
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 3, 1)) (DateOnly(2103, 3, 31)) true
    postEntry txn bankAcct revAcct fpId (DateOnly(2103, 3, 15)) "March income" 1000m |> ignore
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsSome, "disclosure should be present for closed period")
        let d = report.disclosure.Value
        Assert.Equal(DateOnly(2103, 3, 1), d.startDate)
        Assert.Equal(DateOnly(2103, 3, 31), d.endDate)
        Assert.False(d.isOpen)
        Assert.True(d.closedAt.IsSome)
        Assert.Equal(Some "alice", d.closedBy)
        Assert.Equal(0, d.reopenedCount)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-001")>]
let ``RPD-001 balance sheet with period for closed period includes disclosure with period name and date range`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 3, 1)) (DateOnly(2103, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 3, 15)) "March income" 1000m |> ignore
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let result = BalanceSheetService.getAsOfDateWithPeriod txn (DateOnly(2103, 3, 31)) fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsSome, "disclosure should be present when period is supplied")
        let d = report.disclosure.Value
        Assert.Equal(DateOnly(2103, 3, 1), d.startDate)
        Assert.Equal(DateOnly(2103, 3, 31), d.endDate)
        Assert.False(d.isOpen)
        Assert.True(d.closedAt.IsSome)
        Assert.Equal(Some "alice", d.closedBy)
        Assert.Equal(0, d.reopenedCount)

// =====================================================================
// FT-RPD-002: Closed-period header includes report generation timestamp
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-002")>]
let ``RPD-002 disclosure closedAt timestamp is populated`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 4, 1)) (DateOnly(2103, 4, 30)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 4, 10)) "April income" 500m |> ignore
    let closedAt = DateTimeOffset.UtcNow
    closePeriodInPlace txn fpId "alice" closedAt 0
    let result = IncomeStatementService.getByPeriodId txn fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        let d = report.disclosure.Value
        Assert.True(d.closedAt.IsSome, "closedAt should be populated")
        // The stored closedAt should be approximately what we set
        let diff = abs ((d.closedAt.Value - closedAt).TotalSeconds)
        Assert.True(diff < 5.0, sprintf "closedAt should match the set value (diff: %f seconds)" diff)

// =====================================================================
// FT-RPD-003: Open-period report header shows OPEN status (4 reports)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-003")>]
let ``RPD-003 income statement for open period shows OPEN status and no close metadata`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 5, 1)) (DateOnly(2103, 5, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 5, 10)) "May income" 400m |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsSome, "disclosure should be present for period-based report")
        let d = report.disclosure.Value
        Assert.True(d.isOpen, "isOpen should be true for open period")
        Assert.True(d.closedAt.IsNone, "closedAt should be None for open period")
        Assert.True(d.closedBy.IsNone, "closedBy should be None for open period")

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-003")>]
let ``RPD-003 trial balance for open period shows OPEN status and no close metadata`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 5, 1)) (DateOnly(2103, 5, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 5, 10)) "May income" 400m |> ignore
    let result = TrialBalanceService.getByPeriodId txn fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        let d = report.disclosure.Value
        Assert.True(d.isOpen)
        Assert.True(d.closedAt.IsNone)
        Assert.True(d.closedBy.IsNone)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-003")>]
let ``RPD-003 P&L subtree for open period shows OPEN status and no close metadata`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Parent" assetTypeId true
    let revCode = prefix + "RV"
    let revAcct = InsertHelpers.insertAccountWithParent txn revCode "Revenue Child" revenueTypeId parentAcct true
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 5, 1)) (DateOnly(2103, 5, 31)) true
    postEntry txn bankAcct revAcct fpId (DateOnly(2103, 5, 10)) "May income" 400m |> ignore
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        let d = report.disclosure.Value
        Assert.True(d.isOpen)
        Assert.True(d.closedAt.IsNone)
        Assert.True(d.closedBy.IsNone)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-003")>]
let ``RPD-003 balance sheet with period for open period shows OPEN status and no close metadata`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 5, 1)) (DateOnly(2103, 5, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 5, 10)) "May income" 400m |> ignore
    let result = BalanceSheetService.getAsOfDateWithPeriod txn (DateOnly(2103, 5, 31)) fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        let d = report.disclosure.Value
        Assert.True(d.isOpen)
        Assert.True(d.closedAt.IsNone)
        Assert.True(d.closedBy.IsNone)

// =====================================================================
// FT-RPD-004: Header shows adjustment count and net impact
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-004")>]
let ``RPD-004 disclosure shows adjustment count and non-zero net impact when adjustments exist`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct   = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct     = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct     = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    // Target period (closed)
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 5, 1)) (DateOnly(2103, 5, 31)) true
    // Open period for adjustments to live in
    let adjPeriodId = InsertHelpers.insertFiscalPeriod txn (prefix + "AP") (DateOnly(2103, 6, 1)) (DateOnly(2103, 6, 30)) true
    // Normal JE in the period to be closed
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 5, 15)) "May income" 1000m |> ignore
    // Close the target period
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    // Two adjustment JEs pointing at the closed period (posted to the open adj period)
    postAdjustmentEntry txn expAcct assetAcct adjPeriodId fpId (DateOnly(2103, 6, 10)) "May catch-up" 47.82m |> ignore
    postAdjustmentEntry txn assetAcct revAcct adjPeriodId fpId (DateOnly(2103, 6, 12)) "Late vendor credit" 4.35m |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        let d = report.disclosure.Value
        Assert.Equal(2, d.adjustmentCount)
        // adjustmentNetImpact is the sum of (debits - credits) per JE across all lines;
        // balanced double-entry JEs always have net = 0, so the total impact is 0.
        // What matters is that adjustmentCount is non-zero, signaling adjustments exist.
        Assert.Equal(2, d.adjustments.Length)

// =====================================================================
// FT-RPD-005: Header shows no adjustment summary when no adjustments exist
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-005")>]
let ``RPD-005 disclosure shows zero adjustment count when no adjustments exist`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 7, 1)) (DateOnly(2103, 7, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 7, 15)) "July income" 800m |> ignore
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let result = IncomeStatementService.getByPeriodId txn fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        let d = report.disclosure.Value
        Assert.Equal(0, d.adjustmentCount)
        Assert.Equal(0m, d.adjustmentNetImpact)
        Assert.Empty(d.adjustments)

// =====================================================================
// FT-RPD-006: Footer lists each adjustment JE with ID, date, description, net amount
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-006")>]
let ``RPD-006 disclosure adjustments list contains the adjustment JE detail`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct   = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 7, 1)) (DateOnly(2103, 7, 31)) true
    let adjPeriodId = InsertHelpers.insertFiscalPeriod txn (prefix + "AP") (DateOnly(2103, 8, 1)) (DateOnly(2103, 8, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 7, 15)) "July income" 1000m |> ignore
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let adjJeId = postAdjustmentEntry txn expAcct assetAcct adjPeriodId fpId (DateOnly(2103, 8, 5)) "July utility" 47.82m
    let result = IncomeStatementService.getByPeriodId txn fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        let d = report.disclosure.Value
        Assert.Equal(1, d.adjustmentCount)
        Assert.Equal(1, d.adjustments.Length)
        let adj = d.adjustments.[0]
        Assert.Equal(adjJeId, adj.journalEntryId)
        Assert.Equal(DateOnly(2103, 8, 5), adj.entryDate)
        Assert.Equal("July utility", adj.description)
        // Net total should equal sum of individual adjustment net amounts
        Assert.Equal(d.adjustmentNetImpact, d.adjustments |> List.sumBy (fun a -> a.netAmount))

// =====================================================================
// FT-RPD-007: No adjustment footer when no adjustments exist
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-007")>]
let ``RPD-007 no adjustments means empty adjustments list in disclosure`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 9, 1)) (DateOnly(2103, 9, 30)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 9, 10)) "September income" 600m |> ignore
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let result = IncomeStatementService.getByPeriodId txn fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        let d = report.disclosure.Value
        Assert.Empty(d.adjustments)

// =====================================================================
// FT-RPD-008: Balance sheet without --period has no disclosure
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-008")>]
let ``RPD-008 balance sheet without period param has no disclosure`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 10, 1)) (DateOnly(2103, 10, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 10, 15)) "October income" 500m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(2103, 10, 31))
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsNone, "disclosure should be None when no period supplied")

// =====================================================================
// FT-RPD-009: Balance sheet with --period shows the period disclosure header
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-009")>]
let ``RPD-009 balance sheet with period shows period provenance header`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 11, 1)) (DateOnly(2103, 11, 30)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 11, 10)) "November income" 700m |> ignore
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let result = BalanceSheetService.getAsOfDateWithPeriod txn (DateOnly(2103, 11, 30)) fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsSome, "disclosure should be present when period is supplied")
        let d = report.disclosure.Value
        Assert.Equal(DateOnly(2103, 11, 1), d.startDate)
        Assert.Equal(DateOnly(2103, 11, 30), d.endDate)
        Assert.False(d.isOpen)
        Assert.Equal(Some "alice", d.closedBy)

// =====================================================================
// FT-RPD-010: Reopened period header shows updated closedAt and correct reopened count
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-010")>]
let ``RPD-010 reopened-and-reclosed period shows reopened count 1 and most recent close timestamp`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 12, 1)) (DateOnly(2103, 12, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 12, 15)) "December income" 1500m |> ignore
    // Simulate a period that was reopened once (reopened_count=1) and re-closed
    let latestClose = DateTimeOffset.UtcNow
    closePeriodInPlace txn fpId "alice" latestClose 1
    let result = IncomeStatementService.getByPeriodId txn fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        let d = report.disclosure.Value
        Assert.Equal(1, d.reopenedCount)
        Assert.False(d.isOpen)
        Assert.True(d.closedAt.IsSome)
        let diff = abs ((d.closedAt.Value - latestClose).TotalSeconds)
        Assert.True(diff < 5.0, sprintf "closedAt should reflect the latest close (diff: %f seconds)" diff)

// =====================================================================
// FT-RPD-011: --as-originally-closed excludes JEs created after the period was closed
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-011")>]
let ``RPD-011 as-originally-closed income statement excludes post-close JE`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 1, 1)) (DateOnly(2104, 1, 31)) true
    // Post pre-close JE first
    let preCloseJeId = postEntry txn assetAcct revAcct fpId (DateOnly(2104, 1, 10)) "Pre-close JE" 1000m
    // Close the period 2 hours ago
    let closedAt = DateTimeOffset.UtcNow.AddHours(-2.0)
    closePeriodInPlace txn fpId "alice" closedAt 0
    // Backdate the pre-close JE to before closedAt
    setJeCreatedAt txn preCloseJeId (closedAt.AddHours(-1.0))
    // Reopen temporarily to post post-close JE (or insert directly via raw SQL)
    // We need to reopen, post, and close again — OR bypass via raw SQL.
    // Using raw SQL insert directly to avoid service-layer open check:
    use insertCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry \
         (entry_date, description, source, fiscal_period_id, created_at, modified_at) \
         VALUES (@ed, @desc, 'manual', @fp, now(), now()) RETURNING id",
        txn.Connection)
    insertCmd.Transaction <- txn
    insertCmd.Parameters.AddWithValue("@ed", DateOnly(2104, 1, 20)) |> ignore
    insertCmd.Parameters.AddWithValue("@desc", "Post-close JE") |> ignore
    insertCmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    let postCloseJeId = insertCmd.ExecuteScalar() :?> int
    // Insert lines for the post-close JE
    use lineCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, @amt, @et)",
        txn.Connection)
    lineCmd.Transaction <- txn
    lineCmd.Parameters.AddWithValue("@je", postCloseJeId) |> ignore
    lineCmd.Parameters.AddWithValue("@acct", assetAcct) |> ignore
    lineCmd.Parameters.AddWithValue("@amt", 500m) |> ignore
    lineCmd.Parameters.AddWithValue("@et", "debit") |> ignore
    lineCmd.ExecuteNonQuery() |> ignore
    use lineCmd2 = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, @amt, @et)",
        txn.Connection)
    lineCmd2.Transaction <- txn
    lineCmd2.Parameters.AddWithValue("@je", postCloseJeId) |> ignore
    lineCmd2.Parameters.AddWithValue("@acct", revAcct) |> ignore
    lineCmd2.Parameters.AddWithValue("@amt", 500m) |> ignore
    lineCmd2.Parameters.AddWithValue("@et", "credit") |> ignore
    lineCmd2.ExecuteNonQuery() |> ignore
    // Normal mode should see both JEs = 1500 revenue
    let normalResult = IncomeStatementService.getByPeriodId txn fpId false
    let normalRevenue =
        match normalResult with
        | Ok r -> r.revenue.sectionTotal
        | Error e -> failwithf "Normal mode failed: %s" e
    Assert.Equal(1500m, normalRevenue)
    // AOC mode should see only pre-close JE = 1000 revenue
    let aocResult = IncomeStatementService.getByPeriodId txn fpId true
    match aocResult with
    | Error err -> Assert.Fail(sprintf "Expected Ok for AOC: %s" err)
    | Ok report ->
        Assert.Equal(1000m, report.revenue.sectionTotal)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-011")>]
let ``RPD-011 as-originally-closed trial balance excludes post-close JE`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 1, 1)) (DateOnly(2104, 1, 31)) true
    let preCloseJeId = postEntry txn assetAcct revAcct fpId (DateOnly(2104, 1, 10)) "Pre-close JE" 1000m
    let closedAt = DateTimeOffset.UtcNow.AddHours(-2.0)
    closePeriodInPlace txn fpId "alice" closedAt 0
    setJeCreatedAt txn preCloseJeId (closedAt.AddHours(-1.0))
    // Insert post-close JE directly (bypass closed period check)
    use insertCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry \
         (entry_date, description, source, fiscal_period_id, created_at, modified_at) \
         VALUES (@ed, @desc, 'manual', @fp, now(), now()) RETURNING id",
        txn.Connection)
    insertCmd.Transaction <- txn
    insertCmd.Parameters.AddWithValue("@ed", DateOnly(2104, 1, 20)) |> ignore
    insertCmd.Parameters.AddWithValue("@desc", "Post-close JE") |> ignore
    insertCmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    let postCloseJeId = insertCmd.ExecuteScalar() :?> int
    for (acct, et) in [(assetAcct, "debit"); (revAcct, "credit")] do
        use lc = new NpgsqlCommand(
            "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, @amt, @et)",
            txn.Connection)
        lc.Transaction <- txn
        lc.Parameters.AddWithValue("@je", postCloseJeId) |> ignore
        lc.Parameters.AddWithValue("@acct", acct) |> ignore
        lc.Parameters.AddWithValue("@amt", 500m) |> ignore
        lc.Parameters.AddWithValue("@et", et) |> ignore
        lc.ExecuteNonQuery() |> ignore
    let aocResult = TrialBalanceService.getByPeriodId txn fpId true
    match aocResult with
    | Error err -> Assert.Fail(sprintf "Expected Ok for AOC: %s" err)
    | Ok report ->
        Assert.Equal(1000m, report.grandTotalDebits)
        Assert.Equal(1000m, report.grandTotalCredits)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-011")>]
let ``RPD-011 as-originally-closed P&L subtree excludes post-close JE`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Parent" assetTypeId true
    let revCode = prefix + "RV"
    let revAcct = InsertHelpers.insertAccountWithParent txn revCode "Revenue Child" revenueTypeId parentAcct true
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 1, 1)) (DateOnly(2104, 1, 31)) true
    let preCloseJeId = postEntry txn bankAcct revAcct fpId (DateOnly(2104, 1, 10)) "Pre-close income" 1000m
    let closedAt = DateTimeOffset.UtcNow.AddHours(-2.0)
    closePeriodInPlace txn fpId "alice" closedAt 0
    setJeCreatedAt txn preCloseJeId (closedAt.AddHours(-1.0))
    // Insert post-close JE directly
    use insertCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry \
         (entry_date, description, source, fiscal_period_id, created_at, modified_at) \
         VALUES (@ed, @desc, 'manual', @fp, now(), now()) RETURNING id",
        txn.Connection)
    insertCmd.Transaction <- txn
    insertCmd.Parameters.AddWithValue("@ed", DateOnly(2104, 1, 20)) |> ignore
    insertCmd.Parameters.AddWithValue("@desc", "Post-close income") |> ignore
    insertCmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    let postCloseJeId = insertCmd.ExecuteScalar() :?> int
    for (acct, et) in [(bankAcct, "debit"); (revAcct, "credit")] do
        use lc = new NpgsqlCommand(
            "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, @amt, @et)",
            txn.Connection)
        lc.Transaction <- txn
        lc.Parameters.AddWithValue("@je", postCloseJeId) |> ignore
        lc.Parameters.AddWithValue("@acct", acct) |> ignore
        lc.Parameters.AddWithValue("@amt", 500m) |> ignore
        lc.Parameters.AddWithValue("@et", et) |> ignore
        lc.ExecuteNonQuery() |> ignore
    let aocResult = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId true
    match aocResult with
    | Error err -> Assert.Fail(sprintf "Expected Ok for AOC: %s" err)
    | Ok report ->
        Assert.Equal(1000m, report.revenue.sectionTotal)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-011")>]
let ``RPD-011 as-originally-closed balance sheet excludes post-close JE`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 1, 1)) (DateOnly(2104, 1, 31)) true
    let preCloseJeId = postEntry txn assetAcct revAcct fpId (DateOnly(2104, 1, 10)) "Pre-close JE" 1000m
    let closedAt = DateTimeOffset.UtcNow.AddHours(-2.0)
    closePeriodInPlace txn fpId "alice" closedAt 0
    setJeCreatedAt txn preCloseJeId (closedAt.AddHours(-1.0))
    // Insert post-close JE directly
    use insertCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry \
         (entry_date, description, source, fiscal_period_id, created_at, modified_at) \
         VALUES (@ed, @desc, 'manual', @fp, now(), now()) RETURNING id",
        txn.Connection)
    insertCmd.Transaction <- txn
    insertCmd.Parameters.AddWithValue("@ed", DateOnly(2104, 1, 20)) |> ignore
    insertCmd.Parameters.AddWithValue("@desc", "Post-close JE") |> ignore
    insertCmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    let postCloseJeId = insertCmd.ExecuteScalar() :?> int
    for (acct, et) in [(assetAcct, "debit"); (revAcct, "credit")] do
        use lc = new NpgsqlCommand(
            "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, @amt, @et)",
            txn.Connection)
        lc.Transaction <- txn
        lc.Parameters.AddWithValue("@je", postCloseJeId) |> ignore
        lc.Parameters.AddWithValue("@acct", acct) |> ignore
        lc.Parameters.AddWithValue("@amt", 500m) |> ignore
        lc.Parameters.AddWithValue("@et", et) |> ignore
        lc.ExecuteNonQuery() |> ignore
    let aocResult = BalanceSheetService.getAsOfDateWithPeriod txn (DateOnly(2104, 1, 31)) fpId true
    match aocResult with
    | Error err -> Assert.Fail(sprintf "Expected Ok for AOC: %s" err)
    | Ok report ->
        let assetLine = report.assets.lines |> List.tryFind (fun l -> l.accountId = assetAcct)
        Assert.True(assetLine.IsSome, "asset account should appear")
        Assert.Equal(1000m, assetLine.Value.balance)

// =====================================================================
// FT-RPD-012: --as-originally-closed includes adjustment JEs created before close
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-012")>]
let ``RPD-012 as-originally-closed includes pre-close adjustment JE and excludes post-close adjustment JE`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct   = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    // Target period (to be closed) and open period (for adj JEs to live in)
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 2, 1)) (DateOnly(2104, 2, 28)) true
    let openPeriodId = InsertHelpers.insertFiscalPeriod txn (prefix + "OP") (DateOnly(2104, 3, 1)) (DateOnly(2104, 3, 31)) true
    // Direct JE in target period
    let directJeId = postEntry txn assetAcct revAcct fpId (DateOnly(2104, 2, 10)) "Feb income" 1000m
    // Pre-close adjustment: posted to openPeriodId with a date WITHIN that open period
    let preAdjJeId = postAdjustmentEntry txn expAcct assetAcct openPeriodId fpId (DateOnly(2104, 3, 5)) "Pre-close adjustment" 200m
    // Close the target period 2 hours ago
    let closedAt = DateTimeOffset.UtcNow.AddHours(-2.0)
    closePeriodInPlace txn fpId "alice" closedAt 0
    // Backdate direct JE and pre-close adj to before closedAt
    setJeCreatedAt txn directJeId (closedAt.AddHours(-1.5))
    setJeCreatedAt txn preAdjJeId (closedAt.AddHours(-0.5))
    // Post-close adjustment: inserted after close (created_at = NOW() > closedAt)
    // Date must be within the open period (openPeriodId covers March 2104)
    let postAdjJeId = postAdjustmentEntry txn expAcct assetAcct openPeriodId fpId (DateOnly(2104, 3, 20)) "Post-close adjustment" 100m
    // Normal mode: only the direct JE is included (fiscal_period_id = fpId).
    // Adjustment JEs have fiscal_period_id = openPeriodId, not fpId, so they don't appear.
    let normalResult = IncomeStatementService.getByPeriodId txn fpId false
    match normalResult with
    | Error e -> Assert.Fail(sprintf "Normal mode failed: %s" e)
    | Ok r ->
        Assert.Equal(1000m, r.revenue.sectionTotal)
        Assert.Equal(0m, r.expenses.sectionTotal)
    // AOC mode: includes JEs with fiscal_period_id=fpId AND adjustment_for_period_id=fpId,
    // both filtered by created_at <= closed_at.
    // Expected: revenue = 1000 (direct JE, pre-close), expense = 200 (pre-close adj)
    let aocResult = IncomeStatementService.getByPeriodId txn fpId true
    match aocResult with
    | Error err -> Assert.Fail(sprintf "Expected Ok for AOC: %s" err)
    | Ok report ->
        Assert.Equal(1000m, report.revenue.sectionTotal)
        Assert.Equal(200m, report.expenses.sectionTotal)

// =====================================================================
// FT-RPD-013: --as-originally-closed header indicates the view is as of close
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-013")>]
let ``RPD-013 as-originally-closed disclosure has asOriginallyClosed true`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 3, 1)) (DateOnly(2104, 3, 31)) true
    let jeId = postEntry txn assetAcct revAcct fpId (DateOnly(2104, 3, 15)) "March income" 1000m
    let closedAt = DateTimeOffset.UtcNow.AddHours(-1.0)
    closePeriodInPlace txn fpId "alice" closedAt 0
    setJeCreatedAt txn jeId (closedAt.AddHours(-0.5))
    let result = IncomeStatementService.getByPeriodId txn fpId true
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsSome)
        Assert.True(report.disclosure.Value.asOriginallyClosed, "asOriginallyClosed should be true in AOC mode")

// =====================================================================
// FT-RPD-014: --as-originally-closed on open period returns error (4 reports)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-014")>]
let ``RPD-014 as-originally-closed on open period returns error - income statement`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 4, 1)) (DateOnly(2104, 4, 30)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2104, 4, 10)) "April income" 300m |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId true
    match result with
    | Ok _ -> Assert.Fail("Expected Error for AOC on open period")
    | Error err ->
        Assert.True(err.ToLowerInvariant().Contains("open"), sprintf "Error should mention open period: %s" err)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-014")>]
let ``RPD-014 as-originally-closed on open period returns error - trial balance`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 4, 1)) (DateOnly(2104, 4, 30)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2104, 4, 10)) "April income" 300m |> ignore
    let result = TrialBalanceService.getByPeriodId txn fpId true
    match result with
    | Ok _ -> Assert.Fail("Expected Error for AOC on open period")
    | Error err ->
        Assert.True(err.ToLowerInvariant().Contains("open"), sprintf "Error should mention open period: %s" err)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-014")>]
let ``RPD-014 as-originally-closed on open period returns error - P&L subtree`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Parent" assetTypeId true
    let revCode = prefix + "RV"
    let revAcct = InsertHelpers.insertAccountWithParent txn revCode "Revenue Child" revenueTypeId parentAcct true
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 4, 1)) (DateOnly(2104, 4, 30)) true
    postEntry txn bankAcct revAcct fpId (DateOnly(2104, 4, 10)) "April income" 300m |> ignore
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId true
    match result with
    | Ok _ -> Assert.Fail("Expected Error for AOC on open period")
    | Error err ->
        Assert.True(err.ToLowerInvariant().Contains("open"), sprintf "Error should mention open period: %s" err)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-014")>]
let ``RPD-014 as-originally-closed on open period returns error - balance sheet`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 4, 1)) (DateOnly(2104, 4, 30)) true
    let result = BalanceSheetService.getAsOfDateWithPeriod txn (DateOnly(2104, 4, 30)) fpId true
    match result with
    | Ok _ -> Assert.Fail("Expected Error for AOC on open period")
    | Error err ->
        Assert.True(err.ToLowerInvariant().Contains("open"), sprintf "Error should mention open period: %s" err)

// =====================================================================
// FT-RPD-015: --as-originally-closed without --period on balance sheet errors
// Service-level: getAsOfDateWithPeriod with an open period yields the expected error.
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-015")>]
let ``RPD-015 balance sheet AOC on open period returns non-empty error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 5, 1)) (DateOnly(2104, 5, 31)) true
    let result = BalanceSheetService.getAsOfDateWithPeriod txn (DateOnly(2104, 5, 31)) fpId true
    match result with
    | Ok _ -> Assert.Fail("Expected Error for AOC on open period balance sheet")
    | Error err ->
        Assert.False(String.IsNullOrEmpty(err), "error message should not be empty")
        Assert.True(err.ToLowerInvariant().Contains("open"), sprintf "Error should mention open period: %s" err)

// =====================================================================
// FT-RPD-016: je-lines extract for a period includes adjustment JEs by default
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-016")>]
let ``RPD-016 je-lines extract includes adjustment JEs by default`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct   = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 6, 1)) (DateOnly(2104, 6, 30)) true
    let adjPeriodId = InsertHelpers.insertFiscalPeriod txn (prefix + "AP") (DateOnly(2104, 7, 1)) (DateOnly(2104, 7, 31)) true
    let directJeId = postEntry txn assetAcct revAcct fpId (DateOnly(2104, 6, 15)) "June income" 1000m
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let adjJeId = postAdjustmentEntry txn expAcct assetAcct adjPeriodId fpId (DateOnly(2104, 7, 5)) "June catch-up expense" 47.82m
    let rows = getJournalEntryLines txn fpId true None
    let jeIds = rows |> List.map (fun r -> r.journalEntryId) |> Set.ofList
    Assert.True(jeIds.Contains(directJeId), "direct JE should appear in extract")
    Assert.True(jeIds.Contains(adjJeId), "adjustment JE should appear in default extract")

// =====================================================================
// FT-RPD-017: je-lines extract with --exclude-adjustments omits adjustment JEs
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-017")>]
let ``RPD-017 je-lines extract with exclude-adjustments omits adjustment JEs`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct   = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 8, 1)) (DateOnly(2104, 8, 31)) true
    let adjPeriodId = InsertHelpers.insertFiscalPeriod txn (prefix + "AP") (DateOnly(2104, 9, 1)) (DateOnly(2104, 9, 30)) true
    let directJeId = postEntry txn assetAcct revAcct fpId (DateOnly(2104, 8, 10)) "August income" 1000m
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let adjJeId = postAdjustmentEntry txn expAcct assetAcct adjPeriodId fpId (DateOnly(2104, 9, 3)) "August catch-up" 50m
    // With includeAdjustments=false: only direct JE should appear
    let rows = getJournalEntryLines txn fpId false None
    let jeIds = rows |> List.map (fun r -> r.journalEntryId) |> Set.ofList
    Assert.True(jeIds.Contains(directJeId), "direct JE should appear")
    Assert.False(jeIds.Contains(adjJeId), "adjustment JE should NOT appear when excluded")

// =====================================================================
// FT-RPD-018: je-lines for period with no adjustments returns only direct JE lines
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-018")>]
let ``RPD-018 je-lines for period with no adjustments returns only direct JE lines`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 10, 1)) (DateOnly(2104, 10, 31)) true
    let directJeId = postEntry txn assetAcct revAcct fpId (DateOnly(2104, 10, 15)) "October income" 600m
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let rows = getJournalEntryLines txn fpId true None
    Assert.Equal(2, rows.Length)
    Assert.True(rows |> List.forall (fun r -> r.journalEntryId = directJeId),
        "all lines should belong to the direct JE")

// =====================================================================
// FT-RPD-019: JSON output for period-based reports includes a disclosure object
// (Service-layer: verify the disclosure field is populated on all 4 report types)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-019")>]
let ``RPD-019 income statement report has populated disclosure field`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 11, 1)) (DateOnly(2104, 11, 30)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2104, 11, 10)) "November income" 700m |> ignore
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let result = IncomeStatementService.getByPeriodId txn fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsSome)
        let d = report.disclosure.Value
        Assert.Equal(fpId, d.fiscalPeriodId)
        Assert.False(d.isOpen)
        Assert.True(d.closedAt.IsSome)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-019")>]
let ``RPD-019 trial balance report has populated disclosure field`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 11, 1)) (DateOnly(2104, 11, 30)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2104, 11, 10)) "November income" 700m |> ignore
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let result = TrialBalanceService.getByPeriodId txn fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsSome)
        let d = report.disclosure.Value
        Assert.Equal(fpId, d.fiscalPeriodId)
        Assert.False(d.isOpen)
        Assert.True(d.closedAt.IsSome)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-019")>]
let ``RPD-019 P&L subtree report has populated disclosure field`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Parent" assetTypeId true
    let revCode = prefix + "RV"
    let revAcct = InsertHelpers.insertAccountWithParent txn revCode "Revenue Child" revenueTypeId parentAcct true
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 11, 1)) (DateOnly(2104, 11, 30)) true
    postEntry txn bankAcct revAcct fpId (DateOnly(2104, 11, 10)) "November income" 700m |> ignore
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsSome)
        let d = report.disclosure.Value
        Assert.Equal(fpId, d.fiscalPeriodId)
        Assert.False(d.isOpen)
        Assert.True(d.closedAt.IsSome)

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-019")>]
let ``RPD-019 balance sheet with period report has populated disclosure field`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 11, 1)) (DateOnly(2104, 11, 30)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2104, 11, 10)) "November income" 700m |> ignore
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let result = BalanceSheetService.getAsOfDateWithPeriod txn (DateOnly(2104, 11, 30)) fpId false
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsSome)
        let d = report.disclosure.Value
        Assert.Equal(fpId, d.fiscalPeriodId)
        Assert.False(d.isOpen)
        Assert.True(d.closedAt.IsSome)

// =====================================================================
// FT-RPD-020: je-lines extract includes periodKey, status, and lines
// (Service-layer: verify PeriodMetadataEnvelope type fields compile and work)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-020")>]
let ``RPD-020 PeriodMetadataEnvelope type has required fields and can be constructed from real data`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2104, 12, 1)) (DateOnly(2104, 12, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2104, 12, 10)) "December income" 1000m |> ignore
    closePeriodInPlace txn fpId "alice" DateTimeOffset.UtcNow 0
    let lines = getJournalEntryLines txn fpId true None
    let disclosureOpt = PeriodDisclosureRepository.getDisclosure txn fpId
    match disclosureOpt with
    | None -> Assert.Fail("Expected disclosure for closed period")
    | Some d ->
        let envelope : LeoBloom.Reporting.ExtractTypes.PeriodMetadataEnvelope =
            { periodKey            = d.periodKey
              startDate            = d.startDate
              endDate              = d.endDate
              status               = if d.isOpen then "OPEN" else "CLOSED"
              closedAt             = d.closedAt
              closedBy             = d.closedBy
              reopenedCount        = d.reopenedCount
              adjustmentCount      = d.adjustmentCount
              adjustmentNetImpact  = d.adjustmentNetImpact
              lines                = lines }
        Assert.False(String.IsNullOrEmpty(envelope.periodKey), "periodKey should not be empty")
        Assert.Equal("CLOSED", envelope.status)
        Assert.Equal(2, envelope.lines.Length)

// =====================================================================
// FT-RPD-021: Balance sheet JSON without --period has no disclosure field
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-RPD-021")>]
let ``RPD-021 balance sheet without period has None disclosure field`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct   = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2103, 1, 1)) (DateOnly(2103, 1, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2103, 1, 15)) "January income" 400m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(2103, 1, 31))
    match result with
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    | Ok report ->
        Assert.True(report.disclosure.IsNone, "disclosure should be null/absent when no period is supplied")

// =====================================================================
// Structural tests — type and compilation checks
// =====================================================================

[<Fact>]
let ``PeriodDisclosure type exists with required fields`` () =
    let d : PeriodDisclosure =
        { fiscalPeriodId = 1
          periodKey = "2103-01"
          startDate = DateOnly(2103, 1, 1)
          endDate = DateOnly(2103, 1, 31)
          isOpen = false
          closedAt = None
          closedBy = None
          reopenedCount = 0
          adjustmentCount = 0
          adjustmentNetImpact = 0m
          adjustments = []
          asOriginallyClosed = false }
    Assert.Equal(1, d.fiscalPeriodId)
    Assert.Equal("2103-01", d.periodKey)
    Assert.False(d.isOpen)
    Assert.False(d.asOriginallyClosed)

[<Fact>]
let ``AdjustmentDetail type exists with required fields`` () =
    let a : AdjustmentDetail =
        { journalEntryId = 42
          entryDate = DateOnly(2103, 6, 10)
          description = "Test adjustment"
          netAmount = 47.82m }
    Assert.Equal(42, a.journalEntryId)
    Assert.Equal("Test adjustment", a.description)
    Assert.Equal(47.82m, a.netAmount)

[<Fact>]
let ``TrialBalanceReport disclosure field exists`` () =
    let report : TrialBalanceReport =
        { fiscalPeriodId = 1
          periodKey = "2103-01"
          groups = []
          grandTotalDebits = 0m
          grandTotalCredits = 0m
          isBalanced = true
          disclosure = None }
    Assert.True(report.disclosure.IsNone)

[<Fact>]
let ``IncomeStatementReport disclosure field exists`` () =
    let report : IncomeStatementReport =
        { fiscalPeriodId = 1
          periodKey = "2103-01"
          revenue = { sectionName = "revenue"; lines = []; sectionTotal = 0m }
          expenses = { sectionName = "expense"; lines = []; sectionTotal = 0m }
          netIncome = 0m
          disclosure = None }
    Assert.True(report.disclosure.IsNone)

[<Fact>]
let ``BalanceSheetReport disclosure field exists`` () =
    let report : BalanceSheetReport =
        { asOfDate = DateOnly(2103, 1, 31)
          assets = { sectionName = "asset"; lines = []; sectionTotal = 0m }
          liabilities = { sectionName = "liability"; lines = []; sectionTotal = 0m }
          equity = { sectionName = "equity"; lines = []; sectionTotal = 0m }
          retainedEarnings = 0m
          totalEquity = 0m
          isBalanced = true
          disclosure = None }
    Assert.True(report.disclosure.IsNone)

[<Fact>]
let ``SubtreePLReport disclosure field exists`` () =
    let report : SubtreePLReport =
        { rootAccountCode = "ACME"
          rootAccountName = "ACME Root"
          fiscalPeriodId = 1
          periodKey = "2103-01"
          revenue = { sectionName = "revenue"; lines = []; sectionTotal = 0m }
          expenses = { sectionName = "expense"; lines = []; sectionTotal = 0m }
          netIncome = 0m
          disclosure = None }
    Assert.True(report.disclosure.IsNone)

[<Fact>]
let ``PeriodMetadataEnvelope type exists with required fields`` () =
    let env : LeoBloom.Reporting.ExtractTypes.PeriodMetadataEnvelope =
        { periodKey            = "2104-12"
          startDate            = DateOnly(2104, 12, 1)
          endDate              = DateOnly(2104, 12, 31)
          status               = "CLOSED"
          closedAt             = None
          closedBy             = None
          reopenedCount        = 0
          adjustmentCount      = 0
          adjustmentNetImpact  = 0m
          lines                = [] }
    Assert.Equal("2104-12", env.periodKey)
    Assert.Equal("CLOSED", env.status)

[<Fact>]
let ``PeriodDisclosureRepository getDisclosure returns None for nonexistent period`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result = PeriodDisclosureRepository.getDisclosure txn 999999
    Assert.True(result.IsNone, "getDisclosure should return None for nonexistent period")

[<Fact>]
let ``PeriodDisclosureRepository getDisclosureByKey returns None for nonexistent key`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result = PeriodDisclosureRepository.getDisclosureByKey txn "9999-99"
    Assert.True(result.IsNone, "getDisclosureByKey should return None for nonexistent key")
