module LeoBloom.Tests.ReversingEntryTests

// =====================================================================
// Reversing Entry Tests
// Covers Gherkin scenarios in Specs/Behavioral/ReversingEntries.feature
//
// Fiscal period year reservation: 2101 (per DSWF/qe.md table — P083)
// All tests use a single NpgsqlTransaction that is rolled back on dispose.
// No explicit cleanup needed for transactional tests.
//
// Note: reverseEntry calls FiscalPeriodRepository.findOpenPeriodForDate.
// The open 2101-01 period is created in every test's setup. Tests that need
// a different target period create it explicitly. A dateOverride is always
// passed to avoid dependency on DateTime.Today.
// =====================================================================

open System
open Npgsql
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Tests.TestHelpers

/// Insert a fiscal period that is already closed (is_open=false, closed_at=now()).
let private insertClosedFiscalPeriod (txn: NpgsqlTransaction) (periodKey: string) (startDate: DateOnly) (endDate: DateOnly) : int =
    use cmd = new NpgsqlCommand(
        "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date, is_open, closed_at) \
         VALUES (@k, @s, @e, false, now()) RETURNING id",
        txn.Connection)
    cmd.Transaction <- txn
    cmd.Parameters.AddWithValue("@k", periodKey) |> ignore
    cmd.Parameters.AddWithValue("@s", startDate) |> ignore
    cmd.Parameters.AddWithValue("@e", endDate) |> ignore
    cmd.ExecuteScalar() :?> int

/// Post a journal entry directly (bypasses fiscal period open check —
/// needed when we need an entry in a period that may be closed).
let private insertJeDirectly
    (txn: NpgsqlTransaction)
    (fpId: int) (acct1: int) (acct2: int)
    (entryDate: DateOnly) (description: string) (amount: decimal) : int =
    use insertCmd = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry (entry_date, description, fiscal_period_id, source, created_at) \
         VALUES (@d, @desc, @fp, 'manual', now()) RETURNING id",
        txn.Connection)
    insertCmd.Transaction <- txn
    insertCmd.Parameters.AddWithValue("@d", entryDate) |> ignore
    insertCmd.Parameters.AddWithValue("@desc", description) |> ignore
    insertCmd.Parameters.AddWithValue("@fp", fpId) |> ignore
    let jeId = insertCmd.ExecuteScalar() :?> int
    use lineCmd1 = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) \
         VALUES (@je, @a, @amt, 'debit')",
        txn.Connection)
    lineCmd1.Transaction <- txn
    lineCmd1.Parameters.AddWithValue("@je", jeId) |> ignore
    lineCmd1.Parameters.AddWithValue("@a", acct1) |> ignore
    lineCmd1.Parameters.AddWithValue("@amt", amount) |> ignore
    lineCmd1.ExecuteNonQuery() |> ignore
    use lineCmd2 = new NpgsqlCommand(
        "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) \
         VALUES (@je, @a, @amt, 'credit')",
        txn.Connection)
    lineCmd2.Transaction <- txn
    lineCmd2.Parameters.AddWithValue("@je", jeId) |> ignore
    lineCmd2.Parameters.AddWithValue("@a", acct2) |> ignore
    lineCmd2.Parameters.AddWithValue("@amt", amount) |> ignore
    lineCmd2.ExecuteNonQuery() |> ignore
    jeId

/// Standard test setup: create account types, accounts, and an open 2101-01 period.
/// Returns (acct1: int, acct2: int, janPeriodId: int).
let private setupStandard (txn: NpgsqlTransaction) (prefix: string) : int * int * int =
    let atAsset = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let atRev   = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let acct1   = InsertHelpers.insertAccount txn (prefix + "A1") "Asset" atAsset true
    let acct2   = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" atRev true
    let janFp   = InsertHelpers.insertFiscalPeriod txn (prefix + "J1") (DateOnly(2101, 1, 1)) (DateOnly(2101, 1, 31)) true
    (acct1, acct2, janFp)

