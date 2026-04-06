module LeoBloom.Tests.InvoiceCommandsTests

open System
open System.Text.Json
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests

// =====================================================================
// Shared setup: create a "CLI-testable invoice environment"
// -- an open fiscal period for invoice recording
// =====================================================================

module InvoiceCliEnv =
    type Env =
        { FiscalPeriodId: int
          Prefix: string
          Tracker: TestCleanup.Tracker }

    let create () =
        let conn = DataSource.openConnection()
        let tracker = TestCleanup.create conn
        let prefix = TestData.uniquePrefix()
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "FP") (DateOnly(2099, 4, 1)) (DateOnly(2099, 4, 30)) true
        { FiscalPeriodId = fpId
          Prefix = prefix
          Tracker = tracker }

    let cleanup (env: Env) =
        TestCleanup.deleteAll env.Tracker
        env.Tracker.Connection.Dispose()

    /// Record an invoice via CLI (human mode) and return the invoice ID.
    let recordInvoiceViaCli (env: Env) (tenant: string) : int =
        let args =
            sprintf "invoice record --tenant \"%s\" --fiscal-period-id %d --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50 --generated-at \"2026-04-01T12:00Z\""
                tenant env.FiscalPeriodId
        let result = CliRunner.run args
        if result.ExitCode <> 0 then
            failwith (sprintf "Failed to record invoice for test setup. stderr: %s" result.Stderr)
        // Parse invoice ID from human-readable output: "Invoice #NNN"
        let stdout = CliRunner.stripLogLines result.Stdout
        let line = stdout.Split('\n') |> Array.find (fun l -> l.Contains("Invoice #"))
        let hashIdx = line.IndexOf('#')
        let rest = line.Substring(hashIdx + 1).Trim()
        let idStr = rest.Split([| ' '; '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries).[0]
        let invoiceId = Int32.Parse(idStr)
        TestCleanup.trackInvoice invoiceId env.Tracker
        invoiceId

    /// Record an invoice via CLI (JSON mode) and return the invoice ID.
    let recordInvoiceViaCliJson (env: Env) (tenant: string) : int =
        let args =
            sprintf "--json invoice record --tenant \"%s\" --fiscal-period-id %d --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50 --generated-at \"2026-04-01T12:00Z\""
                tenant env.FiscalPeriodId
        let result = CliRunner.run args
        if result.ExitCode <> 0 then
            failwith (sprintf "Failed to record invoice for test setup. stderr: %s" result.Stderr)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        let invoiceId = doc.RootElement.GetProperty("id").GetInt32()
        TestCleanup.trackInvoice invoiceId env.Tracker
        invoiceId

// =====================================================================
// invoice record -- Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ICD-001")>]
let ``record an invoice with all required and optional args`` () =
    let env = InvoiceCliEnv.create()
    try
        let tenant = sprintf "%s_Jeffrey" env.Prefix
        let args =
            sprintf "invoice record --tenant \"%s\" --fiscal-period-id %d --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50 --generated-at \"2026-04-01T12:00Z\" --document-path \"/docs/inv-001.pdf\" --notes \"April charges\""
                tenant env.FiscalPeriodId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Invoice #", stdout)
        Assert.Contains(tenant, stdout)
        Assert.Contains("1200", stdout)
        Assert.Contains("85.5", stdout)
        Assert.Contains("/docs/inv-001.pdf", stdout)
        Assert.Contains("April charges", stdout)
        // Track for cleanup
        if stdout.Contains("Invoice #") then
            let line = stdout.Split('\n') |> Array.find (fun l -> l.Contains("Invoice #"))
            let hashIdx = line.IndexOf('#')
            let rest = line.Substring(hashIdx + 1).Trim()
            let idStr = rest.Split([| ' '; '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries).[0]
            TestCleanup.trackInvoice (Int32.Parse(idStr)) env.Tracker
    finally InvoiceCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-ICD-002")>]
let ``record an invoice with --json flag outputs valid JSON`` () =
    let env = InvoiceCliEnv.create()
    try
        let tenant = sprintf "%s_Jeffrey" env.Prefix
        let args =
            sprintf "--json invoice record --tenant \"%s\" --fiscal-period-id %d --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50 --generated-at \"2026-04-01T12:00Z\""
                tenant env.FiscalPeriodId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
        // Track for cleanup
        let invoiceId = doc.RootElement.GetProperty("id").GetInt32()
        TestCleanup.trackInvoice invoiceId env.Tracker
    finally InvoiceCliEnv.cleanup env

// =====================================================================
// invoice record -- Missing Required Args
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ICD-003")>]
let ``record with no arguments prints error to stderr`` () =
    let result = CliRunner.run "invoice record"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-ICD-004a")>]
let ``record missing --tenant is rejected`` () =
    let result = CliRunner.run "invoice record --fiscal-period-id 1 --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50 --generated-at \"2026-04-01T12:00Z\""
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-ICD-004b")>]
let ``record missing --fiscal-period-id is rejected`` () =
    let result = CliRunner.run "invoice record --tenant \"Jeffrey\" --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50 --generated-at \"2026-04-01T12:00Z\""
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-ICD-004c")>]
let ``record missing --rent-amount is rejected`` () =
    let result = CliRunner.run "invoice record --tenant \"Jeffrey\" --fiscal-period-id 1 --utility-share 85.50 --total-amount 1285.50 --generated-at \"2026-04-01T12:00Z\""
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-ICD-004d")>]
let ``record missing --utility-share is rejected`` () =
    let result = CliRunner.run "invoice record --tenant \"Jeffrey\" --fiscal-period-id 1 --rent-amount 1200.00 --total-amount 1285.50 --generated-at \"2026-04-01T12:00Z\""
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-ICD-004e")>]
let ``record missing --total-amount is rejected`` () =
    let result = CliRunner.run "invoice record --tenant \"Jeffrey\" --fiscal-period-id 1 --rent-amount 1200.00 --utility-share 85.50 --generated-at \"2026-04-01T12:00Z\""
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-ICD-004f")>]
let ``record missing --generated-at is rejected`` () =
    let result = CliRunner.run "invoice record --tenant \"Jeffrey\" --fiscal-period-id 1 --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50"
    Assert.True(result.ExitCode = 1 || result.ExitCode = 2,
                sprintf "Expected exit code 1 or 2, got %d" result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// invoice record -- Service Validation Error
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ICD-005")>]
let ``record that triggers a service validation error surfaces it to stderr`` () =
    // Use a nonexistent fiscal period ID to trigger a service-level error
    let result = CliRunner.run "invoice record --tenant \"Jeffrey\" --fiscal-period-id 999999 --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50 --generated-at \"2026-04-01T12:00Z\""
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// invoice show -- Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ICD-010")>]
let ``show an existing invoice via CLI`` () =
    let env = InvoiceCliEnv.create()
    try
        let tenant = sprintf "%s_Jeffrey" env.Prefix
        let invoiceId = InvoiceCliEnv.recordInvoiceViaCli env tenant
        let args = sprintf "invoice show %d" invoiceId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains("Invoice #", stdout)
        Assert.Contains(tenant, stdout)
    finally InvoiceCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-ICD-011")>]
let ``show with --json flag outputs valid JSON`` () =
    let env = InvoiceCliEnv.create()
    try
        let tenant = sprintf "%s_Jeffrey" env.Prefix
        let invoiceId = InvoiceCliEnv.recordInvoiceViaCliJson env tenant
        let args = sprintf "--json invoice show %d" invoiceId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally InvoiceCliEnv.cleanup env

// =====================================================================
// invoice show -- Error Paths
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ICD-012")>]
let ``show a nonexistent invoice prints error to stderr`` () =
    let result = CliRunner.run "invoice show 999999"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-ICD-013")>]
let ``show with no invoice ID prints error to stderr`` () =
    let result = CliRunner.run "invoice show"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// invoice list -- Happy Path
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ICD-020")>]
let ``list all invoices with no filters`` () =
    let env = InvoiceCliEnv.create()
    try
        // Use different tenants to avoid unique constraint on (tenant, fiscal_period_id)
        let tenantA = sprintf "%s_Jeffrey" env.Prefix
        let tenantB = sprintf "%s_BobRoss" env.Prefix
        InvoiceCliEnv.recordInvoiceViaCli env tenantA |> ignore
        InvoiceCliEnv.recordInvoiceViaCli env tenantB |> ignore
        let result = CliRunner.run "invoice list"
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Should contain table headers or invoice rows
        Assert.Contains("Tenant", stdout)
    finally InvoiceCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-ICD-021")>]
let ``list invoices filtered by tenant`` () =
    let env = InvoiceCliEnv.create()
    try
        let tenantA = sprintf "%s_Jeffrey" env.Prefix
        let tenantB = sprintf "%s_OtherGuy" env.Prefix
        InvoiceCliEnv.recordInvoiceViaCli env tenantA |> ignore
        InvoiceCliEnv.recordInvoiceViaCli env tenantB |> ignore
        let args = sprintf "invoice list --tenant \"%s\"" tenantA
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(tenantA, stdout)
        Assert.DoesNotContain(tenantB, stdout)
    finally InvoiceCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-ICD-022")>]
let ``list invoices filtered by fiscal period`` () =
    let env = InvoiceCliEnv.create()
    try
        let tenant = sprintf "%s_Jeffrey" env.Prefix
        InvoiceCliEnv.recordInvoiceViaCli env tenant |> ignore
        let args = sprintf "invoice list --fiscal-period-id %d" env.FiscalPeriodId
        let result = CliRunner.run args
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        Assert.Contains(tenant, stdout)
    finally InvoiceCliEnv.cleanup env

[<Fact>]
[<Trait("GherkinId", "FT-ICD-023")>]
let ``list with --json flag outputs valid JSON`` () =
    let env = InvoiceCliEnv.create()
    try
        let tenant = sprintf "%s_Jeffrey" env.Prefix
        InvoiceCliEnv.recordInvoiceViaCli env tenant |> ignore
        let result = CliRunner.run "--json invoice list"
        Assert.Equal(0, result.ExitCode)
        let cleanStdout = CliRunner.stripLogLines result.Stdout
        let doc = JsonDocument.Parse(cleanStdout)
        Assert.NotNull(doc)
    finally InvoiceCliEnv.cleanup env

// =====================================================================
// invoice list -- Empty Results
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-ICD-024")>]
let ``list with no matching results prints empty output`` () =
    let env = InvoiceCliEnv.create()
    try
        let result = CliRunner.run "invoice list --tenant \"NonexistentTenant\""
        Assert.Equal(0, result.ExitCode)
        let stdout = CliRunner.stripLogLines result.Stdout
        // Empty or headers-only -- no invoice data rows
        Assert.True(String.IsNullOrWhiteSpace(stdout) || not (stdout.Contains("NonexistentTenant")),
                    "Expected empty output or no matching tenant rows")
    finally InvoiceCliEnv.cleanup env
