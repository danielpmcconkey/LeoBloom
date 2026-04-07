# Test Harness Auditor Report -- LeoBloom

**Date:** 2026-04-07
**Scope:** Test infrastructure scalability assessment
**Test project:** `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj`
**Current test count:** ~800+ across 43 test files (excluding generated/obj)
**Growth model:** Autonomous AI agents (Nightshift) shipping features in parallel

---

## Executive Summary

The test infrastructure has a thoughtful isolation model at its core -- GUID-prefixed test data, per-test cleanup trackers, real database assertions -- but it is under active strain. The architecture works at 800 tests. At 1,600 it will produce phantom failures. At 3,000 it will be abandoned unless the two structural flaws (global-scope reports and cleanup-residue accumulation) are addressed. Several friction patterns will slow feature velocity before you reach those numbers.

---

## Finding 1: Balance Sheet / Trial Balance Query Global Scope

**Classification: STRUCTURAL**

The `BalanceSheetService.getAsOfDate` and related reporting services query ALL data in the database, not scoped to test-created data. Tests attempt to work around this:

- BalanceSheetTests FT-BS-003/004/005 use far-past dates (1950, 1951, 1952) to isolate retained earnings calculations from other tests' revenue/expense entries.
- FT-BS-006 uses DateOnly(1900, 1, 1) and asserts all zeros -- this will break the moment any other test creates data in a date range that a cumulative query picks up.
- Tests filter report output by `accountId` to find their own accounts (e.g., `report.assets.lines |> List.find (fun l -> l.accountId = assetAcct)`), which is a correct mitigation for line-level assertions.

**What breaks:** Any test that asserts on `sectionTotal`, `retainedEarnings`, or `totalEquity` without filtering to its own accounts will collide with data from other tests. The far-past-date workaround is fragile -- it only takes one agent creating test data in 1950 to break FT-BS-003. As more reporting tests are added, the probability of date-range collision approaches 1.

**When:** Next time a Nightshift agent adds reporting tests that use novel date ranges, or when a test fails to clean up revenue/expense entries.

---

## Finding 2: Cleanup Residue from Incomplete Tracker Coverage

**Classification: STRUCTURAL**

The `TestCleanup.Tracker` tracks 6 entity types: journal entries, accounts, account types, fiscal periods, obligation agreements, and invoices. The `deleteAll` function deletes in FK-safe order. However:

1. **Obligation instances are not directly tracked.** The cleanup relies on cascade-by-agreement-id: `tryDeleteMultiColumn "ops.obligation_instance" ["obligation_agreement_id", tracker.ObligationAgreementIds]`. If a test creates an obligation instance but the parent agreement cleanup fails (caught and logged, not re-thrown), the instance becomes orphaned residue.

2. **Transfer rows are cleaned by `journal_entry_id`, `from_account_id`, and `to_account_id`** -- but there is no `mutable TransferIds` on the tracker. If a transfer is created via the service (not raw SQL), it gets an auto-increment ID that is never tracked. Cleanup succeeds only because transfers FK to accounts and journal entries that ARE tracked. But if an account cleanup fails first (exception caught), the transfer remains.

3. **Portfolio entities use a separate `PortfolioTracker`** that is not integrated with the main `TestCleanup.Tracker`. This means portfolio tests use a completely different cleanup path. Any cross-domain test (e.g., a test that creates both ledger accounts and portfolio positions) would need to manage two trackers independently. This is currently fine because the domains are separate, but it will fragment as the portfolio grows.

4. **The `PortfolioStructuralConstraintsTests` don't use either tracker at all.** They do manual inline cleanup in their `finally` blocks with raw SQL. Each test manages its own mutable IDs and delete sequence. This is a different cleanup pattern from every other test file. PSC-010 has a 5-entity manual cleanup chain.

**What breaks:** Over thousands of test runs, residue accumulates in the dev database. Cleanup failures are caught and logged to stderr, not surfaced as test failures. Tests that query globally (reporting tests, list operations) will eventually see phantom data from prior runs. This is the "tests that pass alone but fail together" archetype.

**When:** Already happening or imminent, given that test data collision is described as an ACTIVE problem.

---

## Finding 3: Hardcoded Seed Data IDs

**Classification: FRAGILE**

At least 10 test files hardcode `assetTypeId = 1`, `liabilityTypeId = 2`, `equityTypeId = 3`, `revenueTypeId = 4`, `expenseTypeId = 5`. These magic numbers appear as module-level constants in:

- BalanceSheetTests.fs
- IncomeStatementTests.fs
- TrialBalanceTests.fs
- OpeningBalanceTests.fs
- SubtreePLTests.fs
- ReportingServiceTests.fs
- TransferTests.fs (inline)
- BalanceProjectionTests.fs (inline)
- TransferCommandsTests.fs (inline)
- ReportCommandsTests.fs (inline)
- OrphanedPostingDetectionTests.fs (inline)

