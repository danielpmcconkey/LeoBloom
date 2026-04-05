module LeoBloom.Tests.SubtreePLTests

open System
open Xunit
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
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

// =====================================================================
// Behavioral tests -- mapped to Gherkin scenarios
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SPL-001")>]
let ``subtree with revenue and expense children produces correct P&L`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        // Parent account (asset — won't appear in P&L itself)
        let parentCode = prefix + "PA"
        let _parentAcct = InsertHelpers.insertAccount conn tracker parentCode "Parent" assetTypeId true
        // Revenue child under parent
        let revCode = prefix + "RV"
        let revAcct = InsertHelpers.insertAccountWithParent conn tracker revCode "Revenue Child" revenueTypeId parentCode true
        // Expense child under parent
        let expCode = prefix + "EX"
        let expAcct = InsertHelpers.insertAccountWithParent conn tracker expCode "Expense Child" expenseTypeId parentCode true
        // Asset account for the other side of entries (outside subtree)
        let bankAcct = InsertHelpers.insertAccount conn tracker (prefix + "BK") "Bank" assetTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker bankAcct revAcct fpId (DateOnly(2026, 3, 10)) "Service income" 1000m |> ignore
        postEntry conn tracker expAcct bankAcct fpId (DateOnly(2026, 3, 20)) "Office supplies" 400m |> ignore
        let result = SubtreePLService.getByAccountCodeAndPeriodId parentCode fpId
        match result with
        | Ok report ->
            Assert.Equal(parentCode, report.rootAccountCode)
            Assert.Equal(1000m, report.revenue.sectionTotal)
            Assert.Equal(400m, report.expenses.sectionTotal)
            Assert.Equal(600m, report.netIncome)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SPL-002")>]
let ``subtree with only revenue descendants shows empty expense section`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let parentCode = prefix + "PA"
        let _parentAcct = InsertHelpers.insertAccount conn tracker parentCode "Parent" assetTypeId true
        let revCode = prefix + "RV"
        let revAcct = InsertHelpers.insertAccountWithParent conn tracker revCode "Revenue Child" revenueTypeId parentCode true
        let bankAcct = InsertHelpers.insertAccount conn tracker (prefix + "BK") "Bank" assetTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker bankAcct revAcct fpId (DateOnly(2026, 3, 15)) "Revenue only" 750m |> ignore
        let result = SubtreePLService.getByAccountCodeAndPeriodId parentCode fpId
        match result with
        | Ok report ->
            Assert.Equal(750m, report.revenue.sectionTotal)
            Assert.Equal(1, report.revenue.lines.Length)
            Assert.Equal(0m, report.expenses.sectionTotal)
            Assert.Equal(0, report.expenses.lines.Length)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SPL-003")>]
let ``subtree with only expense descendants shows empty revenue section`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let parentCode = prefix + "PA"
        let _parentAcct = InsertHelpers.insertAccount conn tracker parentCode "Parent" assetTypeId true
        let expCode = prefix + "EX"
        let expAcct = InsertHelpers.insertAccountWithParent conn tracker expCode "Expense Child" expenseTypeId parentCode true
        let bankAcct = InsertHelpers.insertAccount conn tracker (prefix + "BK") "Bank" assetTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker expAcct bankAcct fpId (DateOnly(2026, 3, 15)) "Expense only" 300m |> ignore
        let result = SubtreePLService.getByAccountCodeAndPeriodId parentCode fpId
        match result with
        | Ok report ->
            Assert.Equal(300m, report.expenses.sectionTotal)
            Assert.Equal(1, report.expenses.lines.Length)
            Assert.Equal(0m, report.revenue.sectionTotal)
            Assert.Equal(0, report.revenue.lines.Length)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SPL-004")>]
let ``root account with no children returns single-account P&L`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let revCode = prefix + "RV"
        let revAcct = InsertHelpers.insertAccount conn tracker revCode "Solo Revenue" revenueTypeId true
        let bankAcct = InsertHelpers.insertAccount conn tracker (prefix + "BK") "Bank" assetTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker bankAcct revAcct fpId (DateOnly(2026, 3, 15)) "Solo income" 500m |> ignore
        let result = SubtreePLService.getByAccountCodeAndPeriodId revCode fpId
        match result with
        | Ok report ->
            Assert.Equal(revCode, report.rootAccountCode)
            Assert.Equal(1, report.revenue.lines.Length)
            Assert.Equal(500m, report.revenue.sectionTotal)
            Assert.Equal(500m, report.netIncome)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SPL-005")>]
