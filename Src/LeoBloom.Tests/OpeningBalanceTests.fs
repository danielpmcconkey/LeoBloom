module LeoBloom.Tests.OpeningBalanceTests

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

// =====================================================================
// Behavioral tests -- mapped to Gherkin scenarios
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OB-001")>]
let ``opening balances for asset and liability produce balanced entry`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cashAcct = InsertHelpers.insertAccount conn tracker (prefix + "CA") "Cash" assetTypeId true
        let mortgageAcct = InsertHelpers.insertAccount conn tracker (prefix + "MO") "Mortgage" liabilityTypeId true
        let equityAcct = InsertHelpers.insertAccount conn tracker (prefix + "EQ") "Equity" equityTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = equityAcct
              entries =
                [ { accountId = cashAcct; balance = 10000m }
                  { accountId = mortgageAcct; balance = 200000m } ]
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            // Cash: debit 10000 (normal-debit asset)
            // Mortgage: credit 200000 (normal-credit liability)
            // Equity: debit 190000 (balancing — more credits than debits)
            Assert.Equal(3, posted.lines.Length)
            let totalDebits = posted.lines |> List.filter (fun l -> l.entryType = EntryType.Debit) |> List.sumBy (fun l -> l.amount)
            let totalCredits = posted.lines |> List.filter (fun l -> l.entryType = EntryType.Credit) |> List.sumBy (fun l -> l.amount)
            Assert.Equal(totalDebits, totalCredits)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-002")>]
let ``balancing line computed correctly when debits exceed credits`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cashAcct = InsertHelpers.insertAccount conn tracker (prefix + "CA") "Cash" assetTypeId true
        let equityAcct = InsertHelpers.insertAccount conn tracker (prefix + "EQ") "Equity" equityTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = equityAcct
              entries = [ { accountId = cashAcct; balance = 5000m } ]
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            // Cash debit 5000, Equity credit 5000
            let equityLine = posted.lines |> List.find (fun l -> l.accountId = equityAcct)
            Assert.Equal(EntryType.Credit, equityLine.entryType)
            Assert.Equal(5000m, equityLine.amount)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-003")>]
