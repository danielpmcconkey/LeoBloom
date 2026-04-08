module LeoBloom.Tests.TestHelpers

open System
open System.IO
open Npgsql
open Xunit
open LeoBloom.Utilities

// =====================================================================
// TestData — unique data generation for parallel-safe tests
// =====================================================================

module TestData =
    /// Generate a unique 4-char prefix for this test (e.g., "t7f3")
    /// Kept short to fit varchar constraints (code=10, period_key=7)
    let uniquePrefix () = Guid.NewGuid().ToString("N").[..3]

    /// Generate a unique account code (max 10 chars)
    let accountCode prefix = sprintf "%sAC" prefix

    /// Generate a unique account type name (max 20 chars)
    let accountTypeName prefix = sprintf "%s_type" prefix

    /// Generate a unique fiscal period key (max 7 chars)
    let periodKey prefix = sprintf "%sFP" prefix

// =====================================================================
// Insert helpers — execute within caller's transaction
// =====================================================================

module InsertHelpers =
    let insertAccountType (txn: NpgsqlTransaction) (name: string) (normalBalance: string) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.account_type (name, normal_balance) VALUES (@n, @nb) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@nb", normalBalance) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertAccount (txn: NpgsqlTransaction) (code: string) (name: string) (accountTypeId: int) (isActive: bool) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.account (code, name, account_type_id, is_active) VALUES (@c, @n, @at, @active) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@c", code) |> ignore
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@at", accountTypeId) |> ignore
        cmd.Parameters.AddWithValue("@active", isActive) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertAccountWithParent (txn: NpgsqlTransaction) (code: string) (name: string) (accountTypeId: int) (parentId: int) (isActive: bool) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.account (code, name, account_type_id, parent_id, is_active) VALUES (@c, @n, @at, @pi, @active) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@c", code) |> ignore
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@at", accountTypeId) |> ignore
        cmd.Parameters.AddWithValue("@pi", parentId) |> ignore
        cmd.Parameters.AddWithValue("@active", isActive) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertAccountWithSubType (txn: NpgsqlTransaction) (code: string) (name: string) (accountTypeId: int) (isActive: bool) (subType: string option) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.account (code, name, account_type_id, is_active, account_subtype) VALUES (@c, @n, @at, @active, @st) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@c", code) |> ignore
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@at", accountTypeId) |> ignore
        cmd.Parameters.AddWithValue("@active", isActive) |> ignore
        match subType with
        | Some s -> cmd.Parameters.AddWithValue("@st", s) |> ignore
        | None -> cmd.Parameters.AddWithValue("@st", DBNull.Value) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertAccountWithParentAndSubType (txn: NpgsqlTransaction) (code: string) (name: string) (accountTypeId: int) (parentId: int) (isActive: bool) (subType: string option) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.account (code, name, account_type_id, parent_id, is_active, account_subtype) VALUES (@c, @n, @at, @pi, @active, @st) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@c", code) |> ignore
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@at", accountTypeId) |> ignore
        cmd.Parameters.AddWithValue("@pi", parentId) |> ignore
        cmd.Parameters.AddWithValue("@active", isActive) |> ignore
        match subType with
        | Some s -> cmd.Parameters.AddWithValue("@st", s) |> ignore
        | None -> cmd.Parameters.AddWithValue("@st", DBNull.Value) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertFiscalPeriod (txn: NpgsqlTransaction) (periodKey: string) (startDate: DateOnly) (endDate: DateOnly) (isOpen: bool) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date, is_open) VALUES (@k, @s, @e, @o) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@k", periodKey) |> ignore
        cmd.Parameters.AddWithValue("@s", startDate) |> ignore
        cmd.Parameters.AddWithValue("@e", endDate) |> ignore
        cmd.Parameters.AddWithValue("@o", isOpen) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertJournalEntry (txn: NpgsqlTransaction) (entryDate: DateOnly) (description: string) (fiscalPeriodId: int) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id) VALUES (@d, @desc, @fp) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@d", entryDate) |> ignore
        cmd.Parameters.AddWithValue("@desc", description) |> ignore
        cmd.Parameters.AddWithValue("@fp", fiscalPeriodId) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertObligationAgreement (txn: NpgsqlTransaction) (name: string) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence) VALUES (@n, 'receivable', 'monthly') RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertObligationAgreementFull
        (txn: NpgsqlTransaction)
        (name: string) (obligationType: string) (cadence: string) (isActive: bool) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, is_active) VALUES (@n, @ot, @c, @a) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@ot", obligationType) |> ignore
        cmd.Parameters.AddWithValue("@c", cadence) |> ignore
        cmd.Parameters.AddWithValue("@a", isActive) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertObligationInstance
        (txn: NpgsqlTransaction)
        (agreementId: int) (name: string) (isActive: bool) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date, is_active) \
             VALUES (@aid, @n, 'expected', '2026-04-01', @a) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@aid", agreementId) |> ignore
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@a", isActive) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertObligationAgreementForSpawn
        (txn: NpgsqlTransaction)
        (name: string) (obligationType: string) (cadence: string)
        (expectedDay: int option) (amount: decimal option) (isActive: bool) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, expected_day, amount, is_active) \
             VALUES (@n, @ot, @c, @ed, @amt, @a) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@ot", obligationType) |> ignore
        cmd.Parameters.AddWithValue("@c", cadence) |> ignore
        match expectedDay with
        | Some d -> cmd.Parameters.AddWithValue("@ed", d) |> ignore
        | None -> cmd.Parameters.AddWithValue("@ed", DBNull.Value) |> ignore
        match amount with
        | Some a -> cmd.Parameters.AddWithValue("@amt", a) |> ignore
        | None -> cmd.Parameters.AddWithValue("@amt", DBNull.Value) |> ignore
        cmd.Parameters.AddWithValue("@a", isActive) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertObligationInstanceWithDate
        (txn: NpgsqlTransaction)
        (agreementId: int) (name: string) (expectedDate: DateOnly) (isActive: bool) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date, is_active) \
             VALUES (@aid, @n, 'expected', @ed, @a) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@aid", agreementId) |> ignore
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@ed", expectedDate) |> ignore
        cmd.Parameters.AddWithValue("@a", isActive) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertObligationInstanceFull
        (txn: NpgsqlTransaction)
        (agreementId: int) (name: string) (status: string) (expectedDate: DateOnly)
        (amount: decimal option) (notes: string option) (isActive: bool) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_instance \
             (obligation_agreement_id, name, status, expected_date, amount, notes, is_active) \
             VALUES (@aid, @n, @status, @ed, @amt, @notes, @a) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@aid", agreementId) |> ignore
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@status", status) |> ignore
        cmd.Parameters.AddWithValue("@ed", expectedDate) |> ignore
        match amount with
        | Some a -> cmd.Parameters.AddWithValue("@amt", a) |> ignore
        | None -> cmd.Parameters.AddWithValue("@amt", DBNull.Value) |> ignore
        match notes with
        | Some n -> cmd.Parameters.AddWithValue("@notes", n) |> ignore
        | None -> cmd.Parameters.AddWithValue("@notes", DBNull.Value) |> ignore
        cmd.Parameters.AddWithValue("@a", isActive) |> ignore
        cmd.ExecuteScalar() :?> int

    let insertInvoice
        (txn: NpgsqlTransaction)
        (tenant: string) (fiscalPeriodId: int)
        (rent: decimal) (utility: decimal) (total: decimal) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount, generated_at) \
             VALUES (@t, @fp, @r, @u, @tot, @ga) RETURNING id",
            txn.Connection)
        cmd.Transaction <- txn
        cmd.Parameters.AddWithValue("@t", tenant) |> ignore
        cmd.Parameters.AddWithValue("@fp", fiscalPeriodId) |> ignore
        cmd.Parameters.AddWithValue("@r", rent) |> ignore
        cmd.Parameters.AddWithValue("@u", utility) |> ignore
        cmd.Parameters.AddWithValue("@tot", total) |> ignore
        cmd.Parameters.AddWithValue("@ga", DateTimeOffset.UtcNow) |> ignore
        cmd.ExecuteScalar() :?> int