let ``root account not revenue or expense with no rev/exp descendants returns empty report`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let parentCode = prefix + "PA"
        let _parentAcct = InsertHelpers.insertAccount conn tracker parentCode "Asset Parent" assetTypeId true
        // Child is also an asset — no revenue/expense in subtree
        let _childAcct = InsertHelpers.insertAccountWithParent conn tracker (prefix + "CH") "Asset Child" assetTypeId parentCode true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        let result = SubtreePLService.getByAccountCodeAndPeriodId parentCode fpId
        match result with
        | Ok report ->
            Assert.Equal(0m, report.revenue.sectionTotal)
            Assert.Equal(0m, report.expenses.sectionTotal)
            Assert.Equal(0m, report.netIncome)
            Assert.Equal(0, report.revenue.lines.Length)
            Assert.Equal(0, report.expenses.lines.Length)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SPL-006")>]
let ``voided entries excluded from subtree P&L`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let parentCode = prefix + "PA"
        let _parentAcct = InsertHelpers.insertAccount conn tracker parentCode "Parent" assetTypeId true
        let revCode = prefix + "RV"
        let revAcct = InsertHelpers.insertAccountWithParent conn tracker revCode "Revenue Child" revenueTypeId parentCode true
        let bankAcct = InsertHelpers.insertAccount conn tracker (prefix + "BK") "Bank" assetTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker bankAcct revAcct fpId (DateOnly(2026, 3, 10)) "Good income" 500m |> ignore
        let badEntryId = postEntry conn tracker bankAcct revAcct fpId (DateOnly(2026, 3, 15)) "Bad income" 200m
        let voidCmd = { journalEntryId = badEntryId; voidReason = "Test void" }
        match JournalEntryService.voidEntry voidCmd with
        | Error errs -> Assert.Fail(sprintf "Void failed: %A" errs)
        | Ok _ -> ()
        let result = SubtreePLService.getByAccountCodeAndPeriodId parentCode fpId
        match result with
        | Ok report ->
            Assert.Equal(500m, report.revenue.sectionTotal)
            Assert.Equal(500m, report.netIncome)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SPL-007")>]
let ``accounts outside the subtree are excluded`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        // Subtree parent
        let parentCode = prefix + "PA"
        let _parentAcct = InsertHelpers.insertAccount conn tracker parentCode "Parent" assetTypeId true
        let revInSubtree = InsertHelpers.insertAccountWithParent conn tracker (prefix + "RI") "Rev In Subtree" revenueTypeId parentCode true
        // Revenue account OUTSIDE subtree
        let revOutside = InsertHelpers.insertAccount conn tracker (prefix + "RO") "Rev Outside" revenueTypeId true
        let bankAcct = InsertHelpers.insertAccount conn tracker (prefix + "BK") "Bank" assetTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker bankAcct revInSubtree fpId (DateOnly(2026, 3, 10)) "Subtree revenue" 600m |> ignore
        postEntry conn tracker bankAcct revOutside fpId (DateOnly(2026, 3, 15)) "Outside revenue" 400m |> ignore
        let result = SubtreePLService.getByAccountCodeAndPeriodId parentCode fpId
        match result with
        | Ok report ->
            Assert.Equal(600m, report.revenue.sectionTotal)
            Assert.Equal(1, report.revenue.lines.Length)
            let accountIds = report.revenue.lines |> List.map (fun l -> l.accountId)
            Assert.DoesNotContain(revOutside, accountIds)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SPL-008")>]
let ``multi-level hierarchy includes grandchildren in subtree`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        // Root -> Child -> Grandchild
        let rootCode = prefix + "RT"
        let _rootAcct = InsertHelpers.insertAccount conn tracker rootCode "Root" assetTypeId true
        let childCode = prefix + "CH"
        let _childAcct = InsertHelpers.insertAccountWithParent conn tracker childCode "Child" assetTypeId rootCode true
        let grandchildCode = prefix + "GC"
        let grandchildAcct = InsertHelpers.insertAccountWithParent conn tracker grandchildCode "Grandchild Expense" expenseTypeId childCode true
        let bankAcct = InsertHelpers.insertAccount conn tracker (prefix + "BK") "Bank" assetTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker grandchildAcct bankAcct fpId (DateOnly(2026, 3, 15)) "Deep expense" 250m |> ignore
        let result = SubtreePLService.getByAccountCodeAndPeriodId rootCode fpId
        match result with
        | Ok report ->
            Assert.Equal(1, report.expenses.lines.Length)
            Assert.Equal(250m, report.expenses.sectionTotal)
            let expLine = report.expenses.lines |> List.head
            Assert.Equal(grandchildAcct, expLine.accountId)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SPL-009")>]
