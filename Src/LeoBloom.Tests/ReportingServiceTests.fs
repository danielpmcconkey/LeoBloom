module LeoBloom.Tests.ReportingServiceTests

open System
open Xunit
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Reporting
open LeoBloom.Reporting.ReportingTypes
open LeoBloom.Reporting.ScheduleEMapping
open LeoBloom.Tests.TestHelpers

// Standard account type IDs (seeded in the database)
let private revenueTypeId = 4
let private expenseTypeId = 5
let private assetTypeId = 1

/// Post a balanced journal entry and track for cleanup.
let private postEntry conn tracker acct1 acct2 fpId (entryDate: DateOnly) (desc: string) (amount: decimal) =
    let cmd =
        { entryDate = entryDate
          description = desc
          source = Some "test"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = amount; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = amount; entryType = EntryType.Credit; memo = None } ]
          references = [] }
    match JournalEntryService.post cmd with
    | Ok posted ->
        TestCleanup.trackJournalEntry posted.entry.id tracker
        posted.entry.id
    | Error errs -> failwith (sprintf "Setup post failed: %A" errs)

/// Create a full reporting test environment with Schedule E accounts.
/// Returns (tracker, conn, fpId, account map).
/// Account map keys: "4110", "4120", "5110", "5140", "5150", "5160", "5170", "5180", "5120", "5130", "5200", "5210", "1110"
type ReportingEnv =
    { Tracker: TestCleanup.Tracker
      Connection: NpgsqlConnection
      FiscalPeriodId: int
      Accounts: Map<string, int> }

/// Look up a seed account's ID by code.
let private seedAccountId (conn: NpgsqlConnection) (code: string) : int =
    use cmd = new NpgsqlCommand("SELECT id FROM ledger.account WHERE code = @c", conn)
    cmd.Parameters.AddWithValue("@c", code) |> ignore
    cmd.ExecuteScalar() :?> int

let private createReportingEnv () =
    let conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    let prefix = TestData.uniquePrefix()

    // Create fiscal period covering 2026
    let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31)) true

    // Use seed accounts — ScheduleE mapping requires these exact codes.
    // Seed data is populated by migrations; tests only create journal entries (tracked for cleanup).
    let accounts =
        Map.ofList
            [ "4110", seedAccountId conn "4110"
              "4120", seedAccountId conn "4120"
              "5110", seedAccountId conn "5110"
              "5140", seedAccountId conn "5140"
              "5150", seedAccountId conn "5150"
              "5160", seedAccountId conn "5160"
              "5170", seedAccountId conn "5170"
              "5180", seedAccountId conn "5180"
              "5120", seedAccountId conn "5120"
              "5130", seedAccountId conn "5130"
              "5200", seedAccountId conn "5200"
              "5210", seedAccountId conn "5210"
              "1110", seedAccountId conn "1110" ]

    { Tracker = tracker
      Connection = conn
      FiscalPeriodId = fpId
      Accounts = accounts }

let private cleanupEnv (env: ReportingEnv) =
    TestCleanup.deleteAll env.Tracker
    env.Connection.Dispose()

// =====================================================================
// Schedule E Mapping — pure logic tests (no DB needed)
// =====================================================================

[<Fact>]
let ``scheduleELineMappings covers all expected IRS lines`` () =
    let lineNumbers = scheduleELineMappings |> List.map (fun m -> m.lineNumber) |> Set.ofList
    Assert.Contains(3, lineNumbers)   // Rents received
    Assert.Contains(9, lineNumbers)   // Insurance
    Assert.Contains(12, lineNumbers)  // Mortgage interest
    Assert.Contains(14, lineNumbers)  // Repairs
    Assert.Contains(16, lineNumbers)  // Taxes
    Assert.Contains(18, lineNumbers)  // Depreciation
    Assert.Contains(19, lineNumbers)  // Other

[<Fact>]
let ``allMappedAccountCodes contains all revenue and expense codes from mapping`` () =
    let allCodes = Set.ofList allMappedAccountCodes
    // Revenue
    Assert.Contains("4110", allCodes)
    Assert.Contains("4120", allCodes)
    // Expenses
    Assert.Contains("5110", allCodes)
    Assert.Contains("5150", allCodes)
    Assert.Contains("5170", allCodes)
    Assert.Contains("5140", allCodes)
    Assert.Contains("5160", allCodes)
    Assert.Contains("5120", allCodes)
    Assert.Contains("5130", allCodes)
    Assert.Contains("5200", allCodes)
    Assert.Contains("5210", allCodes)
    Assert.Contains("5180", allCodes)

