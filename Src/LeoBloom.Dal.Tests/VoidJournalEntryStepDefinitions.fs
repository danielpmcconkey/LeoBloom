/// Step definitions for VoidJournalEntry.feature — void write path tests.
module LeoBloom.Dal.Tests.VoidJournalEntryStepDefinitions

open System
open Npgsql
open TickSpec
open global.Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Dal

// -----------------------------------------------------------------
// Context
// -----------------------------------------------------------------

type VoidContext =
    { Transaction: NpgsqlTransaction
      FiscalPeriodId: int option
      Accounts: Map<string, int>
      PostedEntryId: int option
      PendingRefs: PostReferenceCommand list
      PendingPostParams: (DateOnly * string * string) option
      FirstVoidedAt: DateTimeOffset option
      FirstModifiedAt: DateTimeOffset option
      VoidResult: Result<JournalEntry, string list> option }

let openVoidContext () =
    let connStr = ConnectionString.resolve AppContext.BaseDirectory
    let conn = new NpgsqlConnection(connStr)
    conn.Open()
    let txn = conn.BeginTransaction()
    use dbCheck = conn.CreateCommand()
    dbCheck.CommandText <- "SELECT current_database()"
    dbCheck.Transaction <- txn
    let dbName = dbCheck.ExecuteScalar() :?> string
    if dbName <> "leobloom_dev" then
        txn.Rollback(); conn.Dispose()
        failwith $"SAFETY: Connected to '{dbName}' instead of 'leobloom_dev'."
    { Transaction = txn; FiscalPeriodId = None; Accounts = Map.empty
      PostedEntryId = None; PendingRefs = []; PendingPostParams = None
      FirstVoidedAt = None; FirstModifiedAt = None; VoidResult = None }

let cleanup (ctx: VoidContext) =
    try ctx.Transaction.Rollback() with _ -> ()
    try ctx.Transaction.Connection.Close() with _ -> ()
    try ctx.Transaction.Connection.Dispose() with _ -> ()

// -----------------------------------------------------------------
// Table helpers
// -----------------------------------------------------------------

let colIndex (table: Table) (name: string) =
    table.Header |> Array.findIndex (fun h -> h = name)

let parseLines (ctx: VoidContext) (table: Table) : PostLineCommand list =
    let acctCol = colIndex table "account"
    let amtCol = colIndex table "amount"
    let etCol = colIndex table "entry_type"
    table.Rows
    |> Array.toList
    |> List.map (fun row ->
        let code = row[acctCol]
        let acctId = ctx.Accounts |> Map.find code
        let et = match row[etCol] with "debit" -> EntryType.Debit | _ -> EntryType.Credit
        { accountId = acctId
          amount = decimal row[amtCol]
          entryType = et
          memo = None })

let parseRefs (table: Table) : PostReferenceCommand list =
    let rtCol = colIndex table "reference_type"
    let rvCol = colIndex table "reference_value"
    table.Rows
    |> Array.toList
    |> List.map (fun row ->
        { referenceType = row[rtCol]
          referenceValue = row[rvCol] })

// -----------------------------------------------------------------
// DB helpers
// -----------------------------------------------------------------

let insertAccountType (txn: NpgsqlTransaction) (typeName: string) =
    let nb = match typeName with "asset" | "expense" -> "debit" | _ -> "credit"
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.account_type (name, normal_balance) VALUES (@n, @nb)
         ON CONFLICT (name) DO UPDATE SET name = EXCLUDED.name RETURNING id",
        txn.Connection, txn)
    cmd.Parameters.AddWithValue("@n", typeName) |> ignore
    cmd.Parameters.AddWithValue("@nb", nb) |> ignore
    cmd.ExecuteScalar() :?> int

let insertAccount (txn: NpgsqlTransaction) (code: string) (typeName: string) (isActive: bool) =
    let atId = insertAccountType txn typeName
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.account (code, name, account_type_id, is_active)
         VALUES (@c, @n, @at, @active)
         ON CONFLICT (code) DO UPDATE SET is_active = EXCLUDED.is_active RETURNING id",
        txn.Connection, txn)
    cmd.Parameters.AddWithValue("@c", code) |> ignore
    cmd.Parameters.AddWithValue("@n", sprintf "Test Account %s" code) |> ignore
    cmd.Parameters.AddWithValue("@at", atId) |> ignore
    cmd.Parameters.AddWithValue("@active", isActive) |> ignore
    cmd.ExecuteScalar() :?> int

