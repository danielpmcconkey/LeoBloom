# Project 063 — Portfolio Delete Restriction Tests — Plan

## Objective

Add 10 delete restriction test scenarios covering every FK in the portfolio
schema. This closes the gap where ledger and ops have 14 tested RESTRICT
constraints but portfolio has zero. A single migration flipping ON DELETE
RESTRICT to CASCADE could silently destroy data chains — these tests catch
that.

## Phases

### Phase 1: Add Gherkin scenarios to existing feature file

**What:** Append 10 new scenarios (FT-DR-019 through FT-DR-028) to
`Specs/Structural/DeleteRestrictionConstraints.feature`, grouped under a
`# --- portfolio schema ---` comment header.

**Files modified:**
- `Specs/Structural/DeleteRestrictionConstraints.feature`

**Scenarios:**

| Tag | Parent table | Child table | FK column |
|-----|-------------|-------------|-----------|
| FT-DR-019 | tax_bucket | investment_account | tax_bucket_id |
| FT-DR-020 | account_group | investment_account | account_group_id |
| FT-DR-021 | investment_account | position | investment_account_id |
| FT-DR-022 | fund | position | symbol |
| FT-DR-023 | dim_investment_type | fund | investment_type_id |
| FT-DR-024 | dim_market_cap | fund | market_cap_id |
| FT-DR-025 | dim_index_type | fund | index_type_id |
| FT-DR-026 | dim_sector | fund | sector_id |
| FT-DR-027 | dim_region | fund | region_id |
| FT-DR-028 | dim_objective | fund | objective_id |

**Verification:** `grep -c '@FT-DR-' DeleteRestrictionConstraints.feature`
returns 24 (14 existing + 10 new).

### Phase 2: Implement F# test functions

**What:** Add 10 `[<Fact>]` test functions to
`Src/LeoBloom.Tests/DeleteRestrictionTests.fs` following the identical
pattern used by existing tests.

**Files modified:**
- `Src/LeoBloom.Tests/DeleteRestrictionTests.fs`

**Pattern per test:**
1. Open connection, begin transaction (transaction = automatic cleanup on
   dispose — this IS the PortfolioTracker-equivalent isolation strategy for
   structural tests)
2. Insert parent row(s) using `PortfolioInsertHelpers` where available, raw
   SQL for dim tables
3. Insert child row referencing the parent
4. Attempt `DELETE FROM portfolio.<parent> WHERE ...`
5. Assert FK violation via `ConstraintAssert.assertFk`
6. Transaction disposes on function exit → rollback (no manual cleanup)

**New module import needed:** Add `open LeoBloom.Tests.PortfolioTestHelpers`
at the top of the file.

**Dim table tests (DR-023 through DR-028):** Each inserts a dim row via raw
SQL, then inserts a fund with the corresponding dimension FK set, then
attempts to delete the dim row.

**Verification:** `dotnet test --filter "Trait=GherkinId"` — all 24 DR tests
pass.

## Acceptance Criteria

- [ ] AC-1: 10 scenarios exist in the feature file, one per portfolio FK
- [ ] AC-2: Each scenario asserts delete is rejected (FK violation 23503)
- [ ] AC-3: Tests use transaction-scoped isolation (begin txn → auto-rollback
  on dispose), consistent with the existing test pattern and equivalent to
  PortfolioTracker cleanup semantics
- [ ] AC-4: Tag numbering continues from DR-018 (DR-019 through DR-028)
- [ ] AC-5: All 24 delete restriction tests pass

## Risks

- **Low:** PortfolioInsertHelpers doesn't have dim table insert helpers. Raw
  SQL insert is fine — it's the same 2-line pattern used extensively in the
  existing tests. Not worth adding 6 single-use helpers.
- **Low:** Feature file grows from 104 to ~170 lines. Still well under
  "unwieldy" threshold — no need for a separate file.

## Out of Scope

- Adding PortfolioTracker (a cleanup-tracking utility) — these structural
  tests use transaction rollback, same as every existing DR test.
- Adding dim insert helpers to PortfolioTestHelpers — premature abstraction
  for one-line inserts.
- Testing ON DELETE SET NULL or other behaviors — spec is RESTRICT only.
