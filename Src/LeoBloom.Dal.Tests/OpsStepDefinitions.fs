module LeoBloom.Dal.Tests.OpsStepDefinitions

open Npgsql
open TickSpec
open LeoBloom.Dal.Tests.SharedSteps

// =====================================================================
// obligation_agreement
// =====================================================================

let [<When>] ``I insert into obligation_agreement with a null name`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence) VALUES (NULL, 'receivable', 'monthly')"
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_agreement with a null obligation_type`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence) VALUES ('Test', NULL, 'monthly')"
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_agreement with a null cadence`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence) VALUES ('Test', 'receivable', NULL)"
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_agreement with source_account_id 9999`` (ctx: ScenarioContext) =
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, source_account_id) VALUES ('Test', 'receivable', 'monthly', @sa)"
                (fun cmd -> cmd.Parameters.AddWithValue("@sa", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_agreement with dest_account_id 9999`` (ctx: ScenarioContext) =
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, dest_account_id) VALUES ('Test', 'receivable', 'monthly', @da)"
                (fun cmd -> cmd.Parameters.AddWithValue("@da", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert a valid obligation_agreement with a null amount`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, amount) VALUES ('Test', 'receivable', 'monthly', NULL)"
    { ctx with LastException = ex }

// =====================================================================
// obligation_instance
// =====================================================================

let [<When>] ``I insert into obligation_instance with a null obligation_agreement_id`` (ctx: ScenarioContext) =
    let ex = tryInsert ctx "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (NULL, 'Test', 'expected', '2026-04-01')"
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_instance with obligation_agreement_id 9999`` (ctx: ScenarioContext) =
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (@oa, 'Test', 'expected', '2026-04-01')"
                (fun cmd -> cmd.Parameters.AddWithValue("@oa", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_instance with a null name`` (ctx: ScenarioContext) =
    let oaId = insertObligationAgreement ctx "TestAgreement"
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (@oa, NULL, 'expected', '2026-04-01')"
                (fun cmd -> cmd.Parameters.AddWithValue("@oa", oaId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_instance with a null status`` (ctx: ScenarioContext) =
    let oaId = insertObligationAgreement ctx "TestAgreement"
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (@oa, 'Test', NULL, '2026-04-01')"
                (fun cmd -> cmd.Parameters.AddWithValue("@oa", oaId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_instance with a null expected_date`` (ctx: ScenarioContext) =
    let oaId = insertObligationAgreement ctx "TestAgreement"
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (@oa, 'Test', 'expected', NULL)"
                (fun cmd -> cmd.Parameters.AddWithValue("@oa", oaId) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert into obligation_instance with journal_entry_id 9999`` (ctx: ScenarioContext) =
    let oaId = insertObligationAgreement ctx "TestAgreement"
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date, journal_entry_id) VALUES (@oa, 'Test', 'expected', '2026-04-01', @je)"
                (fun cmd ->
                    cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
                    cmd.Parameters.AddWithValue("@je", 9999) |> ignore)
    { ctx with LastException = ex }

let [<When>] ``I insert a valid obligation_instance with null journal_entry_id`` (ctx: ScenarioContext) =
    let oaId = insertObligationAgreement ctx "TestAgreement"
    let ex = tryExec ctx
                "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date, journal_entry_id) VALUES (@oa, 'Test', 'expected', '2026-04-01', NULL)"
                (fun cmd -> cmd.Parameters.AddWithValue("@oa", oaId) |> ignore)
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

let [<Given>] ``an invoice for tenant "Jeffrey" and fiscal_period "2026-03" exists`` () =
    let ctx = openContext ()
    let fpId = getValidFiscalPeriodId ctx
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Jeffrey', @fp, 1000.00, 200.00, 1200.00)",
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

let [<When>] ``I insert another invoice for tenant "Jeffrey" and fiscal_period "2026-03"`` (ctx: ScenarioContext) =
    let fpId = getValidFiscalPeriodId ctx
    let ex = tryExec ctx
                "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) VALUES ('Jeffrey', @fp, 500.00, 100.00, 600.00)"
                (fun cmd -> cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
    { ctx with LastException = ex }