let insertFiscalPeriod (txn: NpgsqlTransaction) (startDate: DateOnly) (endDate: DateOnly) (isOpen: bool) =
    let key = startDate.ToString("yyyy-MM")
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date, is_open)
         VALUES (@k, @s, @e, @o)
         ON CONFLICT (period_key) DO UPDATE SET is_open = EXCLUDED.is_open RETURNING id",
        txn.Connection, txn)
    cmd.Parameters.AddWithValue("@k", key) |> ignore
    cmd.Parameters.AddWithValue("@s", startDate) |> ignore
    cmd.Parameters.AddWithValue("@e", endDate) |> ignore
    cmd.Parameters.AddWithValue("@o", isOpen) |> ignore
    cmd.ExecuteScalar() :?> int

let postEntry (ctx: VoidContext) (entryDate: DateOnly) (desc: string) (source: string) (lines: PostLineCommand list) (refs: PostReferenceCommand list) =
    let fpId = ctx.FiscalPeriodId |> Option.defaultValue 0
    let cmd =
        { entryDate = entryDate; description = desc; source = Some source
          fiscalPeriodId = fpId; lines = lines; references = refs }
    match JournalEntryService.postInTransaction ctx.Transaction cmd with
    | Ok posted -> { ctx with PostedEntryId = Some posted.entry.id }
    | Error errs -> failwith (sprintf "Setup post failed: %A" errs)

// -----------------------------------------------------------------
// Given steps
// -----------------------------------------------------------------

let [<Given>] ``the ledger schema exists for voiding`` () =
    openVoidContext ()

let [<Given>] ``a void-test open fiscal period from (.+) to (.+)`` (s: string) (e: string) (ctx: VoidContext) =
    let fpId = insertFiscalPeriod ctx.Transaction (DateOnly.Parse s) (DateOnly.Parse e) true
    { ctx with FiscalPeriodId = Some fpId }

let [<Given>] ``a void-test active account (\d+) of type (.+)`` (code: string) (typeName: string) (ctx: VoidContext) =
    let acctId = insertAccount ctx.Transaction code typeName true
    { ctx with Accounts = Map.add code acctId ctx.Accounts }

let [<Given>] ``a posted entry dated (.+) described as "(.*)" with source "(.*)" and lines:`` (dateStr: string) (desc: string) (source: string) (table: Table) (ctx: VoidContext) =
    let lines = parseLines ctx table
    postEntry ctx (DateOnly.Parse dateStr) desc source lines []

let [<Given>] ``a posted entry with refs dated (.+) described as "(.*)" with source "(.*)" and refs:`` (dateStr: string) (desc: string) (source: string) (table: Table) (ctx: VoidContext) =
    let refs = parseRefs table
    { ctx with PendingRefs = refs; PendingPostParams = Some (DateOnly.Parse dateStr, desc, source) }

let [<Given>] ``void-test lines:`` (table: Table) (ctx: VoidContext) =
    let lines = parseLines ctx table
    match ctx.PendingPostParams with
    | Some (date, desc, source) ->
        let result = postEntry ctx date desc source lines ctx.PendingRefs
        { result with PendingRefs = []; PendingPostParams = None }
    | None -> failwith "No pending post params — 'lines:' step used without prior setup step"

let [<Given>] ``the entry has been voided with reason "(.*)"`` (reason: string) (ctx: VoidContext) =
    let entryId = ctx.PostedEntryId |> Option.get
    let cmd = { journalEntryId = entryId; voidReason = reason }
    match JournalEntryService.voidInTransaction ctx.Transaction cmd with
    | Ok entry ->
        { ctx with FirstVoidedAt = entry.voidedAt; FirstModifiedAt = Some entry.modifiedAt }
    | Error errs -> failwith (sprintf "Setup void failed: %A" errs)