// =====================================================================
// FT-RVE-001: Reversing a JE produces a new entry with swapped lines
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RVE-001")>]
let ``reversing a JE produces a new entry with swapped debits and credits`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acct1, acct2, janFp) = setupStandard txn prefix
    let jeId = insertJeDirectly txn janFp acct1 acct2 (DateOnly(2101, 1, 10)) "January rent" 1200m
    // Use a date within the Jan period so findOpenPeriodForDate returns janFp
    let result = JournalEntryService.reverseEntry txn jeId (Some (DateOnly(2101, 1, 20)))
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected reversal to succeed: %A" errs)
    | Ok posted ->
        Assert.True(posted.entry.id > 0, "New JE should have a positive ID")
        Assert.Equal(2, List.length posted.lines)
        let line1010 = posted.lines |> List.find (fun l -> l.accountId = acct1)
        let line4010 = posted.lines |> List.find (fun l -> l.accountId = acct2)
        Assert.Equal(EntryType.Credit, line1010.entryType)
        Assert.Equal(EntryType.Debit, line4010.entryType)
        Assert.Equal(1200m, line1010.amount)
        Assert.Equal(1200m, line4010.amount)

// =====================================================================
// FT-RVE-002: Reversal description is auto-generated from the original JE
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RVE-002")>]
let ``reversal description is auto-generated from the original JE`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acct1, acct2, janFp) = setupStandard txn prefix
    let jeId = insertJeDirectly txn janFp acct1 acct2 (DateOnly(2101, 1, 10)) "January rent" 1200m
    let result = JournalEntryService.reverseEntry txn jeId (Some (DateOnly(2101, 1, 20)))
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected reversal to succeed: %A" errs)
    | Ok posted ->
        let expectedDesc = sprintf "Reversal of JE %d: January rent" jeId
        Assert.Equal(expectedDesc, posted.entry.description)

// =====================================================================
// FT-RVE-003: Reversal source is set to "reversal"
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RVE-003")>]
let ``reversal source is set to reversal`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acct1, acct2, janFp) = setupStandard txn prefix
    let jeId = insertJeDirectly txn janFp acct1 acct2 (DateOnly(2101, 1, 10)) "January rent" 1200m
    let result = JournalEntryService.reverseEntry txn jeId (Some (DateOnly(2101, 1, 20)))
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected reversal to succeed: %A" errs)
    | Ok posted ->
        Assert.Equal(Some "reversal", posted.entry.source)

// =====================================================================
// FT-RVE-004: Reversal JE has reference type "reversal" pointing to original
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RVE-004")>]
let ``reversal JE has reference type reversal pointing to original JE id`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acct1, acct2, janFp) = setupStandard txn prefix
    let jeId = insertJeDirectly txn janFp acct1 acct2 (DateOnly(2101, 1, 10)) "January rent" 1200m
    let result = JournalEntryService.reverseEntry txn jeId (Some (DateOnly(2101, 1, 20)))
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected reversal to succeed: %A" errs)
    | Ok posted ->
        Assert.Equal(1, List.length posted.references)
        let ref = posted.references.[0]
        Assert.Equal("reversal", ref.referenceType)
        Assert.Equal(string jeId, ref.referenceValue)

// =====================================================================
// FT-RVE-005: Reversal posts to the fiscal period derived from entry date
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RVE-005")>]
let ``reversal posts to the fiscal period derived from the entry date`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acct1, acct2, janFp) = setupStandard txn prefix
    let jeId = insertJeDirectly txn janFp acct1 acct2 (DateOnly(2101, 1, 10)) "January rent" 1200m
    // Pass a date within Jan 2101 — should route to janFp
    let result = JournalEntryService.reverseEntry txn jeId (Some (DateOnly(2101, 1, 20)))
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected reversal to succeed: %A" errs)
    | Ok posted ->
        Assert.Equal(janFp, posted.entry.fiscalPeriodId)

// =====================================================================
// FT-RVE-006: --date override sets the reversal entry_date and fiscal period
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RVE-006")>]
let ``date override sets entry_date and routes to open period for that date`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acct1, acct2, janFp) = setupStandard txn prefix
    // Create a February period as well
    let febFp = InsertHelpers.insertFiscalPeriod txn (prefix + "F2") (DateOnly(2101, 2, 1)) (DateOnly(2101, 2, 28)) true
    let jeId = insertJeDirectly txn janFp acct1 acct2 (DateOnly(2101, 1, 10)) "January rent" 1200m
    // Override date to Feb 1 — should route to febFp
    let result = JournalEntryService.reverseEntry txn jeId (Some (DateOnly(2101, 2, 1)))
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected reversal to succeed: %A" errs)
    | Ok posted ->
        Assert.Equal(DateOnly(2101, 2, 1), posted.entry.entryDate)
        Assert.Equal(febFp, posted.entry.fiscalPeriodId)

