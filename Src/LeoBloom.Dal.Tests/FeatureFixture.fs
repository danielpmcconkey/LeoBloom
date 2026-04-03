module LeoBloom.Dal.Tests.FeatureFixture

open TickSpec.Xunit
open global.Xunit

let source = AssemblyStepDefinitionsSource(System.Reflection.Assembly.GetExecutingAssembly())
let scenarios resourceName =
    source.ScenariosFromEmbeddedResource resourceName
    |> MemberData.ofScenarios

[<Theory; MemberData("scenarios", "LeoBloom.Dal.Tests.LedgerStructuralConstraints.feature")>]
let ``Ledger structural constraints`` (scenario: XunitSerializableScenario) =
    source.RunScenario(scenario)

[<Theory; MemberData("scenarios", "LeoBloom.Dal.Tests.OpsStructuralConstraints.feature")>]
let ``Ops structural constraints`` (scenario: XunitSerializableScenario) =
    source.RunScenario(scenario)

[<Theory; MemberData("scenarios", "LeoBloom.Dal.Tests.DeleteRestrictionConstraints.feature")>]
let ``Delete restriction constraints`` (scenario: XunitSerializableScenario) =
    source.RunScenario(scenario)
