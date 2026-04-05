module LeoBloom.Tests.SpawnObligationInstanceTests

open System
open Xunit
open LeoBloom.Domain.Ops
open LeoBloom.Utilities
open LeoBloom.Ops
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Pure Date Generation: Monthly (FT-SI-001 through FT-SI-004)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SI-001")>]
let ``monthly cadence produces correct dates for full range`` () =
    let dates =
        ObligationInstanceSpawning.generateExpectedDates
            Monthly 15 (DateOnly(2026, 1, 1)) (DateOnly(2026, 6, 30))
    Assert.Equal(6, dates.Length)
    let expected =
        [ DateOnly(2026, 1, 15); DateOnly(2026, 2, 15); DateOnly(2026, 3, 15)
          DateOnly(2026, 4, 15); DateOnly(2026, 5, 15); DateOnly(2026, 6, 15) ]
    Assert.Equal<DateOnly list>(expected, dates)

[<Fact>]
[<Trait("GherkinId", "FT-SI-002")>]
let ``monthly cadence clamps day 31 to February 28 in non-leap year`` () =
    let dates =
        ObligationInstanceSpawning.generateExpectedDates
            Monthly 31 (DateOnly(2027, 2, 1)) (DateOnly(2027, 2, 28))
    Assert.Equal(1, dates.Length)
    Assert.Equal(DateOnly(2027, 2, 28), dates.[0])

[<Fact>]
[<Trait("GherkinId", "FT-SI-003")>]
let ``monthly cadence clamps day 31 to February 29 in leap year`` () =
    let dates =
        ObligationInstanceSpawning.generateExpectedDates
            Monthly 31 (DateOnly(2028, 2, 1)) (DateOnly(2028, 2, 29))
    Assert.Equal(1, dates.Length)
    Assert.Equal(DateOnly(2028, 2, 29), dates.[0])

[<Fact>]
[<Trait("GherkinId", "FT-SI-004")>]
let ``monthly cadence defaults expectedDay to 1 when agreement has none`` () =
    let dates =
        ObligationInstanceSpawning.generateExpectedDates
            Monthly 1 (DateOnly(2026, 1, 1)) (DateOnly(2026, 3, 31))
    Assert.Equal(3, dates.Length)
    let expected =
        [ DateOnly(2026, 1, 1); DateOnly(2026, 2, 1); DateOnly(2026, 3, 1) ]
    Assert.Equal<DateOnly list>(expected, dates)

// =====================================================================
// Pure Date Generation: Quarterly (FT-SI-005, FT-SI-006)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SI-005")>]
let ``quarterly cadence produces four dates for full-year range`` () =
    let dates =
        ObligationInstanceSpawning.generateExpectedDates
            Quarterly 15 (DateOnly(2026, 1, 1)) (DateOnly(2026, 12, 31))
    Assert.Equal(4, dates.Length)
    let expected =
        [ DateOnly(2026, 1, 15); DateOnly(2026, 4, 15)
          DateOnly(2026, 7, 15); DateOnly(2026, 10, 15) ]
    Assert.Equal<DateOnly list>(expected, dates)

[<Fact>]
[<Trait("GherkinId", "FT-SI-006")>]
let ``quarterly cadence for partial range includes only in-range dates`` () =
    let dates =
        ObligationInstanceSpawning.generateExpectedDates
            Quarterly 15 (DateOnly(2026, 3, 15)) (DateOnly(2026, 9, 30))
    Assert.Equal(2, dates.Length)
    let expected = [ DateOnly(2026, 4, 15); DateOnly(2026, 7, 15) ]
    Assert.Equal<DateOnly list>(expected, dates)

// =====================================================================
// Pure Date Generation: Annual (FT-SI-007)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SI-007")>]
let ``annual cadence produces one date per year across multi-year range`` () =
    let dates =
        ObligationInstanceSpawning.generateExpectedDates
            Annual 15 (DateOnly(2026, 6, 1)) (DateOnly(2028, 6, 30))
    Assert.Equal(3, dates.Length)
    let expected =
        [ DateOnly(2026, 6, 15); DateOnly(2027, 6, 15); DateOnly(2028, 6, 15) ]
    Assert.Equal<DateOnly list>(expected, dates)

