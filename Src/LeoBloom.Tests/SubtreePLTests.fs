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

let private postEntry (txn: NpgsqlTransaction) acct1 acct2 fpId (entryDate: DateOnly) (desc: string) (amount: decimal) =
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
    | Error errs -> failwith (sprintf "Setup post failed: %A" errs)

// =====================================================================
// Behavioral tests -- mapped to Gherkin scenarios
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SPL-001")>]
let ``subtree with revenue and expense children produces correct P&L`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    // Parent account (asset — won't appear in P&L itself)
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Parent" assetTypeId true
    // Revenue child under parent
    let revCode = prefix + "RV"
    let revAcct = InsertHelpers.insertAccountWithParent txn revCode "Revenue Child" revenueTypeId parentAcct true
    // Expense child under parent
    let expCode = prefix + "EX"
    let expAcct = InsertHelpers.insertAccountWithParent txn expCode "Expense Child" expenseTypeId parentAcct true
    // Asset account for the other side of entries (outside subtree)
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn bankAcct revAcct fpId (DateOnly(2026, 3, 10)) "Service income" 1000m |> ignore
    postEntry txn expAcct bankAcct fpId (DateOnly(2026, 3, 20)) "Office supplies" 400m |> ignore
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId
    match result with
    | Ok report ->
        Assert.Equal(parentCode, report.rootAccountCode)
        Assert.Equal(1000m, report.revenue.sectionTotal)
        Assert.Equal(400m, report.expenses.sectionTotal)
        Assert.Equal(600m, report.netIncome)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-SPL-002")>]
let ``subtree with only revenue descendants shows empty expense section`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Parent" assetTypeId true
    let revCode = prefix + "RV"
    let revAcct = InsertHelpers.insertAccountWithParent txn revCode "Revenue Child" revenueTypeId parentAcct true
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn bankAcct revAcct fpId (DateOnly(2026, 3, 15)) "Revenue only" 750m |> ignore
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId
    match result with
    | Ok report ->
        Assert.Equal(750m, report.revenue.sectionTotal)
        Assert.Equal(1, report.revenue.lines.Length)
        Assert.Equal(0m, report.expenses.sectionTotal)
        Assert.Equal(0, report.expenses.lines.Length)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-SPL-003")>]
let ``subtree with only expense descendants shows empty revenue section`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Parent" assetTypeId true
    let expCode = prefix + "EX"
    let expAcct = InsertHelpers.insertAccountWithParent txn expCode "Expense Child" expenseTypeId parentAcct true
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn expAcct bankAcct fpId (DateOnly(2026, 3, 15)) "Expense only" 300m |> ignore
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId
    match result with
    | Ok report ->
        Assert.Equal(300m, report.expenses.sectionTotal)
        Assert.Equal(1, report.expenses.lines.Length)
        Assert.Equal(0m, report.revenue.sectionTotal)
        Assert.Equal(0, report.revenue.lines.Length)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-SPL-004")>]
let ``root account with no children returns single-account P&L`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let revCode = prefix + "RV"
    let revAcct = InsertHelpers.insertAccount txn revCode "Solo Revenue" revenueTypeId true
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn bankAcct revAcct fpId (DateOnly(2026, 3, 15)) "Solo income" 500m |> ignore
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn revCode fpId
    match result with
    | Ok report ->
        Assert.Equal(revCode, report.rootAccountCode)
        Assert.Equal(1, report.revenue.lines.Length)
        Assert.Equal(500m, report.revenue.sectionTotal)
        Assert.Equal(500m, report.netIncome)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-SPL-005")>]
let ``root account not revenue or expense with no rev/exp descendants returns empty report`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Asset Parent" assetTypeId true
    // Child is also an asset — no revenue/expense in subtree
    let _childAcct = InsertHelpers.insertAccountWithParent txn (prefix + "CH") "Asset Child" assetTypeId parentAcct true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId
    match result with
    | Ok report ->
        Assert.Equal(0m, report.revenue.sectionTotal)
        Assert.Equal(0m, report.expenses.sectionTotal)
        Assert.Equal(0m, report.netIncome)
        Assert.Equal(0, report.revenue.lines.Length)
        Assert.Equal(0, report.expenses.lines.Length)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-SPL-006")>]
let ``voided entries excluded from subtree P&L`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Parent" assetTypeId true
    let revCode = prefix + "RV"
    let revAcct = InsertHelpers.insertAccountWithParent txn revCode "Revenue Child" revenueTypeId parentAcct true
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn bankAcct revAcct fpId (DateOnly(2026, 3, 10)) "Good income" 500m |> ignore
    let badEntryId = postEntry txn bankAcct revAcct fpId (DateOnly(2026, 3, 15)) "Bad income" 200m
    let voidCmd = { journalEntryId = badEntryId; voidReason = "Test void" }
    match JournalEntryService.voidEntry txn voidCmd with
    | Error errs -> Assert.Fail(sprintf "Void failed: %A" errs)
    | Ok _ -> ()
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId
    match result with
    | Ok report ->
        Assert.Equal(500m, report.revenue.sectionTotal)
        Assert.Equal(500m, report.netIncome)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-SPL-007")>]
