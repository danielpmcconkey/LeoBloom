module LeoBloom.Tests.ObligationAgreementTests

open System
open Xunit
open LeoBloom.Domain.Ops
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Helpers
// =====================================================================

let private defaultCreateCmd prefix =
    { name = $"{prefix}_agreement"
      obligationType = Receivable
      counterparty = None
      amount = None
      cadence = Monthly
      expectedDay = None
      paymentMethod = None
      sourceAccountId = None
      destAccountId = None
      notes = None } : CreateObligationAgreementCommand

let private defaultUpdateCmd id prefix =
    { id = id
      name = $"{prefix}_updated"
      obligationType = Receivable
      counterparty = None
      amount = None
      cadence = Monthly
      expectedDay = None
      paymentMethod = None
      sourceAccountId = None
      destAccountId = None
      isActive = true
      notes = None } : UpdateObligationAgreementCommand

let private defaultFilter =
    { isActive = Some true
      obligationType = None
      cadence = None } : ListAgreementsFilter

// =====================================================================
// Create: Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OA-001")>]
let ``create agreement with all fields provided`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker (prefix + "_at") "debit"
        let srcAcct = InsertHelpers.insertAccount conn tracker (prefix + "S1") "Source" atId true
        let dstAcct = InsertHelpers.insertAccount conn tracker (prefix + "D1") "Dest" atId true
        let cmd =
            { name = $"{prefix}_Enbridge gas bill"
              obligationType = Payable
              counterparty = Some "Enbridge"
              amount = Some 150.00m
              cadence = Monthly
              expectedDay = Some 15
              paymentMethod = Some AutopayPull
              sourceAccountId = Some srcAcct
              destAccountId = Some dstAcct
              notes = Some "Variable in winter" }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok agreement ->
            TestCleanup.trackObligationAgreement agreement.id tracker
            Assert.True(agreement.id > 0)
            Assert.Contains(prefix, agreement.name)
            Assert.Equal(Payable, agreement.obligationType)
            Assert.Equal(Some "Enbridge", agreement.counterparty)
            Assert.Equal(Some 150.00m, agreement.amount)
            Assert.Equal(Monthly, agreement.cadence)
            Assert.Equal(Some 15, agreement.expectedDay)
            Assert.Equal(Some AutopayPull, agreement.paymentMethod)
            Assert.Equal(Some srcAcct, agreement.sourceAccountId)
            Assert.Equal(Some dstAcct, agreement.destAccountId)
            Assert.True(agreement.isActive)
            Assert.Equal(Some "Variable in winter", agreement.notes)
            Assert.True(agreement.createdAt > DateTimeOffset.MinValue)
            Assert.True(agreement.modifiedAt >= agreement.createdAt)
            // REM-013: Verify persistence via independent read
            let persisted = ObligationAgreementService.getById agreement.id
            match persisted with
            | Some p ->
                Assert.Equal(agreement.id, p.id)
                Assert.Equal(agreement.name, p.name)
                Assert.Equal(agreement.obligationType, p.obligationType)
                Assert.Equal(agreement.counterparty, p.counterparty)
                Assert.Equal(agreement.amount, p.amount)
                Assert.Equal(agreement.cadence, p.cadence)
                Assert.Equal(agreement.expectedDay, p.expectedDay)
                Assert.Equal(agreement.paymentMethod, p.paymentMethod)
                Assert.Equal(agreement.sourceAccountId, p.sourceAccountId)
                Assert.Equal(agreement.destAccountId, p.destAccountId)
                Assert.Equal(agreement.notes, p.notes)
                Assert.Equal(agreement.isActive, p.isActive)
            | None -> Assert.Fail("getById returned None after successful create")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-002")>]
let ``create agreement with only required fields`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cmd =
            { name = $"{prefix}_Jeffrey rent"
              obligationType = Receivable
              counterparty = None
              amount = None
              cadence = Monthly
              expectedDay = None
              paymentMethod = None
              sourceAccountId = None
              destAccountId = None
              notes = None }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok agreement ->
            TestCleanup.trackObligationAgreement agreement.id tracker
            Assert.True(agreement.id > 0)
            Assert.True(agreement.counterparty.IsNone)
            Assert.True(agreement.amount.IsNone)
            Assert.True(agreement.expectedDay.IsNone)
            Assert.True(agreement.paymentMethod.IsNone)
            Assert.True(agreement.sourceAccountId.IsNone)
            Assert.True(agreement.destAccountId.IsNone)
            Assert.True(agreement.notes.IsNone)
            Assert.True(agreement.isActive)
            // REM-013: Verify persistence via independent read
            let persisted = ObligationAgreementService.getById agreement.id
            match persisted with
            | Some p ->
                Assert.Equal(agreement.id, p.id)
                Assert.Equal(agreement.name, p.name)
                Assert.True(p.counterparty.IsNone)
                Assert.True(p.amount.IsNone)
                Assert.True(p.expectedDay.IsNone)
                Assert.True(p.paymentMethod.IsNone)
                Assert.True(p.sourceAccountId.IsNone)
                Assert.True(p.destAccountId.IsNone)
                Assert.True(p.notes.IsNone)
            | None -> Assert.Fail("getById returned None after successful create")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Create: Pure Validation
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OA-003")>]
let ``create with empty name is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let cmd = { defaultCreateCmd "x" with name = "" }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok a ->
            TestCleanup.trackObligationAgreement a.id tracker
            Assert.Fail("Expected Error for empty name")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("name")),
                        sprintf "Expected error containing 'name': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-004")>]