// =====================================================================
// Pure Date Generation: OneTime (FT-SI-008)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SI-008")>]
let ``OneTime cadence produces exactly one date at startDate`` () =
    let dates =
        ObligationInstanceSpawning.generateExpectedDates
            OneTime 1 (DateOnly(2026, 5, 20)) (DateOnly(2026, 12, 31))
    Assert.Equal(1, dates.Length)
    Assert.Equal(DateOnly(2026, 5, 20), dates.[0])

// =====================================================================
// Pure Date Generation: Validation (FT-SI-009)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SI-009")>]
let ``date range where startDate is after endDate is rejected`` () =
    let cmd =
        { obligationAgreementId = 1
          startDate = DateOnly(2026, 6, 1)
          endDate = DateOnly(2026, 1, 1) } : SpawnObligationInstancesCommand
    let result = ObligationInstanceSpawning.validateSpawnCommand cmd
    match result with
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("startdate")),
                    sprintf "Expected error containing 'startdate': %A" errs)
    | Ok _ -> Assert.Fail("Expected Error for startDate > endDate")

// =====================================================================
// Name Generation (FT-SI-010)
// =====================================================================

[<Theory>]
[<Trait("GherkinId", "FT-SI-010")>]
[<InlineData("monthly", "2026-01-15", "Jan 2026")>]
[<InlineData("monthly", "2026-12-01", "Dec 2026")>]
[<InlineData("quarterly", "2026-01-15", "Q1 2026")>]
[<InlineData("quarterly", "2026-04-15", "Q2 2026")>]
[<InlineData("quarterly", "2026-07-15", "Q3 2026")>]
[<InlineData("quarterly", "2026-10-15", "Q4 2026")>]
[<InlineData("annual", "2026-06-15", "2026")>]
[<InlineData("one_time", "2026-05-20", "One-time")>]
let ``instance names follow cadence-specific format`` (cadenceStr: string) (dateStr: string) (expectedName: string) =
    let cadence =
        match RecurrenceCadence.fromString cadenceStr with
        | Ok c -> c
        | Error msg -> failwithf "Bad test data: %s" msg
    let date = DateOnly.Parse(dateStr)
    let name = ObligationInstanceSpawning.generateInstanceName cadence date
    Assert.Equal(expectedName, name)

// =====================================================================
// Integration: Happy Path (FT-SI-011 through FT-SI-013)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SI-011")>]
let ``spawn monthly instances for a 3-month range`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_monthly" "receivable" "monthly" (Some 15) (Some 150.00m) true
        let cmd =
            { obligationAgreementId = agreementId
              startDate = DateOnly(2026, 1, 1)
              endDate = DateOnly(2026, 3, 31) }
        let result = ObligationInstanceService.spawn cmd
        match result with
        | Ok spawnResult ->
            Assert.Equal(3, spawnResult.created.Length)
            Assert.Equal(0, spawnResult.skippedCount)
            // Verify dates
            let dates = spawnResult.created |> List.map (fun i -> i.expectedDate)
            let expectedDates =
                [ DateOnly(2026, 1, 15); DateOnly(2026, 2, 15); DateOnly(2026, 3, 15) ]
            Assert.Equal<DateOnly list>(expectedDates, dates)
            // Verify names
            let names = spawnResult.created |> List.map (fun i -> i.name)
            Assert.Equal<string list>([ "Jan 2026"; "Feb 2026"; "Mar 2026" ], names)
            // Verify status and isActive
            for inst in spawnResult.created do
                Assert.Equal(Expected, inst.status)
                Assert.True(inst.isActive)
                Assert.Equal(Some 150.00m, inst.amount)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SI-012")>]
let ``spawn for variable-amount agreement leaves instance amount empty`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_variable" "receivable" "monthly" (Some 1) None true
        let cmd =
            { obligationAgreementId = agreementId
              startDate = DateOnly(2026, 1, 1)
              endDate = DateOnly(2026, 1, 31) }
        let result = ObligationInstanceService.spawn cmd
        match result with
        | Ok spawnResult ->
            Assert.Equal(1, spawnResult.created.Length)
            Assert.Equal(0, spawnResult.skippedCount)
            Assert.True(spawnResult.created.[0].amount.IsNone,
                        "Expected no amount on instance for variable-amount agreement")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SI-013")>]
