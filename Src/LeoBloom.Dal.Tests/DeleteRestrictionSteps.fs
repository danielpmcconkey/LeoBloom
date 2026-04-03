/// Step definitions for ON DELETE RESTRICT scenarios.
/// Each scenario inserts a parent + child chain within a transaction,
/// attempts to DELETE the parent, and asserts the FK violation.
module LeoBloom.Dal.Tests.DeleteRestrictionSteps

open Npgsql
open TickSpec
open LeoBloom.Dal.Tests.SharedSteps

// =====================================================================
// Helpers — insert test data within the transaction and return IDs
// =====================================================================

let private insertAccountType (ctx: ScenarioContext) (name: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.account_type (name, normal_balance) VALUES (@n, 'debit') RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.ExecuteScalar() :?> int

let private insertAccount (ctx: ScenarioContext) (code: string) (name: string) (atId: int) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.account (code, name, account_type_id) VALUES (@c, @n, @at) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@c", code) |> ignore
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.Parameters.AddWithValue("@at", atId) |> ignore
    cmd.ExecuteScalar() :?> int

let private insertFiscalPeriod (ctx: ScenarioContext) (key: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date) VALUES (@k, '2099-01-01', '2099-01-31') RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@k", key) |> ignore
    cmd.ExecuteScalar() :?> int

let private insertJournalEntry (ctx: ScenarioContext) (fpId: int) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id) VALUES ('2026-01-01', 'Test JE', @fp) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    cmd.ExecuteScalar() :?> int

