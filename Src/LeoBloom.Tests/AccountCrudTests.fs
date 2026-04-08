module LeoBloom.Tests.AccountCrudTests

open System
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Ledger
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

// =======================================================================
// Account CRUD — Service Layer Behavioral Specs
//
// Maps to Specs/Behavioral/AccountCrud.feature
// Tags: @FT-AC-001 through @FT-AC-009
//
// Fiscal period isolation: this file uses year 2096 (FT-AC-009 only).
// All tests use transactions; no data is committed.
// =======================================================================

// Seed account_type IDs (from 1712000002000_CreateAccountType.sql):
//   1 = asset, 4 = revenue
let private assetTypeId   = 1
let private revenueTypeId = 4

// =====================================================================
// @FT-AC-001 — Create account with valid data succeeds
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-001")>]
let ``creating an account with valid data returns ok with correct fields`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // When I create an account with code "crud-1010", name "Test Cash", and type asset
    let result =
        AccountService.createAccount txn
            { code = "crud-1010"; name = "Test Cash"; accountTypeId = assetTypeId; parentId = None; subType = None; externalRef = None }
    // Then the account is created with a valid id, is_active true, and the given code and name
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" errs)
    | Ok acct ->
        Assert.True(acct.id > 0, "id should be a positive integer")
        Assert.Equal("crud-1010", acct.code)
        Assert.Equal("Test Cash", acct.name)
        Assert.Equal(assetTypeId, acct.accountTypeId)
        Assert.True(acct.isActive, "is_active should be true")

// =====================================================================
// @FT-AC-002 — Create with invalid account type is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-002")>]
let ``creating an account with an invalid account type is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // When I create an account with code "crud-1020", name "Bad Type Account", and type_id 99999
    let result =
        AccountService.createAccount txn
            { code = "crud-1020"; name = "Bad Type Account"; accountTypeId = 99999; parentId = None; subType = None; externalRef = None }
    // Then the account creation fails with error containing "account type"
    match result with
    | Ok _ -> Assert.Fail("Expected Error for invalid account type, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("account type")),
            sprintf "Expected error to mention 'account type': %A" errs)

// =====================================================================
// @FT-AC-003 — Create with duplicate code is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-003")>]
let ``creating an account with a duplicate code is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // Given a crud-test active account with code "crud-1030" and name "Original"
    match AccountService.createAccount txn
            { code = "crud-1030"; name = "Original"; accountTypeId = assetTypeId; parentId = None; subType = None; externalRef = None } with
    | Error errs -> Assert.Fail(sprintf "Setup: failed to create original account: %A" errs)
    | Ok _ -> ()
    // When I create an account with code "crud-1030", name "Duplicate", and type asset
    let result =
        AccountService.createAccount txn
            { code = "crud-1030"; name = "Duplicate"; accountTypeId = assetTypeId; parentId = None; subType = None; externalRef = None }
    // Then the account creation fails with error containing "crud-1030"
    match result with
    | Ok _ -> Assert.Fail("Expected Error for duplicate code, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("crud-1030")),
            sprintf "Expected error to mention 'crud-1030': %A" errs)

// =====================================================================
// @FT-AC-004 — Create with invalid parent_id is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-004")>]
let ``creating an account with an invalid parent_id is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // When I create an account with code "crud-1040", name "Orphan Account", type asset, and parent_id 99999
    let result =
        AccountService.createAccount txn
            { code = "crud-1040"; name = "Orphan Account"; accountTypeId = assetTypeId; parentId = Some 99999; subType = None; externalRef = None }
    // Then the account creation fails with error containing "parent"
    match result with
    | Ok _ -> Assert.Fail("Expected Error for invalid parent_id, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("parent")),
            sprintf "Expected error to mention 'parent': %A" errs)

// =====================================================================
// @FT-AC-005 — Create under inactive parent is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-005")>]
let ``creating an account under an inactive parent is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // Given a crud-test inactive account with code "crud-1050"
    let parentId =
        InsertHelpers.insertAccount txn "crud-1050" "Inactive Parent" assetTypeId false
    // When I create an account with code "crud-1051" under parent "crud-1050"
    let result =
        AccountService.createAccount txn
            { code = "crud-1051"; name = "Child Of Inactive"; accountTypeId = assetTypeId; parentId = Some parentId; subType = None; externalRef = None }
    // Then the account creation fails with error containing "inactive"
    match result with
    | Ok _ -> Assert.Fail("Expected Error for inactive parent, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("inactive")),
            sprintf "Expected error to mention 'inactive': %A" errs)

// =====================================================================
// @FT-AC-006 — Update account name succeeds and round-trips
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-006")>]
let ``updating an account name succeeds and round-trips`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // Given a crud-test active account with code "crud-2010" and name "Original Name"
    let accountId =
        InsertHelpers.insertAccount txn "crud-2010" "Original Name" assetTypeId true
    // When I update account "crud-2010" name to "Updated Name"
    let result =
        AccountService.updateAccountName txn
            { accountId = accountId; name = "Updated Name" }
    // Then the account "crud-2010" name is "Updated Name"
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" errs)
    | Ok acct ->
        Assert.Equal("Updated Name", acct.name)
        Assert.Equal("crud-2010", acct.code)

