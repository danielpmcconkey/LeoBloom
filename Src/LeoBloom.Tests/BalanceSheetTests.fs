module LeoBloom.Tests.BalanceSheetTests

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
[<Trait("GherkinId", "FT-BS-001")>]
let ``balanced books produce isBalanced true`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let liabAcct = InsertHelpers.insertAccount txn (prefix + "LI") "Liability" liabilityTypeId true
    let eqAcct = InsertHelpers.insertAccount txn (prefix + "EQ") "Equity" equityTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct eqAcct fpId (DateOnly(2026, 3, 5)) "Owner investment" 5000m |> ignore
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 10)) "Service income" 1000m |> ignore
    postEntry txn expAcct assetAcct fpId (DateOnly(2026, 3, 20)) "Office rent" 400m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(2026, 3, 31))
    match result with
    | Ok report ->
        Assert.True(report.isBalanced)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-002")>]
let ``assets equal liabilities plus total equity`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let liabAcct = InsertHelpers.insertAccount txn (prefix + "LI") "Liability" liabilityTypeId true
    let eqAcct = InsertHelpers.insertAccount txn (prefix + "EQ") "Equity" equityTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct eqAcct fpId (DateOnly(2026, 3, 5)) "Owner investment" 5000m |> ignore
    postEntry txn assetAcct liabAcct fpId (DateOnly(2026, 3, 10)) "Took a loan" 2000m |> ignore
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Earned revenue" 1000m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(2026, 3, 31))
    match result with
    | Ok report ->
        // Balance sheet is cumulative; assert on our specific accounts
        let assetLine = report.assets.lines |> List.find (fun l -> l.accountId = assetAcct)
        Assert.Equal(8000m, assetLine.balance)
        let liabLine = report.liabilities.lines |> List.find (fun l -> l.accountId = liabAcct)
        Assert.Equal(2000m, liabLine.balance)
        let eqLine = report.equity.lines |> List.find (fun l -> l.accountId = eqAcct)
        Assert.Equal(5000m, eqLine.balance)
        // Verify the accounting equation holds globally
        Assert.True(report.isBalanced)
        // Verify total equity = equity section + retained earnings
        Assert.Equal(report.equity.sectionTotal + report.retainedEarnings, report.totalEquity)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-003")>]
let ``positive retained earnings when revenue exceeds expenses`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    // Use far-past dates so parallel tests' revenue/expense entries don't pollute retainedEarnings
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(1950, 3, 1)) (DateOnly(1950, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(1950, 3, 10)) "Revenue" 3000m |> ignore
    postEntry txn expAcct assetAcct fpId (DateOnly(1950, 3, 20)) "Expense" 1000m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(1950, 3, 31))
    match result with
    | Ok report ->
        let assetLine = report.assets.lines |> List.find (fun l -> l.accountId = assetAcct)
        Assert.Equal(2000m, assetLine.balance)
        Assert.Equal(2000m, report.retainedEarnings)
        Assert.True(report.isBalanced)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-004")>]
let ``negative retained earnings when expenses exceed revenue`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let liabAcct = InsertHelpers.insertAccount txn (prefix + "LI") "Liability" liabilityTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    // Use far-past dates so parallel tests' revenue/expense entries don't pollute retainedEarnings
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(1951, 3, 1)) (DateOnly(1951, 3, 31)) true
    postEntry txn assetAcct liabAcct fpId (DateOnly(1951, 3, 5)) "Borrowed to fund operations" 2000m |> ignore
    postEntry txn assetAcct revAcct fpId (DateOnly(1951, 3, 10)) "Small revenue" 200m |> ignore
    postEntry txn expAcct assetAcct fpId (DateOnly(1951, 3, 20)) "Large expense" 800m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(1951, 3, 31))
    match result with
    | Ok report ->
        let assetLine = report.assets.lines |> List.find (fun l -> l.accountId = assetAcct)
        Assert.Equal(1400m, assetLine.balance)
        let liabLine = report.liabilities.lines |> List.find (fun l -> l.accountId = liabAcct)
        Assert.Equal(2000m, liabLine.balance)
        Assert.Equal(-600m, report.retainedEarnings)
        Assert.True(report.isBalanced)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-005")>]
