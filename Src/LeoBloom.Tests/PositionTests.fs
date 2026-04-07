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

let private setupAccountAndFund (conn: NpgsqlConnection) (t: PortfolioTracker) (prefix: string) =
    let tbId    = PortfolioInsertHelpers.insertTaxBucket    conn t (prefix + "_tb")
    let agId    = PortfolioInsertHelpers.insertAccountGroup conn t (prefix + "_ag")
    let acctId  = PortfolioInsertHelpers.insertInvestmentAccount conn t (prefix + "_acct") tbId agId
    let sym     = (prefix + "SYM").ToUpper()
    PortfolioInsertHelpers.insertFund conn t sym (prefix + " Fund") |> ignore
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
    let t = createPortfolioTracker conn
    try
        let prefix = TestData.uniquePrefix()
        let (acctId, sym) = setupAccountAndFund conn t prefix
        let date = DateOnly(2026, 4, 1)
        let result = PositionService.recordPosition acctId sym date 100m 10m 1000m 900m
        match result with
        | Ok pos ->
            trackPosition pos.id t
            Assert.Equal(acctId, pos.investmentAccountId)
            Assert.Equal(sym,    pos.symbol)
            Assert.Equal(date,   pos.positionDate)
            Assert.Equal(100m,   pos.price)
            Assert.Equal(10m,    pos.quantity)
            Assert.Equal(1000m,  pos.currentValue)
            Assert.Equal(900m,   pos.costBasis)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally deletePortfolioAll t

// =====================================================================
// @FT-PF-024 -- List positions filtered by account ID
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-024")>]
let ``list positions filtered by account id`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    let t = createPortfolioTracker conn
    try
        let prefix = TestData.uniquePrefix()
        let (acctId, sym) = setupAccountAndFund conn t prefix
        let date = DateOnly(2026, 4, 1)
        match PositionService.recordPosition acctId sym date 50m 5m 250m 200m with
        | Ok pos -> trackPosition pos.id t
        | Error errs -> Assert.Fail(sprintf "Setup failed: %A" errs)

        let filter = { investmentAccountId = Some acctId; startDate = None; endDate = None }
        match PositionService.listPositions filter with
        | Ok positions ->
            Assert.True(positions |> List.exists (fun p -> p.investmentAccountId = acctId && p.symbol = sym),
                        sprintf "Expected position for acctId %d sym %s" acctId sym)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally deletePortfolioAll t

// =====================================================================
// @FT-PF-026 -- List positions filtered by date range
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-026")>]
let ``list positions filtered by date range`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    let t = createPortfolioTracker conn
    try
        let prefix = TestData.uniquePrefix()
        let (acctId, sym) = setupAccountAndFund conn t prefix
        // Insert two positions on different dates
        match PositionService.recordPosition acctId sym (DateOnly(2026, 3, 1)) 50m 5m 250m 200m with
        | Ok pos -> trackPosition pos.id t
        | Error errs -> Assert.Fail(sprintf "Setup (date1) failed: %A" errs)
        let sym2 = (prefix + "SY2").ToUpper()
        PortfolioInsertHelpers.insertFund conn t sym2 (prefix + " Fund2") |> ignore
        match PositionService.recordPosition acctId sym2 (DateOnly(2026, 4, 15)) 80m 3m 240m 210m with
        | Ok pos -> trackPosition pos.id t
        | Error errs -> Assert.Fail(sprintf "Setup (date2) failed: %A" errs)

        // Filter: only April
        let filter = { investmentAccountId = None; startDate = Some (DateOnly(2026, 4, 1)); endDate = Some (DateOnly(2026, 4, 30)) }
        match PositionService.listPositions filter with
        | Ok positions ->
            Assert.True(positions |> List.exists (fun p -> p.symbol = sym2),
                        "April position should be in results")
            Assert.False(positions |> List.exists (fun p -> p.symbol = sym && p.positionDate = DateOnly(2026, 3, 1)),
                         "March position should NOT be in April-filtered results")
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally deletePortfolioAll t

// =====================================================================
// @FT-PF-027 -- Latest positions snapshot (AC-B7)
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-027")>]
let ``latest positions returns most recent per symbol per account`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    let t = createPortfolioTracker conn
    try
        let prefix = TestData.uniquePrefix()
        let (acctId, sym) = setupAccountAndFund conn t prefix
        // Record two positions for same symbol on different dates
        match PositionService.recordPosition acctId sym (DateOnly(2026, 3, 1)) 50m 5m 250m 200m with
        | Ok pos -> trackPosition pos.id t
        | Error errs -> Assert.Fail(sprintf "Setup (old) failed: %A" errs)
        match PositionService.recordPosition acctId sym (DateOnly(2026, 4, 1)) 75m 5m 375m 200m with
        | Ok pos -> trackPosition pos.id t
        | Error errs -> Assert.Fail(sprintf "Setup (new) failed: %A" errs)

        match PositionService.latestPositionsByAccount acctId with
        | Ok positions ->
            let matching = positions |> List.filter (fun p -> p.symbol = sym)
            Assert.Equal(1, matching.Length)
            Assert.Equal(DateOnly(2026, 4, 1), matching.[0].positionDate)
            Assert.Equal(75m, matching.[0].price)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally deletePortfolioAll t

// =====================================================================
// @FT-PF-023 -- Duplicate position returns error (AC-B4)
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-023")>]
let ``duplicate position returns friendly error`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    let t = createPortfolioTracker conn
    try
        let prefix = TestData.uniquePrefix()
        let (acctId, sym) = setupAccountAndFund conn t prefix
        let date = DateOnly(2026, 4, 1)
        match PositionService.recordPosition acctId sym date 50m 5m 250m 200m with
        | Ok pos -> trackPosition pos.id t
        | Error errs -> Assert.Fail(sprintf "Setup failed: %A" errs)

        // Attempt duplicate
        let result = PositionService.recordPosition acctId sym date 60m 5m 300m 200m
        match result with
        | Ok _ -> Assert.Fail("Expected Error for duplicate position")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("already exists")),
                        sprintf "Expected 'already exists' error: %A" errs)
    finally deletePortfolioAll t

