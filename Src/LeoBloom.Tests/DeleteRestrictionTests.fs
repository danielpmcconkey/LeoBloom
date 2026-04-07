module LeoBloom.Tests.DeleteRestrictionTests

open Npgsql
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

// =====================================================================
// account_type → account
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-001")>]
let ``cannot delete account_type with dependent account`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker $"{prefix}_type" "debit"
        InsertHelpers.insertAccount conn tracker $"{prefix}_ACCT" "Test" atId true |> ignore
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.account_type WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", atId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

// =====================================================================
// account → child account (parent_id)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-002")>]
let ``cannot delete account with dependent child account via parent_id`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker $"{prefix}_type" "debit"
        let parentId = InsertHelpers.insertAccount conn tracker $"{prefix}_P" "Parent" atId true
        // Insert child with parent_id FK
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.account (code, name, account_type_id, parent_id) VALUES (@c, 'Child', @at, @pi) RETURNING id", conn)
        cmd.Parameters.AddWithValue("@c", $"{prefix}_C") |> ignore
        cmd.Parameters.AddWithValue("@at", atId) |> ignore
        cmd.Parameters.AddWithValue("@pi", parentId) |> ignore
        let childId = cmd.ExecuteScalar() :?> int
        TestCleanup.trackAccount childId tracker
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.account WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", parentId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

// =====================================================================
// account → journal_entry_line
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-003")>]
let ``cannot delete account with dependent journal_entry_line`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker $"{prefix}_type" "debit"
        let acctId = InsertHelpers.insertAccount conn tracker $"{prefix}_ACCT" "Test" atId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
        let jeId = InsertHelpers.insertJournalEntry conn tracker (System.DateOnly(2026, 1, 1)) "Test JE" fpId
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, 'debit')", conn)
        cmd.Parameters.AddWithValue("@je", jeId) |> ignore
        cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.account WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", acctId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

// =====================================================================
// account → obligation_agreement (source / dest)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-004")>]
let ``cannot delete account with dependent obligation_agreement source`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker $"{prefix}_type" "debit"
        let acctId = InsertHelpers.insertAccount conn tracker $"{prefix}_ACCT" "Test" atId true
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, source_account_id) VALUES (@n, 'receivable', 'monthly', @acct) RETURNING id", conn)
        cmd.Parameters.AddWithValue("@n", $"{prefix}_oa") |> ignore
        cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
        let oaId = cmd.ExecuteScalar() :?> int
        TestCleanup.trackObligationAgreement oaId tracker
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.account WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", acctId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-DR-005")>]
let ``cannot delete account with dependent obligation_agreement dest`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker $"{prefix}_type" "debit"
        let acctId = InsertHelpers.insertAccount conn tracker $"{prefix}_ACCT" "Test" atId true
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, dest_account_id) VALUES (@n, 'receivable', 'monthly', @acct) RETURNING id", conn)
        cmd.Parameters.AddWithValue("@n", $"{prefix}_oa") |> ignore
        cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
        let oaId = cmd.ExecuteScalar() :?> int
        TestCleanup.trackObligationAgreement oaId tracker
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.account WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", acctId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

// =====================================================================
// account → transfer (from / to)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-006")>]
let ``cannot delete account with dependent transfer from`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker $"{prefix}_type" "debit"
        let fromId = InsertHelpers.insertAccount conn tracker $"{prefix}_F" "From" atId true
        let toId = InsertHelpers.insertAccount conn tracker $"{prefix}_T" "To" atId true
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01')", conn)
        cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
        cmd.Parameters.AddWithValue("@to_", toId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.account WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", fromId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-DR-007")>]
let ``cannot delete account with dependent transfer to`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker $"{prefix}_type" "debit"
        let fromId = InsertHelpers.insertAccount conn tracker $"{prefix}_F" "From" atId true
        let toId = InsertHelpers.insertAccount conn tracker $"{prefix}_T" "To" atId true
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01')", conn)
        cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
        cmd.Parameters.AddWithValue("@to_", toId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.account WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", toId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

// =====================================================================
// fiscal_period → journal_entry / invoice
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-008")>]
let ``cannot delete fiscal_period with dependent journal_entry`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
        InsertHelpers.insertJournalEntry conn tracker (System.DateOnly(2026, 1, 1)) "Test" fpId |> ignore
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.fiscal_period WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", fpId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-DR-009")>]
let ``cannot delete fiscal_period with dependent invoice`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', @fp, 1000.00, 200.00, 1200.00)", conn)
        cmd.Parameters.AddWithValue("@fp", fpId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.fiscal_period WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", fpId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

// =====================================================================
// journal_entry → reference / line / obligation_instance / transfer
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-010")>]
let ``cannot delete journal_entry with dependent reference`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
        let jeId = InsertHelpers.insertJournalEntry conn tracker (System.DateOnly(2026, 1, 1)) "Test" fpId
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) VALUES (@je, 'invoice', 'INV-001')", conn)
        cmd.Parameters.AddWithValue("@je", jeId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.journal_entry WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", jeId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-DR-011")>]
let ``cannot delete journal_entry with dependent line`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker $"{prefix}_type" "debit"
        let acctId = InsertHelpers.insertAccount conn tracker $"{prefix}_ACCT" "Test" atId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
        let jeId = InsertHelpers.insertJournalEntry conn tracker (System.DateOnly(2026, 1, 1)) "Test" fpId
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, 'debit')", conn)
        cmd.Parameters.AddWithValue("@je", jeId) |> ignore
        cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.journal_entry WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", jeId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-DR-012")>]
let ``cannot delete journal_entry with dependent obligation_instance`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
        let jeId = InsertHelpers.insertJournalEntry conn tracker (System.DateOnly(2026, 1, 1)) "Test" fpId
        let oaId = InsertHelpers.insertObligationAgreement conn tracker $"{prefix}_oa"
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date, journal_entry_id) VALUES (@oa, 'Test', 'expected', '2026-04-01', @je)", conn)
        cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
        cmd.Parameters.AddWithValue("@je", jeId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.journal_entry WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", jeId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-DR-013")>]
let ``cannot delete journal_entry with dependent transfer`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker $"{prefix}_type" "debit"
        let fromId = InsertHelpers.insertAccount conn tracker $"{prefix}_F" "From" atId true
        let toId = InsertHelpers.insertAccount conn tracker $"{prefix}_T" "To" atId true
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
        let jeId = InsertHelpers.insertJournalEntry conn tracker (System.DateOnly(2026, 1, 1)) "Test" fpId
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date, journal_entry_id) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01', @je)", conn)
        cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
        cmd.Parameters.AddWithValue("@to_", toId) |> ignore
        cmd.Parameters.AddWithValue("@je", jeId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ledger.journal_entry WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", jeId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker

// =====================================================================
// obligation_agreement → obligation_instance
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-018")>]
let ``cannot delete obligation_agreement with dependent instance`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let oaId = InsertHelpers.insertObligationAgreement conn tracker $"{prefix}_oa"
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (@oa, 'Test', 'expected', '2026-04-01')", conn)
        cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        let ex = ConstraintAssert.tryExec conn
                    "DELETE FROM ops.obligation_agreement WHERE id = @id"
                    (fun cmd -> cmd.Parameters.AddWithValue("@id", oaId) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally TestCleanup.deleteAll tracker