// =====================================================================
// FT-RVE-007: --date override to a closed period is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RVE-007")>]
let ``date override referencing a closed period is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acct1, acct2, janFp) = setupStandard txn prefix
    // Create a closed March period
    let _marchFp = insertClosedFiscalPeriod txn (prefix + "M3") (DateOnly(2101, 3, 1)) (DateOnly(2101, 3, 31))
    let jeId = insertJeDirectly txn janFp acct1 acct2 (DateOnly(2101, 1, 10)) "January rent" 1200m
    // Try to reverse with a date in the closed March period
    let result = JournalEntryService.reverseEntry txn jeId (Some (DateOnly(2101, 3, 15)))
    match result with
    | Ok _ -> Assert.Fail("Expected reversal to fail for closed period date override")
    | Error errs ->
        // findOpenPeriodForDate returns None for a closed period, so expect "No open fiscal period" error
        Assert.True(
            errs |> List.exists (fun e ->
                e.ToLowerInvariant().Contains("no open fiscal period") ||
                e.ToLowerInvariant().Contains("closed") ||
                e.ToLowerInvariant().Contains("period")),
            sprintf "Expected error about closed period: %A" errs)

// =====================================================================
// FT-RVE-008: Reversing an already-voided JE is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RVE-008")>]
let ``reversing an already-voided JE is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acct1, acct2, janFp) = setupStandard txn prefix
    // Post a proper entry via service (open period), then void it
    let postCmd : PostJournalEntryCommand =
        { entryDate = DateOnly(2101, 1, 10)
          description = "January rent"
          source = Some "manual"
          fiscalPeriodId = janFp
          lines =
            [ { accountId = acct1; amount = 500m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 500m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let jeId =
        match JournalEntryService.post txn postCmd with
        | Ok posted -> posted.entry.id
        | Error errs -> failwith (sprintf "Setup post failed: %A" errs)
    // Void it
    let voidCmd = { journalEntryId = jeId; voidReason = "Manual void" }
    match JournalEntryService.voidEntry txn voidCmd with
    | Ok _ -> ()
    | Error errs -> failwith (sprintf "Setup void failed: %A" errs)
    // Now try to reverse the voided entry
    let result = JournalEntryService.reverseEntry txn jeId (Some (DateOnly(2101, 1, 20)))
    match result with
    | Ok _ -> Assert.Fail("Expected Error: cannot reverse a voided entry")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("voided")),
            sprintf "Expected error containing 'voided': %A" errs)

// =====================================================================
// FT-RVE-009: Reversing a JE that already has a reversal is rejected
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RVE-009")>]
let ``reversing a JE that already has a reversal is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acct1, acct2, janFp) = setupStandard txn prefix
    let postCmd : PostJournalEntryCommand =
        { entryDate = DateOnly(2101, 1, 10)
          description = "January rent"
          source = Some "manual"
          fiscalPeriodId = janFp
          lines =
            [ { accountId = acct1; amount = 500m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 500m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let jeId =
        match JournalEntryService.post txn postCmd with
        | Ok posted -> posted.entry.id
        | Error errs -> failwith (sprintf "Setup post failed: %A" errs)
    // First reversal — should succeed
    match JournalEntryService.reverseEntry txn jeId (Some (DateOnly(2101, 1, 20))) with
    | Ok _ -> ()
    | Error errs -> failwith (sprintf "First reversal failed: %A" errs)
    // Second reversal — should be rejected
    let result = JournalEntryService.reverseEntry txn jeId (Some (DateOnly(2101, 1, 25)))
    match result with
    | Ok _ -> Assert.Fail("Expected Error: entry already has a reversal")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e ->
                e.ToLowerInvariant().Contains("already reversed") ||
                e.ToLowerInvariant().Contains("already has a reversal")),
            sprintf "Expected error about entry already being reversed: %A" errs)

// =====================================================================
// FT-RVE-010: Reversing a JE from an open period is valid
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-RVE-010")>]
let ``reversing a JE from an open period is valid`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acct1, acct2, janFp) = setupStandard txn prefix
    let postCmd : PostJournalEntryCommand =
        { entryDate = DateOnly(2101, 1, 10)
          description = "Open period entry"
          source = Some "manual"
          fiscalPeriodId = janFp
          lines =
            [ { accountId = acct1; amount = 300m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 300m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let jeId =
        match JournalEntryService.post txn postCmd with
        | Ok posted -> posted.entry.id
        | Error errs -> failwith (sprintf "Setup post failed: %A" errs)
    let result = JournalEntryService.reverseEntry txn jeId (Some (DateOnly(2101, 1, 20)))
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected reversal to succeed for open-period JE: %A" errs)
    | Ok posted ->
        Assert.True(posted.entry.id > 0, "New JE should have a positive ID")
        Assert.Equal(2, List.length posted.lines)
