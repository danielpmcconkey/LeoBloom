module LeoBloom.Tests.TrialBalanceTests

open System
open Xunit
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

// Standard account type IDs (seeded in the database)
let private assetTypeId = 1
let private liabilityTypeId = 2
let private equityTypeId = 3
let private revenueTypeId = 4
let private expenseTypeId = 5

let private postEntry conn tracker acct1 acct2 fpId (entryDate: DateOnly) (desc: string) (amount: decimal) =
    let cmd =
        { entryDate = entryDate
          description = desc
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = amount; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = amount; entryType = EntryType.Credit; memo = None } ]
          references = [] }
    match JournalEntryService.post cmd with
    | Ok posted ->
        TestCleanup.trackJournalEntry posted.entry.id tracker
        posted.entry.id
    | Error errs -> failwith (sprintf "Setup post failed: %A" errs)

let private postMultiLineEntry tracker fpId (entryDate: DateOnly) (desc: string) (lines: PostLineCommand list) =
    let cmd =
        { entryDate = entryDate
          description = desc
          source = Some "manual"
          fiscalPeriodId = fpId
          lines = lines
          references = [] }
    match JournalEntryService.post cmd with
    | Ok posted ->
        TestCleanup.trackJournalEntry posted.entry.id tracker
        posted.entry.id
    | Error errs -> failwith (sprintf "Setup post failed: %A" errs)

// =====================================================================
// Behavioral tests — mapped to Gherkin scenarios
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-TB-001")>]
let ``balanced entries produce a balanced trial balance`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let assetAcct = InsertHelpers.insertAccount conn tracker (prefix + "AS") "Asset" assetTypeId true
        let revAcct = InsertHelpers.insertAccount conn tracker (prefix + "RV") "Revenue" revenueTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "March rent" 500m |> ignore
        let result = TrialBalanceService.getByPeriodId fpId
        match result with
        | Ok report ->
            Assert.True(report.isBalanced)
            Assert.Equal(500m, report.grandTotalDebits)
            Assert.Equal(500m, report.grandTotalCredits)
            let allLines = report.groups |> List.collect (fun g -> g.lines)
            Assert.Equal(2, allLines.Length)
            Assert.Equal(fpId, report.fiscalPeriodId)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-TB-002")>]
let ``report groups accounts by type with correct subtotals`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let assetAcct1 = InsertHelpers.insertAccount conn tracker (prefix + "A1") "Asset1" assetTypeId true
        let assetAcct2 = InsertHelpers.insertAccount conn tracker (prefix + "A2") "Asset2" assetTypeId true
        let revAcct = InsertHelpers.insertAccount conn tracker (prefix + "RV") "Revenue" revenueTypeId true
        let expAcct = InsertHelpers.insertAccount conn tracker (prefix + "EX") "Expense" expenseTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        // Entry 1: debit asset1 600, debit asset2 400, credit revenue 1000
        postMultiLineEntry tracker fpId (DateOnly(2026, 3, 10)) "Income"
            [ { accountId = assetAcct1; amount = 600m; entryType = EntryType.Debit; memo = None }
              { accountId = assetAcct2; amount = 400m; entryType = EntryType.Debit; memo = None }
              { accountId = revAcct; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
            |> ignore
        // Entry 2: debit expense 200, credit asset1 200
        postEntry conn tracker expAcct assetAcct1 fpId (DateOnly(2026, 3, 20)) "Supplies" 200m |> ignore
        let result = TrialBalanceService.getByPeriodId fpId
        match result with
        | Ok report ->
            Assert.Equal(3, report.groups.Length)
            // Standard order: asset, revenue, expense
            Assert.Equal("asset", report.groups.[0].accountTypeName)
            Assert.Equal("revenue", report.groups.[1].accountTypeName)
            Assert.Equal("expense", report.groups.[2].accountTypeName)
            // Asset group: debit total = 600 + 400 = 1000, credit total = 200
            Assert.Equal(1000m, report.groups.[0].groupDebitTotal)
            Assert.Equal(200m, report.groups.[0].groupCreditTotal)
            // Revenue group: debit total = 0, credit total = 1000
            Assert.Equal(0m, report.groups.[1].groupDebitTotal)
            Assert.Equal(1000m, report.groups.[1].groupCreditTotal)
            // Expense group: debit total = 200, credit total = 0
            Assert.Equal(200m, report.groups.[2].groupDebitTotal)
            Assert.Equal(0m, report.groups.[2].groupCreditTotal)
            Assert.True(report.isBalanced)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-TB-003")>]
let ``groups with no activity are omitted`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let assetAcct = InsertHelpers.insertAccount conn tracker (prefix + "AS") "Asset" assetTypeId true
        let revAcct = InsertHelpers.insertAccount conn tracker (prefix + "RV") "Revenue" revenueTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Simple entry" 300m |> ignore
        let result = TrialBalanceService.getByPeriodId fpId
        match result with
        | Ok report ->
            // Only asset and revenue have activity — exactly 2 groups
            Assert.Equal(2, report.groups.Length)
            let groupNames = report.groups |> List.map (fun g -> g.accountTypeName)
            Assert.Contains("asset", groupNames)
            Assert.Contains("revenue", groupNames)
            // Liability, equity, and expense should not appear
            Assert.DoesNotContain("liability", groupNames)
            Assert.DoesNotContain("equity", groupNames)
            Assert.DoesNotContain("expense", groupNames)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-TB-004")>]
let ``voided entries excluded from trial balance`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let assetAcct = InsertHelpers.insertAccount conn tracker (prefix + "AS") "Asset" assetTypeId true
        let revAcct = InsertHelpers.insertAccount conn tracker (prefix + "RV") "Revenue" revenueTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        let _entryId1 = postEntry conn tracker assetAcct revAcct fpId (DateOnly(2026, 3, 10)) "Good entry" 500m
        let entryId2 = postEntry conn tracker assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Bad entry" 200m
        let voidCmd = { journalEntryId = entryId2; voidReason = "Test void" }
        match JournalEntryService.voidEntry voidCmd with
        | Error errs -> Assert.Fail(sprintf "Void failed: %A" errs)
        | Ok _ -> ()
        let result = TrialBalanceService.getByPeriodId fpId
        match result with
        | Ok report ->
            Assert.Equal(500m, report.grandTotalDebits)
            Assert.Equal(500m, report.grandTotalCredits)
            Assert.True(report.isBalanced)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-TB-005")>]
let ``empty period returns balanced report with zero totals`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let result = TrialBalanceService.getByPeriodId fpId
        match result with
        | Ok report ->
            Assert.True(report.isBalanced)
            Assert.Equal(0m, report.grandTotalDebits)
            Assert.Equal(0m, report.grandTotalCredits)
            Assert.Empty(report.groups)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-TB-006")>]
let ``closed period trial balance still works`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let assetAcct = InsertHelpers.insertAccount conn tracker (prefix + "AS") "Asset" assetTypeId true
        let revAcct = InsertHelpers.insertAccount conn tracker (prefix + "RV") "Revenue" revenueTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2025, 12, 1)) (DateOnly(2025, 12, 31)) true
        postEntry conn tracker assetAcct revAcct fpId (DateOnly(2025, 12, 15)) "December entry" 800m |> ignore
        // Close the period
        use closeCmd = new NpgsqlCommand("UPDATE ledger.fiscal_period SET is_open = false WHERE id = @id", conn)
        closeCmd.Parameters.AddWithValue("@id", fpId) |> ignore
        closeCmd.ExecuteNonQuery() |> ignore
        let result = TrialBalanceService.getByPeriodId fpId
        match result with
        | Ok report ->
            Assert.True(report.isBalanced)
            Assert.Equal(800m, report.grandTotalDebits)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-TB-007")>]