let private insertObligationType (ctx: ScenarioContext) (name: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_type (name) VALUES (@n) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.ExecuteScalar() :?> int

let private insertCadence (ctx: ScenarioContext) (name: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.cadence (name) VALUES (@n) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.ExecuteScalar() :?> int

let private insertPaymentMethod (ctx: ScenarioContext) (name: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.payment_method (name) VALUES (@n) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.ExecuteScalar() :?> int

let private insertObligationStatus (ctx: ScenarioContext) (name: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_status (name) VALUES (@n) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.ExecuteScalar() :?> int

let private tryDelete (ctx: ScenarioContext) (sql: string) (paramSetup: NpgsqlCommand -> unit) =
    tryExec ctx sql paramSetup

// =====================================================================
// 1. account_type → account
// =====================================================================

let [<Given>] ``an account_type with a dependent account exists`` () =
    let ctx = openContext ()
    let atId = insertAccountType ctx "del_test_type"
    insertAccount ctx "DT01" "DelTest Account" atId |> ignore
    ctx

let [<When>] ``I delete the parent account_type`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ledger.account_type WHERE name = @n"
                (fun cmd -> cmd.Parameters.AddWithValue("@n", "del_test_type") |> ignore)
    { ctx with LastException = ex }

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
    ctx

let [<When>] ``I delete the parent account`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ledger.account WHERE code = @c"
                (fun cmd -> cmd.Parameters.AddWithValue("@c", "DP01") |> ignore)
    { ctx with LastException = ex }

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
    ctx

let [<When>] ``I delete the account referenced by journal_entry_line`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ledger.account WHERE code = @c"
                (fun cmd -> cmd.Parameters.AddWithValue("@c", "DJ01") |> ignore)
    { ctx with LastException = ex }

// =====================================================================
// 4-5. account → obligation_agreement (source / dest)
// =====================================================================

let private setupAgreementWithAccount (ctx: ScenarioContext) (code: string) (fkColumn: string) =
    let atId = insertAccountType ctx $"del_{code}_type"
    let acctId = insertAccount ctx code $"{code} Account" atId
    let otId = insertObligationType ctx $"del_{code}_ot"
    let cId = insertCadence ctx $"del_{code}_cad"
    use cmd = new NpgsqlCommand(
        $"INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id, {fkColumn}) VALUES ('Test', @ot, @c, @acct)",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@ot", otId) |> ignore
    cmd.Parameters.AddWithValue("@c", cId) |> ignore
    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

let [<Given>] ``an account with a dependent obligation_agreement source exists`` () =
    let ctx = openContext ()
    setupAgreementWithAccount ctx "DS01" "source_account_id"

let [<When>] ``I delete the account referenced as source_account`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ledger.account WHERE code = @c"
                (fun cmd -> cmd.Parameters.AddWithValue("@c", "DS01") |> ignore)
    { ctx with LastException = ex }

let [<Given>] ``an account with a dependent obligation_agreement dest exists`` () =
    let ctx = openContext ()
    setupAgreementWithAccount ctx "DD01" "dest_account_id"

let [<When>] ``I delete the account referenced as dest_account`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ledger.account WHERE code = @c"
                (fun cmd -> cmd.Parameters.AddWithValue("@c", "DD01") |> ignore)
    { ctx with LastException = ex }

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
    ctx

let [<Given>] ``an account with a dependent transfer from exists`` () =
    let ctx = openContext ()
    setupTransferWithAccount ctx "TF01" "TF02" "from_account_id"

let [<When>] ``I delete the account referenced as from_account`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ledger.account WHERE code = @c"
                (fun cmd -> cmd.Parameters.AddWithValue("@c", "TF01") |> ignore)
    { ctx with LastException = ex }

let [<Given>] ``an account with a dependent transfer to exists`` () =
    let ctx = openContext ()
    setupTransferWithAccount ctx "TT01" "TT02" "to_account_id"

let [<When>] ``I delete the account referenced as to_account`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ledger.account WHERE code = @c"
                (fun cmd -> cmd.Parameters.AddWithValue("@c", "TT01") |> ignore)
    { ctx with LastException = ex }

// =====================================================================
// 8-9. fiscal_period → journal_entry / invoice
// =====================================================================

let [<Given>] ``a fiscal_period with a dependent journal_entry exists`` () =
    let ctx = openContext ()
    let fpId = insertFiscalPeriod ctx "2099-02"
    insertJournalEntry ctx fpId |> ignore
    ctx

let [<Given>] ``a fiscal_period with a dependent invoice exists`` () =
    let ctx = openContext ()
    let fpId = insertFiscalPeriod ctx "2099-03"
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', @fp, 1000.00, 200.00, 1200.00)",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

let [<When>] ``I delete the parent fiscal_period`` (ctx: ScenarioContext) =
    // We need to figure out which fiscal_period to delete. Use the test-inserted one.
    // Since we're in a transaction, the test-inserted period is the one with a known key.
    // But the When step doesn't know which key was used. We'll delete by the latest inserted id.
    let ex = tryDelete ctx
                "DELETE FROM ledger.fiscal_period WHERE period_key LIKE '2099-%'"
                ignore
    { ctx with LastException = ex }

// =====================================================================
// 10-13. journal_entry → reference / line / obligation_instance / transfer
// =====================================================================

let private setupJournalEntryParent (ctx: ScenarioContext) =
    let atId = insertAccountType ctx "del_je_type"
    let acctId = insertAccount ctx "JE01" "JE Account" atId
    let fpId = insertFiscalPeriod ctx "2099-04"
    let jeId = insertJournalEntry ctx fpId
    (atId, acctId, fpId, jeId)

let [<Given>] ``a journal_entry with a dependent reference exists`` () =
    let ctx = openContext ()
    let (_, _, _, jeId) = setupJournalEntryParent ctx
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) VALUES (@je, 'invoice', 'INV-001')",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

let [<Given>] ``a journal_entry with a dependent line exists`` () =
    let ctx = openContext ()
    let (_, acctId, _, jeId) = setupJournalEntryParent ctx
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) VALUES (@je, @acct, 100.00, 'debit')",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.Parameters.AddWithValue("@acct", acctId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

let [<Given>] ``a journal_entry with a dependent obligation_instance exists`` () =
    let ctx = openContext ()
    let (_, _, _, jeId) = setupJournalEntryParent ctx
    let otId = insertObligationType ctx "del_je_oi_ot"
    let cId = insertCadence ctx "del_je_oi_cad"
    let sId = insertObligationStatus ctx "del_je_oi_status"
    use cmdOa = new NpgsqlCommand(
        "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id) VALUES ('Test', @ot, @c) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmdOa.Parameters.AddWithValue("@ot", otId) |> ignore
    cmdOa.Parameters.AddWithValue("@c", cId) |> ignore
    let oaId = cmdOa.ExecuteScalar() :?> int
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status_id, expected_date, journal_entry_id) VALUES (@oa, 'Test', @s, '2026-04-01', @je)",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
    cmd.Parameters.AddWithValue("@s", sId) |> ignore
    cmd.Parameters.AddWithValue("@je", jeId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

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
    ctx

let [<When>] ``I delete the parent journal_entry`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ledger.journal_entry WHERE description = 'Test JE'"
                ignore
    { ctx with LastException = ex }

// =====================================================================
// 14. obligation_type → obligation_agreement
// =====================================================================

let [<Given>] ``an obligation_type with a dependent agreement exists`` () =
    let ctx = openContext ()
    let otId = insertObligationType ctx "del_ot_test"
    let cId = insertCadence ctx "del_ot_cad"
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id) VALUES ('Test', @ot, @c)",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@ot", otId) |> ignore
    cmd.Parameters.AddWithValue("@c", cId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

let [<When>] ``I delete the parent obligation_type`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ops.obligation_type WHERE name = 'del_ot_test'"
                ignore
    { ctx with LastException = ex }

// =====================================================================
// 15. cadence → obligation_agreement
// =====================================================================

let [<Given>] ``a cadence with a dependent agreement exists`` () =
    let ctx = openContext ()
    let otId = insertObligationType ctx "del_cad_ot"
    let cId = insertCadence ctx "del_cad_test"
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id) VALUES ('Test', @ot, @c)",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@ot", otId) |> ignore
    cmd.Parameters.AddWithValue("@c", cId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

let [<When>] ``I delete the parent cadence`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ops.cadence WHERE name = 'del_cad_test'"
                ignore
    { ctx with LastException = ex }

// =====================================================================
// 16. payment_method → obligation_agreement
// =====================================================================

let [<Given>] ``a payment_method with a dependent agreement exists`` () =
    let ctx = openContext ()
    let otId = insertObligationType ctx "del_pm_ot"
    let cId = insertCadence ctx "del_pm_cad"
    let pmId = insertPaymentMethod ctx "del_pm_test"
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id, payment_method_id) VALUES ('Test', @ot, @c, @pm)",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@ot", otId) |> ignore
    cmd.Parameters.AddWithValue("@c", cId) |> ignore
    cmd.Parameters.AddWithValue("@pm", pmId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

let [<When>] ``I delete the parent payment_method`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ops.payment_method WHERE name = 'del_pm_test'"
                ignore
    { ctx with LastException = ex }

// =====================================================================
// 17. obligation_status → obligation_instance
// =====================================================================

let [<Given>] ``an obligation_status with a dependent instance exists`` () =
    let ctx = openContext ()
    let otId = insertObligationType ctx "del_os_ot"
    let cId = insertCadence ctx "del_os_cad"
    let sId = insertObligationStatus ctx "del_os_test"
    use cmdOa = new NpgsqlCommand(
        "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id) VALUES ('Test', @ot, @c) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmdOa.Parameters.AddWithValue("@ot", otId) |> ignore
    cmdOa.Parameters.AddWithValue("@c", cId) |> ignore
    let oaId = cmdOa.ExecuteScalar() :?> int
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status_id, expected_date) VALUES (@oa, 'Test', @s, '2026-04-01')",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
    cmd.Parameters.AddWithValue("@s", sId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

let [<When>] ``I delete the parent obligation_status`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ops.obligation_status WHERE name = 'del_os_test'"
                ignore
    { ctx with LastException = ex }

// =====================================================================
// 18. obligation_agreement → obligation_instance
// =====================================================================

let [<Given>] ``an obligation_agreement with a dependent instance exists`` () =
    let ctx = openContext ()
    let otId = insertObligationType ctx "del_oa_ot"
    let cId = insertCadence ctx "del_oa_cad"
    let sId = insertObligationStatus ctx "del_oa_status"
    use cmdOa = new NpgsqlCommand(
        "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id) VALUES ('del_oa_test', @ot, @c) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmdOa.Parameters.AddWithValue("@ot", otId) |> ignore
    cmdOa.Parameters.AddWithValue("@c", cId) |> ignore
    let oaId = cmdOa.ExecuteScalar() :?> int
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status_id, expected_date) VALUES (@oa, 'Test', @s, '2026-04-01')",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
    cmd.Parameters.AddWithValue("@s", sId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

let [<When>] ``I delete the parent obligation_agreement`` (ctx: ScenarioContext) =
    let ex = tryDelete ctx
                "DELETE FROM ops.obligation_agreement WHERE name = 'del_oa_test'"
                ignore
    { ctx with LastException = ex }
