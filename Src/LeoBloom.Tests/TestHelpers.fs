module LeoBloom.Tests.TestHelpers

open System
open Npgsql
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
// TestCleanup — tracks inserted rows and deletes in FK-safe order
// =====================================================================

module TestCleanup =
    type Tracker =
        { mutable JournalEntryIds: int list
          mutable AccountIds: int list
          mutable AccountTypeIds: int list
          mutable FiscalPeriodIds: int list
          mutable ObligationAgreementIds: int list
          Connection: NpgsqlConnection }

    let create (conn: NpgsqlConnection) =
        Log.initialize()
        { JournalEntryIds = []
          AccountIds = []
          AccountTypeIds = []
          FiscalPeriodIds = []
          ObligationAgreementIds = []
          Connection = conn }

    let trackJournalEntry id tracker =
        tracker.JournalEntryIds <- id :: tracker.JournalEntryIds

    let trackAccount id tracker =
        tracker.AccountIds <- id :: tracker.AccountIds

    let trackAccountType id tracker =
        tracker.AccountTypeIds <- id :: tracker.AccountTypeIds

    let trackFiscalPeriod id tracker =
        tracker.FiscalPeriodIds <- id :: tracker.FiscalPeriodIds

    let trackObligationAgreement id tracker =
        tracker.ObligationAgreementIds <- id :: tracker.ObligationAgreementIds

    /// Delete all tracked rows in FK-safe order.
    /// Catches exceptions per-table so one failure doesn't block the rest.
    /// Logs failures to stderr — silent swallowing is the bug this project fixes.
    let deleteAll (tracker: Tracker) =
        let tryDelete (table: string) (idColumn: string) (ids: int list) =
            if not ids.IsEmpty then
                try
                    use cmd = new NpgsqlCommand(
                        sprintf "DELETE FROM %s WHERE %s = ANY(@ids)" table idColumn,
                        tracker.Connection)
                    cmd.Parameters.AddWithValue("@ids", ids |> List.toArray) |> ignore
                    cmd.ExecuteNonQuery() |> ignore
                with ex ->
                    Log.errorExn ex "TestCleanup failed to clean {Table}" [| table :> obj |]

        let tryDeleteMultiColumn (table: string) (columns: (string * int list) list) =
            for (col, ids) in columns do
                if not ids.IsEmpty then
                    try
                        use cmd = new NpgsqlCommand(
                            sprintf "DELETE FROM %s WHERE %s = ANY(@ids)" table col,
                            tracker.Connection)
                        cmd.Parameters.AddWithValue("@ids", ids |> List.toArray) |> ignore
                        cmd.ExecuteNonQuery() |> ignore
                    with ex ->
                        Log.errorExn ex "TestCleanup failed to clean {Table}.{Column}" [| table :> obj; col :> obj |]

        // FK-safe order: children before parents
        tryDelete "ledger.journal_entry_reference" "journal_entry_id" tracker.JournalEntryIds
        tryDelete "ledger.journal_entry_line" "journal_entry_id" tracker.JournalEntryIds
        tryDeleteMultiColumn "ops.obligation_instance"
            [ "journal_entry_id", tracker.JournalEntryIds
              "obligation_agreement_id", tracker.ObligationAgreementIds ]
        tryDeleteMultiColumn "ops.transfer"
            [ "journal_entry_id", tracker.JournalEntryIds
              "from_account_id", tracker.AccountIds
              "to_account_id", tracker.AccountIds ]
        tryDelete "ops.invoice" "fiscal_period_id" tracker.FiscalPeriodIds
        tryDelete "ledger.journal_entry" "id" tracker.JournalEntryIds
        tryDeleteMultiColumn "ops.obligation_agreement"
            [ "id", tracker.ObligationAgreementIds
              "source_account_id", tracker.AccountIds
              "dest_account_id", tracker.AccountIds ]
        tryDelete "ledger.account" "id" tracker.AccountIds
        tryDelete "ledger.account_type" "id" tracker.AccountTypeIds
        tryDelete "ledger.fiscal_period" "id" tracker.FiscalPeriodIds

// =====================================================================
// Insert helpers — return IDs and register with cleanup tracker
// =====================================================================

module InsertHelpers =
    let insertAccountType (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (name: string) (normalBalance: string) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.account_type (name, normal_balance) VALUES (@n, @nb) RETURNING id",
            conn)
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@nb", normalBalance) |> ignore
        let id = cmd.ExecuteScalar() :?> int
        TestCleanup.trackAccountType id tracker
        id

    let insertAccount (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (code: string) (name: string) (accountTypeId: int) (isActive: bool) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.account (code, name, account_type_id, is_active) VALUES (@c, @n, @at, @active) RETURNING id",
            conn)
        cmd.Parameters.AddWithValue("@c", code) |> ignore
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        cmd.Parameters.AddWithValue("@at", accountTypeId) |> ignore
        cmd.Parameters.AddWithValue("@active", isActive) |> ignore
        let id = cmd.ExecuteScalar() :?> int
        TestCleanup.trackAccount id tracker
        id

    let insertFiscalPeriod (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (periodKey: string) (startDate: DateOnly) (endDate: DateOnly) (isOpen: bool) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date, is_open) VALUES (@k, @s, @e, @o) RETURNING id",
            conn)
        cmd.Parameters.AddWithValue("@k", periodKey) |> ignore
        cmd.Parameters.AddWithValue("@s", startDate) |> ignore
        cmd.Parameters.AddWithValue("@e", endDate) |> ignore
        cmd.Parameters.AddWithValue("@o", isOpen) |> ignore
        let id = cmd.ExecuteScalar() :?> int
        TestCleanup.trackFiscalPeriod id tracker
        id

    let insertJournalEntry (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (entryDate: DateOnly) (description: string) (fiscalPeriodId: int) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id) VALUES (@d, @desc, @fp) RETURNING id",
            conn)
        cmd.Parameters.AddWithValue("@d", entryDate) |> ignore
        cmd.Parameters.AddWithValue("@desc", description) |> ignore
        cmd.Parameters.AddWithValue("@fp", fiscalPeriodId) |> ignore
        let id = cmd.ExecuteScalar() :?> int
        TestCleanup.trackJournalEntry id tracker
        id

    let insertObligationAgreement (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (name: string) : int =
        use cmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence) VALUES (@n, 'receivable', 'monthly') RETURNING id",
            conn)
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        let id = cmd.ExecuteScalar() :?> int
        TestCleanup.trackObligationAgreement id tracker
        id
