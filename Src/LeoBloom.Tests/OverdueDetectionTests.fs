module LeoBloom.Tests.OverdueDetectionTests

open System
open Xunit
open LeoBloom.Domain.Ops
open LeoBloom.Utilities
open LeoBloom.Ops
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Happy Path (FT-OD-001 through FT-OD-004)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OD-001")>]
let ``single overdue instance is transitioned`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_agr" "receivable" "monthly" (Some 1) None true
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_inst" "expected"
            (DateOnly(2026, 3, 15)) None None true |> ignore

        let result = ObligationInstanceService.detectOverdue (DateOnly(2026, 4, 1))
        Assert.Equal(1, result.transitioned)
        Assert.Empty(result.errors)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OD-002")>]
let ``multiple overdue instances are all transitioned`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_agr" "receivable" "monthly" (Some 1) None true
        for month in [ 1; 2; 3 ] do
            InsertHelpers.insertObligationInstanceFull
                conn tracker agreementId $"{prefix}_m{month}" "expected"
                (DateOnly(2026, month, 15)) None None true |> ignore

        let result = ObligationInstanceService.detectOverdue (DateOnly(2026, 4, 1))
        Assert.Equal(3, result.transitioned)
        Assert.Empty(result.errors)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OD-003")>]
let ``instance on the reference date is not overdue`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_agr" "receivable" "monthly" (Some 1) None true
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_inst" "expected"
            (DateOnly(2026, 4, 1)) None None true |> ignore

        let result = ObligationInstanceService.detectOverdue (DateOnly(2026, 4, 1))
        Assert.Equal(0, result.transitioned)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OD-004")>]
let ``instance after the reference date is not overdue`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_agr" "receivable" "monthly" (Some 1) None true
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_inst" "expected"
            (DateOnly(2026, 4, 15)) None None true |> ignore

        let result = ObligationInstanceService.detectOverdue (DateOnly(2026, 4, 1))
        Assert.Equal(0, result.transitioned)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Filtering (FT-OD-005 through FT-OD-008)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OD-005")>]
let ``in_flight instances are not flagged as overdue`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_agr" "receivable" "monthly" (Some 1) None true
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_inst" "in_flight"
            (DateOnly(2026, 3, 15)) None None true |> ignore

        let result = ObligationInstanceService.detectOverdue (DateOnly(2026, 4, 1))
        Assert.Equal(0, result.transitioned)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OD-006")>]
let ``already overdue instances are not re-transitioned`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_agr" "receivable" "monthly" (Some 1) None true
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_inst" "overdue"
            (DateOnly(2026, 3, 15)) None None true |> ignore

        let result = ObligationInstanceService.detectOverdue (DateOnly(2026, 4, 1))
        Assert.Equal(0, result.transitioned)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OD-007")>]
let ``confirmed instances are not flagged as overdue`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_agr" "receivable" "monthly" (Some 1) None true
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_inst" "confirmed"
            (DateOnly(2026, 3, 15)) (Some 500m) None true |> ignore

        let result = ObligationInstanceService.detectOverdue (DateOnly(2026, 4, 1))
        Assert.Equal(0, result.transitioned)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OD-008")>]
let ``inactive instances are not flagged as overdue`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_agr" "receivable" "monthly" (Some 1) None true
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_inst" "expected"
            (DateOnly(2026, 3, 15)) None None false |> ignore

        let result = ObligationInstanceService.detectOverdue (DateOnly(2026, 4, 1))
        Assert.Equal(0, result.transitioned)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Idempotency (FT-OD-009)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OD-009")>]
let ``running detection twice produces same result`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_agr" "receivable" "monthly" (Some 1) None true
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_inst" "expected"
            (DateOnly(2026, 3, 15)) None None true |> ignore

        let result1 = ObligationInstanceService.detectOverdue (DateOnly(2026, 4, 1))
        Assert.Equal(1, result1.transitioned)

        let result2 = ObligationInstanceService.detectOverdue (DateOnly(2026, 4, 1))
        Assert.Equal(0, result2.transitioned)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Mixed Scenarios (FT-OD-010)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OD-010")>]
let ``only eligible instances among a mixed set are transitioned`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementId =
            InsertHelpers.insertObligationAgreementForSpawn
                conn tracker $"{prefix}_agr" "receivable" "monthly" (Some 1) None true

        // expected, before ref date, active — should transition
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_e1" "expected"
            (DateOnly(2026, 3, 1)) None None true |> ignore
        // expected, before ref date, active — should transition
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_e2" "expected"
            (DateOnly(2026, 3, 15)) None None true |> ignore
        // expected, after ref date, active — should NOT transition
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_e3" "expected"
            (DateOnly(2026, 4, 15)) None None true |> ignore
        // in_flight, before ref date — should NOT transition
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_if" "in_flight"
            (DateOnly(2026, 3, 1)) None None true |> ignore
        // confirmed, before ref date — should NOT transition
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_cf" "confirmed"
            (DateOnly(2026, 3, 1)) (Some 500m) None true |> ignore
        // expected, before ref date, inactive — should NOT transition
        InsertHelpers.insertObligationInstanceFull
            conn tracker agreementId $"{prefix}_in" "expected"
            (DateOnly(2026, 3, 1)) None None false |> ignore

        let result = ObligationInstanceService.detectOverdue (DateOnly(2026, 4, 1))
        Assert.Equal(2, result.transitioned)
        Assert.Empty(result.errors)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// No Candidates (FT-OD-011)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OD-011")>]
let ``no overdue instances returns zero transitioned`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        // Don't create any instances — just run detection
        let result = ObligationInstanceService.detectOverdue (DateOnly(2026, 4, 1))
        Assert.Equal(0, result.transitioned)
        Assert.Empty(result.errors)
    finally TestCleanup.deleteAll tracker