// =====================================================================
// @FT-AC-007 — Deactivate a leaf account succeeds
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-007")>]
let ``deactivating a leaf account sets is_active to false`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // Given a crud-test active account with code "crud-3010"
    let accountId =
        InsertHelpers.insertAccount txn "crud-3010" "Leaf Account" assetTypeId true
    // When I deactivate account "crud-3010"
    let result =
        AccountService.deactivateAccount txn { accountId = accountId }
    // Then account "crud-3010" is_active is false
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" errs)
    | Ok acct ->
        Assert.False(acct.isActive, "is_active should be false after deactivation")

// =====================================================================
// @FT-AC-008 — Deactivate account with active children is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-008")>]
let ``deactivating an account with active child accounts is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // Given a crud-test active parent account with code "crud-4010"
    let parentId =
        InsertHelpers.insertAccount txn "crud-4010" "Parent Account" assetTypeId true
    // And a crud-test active child account with code "crud-4011" under parent "crud-4010"
    InsertHelpers.insertAccountWithParent txn "crud-4011" "Child Account" assetTypeId parentId true |> ignore
    // When I deactivate account "crud-4010"
    let result =
        AccountService.deactivateAccount txn { accountId = parentId }
    // Then the account deactivation fails with error containing "children"
    match result with
    | Ok _ -> Assert.Fail("Expected Error for account with children, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("children")),
            sprintf "Expected error to mention 'children': %A" errs)

// =====================================================================
// @FT-AC-009 — Deactivate account with posted journal entries succeeds
//
// Fiscal period isolation: uses year 2096.
// =======================================================================

[<Fact>]
[<Trait("GherkinId", "FT-AC-009")>]
let ``deactivating an account with posted journal entries succeeds`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // Given a crud-test active asset account with code "crud-5010"
    let assetAccountId =
        InsertHelpers.insertAccount txn "crud-5010" "Posted Account" assetTypeId true
    // And a crud-test active revenue account with code "crud-5011"
    let revenueAccountId =
        InsertHelpers.insertAccount txn "crud-5011" "Posted Contra" revenueTypeId true
    // And a crud-test open fiscal period (year 2096 reserved for this file)
    let fiscalPeriodId =
        InsertHelpers.insertFiscalPeriod txn "96-JAN" (DateOnly(2096, 1, 1)) (DateOnly(2096, 1, 31)) true
    // And a journal entry posted to accounts "crud-5010" and "crud-5011"
    let entryId =
        InsertHelpers.insertJournalEntry txn (DateOnly(2096, 1, 15)) "crud-test posted entry" fiscalPeriodId
    use lineCmd = new Npgsql.NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type)
         VALUES (@eid, @aid1, 100.00, 'debit'), (@eid, @aid2, 100.00, 'credit')",
        txn.Connection, txn)
    lineCmd.Parameters.AddWithValue("@eid", entryId) |> ignore
    lineCmd.Parameters.AddWithValue("@aid1", assetAccountId) |> ignore
    lineCmd.Parameters.AddWithValue("@aid2", revenueAccountId) |> ignore
    lineCmd.ExecuteNonQuery() |> ignore
    // When I deactivate account "crud-5010"
    let result =
        AccountService.deactivateAccount txn { accountId = assetAccountId }
    // Then account "crud-5010" is_active is false
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected Ok for deactivate-with-posted-entries, got Error: %A" errs)
    | Ok acct ->
        Assert.False(acct.isActive, "is_active should be false after deactivation")