let ``retained earnings zero when no revenue or expense activity`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let eqAcct = InsertHelpers.insertAccount txn (prefix + "EQ") "Equity" equityTypeId true
    // Use far-past dates so parallel tests' revenue/expense entries don't pollute retainedEarnings
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(1952, 3, 1)) (DateOnly(1952, 3, 31)) true
    postEntry txn assetAcct eqAcct fpId (DateOnly(1952, 3, 10)) "Owner investment only" 5000m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(1952, 3, 31))
    match result with
    | Ok report ->
        let assetLine = report.assets.lines |> List.find (fun l -> l.accountId = assetAcct)
        Assert.Equal(5000m, assetLine.balance)
        let eqLine = report.equity.lines |> List.find (fun l -> l.accountId = eqAcct)
        Assert.Equal(5000m, eqLine.balance)
        Assert.Equal(0m, report.retainedEarnings)
        Assert.True(report.isBalanced)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-006")>]
let ``before any entries all zeros and balanced`` () =
    // Use a date far in the past so no test data exists
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(1900, 1, 1))
    match result with
    | Ok report ->
        Assert.Equal(0m, report.assets.sectionTotal)
        Assert.Equal(0m, report.liabilities.sectionTotal)
        Assert.Equal(0m, report.equity.sectionTotal)
        Assert.Equal(0m, report.retainedEarnings)
        Assert.Equal(0m, report.totalEquity)
        Assert.True(report.isBalanced)
        Assert.Empty(report.assets.lines)
        Assert.Empty(report.liabilities.lines)
        Assert.Empty(report.equity.lines)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-007")>]
let ``voided entries excluded from balance sheet`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let eqAcct = InsertHelpers.insertAccount txn (prefix + "EQ") "Equity" equityTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct eqAcct fpId (DateOnly(2026, 3, 10)) "Good investment" 5000m |> ignore
    let badEntryId = postEntry txn assetAcct eqAcct fpId (DateOnly(2026, 3, 15)) "Bad investment" 2000m
    let voidCmd = { journalEntryId = badEntryId; voidReason = "Test void" }
    match JournalEntryService.voidEntry txn voidCmd with
    | Error errs -> Assert.Fail(sprintf "Void failed: %A" errs)
    | Ok _ -> ()
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(2026, 3, 31))
    match result with
    | Ok report ->
        let assetLine = report.assets.lines |> List.find (fun l -> l.accountId = assetAcct)
        let eqLine = report.equity.lines |> List.find (fun l -> l.accountId = eqAcct)
        Assert.Equal(5000m, assetLine.balance)
        Assert.Equal(5000m, eqLine.balance)
        Assert.True(report.isBalanced)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-008")>]
