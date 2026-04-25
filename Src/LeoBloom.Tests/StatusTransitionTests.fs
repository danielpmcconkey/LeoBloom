module LeoBloom.Tests.StatusTransitionTests

open System
open Xunit
open LeoBloom.Domain.Ops
open LeoBloom.Utilities
open LeoBloom.Ops
open LeoBloom.Tests.TestHelpers

/// Helper: create a minimal agreement and instance in a given status, return (agreementId, instanceId)
let private setupInstance
    (txn: Npgsql.NpgsqlTransaction)
    (prefix: string) (status: string) (amount: decimal option) (notes: string option) (isActive: bool) =
    let agreementId =
        InsertHelpers.insertObligationAgreementForSpawn
            txn $"{prefix}_agr" "receivable" "monthly" (Some 1) None true
    let instanceId =
        InsertHelpers.insertObligationInstanceFull
            txn agreementId $"{prefix}_inst" status
            (DateOnly(2026, 4, 1)) amount notes isActive
    (agreementId, instanceId)

// =====================================================================
// Happy Path: Forward Transitions (FT-ST-001 through FT-ST-009)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ST-001")>]
let ``transition from expected to in_flight succeeds`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" (Some 500m) None true
    let cmd =
        { instanceId = instanceId; targetStatus = InFlight
          amount = None; confirmedDate = None; journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(InFlight, updated.status)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-002")>]
let ``transition from expected to confirmed with amount and date`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" (Some 500m) None true
    let cmd =
        { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
          amount = None; confirmedDate = Some (DateOnly(2026, 4, 1))
          journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(InstanceStatus.Confirmed, updated.status)
        Assert.Equal(Some (DateOnly(2026, 4, 1)), updated.confirmedDate)
        Assert.Equal(Some 500m, updated.amount)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-003")>]
let ``transition from expected to confirmed providing amount in command`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" None None true
    let cmd =
        { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
          amount = Some 750m; confirmedDate = Some (DateOnly(2026, 4, 1))
          journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(InstanceStatus.Confirmed, updated.status)
        Assert.Equal(Some 750m, updated.amount)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-004")>]
let ``transition from in_flight to confirmed`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "in_flight" (Some 300m) None true
    let cmd =
        { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
          amount = None; confirmedDate = Some (DateOnly(2026, 4, 5))
          journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(InstanceStatus.Confirmed, updated.status)
        Assert.Equal(Some (DateOnly(2026, 4, 5)), updated.confirmedDate)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-005")>]
let ``transition from expected to overdue`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" None None true
    let cmd =
        { instanceId = instanceId; targetStatus = Overdue
          amount = None; confirmedDate = None; journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(Overdue, updated.status)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-006")>]
let ``transition from in_flight to overdue`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "in_flight" None None true
    let cmd =
        { instanceId = instanceId; targetStatus = Overdue
          amount = None; confirmedDate = None; journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(Overdue, updated.status)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-007")>]
let ``transition from overdue to confirmed`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "overdue" (Some 500m) None true
    let cmd =
        { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
          amount = None; confirmedDate = Some (DateOnly(2026, 4, 10))
          journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(InstanceStatus.Confirmed, updated.status)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-008")>]
let ``transition from confirmed to posted with journal entry`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    // Need a fiscal period + journal entry for the posted transition
    let fpId = InsertHelpers.insertFiscalPeriod txn
                   $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
    let jeId = InsertHelpers.insertJournalEntry txn
                   (DateOnly(2026, 4, 1)) $"{prefix}_je" fpId
    let (_, instanceId) = setupInstance txn prefix "confirmed" (Some 500m) None true
    let cmd =
        { instanceId = instanceId; targetStatus = Posted
          amount = None; confirmedDate = None
          journalEntryId = Some jeId; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(Posted, updated.status)
        Assert.Equal(Some jeId, updated.journalEntryId)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-009")>]
let ``transition from expected to skipped with notes`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" None None true
    let cmd =
        { instanceId = instanceId; targetStatus = Skipped
          amount = None; confirmedDate = None; journalEntryId = None
          notes = Some "Tenant on vacation, waived" }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(Skipped, updated.status)
        Assert.True(updated.notes.IsSome)
        Assert.Contains("Tenant on vacation", updated.notes.Value)
        Assert.False(updated.isActive, "Expected isActive = false after skipped transition")
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// Amount Handling (FT-ST-010, FT-ST-011)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ST-010")>]
let ``confirmed transition updates amount when provided even if already set`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" (Some 500m) None true
    let cmd =
        { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
          amount = Some 525m; confirmedDate = Some (DateOnly(2026, 4, 1))
          journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(Some 525m, updated.amount)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-011")>]
let ``confirmed transition fails when no amount on instance or in command`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" None None true
    let cmd =
        { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
          amount = None; confirmedDate = Some (DateOnly(2026, 4, 1))
          journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for missing amount")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("amount")),
                    sprintf "Expected error containing 'amount': %A" errs)

// =====================================================================
// Invalid Transitions (FT-ST-012 through FT-ST-015)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ST-012")>]
let ``transition from confirmed to expected is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "confirmed" (Some 500m) None true
    let cmd =
        { instanceId = instanceId; targetStatus = Expected
          amount = None; confirmedDate = None; journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for invalid transition")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("invalid transition")),
                    sprintf "Expected error containing 'invalid transition': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-013")>]
let ``transition from posted to confirmed is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "posted" (Some 500m) None true
    let cmd =
        { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
          amount = None; confirmedDate = Some (DateOnly(2026, 4, 1))
          journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for invalid transition")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("invalid transition")),
                    sprintf "Expected error containing 'invalid transition': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-014")>]
let ``transition from skipped to confirmed is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "skipped" None (Some "skipped reason") true
    let cmd =
        { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
          amount = Some 500m; confirmedDate = Some (DateOnly(2026, 4, 1))
          journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for invalid transition")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("invalid transition")),
                    sprintf "Expected error containing 'invalid transition': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-015")>]
let ``transition from overdue to in_flight is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "overdue" None None true
    let cmd =
        { instanceId = instanceId; targetStatus = InFlight
          amount = None; confirmedDate = None; journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for invalid transition")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("invalid transition")),
                    sprintf "Expected error containing 'invalid transition': %A" errs)

// =====================================================================
// Complete Invalid Transition Coverage (FT-ST-023, REM-002)
// =====================================================================

[<Theory>]
[<Trait("GherkinId", "FT-ST-023")>]
[<InlineData("expected", "posted")>]
[<InlineData("in_flight", "expected")>]
[<InlineData("in_flight", "posted")>]
[<InlineData("in_flight", "skipped")>]
[<InlineData("overdue", "expected")>]
[<InlineData("overdue", "posted")>]
[<InlineData("overdue", "skipped")>]
[<InlineData("confirmed", "in_flight")>]
[<InlineData("confirmed", "overdue")>]
[<InlineData("confirmed", "skipped")>]
[<InlineData("posted", "expected")>]
[<InlineData("posted", "in_flight")>]
[<InlineData("posted", "overdue")>]
[<InlineData("posted", "skipped")>]
[<InlineData("skipped", "expected")>]
[<InlineData("skipped", "in_flight")>]
[<InlineData("skipped", "overdue")>]
[<InlineData("skipped", "posted")>]
let ``invalid transition from status to status is rejected`` (fromStatus: string) (toStatus: string) =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    // posted/skipped/confirmed require specific setup data
    let notes = if fromStatus = "skipped" then Some "skipped reason" else None
    let amount = if fromStatus = "confirmed" || fromStatus = "posted" then Some 500m else None
    let (_, instanceId) = setupInstance txn prefix fromStatus amount notes true
    let targetStatus =
        match InstanceStatus.fromString toStatus with
        | Ok s -> s
        | Error e -> failwith e
    // Supply required guard fields so command validation passes and we hit
    // the transition validity check (the thing we're actually testing)
    let journalEntryId = if toStatus = "posted" then Some 999999 else None
    let confirmedDate = if toStatus = "confirmed" then Some (DateOnly(2026, 1, 1)) else None
    let notes = if toStatus = "skipped" then Some "test notes" else None
    let cmd =
        { instanceId = instanceId; targetStatus = targetStatus
          amount = None; confirmedDate = confirmedDate
          journalEntryId = journalEntryId; notes = notes }
    match ObligationInstanceService.transition txn cmd with
    | Ok _ -> Assert.Fail(sprintf "Expected Error for invalid transition %s -> %s" fromStatus toStatus)
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("invalid transition")),
                    sprintf "Expected error containing 'invalid transition': %A" errs)

// =====================================================================
// Guard Violations (FT-ST-016 through FT-ST-019)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ST-016")>]
let ``transition to posted without journal entry ID fails`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "confirmed" (Some 500m) None true
    let cmd =
        { instanceId = instanceId; targetStatus = Posted
          amount = None; confirmedDate = None; journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for missing journal_entry_id")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("journal_entry_id")),
                    sprintf "Expected error containing 'journal_entry_id': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-017")>]
let ``transition to posted with nonexistent journal entry fails`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "confirmed" (Some 500m) None true
    let cmd =
        { instanceId = instanceId; targetStatus = Posted
          amount = None; confirmedDate = None; journalEntryId = Some 999999; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent journal entry")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                    sprintf "Expected error containing 'does not exist': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-018")>]
let ``transition to skipped without notes fails when instance has no notes`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" None None true
    let cmd =
        { instanceId = instanceId; targetStatus = Skipped
          amount = None; confirmedDate = None; journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for missing notes")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("notes")),
                    sprintf "Expected error containing 'notes': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-019")>]
let ``transition to confirmed without confirmedDate fails`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" (Some 500m) None true
    let cmd =
        { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
          amount = None; confirmedDate = None; journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for missing confirmedDate")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("confirmed_date")),
                    sprintf "Expected error containing 'confirmed_date': %A" errs)

// =====================================================================
// Edge Cases (FT-ST-020 through FT-ST-022)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ST-020")>]
let ``transition on inactive instance fails`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" None None false
    let cmd =
        { instanceId = instanceId; targetStatus = InFlight
          amount = None; confirmedDate = None; journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for inactive instance")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("inactive")),
                    sprintf "Expected error containing 'inactive': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-021")>]
let ``transition on nonexistent instance fails`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let cmd =
        { instanceId = 999999; targetStatus = InFlight
          amount = None; confirmedDate = None; journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent instance")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                    sprintf "Expected error containing 'does not exist': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-022")>]
let ``transition to skipped succeeds when instance already has notes`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" None (Some "existing note") true
    let cmd =
        { instanceId = instanceId; targetStatus = Skipped
          amount = None; confirmedDate = None; journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(Skipped, updated.status)
        Assert.True(updated.notes.IsSome, "Expected notes to be preserved")
        Assert.Contains("existing note", updated.notes.Value)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// Field Passthrough — regression coverage for BUG-A and BUG-B
// (FT-ST-024 through FT-ST-026)
//
// History: 2026-04-25 Saturday routine surfaced two silent-data-loss
// bugs in ObligationInstanceService.transition — the `amountToSet` and
// `notesToSet` matchers gated on target status and discarded caller
// values for any non-Confirmed / non-Skipped target. CLI accepted the
// flags, repository handled None correctly, but the service in between
// dropped them on the floor. These tests lock the contract: when the
// caller supplies a value that's semantically applicable, it gets
// persisted.
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ST-024")>]
let ``transition from expected to in_flight with amount preserves the amount`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" None None true
    let cmd =
        { instanceId = instanceId; targetStatus = InFlight
          amount = Some 62.03m; confirmedDate = None; journalEntryId = None; notes = None }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(InFlight, updated.status)
        Assert.Equal(Some 62.03m, updated.amount)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-ST-025")>]
let ``transition with notes preserves the notes on a non-skipped target`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (_, instanceId) = setupInstance txn prefix "expected" None None true
    let cmd =
        { instanceId = instanceId; targetStatus = InFlight
          amount = None; confirmedDate = None; journalEntryId = None
          notes = Some "autopay confirmed via portal" }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.Equal(InFlight, updated.status)
        Assert.True(updated.notes.IsSome, "Expected notes to be set")
        Assert.Contains("autopay confirmed via portal", updated.notes.Value)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Theory>]
[<Trait("GherkinId", "FT-ST-026")>]
[<InlineData("expected", "in_flight")>]
[<InlineData("expected", "confirmed")>]
[<InlineData("expected", "overdue")>]
[<InlineData("in_flight", "confirmed")>]
[<InlineData("in_flight", "overdue")>]
[<InlineData("overdue", "confirmed")>]
[<InlineData("confirmed", "posted")>]
let ``notes are preserved on every valid non-skipped transition`` (fromStatus: string) (toStatus: string) =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    // Pre-existing amount needed for any path leading into Confirmed/Posted
    let priorAmount =
        if fromStatus = "confirmed" || fromStatus = "posted" || toStatus = "confirmed" || toStatus = "posted"
        then Some 500m else None
    let (_, instanceId) = setupInstance txn prefix fromStatus priorAmount None true
    // Posted needs a real journal entry; confirmed needs a confirmedDate
    let journalEntryId =
        if toStatus = "posted" then
            let fpId = InsertHelpers.insertFiscalPeriod txn
                           $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
            Some (InsertHelpers.insertJournalEntry txn (DateOnly(2026, 4, 1)) $"{prefix}_je" fpId)
        else None
    let confirmedDate = if toStatus = "confirmed" then Some (DateOnly(2026, 4, 1)) else None
    let targetStatus =
        match InstanceStatus.fromString toStatus with
        | Ok s -> s
        | Error e -> failwith e
    let noteText = sprintf "note for %s" toStatus
    let cmd =
        { instanceId = instanceId; targetStatus = targetStatus
          amount = None; confirmedDate = confirmedDate
          journalEntryId = journalEntryId; notes = Some noteText }
    match ObligationInstanceService.transition txn cmd with
    | Ok updated ->
        Assert.True(updated.notes.IsSome,
                    sprintf "Expected notes preserved on %s -> %s but got None" fromStatus toStatus)
        Assert.Contains(noteText, updated.notes.Value)
    | Error errs ->
        Assert.Fail(sprintf "Expected Ok for %s -> %s but got: %A" fromStatus toStatus errs)