These depend on the account_type table's auto-increment IDs matching the seed insertion order. If the seed data changes order, or if a migration re-seeds with different IDs, all of these tests break simultaneously.

Additionally, `ReportingServiceTests.fs` uses `seedAccountId conn "4110"` etc. -- looking up seed accounts by code. This is a better pattern (code is stable, ID is not), but it couples tests to the specific chart of accounts. A change to seed data breaks the Schedule E reporting tests.

**What breaks:** Any migration that alters account_type IDs or rearranges seed data breaks 10+ test files at once.

**When:** Next time anyone modifies the account_type seed data or rebuilds the dev database from a different starting state.

---

## Finding 4: Hardcoded Names in Constraint Tests

**Classification: FRAGILE**

`LedgerConstraintTests.fs` uses hardcoded identifiers like `"lsc004_type"`, `"lsc006_type"`, `"LSC024"`, `"LSC025"`, `"l015fp"` through `"l027fp"`. Similarly, `OpsConstraintTests.fs` uses `"osc017_agreement"`, `"osc020_agreement"`, etc.

These are collision-safe today because:
- Account type names and account codes have UNIQUE constraints, so duplicate insertion attempts will fail.
- The constraint tests are testing for exactly those constraint violations.

However, they create a silent coupling: if two test runs overlap (CI parallel, or manual + Nightshift), the second run's insert of `"lsc004_type"` will hit a unique constraint violation NOT because the test data is set up correctly, but because residue from a prior run exists. The test still passes (it's testing for a NULL violation, not a unique violation), but the cleanup tracker has nothing to delete because the insert failed.

**What breaks:** Parallel test execution or overlapping test runs with the same database.

**When:** If CI is ever configured with parallel test execution, or if Nightshift runs tests while a developer is also running tests.

---

## Finding 5: Duplicate postEntry Helper

**Classification: FRICTION**

The same `postEntry` helper function is copy-pasted identically across 6 test files:

- AccountBalanceTests.fs
- BalanceSheetTests.fs
- IncomeStatementTests.fs
- TrialBalanceTests.fs
- SubtreePLTests.fs
- ReportingServiceTests.fs

Each is a `let private` binding with identical signature: `conn tracker acct1 acct2 fpId entryDate desc amount`. This is a textbook extract-to-shared-helper case.

**Impact:** When the JournalEntryService.post API changes (new required field, changed command shape), 6 files need identical updates. Nightshift agents will inevitably update some but not all.

---

## Finding 6: Duplicate Account Type ID Constants

**Classification: FRICTION**

The block `let private assetTypeId = 1 / liabilityTypeId = 2 / equityTypeId = 3 / revenueTypeId = 4 / expenseTypeId = 5` is duplicated across 7+ files. This is both a friction issue (same constant defined everywhere) and an amplifier of Finding 3 (if the IDs change, the blast radius is maximized).

---

## Finding 7: No Parallelism Configuration

**Classification: FRAGILE**

There is no `xunit.runner.json` file. No `[<CollectionDefinition>]` or `[<Collection>]` attributes. No `IClassFixture` or `ICollectionFixture` usage. xUnit 2.x runs test classes in parallel by default, and tests within a class sequentially.

Since every test file is a separate F# module (= separate class to xUnit), tests across different files ARE running in parallel by default. This works today because of the GUID prefix strategy (Finding 8 below), but it means:

- Any test that queries the full database state (reporting tests) can see data from concurrent tests.
- Tests that modify shared seed data (e.g., deactivating a seed account) will interfere with concurrent tests that read that data.
- There is no explicit documentation that parallel execution is the intended mode.

The BalanceSheetTests FT-BS-011 deactivates an account it created, which is safe. But if any test ever deactivates a SEED account and another test reads it concurrently, both fail intermittently.

**When:** Already a risk. The far-past-date workaround in BalanceSheetTests is evidence that developers have already encountered this problem.

---

## Finding 8: GUID Prefix Isolation Strategy

**Classification: SOLID**

The `TestData.uniquePrefix()` function generates a 4-character hex prefix from `Guid.NewGuid()` for each test. This prefix is prepended to account codes, account type names, fiscal period keys, and other identifying fields. Combined with the per-test `TestCleanup.Tracker`, this provides effective isolation for the majority of tests.

**Why it works:**
- Each test creates its own accounts, types, periods, and entries with unique identifiers.
- Tests assert against their own data by ID, not by name or position.
- The 4-char prefix is short enough to fit varchar constraints (code=10, period_key=7).
- The probability of GUID collision in a 4-char hex space (65,536 values) is negligible for test suite sizes under 10,000.

**Why it scales:** This pattern is self-contained. New tests automatically get isolation by following the pattern. It doesn't require coordination between test authors.

**Caveat:** The 4-char prefix (65,536 values) is sufficient for test isolation within a single run. If test residue accumulates across runs (Finding 2), the effective namespace shrinks. At 100 runs of 800 tests = 80,000 data-creating tests, prefix collisions with residue become possible.

