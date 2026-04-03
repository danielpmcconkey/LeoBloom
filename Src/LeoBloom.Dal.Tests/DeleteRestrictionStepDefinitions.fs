/// Step definitions for ON DELETE RESTRICT scenarios.
/// Each scenario inserts a parent + child chain within a transaction,
/// sets the delete target on ScenarioContext, and a single When step
/// reads the target and attempts the DELETE.
module LeoBloom.Dal.Tests.DeleteRestrictionStepDefinitions

open Npgsql
open TickSpec
open LeoBloom.Dal.Tests.SharedSteps

// =====================================================================
// Single When step — reads DeleteTarget from context and executes
// =====================================================================

let [<When>] ``I delete the parent record`` (ctx: ScenarioContext) =
    match ctx.DeleteTarget with
    | None -> failwith "No DeleteTarget set on ScenarioContext — Given step must set it"
    | Some target ->
        let ex = tryExec ctx target.Sql target.ParamSetup
        { ctx with LastException = ex }

// =====================================================================
// Helper to set delete target on context
// =====================================================================

let private withTarget (sql: string) (paramSetup: NpgsqlCommand -> unit) (ctx: ScenarioContext) =
    { ctx with
        DeleteTarget = Some { Sql = sql; ParamSetup = paramSetup } }

// =====================================================================
// 1. account_type → account
// =====================================================================

let [<Given>] ``an account_type with a dependent account exists`` () =
    let ctx = openContext ()
    let atId = insertAccountType ctx "del_test_type"
    insertAccount ctx "DT01" "DelTest Account" atId |> ignore
    ctx |> withTarget
        "DELETE FROM ledger.account_type WHERE name = @n"
        (fun cmd -> cmd.Parameters.AddWithValue("@n", "del_test_type") |> ignore)

// =====================================================================
// 2. account → child account (parent_code)
// =====================================================================

let [<Given>] ``an account with a dependent child account exists`` () =
    let ctx = openContext ()
    let atId = insertAccountType ctx "del_parent_type"
    insertAccount ctx "DP01" "Parent Account" atId |> ignore
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.account (code, name, account_type_id, parent_code) VALUES ('DC01', 'Child Account', @at, 'DP01')",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@at", atId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx |> withTarget
        "DELETE FROM ledger.account WHERE code = @c"
        (fun cmd -> cmd.Parameters.AddWithValue("@c", "DP01") |> ignore)

// =====================================================================
// 3. account → journal_entry_line
// =====================================================================

let [<Given>] ``an account with a dependent journal_entry_line exists`` () =
    let ctx = openContext ()
    let atId = insertAccountType ctx "del_jel_type"
    let acctId = insertAccount ctx "DJ01" "JEL Account" atId
    let fpId = insertFiscalPeriod ctx "2099-01"
    let jeId = insertJournalEntry ctx fpId
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, 'debit')",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx |> withTarget
        "DELETE FROM ledger.account WHERE code = @c"
        (fun cmd -> cmd.Parameters.AddWithValue("@c", "DJ01") |> ignore)

// =====================================================================
// 4-5. account → obligation_agreement (source / dest)
// =====================================================================

