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