// =====================================================================
// @FT-PF-022 -- Position for nonexistent fund rejected (AC-B10)
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-022")>]
let ``position for nonexistent fund rejected`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    let t = createPortfolioTracker conn
    try
        let prefix = TestData.uniquePrefix()
        let tbId   = PortfolioInsertHelpers.insertTaxBucket    conn t (prefix + "_tb")
        let agId   = PortfolioInsertHelpers.insertAccountGroup conn t (prefix + "_ag")
        let acctId = PortfolioInsertHelpers.insertInvestmentAccount conn t (prefix + "_acct") tbId agId
        let result = PositionService.recordPosition acctId "NOSUCHFUND" (DateOnly(2026, 4, 1)) 50m 5m 250m 200m
        match result with
        | Ok _ -> Assert.Fail("Expected Error for nonexistent fund")
        | Error errs ->
            Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("does not exist")),
                        sprintf "Expected 'does not exist' error: %A" errs)
    finally deletePortfolioAll t

// =====================================================================
// @FT-PF-025 -- List positions filtered by account returns empty when no positions
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-025")>]
let ``list positions filtered by account returns empty when account has no positions`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    let t = createPortfolioTracker conn
    try
        let prefix = TestData.uniquePrefix()
        let tbId   = PortfolioInsertHelpers.insertTaxBucket    conn t (prefix + "_tb")
        let agId   = PortfolioInsertHelpers.insertAccountGroup conn t (prefix + "_ag")
        let acctId = PortfolioInsertHelpers.insertInvestmentAccount conn t (prefix + "_acct") tbId agId
        // No positions created for this account
        let filter = { investmentAccountId = Some acctId; startDate = None; endDate = None }
        match PositionService.listPositions filter with
        | Ok positions -> Assert.Empty(positions)
        | Error errs -> Assert.Fail(sprintf "Expected Ok []: %A" errs)
    finally deletePortfolioAll t

// =====================================================================
// @FT-PF-021 -- Negative value fields rejected (AC-B9)
// =====================================================================

[<Theory>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-021")>]
[<InlineData("price",         -1.0, 10.0, 100.0)>]
[<InlineData("quantity",       1.0, -1.0, 100.0)>]
[<InlineData("current_value",  1.0, 10.0, -1.0)>]
let ``negative value fields rejected at service layer`` (fieldName: string, price: float, qty: float, cv: float) =
    Log.initialize()
    use conn = DataSource.openConnection()
    let t = createPortfolioTracker conn
    try
        let prefix = TestData.uniquePrefix()
        let (acctId, sym) = setupAccountAndFund conn t prefix
        let result =
            PositionService.recordPosition acctId sym (DateOnly(2026, 4, 1))
                (decimal price) (decimal qty) (decimal cv) 200m
        match result with
        | Ok _ -> Assert.Fail(sprintf "Expected Error for negative %s" fieldName)
        | Error errs ->
            Assert.False(errs.IsEmpty, sprintf "Expected at least one error for negative %s" fieldName)
    finally deletePortfolioAll t

// =====================================================================
// @FT-PF-028 -- latestAll returns most recent per (account, symbol)
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-028")>]
let ``latestAll returns most recent position per account and symbol`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    let t = createPortfolioTracker conn
    try
        let prefix = TestData.uniquePrefix()
        let (acctId, sym) = setupAccountAndFund conn t prefix
        match PositionService.recordPosition acctId sym (DateOnly(2026, 2, 1)) 40m 5m 200m 180m with
        | Ok pos -> trackPosition pos.id t
        | Error errs -> Assert.Fail(sprintf "Setup (old) failed: %A" errs)
        match PositionService.recordPosition acctId sym (DateOnly(2026, 4, 1)) 60m 5m 300m 180m with
        | Ok pos -> trackPosition pos.id t
        | Error errs -> Assert.Fail(sprintf "Setup (new) failed: %A" errs)

        match PositionService.latestPositionsAll() with
        | Ok positions ->
            let matching = positions |> List.filter (fun p -> p.investmentAccountId = acctId && p.symbol = sym)
            Assert.Equal(1, matching.Length)
            Assert.Equal(DateOnly(2026, 4, 1), matching.[0].positionDate)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally deletePortfolioAll t
