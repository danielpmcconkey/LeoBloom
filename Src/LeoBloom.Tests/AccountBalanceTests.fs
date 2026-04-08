module LeoBloom.Tests.AccountBalanceTests

open System
open Xunit
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Tests.TestHelpers

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

[<Fact>]
[<Trait("GherkinId", "FT-AB-001")>]
let ``normal-debit account balance after single entry`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let revAt = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetAt true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Single debit" 1000m |> ignore
    let result = AccountBalanceService.getBalanceById txn assetAcct (DateOnly(2026, 3, 31))
    match result with
    | Ok bal ->
        Assert.Equal(1000m, bal.balance)
        Assert.Equal(NormalBalance.Debit, bal.normalBalance)
        Assert.Equal(prefix + "AS", bal.accountCode)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-AB-002")>]
let ``normal-credit account balance after single entry`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let revAt = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetAt true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Single credit" 1000m |> ignore
    let result = AccountBalanceService.getBalanceById txn revAcct (DateOnly(2026, 3, 31))
    match result with
    | Ok bal ->
        Assert.Equal(1000m, bal.balance)
        Assert.Equal(NormalBalance.Credit, bal.normalBalance)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-AB-003")>]
let ``balance accumulates across multiple entries`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let revAt = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetAt true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 10)) "Entry 1" 500m |> ignore
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 20)) "Entry 2" 300m |> ignore
    let result = AccountBalanceService.getBalanceById txn assetAcct (DateOnly(2026, 3, 31))
    match result with
    | Ok bal -> Assert.Equal(800m, bal.balance)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-AB-004")>]
let ``mixed debits and credits net correctly`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let revAt = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let expAt = InsertHelpers.insertAccountType txn (prefix + "_ex") "debit"
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetAt true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revAt true
    let expAcct = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    // First entry: debit asset 1000, credit revenue 1000
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 10)) "Debit entry" 1000m |> ignore
    // Second entry: debit expense 400, credit asset 400
    postEntry txn expAcct assetAcct fpId (DateOnly(2026, 3, 20)) "Credit entry" 400m |> ignore
    let result = AccountBalanceService.getBalanceById txn assetAcct (DateOnly(2026, 3, 31))
    match result with
    | Ok bal -> Assert.Equal(600m, bal.balance)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-AB-005")>]
let ``voided entry excluded from balance`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let revAt = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetAt true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let _entryId1 = postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 10)) "Keep entry" 500m
    let entryId2 = postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 20)) "Void entry" 200m
    let voidCmd = { journalEntryId = entryId2; voidReason = "Test void" }
    match JournalEntryService.voidEntry txn voidCmd with
    | Error errs -> Assert.Fail(sprintf "Void failed: %A" errs)
    | Ok _ -> ()
    let result = AccountBalanceService.getBalanceById txn assetAcct (DateOnly(2026, 3, 31))
    match result with
    | Ok bal -> Assert.Equal(500m, bal.balance)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-AB-006")>]
let ``entry after as_of_date excluded`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let revAt = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetAt true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 10)) "Before cutoff" 500m |> ignore
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 25)) "After cutoff" 300m |> ignore
    let result = AccountBalanceService.getBalanceById txn assetAcct (DateOnly(2026, 3, 15))
    match result with
    | Ok bal -> Assert.Equal(500m, bal.balance)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-AB-007")>]
let ``account with no entries has zero balance`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetAt true
    let result = AccountBalanceService.getBalanceById txn assetAcct (DateOnly(2026, 3, 31))
    match result with
    | Ok bal -> Assert.Equal(0m, bal.balance)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-AB-008")>]
let ``inactive account balance is calculated`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let revAt = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetAt true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Before deactivate" 500m |> ignore
    use cmd = new NpgsqlCommand("UPDATE ledger.account SET is_active = false WHERE id = @id", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@id", assetAcct) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    let result = AccountBalanceService.getBalanceById txn assetAcct (DateOnly(2026, 3, 31))
    match result with
    | Ok bal -> Assert.Equal(500m, bal.balance)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)

[<Fact>]
[<Trait("GherkinId", "FT-AB-009")>]
let ``nonexistent account ID returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result = AccountBalanceService.getBalanceById txn 999999 (DateOnly(2026, 3, 31))
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent account ID")
    | Error err ->
        Assert.Contains("does not exist", err)

[<Fact>]
[<Trait("GherkinId", "FT-AB-010")>]
let ``nonexistent account code returns error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result = AccountBalanceService.getBalanceByCode txn "ZZZZ" (DateOnly(2026, 3, 31))
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent account code")
    | Error err ->
        Assert.Contains("does not exist", err)

[<Fact>]
[<Trait("GherkinId", "FT-AB-011")>]
let ``lookup by account code matches lookup by ID`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let revAt = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let assetAcct = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" assetAt true
    let revAcct = InsertHelpers.insertAccount txn (prefix + "RV") "Revenue" revAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    postEntry txn assetAcct revAcct fpId (DateOnly(2026, 3, 15)) "Code lookup" 750m |> ignore
    let result = AccountBalanceService.getBalanceByCode txn (prefix + "AS") (DateOnly(2026, 3, 31))
    match result with
    | Ok bal ->
        Assert.Equal(750m, bal.balance)
        Assert.Equal(NormalBalance.Debit, bal.normalBalance)
    | Error err -> Assert.Fail(sprintf "Expected Ok: %s" err)
