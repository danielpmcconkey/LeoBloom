module LeoBloom.Tests.IrregularCadenceTests

open System
open Xunit
open LeoBloom.Domain.Ops
open LeoBloom.Utilities
open LeoBloom.Ops
open LeoBloom.Tests.TestHelpers

// =====================================================================
// FT-IRC-001: Listing agreements when one has irregular cadence
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-IRC-001")>]
let ``list agreements when one has irregular cadence does not crash`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let agreementId =
        InsertHelpers.insertObligationAgreementFull txn $"{prefix}_irregular_list" "receivable" "irregular" true
    let filter = { isActive = Some true; obligationType = None; cadence = None } : ListAgreementsFilter
    let results = ObligationAgreementService.list txn filter
    let found = results |> List.tryFind (fun a -> a.id = agreementId)
    Assert.True(found.IsSome, sprintf "Expected irregular-cadence agreement (id=%d) in list results" agreementId)
    match found with
    | Some a -> Assert.Equal(Irregular, a.cadence)
    | None -> () // already failed above

// =====================================================================
// FT-IRC-002: Showing an irregular-cadence agreement displays "irregular"
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-IRC-002")>]
let ``showing an irregular-cadence agreement displays cadence as irregular`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let agreementId =
        InsertHelpers.insertObligationAgreementFull txn $"{prefix}_irregular_show" "receivable" "irregular" true
    let result = ObligationAgreementService.getById txn agreementId
    match result with
    | Some a ->
        Assert.Equal(Irregular, a.cadence)
        Assert.Equal("irregular", RecurrenceCadence.toString a.cadence)
    | None -> Assert.Fail(sprintf "Expected agreement with id=%d to be found" agreementId)

// =====================================================================
// FT-IRC-003: Spawning for an irregular agreement produces zero instances
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-IRC-003")>]
let ``spawn for irregular-cadence agreement produces zero instances and no error`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let agreementId =
        InsertHelpers.insertObligationAgreementForSpawn
            txn $"{prefix}_irregular_spawn" "receivable" "irregular" None None true
    let cmd =
        { obligationAgreementId = agreementId
          startDate = DateOnly(2030, 1, 1)
          endDate = DateOnly(2030, 12, 31) } : SpawnObligationInstancesCommand
    let result = ObligationInstanceService.spawn txn cmd
    match result with
    | Ok spawnResult ->
        Assert.Equal(0, spawnResult.created.Length)
        Assert.Equal(0, spawnResult.skippedCount)
    | Error errs -> Assert.Fail(sprintf "Expected Ok for irregular spawn, got Error: %A" errs)
