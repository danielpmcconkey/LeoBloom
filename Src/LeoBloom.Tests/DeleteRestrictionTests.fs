module LeoBloom.Tests.DeleteRestrictionTests

open Npgsql
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.PortfolioTestHelpers

// =====================================================================
// account_type → account
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-001")>]
let ``cannot delete account_type with dependent account`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn $"{prefix}_type" "debit"
    InsertHelpers.insertAccount txn $"{prefix}_ACCT" "Test" atId true |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.account_type WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", atId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

// =====================================================================
// account → child account (parent_id)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-002")>]
let ``cannot delete account with dependent child account via parent_id`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn $"{prefix}_type" "debit"
    let parentId = InsertHelpers.insertAccount txn $"{prefix}_P" "Parent" atId true
    // Insert child with parent_id FK
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.account (code, name, account_type_id, parent_id) VALUES (@c, 'Child', @at, @pi) RETURNING id", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@c", $"{prefix}_C") |> ignore
    cmd.Parameters.AddWithValue("@at", atId) |> ignore
    cmd.Parameters.AddWithValue("@pi", parentId) |> ignore
    let _childId = cmd.ExecuteScalar() :?> int
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.account WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", parentId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

// =====================================================================
// account → journal_entry_line
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-003")>]
let ``cannot delete account with dependent journal_entry_line`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn $"{prefix}_type" "debit"
    let acctId = InsertHelpers.insertAccount txn $"{prefix}_ACCT" "Test" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test JE" fpId
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, 'debit')", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.account WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", acctId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

// =====================================================================
// account → obligation_agreement (source / dest)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-004")>]
let ``cannot delete account with dependent obligation_agreement source`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn $"{prefix}_type" "debit"
    let acctId = InsertHelpers.insertAccount txn $"{prefix}_ACCT" "Test" atId true
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, source_account_id) VALUES (@n, 'receivable', 'monthly', @acct) RETURNING id", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@n", $"{prefix}_oa") |> ignore
    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
    cmd.ExecuteScalar() :?> int |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.account WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", acctId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-005")>]
let ``cannot delete account with dependent obligation_agreement dest`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn $"{prefix}_type" "debit"
    let acctId = InsertHelpers.insertAccount txn $"{prefix}_ACCT" "Test" atId true
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, dest_account_id) VALUES (@n, 'receivable', 'monthly', @acct) RETURNING id", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@n", $"{prefix}_oa") |> ignore
    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
    cmd.ExecuteScalar() :?> int |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.account WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", acctId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

// =====================================================================
// account → transfer (from / to)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-006")>]
let ``cannot delete account with dependent transfer from`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn $"{prefix}_type" "debit"
    let fromId = InsertHelpers.insertAccount txn $"{prefix}_F" "From" atId true
    let toId = InsertHelpers.insertAccount txn $"{prefix}_T" "To" atId true
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01')", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
    cmd.Parameters.AddWithValue("@to_", toId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.account WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", fromId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-007")>]
let ``cannot delete account with dependent transfer to`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn $"{prefix}_type" "debit"
    let fromId = InsertHelpers.insertAccount txn $"{prefix}_F" "From" atId true
    let toId = InsertHelpers.insertAccount txn $"{prefix}_T" "To" atId true
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01')", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
    cmd.Parameters.AddWithValue("@to_", toId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.account WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", toId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

// =====================================================================
// fiscal_period → journal_entry / invoice
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-008")>]
let ``cannot delete fiscal_period with dependent journal_entry`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test" fpId |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.fiscal_period WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", fpId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-009")>]
let ``cannot delete fiscal_period with dependent invoice`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', @fp, 1000.00, 200.00, 1200.00)", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.fiscal_period WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", fpId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

// =====================================================================
// journal_entry → reference / line / obligation_instance / transfer
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-010")>]
let ``cannot delete journal_entry with dependent reference`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test" fpId
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) VALUES (@je, 'invoice', 'INV-001')", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.journal_entry WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", jeId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-011")>]
let ``cannot delete journal_entry with dependent line`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn $"{prefix}_type" "debit"
    let acctId = InsertHelpers.insertAccount txn $"{prefix}_ACCT" "Test" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test" fpId
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, 'debit')", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.journal_entry WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", jeId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-012")>]
let ``cannot delete journal_entry with dependent obligation_instance`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test" fpId
    let oaId = InsertHelpers.insertObligationAgreement txn $"{prefix}_oa"
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date, journal_entry_id) VALUES (@oa, 'Test', 'expected', '2026-04-01', @je)", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.journal_entry WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", jeId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-013")>]
let ``cannot delete journal_entry with dependent transfer`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn $"{prefix}_type" "debit"
    let fromId = InsertHelpers.insertAccount txn $"{prefix}_F" "From" atId true
    let toId = InsertHelpers.insertAccount txn $"{prefix}_T" "To" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn $"{prefix}_fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test" fpId
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date, journal_entry_id) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01', @je)", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
    cmd.Parameters.AddWithValue("@to_", toId) |> ignore
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ledger.journal_entry WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", jeId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

// =====================================================================
// obligation_agreement → obligation_instance
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-018")>]
let ``cannot delete obligation_agreement with dependent instance`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let oaId = InsertHelpers.insertObligationAgreement txn $"{prefix}_oa"
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (@oa, 'Test', 'expected', '2026-04-01')", txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM ops.obligation_agreement WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", oaId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

