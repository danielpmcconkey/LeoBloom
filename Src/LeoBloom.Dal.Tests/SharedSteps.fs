/// Shared Given/Then step definitions for all structural constraint tests.
/// Given steps handle connection lifecycle (open, begin transaction, prod guard).
/// Then steps handle assertions and cleanup (rollback + dispose in finally blocks).
/// Per-schema When steps live in LedgerStepDefinitions.fs, OpsStepDefinitions.fs,
/// and DeleteRestrictionSteps.fs.
module LeoBloom.Dal.Tests.SharedSteps

open System
open Npgsql
open TickSpec
open global.Xunit

[<assembly: CollectionBehavior(DisableTestParallelization = true)>]
do ()

// ---------------------------------------------------------------------
// Context record passed between steps via TickSpec's type-to-instance cache.
// When steps MUST return the modified record — a forgotten return silently
// breaks downstream assertions (LastException stays None).
// ---------------------------------------------------------------------

type ScenarioContext = {
    Transaction: NpgsqlTransaction
    LastException: exn option
}

// ---------------------------------------------------------------------
// Cleanup helper — called in the finally block of every Then step.
// Swallows exceptions because we're tearing down regardless.
// ---------------------------------------------------------------------

let cleanup (ctx: ScenarioContext) =
    try ctx.Transaction.Rollback() with _ -> ()
    try ctx.Transaction.Connection.Dispose() with _ -> ()

// ---------------------------------------------------------------------
// Helper to open a connection, begin a transaction, and verify we're
// connected to leobloom_dev (not prod).
// ---------------------------------------------------------------------

let openContext () =
    let connStr = LeoBloom.Dal.ConnectionString.resolve AppContext.BaseDirectory
    let conn = new NpgsqlConnection(connStr)
    conn.Open()

    let txn = conn.BeginTransaction()

    // Prod safety guard
    use dbCheck = conn.CreateCommand()
    dbCheck.CommandText <- "SELECT current_database()"
    dbCheck.Transaction <- txn
    let dbName = dbCheck.ExecuteScalar() :?> string
    if dbName <> "leobloom_dev" then
        txn.Rollback()
        conn.Dispose()
        failwith $"SAFETY: Tests connected to '{dbName}' instead of 'leobloom_dev'. Aborting."

    { Transaction = txn; LastException = None }

// ---------------------------------------------------------------------
// Helper to attempt a SQL command and capture any PostgresException.
// Returns Some exn on failure, None on success.
// ---------------------------------------------------------------------

let tryExec (ctx: ScenarioContext) (sql: string) (paramSetup: NpgsqlCommand -> unit) =
    try
        use cmd = new NpgsqlCommand(sql, ctx.Transaction.Connection, ctx.Transaction)
        paramSetup cmd
        cmd.ExecuteNonQuery() |> ignore
        None
    with
    | :? PostgresException as e -> Some (e :> exn)

let tryInsert (ctx: ScenarioContext) (sql: string) =
    tryExec ctx sql ignore

// ---------------------------------------------------------------------
// Shared data helpers — used across step definition files.
// These look up or insert prerequisite rows within the current transaction.
// ---------------------------------------------------------------------

let getValidAccountTypeId (ctx: ScenarioContext) =
    use cmd = new NpgsqlCommand("SELECT id FROM ledger.account_type LIMIT 1", ctx.Transaction.Connection, ctx.Transaction)
    cmd.ExecuteScalar() :?> int

let getValidAccountId (ctx: ScenarioContext) =
    use cmd = new NpgsqlCommand("SELECT id FROM ledger.account LIMIT 1", ctx.Transaction.Connection, ctx.Transaction)
    cmd.ExecuteScalar() :?> int

let getValidFiscalPeriodId (ctx: ScenarioContext) =
    use cmd = new NpgsqlCommand("SELECT id FROM ledger.fiscal_period LIMIT 1", ctx.Transaction.Connection, ctx.Transaction)
    cmd.ExecuteScalar() :?> int

let getValidObligationTypeId (ctx: ScenarioContext) =
    use cmd = new NpgsqlCommand("SELECT id FROM ops.obligation_type LIMIT 1", ctx.Transaction.Connection, ctx.Transaction)
    cmd.ExecuteScalar() :?> int

let getValidCadenceId (ctx: ScenarioContext) =
    use cmd = new NpgsqlCommand("SELECT id FROM ops.cadence LIMIT 1", ctx.Transaction.Connection, ctx.Transaction)
    cmd.ExecuteScalar() :?> int

let getValidObligationStatusId (ctx: ScenarioContext) =
    use cmd = new NpgsqlCommand("SELECT id FROM ops.obligation_status LIMIT 1", ctx.Transaction.Connection, ctx.Transaction)
    cmd.ExecuteScalar() :?> int