let ``create with name exceeding 100 characters is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let longName = String.replicate 101 "x"
        let cmd = { defaultCreateCmd "x" with name = longName }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok a ->
            TestCleanup.trackObligationAgreement a.id tracker
            Assert.Fail("Expected Error for long name")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("name")),
                        sprintf "Expected error containing 'name': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-005")>]
let ``create with counterparty exceeding 100 characters is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let longCp = String.replicate 101 "c"
        let cmd = { defaultCreateCmd prefix with counterparty = Some longCp }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok a ->
            TestCleanup.trackObligationAgreement a.id tracker
            Assert.Fail("Expected Error for long counterparty")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("counterparty")),
                        sprintf "Expected error containing 'counterparty': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Theory>]
[<Trait("GherkinId", "FT-OA-006")>]
[<InlineData(0.0)>]
[<InlineData(-50.0)>]
let ``create with non-positive amount is rejected`` (amount: float) =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cmd = { defaultCreateCmd prefix with amount = Some (decimal amount) }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok a ->
            TestCleanup.trackObligationAgreement a.id tracker
            Assert.Fail("Expected Error for non-positive amount")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("amount")),
                        sprintf "Expected error containing 'amount': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Theory>]
[<Trait("GherkinId", "FT-OA-007")>]
[<InlineData(0)>]
[<InlineData(32)>]
[<InlineData(-1)>]
let ``create with expected day outside 1-31 is rejected`` (day: int) =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cmd = { defaultCreateCmd prefix with expectedDay = Some day }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok a ->
            TestCleanup.trackObligationAgreement a.id tracker
            Assert.Fail("Expected Error for invalid expected day")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("expected")),
                        sprintf "Expected error containing 'expected': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-008")>]
let ``create with multiple validation errors collects all errors`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let cmd =
            { defaultCreateCmd "x" with
                name = ""
                amount = Some -10.00m
                expectedDay = Some 0 }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok a ->
            TestCleanup.trackObligationAgreement a.id tracker
            Assert.Fail("Expected Error for multiple validation failures")
        | Error errs ->
            Assert.True(List.length errs >= 3,
                        sprintf "Expected at least 3 errors, got %d: %A" (List.length errs) errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Create: DB Validation
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OA-009")>]
let ``create with nonexistent source account is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cmd = { defaultCreateCmd prefix with sourceAccountId = Some 99999 }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok a ->
            TestCleanup.trackObligationAgreement a.id tracker
            Assert.Fail("Expected Error for nonexistent source account")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("source")),
                        sprintf "Expected error containing 'source': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-010")>]
let ``create with inactive source account is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker (prefix + "_at") "debit"
        let inactiveAcct = InsertHelpers.insertAccount conn tracker (prefix + "IA") "Inactive" atId false
        let cmd = { defaultCreateCmd prefix with sourceAccountId = Some inactiveAcct }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok a ->
            TestCleanup.trackObligationAgreement a.id tracker
            Assert.Fail("Expected Error for inactive source account")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("inactive")),
                        sprintf "Expected error containing 'inactive': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-011")>]
let ``create with nonexistent dest account is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cmd = { defaultCreateCmd prefix with destAccountId = Some 99999 }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok a ->
            TestCleanup.trackObligationAgreement a.id tracker
            Assert.Fail("Expected Error for nonexistent dest account")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("dest")),
                        sprintf "Expected error containing 'dest': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// REM-008: source = dest rejection
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OA-029")>]
let ``create with source_account_id equal to dest_account_id is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker (prefix + "_at") "debit"
        let acctId = InsertHelpers.insertAccount conn tracker (prefix + "AC") "Shared" atId true
        let cmd = { defaultCreateCmd prefix with sourceAccountId = Some acctId; destAccountId = Some acctId }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok a ->
            TestCleanup.trackObligationAgreement a.id tracker
            Assert.Fail("Expected Error for source = dest account")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("same")),
                        sprintf "Expected error containing 'same': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// REM-009: inactive dest account rejection
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OA-030")>]
let ``create with inactive dest account is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker (prefix + "_at") "debit"
        let inactiveDest = InsertHelpers.insertAccount conn tracker (prefix + "ID") "Inactive Dest" atId false
        let cmd = { defaultCreateCmd prefix with destAccountId = Some inactiveDest }
        let result = ObligationAgreementService.create cmd
        match result with
        | Ok a ->
            TestCleanup.trackObligationAgreement a.id tracker
            Assert.Fail("Expected Error for inactive dest account")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("inactive")),
                        sprintf "Expected error containing 'inactive': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Get by ID
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OA-012")>]
let ``get agreement by ID returns the agreement`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementName = $"{prefix}_Rocket Mortgage"
        let cmd = { defaultCreateCmd prefix with name = agreementName }
        let created =
            match ObligationAgreementService.create cmd with
            | Ok a -> a
            | Error errs -> failwithf "Setup failed: %A" errs
        TestCleanup.trackObligationAgreement created.id tracker
        let result = ObligationAgreementService.getById created.id
        match result with
        | Some found ->
            Assert.Equal(created.id, found.id)
            Assert.Equal(agreementName, found.name)
        | None -> Assert.Fail("Expected Some, got None")
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-013")>]
let ``get agreement by nonexistent ID returns none`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let result = ObligationAgreementService.getById 999999
        Assert.True(result.IsNone, "Expected None for nonexistent ID")
    finally TestCleanup.deleteAll tracker

// =====================================================================
// List
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OA-014")>]
let ``list with default filter returns only active agreements`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let activeName = $"{prefix}_Active Agreement"
        let inactiveName = $"{prefix}_Inactive Agreement"
        let activeId = InsertHelpers.insertObligationAgreementFull conn tracker activeName "receivable" "monthly" true
        let inactiveId = InsertHelpers.insertObligationAgreementFull conn tracker inactiveName "receivable" "monthly" false
        ignore inactiveId
        let results = ObligationAgreementService.list defaultFilter
        Assert.True(results |> List.exists (fun a -> a.id = activeId),
                    "Expected active agreement in results")
        Assert.False(results |> List.exists (fun a -> a.name = inactiveName),
                     "Expected inactive agreement NOT in results")
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-015")>]
let ``list filtered by obligation type`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let receivableName = $"{prefix}_Rent Income"
        let payableName = $"{prefix}_Gas Bill"
        let _recvId = InsertHelpers.insertObligationAgreementFull conn tracker receivableName "receivable" "monthly" true
        let payId = InsertHelpers.insertObligationAgreementFull conn tracker payableName "payable" "monthly" true
        let filter = { defaultFilter with obligationType = Some Payable }
        let results = ObligationAgreementService.list filter
        Assert.True(results |> List.exists (fun a -> a.id = payId),
                    "Expected payable agreement in results")
        Assert.False(results |> List.exists (fun a -> a.name = receivableName),
                     "Expected receivable agreement NOT in results")
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-016")>]
let ``list filtered by cadence`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let monthlyName = $"{prefix}_Monthly Bill"
        let annualName = $"{prefix}_Annual Insurance"
        let monthlyId = InsertHelpers.insertObligationAgreementFull conn tracker monthlyName "receivable" "monthly" true
        let _annualId = InsertHelpers.insertObligationAgreementFull conn tracker annualName "receivable" "annual" true
        let filter = { defaultFilter with cadence = Some Monthly }
        let results = ObligationAgreementService.list filter
        Assert.True(results |> List.exists (fun a -> a.id = monthlyId),
                    "Expected monthly agreement in results")
        Assert.False(results |> List.exists (fun a -> a.name = annualName),
                     "Expected annual agreement NOT in results")
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-017")>]
let ``list with no filter returns all including inactive`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let activeName = $"{prefix}_Active One"
        let inactiveName = $"{prefix}_Inactive One"
        let activeId = InsertHelpers.insertObligationAgreementFull conn tracker activeName "receivable" "monthly" true
        let inactiveId = InsertHelpers.insertObligationAgreementFull conn tracker inactiveName "receivable" "monthly" false
        let filter = { isActive = None; obligationType = None; cadence = None }
        let results = ObligationAgreementService.list filter
        Assert.True(results |> List.exists (fun a -> a.id = activeId),
                    "Expected active agreement in results")
        Assert.True(results |> List.exists (fun a -> a.id = inactiveId),
                    "Expected inactive agreement in results")
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-018")>]
let ``list returns empty when no agreements match`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let results = ObligationAgreementService.list { isActive = None; obligationType = None; cadence = None }
        let matching = results |> List.filter (fun a -> a.name.StartsWith(prefix))
        Assert.Empty(matching)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Update: Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OA-019")>]
