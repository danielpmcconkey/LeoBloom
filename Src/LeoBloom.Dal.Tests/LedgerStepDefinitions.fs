module LeoBloom.Dal.Tests.LedgerStepDefinitions

open Npgsql
open TickSpec
open LeoBloom.Dal.Tests.SharedSteps

// =====================================================================
// account_type
// =====================================================================

let [<Given>] ``an account_type "asset" exists`` () =
    // Seed data from migrations — 'asset' already exists. Just open context.
    openContext ()

let [<When>] ``I insert into account_type with a null name`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ledger.account_type (name, normal_balance) VALUES (NULL, 'debit')"
    { ctx with LastException = ex }

let [<When>] ``I insert another account_type with name "asset"`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ledger.account_type (name, normal_balance) VALUES ('asset', 'debit')"
    { ctx with LastException = ex }

let [<When>] ``I insert into account_type with a null normal_balance`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ledger.account_type (name, normal_balance) VALUES ('test_type', NULL)"
    { ctx with LastException = ex }

// =====================================================================
// account
// =====================================================================

let [<Given>] ``an account with code "1010" exists`` () =
    let ctx = openContext ()
    let atId = getValidAccountTypeId ctx
    use cmd = new NpgsqlCommand("INSERT INTO ledger.account (code, name, account_type_id) VALUES ('1010', 'Test Account', @at)", ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@at", atId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

let [<When>] ``I insert into account with a null code`` (ctx: ScenarioContext) =
    let atId = getValidAccountTypeId ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.account (code, name, account_type_id) VALUES (NULL, 'Test', @at)"
                (fun cmd -> cmd.Parameters.AddWithValue("@at", atId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert another account with code "1010"`` (ctx: ScenarioContext) =
    let atId = getValidAccountTypeId ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.account (code, name, account_type_id) VALUES ('1010', 'Duplicate', @at)"
                (fun cmd -> cmd.Parameters.AddWithValue("@at", atId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into account with a null name`` (ctx: ScenarioContext) =
    let atId = getValidAccountTypeId ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.account (code, name, account_type_id) VALUES ('ZZZZ', NULL, @at)"
                (fun cmd -> cmd.Parameters.AddWithValue("@at", atId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into account with a null account_type_id`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ledger.account (code, name, account_type_id) VALUES ('ZZZZ', 'Test', NULL)"
    { ctx with LastException = ex }

let [<When>] ``I insert into account with account_type_id 9999`` (ctx: ScenarioContext) =
    let ex = tryExec ctx
                "INSERT INTO ledger.account (code, name, account_type_id) VALUES ('ZZZZ', 'Test', @id)"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into account with parent_code "XXXX" that does not exist`` (ctx: ScenarioContext) =
    let atId = getValidAccountTypeId ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.account (code, name, account_type_id, parent_code) VALUES ('ZZZZ', 'Test', @at, @pc)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@at", atId) |> ignore
                    cmd.Parameters.AddWithValue("@pc", "XXXX") |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert a valid account with a null parent_code`` (ctx: ScenarioContext) =
    let atId = getValidAccountTypeId ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.account (code, name, account_type_id, parent_code) VALUES ('ZZZZ', 'Test', @at, NULL)"
                (fun cmd -> cmd.Parameters.AddWithValue("@at", atId) |> ignore)
    { ctx with LastException = ex }

// =====================================================================
// fiscal_period
// =====================================================================

let [<Given>] ``a fiscal_period "2026-03" exists`` () =
    // Seed data from migrations — '2026-03' should exist.
    openContext ()

let [<When>] ``I insert into fiscal_period with a null period_key`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date) VALUES (NULL, '2099-01-01', '2099-01-31')"
    { ctx with LastException = ex }

let [<When>] ``I insert another fiscal_period with period_key "2026-03"`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date) VALUES ('2026-03', '2026-03-01', '2026-03-31')"
    { ctx with LastException = ex }

