module LeoBloom.Tests.OpsConstraintTests

open Npgsql
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

// =====================================================================
// obligation_agreement
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OSC-009")>]
let ``obligation_agreement requires a name`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let ex = ConstraintAssert.tryInsert conn "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence) VALUES (NULL, 'receivable', 'monthly')"
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-015")>]
let ``obligation_agreement source_account_id must reference a valid ledger account`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, source_account_id) VALUES ('Test', 'receivable', 'monthly', @sa)"
                    (fun cmd -> cmd.Parameters.AddWithValue("@sa", 9999) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-016")>]
let ``obligation_agreement dest_account_id must reference a valid ledger account`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, dest_account_id) VALUES ('Test', 'receivable', 'monthly', @da)"
                    (fun cmd -> cmd.Parameters.AddWithValue("@da", 9999) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-017")>]
let ``obligation_agreement amount is nullable`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let oaId = InsertHelpers.insertObligationAgreement conn tracker "osc017_agreement"
        Assert.True(oaId > 0)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// obligation_instance
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OSC-018")>]
let ``obligation_instance requires an obligation_agreement_id`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let ex = ConstraintAssert.tryInsert conn "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (NULL, 'Test', 'expected', '2026-04-01')"
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-019")>]
let ``obligation_instance obligation_agreement_id must reference a valid agreement`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (@oa, 'Test', 'expected', '2026-04-01')"
                    (fun cmd -> cmd.Parameters.AddWithValue("@oa", 9999) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-020")>]
let ``obligation_instance requires a name`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let oaId = InsertHelpers.insertObligationAgreement conn tracker "osc020_agreement"
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (@oa, NULL, 'expected', '2026-04-01')"
                    (fun cmd -> cmd.Parameters.AddWithValue("@oa", oaId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-023")>]
let ``obligation_instance requires an expected_date`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let oaId = InsertHelpers.insertObligationAgreement conn tracker "osc023_agreement"
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (@oa, 'Test', 'expected', NULL)"
                    (fun cmd -> cmd.Parameters.AddWithValue("@oa", oaId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-024")>]
let ``obligation_instance journal_entry_id must reference a valid journal_entry`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let oaId = InsertHelpers.insertObligationAgreement conn tracker "osc024_agreement"
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date, journal_entry_id) VALUES (@oa, 'Test', 'expected', '2026-04-01', @je)"
                    (fun cmd ->
                        cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
                        cmd.Parameters.AddWithValue("@je", 9999) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-025")>]
let ``obligation_instance journal_entry_id is nullable`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let oaId = InsertHelpers.insertObligationAgreement conn tracker "osc025_agreement"
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date, journal_entry_id) VALUES (@oa, 'Test', 'expected', '2026-04-01', NULL)", conn)
        cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
    finally TestCleanup.deleteAll tracker

// =====================================================================
// transfer
// =====================================================================

let private insertTwoAccounts (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (prefix: string) =
    let atId = InsertHelpers.insertAccountType conn tracker $"{prefix}_type" "debit"
    let fromId = InsertHelpers.insertAccount conn tracker $"{prefix}_F" "From" atId true
    let toId = InsertHelpers.insertAccount conn tracker $"{prefix}_T" "To" atId true
    (fromId, toId)

[<Fact>]
[<Trait("GherkinId", "FT-OSC-026")>]
let ``transfer requires a from_account_id`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let (_, toId) = insertTwoAccounts conn tracker "osc026"
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (NULL, @to_, 100.00, 'initiated', '2026-04-01')"
                    (fun cmd -> cmd.Parameters.AddWithValue("@to_", toId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-027")>]
let ``transfer from_account_id must reference a valid ledger account`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let (_, toId) = insertTwoAccounts conn tracker "osc027"
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01')"
                    (fun cmd ->
                        cmd.Parameters.AddWithValue("@from_", 9999) |> ignore
                        cmd.Parameters.AddWithValue("@to_", toId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-028")>]
let ``transfer requires a to_account_id`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let (fromId, _) = insertTwoAccounts conn tracker "osc028"
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, NULL, 100.00, 'initiated', '2026-04-01')"
                    (fun cmd -> cmd.Parameters.AddWithValue("@from_", fromId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-029")>]
let ``transfer to_account_id must reference a valid ledger account`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let (fromId, _) = insertTwoAccounts conn tracker "osc029"
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01')"
                    (fun cmd ->
                        cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
                        cmd.Parameters.AddWithValue("@to_", 9999) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-030")>]
let ``transfer requires an amount`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let (fromId, toId) = insertTwoAccounts conn tracker "osc030"
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, NULL, 'initiated', '2026-04-01')"
                    (fun cmd ->
                        cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
                        cmd.Parameters.AddWithValue("@to_", toId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-031")>]
let ``transfer requires a status`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let (fromId, toId) = insertTwoAccounts conn tracker "osc031"
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, NULL, '2026-04-01')"
                    (fun cmd ->
                        cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
                        cmd.Parameters.AddWithValue("@to_", toId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-032")>]
let ``transfer requires an initiated_date`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let (fromId, toId) = insertTwoAccounts conn tracker "osc032"
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, 'initiated', NULL)"
                    (fun cmd ->
                        cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
                        cmd.Parameters.AddWithValue("@to_", toId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-033")>]
let ``transfer journal_entry_id must reference a valid journal_entry`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let (fromId, toId) = insertTwoAccounts conn tracker "osc033"
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date, journal_entry_id) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01', @je)"
                    (fun cmd ->
                        cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
                        cmd.Parameters.AddWithValue("@to_", toId) |> ignore
                        cmd.Parameters.AddWithValue("@je", 9999) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-034")>]
let ``transfer journal_entry_id is nullable`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (fromId, toId) = insertTwoAccounts conn tracker prefix
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date, journal_entry_id) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01', NULL)", conn)
        cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
        cmd.Parameters.AddWithValue("@to_", toId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
    finally TestCleanup.deleteAll tracker

// =====================================================================
// invoice
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OSC-035")>]
let ``invoice requires a tenant`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker "o035fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES (NULL, @fp, 1000.00, 200.00, 1200.00)"
                    (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-036")>]
let ``invoice requires a fiscal_period_id`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let ex = ConstraintAssert.tryInsert conn "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', NULL, 1000.00, 200.00, 1200.00)"
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-037")>]
let ``invoice fiscal_period_id must reference a valid fiscal_period`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', @fp, 1000.00, 200.00, 1200.00)"
                    (fun cmd -> cmd.Parameters.AddWithValue("@fp", 9999) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-038")>]
let ``invoice requires rent_amount`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker "o038fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', @fp, NULL, 200.00, 1200.00)"
                    (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-039")>]
let ``invoice requires utility_share`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker "o039fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', @fp, 1000.00, NULL, 1200.00)"
                    (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-040")>]
let ``invoice requires total_amount`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker "o040fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', @fp, 1000.00, 200.00, NULL)"
                    (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-041")>]
let ``invoice tenant and fiscal_period_id must be unique together`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (TestData.periodKey prefix) (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
        let tenant = $"{prefix}_tenant"
        // First insert
        use cmd1 = new NpgsqlCommand(
            "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES (@t, @fp, 1000.00, 200.00, 1200.00)",
            conn)
        cmd1.Parameters.AddWithValue("@t", tenant) |> ignore
        cmd1.Parameters.AddWithValue("@fp", fpId) |> ignore
        cmd1.ExecuteNonQuery() |> ignore
        // Second insert — should fail UNIQUE
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES (@t, @fp, 500.00, 100.00, 600.00)"
                    (fun cmd ->
                        cmd.Parameters.AddWithValue("@t", tenant) |> ignore
                        cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
        ConstraintAssert.assertUnique ex "Expected UNIQUE violation"
    finally TestCleanup.deleteAll tracker

// =====================================================================
// obligation_agreement NOT NULL for varchar columns
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OSC-042")>]
let ``obligation_agreement requires an obligation_type`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let ex = ConstraintAssert.tryInsert conn "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence) VALUES ('Test', NULL, 'monthly')"
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-043")>]
let ``obligation_agreement requires a cadence`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let ex = ConstraintAssert.tryInsert conn "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence) VALUES ('Test', 'receivable', NULL)"
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OSC-044")>]
let ``obligation_instance requires a status`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let oaId = InsertHelpers.insertObligationAgreement conn tracker "osc044_agreement"
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (@oa, 'Test', NULL, '2026-04-01')"
                    (fun cmd -> cmd.Parameters.AddWithValue("@oa", oaId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally TestCleanup.deleteAll tracker