let ``update agreement with all fields`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let createCmd = { defaultCreateCmd prefix with name = $"{prefix}_Old Name"; amount = Some 100.00m }
        let created =
            match ObligationAgreementService.create createCmd with
            | Ok a -> a
            | Error errs -> failwithf "Setup create failed: %A" errs
        TestCleanup.trackObligationAgreement created.id tracker
        let updateCmd =
            { defaultUpdateCmd created.id prefix with
                name = $"{prefix}_New Name"
                amount = Some 200.00m }
        let result = ObligationAgreementService.update updateCmd
        match result with
        | Ok updated ->
            Assert.Equal($"{prefix}_New Name", updated.name)
            Assert.Equal(Some 200.00m, updated.amount)
            Assert.True(updated.modifiedAt > created.createdAt,
                        "modified_at should be later than created_at")
            // REM-013: Verify persistence via independent read
            let persisted = ObligationAgreementService.getById updated.id
            match persisted with
            | Some p ->
                Assert.Equal(updated.name, p.name)
                Assert.Equal(updated.amount, p.amount)
            | None -> Assert.Fail("getById returned None after successful update")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Update: Errors
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OA-020")>]
let ``update nonexistent agreement is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let cmd = { defaultUpdateCmd 999999 prefix with name = "Ghost" }
        let result = ObligationAgreementService.update cmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for nonexistent agreement")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                        sprintf "Expected error containing 'does not exist': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-021")>]
let ``update with empty name is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let createCmd = { defaultCreateCmd prefix with name = $"{prefix}_Valid Name" }
        let created =
            match ObligationAgreementService.create createCmd with
            | Ok a -> a
            | Error errs -> failwithf "Setup create failed: %A" errs
        TestCleanup.trackObligationAgreement created.id tracker
        let updateCmd = { defaultUpdateCmd created.id prefix with name = "" }
        let result = ObligationAgreementService.update updateCmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for empty name")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("name")),
                        sprintf "Expected error containing 'name': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-022")>]
let ``update with non-positive amount is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let createCmd = { defaultCreateCmd prefix with name = $"{prefix}_Test Agreement" }
        let created =
            match ObligationAgreementService.create createCmd with
            | Ok a -> a
            | Error errs -> failwithf "Setup create failed: %A" errs
        TestCleanup.trackObligationAgreement created.id tracker
        let updateCmd = { defaultUpdateCmd created.id prefix with amount = Some 0.00m }
        let result = ObligationAgreementService.update updateCmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for non-positive amount")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("amount")),
                        sprintf "Expected error containing 'amount': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-023")>]
let ``update with expected day outside 1-31 is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let createCmd = { defaultCreateCmd prefix with name = $"{prefix}_Test Agreement" }
        let created =
            match ObligationAgreementService.create createCmd with
            | Ok a -> a
            | Error errs -> failwithf "Setup create failed: %A" errs
        TestCleanup.trackObligationAgreement created.id tracker
        let updateCmd = { defaultUpdateCmd created.id prefix with expectedDay = Some 32 }
        let result = ObligationAgreementService.update updateCmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for invalid expected day")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("expected")),
                        sprintf "Expected error containing 'expected': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-024")>]
let ``update with inactive source account is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker (prefix + "_at") "debit"
        let inactiveAcct = InsertHelpers.insertAccount conn tracker (prefix + "IA") "Inactive" atId false
        let createCmd = { defaultCreateCmd prefix with name = $"{prefix}_Test Agreement" }
        let created =
            match ObligationAgreementService.create createCmd with
            | Ok a -> a
            | Error errs -> failwithf "Setup create failed: %A" errs
        TestCleanup.trackObligationAgreement created.id tracker
        let updateCmd = { defaultUpdateCmd created.id prefix with sourceAccountId = Some inactiveAcct }
        let result = ObligationAgreementService.update updateCmd
        match result with
        | Ok _ -> Assert.Fail("Expected Error for inactive source account")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("inactive")),
                        sprintf "Expected error containing 'inactive': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-025")>]
let ``reactivate a previously deactivated agreement`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let dormantName = $"{prefix}_Dormant Agreement"
        let agreementId = InsertHelpers.insertObligationAgreementFull conn tracker dormantName "receivable" "monthly" false
        let updateCmd =
            { defaultUpdateCmd agreementId prefix with
                name = dormantName
                isActive = true }
        let result = ObligationAgreementService.update updateCmd
        match result with
        | Ok updated ->
            Assert.True(updated.isActive, "Expected isActive = true after reactivation")
            // REM-013: Verify persistence via independent read
            let persisted = ObligationAgreementService.getById updated.id
            match persisted with
            | Some p -> Assert.True(p.isActive, "getById should show isActive = true")
            | None -> Assert.Fail("getById returned None after successful reactivation")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Deactivate
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-OA-026")>]
let ``deactivate an active agreement`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let createCmd = { defaultCreateCmd prefix with name = $"{prefix}_Soon Inactive" }
        let created =
            match ObligationAgreementService.create createCmd with
            | Ok a -> a
            | Error errs -> failwithf "Setup create failed: %A" errs
        TestCleanup.trackObligationAgreement created.id tracker
        let result = ObligationAgreementService.deactivate created.id
        match result with
        | Ok deactivated ->
            Assert.False(deactivated.isActive, "Expected isActive = false after deactivation")
            // REM-013: Verify persistence via independent read
            let persisted = ObligationAgreementService.getById deactivated.id
            match persisted with
            | Some p -> Assert.False(p.isActive, "getById should show isActive = false")
            | None -> Assert.Fail("getById returned None after successful deactivation")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-027")>]
let ``deactivate a nonexistent agreement is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let result = ObligationAgreementService.deactivate 999999
        match result with
        | Ok _ -> Assert.Fail("Expected Error for nonexistent agreement")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                        sprintf "Expected error containing 'does not exist': %A" errs)
    finally TestCleanup.deleteAll tracker

[<Fact>]
[<Trait("GherkinId", "FT-OA-028")>]
let ``deactivate blocked by active obligation instances`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let agreementName = $"{prefix}_Has Instances"
        let agreementId = InsertHelpers.insertObligationAgreement conn tracker agreementName
        let _instanceId = InsertHelpers.insertObligationInstance conn tracker agreementId $"{prefix}_inst" true
        let result = ObligationAgreementService.deactivate agreementId
        match result with
        | Ok _ -> Assert.Fail("Expected Error when active instances exist")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("active obligation instances")),
                        sprintf "Expected error containing 'active obligation instances': %A" errs)
    finally TestCleanup.deleteAll tracker
