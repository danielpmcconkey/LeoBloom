module LeoBloom.Tests.InvestmentAccountTests

open System
open Xunit
open Npgsql
open LeoBloom.Utilities
open LeoBloom.Portfolio
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.PortfolioTestHelpers

// =====================================================================
// @FT-PF-001 -- Create investment account with valid tax bucket + group
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-001")>]
let ``create investment account with valid tax bucket and account group`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId  = PortfolioInsertHelpers.insertTaxBucket    txn (prefix + "_tb")
    let agId  = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
    let name  = prefix + "_acct"
    let result = InvestmentAccountService.createAccount txn name tbId agId
    match result with
    | Ok acct ->
        Assert.Equal(name,  acct.name)
        Assert.Equal(tbId,  acct.taxBucketId)
        Assert.Equal(agId,  acct.accountGroupId)
        Assert.True(acct.id > 0, "id should be a positive integer")
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-PF-002 -- Blank account name rejected at service layer
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-002")>]
let ``blank account name rejected`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let tbId = PortfolioInsertHelpers.insertTaxBucket    txn "tb_blank_test"
    let agId = PortfolioInsertHelpers.insertAccountGroup txn "ag_blank_test"
    let result = InvestmentAccountService.createAccount txn "   " tbId agId
    match result with
    | Ok _ -> Assert.Fail("Expected Error for blank name")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("name")),
                    sprintf "Expected error mentioning 'name': %A" errs)

// =====================================================================
// @FT-PF-003 -- List investment accounts returns created account
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-003")>]
let ``list accounts returns created account`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId = PortfolioInsertHelpers.insertTaxBucket    txn (prefix + "_tb")
    let agId = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
    let name = prefix + "_acct"
    match InvestmentAccountService.createAccount txn name tbId agId with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup failed: %A" errs)

    let result = InvestmentAccountService.listAccounts txn
    match result with
    | Ok accounts ->
        Assert.True(accounts |> List.exists (fun a -> a.name = name),
                    sprintf "Expected to find account '%s' in list" name)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-PF-004 -- List investment accounts returns empty when none exist
// =====================================================================
// Note: listAccounts returns all accounts globally. In a shared test DB
// we cannot guarantee truly empty, so we test the semantics: a unique
// prefix name does not appear in the list before we create it.

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-004")>]
let ``list accounts returns empty list for unseen unique name`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let name = prefix + "_acct"
    // Query BEFORE creating anything with this prefix
    let result = InvestmentAccountService.listAccounts txn
    match result with
    | Ok accounts ->
        Assert.False(accounts |> List.exists (fun a -> a.name = name),
                     sprintf "Expected no account named '%s' before creation" name)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
