module LeoBloom.Tests.PositionTests

open System
open Xunit
open Npgsql
open LeoBloom.Utilities
open LeoBloom.Domain.Portfolio
open LeoBloom.Portfolio
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.PortfolioTestHelpers

// =====================================================================
// Shared setup helper
// =====================================================================

let private setupAccountAndFund (txn: NpgsqlTransaction) (prefix: string) =
    let tbId    = PortfolioInsertHelpers.insertTaxBucket    txn (prefix + "_tb")
    let agId    = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
    let acctId  = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_acct") tbId agId
    let sym     = (prefix + "SYM").ToUpper()
    PortfolioInsertHelpers.insertFund txn sym (prefix + " Fund") |> ignore
    (acctId, sym)

// =====================================================================
// @FT-PF-020 -- Record position with valid account and fund
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-020")>]
let ``record position with valid account and fund`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acctId, sym) = setupAccountAndFund txn prefix
    let date = DateOnly(2026, 4, 1)
    let result = PositionService.recordPosition txn acctId sym date 100m 10m 1000m 900m
    match result with
    | Ok pos ->
        Assert.Equal(acctId, pos.investmentAccountId)
        Assert.Equal(sym,    pos.symbol)
        Assert.Equal(date,   pos.positionDate)
        Assert.Equal(100m,   pos.price)
        Assert.Equal(10m,    pos.quantity)
        Assert.Equal(1000m,  pos.currentValue)
        Assert.Equal(900m,   pos.costBasis)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-PF-024 -- List positions filtered by account ID
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-024")>]
let ``list positions filtered by account id`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acctId, sym) = setupAccountAndFund txn prefix
    let date = DateOnly(2026, 4, 1)
    match PositionService.recordPosition txn acctId sym date 50m 5m 250m 200m with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup failed: %A" errs)

    let filter = { investmentAccountId = Some acctId; startDate = None; endDate = None }
    match PositionService.listPositions txn filter with
    | Ok positions ->
        Assert.True(positions |> List.exists (fun p -> p.investmentAccountId = acctId && p.symbol = sym),
                    sprintf "Expected position for acctId %d sym %s" acctId sym)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-PF-026 -- List positions filtered by date range
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-026")>]
let ``list positions filtered by date range`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acctId, sym) = setupAccountAndFund txn prefix
    // Insert two positions on different dates
    match PositionService.recordPosition txn acctId sym (DateOnly(2026, 3, 1)) 50m 5m 250m 200m with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup (date1) failed: %A" errs)
    let sym2 = (prefix + "SY2").ToUpper()
    PortfolioInsertHelpers.insertFund txn sym2 (prefix + " Fund2") |> ignore
    match PositionService.recordPosition txn acctId sym2 (DateOnly(2026, 4, 7)) 80m 3m 240m 210m with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup (date2) failed: %A" errs)

    // Filter: only April
    let filter = { investmentAccountId = None; startDate = Some (DateOnly(2026, 4, 1)); endDate = Some (DateOnly(2026, 4, 30)) }
    match PositionService.listPositions txn filter with
    | Ok positions ->
        Assert.True(positions |> List.exists (fun p -> p.symbol = sym2),
                    "April position should be in results")
        Assert.False(positions |> List.exists (fun p -> p.symbol = sym && p.positionDate = DateOnly(2026, 3, 1)),
                     "March position should NOT be in April-filtered results")
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-PF-027 -- Latest positions snapshot (AC-B7)
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-027")>]
let ``latest positions returns most recent per symbol per account`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acctId, sym) = setupAccountAndFund txn prefix
    // Record two positions for same symbol on different dates
    match PositionService.recordPosition txn acctId sym (DateOnly(2026, 3, 1)) 50m 5m 250m 200m with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup (old) failed: %A" errs)
    match PositionService.recordPosition txn acctId sym (DateOnly(2026, 4, 1)) 75m 5m 375m 200m with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup (new) failed: %A" errs)

    match PositionService.latestPositionsByAccount txn acctId with
    | Ok positions ->
        let matching = positions |> List.filter (fun p -> p.symbol = sym)
        Assert.Equal(1, matching.Length)
        Assert.Equal(DateOnly(2026, 4, 1), matching.[0].positionDate)
        Assert.Equal(75m, matching.[0].price)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-PF-023 -- Duplicate position returns error (AC-B4)
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-023")>]
let ``duplicate position returns friendly error`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acctId, sym) = setupAccountAndFund txn prefix
    let date = DateOnly(2026, 4, 1)
    match PositionService.recordPosition txn acctId sym date 50m 5m 250m 200m with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup failed: %A" errs)

    // Attempt duplicate
    let result = PositionService.recordPosition txn acctId sym date 60m 5m 300m 200m
    match result with
    | Ok _ -> Assert.Fail("Expected Error for duplicate position")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("already exists")),
                    sprintf "Expected 'already exists' error: %A" errs)

// =====================================================================
// @FT-PF-022 -- Position for nonexistent fund rejected (AC-B10)
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-022")>]
let ``position for nonexistent fund rejected`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId   = PortfolioInsertHelpers.insertTaxBucket    txn (prefix + "_tb")
    let agId   = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
    let acctId = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_acct") tbId agId
    let result = PositionService.recordPosition txn acctId "NOSUCHFUND" (DateOnly(2026, 4, 1)) 50m 5m 250m 200m
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent fund")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                    sprintf "Expected 'does not exist' error: %A" errs)

