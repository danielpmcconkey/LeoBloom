module LeoBloom.Tests.LedgerConstraintTests

open Npgsql
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

// =====================================================================
// account_type
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LSC-001")>]
let ``account_type requires a name`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let ex = ConstraintAssert.tryInsertTxn txn "INSERT INTO ledger.account_type (name, normal_balance) VALUES (NULL, 'debit')"
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-002")>]
let ``account_type name must be unique`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let name = TestData.accountTypeName prefix
    InsertHelpers.insertAccountType txn name "debit" |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.account_type (name, normal_balance) VALUES (@n, 'debit')"
                (fun cmd -> cmd.Parameters.AddWithValue("@n", name) |> ignore)
    ConstraintAssert.assertUnique ex "Expected UNIQUE violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-003")>]
let ``account_type requires a normal_balance`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let ex = ConstraintAssert.tryInsertTxn txn "INSERT INTO ledger.account_type (name, normal_balance) VALUES ('test_type', NULL)"
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

// =====================================================================
// account
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LSC-004")>]
let ``account requires a code`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let atId = InsertHelpers.insertAccountType txn "lsc004_type" "debit"
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.account (code, name, account_type_id) VALUES (NULL, 'Test', @at)"
                (fun cmd -> cmd.Parameters.AddWithValue("@at", atId) |> ignore)
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-005")>]
let ``account code must be unique`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (TestData.accountTypeName prefix) "debit"
    let code = TestData.accountCode prefix
    InsertHelpers.insertAccount txn code "Test Account" atId true |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.account (code, name, account_type_id) VALUES (@c, 'Duplicate', @at)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@c", code) |> ignore
                    cmd.Parameters.AddWithValue("@at", atId) |> ignore)
    ConstraintAssert.assertUnique ex "Expected UNIQUE violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-006")>]
let ``account requires a name`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let atId = InsertHelpers.insertAccountType txn "lsc006_type" "debit"
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.account (code, name, account_type_id) VALUES ('ZZZZ', NULL, @at)"
                (fun cmd -> cmd.Parameters.AddWithValue("@at", atId) |> ignore)
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-007")>]
let ``account requires an account_type_id`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let ex = ConstraintAssert.tryInsertTxn txn "INSERT INTO ledger.account (code, name, account_type_id) VALUES ('ZZZZ', 'Test', NULL)"
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-008")>]
let ``account account_type_id must reference a valid account_type`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.account (code, name, account_type_id) VALUES ('ZZZZ', 'Test', @id)"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", 9999) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-009")>]
let ``account parent_id must reference a valid account id`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let atId = InsertHelpers.insertAccountType txn "lsc009_type" "debit"
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.account (code, name, account_type_id, parent_id) VALUES ('ZZZZ', 'Test', @at, @pi)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@at", atId) |> ignore
                    cmd.Parameters.AddWithValue("@pi", -999999) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-010")>]
let ``account parent_id is nullable`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (TestData.accountTypeName prefix) "debit"
    // InsertHelpers.insertAccount tracks the ID automatically
    let acctId = InsertHelpers.insertAccount txn (TestData.accountCode prefix) "Test" atId true
    Assert.True(acctId > 0)

// =====================================================================
// fiscal_period
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LSC-011")>]
let ``fiscal_period requires a period_key`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let ex = ConstraintAssert.tryInsertTxn txn "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date) VALUES (NULL, '2099-01-01', '2099-01-31')"
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-012")>]
let ``fiscal_period period_key must be unique`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let key = TestData.periodKey prefix
    InsertHelpers.insertFiscalPeriod txn key (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true |> ignore
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date) VALUES (@k, '2099-02-01', '2099-02-28')"
                (fun cmd -> cmd.Parameters.AddWithValue("@k", key) |> ignore)
    ConstraintAssert.assertUnique ex "Expected UNIQUE violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-013")>]
let ``fiscal_period requires a start_date`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let ex = ConstraintAssert.tryInsertTxn txn "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date) VALUES ('2099-01', NULL, '2099-01-31')"
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-014")>]
let ``fiscal_period requires an end_date`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let ex = ConstraintAssert.tryInsertTxn txn "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date) VALUES ('2099-01', '2099-01-01', NULL)"
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

