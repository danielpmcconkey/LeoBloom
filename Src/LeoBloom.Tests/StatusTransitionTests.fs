module LeoBloom.Tests.StatusTransitionTests

open System
open Xunit
open LeoBloom.Domain.Ops
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

/// Helper: create a minimal agreement and instance in a given status, return (agreementId, instanceId)
let private setupInstance
    (conn: Npgsql.NpgsqlConnection) (tracker: TestCleanup.Tracker)
    (prefix: string) (status: string) (amount: decimal option) (notes: string option) (isActive: bool) =
    let agreementId =
        InsertHelpers.insertObligationAgreementForSpawn
            conn tracker $"{prefix}_agr" "receivable" "monthly" (Some 1) None true
    let instanceId =
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_inst" status
            (DateOnly(2026, 4, 1)) amount notes isActive
    (agreementId, instanceId)

// =====================================================================
// Happy Path: Forward Transitions (FT-ST-001 through FT-ST-009)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ST-001")>]
let ``transition from expected to in_flight succeeds`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "expected" (Some 500m) None true
        let cmd =
            { instanceId = instanceId; targetStatus = InFlight
              amount = None; confirmedDate = None; journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok updated ->
            Assert.Equal(InFlight, updated.status)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-002")>]
let ``transition from expected to confirmed with amount and date`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "expected" (Some 500m) None true
        let cmd =
            { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
              amount = None; confirmedDate = Some (DateOnly(2026, 4, 1))
              journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok updated ->
            Assert.Equal(InstanceStatus.Confirmed, updated.status)
            Assert.Equal(Some (DateOnly(2026, 4, 1)), updated.confirmedDate)
            Assert.Equal(Some 500m, updated.amount)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-003")>]
let ``transition from expected to confirmed providing amount in command`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "expected" None None true
        let cmd =
            { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
              amount = Some 750m; confirmedDate = Some (DateOnly(2026, 4, 1))
              journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok updated ->
            Assert.Equal(InstanceStatus.Confirmed, updated.status)
            Assert.Equal(Some 750m, updated.amount)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-004")>]
let ``transition from in_flight to confirmed`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "in_flight" (Some 300m) None true
        let cmd =
            { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
              amount = None; confirmedDate = Some (DateOnly(2026, 4, 5))
              journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok updated ->
            Assert.Equal(InstanceStatus.Confirmed, updated.status)
            Assert.Equal(Some (DateOnly(2026, 4, 5)), updated.confirmedDate)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-005")>]
let ``transition from expected to overdue`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "expected" None None true
        let cmd =
            { instanceId = instanceId; targetStatus = Overdue
              amount = None; confirmedDate = None; journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok updated ->
            Assert.Equal(Overdue, updated.status)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-006")>]
let ``transition from in_flight to overdue`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "in_flight" None None true
        let cmd =
            { instanceId = instanceId; targetStatus = Overdue
              amount = None; confirmedDate = None; journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok updated ->
            Assert.Equal(Overdue, updated.status)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-007")>]
let ``transition from overdue to confirmed`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "overdue" (Some 500m) None true
        let cmd =
            { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
              amount = None; confirmedDate = Some (DateOnly(2026, 4, 10))
              journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok updated ->
            Assert.Equal(InstanceStatus.Confirmed, updated.status)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-008")>]
let ``transition from confirmed to posted with journal entry`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        // Need a fiscal period + journal entry for the posted transition
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker
                       $"{prefix}FP" (DateOnly(2026, 4, 1)) (DateOnly(2026, 4, 30)) true
        let jeId = InsertHelpers.insertJournalEntry conn tracker
                       (DateOnly(2026, 4, 1)) $"{prefix}_je" fpId
        let (_, instanceId) = setupInstance conn tracker prefix "confirmed" (Some 500m) None true
        let cmd =
            { instanceId = instanceId; targetStatus = Posted
              amount = None; confirmedDate = None
              journalEntryId = Some jeId; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok updated ->
            Assert.Equal(Posted, updated.status)
            Assert.Equal(Some jeId, updated.journalEntryId)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-009")>]
let ``transition from expected to skipped with notes`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "expected" None None true
        let cmd =
            { instanceId = instanceId; targetStatus = Skipped
              amount = None; confirmedDate = None; journalEntryId = None
              notes = Some "Tenant on vacation, waived" }
        match ObligationInstanceService.transition cmd with
        | Ok updated ->
            Assert.Equal(Skipped, updated.status)
            Assert.True(updated.notes.IsSome)
            Assert.Contains("Tenant on vacation", updated.notes.Value)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Amount Handling (FT-ST-010, FT-ST-011)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ST-010")>]
let ``confirmed transition updates amount when provided even if already set`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "expected" (Some 500m) None true
        let cmd =
            { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
              amount = Some 525m; confirmedDate = Some (DateOnly(2026, 4, 1))
              journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok updated ->
            Assert.Equal(Some 525m, updated.amount)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-011")>]
let ``confirmed transition fails when no amount on instance or in command`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "expected" None None true
        let cmd =
            { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
              amount = None; confirmedDate = Some (DateOnly(2026, 4, 1))
              journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok _ -> Assert.Fail("Expected Error for missing amount")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("amount")),
                        sprintf "Expected error containing 'amount': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Invalid Transitions (FT-ST-012 through FT-ST-015)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ST-012")>]
let ``transition from confirmed to expected is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "confirmed" (Some 500m) None true
        let cmd =
            { instanceId = instanceId; targetStatus = Expected
              amount = None; confirmedDate = None; journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok _ -> Assert.Fail("Expected Error for invalid transition")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("invalid transition")),
                        sprintf "Expected error containing 'invalid transition': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-013")>]
let ``transition from posted to confirmed is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "posted" (Some 500m) None true
        let cmd =
            { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
              amount = None; confirmedDate = Some (DateOnly(2026, 4, 1))
              journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok _ -> Assert.Fail("Expected Error for invalid transition")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("invalid transition")),
                        sprintf "Expected error containing 'invalid transition': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-014")>]
let ``transition from skipped to confirmed is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "skipped" None (Some "skipped reason") true
        let cmd =
            { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
              amount = Some 500m; confirmedDate = Some (DateOnly(2026, 4, 1))
              journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok _ -> Assert.Fail("Expected Error for invalid transition")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("invalid transition")),
                        sprintf "Expected error containing 'invalid transition': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-015")>]
let ``transition from overdue to in_flight is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "overdue" None None true
        let cmd =
            { instanceId = instanceId; targetStatus = InFlight
              amount = None; confirmedDate = None; journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok _ -> Assert.Fail("Expected Error for invalid transition")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("invalid transition")),
                        sprintf "Expected error containing 'invalid transition': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Guard Violations (FT-ST-016 through FT-ST-019)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ST-016")>]
let ``transition to posted without journal entry ID fails`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "confirmed" (Some 500m) None true
        let cmd =
            { instanceId = instanceId; targetStatus = Posted
              amount = None; confirmedDate = None; journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok _ -> Assert.Fail("Expected Error for missing journal_entry_id")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("journal_entry_id")),
                        sprintf "Expected error containing 'journal_entry_id': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-017")>]
let ``transition to posted with nonexistent journal entry fails`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "confirmed" (Some 500m) None true
        let cmd =
            { instanceId = instanceId; targetStatus = Posted
              amount = None; confirmedDate = None; journalEntryId = Some 999999; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok _ -> Assert.Fail("Expected Error for nonexistent journal entry")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                        sprintf "Expected error containing 'does not exist': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-018")>]
let ``transition to skipped without notes fails when instance has no notes`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "expected" None None true
        let cmd =
            { instanceId = instanceId; targetStatus = Skipped
              amount = None; confirmedDate = None; journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok _ -> Assert.Fail("Expected Error for missing notes")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("notes")),
                        sprintf "Expected error containing 'notes': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-019")>]
let ``transition to confirmed without confirmedDate fails`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "expected" (Some 500m) None true
        let cmd =
            { instanceId = instanceId; targetStatus = InstanceStatus.Confirmed
              amount = None; confirmedDate = None; journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok _ -> Assert.Fail("Expected Error for missing confirmedDate")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("confirmed_date")),
                        sprintf "Expected error containing 'confirmed_date': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Edge Cases (FT-ST-020 through FT-ST-022)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ST-020")>]
let ``transition on inactive instance fails`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "expected" None None false
        let cmd =
            { instanceId = instanceId; targetStatus = InFlight
              amount = None; confirmedDate = None; journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok _ -> Assert.Fail("Expected Error for inactive instance")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("inactive")),
                        sprintf "Expected error containing 'inactive': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-021")>]
let ``transition on nonexistent instance fails`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let cmd =
            { instanceId = 999999; targetStatus = InFlight
              amount = None; confirmedDate = None; journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok _ -> Assert.Fail("Expected Error for nonexistent instance")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                        sprintf "Expected error containing 'does not exist': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-ST-022")>]
let ``transition to skipped succeeds when instance already has notes`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let (_, instanceId) = setupInstance conn tracker prefix "expected" None (Some "existing note") true
        let cmd =
            { instanceId = instanceId; targetStatus = Skipped
              amount = None; confirmedDate = None; journalEntryId = None; notes = None }
        match ObligationInstanceService.transition cmd with
        | Ok updated ->
            Assert.Equal(Skipped, updated.status)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker
