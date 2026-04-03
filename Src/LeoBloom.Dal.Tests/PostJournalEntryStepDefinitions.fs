/// Step definitions for PostJournalEntry.feature — the core write path tests.
module LeoBloom.Dal.Tests.PostJournalEntryStepDefinitions

open System
open Npgsql
open TickSpec
open global.Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Dal

// -----------------------------------------------------------------
// Context
// -----------------------------------------------------------------

type PostContext =
    { Transaction: NpgsqlTransaction
      FiscalPeriodId: int option
      Accounts: Map<string, int>
      PendingRefs: PostReferenceCommand list
      PostResult: Result<PostedJournalEntry, string list> option
      EntryTypeParseResult: Result<EntryType, string> option }

let openPostContext () =
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
      PendingRefs = []; PostResult = None; EntryTypeParseResult = None }

let cleanup (ctx: PostContext) =
    try ctx.Transaction.Rollback() with _ -> ()
    try ctx.Transaction.Connection.Close() with _ -> ()
    try ctx.Transaction.Connection.Dispose() with _ -> ()

// -----------------------------------------------------------------
// Table helpers
// -----------------------------------------------------------------

let colIndex (table: Table) (name: string) =
    table.Header |> Array.findIndex (fun h -> h = name)

let parseLines (ctx: PostContext) (table: Table) : PostLineCommand list =
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

let parseLinesWithMemo (ctx: PostContext) (table: Table) : PostLineCommand list =
    let acctCol = colIndex table "account"
    let amtCol = colIndex table "amount"
    let etCol = colIndex table "entry_type"
    let memoCol = colIndex table "memo"
    table.Rows
    |> Array.toList
    |> List.map (fun row ->
        let code = row[acctCol]
        let acctId = ctx.Accounts |> Map.find code
        let et = match row[etCol] with "debit" -> EntryType.Debit | _ -> EntryType.Credit
        { accountId = acctId
          amount = decimal row[amtCol]
          entryType = et
          memo = Some row[memoCol] })

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

let doPost (ctx: PostContext) (entryDate: DateOnly) (desc: string) (source: string option) (lines: PostLineCommand list) =
    let fpId = ctx.FiscalPeriodId |> Option.defaultValue 0
    let cmd =
        { entryDate = entryDate; description = desc; source = source
          fiscalPeriodId = fpId; lines = lines; references = ctx.PendingRefs }
    let result = JournalEntryService.postInTransaction ctx.Transaction cmd
    { ctx with PostResult = Some result; PendingRefs = [] }

// -----------------------------------------------------------------
// Given steps
// -----------------------------------------------------------------

let [<Given>] ``the ledger schema exists for posting`` () =
    openPostContext ()

let [<Given>] ``a valid open fiscal period from (.+) to (.+)`` (s: string) (e: string) (ctx: PostContext) =
    let fpId = insertFiscalPeriod ctx.Transaction (DateOnly.Parse s) (DateOnly.Parse e) true
    { ctx with FiscalPeriodId = Some fpId }

let [<Given>] ``a closed fiscal period from (.+) to (.+)`` (s: string) (e: string) (ctx: PostContext) =
    let fpId = insertFiscalPeriod ctx.Transaction (DateOnly.Parse s) (DateOnly.Parse e) false
    { ctx with FiscalPeriodId = Some fpId }

let [<Given>] ``an active account (\d+) of type (.+)`` (code: string) (typeName: string) (ctx: PostContext) =
    let acctId = insertAccount ctx.Transaction code typeName true
    { ctx with Accounts = Map.add code acctId ctx.Accounts }

let [<Given>] ``an inactive account (\d+) of type (.+)`` (code: string) (typeName: string) (ctx: PostContext) =
    let acctId = insertAccount ctx.Transaction code typeName false
    { ctx with Accounts = Map.add code acctId ctx.Accounts }

let [<Given>] ``an existing journal entry with reference type "(.+)" and value "(.+)"`` (refType: string) (refValue: string) (ctx: PostContext) =
    let fpId = ctx.FiscalPeriodId |> Option.get
    let acctIds = ctx.Accounts |> Map.toList |> List.map snd
    let cmd =
        { entryDate = DateOnly(2026, 3, 10)
          description = "Pre-existing entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
              [ { accountId = acctIds[0]; amount = 100m; entryType = EntryType.Debit; memo = None }
                { accountId = acctIds[1]; amount = 100m; entryType = EntryType.Credit; memo = None } ]
          references = [ { referenceType = refType; referenceValue = refValue } ] }
    JournalEntryService.postInTransaction ctx.Transaction cmd |> ignore
    ctx

let [<Given>] ``pending references:`` (table: Table) (ctx: PostContext) =
    let refs = parseRefs table
    { ctx with PendingRefs = refs }

// -----------------------------------------------------------------
// When steps
// -----------------------------------------------------------------

let [<When>] ``I post a journal entry dated (.+) described as "(.*)" with source "(.*)" and lines:`` (dateStr: string) (desc: string) (source: string) (table: Table) (ctx: PostContext) =
    let lines = parseLines ctx table
    let src = if String.IsNullOrEmpty source then Some "" else Some source
    doPost ctx (DateOnly.Parse dateStr) desc src lines

let [<When>] ``I post a journal entry dated (.+) described as "(.*)" with no source and lines:`` (dateStr: string) (desc: string) (table: Table) (ctx: PostContext) =
    let lines = parseLines ctx table
    doPost ctx (DateOnly.Parse dateStr) desc None lines