// =====================================================================
// journal_entry
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LSC-015")>]
let ``journal_entry requires an entry_date`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let fpId = InsertHelpers.insertFiscalPeriod txn "l015fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id) VALUES (NULL, 'Test', @fp)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-016")>]
let ``journal_entry requires a description`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let fpId = InsertHelpers.insertFiscalPeriod txn "l016fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id) VALUES ('2026-01-01', NULL, @fp)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-017")>]
let ``journal_entry requires a fiscal_period_id`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let ex = ConstraintAssert.tryInsertTxn txn "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id) VALUES ('2026-01-01', 'Test', NULL)"
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-018")>]
let ``journal_entry fiscal_period_id must reference a valid fiscal_period`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id) VALUES ('2026-01-01', 'Test', @fp)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", 9999) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-019")>]
let ``journal_entry voided_at is nullable`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let fpId = InsertHelpers.insertFiscalPeriod txn "l019fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test" fpId
    // If we get here, voided_at defaulted to NULL successfully
    Assert.True(jeId > 0)

// =====================================================================
// journal_entry_reference
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LSC-020")>]
let ``journal_entry_reference requires a journal_entry_id`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let ex = ConstraintAssert.tryInsertTxn txn "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) VALUES (NULL, 'invoice', 'INV-001')"
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-021")>]
let ``journal_entry_reference journal_entry_id must reference a valid journal_entry`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) VALUES (@je, 'invoice', 'INV-001')"
                (fun cmd -> cmd.Parameters.AddWithValue("@je", 9999) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-022")>]
let ``journal_entry_reference requires reference_type`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let fpId = InsertHelpers.insertFiscalPeriod txn "l022fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test" fpId
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) VALUES (@je, NULL, 'INV-001')"
                (fun cmd -> cmd.Parameters.AddWithValue("@je", jeId) |> ignore)
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-023")>]
let ``journal_entry_reference requires reference_value`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let fpId = InsertHelpers.insertFiscalPeriod txn "l023fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test" fpId
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) VALUES (@je, 'invoice', NULL)"
                (fun cmd -> cmd.Parameters.AddWithValue("@je", jeId) |> ignore)
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

// =====================================================================
// journal_entry_line
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LSC-024")>]
let ``journal_entry_line requires a journal_entry_id`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let atId = InsertHelpers.insertAccountType txn "lsc024_type" "debit"
    let acctId = InsertHelpers.insertAccount txn "LSC024" "Test" atId true
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (NULL, @acct, 100.00, 'debit')"
                (fun cmd -> cmd.Parameters.AddWithValue("@acct", acctId) |> ignore)
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-025")>]
let ``journal_entry_line journal_entry_id must reference a valid journal_entry`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let atId = InsertHelpers.insertAccountType txn "lsc025_type" "debit"
    let acctId = InsertHelpers.insertAccount txn "LSC025" "Test" atId true
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, 'debit')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@je", 9999) |> ignore
                    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-026")>]
let ``journal_entry_line requires an account_id`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let fpId = InsertHelpers.insertFiscalPeriod txn "l026fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test" fpId
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, NULL, 100.00, 'debit')"
                (fun cmd -> cmd.Parameters.AddWithValue("@je", jeId) |> ignore)
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-027")>]
let ``journal_entry_line account_id must reference a valid account`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let fpId = InsertHelpers.insertFiscalPeriod txn "l027fp" (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test" fpId
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, 'debit')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
                    cmd.Parameters.AddWithValue("@acct", 9999) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-028")>]
let ``journal_entry_line requires an amount`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (TestData.accountTypeName prefix) "debit"
    let acctId = InsertHelpers.insertAccount txn (TestData.accountCode prefix) "Test" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (TestData.periodKey prefix) (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test" fpId
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, NULL, 'debit')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
                    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore)
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"

[<Fact>]
[<Trait("GherkinId", "FT-LSC-029")>]
let ``journal_entry_line requires an entry_type`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (TestData.accountTypeName prefix) "debit"
    let acctId = InsertHelpers.insertAccount txn (TestData.accountCode prefix) "Test" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (TestData.periodKey prefix) (System.DateOnly(2099, 1, 1)) (System.DateOnly(2099, 1, 31)) true
    let jeId = InsertHelpers.insertJournalEntry txn (System.DateOnly(2026, 1, 1)) "Test" fpId
    let ex = ConstraintAssert.tryExecTxn txn
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, NULL)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
                    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore)
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