let insertAccountType (ctx: ScenarioContext) (name: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.account_type (name, normal_balance) VALUES (@n, 'debit') RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.ExecuteScalar() :?> int

let insertAccount (ctx: ScenarioContext) (code: string) (name: string) (atId: int) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.account (code, name, account_type_id) VALUES (@c, @n, @at) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@c", code) |> ignore
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.Parameters.AddWithValue("@at", atId) |> ignore
    cmd.ExecuteScalar() :?> int

let insertFiscalPeriod (ctx: ScenarioContext) (key: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date) VALUES (@k, '2099-01-01', '2099-01-31') RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@k", key) |> ignore
    cmd.ExecuteScalar() :?> int

let insertJournalEntry (ctx: ScenarioContext) (fpId: int) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id) VALUES ('2026-01-01', 'Test JE', @fp) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    cmd.ExecuteScalar() :?> int

let insertObligationType (ctx: ScenarioContext) (name: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_type (name) VALUES (@n) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.ExecuteScalar() :?> int

let insertCadence (ctx: ScenarioContext) (name: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.cadence (name) VALUES (@n) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.ExecuteScalar() :?> int

let insertPaymentMethod (ctx: ScenarioContext) (name: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.payment_method (name) VALUES (@n) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.ExecuteScalar() :?> int

let insertObligationStatus (ctx: ScenarioContext) (name: string) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_status (name) VALUES (@n) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.ExecuteScalar() :?> int

let insertObligationAgreement (ctx: ScenarioContext) (name: string) =
    let otId = getValidObligationTypeId ctx
    let cId = getValidCadenceId ctx
    use cmd = new NpgsqlCommand(
        "INSERT INTO ops.obligation_agreement (name, obligation_type_id, cadence_id) VALUES (@n, @ot, @c) RETURNING id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.Parameters.AddWithValue("@ot", otId) |> ignore
    cmd.Parameters.AddWithValue("@c", cId) |> ignore
    cmd.ExecuteScalar() :?> int

// ---------------------------------------------------------------------
// Given steps
// ---------------------------------------------------------------------

let [<Given>] ``the ledger schema exists`` () =
    openContext ()

let [<Given>] ``the ops schema exists`` () =
    openContext ()

// ---------------------------------------------------------------------
// Then steps — assertions + mandatory cleanup in finally block
// ---------------------------------------------------------------------

let [<Then>] ``the insert is rejected with a NOT NULL violation`` (ctx: ScenarioContext) =
    try
        match ctx.LastException with
        | Some (:? PostgresException as pgEx) ->
            Assert.Equal("23502", pgEx.SqlState)
        | Some ex ->
            Assert.Fail($"Expected PostgresException but got: {ex.GetType().Name}: {ex.Message}")
        | None ->
            Assert.Fail("Expected NOT NULL violation but insert succeeded")
    finally
        cleanup ctx

let [<Then>] ``the insert is rejected with a UNIQUE violation`` (ctx: ScenarioContext) =
    try
        match ctx.LastException with
        | Some (:? PostgresException as pgEx) ->
            Assert.Equal("23505", pgEx.SqlState)
        | Some ex ->
            Assert.Fail($"Expected PostgresException but got: {ex.GetType().Name}: {ex.Message}")
        | None ->
            Assert.Fail("Expected UNIQUE violation but insert succeeded")
    finally
        cleanup ctx

let [<Then>] ``the insert is rejected with a FK violation`` (ctx: ScenarioContext) =
    try
        match ctx.LastException with
        | Some (:? PostgresException as pgEx) ->
            Assert.Equal("23503", pgEx.SqlState)
        | Some ex ->
            Assert.Fail($"Expected PostgresException but got: {ex.GetType().Name}: {ex.Message}")
        | None ->
            Assert.Fail("Expected FK violation but insert succeeded")
    finally
        cleanup ctx

let [<Then>] ``the insert succeeds`` (ctx: ScenarioContext) =
    try
        match ctx.LastException with
        | None -> ()
        | Some ex -> Assert.Fail($"Expected insert to succeed but got: {ex.Message}")
    finally
        cleanup ctx

let [<Then>] ``the delete is rejected with a FK violation`` (ctx: ScenarioContext) =
    try
        match ctx.LastException with
        | Some (:? PostgresException as pgEx) ->
            Assert.Equal("23503", pgEx.SqlState)
        | Some ex ->
            Assert.Fail($"Expected PostgresException but got: {ex.GetType().Name}: {ex.Message}")
        | None ->
            Assert.Fail("Expected FK violation on delete but delete succeeded")
    finally
        cleanup ctx