[<Fact>]
let ``line19SubDetail maps expected account codes to descriptions`` () =
    Assert.Equal("HOA Dues", Map.find "5160" line19SubDetail)
    Assert.Equal("Water & Electric", Map.find "5120" line19SubDetail)
    Assert.Equal("Gas", Map.find "5130" line19SubDetail)
    Assert.Equal("Lawn Care", Map.find "5200" line19SubDetail)
    Assert.Equal("Pest Control", Map.find "5210" line19SubDetail)
    Assert.Equal("Supplies", Map.find "5180" line19SubDetail)

// =====================================================================
// Schedule E Service — behavioral tests
// =====================================================================

[<Fact>]
let ``ScheduleE generate with valid year and activity returns correct report`` () =
    let env = createReportingEnv()
    try
        // Post rental income: debit cash, credit revenue
        postEntry env.Connection env.Tracker env.Accounts.["1110"] env.Accounts.["4110"] env.FiscalPeriodId
            (DateOnly(2026, 3, 1)) "March rent Tenant A" 2000m |> ignore

        // Post mortgage expense: debit expense, credit cash
        postEntry env.Connection env.Tracker env.Accounts.["5110"] env.Accounts.["1110"] env.FiscalPeriodId
            (DateOnly(2026, 3, 5)) "March mortgage" 1200m |> ignore

        // Post HOA dues (line 19 sub-detail): debit expense, credit cash
        postEntry env.Connection env.Tracker env.Accounts.["5160"] env.Accounts.["1110"] env.FiscalPeriodId
            (DateOnly(2026, 3, 10)) "March HOA" 250m |> ignore

        let result = ScheduleEService.generate 2026

        match result with
        | Ok report ->
            Assert.Equal(2026, report.year)

            // Line 3: Rents received
            let line3 = report.lineItems |> List.find (fun l -> l.lineNumber = 3)
            Assert.Equal(2000m, line3.amount)
            Assert.Equal("Rents received", line3.description)

            // Line 12: Mortgage interest
            let line12 = report.lineItems |> List.find (fun l -> l.lineNumber = 12)
            Assert.Equal(1200m, line12.amount)

            // Line 18: Depreciation reads from account 5190 balance (no entries posted = 0)
            let line18 = report.lineItems |> List.find (fun l -> l.lineNumber = 18)
            Assert.Equal(0m, line18.amount)

            // Line 19: Other with sub-detail
            let line19 = report.lineItems |> List.find (fun l -> l.lineNumber = 19)
            Assert.Equal(250m, line19.amount)
            Assert.True(line19.subDetail.Length > 0, "Line 19 should have sub-detail")
            let hoaDetail = line19.subDetail |> List.tryFind (fun (desc, _) -> desc = "HOA Dues")
            Assert.True(hoaDetail.IsSome, "Expected HOA Dues in sub-detail")
            Assert.Equal(250m, snd hoaDetail.Value)

            // Net rental income
            let expectedExpenses = 1200m + 250m
            Assert.Equal(2000m - expectedExpenses, report.netRentalIncome)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

[<Fact>]
let ``ScheduleE generate with no activity returns zeroed report`` () =
    let env = createReportingEnv()
    try
        let result = ScheduleEService.generate 2026
        match result with
        | Ok report ->
            Assert.Equal(2026, report.year)
            Assert.True(report.lineItems.Length > 0, "Should still have line items even with no activity")
            for item in report.lineItems do
                Assert.Equal(0m, item.amount)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

[<Fact>]
let ``ScheduleE generate line 19 sub-detail includes multiple Other categories`` () =
    let env = createReportingEnv()
    try
        // Post multiple line 19 expenses
        postEntry env.Connection env.Tracker env.Accounts.["5160"] env.Accounts.["1110"] env.FiscalPeriodId
            (DateOnly(2026, 4, 1)) "April HOA" 300m |> ignore
        postEntry env.Connection env.Tracker env.Accounts.["5120"] env.Accounts.["1110"] env.FiscalPeriodId
            (DateOnly(2026, 4, 5)) "April utilities" 150m |> ignore
        postEntry env.Connection env.Tracker env.Accounts.["5200"] env.Accounts.["1110"] env.FiscalPeriodId
            (DateOnly(2026, 4, 10)) "April lawn" 75m |> ignore

        let result = ScheduleEService.generate 2026
        match result with
        | Ok report ->
            let line19 = report.lineItems |> List.find (fun l -> l.lineNumber = 19)
            Assert.Equal(525m, line19.amount)
            Assert.Equal(3, line19.subDetail.Length)
            let descriptions = line19.subDetail |> List.map fst |> Set.ofList
            Assert.Contains("HOA Dues", descriptions)
            Assert.Contains("Water & Electric", descriptions)
            Assert.Contains("Lawn Care", descriptions)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