let ``spawn OneTime creates a single instance`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_onetime" "receivable" "one_time" None (Some 500.00m) true
        let cmd =
            { obligationAgreementId = agreementId
              startDate = DateOnly(2026, 5, 20)
              endDate = DateOnly(2026, 5, 20) }
        let result = ObligationInstanceService.spawn cmd
        match result with
        | Ok spawnResult ->
            Assert.Equal(1, spawnResult.created.Length)
            Assert.Equal(0, spawnResult.skippedCount)
            let inst = spawnResult.created.[0]
            Assert.Equal("One-time", inst.name)
            Assert.Equal(Some 500.00m, inst.amount)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// SI-014 removed (REM-014): redundant with SI-011

// =====================================================================
// Integration: Overlap and Idempotency (FT-SI-015, FT-SI-016)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SI-015")>]
let ``spawn overlapping range skips existing dates and creates new ones`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_overlap" "receivable" "monthly" (Some 15) (Some 100.00m) true
        // Pre-insert instances for Jan 15 and Feb 15
        InsertHelpers.insertObligationInstanceWithDate
            conn tracker agreementId "Jan 2026" (DateOnly(2026, 1, 15)) true |> ignore
        InsertHelpers.insertObligationInstanceWithDate
            conn tracker agreementId "Feb 2026" (DateOnly(2026, 2, 15)) true |> ignore
        let cmd =
            { obligationAgreementId = agreementId
              startDate = DateOnly(2026, 1, 1)
              endDate = DateOnly(2026, 4, 30) }
        let result = ObligationInstanceService.spawn cmd
        match result with
        | Ok spawnResult ->
            Assert.Equal(2, spawnResult.created.Length)
            Assert.Equal(2, spawnResult.skippedCount)
            let createdDates = spawnResult.created |> List.map (fun i -> i.expectedDate)
            let expectedDates = [ DateOnly(2026, 3, 15); DateOnly(2026, 4, 15) ]
            Assert.Equal<DateOnly list>(expectedDates, createdDates)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SI-016")>]
let ``spawn OneTime when instance already exists skips without error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_otdup" "receivable" "one_time" None (Some 500.00m) true
        // Pre-insert the one-time instance
        InsertHelpers.insertObligationInstanceWithDate
            conn tracker agreementId "One-time" (DateOnly(2026, 5, 20)) true |> ignore
        let cmd =
            { obligationAgreementId = agreementId
              startDate = DateOnly(2026, 5, 20)
              endDate = DateOnly(2026, 5, 20) }
        let result = ObligationInstanceService.spawn cmd
        match result with
        | Ok spawnResult ->
            Assert.Equal(0, spawnResult.created.Length)
            Assert.Equal(1, spawnResult.skippedCount)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Integration: Error Cases (FT-SI-017, FT-SI-018)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SI-017")>]
let ``spawn for inactive agreement returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_inactive" "receivable" "monthly" (Some 1) None false
        let cmd =
            { obligationAgreementId = agreementId
              startDate = DateOnly(2026, 1, 1)
              endDate = DateOnly(2026, 6, 30) }
        let result = ObligationInstanceService.spawn cmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for inactive agreement")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("inactive")),
                        sprintf "Expected error containing 'inactive': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-SI-018")>]
let ``spawn for nonexistent agreement returns error`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let cmd =
            { obligationAgreementId = 999999
              startDate = DateOnly(2026, 1, 1)
              endDate = DateOnly(2026, 6, 30) }
        let result = ObligationInstanceService.spawn cmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for nonexistent agreement")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                        sprintf "Expected error containing 'does not exist': %A" errs)
    finally TestCleanup.deleteAll tracker
