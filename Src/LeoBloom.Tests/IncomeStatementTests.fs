module LeoBloom.Tests.IncomeStatementTests

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
          references = [] }
    match JournalEntryService.post txn cmd with
    | Ok posted -> posted.entry.id
    | Error errs -> failwith (sprintf "Setup post failed: %A" errs)

// =====================================================================
// Behavioral tests -- mapped to Gherkin scenarios
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-IS-001")>]
let ``period with revenue and expense activity produces correct net income`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 10)) "Service income" 1000m |> ignore
    postEntry txn expAcct assetAcct fpId (DateOnly(2026, 3, 20)) "Office supplies" 400m |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId
    match result with
    | Ok report ->
        Assert.Equal(1000m, report.revenue.sectionTotal)
        Assert.Equal(400m, report.expenses.sectionTotal)
        Assert.Equal(600m, report.netIncome)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-002")>]
let ``revenue only period shows revenue section with empty expenses`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Revenue only" 750m |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId
    match result with
    | Ok report ->
        Assert.Equal(750m, report.revenue.sectionTotal)
        Assert.Equal(1, report.revenue.lines.Length)
        Assert.Equal(0m, report.expenses.sectionTotal)
        Assert.Equal(0, report.expenses.lines.Length)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-003")>]
let ``expenses only period shows expense section with empty revenue`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let expAcct = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn expAcct assetAcct fpId (DateOnly(2026, 3, 15)) "Expense only" 300m |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId
    match result with
    | Ok report ->
        Assert.Equal(300m, report.expenses.sectionTotal)
        Assert.Equal(1, report.expenses.lines.Length)
        Assert.Equal(0m, report.revenue.sectionTotal)
        Assert.Equal(0, report.revenue.lines.Length)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-004")>]
let ``voided entries excluded from income statement`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 10)) "Good income" 500m |> ignore
    let badEntryId = postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Bad income" 200m
    let voidCmd = { journalEntryId = badEntryId; voidReason = "Test void" }
    match JournalEntryService.voidEntry txn voidCmd with
    | Error errs -> Assert.Fail(sprintf "Void failed: %A" errs)
    | Ok _ -> ()
    let result = IncomeStatementService.getByPeriodId txn fpId
    match result with
    | Ok report ->
        Assert.Equal(500m, report.revenue.sectionTotal)
        Assert.Equal(500m, report.netIncome)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-005")>]
let ``accounts with no activity in the period are omitted from sections`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct1 = InsertHelpers.insertAccount txn (prefix + "R1") "Revenue1" revenueTypeId true
    let _revAcct2 = InsertHelpers.insertAccount txn (prefix + "R2") "Revenue2" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct1 fpId (DateOnly(2026, 3, 15)) "One revenue only" 600m |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId
    match result with
    | Ok report ->
        Assert.Equal(1, report.revenue.lines.Length)
        let accountIds = report.revenue.lines |> List.map (fun l -> l.accountId)
        Assert.DoesNotContain(_revAcct2, accountIds)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-006")>]
let ``inactive accounts with activity in the period still appear`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Before deactivation" 500m |> ignore
    // Deactivate the revenue account
    use deactCmd = new NpgsqlCommand("UPDATE ledger.account SET is_active = false WHERE id = @id", txn.Connection)
    deactCmd.Transaction <- txn
    deactCmd.Parameters.AddWithValue("@id", revAcct) |> ignore
    deactCmd.ExecuteNonQuery() |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId
    match result with
    | Ok report ->
        let revLine = report.revenue.lines |> List.tryFind (fun l -> l.accountId = revAcct)
        Assert.True(revLine.IsSome, "Inactive account with activity should appear")
        Assert.Equal(500m, revLine.Value.balance)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-007")>]
let ``empty period produces zero net income`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
    let result = IncomeStatementService.getByPeriodId txn fpId
    match result with
    | Ok report ->
        Assert.Equal(0m, report.revenue.sectionTotal)
        Assert.Equal(0m, report.expenses.sectionTotal)
        Assert.Equal(0m, report.netIncome)
        Assert.Equal(0, report.revenue.lines.Length)
        Assert.Equal(0, report.expenses.lines.Length)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

// IS-008 removed (REM-014): redundant with IS-001

[<Fact>]
[<Trait("GherkinId", "FT-IS-009")>]
let ``net loss when expenses exceed revenue`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 10)) "Small income" 300m |> ignore
    postEntry txn expAcct assetAcct fpId (DateOnly(2026, 3, 20)) "Big expense" 800m |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId
    match result with
    | Ok report ->
        Assert.Equal(-500m, report.netIncome)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-010")>]