let ``entries across multiple fiscal periods all contribute`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let eqAcct = InsertHelpers.insertAccount txn (prefix + "EQ") "Equity" equityTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let fp1 = InsertHelpers.insertFiscalPeriod txn (prefix + "P1") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
    let fp2 = InsertHelpers.insertFiscalPeriod txn (prefix + "P2") (DateOnly(2026, 2, 1)) (DateOnly(2026, 2, 28)) true
    let fp3 = InsertHelpers.insertFiscalPeriod txn (prefix + "P3") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct eqAcct fp1 (DateOnly(2026, 1, 15)) "January investment" 3000m |> ignore
    postEntry txn assetAcct revAcct fp2 (DateOnly(2026, 2, 15)) "February income" 1000m |> ignore
    postEntry txn assetAcct revAcct fp3 (DateOnly(2026, 3, 15)) "March income" 500m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(2026, 3, 31))
    match result with
    | Ok report ->
        // Balance sheet is cumulative across all DB data; assert on our specific accounts
        let assetLine = report.assets.lines |> List.find (fun l -> l.accountId = assetAcct)
        // Jan: 3000 + Feb: 1000 + Mar: 500 = 4500 total for our asset
        Assert.Equal(4500m, assetLine.balance)
        let eqLine = report.equity.lines |> List.find (fun l -> l.accountId = eqAcct)
        Assert.Equal(3000m, eqLine.balance)
        // Verify the accounting equation holds globally
        Assert.True(report.isBalanced)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-009")>]
let ``multiple accounts per section accumulate correctly`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let asset1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset1" assetTypeId true
    let asset2 = InsertHelpers.insertAccount txn (prefix + "A2") "Asset2" assetTypeId true
    let liab1 = InsertHelpers.insertAccount txn (prefix + "L1") "Liability1" liabilityTypeId true
    let liab2 = InsertHelpers.insertAccount txn (prefix + "L2") "Liability2" liabilityTypeId true
    let eq1 = InsertHelpers.insertAccount txn (prefix + "Q1") "Equity1" equityTypeId true
    let eq2 = InsertHelpers.insertAccount txn (prefix + "Q2") "Equity2" equityTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn asset1 eq1 fpId (DateOnly(2026, 3, 5)) "Owner investment into checking" 5000m |> ignore
    postEntry txn asset2 liab1 fpId (DateOnly(2026, 3, 10)) "Equipment purchase on credit" 2000m |> ignore
    postEntry txn asset1 eq2 fpId (DateOnly(2026, 3, 15)) "Partner investment" 3000m |> ignore
    postEntry txn asset1 liab2 fpId (DateOnly(2026, 3, 20)) "Additional loan" 1000m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(2026, 3, 31))
    match result with
    | Ok report ->
        // Filter to our specific accounts to avoid parallel test interference
        let ourAssetIds = set [ asset1; asset2 ]
        let ourAssets = report.assets.lines |> List.filter (fun l -> ourAssetIds.Contains(l.accountId))
        Assert.Equal(2, ourAssets.Length)
        let ourAssetTotal = ourAssets |> List.sumBy (fun l -> l.balance)
        Assert.Equal(11000m, ourAssetTotal)
        let ourLiabIds = set [ liab1; liab2 ]
        let ourLiabs = report.liabilities.lines |> List.filter (fun l -> ourLiabIds.Contains(l.accountId))
        Assert.Equal(2, ourLiabs.Length)
        let ourLiabTotal = ourLiabs |> List.sumBy (fun l -> l.balance)
        Assert.Equal(3000m, ourLiabTotal)
        let ourEqIds = set [ eq1; eq2 ]
        let ourEqs = report.equity.lines |> List.filter (fun l -> ourEqIds.Contains(l.accountId))
        Assert.Equal(2, ourEqs.Length)
        let ourEqTotal = ourEqs |> List.sumBy (fun l -> l.balance)
        Assert.Equal(8000m, ourEqTotal)
        Assert.True(report.isBalanced)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-010")>]