let [<When>] ``I insert into fiscal_period with a null start_date`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date) VALUES ('2099-01', NULL, '2099-01-31')"
    { ctx with LastException = ex }

let [<When>] ``I insert into fiscal_period with a null end_date`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date) VALUES ('2099-01', '2099-01-01', NULL)"
    { ctx with LastException = ex }

// =====================================================================
// journal_entry
// =====================================================================

let [<When>] ``I insert into journal_entry with a null entry_date`` (ctx: ScenarioContext) =
    let fpId = getValidFiscalPeriodId ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id) VALUES (NULL, 'Test', @fp)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into journal_entry with a null description`` (ctx: ScenarioContext) =
    let fpId = getValidFiscalPeriodId ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id) VALUES ('2026-01-01', NULL, @fp)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into journal_entry with a null fiscal_period_id`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id) VALUES ('2026-01-01', 'Test', NULL)"
    { ctx with LastException = ex }

let [<When>] ``I insert into journal_entry with fiscal_period_id 9999`` (ctx: ScenarioContext) =
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id) VALUES ('2026-01-01', 'Test', @fp)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert a valid journal_entry with null voided_at`` (ctx: ScenarioContext) =
    let fpId = getValidFiscalPeriodId ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id, voided_at) VALUES ('2026-01-01', 'Test', @fp, NULL)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
    { ctx with LastException = ex }

// =====================================================================
// journal_entry_reference
// =====================================================================

let private insertJournalEntryLocal (ctx: ScenarioContext) =
    let fpId = getValidFiscalPeriodId ctx
    insertJournalEntry ctx fpId

let [<When>] ``I insert into journal_entry_reference with a null journal_entry_id`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) VALUES (NULL, 'invoice', 'INV-001')"
    { ctx with LastException = ex }

let [<When>] ``I insert into journal_entry_reference with journal_entry_id 9999`` (ctx: ScenarioContext) =
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) VALUES (@je, 'invoice', 'INV-001')"
                (fun cmd -> cmd.Parameters.AddWithValue("@je", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into journal_entry_reference with a null reference_type`` (ctx: ScenarioContext) =
    let jeId = insertJournalEntryLocal ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) VALUES (@je, NULL, 'INV-001')"
                (fun cmd -> cmd.Parameters.AddWithValue("@je", jeId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into journal_entry_reference with a null reference_value`` (ctx: ScenarioContext) =
    let jeId = insertJournalEntryLocal ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) VALUES (@je, 'invoice', NULL)"
                (fun cmd -> cmd.Parameters.AddWithValue("@je", jeId) |> ignore)
    { ctx with LastException = ex }

// =====================================================================
// journal_entry_line
// =====================================================================

let [<When>] ``I insert into journal_entry_line with a null journal_entry_id`` (ctx: ScenarioContext) =
    let acctId = getValidAccountId ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (NULL, @acct, 100.00, 'debit')"
                (fun cmd -> cmd.Parameters.AddWithValue("@acct", acctId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into journal_entry_line with journal_entry_id 9999`` (ctx: ScenarioContext) =
    let acctId = getValidAccountId ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, 'debit')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@je", 9999) |> ignore
                    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into journal_entry_line with a null account_id`` (ctx: ScenarioContext) =
    let jeId = insertJournalEntryLocal ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, NULL, 100.00, 'debit')"
                (fun cmd -> cmd.Parameters.AddWithValue("@je", jeId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into journal_entry_line with account_id 9999`` (ctx: ScenarioContext) =
    let jeId = insertJournalEntryLocal ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, 'debit')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
                    cmd.Parameters.AddWithValue("@acct", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into journal_entry_line with a null amount`` (ctx: ScenarioContext) =
    let jeId = insertJournalEntryLocal ctx
    let acctId = getValidAccountId ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, NULL, 'debit')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
                    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into journal_entry_line with a null entry_type`` (ctx: ScenarioContext) =
    let jeId = insertJournalEntryLocal ctx
    let acctId = getValidAccountId ctx
    let ex = tryExec ctx
                "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, NULL)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
                    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore)
    { ctx with LastException = ex }