[<Fact>]
let ``ScheduleE generate rejects negative year`` () =
    let result = ScheduleEService.generate -1
    match result with
    | Error errs ->
        Assert.True(errs.Length > 0)
        Assert.True(errs |> List.exists (fun e -> e.Contains("positive")))
    | Ok _ -> Assert.Fail("Expected Error for negative year")

[<Fact>]
let ``ScheduleE generate rejects year before 1900`` () =
    let result = ScheduleEService.generate 1800
    match result with
    | Error errs ->
        Assert.True(errs.Length > 0)
        Assert.True(errs |> List.exists (fun e -> e.Contains("unreasonably early")))
    | Ok _ -> Assert.Fail("Expected Error for unreasonably early year")

[<Fact>]
let ``ScheduleE generate rejects year after 2100`` () =
    let result = ScheduleEService.generate 2200
    match result with
    | Error errs ->
        Assert.True(errs.Length > 0)
        Assert.True(errs |> List.exists (fun e -> e.Contains("unreasonably late")))
    | Ok _ -> Assert.Fail("Expected Error for unreasonably late year")

[<Fact>]
let ``ScheduleE generate excludes voided entries from balances`` () =
    let env = createReportingEnv()
    try
        // Post two rental income entries
        postEntry env.Connection env.Tracker env.Accounts.["1110"] env.Accounts.["4110"] env.FiscalPeriodId
            (DateOnly(2026, 5, 1)) "May rent" 1500m |> ignore
        let voidableId =
            postEntry env.Connection env.Tracker env.Accounts.["1110"] env.Accounts.["4110"] env.FiscalPeriodId
                (DateOnly(2026, 5, 15)) "Bad rent entry" 500m

        // Void the second entry
        let voidCmd = { journalEntryId = voidableId; voidReason = "Posted in error" }
        match JournalEntryService.voidEntry voidCmd with
        | Error errs -> failwith (sprintf "Void failed: %A" errs)
        | Ok _ -> ()

        let result = ScheduleEService.generate 2026
        match result with
        | Ok report ->
            let line3 = report.lineItems |> List.find (fun l -> l.lineNumber = 3)
            // Voided entry's 500m should be excluded; only 1500m remains
            Assert.Equal(1500m, line3.amount)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

[<Fact>]
let ``ScheduleE line items are ordered by line number`` () =
    let env = createReportingEnv()
    try
        let result = ScheduleEService.generate 2026
        match result with
        | Ok report ->
            let lineNumbers = report.lineItems |> List.map (fun l -> l.lineNumber)
            Assert.Equal<int list>(lineNumbers, lineNumbers |> List.sort)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

// =====================================================================
// General Ledger Report Service — behavioral tests
// =====================================================================

[<Fact>]
let ``GeneralLedger generate for valid account with activity returns entries`` () =
    let env = createReportingEnv()
    try
        // Post some activity against 1110 (cash account)
        postEntry env.Connection env.Tracker env.Accounts.["1110"] env.Accounts.["4110"] env.FiscalPeriodId
            (DateOnly(2026, 2, 1)) "Feb rent" 1800m |> ignore
        postEntry env.Connection env.Tracker env.Accounts.["5110"] env.Accounts.["1110"] env.FiscalPeriodId
            (DateOnly(2026, 2, 5)) "Feb mortgage" 1200m |> ignore

        let result = GeneralLedgerReportService.generate "1110" (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31))
        match result with
        | Ok report ->
            Assert.Equal("1110", report.accountCode)
            Assert.Equal("Bank A — Operating", report.accountName)
            Assert.True(report.entries.Length >= 2, sprintf "Expected at least 2 entries, got %d" report.entries.Length)

            // Verify running balance computation
            // First entry: debit 1800 to 1110, running balance = +1800
            let first = report.entries.[0]
            Assert.Equal(1800m, first.debitAmount)
            Assert.Equal(0m, first.creditAmount)
            Assert.Equal(1800m, first.runningBalance)

            // Second entry: credit 1200 from 1110, running balance = 1800 - 1200 = 600
            let second = report.entries.[1]
            Assert.Equal(0m, second.debitAmount)
            Assert.Equal(1200m, second.creditAmount)
            Assert.Equal(600m, second.runningBalance)

            Assert.Equal(600m, report.endingBalance)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