let ``multiple entries in same period accumulate per account`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let assetAcct = InsertHelpers.insertAccount conn tracker (prefix + "AS") "Asset" assetTypeId true
        let revAcct = InsertHelpers.insertAccount conn tracker (prefix + "RV") "Revenue" revenueTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker assetAcct revAcct fpId (DateOnly(2026, 3, 10)) "First payment" 500m |> ignore
        postEntry conn tracker assetAcct revAcct fpId (DateOnly(2026, 3, 20)) "Second payment" 300m |> ignore
        let result = TrialBalanceService.getByPeriodId fpId
        match result with
        | Ok report ->
            let allLines = report.groups |> List.collect (fun g -> g.lines)
            let assetLine = allLines |> List.find (fun l -> l.accountId = assetAcct)
            let revLine = allLines |> List.find (fun l -> l.accountId = revAcct)
            Assert.Equal(800m, assetLine.debitTotal)
            Assert.Equal(0m, assetLine.creditTotal)
            Assert.Equal(0m, revLine.debitTotal)
            Assert.Equal(800m, revLine.creditTotal)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-TB-008")>]
let ``net balance uses normal_balance formula`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let assetAcct = InsertHelpers.insertAccount conn tracker (prefix + "AS") "Asset" assetTypeId true
        let revAcct = InsertHelpers.insertAccount conn tracker (prefix + "RV") "Revenue" revenueTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Net test" 700m |> ignore
        let result = TrialBalanceService.getByPeriodId fpId
        match result with
        | Ok report ->
            let allLines = report.groups |> List.collect (fun g -> g.lines)
            let assetLine = allLines |> List.find (fun l -> l.accountId = assetAcct)
            let revLine = allLines |> List.find (fun l -> l.accountId = revAcct)
            // Asset (normal-debit): netBalance = debitTotal - creditTotal = 700 - 0 = 700 (positive)
            Assert.Equal(NormalBalance.Debit, assetLine.normalBalance)
            Assert.Equal(700m, assetLine.netBalance)
            // Revenue (normal-credit): netBalance = creditTotal - debitTotal = 700 - 0 = 700 (positive)
            Assert.Equal(NormalBalance.Credit, revLine.normalBalance)
            Assert.Equal(700m, revLine.netBalance)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-TB-009")>]
