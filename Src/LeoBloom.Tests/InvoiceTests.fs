module LeoBloom.Tests.InvoiceTests

open System
open Xunit
open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Utilities
open LeoBloom.Ops
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Local helpers
// =====================================================================

/// Create an active fiscal period and return its id.
let private setupActiveFiscalPeriod (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (prefix: string) =
    InsertHelpers.insertFiscalPeriod conn tracker (TestData.periodKey prefix) (DateOnly(2099, 4, 1)) (DateOnly(2099, 4, 30)) true

/// Create a closed fiscal period and return its id.
let private setupClosedFiscalPeriod (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (prefix: string) =
    InsertHelpers.insertFiscalPeriod conn tracker (TestData.periodKey prefix) (DateOnly(2099, 5, 1)) (DateOnly(2099, 5, 31)) false

/// Build a default RecordInvoiceCommand with all fields populated.
let private defaultCmd (tenant: string) (fpId: int) : RecordInvoiceCommand =
    { tenant = tenant
      fiscalPeriodId = fpId
      rentAmount = 1200.00m
      utilityShare = 85.50m
      totalAmount = 1285.50m
      generatedAt = DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero)
      documentPath = Some "/docs/inv-001.pdf"
      notes = Some "April charges" }

/// Record an invoice through the service, assert success, track for cleanup, return Invoice.
let private recordAndTrack (tracker: TestCleanup.Tracker) (cmd: RecordInvoiceCommand) : Invoice =
    match InvoiceService.recordInvoice cmd with
    | Ok inv ->
        TestCleanup.trackInvoice inv.id tracker
        inv
    | Error errs -> failwithf "Setup failed - could not record invoice: %A" errs

// =====================================================================
// Record: Happy Path -- @FT-INV-001
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-001")>]
let ``Recording a valid invoice persists it and returns a complete record`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd = defaultCmd $"{prefix}_Jeffrey" fpId
        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.True(inv.id > 0, "Expected generated id > 0")
            Assert.True(inv.createdAt > DateTimeOffset.MinValue, "Expected createdAt timestamp")
            Assert.True(inv.modifiedAt > DateTimeOffset.MinValue, "Expected modifiedAt timestamp")
            Assert.Equal($"{prefix}_Jeffrey", inv.tenant)
            Assert.Equal(1285.50m, inv.totalAmount)
            Assert.Equal(DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero), inv.generatedAt)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Null optional fields -- @FT-INV-002
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-002")>]
let ``Recording an invoice with null optional fields succeeds`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd =
            { defaultCmd $"{prefix}_Jeffrey" fpId with
                rentAmount = 1200.00m
                utilityShare = 0.00m
                totalAmount = 1200.00m
                documentPath = None
                notes = None }

        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.True(inv.id > 0)
            Assert.True(inv.documentPath.IsNone, "Expected null documentPath")
            Assert.True(inv.notes.IsNone, "Expected null notes")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Closed fiscal period -- @FT-INV-003
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-003")>]
let ``Recording an invoice for a closed fiscal period succeeds`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupClosedFiscalPeriod conn tracker prefix

        let cmd = { defaultCmd $"{prefix}_Jeffrey" fpId with totalAmount = 1285.50m }
        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.True(inv.id > 0)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Zero rent, non-zero utility -- @FT-INV-004
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-004")>]
let ``Recording an invoice with zero rent and non-zero utility succeeds`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd =
            { defaultCmd $"{prefix}_Jeffrey" fpId with
                rentAmount = 0.00m
                utilityShare = 85.50m
                totalAmount = 85.50m }

        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.True(inv.id > 0)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Zero utility, non-zero rent -- @FT-INV-005
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-005")>]
let ``Recording an invoice with zero utility and non-zero rent succeeds`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd =
            { defaultCmd $"{prefix}_Jeffrey" fpId with
                rentAmount = 1200.00m
                utilityShare = 0.00m
                totalAmount = 1200.00m }

        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.True(inv.id > 0)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Empty tenant -- @FT-INV-006
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-006")>]
let ``Recording with empty tenant is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd = { defaultCmd "" fpId with totalAmount = 1285.50m }
        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.Fail("Expected Error for empty tenant")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("tenant")),
                        sprintf "Expected error containing 'tenant': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Tenant exceeding 50 chars -- @FT-INV-007
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-007")>]
let ``Recording with tenant exceeding 50 characters is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix
        let longTenant = String.replicate 51 "X"

        let cmd = { defaultCmd longTenant fpId with totalAmount = 1285.50m }
        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.Fail("Expected Error for long tenant")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("tenant")),
                        sprintf "Expected error containing 'tenant': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Negative rentAmount -- @FT-INV-008a
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-008")>]
let ``Recording with negative rentAmount is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd =
            { defaultCmd $"{prefix}_Jeffrey" fpId with
                rentAmount = -1.00m
                utilityShare = 85.50m
                totalAmount = 84.50m }

        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.Fail("Expected Error for negative rentAmount")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("rentamount")),
                        sprintf "Expected error containing 'rentAmount': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Negative utilityShare -- @FT-INV-008b
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-008")>]
let ``Recording with negative utilityShare is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd =
            { defaultCmd $"{prefix}_Jeffrey" fpId with
                rentAmount = 1200.00m
                utilityShare = -1.00m
                totalAmount = 1199.00m }

        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.Fail("Expected Error for negative utilityShare")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("utilityshare")),
                        sprintf "Expected error containing 'utilityShare': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Negative totalAmount -- @FT-INV-008c
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-008")>]
let ``Recording with negative totalAmount is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd =
            { defaultCmd $"{prefix}_Jeffrey" fpId with
                rentAmount = 0.00m
                utilityShare = 0.00m
                totalAmount = -1.00m }

        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.Fail("Expected Error for negative totalAmount")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("totalamount")),
                        sprintf "Expected error containing 'totalAmount': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: rentAmount with >2 decimal places -- @FT-INV-009a
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-009")>]
let ``Recording with rentAmount having more than 2 decimal places is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd =
            { defaultCmd $"{prefix}_Jeffrey" fpId with
                rentAmount = 100.005m
                utilityShare = 50.00m
                totalAmount = 150.005m }

        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.Fail("Expected Error for >2 decimal places")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("decimal places")),
                        sprintf "Expected error containing 'decimal places': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: utilityShare with >2 decimal places -- @FT-INV-009b
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-009")>]
let ``Recording with utilityShare having more than 2 decimal places is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd =
            { defaultCmd $"{prefix}_Jeffrey" fpId with
                rentAmount = 100.00m
                utilityShare = 50.999m
                totalAmount = 150.999m }

        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.Fail("Expected Error for >2 decimal places")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("decimal places")),
                        sprintf "Expected error containing 'decimal places': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: totalAmount with >2 decimal places -- @FT-INV-009c
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-009")>]
let ``Recording with totalAmount having more than 2 decimal places is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd =
            { defaultCmd $"{prefix}_Jeffrey" fpId with
                rentAmount = 100.00m
                utilityShare = 50.001m
                totalAmount = 150.001m }

        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.Fail("Expected Error for >2 decimal places")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("decimal places")),
                        sprintf "Expected error containing 'decimal places': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Total not equal to rent + utility -- @FT-INV-010
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-010")>]
let ``Recording with total not equal to rent plus utility is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd =
            { defaultCmd $"{prefix}_Jeffrey" fpId with
                rentAmount = 1200.00m
                utilityShare = 85.50m
                totalAmount = 1300.00m }

        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.Fail("Expected Error for mismatched total")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("total")),
                        sprintf "Expected error containing 'total': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Multiple validation errors -- @FT-INV-011
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-011")>]
let ``Recording with multiple validation errors collects all errors`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix

        let cmd =
            { defaultCmd "" fpId with
                rentAmount = -1.00m
                utilityShare = -1.00m
                totalAmount = -1.00m }

        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.Fail("Expected Error for multiple validation failures")
        | Error errs ->
            Assert.True(errs.Length >= 3,
                        sprintf "Expected at least 3 errors, got %d: %A" errs.Length errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Nonexistent fiscal period -- @FT-INV-012
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-012")>]
let ``Recording with nonexistent fiscal period is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()

        let cmd = defaultCmd $"{prefix}_Jeffrey" 999999
        let result = InvoiceService.recordInvoice cmd

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.Fail("Expected Error for nonexistent fiscal period")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("fiscal period")),
                        sprintf "Expected error containing 'fiscal period': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Record: Duplicate tenant + fiscal period -- @FT-INV-013
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-013")>]
let ``Recording a duplicate tenant and fiscal period is rejected`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix
        let tenant = $"{prefix}_Jeffrey"

        // Insert the first invoice
        let _firstInv = recordAndTrack tracker (defaultCmd tenant fpId)

        // Attempt duplicate
        let result = InvoiceService.recordInvoice (defaultCmd tenant fpId)

        match result with
        | Ok inv ->
            TestCleanup.trackInvoice inv.id tracker
            Assert.Fail("Expected Error for duplicate invoice")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("already exists")),
                        sprintf "Expected error containing 'already exists': %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Show: Retrieve by ID -- @FT-INV-014
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-014")>]
let ``Showing an invoice by ID returns the full record`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix
        let tenant = $"{prefix}_Jeffrey"
        let inv = recordAndTrack tracker (defaultCmd tenant fpId)

        let result = InvoiceService.showInvoice inv.id

        match result with
        | Ok shown ->
            Assert.Equal(tenant, shown.tenant)
            Assert.Equal(inv.id, shown.id)
            Assert.Equal(inv.totalAmount, shown.totalAmount)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// Show: Nonexistent invoice -- @FT-INV-015
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-015")>]
let ``Showing a nonexistent invoice returns an error`` () =
    let result = InvoiceService.showInvoice 999999

    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent invoice")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                    sprintf "Expected error containing 'does not exist': %A" errs)

// =====================================================================
// List: No filter returns all active -- @FT-INV-016
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-016")>]
let ``Listing invoices with no filter returns all active invoices`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix
        let tenantJ = $"{prefix}_Jeffrey"
        let tenantA = $"{prefix}_Adam"

        let _invJ = recordAndTrack tracker (defaultCmd tenantJ fpId)
        let _invA = recordAndTrack tracker
                        { defaultCmd tenantA fpId with
                            rentAmount = 1000.00m
                            utilityShare = 50.00m
                            totalAmount = 1050.00m }

        let results = InvoiceService.listInvoices { tenant = None; fiscalPeriodId = None }

        let found = results |> List.filter (fun i -> i.tenant = tenantJ || i.tenant = tenantA)
        Assert.True(found.Length >= 2,
                    sprintf "Expected at least 2 matching invoices, got %d" found.Length)
        Assert.True(found |> List.exists (fun i -> i.tenant = tenantJ))
        Assert.True(found |> List.exists (fun i -> i.tenant = tenantA))
    finally TestCleanup.deleteAll tracker

// =====================================================================
// List: Filtered by tenant -- @FT-INV-017
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-017")>]
let ``Listing invoices filtered by tenant returns only that tenant`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fpId = setupActiveFiscalPeriod conn tracker prefix
        let tenantJ = $"{prefix}_Jeffrey"
        let tenantA = $"{prefix}_Adam"

        let _invJ = recordAndTrack tracker (defaultCmd tenantJ fpId)
        let _invA = recordAndTrack tracker
                        { defaultCmd tenantA fpId with
                            rentAmount = 1000.00m
                            utilityShare = 50.00m
                            totalAmount = 1050.00m }

        let results = InvoiceService.listInvoices { tenant = Some tenantJ; fiscalPeriodId = None }

        Assert.True(results |> List.exists (fun i -> i.tenant = tenantJ),
                    "Expected invoice for Jeffrey")
        Assert.True(results |> List.forall (fun i -> i.tenant <> tenantA),
                    "Expected no invoice for Adam in filtered results")
    finally TestCleanup.deleteAll tracker

// =====================================================================
// List: Filtered by fiscal period -- @FT-INV-018
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-018")>]
let ``Listing invoices filtered by fiscal period returns only that period`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fp1 = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}F1" (DateOnly(2099, 1, 1)) (DateOnly(2099, 1, 31)) true
        let fp2 = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}F2" (DateOnly(2099, 2, 1)) (DateOnly(2099, 2, 28)) true
        let tenant = $"{prefix}_Jeffrey"

        let _inv1 = recordAndTrack tracker (defaultCmd tenant fp1)
        let _inv2 = recordAndTrack tracker
                        { defaultCmd tenant fp2 with
                            rentAmount = 1200.00m
                            utilityShare = 85.50m
                            totalAmount = 1285.50m }

        let results = InvoiceService.listInvoices { tenant = None; fiscalPeriodId = Some fp1 }

        let matching = results |> List.filter (fun i -> i.tenant = tenant && i.fiscalPeriodId = fp1)
        Assert.Equal(1, matching.Length)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// List: Filtered by tenant and fiscal period -- @FT-INV-019
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-019")>]
let ``Listing invoices filtered by both tenant and fiscal period`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let fp1 = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}F1" (DateOnly(2099, 3, 1)) (DateOnly(2099, 3, 31)) true
        let fp2 = InsertHelpers.insertFiscalPeriod conn tracker $"{prefix}F2" (DateOnly(2099, 4, 1)) (DateOnly(2099, 4, 30)) true
        let tenantJ = $"{prefix}_Jeffrey"
        let tenantA = $"{prefix}_Adam"

        let _invJF1 = recordAndTrack tracker (defaultCmd tenantJ fp1)
        let _invAF1 = recordAndTrack tracker
                        { defaultCmd tenantA fp1 with
                            rentAmount = 1000.00m
                            utilityShare = 50.00m
                            totalAmount = 1050.00m }
        let _invJF2 = recordAndTrack tracker
                        { defaultCmd tenantJ fp2 with
                            rentAmount = 1200.00m
                            utilityShare = 85.50m
                            totalAmount = 1285.50m }

        let results = InvoiceService.listInvoices { tenant = Some tenantJ; fiscalPeriodId = Some fp1 }

        Assert.Equal(1, results.Length)
        Assert.Equal(tenantJ, results.[0].tenant)
    finally TestCleanup.deleteAll tracker

// =====================================================================
// List: No matches returns empty -- @FT-INV-020
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "@FT-INV-020")>]
let ``Listing invoices returns empty when none match`` () =
    let results = InvoiceService.listInvoices { tenant = Some "Nobody"; fiscalPeriodId = None }
    Assert.Empty(results)
