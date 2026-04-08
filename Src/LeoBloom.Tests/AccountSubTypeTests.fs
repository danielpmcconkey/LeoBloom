module LeoBloom.Tests.AccountSubTypeTests

open System
open Npgsql
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Helpers
// =====================================================================

/// Map a human-readable account type name to its seed account_type_id.
let private accountTypeNameToId (name: string) =
    match name.ToLowerInvariant() with
    | "asset" -> 1
    | "liability" -> 2
    | "equity" -> 3
    | "revenue" -> 4
    | "expense" -> 5
    | _ -> failwithf "Unknown account type name: %s" name

/// Parse a subtype name string into an AccountSubType DU value.
let private parseSubType (s: string) : AccountSubType =
    match AccountSubType.fromDbString s with
    | Ok v -> v
    | Error msg -> failwithf "Bad subtype in test data: %s" msg

/// Read account_subtype from DB by account code within a transaction.
let private readSubTypeByCodeTxn (txn: NpgsqlTransaction) (code: string) : string option =
    use cmd = new NpgsqlCommand(
        "SELECT account_subtype FROM ledger.account WHERE code = @c",
        txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@c", code) |> ignore
    let result = cmd.ExecuteScalar()
    if result = null || result = (box DBNull.Value) then None
    else Some (result :?> string)

/// Read account_subtype from DB by account code using a bare connection (for seed-data reads).
let private readSubTypeByCode (conn: NpgsqlConnection) (code: string) : string option =
    use cmd = new NpgsqlCommand(
        "SELECT account_subtype FROM ledger.account WHERE code = @c",
        conn)
    cmd.Parameters.AddWithValue("@c", code) |> ignore
    let result = cmd.ExecuteScalar()
    if result = null || result = (box DBNull.Value) then None
    else Some (result :?> string)

/// Update account_subtype by account code within a transaction.
let private updateSubTypeTxn (txn: NpgsqlTransaction) (code: string) (subType: string option) =
    use cmd = new NpgsqlCommand(
        "UPDATE ledger.account SET account_subtype = @st WHERE code = @c",
        txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@c", code) |> ignore
    match subType with
    | Some s -> cmd.Parameters.AddWithValue("@st", s) |> ignore
    | None -> cmd.Parameters.AddWithValue("@st", DBNull.Value) |> ignore
    cmd.ExecuteNonQuery() |> ignore

// =====================================================================
// FT-AST-001: Valid subtype for account type is accepted
// =====================================================================

[<Theory>]
[<Trait("GherkinId", "@FT-AST-001")>]
[<InlineData("Asset", "Cash")>]
[<InlineData("Asset", "FixedAsset")>]
[<InlineData("Asset", "Investment")>]
[<InlineData("Liability", "CurrentLiability")>]
[<InlineData("Liability", "LongTermLiability")>]
[<InlineData("Revenue", "OperatingRevenue")>]
[<InlineData("Revenue", "OtherRevenue")>]
[<InlineData("Expense", "OperatingExpense")>]
[<InlineData("Expense", "OtherExpense")>]
let ``Valid subtype for account type is accepted`` (accountType: string) (subtype: string) =
    let st = parseSubType subtype
    Assert.True(AccountSubType.isValidSubType accountType (Some st),
                sprintf "%s should be valid for %s" subtype accountType)

// =====================================================================
// FT-AST-002: Invalid subtype for account type is rejected
// =====================================================================

[<Theory>]
[<Trait("GherkinId", "@FT-AST-002")>]
[<InlineData("Revenue", "Cash")>]
[<InlineData("Expense", "Cash")>]
[<InlineData("Liability", "Cash")>]
[<InlineData("Asset", "CurrentLiability")>]
[<InlineData("Asset", "OperatingExpense")>]
[<InlineData("Revenue", "LongTermLiability")>]
[<InlineData("Expense", "Investment")>]
[<InlineData("Liability", "OperatingRevenue")>]
let ``Invalid subtype for account type is rejected`` (accountType: string) (subtype: string) =
    let st = parseSubType subtype
    Assert.False(AccountSubType.isValidSubType accountType (Some st),
                 sprintf "%s should be invalid for %s" subtype accountType)

// =====================================================================
// FT-AST-003: Equity accepts no subtypes
// =====================================================================

[<Theory>]
[<Trait("GherkinId", "@FT-AST-003")>]
[<InlineData("Cash")>]
[<InlineData("FixedAsset")>]
[<InlineData("Investment")>]
[<InlineData("CurrentLiability")>]
[<InlineData("LongTermLiability")>]
[<InlineData("OperatingRevenue")>]
[<InlineData("OtherRevenue")>]
[<InlineData("OperatingExpense")>]
[<InlineData("OtherExpense")>]
let ``Equity accepts no subtypes`` (subtype: string) =
    let st = parseSubType subtype
    Assert.False(AccountSubType.isValidSubType "Equity" (Some st),
                 sprintf "%s should be invalid for Equity" subtype)

// =====================================================================
// FT-AST-004: Null subtype is valid for any account type
// =====================================================================

[<Theory>]
[<Trait("GherkinId", "@FT-AST-004")>]
[<InlineData("Asset")>]
[<InlineData("Liability")>]
[<InlineData("Equity")>]
[<InlineData("Revenue")>]
[<InlineData("Expense")>]
let ``Null subtype is valid for any account type`` (accountType: string) =
    Assert.True(AccountSubType.isValidSubType accountType None,
                sprintf "None should be valid for %s" accountType)

// =====================================================================
// FT-AST-005: toDbString and fromDbString round-trip all subtypes
// =====================================================================

[<Theory>]
[<Trait("GherkinId", "@FT-AST-005")>]
[<InlineData("Cash")>]
[<InlineData("FixedAsset")>]
[<InlineData("Investment")>]
[<InlineData("CurrentLiability")>]
[<InlineData("LongTermLiability")>]
[<InlineData("OperatingRevenue")>]
[<InlineData("OtherRevenue")>]
[<InlineData("OperatingExpense")>]
[<InlineData("OtherExpense")>]
let ``toDbString and fromDbString round-trip all subtypes`` (subtype: string) =
    let original = parseSubType subtype
    let dbStr = AccountSubType.toDbString original
    match AccountSubType.fromDbString dbStr with
    | Ok roundTripped -> Assert.Equal(original, roundTripped)
    | Error msg -> Assert.Fail(sprintf "Round-trip failed: %s" msg)

// =====================================================================
// FT-AST-006: fromDbString rejects an unrecognized string
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-AST-006")>]
let ``fromDbString rejects an unrecognized string`` () =
    match AccountSubType.fromDbString "Bogus" with
    | Error _ -> ()
    | Ok v -> Assert.Fail(sprintf "Expected Error but got Ok %A" v)

// =====================================================================
// FT-AST-007: Account with subtype persists and reads back correctly
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-AST-007")>]
let ``Account with subtype persists and reads back correctly`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (TestData.accountTypeName prefix) "debit"
    let code = sprintf "%s1110" prefix
    InsertHelpers.insertAccountWithSubType txn code (sprintf "%s_checking" prefix) atId true (Some "Cash") |> ignore
    let result = readSubTypeByCodeTxn txn code
    Assert.Equal(Some "Cash", result)

// =====================================================================
// FT-AST-008: Account with null subtype persists and reads back correctly
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-AST-008")>]
let ``Account with null subtype persists and reads back correctly`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (TestData.accountTypeName prefix) "debit"
    let code = sprintf "%s1000" prefix
    InsertHelpers.insertAccountWithSubType txn code (sprintf "%s_header" prefix) atId true None |> ignore
    let result = readSubTypeByCodeTxn txn code
    Assert.Equal(None, result)

// =====================================================================
// FT-AST-009: Subtypes round-trip through write and read paths
// =====================================================================

[<Theory>]
[<Trait("GherkinId", "@FT-AST-009")>]
[<InlineData("1110", "asset", "Cash")>]
[<InlineData("1120", "asset", "FixedAsset")>]
[<InlineData("1210", "asset", "Investment")>]
[<InlineData("2110", "liability", "CurrentLiability")>]
[<InlineData("2210", "liability", "LongTermLiability")>]
[<InlineData("4110", "revenue", "OperatingRevenue")>]
[<InlineData("4210", "revenue", "OtherRevenue")>]
[<InlineData("5110", "expense", "OperatingExpense")>]
[<InlineData("5210", "expense", "OtherExpense")>]
let ``Subtypes round-trip through write and read paths`` (codeSuffix: string) (accountType: string) (subtype: string) =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = accountTypeNameToId accountType
    // Use a seed account_type_id directly — they exist in the DB from seed migrations.
    // Create a unique account with the given subtype.
    let code = sprintf "%s%s" prefix codeSuffix
    InsertHelpers.insertAccountWithSubType txn code (sprintf "%s_test" prefix) atId true (Some subtype) |> ignore
    let result = readSubTypeByCodeTxn txn code
    Assert.Equal(Some subtype, result)

// =====================================================================
// FT-AST-010: Invalid subtype for account type is rejected by domain validation
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-AST-010")>]
let ``Invalid subtype for account type is rejected by domain validation`` () =
    let subtype = parseSubType "OperatingExpense"
    Assert.False(AccountSubType.isValidSubType "Asset" (Some subtype),
                 "OperatingExpense should be invalid for Asset")

// =====================================================================
// FT-AST-011: Any subtype on equity is rejected by domain validation
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-AST-011")>]
let ``Any subtype on equity is rejected by domain validation`` () =
    let subtype = parseSubType "Cash"
    Assert.False(AccountSubType.isValidSubType "Equity" (Some subtype),
                 "Cash should be invalid for Equity")

// =====================================================================
// FT-AST-012: Invalid subtype change is rejected by domain validation
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-AST-012")>]
let ``Invalid subtype change is rejected by domain validation`` () =
    let subtype = parseSubType "OperatingRevenue"
    Assert.False(AccountSubType.isValidSubType "Asset" (Some subtype),
                 "OperatingRevenue should be invalid for Asset")

// =====================================================================
// FT-AST-013: Updating an account to a valid subtype succeeds
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-AST-013")>]
let ``Updating an account to a valid subtype succeeds`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (TestData.accountTypeName prefix) "debit"
    let code = sprintf "%s1110" prefix
    InsertHelpers.insertAccountWithSubType txn code (sprintf "%s_checking" prefix) atId true (Some "Cash") |> ignore

    // Validate that Investment is valid for Asset
    let isValid = AccountSubType.isValidSubType "Asset" (Some Investment)
    Assert.True(isValid, "Investment should be valid for Asset")

    // Perform the update
    updateSubTypeTxn txn code (Some "Investment")

    // Verify the update persisted
    let result = readSubTypeByCodeTxn txn code
    Assert.Equal(Some "Investment", result)

// =====================================================================
// FT-AST-014: Updating an account to null subtype succeeds
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-AST-014")>]
let ``Updating an account to null subtype succeeds`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (TestData.accountTypeName prefix) "debit"
    let code = sprintf "%s1110" prefix
    InsertHelpers.insertAccountWithSubType txn code (sprintf "%s_checking" prefix) atId true (Some "Cash") |> ignore

    // Validate that None is valid for Asset
    let isValid = AccountSubType.isValidSubType "Asset" None
    Assert.True(isValid, "None should be valid for Asset")

    // Perform the update
    updateSubTypeTxn txn code None

    // Verify the update persisted
    let result = readSubTypeByCodeTxn txn code
    Assert.Equal(None, result)

// =====================================================================
// FT-AST-015: Seed accounts have expected subtypes after migration
// =====================================================================

[<Theory>]
[<Trait("GherkinId", "@FT-AST-015")>]
[<InlineData("1110", "Cash")>]
[<InlineData("1120", "Cash")>]
[<InlineData("1210", "Investment")>]
[<InlineData("1220", "Cash")>]
[<InlineData("2110", "LongTermLiability")>]
[<InlineData("2210", "LongTermLiability")>]
[<InlineData("2220", "CurrentLiability")>]
[<InlineData("4110", "OperatingRevenue")>]
[<InlineData("4120", "OperatingRevenue")>]
[<InlineData("4210", "OperatingRevenue")>]
[<InlineData("4220", "OperatingRevenue")>]
[<InlineData("7110", "OtherRevenue")>]
[<InlineData("7210", "OtherExpense")>]
[<InlineData("7220", "OtherExpense")>]
let ``Seed accounts have expected subtypes after migration`` (code: string) (expectedSubtype: string) =
    use conn = DataSource.openConnection()
    let result = readSubTypeByCode conn code
    Assert.Equal(Some expectedSubtype, result)

// =====================================================================
// FT-AST-016: Header accounts have null subtype after migration
// =====================================================================

[<Theory>]
[<Trait("GherkinId", "@FT-AST-016")>]
[<InlineData("1000")>]
[<InlineData("1100")>]
[<InlineData("1200")>]
[<InlineData("2000")>]
[<InlineData("2100")>]
[<InlineData("2200")>]
[<InlineData("3000")>]
[<InlineData("4000")>]
[<InlineData("4100")>]
[<InlineData("4200")>]
[<InlineData("5000")>]
[<InlineData("5100")>]
[<InlineData("5300")>]
[<InlineData("7100")>]
[<InlineData("7200")>]
let ``Header accounts have null subtype after migration`` (code: string) =
    use conn = DataSource.openConnection()
    let result = readSubTypeByCode conn code
    Assert.Equal(None, result)

// =====================================================================
// FT-AST-017: Equity leaf accounts have null subtype after migration
// =====================================================================

[<Theory>]
[<Trait("GherkinId", "@FT-AST-017")>]
[<InlineData("3010")>]
[<InlineData("3020")>]
[<InlineData("3099")>]
let ``Equity leaf accounts have null subtype after migration`` (code: string) =
    use conn = DataSource.openConnection()
    let result = readSubTypeByCode conn code
    Assert.Equal(None, result)