// =====================================================================
// @FT-PF-025 -- List positions filtered by account returns empty when no positions
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-025")>]
let ``list positions filtered by account returns empty when account has no positions`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let tbId   = PortfolioInsertHelpers.insertTaxBucket    txn (prefix + "_tb")
    let agId   = PortfolioInsertHelpers.insertAccountGroup txn (prefix + "_ag")
    let acctId = PortfolioInsertHelpers.insertInvestmentAccount txn (prefix + "_acct") tbId agId
    // No positions created for this account
    let filter = { investmentAccountId = Some acctId; startDate = None; endDate = None }
    match PositionService.listPositions txn filter with
    | Ok positions -> Assert.Empty(positions)
    | Error errs -> Assert.Fail(sprintf "Expected Ok []: %A" errs)

// =====================================================================
// @FT-PF-021 -- Negative value fields rejected (AC-B9)
// =====================================================================

[<Theory>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-021")>]
[<InlineData("price",         -1.0, 10.0, 100.0, 200.0)>]
[<InlineData("quantity",       1.0, -1.0, 100.0, 200.0)>]
[<InlineData("current_value",  1.0, 10.0,  -1.0, 200.0)>]
[<InlineData("cost_basis",   100.0, 10.0, 1000.0, -5000.0)>]
let ``negative value fields rejected at service layer`` (fieldName: string, price: float, qty: float, cv: float, cb: float) =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acctId, sym) = setupAccountAndFund txn prefix
    let result =
        PositionService.recordPosition txn acctId sym (DateOnly(2026, 4, 1))
            (decimal price) (decimal qty) (decimal cv) (decimal cb)
    match result with
    | Ok _ -> Assert.Fail(sprintf "Expected Error for negative %s" fieldName)
    | Error errs ->
        Assert.False(errs.IsEmpty, sprintf "Expected at least one error for negative %s" fieldName)

// =====================================================================
// @FT-PF-028 -- latestAll returns most recent per (account, symbol)
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-028")>]
let ``latestAll returns most recent position per account and symbol`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let (acctId, sym) = setupAccountAndFund txn prefix
    match PositionService.recordPosition txn acctId sym (DateOnly(2026, 2, 1)) 40m 5m 200m 180m with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup (old) failed: %A" errs)
    match PositionService.recordPosition txn acctId sym (DateOnly(2026, 4, 1)) 60m 5m 300m 180m with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "Setup (new) failed: %A" errs)

    match PositionService.latestPositionsAll txn with
    | Ok positions ->
        let matching = positions |> List.filter (fun p -> p.investmentAccountId = acctId && p.symbol = sym)
        Assert.Equal(1, matching.Length)
        Assert.Equal(DateOnly(2026, 4, 1), matching.[0].positionDate)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-PF-029 -- Future position date rejected (AC-B9b)
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-029")>]
let ``position with future date is rejected`` () =
    Log.initialize()
    // Pure validation — fires before any DB call, so no setup needed.
    // Use a throwaway transaction to satisfy the signature.
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let result =
        PositionService.recordPosition txn 0 "DUMMY" (DateOnly(2099, 1, 1))
            100m 10m 1000m 900m
    match result with
    | Ok _ -> Assert.Fail("Expected Error for future position date")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("date")),
            sprintf "Expected error containing 'date': %A" errs)