[<Fact>]
let ``GeneralLedger generate for account with no activity returns empty entries`` () =
    let env = createReportingEnv()
    try
        let result = GeneralLedgerReportService.generate "4120" (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31))
        match result with
        | Ok report ->
            Assert.Equal("4120", report.accountCode)
            Assert.Equal(0, report.entries.Length)
            Assert.Equal(0m, report.endingBalance)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

[<Fact>]
let ``GeneralLedger generate rejects nonexistent account code`` () =
    let result = GeneralLedgerReportService.generate "9999" (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31))
    match result with
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("does not exist")))
    | Ok _ -> Assert.Fail("Expected Error for nonexistent account")

[<Fact>]
let ``GeneralLedger generate rejects from date after to date`` () =
    let result = GeneralLedgerReportService.generate "1110" (DateOnly(2026, 12, 31)) (DateOnly(2026, 1, 1))
    match result with
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("after")))
    | Ok _ -> Assert.Fail("Expected Error for reversed date range")

[<Fact>]
let ``GeneralLedger generate rejects empty account code`` () =
    let result = GeneralLedgerReportService.generate "" (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31))
    match result with
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("required")))
    | Ok _ -> Assert.Fail("Expected Error for empty account code")

[<Fact>]
let ``GeneralLedger excludes voided entries`` () =
    let env = createReportingEnv()
    try
        postEntry env.Connection env.Tracker env.Accounts.["1110"] env.Accounts.["4110"] env.FiscalPeriodId
            (DateOnly(2026, 7, 1)) "Good entry" 1000m |> ignore
        let voidableId =
            postEntry env.Connection env.Tracker env.Accounts.["1110"] env.Accounts.["4110"] env.FiscalPeriodId
                (DateOnly(2026, 7, 15)) "Bad entry" 500m

        let voidCmd = { journalEntryId = voidableId; voidReason = "Mistake" }
        match JournalEntryService.voidEntry voidCmd with
        | Error errs -> failwith (sprintf "Void failed: %A" errs)
        | Ok _ -> ()

        let result = GeneralLedgerReportService.generate "1110" (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31))
        match result with
        | Ok report ->
            // Should only see the non-voided entry's debit side on 1110
            let entryIds = report.entries |> List.map (fun e -> e.journalEntryId) |> Set.ofList
            Assert.DoesNotContain(voidableId, entryIds)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

[<Fact>]
let ``GeneralLedger entries are ordered by date then entry ID`` () =
    let env = createReportingEnv()
    try
        // Post entries in non-chronological order
        postEntry env.Connection env.Tracker env.Accounts.["1110"] env.Accounts.["4110"] env.FiscalPeriodId
            (DateOnly(2026, 8, 15)) "Mid Aug" 500m |> ignore
        postEntry env.Connection env.Tracker env.Accounts.["1110"] env.Accounts.["4110"] env.FiscalPeriodId
            (DateOnly(2026, 8, 1)) "Early Aug" 300m |> ignore

        let result = GeneralLedgerReportService.generate "1110" (DateOnly(2026, 8, 1)) (DateOnly(2026, 8, 31))
        match result with
        | Ok report ->
            Assert.True(report.entries.Length >= 2)
            let dates = report.entries |> List.map (fun e -> e.date)
            Assert.Equal<DateOnly list>(dates, dates |> List.sort)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

// =====================================================================
// Cash Flow Report Service — behavioral tests
// =====================================================================

[<Fact>]
let ``CashReceipts returns debits to cash accounts as receipts`` () =
    let env = createReportingEnv()
    try
        // Debit to 1110 (cash in) = receipt
        postEntry env.Connection env.Tracker env.Accounts.["1110"] env.Accounts.["4110"] env.FiscalPeriodId
            (DateOnly(2026, 3, 1)) "Rent received" 2000m |> ignore

        let result = CashFlowReportService.getReceipts (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31))
        match result with
        | Ok report ->
            Assert.True(report.entries.Length >= 1, sprintf "Expected at least 1 receipt, got %d" report.entries.Length)
            let receipt = report.entries |> List.find (fun e -> e.amount = 2000m)
            Assert.Equal(2000m, receipt.amount)
            Assert.True(report.totalReceipts >= 2000m)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