let ``multiple revenue and expense accounts accumulate correctly`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct1 = InsertHelpers.insertAccount txn (prefix + "R1") "Revenue1" revenueTypeId true
    let revAcct2 = InsertHelpers.insertAccount txn (prefix + "R2") "Revenue2" revenueTypeId true
    let expAcct1 = InsertHelpers.insertAccount txn (prefix + "E1") "Expense1" expenseTypeId true
    let expAcct2 = InsertHelpers.insertAccount txn (prefix + "E2") "Expense2" expenseTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct1 fpId (DateOnly(2026, 3, 5)) "Sales revenue" 600m |> ignore
    postEntry txn assetAcct revAcct2 fpId (DateOnly(2026, 3, 10)) "Service revenue" 400m |> ignore
    postEntry txn expAcct1 assetAcct fpId (DateOnly(2026, 3, 15)) "Rent expense" 200m |> ignore
    postEntry txn expAcct2 assetAcct fpId (DateOnly(2026, 3, 20)) "Utility expense" 150m |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId
    match result with
    | Ok report ->
        Assert.Equal(2, report.revenue.lines.Length)
        Assert.Equal(1000m, report.revenue.sectionTotal)
        Assert.Equal(2, report.expenses.lines.Length)
        Assert.Equal(350m, report.expenses.sectionTotal)
        Assert.Equal(650m, report.netIncome)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-011")>]
let ``revenue balance equals credits minus debits`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    // Credit revenue 700
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 10)) "Revenue credit" 700m |> ignore
    // Debit revenue 100 (adjustment)
    postEntry txn revAcct assetAcct fpId (DateOnly(2026, 3, 20)) "Revenue debit adjustment" 100m |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId
    match result with
    | Ok report ->
        let revLine = report.revenue.lines |> List.find (fun l -> l.accountId = revAcct)
        Assert.Equal(600m, revLine.balance)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-012")>]
let ``expense balance equals debits minus credits`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let expAcct = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    // Debit expense 500
    postEntry txn expAcct assetAcct fpId (DateOnly(2026, 3, 10)) "Expense debit" 500m |> ignore
    // Credit expense 150 (adjustment)
    postEntry txn assetAcct expAcct fpId (DateOnly(2026, 3, 20)) "Expense credit adjustment" 150m |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId
    match result with
    | Ok report ->
        let expLine = report.expenses.lines |> List.find (fun l -> l.accountId = expAcct)
        Assert.Equal(350m, expLine.balance)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-013")>]
let ``lookup by period key returns same result as by period ID`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let periodKey = prefix + "FP"
    let fpId = InsertHelpers.insertFiscalPeriod txn periodKey (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Lookup test" 250m |> ignore
    let resultById = IncomeStatementService.getByPeriodId txn fpId
    let resultByKey = IncomeStatementService.getByPeriodKey txn periodKey
    match resultById, resultByKey with
    | Ok reportById, Ok reportByKey ->
        Assert.Equal(reportById.revenue.sectionTotal, reportByKey.revenue.sectionTotal)
        Assert.Equal(reportById.expenses.sectionTotal, reportByKey.expenses.sectionTotal)
        Assert.Equal(reportById.netIncome, reportByKey.netIncome)
        Assert.Equal(reportById.fiscalPeriodId, reportByKey.fiscalPeriodId)
        Assert.Equal(reportById.periodKey, reportByKey.periodKey)
        Assert.Equal(reportById.revenue.lines.Length, reportByKey.revenue.lines.Length)
        Assert.Equal(reportById.expenses.lines.Length, reportByKey.expenses.lines.Length)
    | Error err, _ -> Assert.Fail(sprintf "getByPeriodId failed: %s" err)
    | _, Error err -> Assert.Fail(sprintf "getByPeriodKey failed: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-014")>]