let ``lookup by period key returns same result as by period ID`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let assetAcct = InsertHelpers.insertAccount conn tracker (prefix + "AS") "Asset" assetTypeId true
        let revAcct = InsertHelpers.insertAccount conn tracker (prefix + "RV") "Revenue" revenueTypeId true
        let periodKey = prefix + "FP"
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker periodKey (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Lookup test" 250m |> ignore
        let resultById = TrialBalanceService.getByPeriodId fpId
        let resultByKey = TrialBalanceService.getByPeriodKey periodKey
        match resultById, resultByKey with
        | Ok reportById, Ok reportByKey ->
            Assert.Equal(reportById.grandTotalDebits, reportByKey.grandTotalDebits)
            Assert.Equal(reportById.grandTotalCredits, reportByKey.grandTotalCredits)
            Assert.Equal(reportById.isBalanced, reportByKey.isBalanced)
            Assert.Equal(reportById.groups.Length, reportByKey.groups.Length)
            Assert.Equal(reportById.fiscalPeriodId, reportByKey.fiscalPeriodId)
            Assert.Equal(reportById.periodKey, reportByKey.periodKey)
        | Error err, _ -> Assert.Fail(sprintf "getByPeriodId failed: %s" err)
        | _, Error err -> Assert.Fail(sprintf "getByPeriodKey failed: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-TB-010")>]
let ``nonexistent period ID returns error`` () =
    let result = TrialBalanceService.getByPeriodId 999999
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent period ID")
    | Error err ->
        Assert.Contains("does not exist", err)

[<Fact>]
[<Trait("GherkinId", "FT-TB-011")>]
let ``nonexistent period key returns error`` () =
    let result = TrialBalanceService.getByPeriodKey "9999-99"
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent period key")
    | Error err ->
        Assert.Contains("does not exist", err)

// =====================================================================
// Structural tests — acceptance criteria owned by QE
// =====================================================================

[<Fact>]
let ``TrialBalanceReport type has required fields`` () =
    // Verify the type exists and has the expected fields by constructing one
    let report : TrialBalanceReport =
        { fiscalPeriodId = 1
          periodKey = "2026-03"
          groups = []
          grandTotalDebits = 0m
          grandTotalCredits = 0m
          isBalanced = true }
    Assert.Equal(1, report.fiscalPeriodId)
    Assert.Equal("2026-03", report.periodKey)
    Assert.Empty(report.groups)
    Assert.Equal(0m, report.grandTotalDebits)
    Assert.Equal(0m, report.grandTotalCredits)
    Assert.True(report.isBalanced)

[<Fact>]
let ``TrialBalanceGroup type has required fields`` () =
    let group : TrialBalanceGroup =
        { accountTypeName = "asset"
          lines = []
          groupDebitTotal = 100m
          groupCreditTotal = 50m }
    Assert.Equal("asset", group.accountTypeName)
    Assert.Empty(group.lines)
    Assert.Equal(100m, group.groupDebitTotal)
    Assert.Equal(50m, group.groupCreditTotal)

[<Fact>]
let ``TrialBalanceAccountLine type has required fields`` () =
    let line : TrialBalanceAccountLine =
        { accountId = 1
          accountCode = "1010"
          accountName = "Cash"
          accountTypeName = "asset"
          normalBalance = NormalBalance.Debit
          debitTotal = 500m
          creditTotal = 200m
          netBalance = 300m }
    Assert.Equal(1, line.accountId)
    Assert.Equal("1010", line.accountCode)
    Assert.Equal("Cash", line.accountName)
    Assert.Equal("asset", line.accountTypeName)
    Assert.Equal(NormalBalance.Debit, line.normalBalance)
    Assert.Equal(500m, line.debitTotal)
    Assert.Equal(200m, line.creditTotal)
    Assert.Equal(300m, line.netBalance)

[<Fact>]
let ``getByPeriodId returns Result of TrialBalanceReport or string`` () =
    // Call with a nonexistent ID to verify the return type is Result<TrialBalanceReport, string>
    let result : Result<TrialBalanceReport, string> = TrialBalanceService.getByPeriodId 999998
    match result with
    | Error err -> Assert.Contains("does not exist", err)
    | Ok _ -> Assert.Fail("Expected Error for nonexistent period")

[<Fact>]
let ``getByPeriodKey returns Result of TrialBalanceReport or string`` () =
    // Call with a nonexistent key to verify the return type is Result<TrialBalanceReport, string>
    let result : Result<TrialBalanceReport, string> = TrialBalanceService.getByPeriodKey "0000-00"
    match result with
    | Error err -> Assert.Contains("does not exist", err)
    | Ok _ -> Assert.Fail("Expected Error for nonexistent period")
