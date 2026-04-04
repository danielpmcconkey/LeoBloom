# Brainstorm: LeoBloom Test Architecture Overhaul

**Date:** 2026-04-04
**Status:** Brainstormed
**Participants:** Dan, BD

---

## What We're Building

A new test architecture for LeoBloom that replaces the current TickSpec-based BDD
test harness with plain xUnit tests backed by an NpgsqlDataSource singleton for
connection management. Gherkin .feature files remain as the source-of-truth spec
but are no longer directly executed by the test framework.

## Why This Approach

### Problems with the current architecture

1. **Connection leaks crash the test suite.** Tests manually manage connection
   lifecycle with `let conn` (no `use`). Cleanup only runs if TickSpec reaches
   the Then step's `finally` block. Any exception in a Given or When step leaks
   a connection, poisons the pool, and cascades into 30-second timeouts across
   subsequent tests. We saw 23 of 112 tests fail this way.

2. **TickSpec is slow and fragile.** 112 BDD scenarios take 10+ minutes vs.
   PersonalFinance's 679 DB tests in 13 seconds. Step matching is regex-based
   with runtime ambiguity errors instead of compile-time safety. Code flow is
   hard to follow — no IDE navigation from Gherkin to implementation.

3. **Two test architectures.** Domain.Tests uses plain xUnit. Dal.Tests uses
   TickSpec. Two patterns to maintain, learn, and debug.

4. **Test-only code in production.** JournalEntryService and
   AccountBalanceService each have dual function signatures — e.g. `post` (owns
   connection) and `postInTransaction` (borrows transaction, comment says
   "for testing"). Production code should not know tests exist.

5. **Hardcoded test data causes lock contention.** OpsStepDefinitions hardcodes
   `account_type_id = 1` across all transfer tests. Leaked transactions hold
   locks on that row, blocking every subsequent transfer test.

6. **Copy-paste boilerplate.** Four step definition files each have their own
   `openContext`, `cleanup`, context record type, and duplicate `insertAccountType`
   / `insertAccount` / `insertFiscalPeriod` helpers.

### Why Gherkin stays (as spec, not as test harness)

Dan's thesis: BDD + AI is the experiment. Gherkin is the permanent artifact that
survives a full rewrite. If the code passes all tests that honor the spec, it's
functionally equivalent to the old code. Gherkin is the industry standard for BDD
specs. It does not need to be what the test harness revolves around.

## Key Decisions

### 1. Gherkin is spec, not test framework

- `.feature` files remain in `Specs/` as the source of truth
- Tests are plain xUnit (Fact/Theory) — same as Domain.Tests
- Each Gherkin scenario has a tag like `@AB-003`
- Each test function is annotated with the Gherkin ID(s) it covers
- The mapping is many-to-many: one Theory can cover many scenario IDs, one
  scenario may need multiple test methods
- CI check validates every Gherkin scenario ID has at least one test

### 2. NpgsqlDataSource singleton in LeoBloom.Dal

- Lazy-initialized on first access (F#'s `lazy` keyword)
- Resolves connection string from LEOBLOOM_ENV + appsettings, builds
  NpgsqlDataSource once, caches forever — same pattern as PgContext's static
  NpgsqlDataSource in PersonalFinance
- **Connection string resolution and the NpgsqlDataSource instance are both
  private to the DataSource module.** The only public function is
  `DataSource.openConnection()`. No code anywhere in the solution can access
  the connection string or the data source directly.
- `ConnectionString.resolve` and manual `new NpgsqlConnection(connStr)` are
  removed from all production and test code
- Both production and test code use the same singleton pool

### 3. One API surface — no test-only functions in production code

- All code — services, tests, migrations — gets connections from the
  DataSource singleton. No special connection management anywhere.
- `postInTransaction`, `voidInTransaction`, `getBalanceByIdInTransaction`,
  `getBalanceByCodeInTransaction` are deleted
- Tests call the real production functions (`post`, `voidEntry`,
  `getBalanceById`, `getBalanceByCode`) — same code path as production
- No function in `Src/LeoBloom.Dal/` should exist solely for testing

### 4. Each test is one atomic function with commit + delete cleanup

- Each test is a single xUnit Fact/Theory function — arrange, act, assert,
  cleanup all in one scope. No splitting logic across multiple functions like
  TickSpec's Given/When/Then handlers. This is why TickSpec was dangerous:
  AI lost sight of the cleanup pattern when logic was scattered across separate
  functions with no compiler-enforced link between them.
- One `try`/`finally` per test. The `finally` cleans up by deleting rows
  using tracked IDs. Simple, visible, impossible to miss.
- Tests call real production code, which commits real data
- Same proven pattern as PersonalFinance's 679 DB tests
- `use` on connections guarantees disposal regardless of exceptions —
  no more orphaned connections from missed cleanup paths

### 5. One test pattern for everything

- Structural constraint tests (NOT NULL, FK, UNIQUE) and behavioral tests
  (post, void, balance) use the same architecture
- Same DataSource, same cleanup pattern, same test helpers
- No special-casing by test category

### 6. Design for parallelism

- Tests generate unique identifiers for test data (no hardcoded codes like
  `'TF01'` or assumptions like `account_type_id = 1`)
- Each test tracks and cleans up only its own data
- Connection pool handles concurrent access
- `DisableTestParallelization` can be removed once migration is complete

## What Gets Deleted

- All TickSpec step definition files (SharedSteps.fs, PostJournalEntryStepDefinitions.fs,
  VoidJournalEntryStepDefinitions.fs, AccountBalanceStepDefinitions.fs,
  OpsStepDefinitions.fs, LedgerStepDefinitions.fs, DeleteRestrictionStepDefinitions.fs)
- FeatureFixture.fs (TickSpec test runner)
- `postInTransaction`, `voidInTransaction`, `getBalanceByIdInTransaction`,
  `getBalanceByCodeInTransaction` from service files
- `ConnectionString.resolve` function (replaced by DataSource module)
- TickSpec NuGet dependency
- The TickSpec ambiguity checker hook (`/workspace/.claude/hooks/check-tickspec-ambiguity.sh`
  and its wiring in `/workspace/.claude/settings.local.json`) — no more regex-matched
  step definitions means no ambiguity to check
- The git pre-commit ambiguity hook (`.git/hooks/pre-commit`)
- `Scripts/check-tickspec-ambiguity.sh` from the LeoBloom repo
- `BdsNotes/tickspec-step-patterns.md` (TickSpec research doc, no longer relevant)

## What Gets Created

- `Src/LeoBloom.Dal/DataSource.fs` — singleton NpgsqlDataSource module
- New xUnit test files in Dal.Tests replacing step definitions
- Shared test helper module for DB cleanup and unique data generation
- CI check script that parses .feature files for scenario IDs and validates
  test coverage annotations