let ``nonexistent period ID returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result = IncomeStatementService.getByPeriodId txn 999999
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent period ID")
    | Error err ->
        Assert.Contains("does not exist", err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-015")>]
let ``nonexistent period key returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result = IncomeStatementService.getByPeriodKey txn "9999-99"
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent period key")
    | Error err ->
        Assert.Contains("does not exist", err)

[<Fact>]
[<Trait("GherkinId", "FT-IS-016")>]
let ``closed period income statement still works`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2025, 12, 1)) (DateOnly(2025, 12, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2025, 12, 15)) "December income" 800m |> ignore
    // Close the period
    use closeCmd = new NpgsqlCommand("UPDATE ledger.fiscal_period SET is_open = false WHERE id = @id", txn.Connection)
    closeCmd.Transaction <- txn
    closeCmd.Parameters.AddWithValue("@id", fpId) |> ignore
    closeCmd.ExecuteNonQuery() |> ignore
    let result = IncomeStatementService.getByPeriodId txn fpId
    match result with
    | Ok report ->
        Assert.Equal(800m, report.revenue.sectionTotal)
        Assert.Equal(800m, report.netIncome)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

// =====================================================================
// Period Scoping — @FT-IS-017 (REM-010)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-IS-017")>]
let ``income statement for one period excludes another period's activity`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    let fpMar = InsertHelpers.insertFiscalPeriod txn (prefix + "M3") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let fpApr = InsertHelpers.insertFiscalPeriod txn (prefix + "A4") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
    // March: revenue entry
    postEntry txn assetAcct revAcct fpMar (DateOnly(2026, 3, 15)) "March revenue" 800m |> ignore
    // April: expense entry
    postEntry txn expAcct assetAcct fpApr (DateOnly(2026, 4, 10)) "April expense" 300m |> ignore
    // Query March only
    let result = IncomeStatementService.getByPeriodId txn fpMar
    match result with
    | Ok report ->
        Assert.Equal(800m, report.revenue.sectionTotal)
        Assert.Equal(0m, report.expenses.sectionTotal)
        Assert.Equal(800m, report.netIncome)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

// =====================================================================
// Structural tests -- acceptance criteria owned by QE
// =====================================================================

[<Fact>]
let ``IncomeStatementLine type has required fields`` () =
    let line : IncomeStatementLine =
        { accountId = 1
          accountCode = "4010"
          accountName = "Sales Revenue"
          balance = 1000m }
    Assert.Equal(1, line.accountId)
    Assert.Equal("4010", line.accountCode)
    Assert.Equal("Sales Revenue", line.accountName)
    Assert.Equal(1000m, line.balance)

[<Fact>]
let ``IncomeStatementSection type has required fields`` () =
    let section : IncomeStatementSection =
        { sectionName = "revenue"
          lines = []
          sectionTotal = 0m }
    Assert.Equal("revenue", section.sectionName)
    Assert.Empty(section.lines)
    Assert.Equal(0m, section.sectionTotal)

[<Fact>]
let ``IncomeStatementReport type has required fields`` () =
    let report : IncomeStatementReport =
        { fiscalPeriodId = 1
          periodKey = "2026-03"
          revenue = { sectionName = "revenue"; lines = []; sectionTotal = 0m }
          expenses = { sectionName = "expense"; lines = []; sectionTotal = 0m }
          netIncome = 0m }
    Assert.Equal(1, report.fiscalPeriodId)
    Assert.Equal("2026-03", report.periodKey)
    Assert.Equal("revenue", report.revenue.sectionName)
    Assert.Equal("expense", report.expenses.sectionName)
    Assert.Equal(0m, report.netIncome)

[<Fact>]
let ``getByPeriodId returns Result of IncomeStatementReport or string`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result : Result<IncomeStatementReport, string> = IncomeStatementService.getByPeriodId txn 999998
    match result with
    | Error err -> Assert.Contains("does not exist", err)
    | Ok _ -> Assert.Fail("Expected Error for nonexistent period")

[<Fact>]
let ``getByPeriodKey returns Result of IncomeStatementReport or string`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result : Result<IncomeStatementReport, string> = IncomeStatementService.getByPeriodKey txn "0000-00"
    match result with
    | Error err -> Assert.Contains("does not exist", err)
    | Ok _ -> Assert.Fail("Expected Error for nonexistent period")