---

## Finding 9: try/finally Cleanup Pattern

**Classification: SOLID**

Every database-touching test follows the same pattern:

```fsharp
use conn = DataSource.openConnection()
let tracker = TestCleanup.create conn
try
    // ... test body ...
finally TestCleanup.deleteAll tracker
```

This is consistent across all 43 test files. The `finally` block ensures cleanup runs even if the test fails or throws. The `use conn` ensures the connection is disposed.

**Why it works:** The pattern is simple, visible, and hard to get wrong. There's no implicit teardown that could be accidentally skipped. Every test is self-documenting about what it creates and what it cleans up.

**Why it scales:** New tests copy the pattern. It's not clever enough to break.

---

## Finding 10: PortfolioStructuralConstraintsTests Manual Cleanup Divergence

**Classification: FRICTION**

`PortfolioStructuralConstraintsTests.fs` uses a completely different cleanup approach from every other test file. Instead of using the PortfolioTracker or TestCleanup.Tracker, each test manually tracks mutable IDs and issues raw DELETE statements in the finally block. Example from PSC-010:

```fsharp
let mutable tbId = 0
let mutable agId = 0
let mutable iaId = 0
let mutable posId = 0
try
    // ... create data ...
finally
    if posId > 0 then try DELETE ... with ex -> eprintfn ...
    if iaId > 0 then try DELETE ... with ex -> eprintfn ...
    // ... 3 more ...
```

This pattern is:
- Error-prone (manual FK ordering, easy to miss an entity).
- Verbose (PSC-010 is 40 lines of cleanup for a 20-line test).
- Inconsistent with the rest of the codebase (every other file uses a tracker).
- A template that Nightshift agents will copy when writing new portfolio constraint tests.

**Impact:** The next agent that adds portfolio constraint tests will likely copy this pattern instead of using PortfolioTestHelpers, further entrenching the inconsistency.

---

## Finding 11: No Transaction Rollback Strategy

**Classification: STRUCTURAL (latent)**

The test suite uses INSERT-then-DELETE as its isolation strategy. There is no transaction wrapping -- tests commit data to the shared database and rely on cleanup to remove it. This is a deliberate design choice (testing against real committed data), but it means:

- Tests that fail mid-execution (unhandled exception before `finally`) leave committed data in the database.
- The `TestCleanup.deleteAll` function catches exceptions per-table, so a single FK violation during cleanup doesn't block other deletes. But the failed delete's data remains.
- There is no periodic database reset or truncation to clear accumulated residue.

For a GAAP accounting system where correctness is paramount, testing against real committed data is arguably correct. But the tradeoff is that the database accumulates test residue over time, which degrades the reliability of any test that queries globally.

**When:** This is not a problem for tests that only assert on data they created (the majority). It IS a problem for reporting tests (Finding 1) and list-all operations.

---

## Growth Trajectory: What Breaks First

Given that Nightshift agents are adding features in parallel:

1. **Immediate risk (now -- 1,000 tests):** Reporting tests that assert on section totals or retained earnings will intermittently fail when concurrent tests create revenue/expense entries. This is Finding 1 + Finding 7 combined.

2. **Near-term risk (1,000 -- 1,500 tests):** The duplicate postEntry helper (Finding 5) will diverge as different agents modify different copies. A JournalEntryService API change will cause a cascade of compilation errors across 6 files.

3. **Medium-term risk (1,500 -- 2,500 tests):** Test residue accumulation (Finding 2) will cause list/query operations to return unexpected data. Tests that worked in isolation will fail in full-suite runs.

4. **Longer-term risk (2,500+ tests):** The hardcoded seed ID pattern (Finding 3) will break when the schema evolves enough to require seed data changes. The blast radius is 10+ files.

---

## Recommendations (ranked by urgency)

1. **Scope reporting queries in tests.** Either filter report output to test-created account IDs (already done in some tests), or introduce a per-test fiscal period + account prefix filtering convention that all reporting tests follow. Never assert on section totals without filtering.

2. **Extract postEntry and seed type IDs to TestHelpers.** One definition, used everywhere. Eliminates the copy-paste divergence risk.

3. **Migrate PortfolioStructuralConstraintsTests to use PortfolioTracker.** The manual cleanup pattern should not be the template for new portfolio tests.

4. **Replace hardcoded account_type IDs with lookups.** A shared `SeedData.accountTypeId conn "asset"` function that queries by name is resilient to ID changes.

5. **Add a database-state assertion to CI.** After the full test suite runs, assert that no test-created data remains (e.g., no account_type names containing the GUID prefix pattern). This catches cleanup failures before they accumulate.

6. **Consider adding xunit.runner.json** with explicit parallel configuration and max threads, so the parallelism behavior is documented and controllable rather than defaulted.

---

*Signed: Test Harness Auditor*
*Assessment date: 2026-04-07*
*Test suite snapshot: ~800+ tests, 43 source files, 2 tracker types, 0 collection fixtures*
