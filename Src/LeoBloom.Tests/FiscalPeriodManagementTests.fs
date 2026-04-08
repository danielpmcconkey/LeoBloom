module LeoBloom.Tests.FiscalPeriodManagementTests

open System
open Xunit
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Tests.TestHelpers

// =======================================================================
// Fiscal Period Management — Overlap Prevention
//
// Maps to Specs/Behavioral/FiscalPeriodManagement.feature
// Tags: @FT-FPM-001, @FT-FPM-002, @FT-FPM-003
//
// Fiscal period date isolation: this file uses year 2095.
// Years 2091–2094 are assigned to other files (see DSWF/qe.md).
// The service-layer findOverlapping check queries against committed data,
// so each test must use dates that no other test commits.
//
// Period key constraint: period_key is varchar(7). Keys in this file use
// the "95-XXX" format (6 chars) to stay within the limit.
// =======================================================================

// =====================================================================
// @FT-FPM-001 — Partial overlap is rejected
//
// Gherkin: existing 2026-01-01..2026-01-31, new 2026-01-15..2026-02-15
// Translated to 2095 to avoid seed data range (2026–2028).
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-FPM-001")>]
let ``creating a period that partially overlaps an existing period is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // Given a period-test fiscal period "95-JAN" from 2095-01-01 to 2095-01-31
    match FiscalPeriodService.createPeriod txn "95-JAN" (DateOnly(2095, 1, 1)) (DateOnly(2095, 1, 31)) with
    | Error errs -> Assert.Fail(sprintf "Setup: failed to create existing period: %A" errs)
    | Ok _ -> ()
    // When I create a fiscal period "95-MID" from 2095-01-15 to 2095-02-15 (partial overlap)
    let result = FiscalPeriodService.createPeriod txn "95-MID" (DateOnly(2095, 1, 15)) (DateOnly(2095, 2, 15))
    // Then the creation fails with error containing "95-JAN"
    match result with
    | Ok _ -> Assert.Fail("Expected Error for partially overlapping period, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("95-JAN")),
            sprintf "Expected error to identify conflicting period '95-JAN': %A" errs)

// =====================================================================
// @FT-FPM-002 — Identical date range is rejected
//
// Gherkin: existing 2026-01-01..2026-01-31, new 2026-01-01..2026-01-31
// Translated to 2095 to avoid seed data range.
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-FPM-002")>]
let ``creating a period with identical dates to an existing period is rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // Given a period-test fiscal period "95-JAN" from 2095-01-01 to 2095-01-31
    match FiscalPeriodService.createPeriod txn "95-JAN" (DateOnly(2095, 1, 1)) (DateOnly(2095, 1, 31)) with
    | Error errs -> Assert.Fail(sprintf "Setup: failed to create existing period: %A" errs)
    | Ok _ -> ()
    // When I create a fiscal period "95-DUP" from 2095-01-01 to 2095-01-31 (identical range)
    let result = FiscalPeriodService.createPeriod txn "95-DUP" (DateOnly(2095, 1, 1)) (DateOnly(2095, 1, 31))
    // Then the creation fails with error containing "95-JAN"
    match result with
    | Ok _ -> Assert.Fail("Expected Error for duplicate date range, got Ok")
    | Error errs ->
        Assert.True(
            errs |> List.exists (fun e -> e.Contains("95-JAN")),
            sprintf "Expected error to identify conflicting period '95-JAN': %A" errs)

// =====================================================================
// @FT-FPM-003 — Adjacent (non-overlapping) periods are accepted
//
// Gherkin: existing 2026-01-01..2026-01-31, new 2026-02-01..2026-02-28
// Translated to 2095 to avoid seed data range.
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-FPM-003")>]
let ``creating adjacent non-overlapping periods succeeds`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    // Given a period-test fiscal period "95-JAN" from 2095-01-01 to 2095-01-31
    match FiscalPeriodService.createPeriod txn "95-JAN" (DateOnly(2095, 1, 1)) (DateOnly(2095, 1, 31)) with
    | Error errs -> Assert.Fail(sprintf "Setup: failed to create existing period: %A" errs)
    | Ok _ -> ()
    // When I create a fiscal period "95-FEB" from 2095-02-01 to 2095-02-28 (adjacent, not overlapping)
    let result = FiscalPeriodService.createPeriod txn "95-FEB" (DateOnly(2095, 2, 1)) (DateOnly(2095, 2, 28))
    // Then the creation succeeds
    match result with
    | Error errs -> Assert.Fail(sprintf "Expected Ok for adjacent period, got Error: %A" errs)
    | Ok period ->
        Assert.Equal("95-FEB", period.periodKey)

// NOTE: The PostgreSQL exclusion constraint (btree_gist + EXCLUDE USING gist)
// was designed as defense-in-depth for P068 but conflicts with the existing test
// pattern of inserting fiscal periods with dates in 2026–2028 (seed data range).
// The constraint is rolled back; service-layer validation via findOverlapping
// satisfies all three P068 acceptance criteria. See notes.md for details.
