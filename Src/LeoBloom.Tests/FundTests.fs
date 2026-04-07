module LeoBloom.Tests.FundTests

open System
open Xunit
open Npgsql
open LeoBloom.Utilities
open LeoBloom.Domain.Portfolio
open LeoBloom.Portfolio
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.PortfolioTestHelpers

// =====================================================================
// @FT-PF-010 -- Create fund with symbol and name
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-010")>]
let ``create fund with symbol and name`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    let t = createPortfolioTracker conn
    try
        let prefix = TestData.uniquePrefix()
        let sym = (prefix + "FND").ToUpper()
        let fund : Fund =
            { symbol = sym; name = prefix + " Fund"
              investmentTypeId = None; marketCapId = None; indexTypeId = None
              sectorId = None; regionId = None; objectiveId = None }
        let result = FundService.createFund fund
        match result with
        | Ok f ->
            trackFund f.symbol t
            Assert.Equal(sym,           f.symbol)
            Assert.Equal(fund.name,     f.name)
            Assert.True(f.investmentTypeId.IsNone)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally deletePortfolioAll t

// =====================================================================
// @FT-PF-011 -- Create fund with optional dimension IDs
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-011")>]
let ``create fund with optional dimension ids`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    let t = createPortfolioTracker conn
    try
        let prefix = TestData.uniquePrefix()
        let sym = (prefix + "DIM").ToUpper()
        // Use seeded dimension IDs — P057 seeds guarantee at least 1 row in each dim table
        use dimCmd = new NpgsqlCommand(
            "SELECT id FROM portfolio.dim_investment_type LIMIT 1", conn)
        let itId = dimCmd.ExecuteScalar() :?> int

        let fund : Fund =
            { symbol = sym; name = prefix + " DimFund"
              investmentTypeId = Some itId; marketCapId = None; indexTypeId = None
              sectorId = None; regionId = None; objectiveId = None }
        let result = FundService.createFund fund
        match result with
        | Ok f ->
            trackFund f.symbol t
            Assert.Equal(Some itId, f.investmentTypeId)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally deletePortfolioAll t

// =====================================================================
// @FT-PF-012 -- Blank symbol rejected
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-012")>]
let ``blank fund symbol rejected`` () =
    Log.initialize()
    let fund : Fund =
        { symbol = ""; name = "SomeFund"
          investmentTypeId = None; marketCapId = None; indexTypeId = None
          sectorId = None; regionId = None; objectiveId = None }
    let result = FundService.createFund fund
    match result with
    | Ok _ -> Assert.Fail("Expected Error for blank symbol")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("symbol")),
                    sprintf "Expected error mentioning 'symbol': %A" errs)

// =====================================================================
// @FT-PF-013 -- Blank name rejected
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-013")>]
let ``blank fund name rejected`` () =
    Log.initialize()
    let fund : Fund =
        { symbol = "TSYM"; name = "   "
          investmentTypeId = None; marketCapId = None; indexTypeId = None
          sectorId = None; regionId = None; objectiveId = None }
    let result = FundService.createFund fund
    match result with
    | Ok _ -> Assert.Fail("Expected Error for blank name")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.ToLowerInvariant().Contains("name")),
                    sprintf "Expected error mentioning 'name': %A" errs)

// =====================================================================
// @FT-PF-015 -- List funds returns created fund
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-015")>]
let ``list funds returns created fund`` () =
    Log.initialize()
    use conn = DataSource.openConnection()
    let t = createPortfolioTracker conn
    try
        let prefix = TestData.uniquePrefix()
        let sym = (prefix + "LST").ToUpper()
        let fund : Fund =
            { symbol = sym; name = prefix + " ListFund"
              investmentTypeId = None; marketCapId = None; indexTypeId = None
              sectorId = None; regionId = None; objectiveId = None }
        match FundService.createFund fund with
        | Ok f -> trackFund f.symbol t
        | Error errs -> Assert.Fail(sprintf "Setup failed: %A" errs)

        match FundService.listFunds() with
        | Ok funds ->
            Assert.True(funds |> List.exists (fun f -> f.symbol = sym),
                        sprintf "Expected symbol '%s' in fund list" sym)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally deletePortfolioAll t

// =====================================================================
// @FT-PF-014 -- findBySymbol for nonexistent symbol returns None
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-014")>]
let ``findBySymbol for nonexistent symbol returns none`` () =
    Log.initialize()
    let result = FundService.findFundBySymbol "XYZNOSUCHSYMBOL"
    match result with
    | Ok None -> ()
    | Ok (Some f) -> Assert.Fail(sprintf "Expected None but got fund: %A" f)
    | Error errs -> Assert.Fail(sprintf "Expected Ok None: %A" errs)

// =====================================================================
// @FT-PF-016 -- List funds by dimension filter (all 6 cases)
// =====================================================================

[<Theory>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-016")>]
[<InlineData("investment_type")>]
[<InlineData("market_cap")>]
[<InlineData("index_type")>]
[<InlineData("sector")>]
[<InlineData("region")>]
[<InlineData("objective")>]
let ``list funds by dimension returns matching funds`` (dimName: string) =
    Log.initialize()
    use conn = DataSource.openConnection()
    let t = createPortfolioTracker conn
    try
        // Get a seeded dimension ID for the given dim table
        let tableName = sprintf "portfolio.dim_%s" dimName
        use dimCmd = new NpgsqlCommand(sprintf "SELECT id FROM %s LIMIT 1" tableName, conn)
        let dimId = dimCmd.ExecuteScalar() :?> int

        let prefix = TestData.uniquePrefix()
        let sym = (prefix + "DIM").ToUpper()

        // Build a fund with the matching dimension set
        let fund : Fund =
            { symbol = sym; name = prefix + " " + dimName + " Fund"
              investmentTypeId = if dimName = "investment_type" then Some dimId else None
              marketCapId      = if dimName = "market_cap"      then Some dimId else None
              indexTypeId      = if dimName = "index_type"      then Some dimId else None
              sectorId         = if dimName = "sector"          then Some dimId else None
              regionId         = if dimName = "region"          then Some dimId else None
              objectiveId      = if dimName = "objective"       then Some dimId else None }
        match FundService.createFund fund with
        | Ok f -> trackFund f.symbol t
        | Error errs -> Assert.Fail(sprintf "Setup createFund failed: %A" errs)

        let filter =
            match dimName with
            | "investment_type" -> ByInvestmentType dimId
            | "market_cap"      -> ByMarketCap      dimId
            | "index_type"      -> ByIndexType      dimId
            | "sector"          -> BySector         dimId
            | "region"          -> ByRegion         dimId
            | "objective"       -> ByObjective      dimId
            | other             -> failwithf "Unknown dim: %s" other

        match FundService.listFundsByDimension filter with
        | Ok funds ->
            Assert.True(funds |> List.exists (fun f -> f.symbol = sym),
                        sprintf "Expected symbol '%s' in filtered list for dim '%s'" sym dimName)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally deletePortfolioAll t

// =====================================================================
// @FT-PF-017 -- List funds by dimension returns empty when no funds match
// =====================================================================

[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "FT-PF-017")>]
let ``list funds by dimension returns empty when no funds match`` () =
    Log.initialize()
    // Use an ID that is guaranteed not to exist (no seed data uses 999999)
    let result = FundService.listFundsByDimension (ByInvestmentType 999999)
    match result with
    | Ok funds -> Assert.Empty(funds)
    | Error errs -> Assert.Fail(sprintf "Expected Ok []: %A" errs)