// =====================================================================
// portfolio schema
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DR-019")>]
let ``cannot delete tax_bucket with dependent investment_account`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId  = PortfolioInsertHelpers.insertTaxBucket txn $"{prefix}_tb"
    let agId  = PortfolioInsertHelpers.insertAccountGroup txn $"{prefix}_ag"
    PortfolioInsertHelpers.insertInvestmentAccount txn $"{prefix}_ia" tbId agId |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM portfolio.tax_bucket WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", tbId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-020")>]
let ``cannot delete account_group with dependent investment_account`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId  = PortfolioInsertHelpers.insertTaxBucket txn $"{prefix}_tb"
    let agId  = PortfolioInsertHelpers.insertAccountGroup txn $"{prefix}_ag"
    PortfolioInsertHelpers.insertInvestmentAccount txn $"{prefix}_ia" tbId agId |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM portfolio.account_group WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", agId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-021")>]
let ``cannot delete investment_account with dependent position`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId   = PortfolioInsertHelpers.insertTaxBucket txn $"{prefix}_tb"
    let agId   = PortfolioInsertHelpers.insertAccountGroup txn $"{prefix}_ag"
    let iaId   = PortfolioInsertHelpers.insertInvestmentAccount txn $"{prefix}_ia" tbId agId
    let sym    = PortfolioInsertHelpers.insertFund txn $"{prefix}F" $"{prefix} Fund"
    PortfolioInsertHelpers.insertPosition txn iaId sym (System.DateOnly(2026, 1, 1)) 100m 10m 1000m 900m |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM portfolio.investment_account WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", iaId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-022")>]
let ``cannot delete fund with dependent position`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId   = PortfolioInsertHelpers.insertTaxBucket txn $"{prefix}_tb"
    let agId   = PortfolioInsertHelpers.insertAccountGroup txn $"{prefix}_ag"
    let iaId   = PortfolioInsertHelpers.insertInvestmentAccount txn $"{prefix}_ia" tbId agId
    let sym    = PortfolioInsertHelpers.insertFund txn $"{prefix}F" $"{prefix} Fund"
    PortfolioInsertHelpers.insertPosition txn iaId sym (System.DateOnly(2026, 1, 1)) 100m 10m 1000m 900m |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM portfolio.fund WHERE symbol = @s"
                (fun cmd -> cmd.Parameters.AddWithValue("@s", sym) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-023")>]
let ``cannot delete dim_investment_type with dependent fund`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    use dimCmd = new NpgsqlCommand(
        "INSERT INTO portfolio.dim_investment_type (name) VALUES (@n) RETURNING id", txn.Connection)
    dimCmd.Transaction <- txn
    dimCmd.Parameters.AddWithValue("@n", $"{prefix}_it") |> ignore
    let dimId = dimCmd.ExecuteScalar() :?> int
    use fundCmd = new NpgsqlCommand(
        "INSERT INTO portfolio.fund (symbol, name, investment_type_id) VALUES (@s, @n, @d)", txn.Connection)
    fundCmd.Transaction <- txn
    fundCmd.Parameters.AddWithValue("@s", $"{prefix}F") |> ignore
    fundCmd.Parameters.AddWithValue("@n", $"{prefix} Fund") |> ignore
    fundCmd.Parameters.AddWithValue("@d", dimId) |> ignore
    fundCmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM portfolio.dim_investment_type WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", dimId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-024")>]
let ``cannot delete dim_market_cap with dependent fund`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    use dimCmd = new NpgsqlCommand(
        "INSERT INTO portfolio.dim_market_cap (name) VALUES (@n) RETURNING id", txn.Connection)
    dimCmd.Transaction <- txn
    dimCmd.Parameters.AddWithValue("@n", $"{prefix}_mc") |> ignore
    let dimId = dimCmd.ExecuteScalar() :?> int
    use fundCmd = new NpgsqlCommand(
        "INSERT INTO portfolio.fund (symbol, name, market_cap_id) VALUES (@s, @n, @d)", txn.Connection)
    fundCmd.Transaction <- txn
    fundCmd.Parameters.AddWithValue("@s", $"{prefix}F") |> ignore
    fundCmd.Parameters.AddWithValue("@n", $"{prefix} Fund") |> ignore
    fundCmd.Parameters.AddWithValue("@d", dimId) |> ignore
    fundCmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM portfolio.dim_market_cap WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", dimId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-025")>]