let ``account with activity netting to zero still appears with balance zero`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let asset1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset1" assetTypeId true
    let asset2 = InsertHelpers.insertAccount txn (prefix + "A2") "Asset2" assetTypeId true
    let eqAcct = InsertHelpers.insertAccount txn (prefix + "EQ") "Equity" equityTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn asset1 eqAcct fpId (DateOnly(2026, 3, 5)) "Investment" 5000m |> ignore
    postEntry txn asset2 asset1 fpId (DateOnly(2026, 3, 10)) "Transfer to savings" 1000m |> ignore
    postEntry txn asset1 asset2 fpId (DateOnly(2026, 3, 15)) "Transfer back" 1000m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(2026, 3, 31))
    match result with
    | Ok report ->
        let savingsLine = report.assets.lines |> List.tryFind (fun l -> l.accountId = asset2)
        Assert.True(savingsLine.IsSome, "Account with zero-net activity should still appear")
        Assert.Equal(0m, savingsLine.Value.balance)
        // Balance sheet is cumulative; other tests' accounts may appear too.
        // Verify our two accounts are both present.
        let ourAssetIds = set [ asset1; asset2 ]
        let ourLines = report.assets.lines |> List.filter (fun l -> ourAssetIds.Contains(l.accountId))
        Assert.Equal(2, ourLines.Length)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-011")>]
let ``inactive accounts with cumulative balances still appear`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let eqAcct = InsertHelpers.insertAccount txn (prefix + "EQ") "Equity" equityTypeId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct eqAcct fpId (DateOnly(2026, 3, 15)) "Before deactivation" 5000m |> ignore
    // Deactivate the asset account
    use deactCmd = new NpgsqlCommand("UPDATE ledger.account SET is_active = false WHERE id = @id", txn.Connection)
    deactCmd.Transaction <- txn
    deactCmd.Parameters.AddWithValue("@id", assetAcct) |> ignore
    deactCmd.ExecuteNonQuery() |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(2026, 3, 31))
    match result with
    | Ok report ->
        let assetLine = report.assets.lines |> List.tryFind (fun l -> l.accountId = assetAcct)
        Assert.True(assetLine.IsSome, "Inactive account with balance should still appear")
        Assert.Equal(5000m, assetLine.Value.balance)
        Assert.True(report.isBalanced)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-012")>]
let ``accounting equation holds with positive retained earnings`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let liabAcct = InsertHelpers.insertAccount txn (prefix + "LI") "Liability" liabilityTypeId true
    let eqAcct = InsertHelpers.insertAccount txn (prefix + "EQ") "Equity" equityTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    // Use far-past year 1953 to avoid retained earnings pollution from parallel tests
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(1953, 1, 1)) (DateOnly(1953, 1, 31)) true
    postEntry txn assetAcct eqAcct fpId (DateOnly(1953, 1, 5)) "Owner investment" 10000m |> ignore
    postEntry txn assetAcct liabAcct fpId (DateOnly(1953, 1, 10)) "Bank loan" 3000m |> ignore
    postEntry txn assetAcct revAcct fpId (DateOnly(1953, 1, 15)) "Service revenue" 2000m |> ignore
    postEntry txn expAcct assetAcct fpId (DateOnly(1953, 1, 20)) "Operating expense" 500m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(1953, 1, 31))
    match result with
    | Ok report ->
        Assert.Equal(14500m, report.assets.sectionTotal)
        Assert.Equal(3000m, report.liabilities.sectionTotal)
        Assert.Equal(10000m, report.equity.sectionTotal)
        Assert.Equal(1500m, report.retainedEarnings)
        // AC-1/AC-2: verify equation directly from section totals — never references isBalanced
        Assert.Equal(report.assets.sectionTotal, report.liabilities.sectionTotal + report.equity.sectionTotal + report.retainedEarnings)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-013")>]
let ``accounting equation holds with negative retained earnings`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let liabAcct = InsertHelpers.insertAccount txn (prefix + "LI") "Liability" liabilityTypeId true
    let eqAcct = InsertHelpers.insertAccount txn (prefix + "EQ") "Equity" equityTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    let expAcct = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expenseTypeId true
    // Use far-past year 1954 to avoid retained earnings pollution from parallel tests
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(1954, 1, 1)) (DateOnly(1954, 1, 31)) true
    postEntry txn assetAcct eqAcct fpId (DateOnly(1954, 1, 5)) "Owner investment" 5000m |> ignore
    postEntry txn assetAcct liabAcct fpId (DateOnly(1954, 1, 10)) "Bank loan" 4000m |> ignore
    postEntry txn assetAcct revAcct fpId (DateOnly(1954, 1, 15)) "Small revenue" 300m |> ignore
    postEntry txn expAcct assetAcct fpId (DateOnly(1954, 1, 20)) "Large expense" 1200m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(1954, 1, 31))
    match result with
    | Ok report ->
        Assert.Equal(8100m, report.assets.sectionTotal)
        Assert.Equal(4000m, report.liabilities.sectionTotal)
        Assert.Equal(5000m, report.equity.sectionTotal)
        Assert.Equal(-900m, report.retainedEarnings)
        // AC-1/AC-2: verify equation directly from section totals — never references isBalanced
        Assert.Equal(report.assets.sectionTotal, report.liabilities.sectionTotal + report.equity.sectionTotal + report.retainedEarnings)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-BS-014")>]