let ``balancing line computed correctly when credits exceed debits`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let mortgageAcct = InsertHelpers.insertAccount conn tracker (prefix + "MO") "Mortgage" liabilityTypeId true
        let equityAcct = InsertHelpers.insertAccount conn tracker (prefix + "EQ") "Equity" equityTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = equityAcct
              entries = [ { accountId = mortgageAcct; balance = 200000m } ]
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            // Mortgage credit 200000, Equity debit 200000
            let equityLine = posted.lines |> List.find (fun l -> l.accountId = equityAcct)
            Assert.Equal(EntryType.Debit, equityLine.entryType)
            Assert.Equal(200000m, equityLine.amount)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-004")>]
let ``single account entry creates two-line journal entry`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cashAcct = InsertHelpers.insertAccount conn tracker (prefix + "CA") "Cash" assetTypeId true
        let equityAcct = InsertHelpers.insertAccount conn tracker (prefix + "EQ") "Equity" equityTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = equityAcct
              entries = [ { accountId = cashAcct; balance = 1000m } ]
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            Assert.Equal(2, posted.lines.Length)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-005")>]
let ``posted entry is retrievable and has correct line count`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cashAcct = InsertHelpers.insertAccount conn tracker (prefix + "CA") "Cash" assetTypeId true
        let brokerAcct = InsertHelpers.insertAccount conn tracker (prefix + "BR") "Brokerage" assetTypeId true
        let mortgageAcct = InsertHelpers.insertAccount conn tracker (prefix + "MO") "Mortgage" liabilityTypeId true
        let equityAcct = InsertHelpers.insertAccount conn tracker (prefix + "EQ") "Equity" equityTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = equityAcct
              entries =
                [ { accountId = cashAcct; balance = 10000m }
                  { accountId = brokerAcct; balance = 50000m }
                  { accountId = mortgageAcct; balance = 200000m } ]
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            // 3 accounts + 1 balancing = 4 lines
            Assert.Equal(4, posted.lines.Length)
            Assert.Equal("Opening balances", posted.entry.description)
            Assert.Equal(Some "opening_balance", posted.entry.source)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-006")>]
let ``duplicate account in entries returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cashAcct = InsertHelpers.insertAccount conn tracker (prefix + "CA") "Cash" assetTypeId true
        let equityAcct = InsertHelpers.insertAccount conn tracker (prefix + "EQ") "Equity" equityTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = equityAcct
              entries =
                [ { accountId = cashAcct; balance = 1000m }
                  { accountId = cashAcct; balance = 2000m } ]
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            Assert.Fail("Expected Error for duplicate accounts")
        | Error errs ->
            let msg = errs |> String.concat " "
            Assert.Contains("Duplicate", msg)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-007")>]
let ``empty entries list returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let equityAcct = InsertHelpers.insertAccount conn tracker (prefix + "EQ") "Equity" equityTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = equityAcct
              entries = []
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok _ -> Assert.Fail("Expected Error for empty entries")
        | Error errs ->
            let msg = errs |> String.concat " "
            Assert.Contains("empty", msg)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-008")>]
let ``zero balance entry returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cashAcct = InsertHelpers.insertAccount conn tracker (prefix + "CA") "Cash" assetTypeId true
        let equityAcct = InsertHelpers.insertAccount conn tracker (prefix + "EQ") "Equity" equityTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = equityAcct
              entries = [ { accountId = cashAcct; balance = 0m } ]
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok _ -> Assert.Fail("Expected Error for zero balance")
        | Error errs ->
            let msg = errs |> String.concat " "
            Assert.Contains("non-positive", msg)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-009")>]
let ``balancing account in entries list returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cashAcct = InsertHelpers.insertAccount conn tracker (prefix + "CA") "Cash" assetTypeId true
        let equityAcct = InsertHelpers.insertAccount conn tracker (prefix + "EQ") "Equity" equityTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = equityAcct
              entries =
                [ { accountId = cashAcct; balance = 1000m }
                  { accountId = equityAcct; balance = 500m } ]
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            Assert.Fail("Expected Error for balancing account in entries")
        | Error errs ->
            let msg = errs |> String.concat " "
            Assert.Contains("Balancing account", msg)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-010")>]
let ``nonexistent account returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let equityAcct = InsertHelpers.insertAccount conn tracker (prefix + "EQ") "Equity" equityTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = equityAcct
              entries = [ { accountId = 999999; balance = 1000m } ]
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            Assert.Fail("Expected Error for nonexistent account")
        | Error errs ->
            let msg = errs |> String.concat " "
            Assert.Contains("does not exist", msg)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-011")>]
let ``nonexistent balancing account returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cashAcct = InsertHelpers.insertAccount conn tracker (prefix + "CA") "Cash" assetTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = 999999
              entries = [ { accountId = cashAcct; balance = 1000m } ]
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            Assert.Fail("Expected Error for nonexistent balancing account")
        | Error errs ->
            let msg = errs |> String.concat " "
            Assert.Contains("does not exist", msg)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-012")>]
let ``non-equity balancing account returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cashAcct = InsertHelpers.insertAccount conn tracker (prefix + "CA") "Cash" assetTypeId true
        let bankAcct = InsertHelpers.insertAccount conn tracker (prefix + "BK") "Bank" assetTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = bankAcct
              entries = [ { accountId = cashAcct; balance = 1000m } ]
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            Assert.Fail("Expected Error for non-equity balancing account")
        | Error errs ->
            let msg = errs |> String.concat " "
            Assert.Contains("equity", msg)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-013")>]
let ``default description is Opening balances when not provided`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cashAcct = InsertHelpers.insertAccount conn tracker (prefix + "CA") "Cash" assetTypeId true
        let equityAcct = InsertHelpers.insertAccount conn tracker (prefix + "EQ") "Equity" equityTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = equityAcct
              entries = [ { accountId = cashAcct; balance = 1000m } ]
              description = None }
        match OpeningBalanceService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            Assert.Equal("Opening balances", posted.entry.description)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OB-014")>]
let ``custom description is used when provided`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cashAcct = InsertHelpers.insertAccount conn tracker (prefix + "CA") "Cash" assetTypeId true
        let equityAcct = InsertHelpers.insertAccount conn tracker (prefix + "EQ") "Equity" equityTypeId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 1, 31)) true
        let cmd : PostOpeningBalancesCommand =
            { entryDate = DateOnly(2026, 1, 1)
              fiscalPeriodId = fpId
              balancingAccountId = equityAcct
              entries = [ { accountId = cashAcct; balance = 1000m } ]
              description = Some "Migration from QuickBooks" }
        match OpeningBalanceService.post cmd with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            Assert.Equal("Migration from QuickBooks", posted.entry.description)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Structural tests -- acceptance criteria owned by QE
// =====================================================================

[<Fact>]
let ``OpeningBalanceEntry type has required fields`` () =
    let entry : OpeningBalanceEntry =
        { accountId = 1
          balance = 1000m }
    Assert.Equal(1, entry.accountId)
    Assert.Equal(1000m, entry.balance)

[<Fact>]
let ``PostOpeningBalancesCommand type has required fields`` () =
    let cmd : PostOpeningBalancesCommand =
        { entryDate = DateOnly(2026, 1, 1)
          fiscalPeriodId = 1
          balancingAccountId = 99
          entries = []
          description = Some "Test" }
    Assert.Equal(DateOnly(2026, 1, 1), cmd.entryDate)
    Assert.Equal(1, cmd.fiscalPeriodId)
    Assert.Equal(99, cmd.balancingAccountId)
    Assert.Equal(Some "Test", cmd.description)
