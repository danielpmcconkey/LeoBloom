module LeoBloom.Tests.AccountUpdateTests

open System
open System.Text.Json
open Npgsql
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Ledger
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests

// =======================================================================
// Account Update — Service Layer Tests
//
// Maps to Specs/CLI/AccountUpdateCommand.feature tags FT-ACT-070..080.
//
// Seed account_type IDs:
//   1 = asset   (valid subtypes: Cash, FixedAsset, Investment)
//   2 = liability
//   3 = equity  (no valid subtypes)
//   4 = revenue
//   5 = expense
//
// All service tests use transactions and do NOT commit.
// =======================================================================

let private assetTypeId   = 1
let private equityTypeId  = 3

// =====================================================================
// @FT-ACT-070 — Update name only
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ACT-070-svc")>]
let ``update name only returns Ok with changed name and unchanged other fields`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let acctId = InsertHelpers.insertAccount txn (prefix + "UN1") "Original Name" assetTypeId true
    let cmd = { accountId = acctId; name = Some "Updated Name"; subType = None; externalRef = None }
    match AccountService.updateAccount txn cmd with
    | Error errs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" errs)
    | Ok (before, after) ->
        Assert.Equal("Original Name", before.name)
        Assert.Equal("Updated Name", after.name)
        Assert.Equal(before.subType, after.subType)
        Assert.Equal(before.externalRef, after.externalRef)
        Assert.Equal(before.accountTypeId, after.accountTypeId)

// =====================================================================
// @FT-ACT-071 — Update subtype only (valid for type)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ACT-071-svc")>]
let ``update subtype only with valid subtype for type returns Ok with changed subtype`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let acctId = InsertHelpers.insertAccount txn (prefix + "US1") "Checking" assetTypeId true
    let cmd = { accountId = acctId; name = None; subType = Some Cash; externalRef = None }
    match AccountService.updateAccount txn cmd with
    | Error errs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" errs)
    | Ok (before, after) ->
        Assert.Equal(None, before.subType)
        Assert.Equal(Some Cash, after.subType)
        Assert.Equal(before.name, after.name)

// =====================================================================
// @FT-ACT-072 — Update external_ref only
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ACT-072-svc")>]
let ``update externalRef only returns Ok with changed externalRef`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let acctId = InsertHelpers.insertAccount txn (prefix + "UE1") "Ally Savings" assetTypeId true
    let cmd = { accountId = acctId; name = None; subType = None; externalRef = Some "x9999" }
    match AccountService.updateAccount txn cmd with
    | Error errs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" errs)
    | Ok (before, after) ->
        Assert.Equal(None, before.externalRef)
        Assert.Equal(Some "x9999", after.externalRef)
        Assert.Equal(before.name, after.name)

// =====================================================================
// @FT-ACT-073 — Update multiple fields at once
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ACT-073-svc")>]
let ``update all three fields at once changes all fields`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let acctId = InsertHelpers.insertAccount txn (prefix + "UA1") "Old Name" assetTypeId true
    let cmd = { accountId = acctId; name = Some "New Name"; subType = Some Investment; externalRef = Some "ref-001" }
    match AccountService.updateAccount txn cmd with
    | Error errs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" errs)
    | Ok (before, after) ->
        Assert.Equal("Old Name", before.name)
        Assert.Equal("New Name", after.name)
        Assert.Equal(Some Investment, after.subType)
        Assert.Equal(Some "ref-001", after.externalRef)

// =====================================================================
// @FT-ACT-076 — Invalid subtype for account type is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ACT-076-svc")>]
let ``update with subtype invalid for account type returns Error mentioning subtype`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    // equity type has no valid subtypes; Cash is only valid for asset
    let acctId = InsertHelpers.insertAccount txn (prefix + "IV1") "Bad Subtype" equityTypeId true
    let cmd = { accountId = acctId; name = None; subType = Some Cash; externalRef = None }
    match AccountService.updateAccount txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for invalid subtype, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("subtype")),
            sprintf "Expected error mentioning 'subtype': %A" errs)

// =====================================================================
// Blank name rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ACT-070-blank-svc")>]
let ``update with blank name returns Error mentioning blank`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let acctId = InsertHelpers.insertAccount txn (prefix + "BN1") "Has Name" assetTypeId true
    let cmd = { accountId = acctId; name = Some "   "; subType = None; externalRef = None }
    match AccountService.updateAccount txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for blank name, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("blank")),
            sprintf "Expected error mentioning 'blank': %A" errs)

