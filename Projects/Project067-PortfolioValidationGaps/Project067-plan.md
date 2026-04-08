# Project 067 — Portfolio Validation Gaps — Plan

## Objective

Close two spec-and-service gaps identified by the Domain Invariant Auditor:
negative `cost_basis` values pass silently (no validation, no spec), and
future-dated positions are accepted despite P058 stating otherwise. Ship
the service-layer guards and matching behavioral specs for both.

## Current State

- **PositionService.recordPosition** (`Src/LeoBloom.Portfolio/PositionService.fs:16`)
  validates `price`, `quantity`, and `currentValue` for non-negativity but
  **skips `costBasis`**. No future-date check exists.
- **FT-PF-021** scenario outline (`Specs/Portfolio/Position.feature:20`) has
  example rows for price, quantity, current_value — no cost_basis row.
- **PositionTests.fs:210** mirrors FT-PF-021 as `[<Theory>]` with `InlineData`
  rows for the same three fields; costBasis is hardcoded to `200m`.
- No future-date scenario or test exists anywhere.

## Phases

### Phase 1: Negative cost_basis guard + spec (AC-1, AC-2)

**What:**
1. Add `costBasis` validation to `PositionService.recordPosition` — reject
   if `costBasis < 0m`, add error `"Cost basis must not be negative"`.
2. Add one example row to the FT-PF-021 scenario outline in
   `Specs/Portfolio/Position.feature`:
   ```
   | cost_basis    | -5000.00 |
   ```
3. Extend the xUnit theory in `PositionTests.fs` to cover cost_basis:
   - Add a fourth `costBasis` parameter to the `InlineData` signature
     (default `200.0` for existing rows, `-1.0` for the new row).
   - Pass `decimal cb` through to `recordPosition` instead of the hardcoded `200m`.

**Files modified:**
- `Src/LeoBloom.Portfolio/PositionService.fs` — add two lines (if + errors.Add)
- `Specs/Portfolio/Position.feature` — add one example row
- `Src/LeoBloom.Tests/PositionTests.fs` — extend Theory signature, add InlineData row

**Verification:** `dotnet test --filter "GherkinId=FT-PF-021"` passes with
four InlineData rows, including the new cost_basis one.

### Phase 2: Future-date guard + spec (AC-3, AC-4)

**What:**
1. Add future-date check in `PositionService.recordPosition` alongside
   the existing non-negative guards (pure validation block, before DB access):
   ```fsharp
   if positionDate > DateOnly.FromDateTime(DateTime.UtcNow) then
       errors.Add("Position date must not be in the future")
   ```
2. Add new scenario to `Specs/Portfolio/Position.feature` under the
   "Record: Pure Validation" section, tagged `@FT-PF-029`:
   ```gherkin
   @FT-PF-029
   Scenario: Record position with future date is rejected
       Given the portfolio schema exists for position management
       And a portfolio investment account "Test Account" exists
       And a portfolio fund "VTI" exists
       When I record a position for account "Test Account", symbol "VTI", dated 2099-01-01, with price 100.00, quantity 10.0000, current_value 1000.00, cost_basis 900.00
       Then the record fails with error containing "future"
   ```
3. Add corresponding xUnit test in `PositionTests.fs`, tagged
   `GherkinId=FT-PF-029`:
   ```fsharp
   [<Fact>]
   [<Trait("Category", "Portfolio")>]
   [<Trait("GherkinId", "FT-PF-029")>]
   let ``future-dated position rejected`` () =
       // use date far in the future (2099-01-01) to avoid timezone edge cases
       // call recordPosition, assert Error containing "future"
   ```

**Files modified:**
- `Src/LeoBloom.Portfolio/PositionService.fs` — add two lines
- `Specs/Portfolio/Position.feature` — add new scenario
- `Src/LeoBloom.Tests/PositionTests.fs` — add new test

**Verification:** `dotnet test --filter "GherkinId=FT-PF-029"` passes.

## Acceptance Criteria

- [ ] AC-1: `PositionService.recordPosition` returns `Error` when `costBasis < 0m`
- [ ] AC-2: FT-PF-021 scenario outline includes a `cost_basis | -5000.00` example row, and the matching xUnit theory row passes
- [ ] AC-3: `PositionService.recordPosition` returns `Error` when `positionDate` is in the future
- [ ] AC-4: FT-PF-029 scenario and matching xUnit test exercise the future-date rejection path and pass

## Risks

- **Timezone edge case on future-date check:** Using `DateOnly.FromDateTime(DateTime.UtcNow)` means the boundary is UTC-relative. The test uses 2099-01-01 to avoid flaky boundary behavior. Documented in test comment.
- **InlineData signature change:** Expanding the Theory from 4 to 5 parameters is backward-compatible since all existing rows get the new `costBasis` column added explicitly. No runtime breakage.

## Out of Scope

- Updating the P058 backlog status (PO already noted it's stale but delivered).
- Adding validation for zero values (spec only says "negative").
- Adding database-level CHECK constraints for these validations (service-layer-only per P058 design).
