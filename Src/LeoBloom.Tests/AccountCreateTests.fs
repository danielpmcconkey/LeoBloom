module LeoBloom.Tests.AccountCreateTests

open System
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Ledger
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

// =======================================================================
// Account Create — Service Layer Behavioral Specs
//
// Maps to Specs/Behavioral/AccountCrud.feature
// Tags: @FT-AC-010, @FT-AC-011 and related create-with-subtype scenarios.
//
// Seed account_type IDs (from 1712000002000_CreateAccountType.sql):
//   1 = asset, 5 = expense
// All tests use transactions; no data is committed.
// =======================================================================

let private assetTypeId   = 1
let private expenseTypeId = 5

// =====================================================================
// @FT-AC-010 — Create with valid subtype (Cash for Asset) succeeds
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-010")>]
let ``creating an asset account with Cash subtype succeeds and persists subtype`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let result =
        AccountService.createAccount txn
            { code = prefix + "CA10"; name = "Cash Account"; accountTypeId = assetTypeId
              parentId = None; subType = Some Cash }
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" errs)
    | Ok acct ->
        Assert.True(acct.id > 0, "id should be positive")
        Assert.Equal(Some Cash, acct.subType)
        Assert.Equal(assetTypeId, acct.accountTypeId)

// =====================================================================
// @FT-AC-010b — Create without subtype succeeds with None subtype
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-010b")>]
let ``creating an account without subtype succeeds and subtype is None`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let result =
        AccountService.createAccount txn
            { code = prefix + "CA11"; name = "No Subtype Account"; accountTypeId = assetTypeId
              parentId = None; subType = None }
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" errs)
    | Ok acct ->
        Assert.Equal(None, acct.subType)

// =====================================================================
// @FT-AC-011 — Create with invalid subtype (Cash for Expense) is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-011")>]
let ``creating an expense account with Cash subtype is rejected with subtype error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let result =
        AccountService.createAccount txn
            { code = prefix + "CA12"; name = "Bad Subtype Account"; accountTypeId = expenseTypeId
              parentId = None; subType = Some Cash }
    match result with
    | Ok _ -> Assert.Fail("Expected Error for invalid subtype, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("subtype")),
            sprintf "Expected error to mention 'subtype': %A" errs)

// =====================================================================
// Duplicate code rejection still works with subType field present
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-010c")>]
let ``creating account with duplicate code is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let code = prefix + "DUP"
    match AccountService.createAccount txn
            { code = code; name = "Original"; accountTypeId = assetTypeId
              parentId = None; subType = None } with
    | Error errs -> Assert.Fail(sprintf "Setup: %A" errs)
    | Ok _ -> ()
    let result =
        AccountService.createAccount txn
            { code = code; name = "Duplicate"; accountTypeId = assetTypeId
              parentId = None; subType = None }
    match result with
    | Ok _ -> Assert.Fail("Expected Error for duplicate code, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("already exists")),
            sprintf "Expected 'already exists' in error: %A" errs)

// =====================================================================
// Invalid parent rejection still works with subType field present
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-010d")>]
let ``creating account with nonexistent parent is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let result =
        AccountService.createAccount txn
            { code = prefix + "NPR"; name = "No Parent Account"; accountTypeId = assetTypeId
              parentId = Some 99999; subType = None }
    match result with
    | Ok _ -> Assert.Fail("Expected Error for invalid parent, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("does not exist")),
            sprintf "Expected 'does not exist' in error: %A" errs)