let ``lookup by period key returns same result as by period ID`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let parentCode = prefix + "PA"
        let _parentAcct = InsertHelpers.insertAccount conn tracker parentCode "Parent" assetTypeId true
        let revCode = prefix + "RV"
        let revAcct = InsertHelpers.insertAccountWithParent conn tracker revCode "Revenue Child" revenueTypeId parentCode true
        let bankAcct = InsertHelpers.insertAccount conn tracker (prefix + "BK") "Bank" assetTypeId true
        let periodKey = prefix + "FP"
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker periodKey (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        postEntry conn tracker bankAcct revAcct fpId (DateOnly(2026, 3, 15)) "Lookup test" 250m |> ignore
        let resultById = SubtreePLService.getByAccountCodeAndPeriodId parentCode fpId
        let resultByKey = SubtreePLService.getByAccountCodeAndPeriodKey parentCode periodKey
        match resultById, resultByKey with
        | Ok reportById, Ok reportByKey ->
            Assert.Equal(reportById.revenue.sectionTotal, reportByKey.revenue.sectionTotal)
            Assert.Equal(reportById.expenses.sectionTotal, reportByKey.expenses.sectionTotal)
            Assert.Equal(reportById.netIncome, reportByKey.netIncome)
            Assert.Equal(reportById.fiscalPeriodId, reportByKey.fiscalPeriodId)
            Assert.Equal(reportById.periodKey, reportByKey.periodKey)
            Assert.Equal(reportById.rootAccountCode, reportByKey.rootAccountCode)
        | Error err, _ -> Assert.Fail(sprintf "getByAccountCodeAndPeriodId failed: %s" err)
        | _, Error err -> Assert.Fail(sprintf "getByAccountCodeAndPeriodKey failed: %s" err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SPL-010")>]
let ``nonexistent account code returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
        let result = SubtreePLService.getByAccountCodeAndPeriodId "ZZZZNOEXIST" fpId
        match result with
        | Ok _ -> Assert.Fail("Expected Error for nonexistent account code")
        | Error err -> Assert.Contains("does not exist", err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SPL-011")>]
let ``nonexistent period returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let acctCode = prefix + "AC"
        let _acct = InsertHelpers.insertAccount conn tracker acctCode "Test" revenueTypeId true
        let result = SubtreePLService.getByAccountCodeAndPeriodId acctCode 999999
        match result with
        | Ok _ -> Assert.Fail("Expected Error for nonexistent period")
        | Error err -> Assert.Contains("does not exist", err)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SPL-012")>]
let ``empty period for subtree produces zero net income`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let parentCode = prefix + "PA"
        let _parentAcct = InsertHelpers.insertAccount conn tracker parentCode "Parent" assetTypeId true
        let _revAcct = InsertHelpers.insertAccountWithParent conn tracker (prefix + "RV") "Revenue Child" revenueTypeId parentCode true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let result = SubtreePLService.getByAccountCodeAndPeriodId parentCode fpId
        match result with
        | Ok report ->
            Assert.Equal(0m, report.revenue.sectionTotal)
            Assert.Equal(0m, report.expenses.sectionTotal)
            Assert.Equal(0m, report.netIncome)
            Assert.Equal(0, report.revenue.lines.Length)
            Assert.Equal(0, report.expenses.lines.Length)
        | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Structural tests -- acceptance criteria owned by QE
// =====================================================================

[<Fact>]
let ``SubtreePLReport type has required fields`` () =
    let report : SubtreePLReport =
        { rootAccountCode = "4000"
          rootAccountName = "Revenue"
          fiscalPeriodId = 1
          periodKey = "2026-03"
          revenue = { sectionName = "revenue"; lines = []; sectionTotal = 0m }
          expenses = { sectionName = "expense"; lines = []; sectionTotal = 0m }
          netIncome = 0m }
    Assert.Equal("4000", report.rootAccountCode)
    Assert.Equal("Revenue", report.rootAccountName)
    Assert.Equal(1, report.fiscalPeriodId)
    Assert.Equal("2026-03", report.periodKey)
    Assert.Equal("revenue", report.revenue.sectionName)
    Assert.Equal("expense", report.expenses.sectionName)
    Assert.Equal(0m, report.netIncome)

[<Fact>]
let ``getByAccountCodeAndPeriodId returns Result of SubtreePLReport or string`` () =
    let result : Result<SubtreePLReport, string> = SubtreePLService.getByAccountCodeAndPeriodId "ZZZNOEXIST" 999998
    match result with
    | Error err -> Assert.Contains("does not exist", err)
    | Ok _ -> Assert.Fail("Expected Error for nonexistent account")

[<Fact>]
let ``getByAccountCodeAndPeriodKey returns Result of SubtreePLReport or string`` () =
    let result : Result<SubtreePLReport, string> = SubtreePLService.getByAccountCodeAndPeriodKey "ZZZNOEXIST" "0000-00"
    match result with
    | Error err -> Assert.Contains("does not exist", err)
    | Ok _ -> Assert.Fail("Expected Error for nonexistent account")
