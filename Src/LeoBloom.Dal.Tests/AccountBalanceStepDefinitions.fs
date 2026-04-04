/// Step definitions for AccountBalance.feature — balance read path tests.
module LeoBloom.Dal.Tests.AccountBalanceStepDefinitions

open System
open Npgsql
open TickSpec
open global.Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Dal

// -----------------------------------------------------------------
// Context
// -----------------------------------------------------------------

type BalanceContext =
    { Transaction: NpgsqlTransaction
      FiscalPeriodId: int option
      Accounts: Map<string, int>
      PostedEntries: Map<string, int>  // description -> entry id
      BalanceResult: Result<AccountBalance, string> option }

let openBalanceContext () =
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
      PostedEntries = Map.empty; BalanceResult = None }

let cleanup (ctx: BalanceContext) =
    try ctx.Transaction.Rollback() with _ -> ()
    try ctx.Transaction.Connection.Close() with _ -> ()
    try ctx.Transaction.Connection.Dispose() with _ -> ()

// -----------------------------------------------------------------
// Table helpers
// -----------------------------------------------------------------

let colIndex (table: Table) (name: string) =
    table.Header |> Array.findIndex (fun h -> h = name)

let parseLines (ctx: BalanceContext) (table: Table) : PostLineCommand list =
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

let postEntry (ctx: BalanceContext) (entryDate: DateOnly) (desc: string) (lines: PostLineCommand list) =
    let fpId = ctx.FiscalPeriodId |> Option.defaultValue 0
    let cmd =
        { entryDate = entryDate; description = desc; source = Some "manual"
          fiscalPeriodId = fpId; lines = lines; references = [] }
    match JournalEntryService.postInTransaction ctx.Transaction cmd with
    | Ok posted ->
        { ctx with PostedEntries = Map.add desc posted.entry.id ctx.PostedEntries }
    | Error errs -> failwith (sprintf "Setup post failed: %A" errs)

// -----------------------------------------------------------------
// Given steps
// -----------------------------------------------------------------

let [<Given>] ``the ledger schema exists for balance queries`` () =
    openBalanceContext ()

let [<Given>] ``a balance-test open fiscal period from (.+) to (.+)`` (s: string) (e: string) (ctx: BalanceContext) =
    let fpId = insertFiscalPeriod ctx.Transaction (DateOnly.Parse s) (DateOnly.Parse e) true
    { ctx with FiscalPeriodId = Some fpId }

let [<Given>] ``a balance-test active account (\d+) of type (.+)`` (code: string) (typeName: string) (ctx: BalanceContext) =
    let acctId = insertAccount ctx.Transaction code typeName true
    { ctx with Accounts = Map.add code acctId ctx.Accounts }

let [<Given>] ``a balance-test entry dated (.+) described as "(.*)" with lines:`` (dateStr: string) (desc: string) (table: Table) (ctx: BalanceContext) =
    let lines = parseLines ctx table
    postEntry ctx (DateOnly.Parse dateStr) desc lines

let [<Given>] ``the balance-test entry "(.*)" has been voided`` (desc: string) (ctx: BalanceContext) =
    let entryId = ctx.PostedEntries |> Map.find desc
    let cmd = { journalEntryId = entryId; voidReason = "Test void" }
    match JournalEntryService.voidInTransaction ctx.Transaction cmd with
    | Ok _ -> ctx
    | Error errs -> failwith (sprintf "Setup void failed: %A" errs)

let [<Given>] ``balance-test account (\d+) is now deactivated`` (code: string) (ctx: BalanceContext) =
    let acctId = ctx.Accounts |> Map.find code
    use cmd = new NpgsqlCommand(
        "UPDATE ledger.account SET is_active = false WHERE id = @id",
        ctx.Transaction.Connection, ctx.Transaction)
    cmd.Parameters.AddWithValue("@id", acctId) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    ctx

// -----------------------------------------------------------------
// When steps
// -----------------------------------------------------------------

let [<When>] ``I query the balance of account (\d+) as of (\d{4}-\d{2}-\d{2})`` (code: string) (dateStr: string) (ctx: BalanceContext) =
    let acctId = ctx.Accounts |> Map.find code
    let result = AccountBalanceService.getBalanceByIdInTransaction ctx.Transaction acctId (DateOnly.Parse dateStr)
    { ctx with BalanceResult = Some result }

let [<When>] ``I query the balance of account ID (\d+) as of (\d{4}-\d{2}-\d{2})`` (acctId: string) (dateStr: string) (ctx: BalanceContext) =
    let result = AccountBalanceService.getBalanceByIdInTransaction ctx.Transaction (int acctId) (DateOnly.Parse dateStr)
    { ctx with BalanceResult = Some result }

let [<When>] ``I query the balance of account code "(.*)" as of (\d{4}-\d{2}-\d{2})`` (code: string) (dateStr: string) (ctx: BalanceContext) =
    let result = AccountBalanceService.getBalanceByCodeInTransaction ctx.Transaction code (DateOnly.Parse dateStr)
    { ctx with BalanceResult = Some result }

let [<When>] ``I query the balance of account (\d+) by code as of (\d{4}-\d{2}-\d{2})`` (code: string) (dateStr: string) (ctx: BalanceContext) =
    let result = AccountBalanceService.getBalanceByCodeInTransaction ctx.Transaction code (DateOnly.Parse dateStr)
    { ctx with BalanceResult = Some result }

// -----------------------------------------------------------------
// Then steps — one compound Then per scenario
// -----------------------------------------------------------------

let [<Then>] ``the balance is (.+) for a normal-debit account with code "(.*)"`` (expected: string) (code: string) (ctx: BalanceContext) =
    try
        match ctx.BalanceResult with
        | Some (Ok bal) ->
            Assert.Equal(decimal expected, bal.balance)
            Assert.Equal(NormalBalance.Debit, bal.normalBalance)
            Assert.Equal(code, bal.accountCode)
            Assert.False(String.IsNullOrEmpty bal.accountName, "accountName should not be empty")
        | Some (Error err) -> Assert.Fail(sprintf "Expected Ok but got Error: %s" err)
        | None -> Assert.Fail("No balance result")
    finally cleanup ctx

let [<Then>] ``the balance is (.+) for a normal-credit account with code "(.*)"`` (expected: string) (code: string) (ctx: BalanceContext) =
    try
        match ctx.BalanceResult with
        | Some (Ok bal) ->
            Assert.Equal(decimal expected, bal.balance)
            Assert.Equal(NormalBalance.Credit, bal.normalBalance)
            Assert.Equal(code, bal.accountCode)
        | Some (Error err) -> Assert.Fail(sprintf "Expected Ok but got Error: %s" err)
        | None -> Assert.Fail("No balance result")
    finally cleanup ctx

let [<Then>] ``the balance result is exactly (\d+\.\d+)`` (expected: string) (ctx: BalanceContext) =
    try
        match ctx.BalanceResult with
        | Some (Ok bal) ->
            Assert.Equal(decimal expected, bal.balance)
        | Some (Error err) -> Assert.Fail(sprintf "Expected Ok but got Error: %s" err)
        | None -> Assert.Fail("No balance result")
    finally cleanup ctx

let [<Then>] ``the balance result is Error containing "(.*)"`` (substring: string) (ctx: BalanceContext) =
    try
        match ctx.BalanceResult with
        | Some (Error err) ->
            Assert.Contains(substring, err, StringComparison.OrdinalIgnoreCase)
        | Some (Ok _) -> Assert.Fail("Expected Error but got Ok")
        | None -> Assert.Fail("No balance result")
    finally cleanup ctx