let private setupAgreementWithAccount (ctx: ScenarioContext) (code: string) (fkColumn: string) =
    let atId = insertAccountType ctx $"del_{code}_type"
    let acctId = insertAccount ctx code $"{code} Account" atId
    use cmd = new NpgsqlCommand(
        $"INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, {fkColumn}) VALUES ('Test', 'receivable', 'monthly', @acct)",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx |> withTarget
        "DELETE FROM ledger.account WHERE code = @c"
        (fun cmd -> cmd.Parameters.AddWithValue("@c", code) |> ignore)

let [<Given>] ``an account with a dependent obligation_agreement source exists`` () =
    let ctx = openContext ()
    setupAgreementWithAccount ctx "DS01" "source_account_id"

let [<Given>] ``an account with a dependent obligation_agreement dest exists`` () =
    let ctx = openContext ()
    setupAgreementWithAccount ctx "DD01" "dest_account_id"

// =====================================================================
// 6-7. account → transfer (from / to)
// =====================================================================

let private setupTransferWithAccount (ctx: ScenarioContext) (testCode: string) (otherCode: string) (fkColumn: string) =
    let atId = insertAccountType ctx $"del_{testCode}_type"
    let testAcctId = insertAccount ctx testCode $"{testCode} Account" atId
    let otherAcctId = insertAccount ctx otherCode $"{otherCode} Account" atId
    let (fromId, toId) =
        if fkColumn = "from_account_id" then (testAcctId, otherAcctId)
        else (otherAcctId, testAcctId)
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01')",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
    cmd.Parameters.AddWithValue("@to_", toId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx |> withTarget
        "DELETE FROM ledger.account WHERE code = @c"
        (fun cmd -> cmd.Parameters.AddWithValue("@c", testCode) |> ignore)

let [<Given>] ``an account with a dependent transfer from exists`` () =
    let ctx = openContext ()
    setupTransferWithAccount ctx "TF01" "TF02" "from_account_id"

let [<Given>] ``an account with a dependent transfer to exists`` () =
    let ctx = openContext ()
    setupTransferWithAccount ctx "TT01" "TT02" "to_account_id"

// =====================================================================
// 8-9. fiscal_period → journal_entry / invoice
// =====================================================================

let [<Given>] ``a fiscal_period with a dependent journal_entry exists`` () =
    let ctx = openContext ()
    let fpId = insertFiscalPeriod ctx "2099-99"
    insertJournalEntry ctx fpId |> ignore
    ctx |> withTarget
        "DELETE FROM ledger.fiscal_period WHERE period_key = @k"
        (fun cmd -> cmd.Parameters.AddWithValue("@k", "2099-99") |> ignore)

let [<Given>] ``a fiscal_period with a dependent invoice exists`` () =
    let ctx = openContext ()
    let fpId = insertFiscalPeriod ctx "2099-99"
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', @fp, 1000.00, 200.00, 1200.00)",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx |> withTarget
        "DELETE FROM ledger.fiscal_period WHERE period_key = @k"
        (fun cmd -> cmd.Parameters.AddWithValue("@k", "2099-99") |> ignore)

// =====================================================================
// 10-13. journal_entry → reference / line / obligation_instance / transfer
// =====================================================================

let private setupJournalEntryParent (ctx: ScenarioContext) =
    let atId = insertAccountType ctx "del_je_type"
    let acctId = insertAccount ctx "JE01" "JE Account" atId
    let fpId = insertFiscalPeriod ctx "2099-04"
    let jeId = insertJournalEntry ctx fpId
    (atId, acctId, fpId, jeId)

let private withJournalEntryTarget (ctx: ScenarioContext) =
    ctx |> withTarget
        "DELETE FROM ledger.journal_entry WHERE description = 'Test JE'"
        ignore

let [<Given>] ``a journal_entry with a dependent reference exists`` () =
    let ctx = openContext ()
    let (_, _, _, jeId) = setupJournalEntryParent ctx
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) VALUES (@je, 'invoice', 'INV-001')",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    withJournalEntryTarget ctx

let [<Given>] ``a journal_entry with a dependent line exists`` () =
    let ctx = openContext ()
    let (_, acctId, _, jeId) = setupJournalEntryParent ctx
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, 'debit')",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    withJournalEntryTarget ctx

let [<Given>] ``a journal_entry with a dependent obligation_instance exists`` () =
    let ctx = openContext ()
    let (_, _, _, jeId) = setupJournalEntryParent ctx
    let oaId = insertObligationAgreement ctx "del_je_oi_agreement"
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date, journal_entry_id) VALUES (@oa, 'Test', 'expected', '2026-04-01', @je)",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    withJournalEntryTarget ctx

let [<Given>] ``a journal_entry with a dependent transfer exists`` () =
    let ctx = openContext ()
    let (atId, _, _, jeId) = setupJournalEntryParent ctx
    let fromId = insertAccount ctx "JTF1" "JE Transfer From" atId
    let toId = insertAccount ctx "JTT1" "JE Transfer To" atId
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date, journal_entry_id) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01', @je)",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
    cmd.Parameters.AddWithValue("@to_", toId) |> ignore
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    withJournalEntryTarget ctx

// =====================================================================
// 18. obligation_agreement → obligation_instance
// =====================================================================

let [<Given>] ``an obligation_agreement with a dependent instance exists`` () =
    let ctx = openContext ()
    let oaId = insertObligationAgreement ctx "del_oa_test"
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (@oa, 'Test', 'expected', '2026-04-01')",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx |> withTarget
        "DELETE FROM ops.obligation_agreement WHERE name = 'del_oa_test'"
        ignore