let [<Given>] ``the fiscal period is now closed`` (ctx: VoidContext) =
    let fpId = ctx.FiscalPeriodId |> Option.get
    use cmd = new NpgsqlCommand(
        "UPDATE ledger.fiscal_period SET is_open = false WHERE id = @id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@id", fpId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

// -----------------------------------------------------------------
// When steps
// -----------------------------------------------------------------

let [<When>] ``I void the entry with reason "(.*)"`` (reason: string) (ctx: VoidContext) =
    let entryId = ctx.PostedEntryId |> Option.get
    let cmd = { journalEntryId = entryId; voidReason = reason }
    let result = JournalEntryService.voidInTransaction ctx.Transaction cmd
    { ctx with VoidResult = Some result }

let [<When>] ``I void entry ID (\d+) with reason "(.*)"`` (entryId: string) (reason: string) (ctx: VoidContext) =
    let cmd = { journalEntryId = int entryId; voidReason = reason }
    let result = JournalEntryService.voidInTransaction ctx.Transaction cmd
    { ctx with VoidResult = Some result }

// -----------------------------------------------------------------
// Then steps — one compound Then per scenario
// -----------------------------------------------------------------

let [<Then>] ``the void succeeds with voided_at set and void_reason "(.*)"`` (expectedReason: string) (ctx: VoidContext) =
    try
        match ctx.VoidResult with
        | Some (Ok entry) ->
            Assert.True(entry.voidedAt.IsSome, "Expected voided_at to be set")
            Assert.Equal(Some expectedReason, entry.voidReason)
            let voidedAt = entry.voidedAt.Value
            let age = DateTimeOffset.UtcNow - voidedAt
            Assert.True(age.TotalSeconds < 30.0, sprintf "voided_at is too old: %A" voidedAt)
        | Some (Error errs) -> Assert.Fail(sprintf "Expected Ok but got Error: %A" errs)
        | None -> Assert.Fail("No void result")
    finally cleanup ctx

let [<Then>] ``the void succeeds and the entry still exists in the database with description "(.*)"`` (desc: string) (ctx: VoidContext) =
    try
        match ctx.VoidResult with
        | Some (Ok entry) ->
            Assert.True(entry.voidedAt.IsSome, "Expected voided_at to be set")
            use cmd = new NpgsqlCommand(
                "SELECT description, voided_at FROM ledger.journal_entry WHERE id = @id",
                ctx.Transaction.Connection, ctx.Transaction)
            cmd.Parameters.AddWithValue("@id", entry.id) |> ignore
            use reader = cmd.ExecuteReader()
            Assert.True(reader.Read(), "Entry should exist in DB")
            Assert.Equal(desc, reader.GetString(0))
            Assert.False(reader.IsDBNull(1), "voided_at should not be null")
            reader.Close()
        | Some (Error errs) -> Assert.Fail(sprintf "Expected Ok but got Error: %A" errs)
        | None -> Assert.Fail("No void result")
    finally cleanup ctx

let [<Then>] ``the void succeeds and the entry has (\d+) lines and (\d+) reference with type "(.*)" and value "(.*)"`` (lineCount: string) (refCount: string) (refType: string) (refValue: string) (ctx: VoidContext) =
    try
        match ctx.VoidResult with
        | Some (Ok entry) ->
            Assert.True(entry.voidedAt.IsSome, "Expected voided_at to be set")
            use lineCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM ledger.journal_entry_line WHERE journal_entry_id = @id",
                ctx.Transaction.Connection, ctx.Transaction)
            lineCmd.Parameters.AddWithValue("@id", entry.id) |> ignore
            let lines = lineCmd.ExecuteScalar() :?> int64
            Assert.Equal(int64 (int lineCount), lines)
            use refCmd = new NpgsqlCommand(
                "SELECT reference_type, reference_value FROM ledger.journal_entry_reference WHERE journal_entry_id = @id",
                ctx.Transaction.Connection, ctx.Transaction)
            refCmd.Parameters.AddWithValue("@id", entry.id) |> ignore
            use reader = refCmd.ExecuteReader()
            let mutable refRows = 0
            while reader.Read() do
                Assert.Equal(refType, reader.GetString(0))
                Assert.Equal(refValue, reader.GetString(1))
                refRows <- refRows + 1
            reader.Close()
            Assert.Equal(int refCount, refRows)
        | Some (Error errs) -> Assert.Fail(sprintf "Expected Ok but got Error: %A" errs)
        | None -> Assert.Fail("No void result")
    finally cleanup ctx

let [<Then>] ``the void succeeds with the original voided_at and void_reason "(.*)" unchanged`` (expectedReason: string) (ctx: VoidContext) =
    try
        match ctx.VoidResult with
        | Some (Ok entry) ->
            Assert.True(entry.voidedAt.IsSome, "Expected voided_at to be set")
            Assert.Equal(Some expectedReason, entry.voidReason)
            Assert.Equal(ctx.FirstVoidedAt, entry.voidedAt)
            Assert.Equal(ctx.FirstModifiedAt, Some entry.modifiedAt)
        | Some (Error errs) -> Assert.Fail(sprintf "Expected Ok but got Error: %A" errs)
        | None -> Assert.Fail("No void result")
    finally cleanup ctx

let [<Then>] ``the void fails with error containing "(.*)"`` (substring: string) (ctx: VoidContext) =
    try
        match ctx.VoidResult with
        | Some (Error errs) ->
            let joined = String.Join("; ", errs)
            Assert.Contains(substring, joined, StringComparison.OrdinalIgnoreCase)
        | Some (Ok _) -> Assert.Fail("Expected Error but got Ok")
        | None -> Assert.Fail("No void result")
    finally cleanup ctx
