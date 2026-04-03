module LeoBloom.Dal.Tests.OpsStepDefinitions

open Npgsql
open TickSpec
open LeoBloom.Dal.Tests.SharedSteps

// =====================================================================
// obligation_type
// =====================================================================

let [<Given>] ``an obligation_type "receivable" exists`` () =
    // Seed data from migrations — 'receivable' already exists.
    openContext ()

let [<When>] ``I insert another obligation_type with name "receivable"`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.obligation_type (name) VALUES ('receivable')"
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_type with a null name`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.obligation_type (name) VALUES (NULL)"
    { ctx with LastException = ex }

// =====================================================================
// obligation_status
// =====================================================================

let [<Given>] ``an obligation_status "expected" exists`` () =
    // Seed data from migrations — 'expected' already exists.
    openContext ()

let [<When>] ``I insert another obligation_status with name "expected"`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.obligation_status (name) VALUES ('expected')"
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_status with a null name`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.obligation_status (name) VALUES (NULL)"
    { ctx with LastException = ex }

// =====================================================================
// cadence
// =====================================================================

let [<Given>] ``a cadence "monthly" exists`` () =
    // Seed data from migrations — 'monthly' already exists.
    openContext ()

let [<When>] ``I insert another cadence with name "monthly"`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.cadence (name) VALUES ('monthly')"
    { ctx with LastException = ex }

let [<When>] ``I insert into cadence with a null name`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.cadence (name) VALUES (NULL)"
    { ctx with LastException = ex }

// =====================================================================
// payment_method
// =====================================================================

let [<Given>] ``a payment_method "zelle" exists`` () =
    // Seed data from migrations — 'zelle' already exists.
    openContext ()

let [<When>] ``I insert another payment_method with name "zelle"`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.payment_method (name) VALUES ('zelle')"
    { ctx with LastException = ex }

let [<When>] ``I insert into payment_method with a null name`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.payment_method (name) VALUES (NULL)"
    { ctx with LastException = ex }

// =====================================================================
// obligation_agreement
// =====================================================================