let ``accounting equation holds with zero equity section`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetTypeId true
    let liabAcct = InsertHelpers.insertAccount txn (prefix + "LI") "Liability" liabilityTypeId true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revenueTypeId true
    // Use far-past year 1955 to avoid retained earnings pollution from parallel tests
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(1955, 1, 1)) (DateOnly(1955, 1, 31)) true
    postEntry txn assetAcct liabAcct fpId (DateOnly(1955, 1, 5)) "Loan to fund operations" 6000m |> ignore
    postEntry txn assetAcct revAcct fpId (DateOnly(1955, 1, 15)) "Service revenue" 1000m |> ignore
    let result = BalanceSheetService.getAsOfDate txn (DateOnly(1955, 1, 31))
    match result with
    | Ok report ->
        Assert.Equal(7000m, report.assets.sectionTotal)
        Assert.Equal(6000m, report.liabilities.sectionTotal)
        Assert.Equal(0m, report.equity.sectionTotal)
        Assert.Equal(1000m, report.retainedEarnings)
        // AC-1/AC-2: verify equation directly from section totals — never references isBalanced
        Assert.Equal(report.assets.sectionTotal, report.liabilities.sectionTotal + report.equity.sectionTotal + report.retainedEarnings)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

// =====================================================================
// Structural tests -- acceptance criteria owned by QE
// =====================================================================

[<Fact>]
let ``BalanceSheetLine type has required fields`` () =
    let line : BalanceSheetLine =
        { accountId = 1
          accountCode = "1010"
          accountName = "Cash"
          balance = 5000m }
    Assert.Equal(1, line.accountId)
    Assert.Equal("1010", line.accountCode)
    Assert.Equal("Cash", line.accountName)
    Assert.Equal(5000m, line.balance)

[<Fact>]
let ``BalanceSheetSection type has required fields`` () =
    let section : BalanceSheetSection =
        { sectionName = "asset"
          lines = []
          sectionTotal = 0m }
    Assert.Equal("asset", section.sectionName)
    Assert.Empty(section.lines)
    Assert.Equal(0m, section.sectionTotal)

[<Fact>]
let ``BalanceSheetReport type has required fields`` () =
    let report : BalanceSheetReport =
        { asOfDate = DateOnly(2026, 3, 31)
          assets = { sectionName = "asset"; lines = []; sectionTotal = 0m }
          liabilities = { sectionName = "liability"; lines = []; sectionTotal = 0m }
          equity = { sectionName = "equity"; lines = []; sectionTotal = 0m }
          retainedEarnings = 0m
          totalEquity = 0m
          isBalanced = true }
    Assert.Equal(DateOnly(2026, 3, 31), report.asOfDate)
    Assert.Equal("asset", report.assets.sectionName)
    Assert.Equal("liability", report.liabilities.sectionName)
    Assert.Equal("equity", report.equity.sectionName)
    Assert.Equal(0m, report.retainedEarnings)
    Assert.Equal(0m, report.totalEquity)
    Assert.True(report.isBalanced)

[<Fact>]
let ``getAsOfDate returns Result of BalanceSheetReport or string`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result : Result<BalanceSheetReport, string> = BalanceSheetService.getAsOfDate txn (DateOnly(1900, 1, 1))
    match result with
    | Ok report ->
        // Very early date should have no data, but the call should succeed
        Assert.True(report.isBalanced)
    | Error err -> Assert.Fail(sprintf "Expected Ok for valid date: %s" err)
