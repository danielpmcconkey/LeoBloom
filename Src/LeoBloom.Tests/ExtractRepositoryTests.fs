module LeoBloom.Tests.ExtractRepositoryTests

open System
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Reporting.ExtractRepository
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.PortfolioTestHelpers

// =====================================================================
// Account Tree
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-100")>]
let ``account tree returns non-empty result with required fields`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    InsertHelpers.insertAccount txn (prefix + "01") "Root Account" atId true |> ignore

    let rows = getAccountTree txn

    Assert.NotEmpty(rows)
    for row in rows do
        Assert.False(String.IsNullOrEmpty(row.code), "code should be non-empty")
        Assert.False(String.IsNullOrEmpty(row.name), "name should be non-empty")
        Assert.False(String.IsNullOrEmpty(row.accountType), "accountType should be non-empty")

[<Fact>]
[<Trait("GherkinId", "FT-EXT-100")>]
let ``account tree parent_id references valid id in result`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let parentId = InsertHelpers.insertAccount txn (prefix + "00") "Parent" atId true
    InsertHelpers.insertAccountWithParent txn (prefix + "01") "Child" atId parentId true |> ignore

    let rows = getAccountTree txn
    let ids = rows |> List.map (fun r -> r.id) |> Set.ofList

    for row in rows do
        match row.parentId with
        | Some pid -> Assert.True(ids.Contains(pid), sprintf "parent_id %d not in result set" pid)
        | None -> ()  // root account, fine