let [<When>] ``I insert into obligation_agreement with a null name`` (ctx: ScenarioContext) =
    let otId = getValidObligationTypeId ctx
    let cId = getValidCadenceId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id) VALUES (NULL, @ot, @c)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@ot", otId) |> ignore
                    cmd.Parameters.AddWithValue("@c", cId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_agreement with a null obligation_type_id`` (ctx: ScenarioContext) =
    let cId = getValidCadenceId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id) VALUES ('Test', NULL, @c)"
                (fun cmd -> cmd.Parameters.AddWithValue("@c", cId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_agreement with obligation_type_id 9999`` (ctx: ScenarioContext) =
    let cId = getValidCadenceId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id) VALUES ('Test', @ot, @c)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@ot", 9999) |> ignore
                    cmd.Parameters.AddWithValue("@c", cId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_agreement with a null cadence_id`` (ctx: ScenarioContext) =
    let otId = getValidObligationTypeId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id) VALUES ('Test', @ot, NULL)"
                (fun cmd -> cmd.Parameters.AddWithValue("@ot", otId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_agreement with cadence_id 9999`` (ctx: ScenarioContext) =
    let otId = getValidObligationTypeId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id) VALUES ('Test', @ot, @c)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@ot", otId) |> ignore
                    cmd.Parameters.AddWithValue("@c", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_agreement with payment_method_id 9999`` (ctx: ScenarioContext) =
    let otId = getValidObligationTypeId ctx
    let cId = getValidCadenceId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id, payment_method_id) VALUES ('Test', @ot, @c, @pm)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@ot", otId) |> ignore
                    cmd.Parameters.AddWithValue("@c", cId) |> ignore
                    cmd.Parameters.AddWithValue("@pm", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_agreement with source_account_id 9999`` (ctx: ScenarioContext) =
    let otId = getValidObligationTypeId ctx
    let cId = getValidCadenceId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id, source_account_id) VALUES ('Test', @ot, @c, @sa)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@ot", otId) |> ignore
                    cmd.Parameters.AddWithValue("@c", cId) |> ignore
                    cmd.Parameters.AddWithValue("@sa", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_agreement with dest_account_id 9999`` (ctx: ScenarioContext) =
    let otId = getValidObligationTypeId ctx
    let cId = getValidCadenceId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id, dest_account_id) VALUES ('Test', @ot, @c, @da)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@ot", otId) |> ignore
                    cmd.Parameters.AddWithValue("@c", cId) |> ignore
                    cmd.Parameters.AddWithValue("@da", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert a valid obligation_agreement with a null amount`` (ctx: ScenarioContext) =
    let otId = getValidObligationTypeId ctx
    let cId = getValidCadenceId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id, amount) VALUES ('Test', @ot, @c, NULL)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@ot", otId) |> ignore
                    cmd.Parameters.AddWithValue("@c", cId) |> ignore)
    { ctx with LastException = ex }

// =====================================================================
// obligation_instance
// =====================================================================

let [<When>] ``I insert into obligation_instance with a null obligation_agreement_id`` (ctx: ScenarioContext) =
    let sId = getValidObligationStatusId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status_id, expected_date) VALUES (NULL, 'Test', @s, '2026-04-01')"
                (fun cmd -> cmd.Parameters.AddWithValue("@s", sId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_instance with obligation_agreement_id 9999`` (ctx: ScenarioContext) =
    let sId = getValidObligationStatusId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status_id, expected_date) VALUES (@oa, 'Test', @s, '2026-04-01')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@oa", 9999) |> ignore
                    cmd.Parameters.AddWithValue("@s", sId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_instance with a null name`` (ctx: ScenarioContext) =
    let oaId = insertObligationAgreement ctx "TestAgreement"
    let sId = getValidObligationStatusId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status_id, expected_date) VALUES (@oa, NULL, @s, '2026-04-01')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
                    cmd.Parameters.AddWithValue("@s", sId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_instance with a null status_id`` (ctx: ScenarioContext) =
    let oaId = insertObligationAgreement ctx "TestAgreement"
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status_id, expected_date) VALUES (@oa, 'Test', NULL, '2026-04-01')"
                (fun cmd -> cmd.Parameters.AddWithValue("@oa", oaId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_instance with status_id 9999`` (ctx: ScenarioContext) =
    let oaId = insertObligationAgreement ctx "TestAgreement"
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status_id, expected_date) VALUES (@oa, 'Test', @s, '2026-04-01')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
                    cmd.Parameters.AddWithValue("@s", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_instance with a null expected_date`` (ctx: ScenarioContext) =
    let oaId = insertObligationAgreement ctx "TestAgreement"
    let sId = getValidObligationStatusId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status_id, expected_date) VALUES (@oa, 'Test', @s, NULL)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
                    cmd.Parameters.AddWithValue("@s", sId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_instance with journal_entry_id 9999`` (ctx: ScenarioContext) =
    let oaId = insertObligationAgreement ctx "TestAgreement"
    let sId = getValidObligationStatusId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status_id, expected_date, journal_entry_id) VALUES (@oa, 'Test', @s, '2026-04-01', @je)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
                    cmd.Parameters.AddWithValue("@s", sId) |> ignore
                    cmd.Parameters.AddWithValue("@je", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert a valid obligation_instance with null journal_entry_id`` (ctx: ScenarioContext) =
    let oaId = insertObligationAgreement ctx "TestAgreement"
    let sId = getValidObligationStatusId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status_id, expected_date, journal_entry_id) VALUES (@oa, 'Test', @s, '2026-04-01', NULL)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
                    cmd.Parameters.AddWithValue("@s", sId) |> ignore)
    { ctx with LastException = ex }

// =====================================================================
// transfer
// =====================================================================

let private insertTwoAccounts (ctx: ScenarioContext) =
    let fromId =
        use cmd = new NpgsqlCommand("INSERT INTO ledger.account (code, name, account_type_id) VALUES ('TF01', 'Transfer From', 1) RETURNING id", ctx.Transaction.Connection, ctx.Transaction)
        cmd.ExecuteScalar() :?> int
    let toId =
        use cmd = new NpgsqlCommand("INSERT INTO ledger.account (code, name, account_type_id) VALUES ('TT01', 'Transfer To', 1) RETURNING id", ctx.Transaction.Connection, ctx.Transaction)
        cmd.ExecuteScalar() :?> int
    (fromId, toId)

let [<When>] ``I insert into transfer with a null from_account_id`` (ctx: ScenarioContext) =
    let (_, toId) = insertTwoAccounts ctx
    let ex = tryExec ctx
                "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (NULL, @to_, 100.00, 'initiated', '2026-04-01')"
                (fun cmd -> cmd.Parameters.AddWithValue("@to_", toId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into transfer with from_account_id 9999`` (ctx: ScenarioContext) =
    let (_, toId) = insertTwoAccounts ctx
    let ex = tryExec ctx
                "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@from_", 9999) |> ignore
                    cmd.Parameters.AddWithValue("@to_", toId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into transfer with a null to_account_id`` (ctx: ScenarioContext) =
    let (fromId, _) = insertTwoAccounts ctx
    let ex = tryExec ctx
                "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, NULL, 100.00, 'initiated', '2026-04-01')"
                (fun cmd -> cmd.Parameters.AddWithValue("@from_", fromId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into transfer with to_account_id 9999`` (ctx: ScenarioContext) =
    let (fromId, _) = insertTwoAccounts ctx
    let ex = tryExec ctx
                "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
                    cmd.Parameters.AddWithValue("@to_", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into transfer with a null amount`` (ctx: ScenarioContext) =
    let (fromId, toId) = insertTwoAccounts ctx
    let ex = tryExec ctx
                "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, NULL, 'initiated', '2026-04-01')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
                    cmd.Parameters.AddWithValue("@to_", toId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into transfer with a null status`` (ctx: ScenarioContext) =
    let (fromId, toId) = insertTwoAccounts ctx
    let ex = tryExec ctx
                "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, NULL, '2026-04-01')"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
                    cmd.Parameters.AddWithValue("@to_", toId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into transfer with a null initiated_date`` (ctx: ScenarioContext) =
    let (fromId, toId) = insertTwoAccounts ctx
    let ex = tryExec ctx
                "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date) VALUES (@from_, @to_, 100.00, 'initiated', NULL)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
                    cmd.Parameters.AddWithValue("@to_", toId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into transfer with journal_entry_id 9999`` (ctx: ScenarioContext) =
    let (fromId, toId) = insertTwoAccounts ctx
    let ex = tryExec ctx
                "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date, journal_entry_id) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01', @je)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
                    cmd.Parameters.AddWithValue("@to_", toId) |> ignore
                    cmd.Parameters.AddWithValue("@je", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert a valid transfer with null journal_entry_id`` (ctx: ScenarioContext) =
    let (fromId, toId) = insertTwoAccounts ctx
    let ex = tryExec ctx
                "INSERT INTO ops.transfer (from_account_id, to_account_id, amount, status, initiated_date, journal_entry_id) VALUES (@from_, @to_, 100.00, 'initiated', '2026-04-01', NULL)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@from_", fromId) |> ignore
                    cmd.Parameters.AddWithValue("@to_", toId) |> ignore)
    { ctx with LastException = ex }

// =====================================================================
// invoice
// =====================================================================

let [<Given>] ``an invoice for tenant "Brian" and fiscal_period "2026-03" exists`` () =
    let ctx = openContext ()
    let fpId = getValidFiscalPeriodId ctx
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Brian', @fp, 1000.00, 200.00, 1200.00)",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

let [<When>] ``I insert into invoice with a null tenant`` (ctx: ScenarioContext) =
    let fpId = getValidFiscalPeriodId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES (NULL, @fp, 1000.00, 200.00, 1200.00)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into invoice with a null fiscal_period_id`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', NULL, 1000.00, 200.00, 1200.00)"
    { ctx with LastException = ex }

let [<When>] ``I insert into invoice with fiscal_period_id 9999`` (ctx: ScenarioContext) =
    let ex = tryExec ctx
                "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', @fp, 1000.00, 200.00, 1200.00)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into invoice with a null rent_amount`` (ctx: ScenarioContext) =
    let fpId = getValidFiscalPeriodId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', @fp, NULL, 200.00, 1200.00)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into invoice with a null utility_share`` (ctx: ScenarioContext) =
    let fpId = getValidFiscalPeriodId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', @fp, 1000.00, NULL, 1200.00)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into invoice with a null total_amount`` (ctx: ScenarioContext) =
    let fpId = getValidFiscalPeriodId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Test', @fp, 1000.00, 200.00, NULL)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert another invoice for tenant "Brian" and fiscal_period "2026-03"`` (ctx: ScenarioContext) =
    let fpId = getValidFiscalPeriodId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Brian', @fp, 500.00, 100.00, 600.00)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
    { ctx with LastException = ex }