let ``cannot delete dim_index_type with dependent fund`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    use dimCmd = new NpgsqlCommand(
        "INSERT INTO portfolio.dim_index_type (name) VALUES (@n) RETURNING id", txn.Connection)
    dimCmd.Transaction <- txn
    dimCmd.Parameters.AddWithValue("@n", $"{prefix}_idx") |> ignore
    let dimId = dimCmd.ExecuteScalar() :?> int
    use fundCmd = new NpgsqlCommand(
        "INSERT INTO portfolio.fund (symbol, name, index_type_id) VALUES (@s, @n, @d)", txn.Connection)
    fundCmd.Transaction <- txn
    fundCmd.Parameters.AddWithValue("@s", $"{prefix}F") |> ignore
    fundCmd.Parameters.AddWithValue("@n", $"{prefix} Fund") |> ignore
    fundCmd.Parameters.AddWithValue("@d", dimId) |> ignore
    fundCmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM portfolio.dim_index_type WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", dimId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-026")>]
let ``cannot delete dim_sector with dependent fund`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    use dimCmd = new NpgsqlCommand(
        "INSERT INTO portfolio.dim_sector (name) VALUES (@n) RETURNING id", txn.Connection)
    dimCmd.Transaction <- txn
    dimCmd.Parameters.AddWithValue("@n", $"{prefix}_sec") |> ignore
    let dimId = dimCmd.ExecuteScalar() :?> int
    use fundCmd = new NpgsqlCommand(
        "INSERT INTO portfolio.fund (symbol, name, sector_id) VALUES (@s, @n, @d)", txn.Connection)
    fundCmd.Transaction <- txn
    fundCmd.Parameters.AddWithValue("@s", $"{prefix}F") |> ignore
    fundCmd.Parameters.AddWithValue("@n", $"{prefix} Fund") |> ignore
    fundCmd.Parameters.AddWithValue("@d", dimId) |> ignore
    fundCmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM portfolio.dim_sector WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", dimId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-027")>]
let ``cannot delete dim_region with dependent fund`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    use dimCmd = new NpgsqlCommand(
        "INSERT INTO portfolio.dim_region (name) VALUES (@n) RETURNING id", txn.Connection)
    dimCmd.Transaction <- txn
    dimCmd.Parameters.AddWithValue("@n", $"{prefix}_reg") |> ignore
    let dimId = dimCmd.ExecuteScalar() :?> int
    use fundCmd = new NpgsqlCommand(
        "INSERT INTO portfolio.fund (symbol, name, region_id) VALUES (@s, @n, @d)", txn.Connection)
    fundCmd.Transaction <- txn
    fundCmd.Parameters.AddWithValue("@s", $"{prefix}F") |> ignore
    fundCmd.Parameters.AddWithValue("@n", $"{prefix} Fund") |> ignore
    fundCmd.Parameters.AddWithValue("@d", dimId) |> ignore
    fundCmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM portfolio.dim_region WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", dimId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-DR-028")>]
let ``cannot delete dim_objective with dependent fund`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    use dimCmd = new NpgsqlCommand(
        "INSERT INTO portfolio.dim_objective (name) VALUES (@n) RETURNING id", txn.Connection)
    dimCmd.Transaction <- txn
    dimCmd.Parameters.AddWithValue("@n", $"{prefix}_obj") |> ignore
    let dimId = dimCmd.ExecuteScalar() :?> int
    use fundCmd = new NpgsqlCommand(
        "INSERT INTO portfolio.fund (symbol, name, objective_id) VALUES (@s, @n, @d)", txn.Connection)
    fundCmd.Transaction <- txn
    fundCmd.Parameters.AddWithValue("@s", $"{prefix}F") |> ignore
    fundCmd.Parameters.AddWithValue("@n", $"{prefix} Fund") |> ignore
    fundCmd.Parameters.AddWithValue("@d", dimId) |> ignore
    fundCmd.ExecuteNonQuery() |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "DELETE FROM portfolio.dim_objective WHERE id = @id"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", dimId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"