let [<When>] ``I post a journal entry dated (.+) described as "(.*)" with source "(.*)" and lines with memo:`` (dateStr: string) (desc: string) (source: string) (table: Table) (ctx: PostContext) =
    let lines = parseLinesWithMemo ctx table
    doPost ctx (DateOnly.Parse dateStr) desc (Some source) lines

let [<When>] ``I post a journal entry with nonexistent period (\d+) dated (.+) described as "(.*)" with source "(.*)" and lines:`` (periodId: string) (dateStr: string) (desc: string) (source: string) (table: Table) (ctx: PostContext) =
    let lines = parseLines ctx table
    let cmd =
        { entryDate = DateOnly.Parse dateStr
          description = desc; source = Some source
          fiscalPeriodId = int periodId; lines = lines; references = ctx.PendingRefs }
    let result = JournalEntryService.postInTransaction ctx.Transaction cmd
    { ctx with PostResult = Some result; PendingRefs = [] }

let [<When>] ``I attempt to parse entry_type "(.*)"`` (value: string) (ctx: PostContext) =
    let result : Result<EntryType, string> = EntryType.fromString value
    { ctx with EntryTypeParseResult = Some result }

// -----------------------------------------------------------------
// Then steps — each scenario has exactly ONE Then for proper cleanup
// -----------------------------------------------------------------

let [<Then>] ``the post succeeds with (\d+) lines and valid id and timestamps`` (count: string) (ctx: PostContext) =
    try
        match ctx.PostResult with
        | Some (Ok posted) ->
            Assert.True(posted.entry.id > 0, "Expected id > 0")
            Assert.True(posted.entry.createdAt > DateTimeOffset.MinValue)
            Assert.True(posted.entry.modifiedAt > DateTimeOffset.MinValue)
            Assert.Equal(int count, List.length posted.lines)
            for line in posted.lines do
                Assert.True(line.id > 0)
                Assert.True(line.journalEntryId = posted.entry.id)
                Assert.True(line.amount > 0m)
        | Some (Error errs) -> Assert.Fail(sprintf "Expected Ok but got Error: %A" errs)
        | None -> Assert.Fail("No post result")
    finally cleanup ctx

let [<Then>] ``the post succeeds with (\d+) lines`` (count: string) (ctx: PostContext) =
    try
        match ctx.PostResult with
        | Some (Ok posted) -> Assert.Equal(int count, List.length posted.lines)
        | Some (Error errs) -> Assert.Fail(sprintf "Expected Ok but got Error: %A" errs)
        | None -> Assert.Fail("No post result")
    finally cleanup ctx

let [<Then>] ``the post succeeds with (\d+) references`` (count: string) (ctx: PostContext) =
    try
        match ctx.PostResult with
        | Some (Ok posted) -> Assert.Equal(int count, List.length posted.references)
        | Some (Error errs) -> Assert.Fail(sprintf "Expected Ok but got Error: %A" errs)
        | None -> Assert.Fail("No post result")
    finally cleanup ctx

let [<Then>] ``the post succeeds with null source`` (ctx: PostContext) =
    try
        match ctx.PostResult with
        | Some (Ok posted) -> Assert.True(posted.entry.source.IsNone, "Expected source = None")
        | Some (Error errs) -> Assert.Fail(sprintf "Expected Ok but got Error: %A" errs)
        | None -> Assert.Fail("No post result")
    finally cleanup ctx

let [<Then>] ``the post succeeds with memo values on all lines`` (ctx: PostContext) =
    try
        match ctx.PostResult with
        | Some (Ok posted) ->
            for line in posted.lines do
                Assert.True(line.memo.IsSome, sprintf "Expected memo on line %d" line.id)
        | Some (Error errs) -> Assert.Fail(sprintf "Expected Ok but got Error: %A" errs)
        | None -> Assert.Fail("No post result")
    finally cleanup ctx

let [<Then>] ``the post succeeds`` (ctx: PostContext) =
    try
        match ctx.PostResult with
        | Some (Ok _) -> ()
        | Some (Error errs) -> Assert.Fail(sprintf "Expected Ok but got Error: %A" errs)
        | None -> Assert.Fail("No post result")
    finally cleanup ctx

let [<Then>] ``the post fails with error containing "(.*)"`` (substring: string) (ctx: PostContext) =
    try
        match ctx.PostResult with
        | Some (Error errs) ->
            let joined = String.Join("; ", errs)
            Assert.Contains(substring, joined, StringComparison.OrdinalIgnoreCase)
        | Some (Ok _) -> Assert.Fail("Expected Error but got Ok")
        | None -> Assert.Fail("No post result")
    finally cleanup ctx

let [<Then>] ``the post fails and no rows persisted for "(.*)"`` (desc: string) (ctx: PostContext) =
    try
        match ctx.PostResult with
        | Some (Error _) ->
            use cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM ledger.journal_entry WHERE description = @d",
                ctx.Transaction.Connection, ctx.Transaction)
            cmd.Parameters.AddWithValue("@d", desc) |> ignore
            let count = cmd.ExecuteScalar() :?> int64
            Assert.Equal(0L, count)
        | Some (Ok _) -> Assert.Fail("Expected Error but got Ok")
        | None -> Assert.Fail("No post result")
    finally cleanup ctx

let [<Then>] ``the entry_type parse result is Error`` (ctx: PostContext) =
    try
        match ctx.EntryTypeParseResult with
        | Some (Error _) -> ()
        | Some (Ok _) -> Assert.Fail("Expected Error but got Ok")
        | None -> Assert.Fail("No parse result")
    finally cleanup ctx