let ``accounts outside the subtree are excluded`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    // Subtree parent
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Parent" assetTypeId true
    let revInSubtree = InsertHelpers.insertAccountWithParent txn (prefix + "RI") "Rev In Subtree" revenueTypeId parentAcct true
    // Revenue account OUTSIDE subtree
    let revOutside = InsertHelpers.insertAccount txn (prefix + "RO") "Rev Outside" revenueTypeId true
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn bankAcct revInSubtree fpId (DateOnly(2026, 3, 10)) "Subtree revenue" 600m |> ignore
    postEntry txn bankAcct revOutside fpId (DateOnly(2026, 3, 15)) "Outside revenue" 400m |> ignore
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId
    match result with
    | Ok report ->
        Assert.Equal(600m, report.revenue.sectionTotal)
        Assert.Equal(1, report.revenue.lines.Length)
        let accountIds = report.revenue.lines |> List.map (fun l -> l.accountId)
        Assert.DoesNotContain(revOutside, accountIds)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-SPL-008")>]
let ``multi-level hierarchy includes grandchildren in subtree`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    // Root -> Child -> Grandchild
    let rootCode = prefix + "RT"
    let rootAcct = InsertHelpers.insertAccount txn rootCode "Root" assetTypeId true
    let childCode = prefix + "CH"
    let childAcct = InsertHelpers.insertAccountWithParent txn childCode "Child" assetTypeId rootAcct true
    let grandchildCode = prefix + "GC"
    let grandchildAcct = InsertHelpers.insertAccountWithParent txn grandchildCode "Grandchild Expense" expenseTypeId childAcct true
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn grandchildAcct bankAcct fpId (DateOnly(2026, 3, 15)) "Deep expense" 250m |> ignore
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn rootCode fpId
    match result with
    | Ok report ->
        Assert.Equal(1, report.expenses.lines.Length)
        Assert.Equal(250m, report.expenses.sectionTotal)
        let expLine = report.expenses.lines |> List.head
        Assert.Equal(grandchildAcct, expLine.accountId)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-SPL-009")>]
let ``lookup by period key returns same result as by period ID`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Parent" assetTypeId true
    let revCode = prefix + "RV"
    let revAcct = InsertHelpers.insertAccountWithParent txn revCode "Revenue Child" revenueTypeId parentAcct true
    let bankAcct = InsertHelpers.insertAccount txn (prefix + "BK") "Bank" assetTypeId true
    let periodKey = prefix + "FP"
    let fpId = InsertHelpers.insertFiscalPeriod txn periodKey (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn bankAcct revAcct fpId (DateOnly(2026, 3, 15)) "Lookup test" 250m |> ignore
    let resultById = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId
    let resultByKey = SubtreePLService.getByAccountCodeAndPeriodKey txn parentCode periodKey
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

[<Fact>]
[<Trait("GherkinId", "FT-SPL-010")>]
let ``nonexistent account code returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn "ZZZZNOEXIST" fpId
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent account code")
    | Error err -> Assert.Contains("does not exist", err)

[<Fact>]
[<Trait("GherkinId", "FT-SPL-011")>]
let ``nonexistent period returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let acctCode = prefix + "AC"
    let _acct = InsertHelpers.insertAccount txn acctCode "Test" revenueTypeId true
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn acctCode 999999
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent period")
    | Error err -> Assert.Contains("does not exist", err)

[<Fact>]
[<Trait("GherkinId", "FT-SPL-012")>]
let ``empty period for subtree produces zero net income`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let parentCode = prefix + "PA"
    let parentAcct = InsertHelpers.insertAccount txn parentCode "Parent" assetTypeId true
    let _revAcct = InsertHelpers.insertAccountWithParent txn (prefix + "RV") "Revenue Child" revenueTypeId parentAcct true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
    let result = SubtreePLService.getByAccountCodeAndPeriodId txn parentCode fpId
    match result with
    | Ok report ->
        Assert.Equal(0m, report.revenue.sectionTotal)
        Assert.Equal(0m, report.expenses.sectionTotal)
        Assert.Equal(0m, report.netIncome)
        Assert.Equal(0, report.revenue.lines.Length)
        Assert.Equal(0, report.expenses.lines.Length)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

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
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result : Result<SubtreePLReport, string> = SubtreePLService.getByAccountCodeAndPeriodId txn "ZZZNOEXIST" 999998
    match result with
    | Error err -> Assert.Contains("does not exist", err)
    | Ok _ -> Assert.Fail("Expected Error for nonexistent account")

[<Fact>]
let ``getByAccountCodeAndPeriodKey returns Result of SubtreePLReport or string`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result : Result<SubtreePLReport, string> = SubtreePLService.getByAccountCodeAndPeriodKey txn "ZZZNOEXIST" "0000-00"
    match result with
    | Error err -> Assert.Contains("does not exist", err)
    | Ok _ -> Assert.Fail("Expected Error for nonexistent account")