[<Fact>]
let ``CashDisbursements returns credits to cash accounts as disbursements`` () =
    let env = createReportingEnv()
    try
        // Credit to 1110 (cash out) = disbursement
        // Post: debit mortgage expense, credit cash
        postEntry env.Connection env.Tracker env.Accounts.["5110"] env.Accounts.["1110"] env.FiscalPeriodId
            (DateOnly(2026, 3, 5)) "Mortgage payment" 1200m |> ignore

        let result = CashFlowReportService.getDisbursements (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31))
        match result with
        | Ok report ->
            Assert.True(report.entries.Length >= 1, sprintf "Expected at least 1 disbursement, got %d" report.entries.Length)
            let disbursement = report.entries |> List.find (fun e -> e.amount = 1200m)
            Assert.Equal(1200m, disbursement.amount)
            Assert.True(report.totalDisbursements >= 1200m)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

[<Fact>]
let ``CashReceipts includes counterparty account name`` () =
    let env = createReportingEnv()
    try
        postEntry env.Connection env.Tracker env.Accounts.["1110"] env.Accounts.["4110"] env.FiscalPeriodId
            (DateOnly(2026, 4, 1)) "April rent" 1500m |> ignore

        let result = CashFlowReportService.getReceipts (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31))
        match result with
        | Ok report ->
            let receipt = report.entries |> List.find (fun e -> e.amount = 1500m)
            Assert.False(String.IsNullOrWhiteSpace(receipt.counterpartyAccount),
                "Expected non-empty counterparty account")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

[<Fact>]
let ``CashReceipts rejects from date after to date`` () =
    let result = CashFlowReportService.getReceipts (DateOnly(2026, 12, 31)) (DateOnly(2026, 1, 1))
    match result with
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("after")))
    | Ok _ -> Assert.Fail("Expected Error for reversed date range")

[<Fact>]
let ``CashDisbursements rejects from date after to date`` () =
    let result = CashFlowReportService.getDisbursements (DateOnly(2026, 12, 31)) (DateOnly(2026, 1, 1))
    match result with
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("after")))
    | Ok _ -> Assert.Fail("Expected Error for reversed date range")

[<Fact>]
let ``CashReceipts with no activity returns empty report`` () =
    let env = createReportingEnv()
    try
        // Query a range with no activity
        let result = CashFlowReportService.getReceipts (DateOnly(2099, 1, 1)) (DateOnly(2099, 12, 31))
        match result with
        | Ok report ->
            Assert.Equal(0, report.entries.Length)
            Assert.Equal(0m, report.totalReceipts)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

[<Fact>]
let ``CashDisbursements with no activity returns empty report`` () =
    let env = createReportingEnv()
    try
        let result = CashFlowReportService.getDisbursements (DateOnly(2099, 1, 1)) (DateOnly(2099, 12, 31))
        match result with
        | Ok report ->
            Assert.Equal(0, report.entries.Length)
            Assert.Equal(0m, report.totalDisbursements)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env

[<Fact>]
let ``CashReceipts excludes voided entries`` () =
    let env = createReportingEnv()
    try
        postEntry env.Connection env.Tracker env.Accounts.["1110"] env.Accounts.["4110"] env.FiscalPeriodId
            (DateOnly(2026, 9, 1)) "Good receipt" 800m |> ignore
        let voidableId =
            postEntry env.Connection env.Tracker env.Accounts.["1110"] env.Accounts.["4110"] env.FiscalPeriodId
                (DateOnly(2026, 9, 15)) "Bad receipt" 200m

        let voidCmd = { journalEntryId = voidableId; voidReason = "Duplicate" }
        match JournalEntryService.voidEntry voidCmd with
        | Error errs -> failwith (sprintf "Void failed: %A" errs)
        | Ok _ -> ()

        let result = CashFlowReportService.getReceipts (DateOnly(2026, 9, 1)) (DateOnly(2026, 9, 30))
        match result with
        | Ok report ->
            let entryIds = report.entries |> List.map (fun e -> e.journalEntryId) |> Set.ofList
            Assert.DoesNotContain(voidableId, entryIds)
            Assert.Equal(800m, report.totalReceipts)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally cleanupEnv env