// =====================================================================
// Balances — void filtering
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-112")>]
let ``balances excludes voided entries`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atDebit = InsertHelpers.insertAccountType txn (prefix + "_ad") "debit"
    let atCredit = InsertHelpers.insertAccountType txn (prefix + "_ac") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset" atDebit true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" atCredit true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

    // Post good entry: 500 debit
    let goodCmd =
        { entryDate = DateOnly(2026, 3, 10)
          description = "Good entry"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 500m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 500m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    JournalEntryService.post txn goodCmd |> ignore

    // Post and void another entry: 200 debit
    let voidCmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Voided entry"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 200m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 200m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let postedId =
        match JournalEntryService.post txn voidCmd with
        | Ok posted -> posted.entry.id
        | Error errs -> failwith (sprintf "Post failed: %A" errs)
    JournalEntryService.voidEntry txn { journalEntryId = postedId; voidReason = "test" } |> ignore

    let rows = getBalances txn (DateOnly(2026, 3, 31))
    let acct1Row = rows |> List.tryFind (fun r -> r.accountId = acct1)

    Assert.True(acct1Row.IsSome, "acct1 should appear in balances")
    Assert.Equal(500m, acct1Row.Value.balance)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-113")>]
let ``balances excludes account when all entries are voided`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atDebit = InsertHelpers.insertAccountType txn (prefix + "_ad") "debit"
    let atCredit = InsertHelpers.insertAccountType txn (prefix + "_ac") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset" atDebit true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" atCredit true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

    let cmd =
        { entryDate = DateOnly(2026, 3, 10)
          description = "Only entry"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 300m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 300m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let postedId =
        match JournalEntryService.post txn cmd with
        | Ok posted -> posted.entry.id
        | Error errs -> failwith (sprintf "Post failed: %A" errs)
    JournalEntryService.voidEntry txn { journalEntryId = postedId; voidReason = "test" } |> ignore

    let rows = getBalances txn (DateOnly(2026, 3, 31))

    Assert.True(rows |> List.forall (fun r -> r.accountId <> acct1), "voided account should not appear")
    Assert.True(rows |> List.forall (fun r -> r.accountId <> acct2), "voided account should not appear")

// =====================================================================
// Balances — as-of date
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-110")>]
let ``balances respects as-of date`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atDebit = InsertHelpers.insertAccountType txn (prefix + "_ad") "debit"
    let atCredit = InsertHelpers.insertAccountType txn (prefix + "_ac") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset" atDebit true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" atCredit true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 4, 30)) true

    // Early entry: 1000 debit
    let earlyCmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Early"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    JournalEntryService.post txn earlyCmd |> ignore

    // Later entry: 500 debit
    let laterCmd =
        { entryDate = DateOnly(2026, 4, 10)
          description = "Later"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 500m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 500m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    JournalEntryService.post txn laterCmd |> ignore

    // As-of 2026-03-31 should only include the 1000 entry
    let rows = getBalances txn (DateOnly(2026, 3, 31))
    let acct1Row = rows |> List.tryFind (fun r -> r.accountId = acct1)

    Assert.True(acct1Row.IsSome)
    Assert.Equal(1000m, acct1Row.Value.balance)

// =====================================================================
// Portfolio Positions
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-120")>]
let ``positions returns latest snapshot per account-symbol pair`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId = PortfolioInsertHelpers.insertTaxBucket txn (prefix + "_tb")
    let agId = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
    let iaId = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_ia") tbId agId
    let symbol = prefix + "SY"
    PortfolioInsertHelpers.insertFund txn symbol (prefix + "_fund") |> ignore

    // Older snapshot
    PortfolioInsertHelpers.insertPosition txn iaId symbol (DateOnly(2026, 2, 28)) 100m 10m 23000m 20000m |> ignore
    // Newer snapshot
    PortfolioInsertHelpers.insertPosition txn iaId symbol (DateOnly(2026, 3, 31)) 110m 10m 24500m 20000m |> ignore

    let rows = getPositions txn (DateOnly(2026, 4, 11))
    let matchRows = rows |> List.filter (fun r -> r.investmentAccountId = iaId && r.symbol = symbol)

    Assert.Equal(1, matchRows.Length)
    Assert.Equal(24500m, matchRows.[0].currentValue)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-123")>]
let ``positions excludes zero current_value`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId = PortfolioInsertHelpers.insertTaxBucket txn (prefix + "_tb")
    let agId = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
    let iaId = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_ia") tbId agId
    let symbol = prefix + "SY"
    PortfolioInsertHelpers.insertFund txn symbol (prefix + "_fund") |> ignore

    PortfolioInsertHelpers.insertPosition txn iaId symbol (DateOnly(2026, 3, 31)) 0m 0m 0m 0m |> ignore

    let rows = getPositions txn (DateOnly(2026, 4, 11))
    let matchRows = rows |> List.filter (fun r -> r.investmentAccountId = iaId && r.symbol = symbol)

    Assert.Empty(matchRows)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-124")>]
let ``positions excludes position when latest snapshot has zero value even if earlier had value`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId = PortfolioInsertHelpers.insertTaxBucket txn (prefix + "_tb")
    let agId = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
    let iaId = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_ia") tbId agId
    let symbol = prefix + "SY"
    PortfolioInsertHelpers.insertFund txn symbol (prefix + "_fund") |> ignore

    // Earlier snapshot with value
    PortfolioInsertHelpers.insertPosition txn iaId symbol (DateOnly(2026, 2, 28)) 100m 10m 24500m 20000m |> ignore
    // Latest snapshot: zero (fully liquidated)
    PortfolioInsertHelpers.insertPosition txn iaId symbol (DateOnly(2026, 3, 31)) 0m 0m 0m 0m |> ignore

    let rows = getPositions txn (DateOnly(2026, 4, 11))
    let matchRows = rows |> List.filter (fun r -> r.investmentAccountId = iaId && r.symbol = symbol)

    Assert.Empty(matchRows)

// =====================================================================
// JE Lines — fiscal period filtering
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-130")>]
let ``je-lines returns only lines for the specified fiscal period`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atDebit = InsertHelpers.insertAccountType txn (prefix + "_ad") "debit"
    let atCredit = InsertHelpers.insertAccountType txn (prefix + "_ac") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset" atDebit true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" atCredit true
    let fp1 = InsertHelpers.insertFiscalPeriod txn (prefix + "P1") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let fp2 = InsertHelpers.insertFiscalPeriod txn (prefix + "P2") (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true

    let period1Cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Period 1 entry"
          source = None
          fiscalPeriodId = fp1
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    JournalEntryService.post txn period1Cmd |> ignore

    let period2Cmd =
        { entryDate = DateOnly(2026, 4, 5)
          description = "Period 2 entry"
          source = None
          fiscalPeriodId = fp2
          lines =
            [ { accountId = acct1; amount = 500m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 500m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    JournalEntryService.post txn period2Cmd |> ignore

    let rows = getJournalEntryLines txn fp1 true None

    Assert.Equal(2, rows.Length)
    Assert.True(rows |> List.forall (fun r -> r.entryDate = DateOnly(2026, 3, 15)))

[<Fact>]
[<Trait("GherkinId", "FT-EXT-132")>]
let ``je-lines excludes voided entries`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atDebit = InsertHelpers.insertAccountType txn (prefix + "_ad") "debit"
    let atCredit = InsertHelpers.insertAccountType txn (prefix + "_ac") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset" atDebit true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" atCredit true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

    let goodCmd =
        { entryDate = DateOnly(2026, 3, 10)
          description = "Good entry"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 500m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 500m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    JournalEntryService.post txn goodCmd |> ignore

    let voidCmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Voided entry"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 200m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 200m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let postedId =
        match JournalEntryService.post txn voidCmd with
        | Ok posted -> posted.entry.id
        | Error errs -> failwith (sprintf "Post failed: %A" errs)
    JournalEntryService.voidEntry txn { journalEntryId = postedId; voidReason = "test" } |> ignore

    let rows = getJournalEntryLines txn fpId true None

    Assert.Equal(2, rows.Length)
    Assert.True(rows |> List.forall (fun r -> r.description = "Good entry"))

// =====================================================================
// Account Tree — active/inactive and ordering
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-101")>]
let ``account tree includes both active and inactive accounts`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let activeId   = InsertHelpers.insertAccount txn (prefix + "01") "Active"   atId true
    let inactiveId = InsertHelpers.insertAccount txn (prefix + "02") "Inactive" atId false

    let rows = getAccountTree txn
    let activeRow   = rows |> List.tryFind (fun r -> r.id = activeId)
    let inactiveRow = rows |> List.tryFind (fun r -> r.id = inactiveId)

    Assert.True(activeRow.IsSome,   "active account should appear")
    Assert.True(inactiveRow.IsSome, "inactive account should appear")
    Assert.True(activeRow.Value.isActive,    "active row has isActive = true")
    Assert.False(inactiveRow.Value.isActive, "inactive row has isActive = false")

[<Fact>]
[<Trait("GherkinId", "FT-EXT-102")>]
let ``account tree result is ordered by account code ascending`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    InsertHelpers.insertAccount txn (prefix + "03") "Third"  atId true |> ignore
    InsertHelpers.insertAccount txn (prefix + "01") "First"  atId true |> ignore
    InsertHelpers.insertAccount txn (prefix + "02") "Second" atId true |> ignore

    let rows = getAccountTree txn
    let codes = rows |> List.map (fun r -> r.code)

    let sorted = codes |> List.sort
    Assert.Equal<string list>(sorted, codes)

// =====================================================================
// Balances — on-date inclusion, raw debit-minus-credit, zero omission
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-111")>]
let ``balances includes entries dated exactly on the as-of date`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atDebit  = InsertHelpers.insertAccountType txn (prefix + "_ad") "debit"
    let atCredit = InsertHelpers.insertAccountType txn (prefix + "_ac") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset"   atDebit  true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" atCredit true
    let fpId  = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

    let cmd =
        { entryDate = DateOnly(2026, 3, 31)
          description = "On-date entry"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 750m; entryType = EntryType.Debit;  memo = None }
              { accountId = acct2; amount = 750m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    JournalEntryService.post txn cmd |> ignore

    let rows = getBalances txn (DateOnly(2026, 3, 31))
    let acct1Row = rows |> List.tryFind (fun r -> r.accountId = acct1)

    Assert.True(acct1Row.IsSome, "on-date entry should be included")
    Assert.Equal(750m, acct1Row.Value.balance)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-114")>]
let ``balances is raw debit-minus-credit regardless of normal balance side`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atDebit  = InsertHelpers.insertAccountType txn (prefix + "_ad") "debit"
    let atCredit = InsertHelpers.insertAccountType txn (prefix + "_ac") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset"   atDebit  true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" atCredit true
    let fpId  = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Raw balance entry"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit;  memo = None }
              { accountId = acct2; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    JournalEntryService.post txn cmd |> ignore

    let rows = getBalances txn (DateOnly(2026, 3, 31))
    let acct1Row = rows |> List.tryFind (fun r -> r.accountId = acct1)
    let acct2Row = rows |> List.tryFind (fun r -> r.accountId = acct2)

    Assert.True(acct1Row.IsSome, "debit account should appear")
    Assert.True(acct2Row.IsSome, "credit account should appear")
    Assert.Equal(1000m,  acct1Row.Value.balance)   // debit: +1000
    Assert.Equal(-1000m, acct2Row.Value.balance)   // credit: -1000

[<Fact>]
[<Trait("GherkinId", "FT-EXT-115")>]
let ``account with net-zero balance is omitted from balances output`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atDebit  = InsertHelpers.insertAccountType txn (prefix + "_ad") "debit"
    let atCredit = InsertHelpers.insertAccountType txn (prefix + "_ac") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset"   atDebit  true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" atCredit true
    let fpId  = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

    let income =
        { entryDate = DateOnly(2026, 3, 10)
          description = "Income"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit;  memo = None }
              { accountId = acct2; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    JournalEntryService.post txn income |> ignore

    let offset =
        { entryDate = DateOnly(2026, 3, 20)
          description = "Offset"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct2; amount = 1000m; entryType = EntryType.Debit;  memo = None }
              { accountId = acct1; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    JournalEntryService.post txn offset |> ignore

    let rows = getBalances txn (DateOnly(2026, 3, 31))

    Assert.True(rows |> List.forall (fun r -> r.accountId <> acct1), "zero-balance asset should be omitted")
    Assert.True(rows |> List.forall (fun r -> r.accountId <> acct2), "zero-balance revenue should be omitted")

// =====================================================================
// Portfolio Positions — as-of cutoff and multiple accounts
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-121")>]
let ``positions as-of date excludes snapshots taken after the cutoff`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId = PortfolioInsertHelpers.insertTaxBucket txn (prefix + "_tb")
    let agId = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
    let iaId = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_ia") tbId agId
    let symbol = prefix + "SY"
    PortfolioInsertHelpers.insertFund txn symbol (prefix + "_fund") |> ignore

    // Pre-cutoff snapshot
    PortfolioInsertHelpers.insertPosition txn iaId symbol (DateOnly(2026, 1, 31)) 100m 10m 22000m 20000m |> ignore
    // Post-cutoff snapshot
    PortfolioInsertHelpers.insertPosition txn iaId symbol (DateOnly(2026, 3, 31)) 110m 10m 24500m 20000m |> ignore

    let rows = getPositions txn (DateOnly(2026, 2, 28))
    let matchRows = rows |> List.filter (fun r -> r.investmentAccountId = iaId && r.symbol = symbol)

    Assert.Equal(1, matchRows.Length)
    Assert.Equal(22000m, matchRows.[0].currentValue)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-122")>]
let ``positions returns one row per distinct account-symbol pair across multiple accounts`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId1 = PortfolioInsertHelpers.insertTaxBucket txn (prefix + "_t1")
    let tbId2 = PortfolioInsertHelpers.insertTaxBucket txn (prefix + "_t2")
    let agId  = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
    let ia1   = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_i1") tbId1 agId
    let ia2   = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_i2") tbId2 agId
    let symbol = prefix + "SY"
    PortfolioInsertHelpers.insertFund txn symbol (prefix + "_fund") |> ignore

    PortfolioInsertHelpers.insertPosition txn ia1 symbol (DateOnly(2026, 3, 31)) 100m 10m 24500m 20000m |> ignore
    PortfolioInsertHelpers.insertPosition txn ia2 symbol (DateOnly(2026, 3, 31)) 100m  5m 10000m  8000m |> ignore

    let rows = getPositions txn (DateOnly(2026, 4, 11))
    let matchRows = rows |> List.filter (fun r -> r.symbol = symbol && (r.investmentAccountId = ia1 || r.investmentAccountId = ia2))

    Assert.Equal(2, matchRows.Length)
    let ids = matchRows |> List.map (fun r -> r.investmentAccountId) |> Set.ofList
    Assert.Equal(2, ids.Count)

// =====================================================================
// JE Lines — empty period and ordering
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-EXT-131")>]
let ``je-lines for a period with no entries returns empty list`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

    let rows = getJournalEntryLines txn fpId true None

    Assert.Empty(rows)

[<Fact>]
[<Trait("GherkinId", "FT-EXT-133")>]
let ``je-lines are ordered by account_code ASC then entry_date ASC then journal_entry_id ASC`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atDebit  = InsertHelpers.insertAccountType txn (prefix + "_ad") "debit"
    let atCredit = InsertHelpers.insertAccountType txn (prefix + "_ac") "credit"
    // Use codes that produce predictable ordering: prefix+"A" < prefix+"Z"
    let acctA = InsertHelpers.insertAccount txn (prefix + "A") "AccountA" atDebit  true
    let acctZ = InsertHelpers.insertAccount txn (prefix + "Z") "AccountZ" atCredit true
    let fpId  = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

    // Entry1 (earlier date)
    let early =
        { entryDate = DateOnly(2026, 3, 10)
          description = "Early entry"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acctA; amount = 100m; entryType = EntryType.Debit;  memo = None }
              { accountId = acctZ; amount = 100m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let earlyId =
        match JournalEntryService.post txn early with
        | Ok posted -> posted.entry.id
        | Error e -> failwith (sprintf "Post failed: %A" e)

    // Entry2 (later date)
    let late =
        { entryDate = DateOnly(2026, 3, 20)
          description = "Late entry"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acctA; amount = 200m; entryType = EntryType.Debit;  memo = None }
              { accountId = acctZ; amount = 200m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let lateId =
        match JournalEntryService.post txn late with
        | Ok posted -> posted.entry.id
        | Error e -> failwith (sprintf "Post failed: %A" e)

    let rows = getJournalEntryLines txn fpId true None
    let myRows = rows |> List.filter (fun r -> r.accountId = acctA || r.accountId = acctZ)

    // Should be 4 lines: (A,03-10), (A,03-20), (Z,03-10), (Z,03-20)
    Assert.Equal(4, myRows.Length)

    // Verify code ordering: first two rows are for acctA, last two for acctZ
    let codeA = prefix + "A"
    let codeZ = prefix + "Z"
    Assert.Equal(codeA, myRows.[0].accountCode)
    Assert.Equal(codeA, myRows.[1].accountCode)
    Assert.Equal(codeZ, myRows.[2].accountCode)
    Assert.Equal(codeZ, myRows.[3].accountCode)

    // Within acctA: earlier date first
    Assert.Equal(DateOnly(2026, 3, 10), myRows.[0].entryDate)
    Assert.Equal(DateOnly(2026, 3, 20), myRows.[1].entryDate)

    // Within acctZ: earlier date first
    Assert.Equal(DateOnly(2026, 3, 10), myRows.[2].entryDate)
    Assert.Equal(DateOnly(2026, 3, 20), myRows.[3].entryDate)

    // JE id ordering: earlier entry has smaller id
    Assert.True(earlyId < lateId, "earlyId should be less than lateId")
    Assert.Equal(earlyId, myRows.[0].journalEntryId)
    Assert.Equal(lateId,  myRows.[1].journalEntryId)