// =====================================================================
// @FT-ACT-077 — Nonexistent account ID
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ACT-077-svc")>]
let ``update a nonexistent account ID returns Error mentioning does not exist`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let cmd = { accountId = 999999; name = Some "Ghost"; subType = None; externalRef = None }
    match AccountService.updateAccount txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent account, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("does not exist")),
            sprintf "Expected error mentioning 'does not exist': %A" errs)

// =======================================================================
// Account Update — CLI-level Tests
// =======================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ACT-070")>]
let ``CLI update with --name returns exit 0 and stdout shows before and after name`` () =
    let prefix = TestData.uniquePrefix()
    let code = prefix + "CLI1"
    let conn = DataSource.openConnection()
    let acctId =
        use txn = conn.BeginTransaction()
        let id = InsertHelpers.insertAccount txn code "Original Name" assetTypeId true
        txn.Commit()
        id
    try
        let result = CliRunner.run (sprintf "account update %d --name \"Updated Name\"" acctId)
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Original Name", stdout)
        Assert.Contains("Updated Name", stdout)
    finally
        use cmd = new NpgsqlCommand("DELETE FROM ledger.account WHERE id = @id", conn)
        cmd.Parameters.AddWithValue("@id", acctId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        conn.Dispose()

[<Fact>]
[<Trait("GherkinId", "FT-ACT-074")>]
let ``CLI update with --json returns exit 0 and valid JSON with before and after keys`` () =
    let prefix = TestData.uniquePrefix()
    let code = prefix + "CLI2"
    let conn = DataSource.openConnection()
    let acctId =
        use txn = conn.BeginTransaction()
        let id = InsertHelpers.insertAccount txn code "JSON Target" assetTypeId true
        txn.Commit()
        id
    try
        let result = CliRunner.run (sprintf "account update %d --name \"JSON Updated\" --json" acctId)
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.True(doc.RootElement.ValueKind = JsonValueKind.Object, "Expected JSON object")
        let mutable beforeEl = Unchecked.defaultof<JsonElement>
        let mutable afterEl  = Unchecked.defaultof<JsonElement>
        Assert.True(doc.RootElement.TryGetProperty("before", &beforeEl), "Expected 'before' key in JSON")
        Assert.True(doc.RootElement.TryGetProperty("after", &afterEl), "Expected 'after' key in JSON")
    finally
        use cmd = new NpgsqlCommand("DELETE FROM ledger.account WHERE id = @id", conn)
        cmd.Parameters.AddWithValue("@id", acctId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        conn.Dispose()

[<Fact>]
[<Trait("GherkinId", "FT-ACT-075")>]
let ``CLI update with no mutable flags returns exit 1 and stderr error`` () =
    let prefix = TestData.uniquePrefix()
    let code = prefix + "CLI3"
    let conn = DataSource.openConnection()
    let acctId =
        use txn = conn.BeginTransaction()
        let id = InsertHelpers.insertAccount txn code "No Op Target" assetTypeId true
        txn.Commit()
        id
    try
        let result = CliRunner.run (sprintf "account update %d" acctId)
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")
    finally
        use cmd = new NpgsqlCommand("DELETE FROM ledger.account WHERE id = @id", conn)
        cmd.Parameters.AddWithValue("@id", acctId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        conn.Dispose()

[<Fact>]
[<Trait("GherkinId", "FT-ACT-076")>]
let ``CLI update with invalid subtype returns exit 1 and stderr error`` () =
    let prefix = TestData.uniquePrefix()
    let code = prefix + "CLI4"
    let conn = DataSource.openConnection()
    let acctId =
        use txn = conn.BeginTransaction()
        let id = InsertHelpers.insertAccount txn code "Bad Subtype" equityTypeId true
        txn.Commit()
        id
    try
        // Cash is not valid for equity (type 3)
        let result = CliRunner.run (sprintf "account update %d --subtype Cash" acctId)
        Assert.Equal(1, result.ExitCode)
        Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")
    finally
        use cmd = new NpgsqlCommand("DELETE FROM ledger.account WHERE id = @id", conn)
        cmd.Parameters.AddWithValue("@id", acctId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        conn.Dispose()

[<Fact>]
[<Trait("GherkinId", "FT-ACT-077")>]
let ``CLI update nonexistent account returns exit 1 and stderr error`` () =
    let result = CliRunner.run "account update 999999 --name Ghost"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-ACT-078")>]
let ``CLI update with no account ID returns exit 2`` () =
    let result = CliRunner.run "account update"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected usage on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-ACT-079")>]
let ``account update --help does not expose a --code flag`` () =
    let result = CliRunner.run "account update --help"
    Assert.Equal(0, result.ExitCode)
    Assert.DoesNotContain("--code", result.Stdout)

[<Fact>]
[<Trait("GherkinId", "FT-ACT-080")>]
let ``account update --help does not expose a --type flag`` () =
    let result = CliRunner.run "account update --help"
    Assert.Equal(0, result.ExitCode)
    Assert.DoesNotContain("--type", result.Stdout)