// =====================================================================
// ConstraintAssert — shared SQL constraint test helpers
// =====================================================================

module ConstraintAssert =
    /// Attempt SQL execution, return PostgresException or None.
    let tryExec (conn: NpgsqlConnection) (sql: string) (paramSetup: NpgsqlCommand -> unit) =
        try
            use cmd = new NpgsqlCommand(sql, conn)
            paramSetup cmd
            cmd.ExecuteNonQuery() |> ignore
            None
        with :? PostgresException as e -> Some e

    /// Convenience: tryExec with no parameters.
    let tryInsert conn sql = tryExec conn sql ignore

    /// Assert that the exception has the expected SqlState code.
    let assertSqlState (expected: string) (ex: PostgresException option) (failMsg: string) =
        match ex with
        | Some pgEx -> Assert.Equal(expected, pgEx.SqlState)
        | None -> Assert.Fail(failMsg)

    let assertNotNull = assertSqlState "23502"
    let assertUnique = assertSqlState "23505"
    let assertFk = assertSqlState "23503"

    /// Attempt SQL execution within a transaction using a savepoint so that a
    /// constraint violation does not abort the outer transaction.
    let tryExecTxn (txn: NpgsqlTransaction) (sql: string) (paramSetup: NpgsqlCommand -> unit) =
        let savepointName = "constraint_check"
        txn.Save(savepointName)
        try
            use cmd = new NpgsqlCommand(sql, txn.Connection)
            cmd.Transaction <- txn
            paramSetup cmd
            cmd.ExecuteNonQuery() |> ignore
            None
        with
        | :? PostgresException as e ->
            txn.Rollback(savepointName)
            Some e
        | _ ->
            txn.Rollback(savepointName)
            reraise()

    /// Convenience: tryExecTxn with no parameters.
    let tryInsertTxn txn sql = tryExecTxn txn sql ignore

    /// Assert that the operation succeeded (no exception).
    let assertSuccess (ex: PostgresException option) =
        match ex with
        | None -> ()
        | Some pgEx -> Assert.Fail($"Expected success but got: {pgEx.Message}")

// =====================================================================
// RepoPath — derive repo root from caller source file path
// =====================================================================

module RepoPath =
    /// Derive repo root from this source file's compile-time directory,
    /// walking up until a directory containing LeoBloom.sln is found.
    let repoRoot =
        let rec walkUp (dir: string) =
            if File.Exists(Path.Combine(dir, "LeoBloom.sln")) then dir
            else
                let parent = Directory.GetParent(dir)
                if parent = null then failwith "Could not find repo root from source directory"
                walkUp parent.FullName
        walkUp __SOURCE_DIRECTORY__

    /// Convenience: the Src directory under repo root.
    let srcDir = Path.Combine(repoRoot, "Src")
