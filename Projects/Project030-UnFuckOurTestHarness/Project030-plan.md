---
title: "refactor: Unfuck the test harness"
type: refactor
status: active
date: 2026-04-04
origin: Projects/Project030-UnFuckOurTestHarness/Project030-brainstorm.md
deepened: 2026-04-04
---

# Unfuck the Test Harness

## Enhancement Summary

**Deepened on:** 2026-04-04
**Agents used:** architecture-strategist, code-simplicity-reviewer,
pattern-recognition-specialist, performance-oracle, data-integrity-guardian,
best-practices-researcher, framework-docs-researcher

### Key Improvements from Deepening
1. DataSource uses eager init (not lazy) — fails loud at module load, no
   cached exception problem
2. TestCleanup implemented as an F# module (not OOP class) to match
   codebase conventions
3. Cleanup failures are logged to test output, not silently swallowed
4. DataSource builder configured with `IncludeErrorDetail=true` and
   `ApplicationName="LeoBloom"` for debuggability
5. Phase ordering fix: consolidate test projects (Phase 3) before
   refactoring production signatures (Phase 2) — prevents a broken-build
   gap where old tests can't compile but new tests don't exist yet
6. Prod safety guard (`SELECT current_database()`) integrated into
   DataSource eager init — comes for free since we're already opening
   a connection at module load

---

## Overview

Replace LeoBloom's TickSpec-based BDD test execution with plain xUnit tests
backed by an NpgsqlDataSource singleton. Gherkin .feature files stay as the
source-of-truth spec. Tests become atomic xUnit Fact/Theory functions that
call real production code, commit real data, and clean up via DELETE in
finally blocks.

This is a full rewrite of 112 Dal tests, a production API change to
service function signatures, consolidation of Domain.Tests and Dal.Tests
into one test project (LeoBloom.Tests), and removal of all TickSpec
infrastructure.

See brainstorm: `Projects/Project030-UnFuckOurTestHarness/Project030-brainstorm.md`

## Problem Statement

Six compounding problems documented in the brainstorm:

1. Connection leaks crash the test suite (23 of 112 failures from pool poisoning)
2. TickSpec is slow and fragile (10+ min vs PersonalFinance's 13 sec for 679 tests)
3. Two test projects with two architectures (Domain.Tests = xUnit, Dal.Tests = TickSpec)
4. Test-only `*InTransaction` functions in production code
5. Hardcoded test data causes lock contention
6. Copy-paste boilerplate across 4 step definition files

## Proposed Solution

Ten-phase migration that replaces the foundation first, consolidates test
projects, rewrites tests, then cleans up.

**Phase ordering note:** Phase 3 (consolidate test projects) should execute
before Phase 2 (refactor production services). Deleting `ConnectionString.fs`
in Phase 2 breaks the old `Dal.Tests` project. If Phase 3 runs first,
Domain tests are already in the new project and stay green throughout.
Phases are numbered by logical grouping, not strict execution order.

## Technical Approach

### Phase 1: DataSource Singleton

Create `Src/LeoBloom.Dal/DataSource.fs`.

**What it does:**
- Private function resolves connection string from LEOBLOOM_ENV + appsettings.
  **This is a behavioral change from `ConnectionString.resolve`:** the current
  code takes an explicit `basePath` parameter, while the DataSource module
  uses `AppContext.BaseDirectory` implicitly. This is intentional — no caller
  should need to know or pass the base path.
- **Eager init** — the NpgsqlDataSource is built at module load time, not
  lazily on first access. If the config is missing, the env var is wrong,
  or the database is unreachable, the module fails to load immediately with
  a clear error message. This avoids the `Lazy<T>` problem where a failed
  init caches the exception and every subsequent call re-throws the same
  cryptic `TargetInvocationException`.
- One public function: `DataSource.openConnection()` — returns a pooled,
  already-open `NpgsqlConnection` (uses `dataSource.OpenConnection()`, not
  `CreateConnection()`).
- Nothing else is public. Connection string, data source instance, and
  resolution logic are all private to this module.

**DataSource builder configuration:**
- `IncludeErrorDetail = true` — includes column names and values in Postgres
  error messages. Essential for debugging constraint violations. This is a
  dev database — never enable this in prod.
- `ApplicationName = "LeoBloom"` — identifies connections in
  `pg_stat_activity`. Trivial to add, invaluable for debugging.
- `Timeout = 5` — connection acquisition timeout. If a test waits 5 seconds
  for a pool connection, something is catastrophically wrong. Fast-fail
  instead of the default 15-second stall. (Dan: change this back if it
  causes spurious failures. It's a dev convenience, not a design decision.)
- `CommandTimeout = 5` — same reasoning as above for query execution.

**Prod safety guard (integrated into eager init):**
Since the module eagerly opens a connection at load time, run
`SELECT current_database()` right there. If it returns anything other than
`leobloom_dev`, throw with a clear message. This comes for free — we're
already validating the connection works.

**fsproj placement:** `DataSource.fs` replaces `ConnectionString.fs` in the
same position in the compile order.

**Migrations note:** `Src/LeoBloom.Migrations/Program.fs` also calls
`ConnectionString.resolve`. Migrations runs as a separate executable, so
`AppContext.BaseDirectory` will resolve to its own output directory with its
own `appsettings.{env}.json` (which has `search_path=migrondi`). The
DataSource module handles this correctly — each process gets its own eager
init reading from its own config. Migrations switches to
`DataSource.openConnection()` like everything else.

**Note for future API work:** The API project should also use
`DataSource.openConnection()` from this module — not register its own
`NpgsqlDataSource` via DI. One connection strategy for the whole solution.

**Success criteria:**
- DataSource module compiles in LeoBloom.Dal
- `DataSource.openConnection()` returns a working connection
- Module fails loud with a clear message if config is missing or DB is wrong
- Existing code still works (ConnectionString.fs still exists temporarily)

---

### Phase 2: Refactor Production Services

**Execute after Phase 3** (see phase ordering note above).

Update `JournalEntryService.fs` and `AccountBalanceService.fs`.

**Signature changes:**

| Function | Before | After |
|----------|--------|-------|
| `post` | `post (basePath: string) (cmd)` | `post (cmd)` |
| `voidEntry` | `voidEntry (basePath: string) (cmd)` | `voidEntry (cmd)` |
| `getBalanceById` | `getBalanceById (basePath: string) (acctId) (date)` | `getBalanceById (acctId) (date)` |
| `getBalanceByCode` | `getBalanceByCode (basePath: string) (code) (date)` | `getBalanceByCode (code) (date)` |

The `basePath` parameter is removed. Internally, these functions call
`DataSource.openConnection()` instead of `ConnectionString.resolve basePath`
→ `new NpgsqlConnection(connStr)`.

**Blast radius check:** No external callers. The API project exists but
doesn't reference these functions yet. Only callers are the test step
definitions (which are being deleted anyway) and the `*InTransaction`
variants (also being deleted).

**Delete these test-only functions:**
- `JournalEntryService.postInTransaction`
- `JournalEntryService.voidInTransaction`
- `AccountBalanceService.getBalanceByIdInTransaction`
- `AccountBalanceService.getBalanceByCodeInTransaction`

**Delete:** `Src/LeoBloom.Dal/ConnectionString.fs` (all consumers now use
DataSource).

**Update:** `Src/LeoBloom.Migrations/Program.fs` to use
`DataSource.openConnection()`.

**Success criteria:**
- Production services compile without `basePath`
- Migrations still runs
- `ConnectionString.fs` is deleted
- No `*InTransaction` functions remain in production code

---

### Phase 3: Consolidate Test Projects

**Execute before Phase 2** (see phase ordering note above).

Merge `LeoBloom.Domain.Tests` and `LeoBloom.Dal.Tests` into a single
`LeoBloom.Tests` project.

**Why:** One test project means shared helpers are accessible to all tests
without cross-project references. One fsproj to maintain. One test assembly.
Domain tests (pure, no DB) and Dal tests (DB-backed) coexist — they're
just different files in the same project.

**Steps:**
- Create `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` referencing both
  `LeoBloom.Domain` and `LeoBloom.Dal`
- Move `Src/LeoBloom.Domain.Tests/Tests.fs` into `LeoBloom.Tests/` (rename
  to `DomainTests.fs` for clarity)
- **Update the module declaration** in `DomainTests.fs` from
  `module LeoBloom.Domain.Tests.DomainTests` to
  `module LeoBloom.Tests.DomainTests` — compile error if you forget
- Do NOT move the old TickSpec step definition files — those are being
  rewritten, not migrated
- Remove `LeoBloom.Domain.Tests` and `LeoBloom.Dal.Tests` projects from
  the solution
- Update the sln file
- **Do NOT touch** the other `Src/` directories (`LeoBloom.Ledger`,
  `LeoBloom.Ops`, `LeoBloom.Data`, etc.) — those are future projects,
  not vestigial

**fsproj compile order (initial):**
1. `TestHelpers.fs` (created in Phase 4)
2. `DomainTests.fs` (moved from Domain.Tests)
3. Dal test files (added in Phases 6-7)

**Success criteria:**
- `dotnet test` runs Domain tests from the new project
- Old test projects removed from solution
- Solution compiles clean

---

### Phase 4: Shared Test Helpers

Create `Src/LeoBloom.Tests/TestHelpers.fs` — compiled first in the
fsproj, before any test files.

**Unique data generation:**

Every test generates unique identifiers for its test data. No hardcoded
codes like `'TF01'` or assumptions like `account_type_id = 1`.

```fsharp
module TestData =
    /// Generate a unique prefix for this test (e.g., "t7f3a2")
    let uniquePrefix () = Guid.NewGuid().ToString("N").[..5]

    /// Generate a unique account code
    let accountCode prefix = sprintf "%s_ACCT" prefix

    /// Generate a unique account type name
    let accountTypeName prefix = sprintf "%s_type" prefix
```

**Cleanup with correct FK ordering:**

The FK dependency chain for LeoBloom's schema requires deletes in this order:

1. `ledger.journal_entry_reference` (FK → journal_entry)
2. `ledger.journal_entry_line` (FK → journal_entry, account)
3. `ops.obligation_instance` (FK → obligation_agreement, journal_entry)
4. `ops.transfer` (FK → account, journal_entry)
5. `ops.invoice` (FK → fiscal_period)
6. `ledger.journal_entry` (FK → fiscal_period)
7. `ops.obligation_agreement` (FK → account)
8. `ledger.account` (FK → account_type, self-ref parent_code)
9. `ledger.account_type`
10. `ledger.fiscal_period`

**Implementation as an F# module** (not a class — matches the codebase's
functional-module convention):

```fsharp
module TestCleanup =
    type Tracker =
        { mutable JournalEntryIds: int list
          mutable AccountIds: int list
          mutable AccountTypeIds: int list
          mutable FiscalPeriodIds: int list
          // ... etc for each table
          Connection: NpgsqlConnection }

    let create (conn: NpgsqlConnection) =
        { JournalEntryIds = []; AccountIds = []; AccountTypeIds = []
          FiscalPeriodIds = []; Connection = conn }

    let trackJournalEntry id tracker =
        tracker.JournalEntryIds <- id :: tracker.JournalEntryIds

    let trackAccount id tracker =
        tracker.AccountIds <- id :: tracker.AccountIds

    // ... etc

    /// Delete all tracked rows in FK-safe order.
    /// Catches exceptions per-table so one failure doesn't block the rest.
    /// **Logs failures to stderr** — silent swallowing is the exact class
    /// of bug this project exists to fix.
    let deleteAll tracker =
        let tryDelete table ids =
            try
                // DELETE FROM {table} WHERE id = ANY(@ids)
                ...
            with ex ->
                eprintfn "[TestCleanup] FAILED to clean %s: %s" table ex.Message
        // Order matters — children before parents
        tryDelete "ledger.journal_entry_reference" tracker.JournalEntryIds
        tryDelete "ledger.journal_entry_line" tracker.JournalEntryIds
        // ... etc in FK order
```

**Prod safety guard:** Integrated into DataSource eager init (Phase 1).
No separate test-level guard needed.

**Shared insert helpers:**

Consolidate the duplicated `insertAccountType`, `insertAccount`,
`insertFiscalPeriod` helpers into `TestHelpers.fs`. Each helper returns
the inserted ID and registers it with the `TestCleanup` tracker.

**Parallel test isolation constraint:** All production queries and test
helpers must query by specific IDs (e.g., `WHERE id = @id`), never by
unscoped predicates (e.g., `WHERE is_open = true`). Unscoped queries
will see data from other parallel tests and produce flaky failures.
This constraint will be formally documented in Project 032 (Test Author
Agent Blueprint).

**Success criteria:**
- TestHelpers.fs compiles in LeoBloom.Tests
- TestCleanup deletes in correct FK order
- Cleanup failures are logged to test output, not silently swallowed
- No duplicated helper code across test files

---

### Phase 5: Tag Gherkin Scenarios

Add `@ID` tags to all behavioral feature files. Structural files already
have tags.

**Existing tag convention:**
- `@FT-LSC-001` through `@FT-LSC-029` (Ledger Structural Constraints)
- `@FT-OSC-001` through `@FT-OSC-022` (Ops Structural Constraints) — has gaps, that's fine
- `@FT-DR-001` through `@FT-DR-018` (Delete Restrictions) — has gaps

**New tags for behavioral features:**
- `@FT-PJE-001` through `@FT-PJE-0XX` (PostJournalEntry — 20 scenarios)
- `@FT-VJE-001` through `@FT-VJE-0XX` (VoidJournalEntry — 8 scenarios)
- `@FT-AB-001` through `@FT-AB-0XX` (AccountBalance — 11 scenarios)

IDs are stable identifiers, not sequential. Gaps are fine. Never renumber.

**Success criteria:**
- Every scenario in every .feature file has an `@FT-*` tag
- No duplicate tags across the entire Specs/ directory

---

### Phase 6: Rewrite Structural Tests

Rewrite 65 structural constraint scenarios (Ledger + Ops + DeleteRestriction)
as plain xUnit.

**Pattern:** Each test is one atomic function. Arrange → Act → Assert →
Cleanup, all in one scope.

```fsharp
[<Fact>]
[<Trait("GherkinId", "FT-LSC-001")>]
let ``account_type name is NOT NULL`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.account_type (name, normal_balance) VALUES (NULL, 'debit')",
            conn)
        try
            cmd.ExecuteNonQuery() |> ignore
            Assert.Fail("Expected NOT NULL violation")
        with :? PostgresException as ex ->
            Assert.Equal("23502", ex.SqlState)
    finally
        TestCleanup.deleteAll tracker
```

For structural tests that only test failure modes (NOT NULL, FK, UNIQUE
violations), the cleanup has nothing to delete — the INSERT was rejected.
The `try`/`finally` is still there for consistency and for the few tests
that do insert setup data.

**Annotation:** Each test carries `[<Trait("GherkinId", "FT-XXX-NNN")>]`
matching the Gherkin scenario tag. One Theory covering multiple scenarios
carries multiple Trait attributes.

**File organization:**
- `Src/LeoBloom.Tests/LedgerConstraintTests.fs`
- `Src/LeoBloom.Tests/OpsConstraintTests.fs`
- `Src/LeoBloom.Tests/DeleteRestrictionTests.fs`

**Success criteria:**
- All 65 structural scenarios covered by passing xUnit tests
- Every test has a GherkinId Trait matching a scenario tag
- No TickSpec step definitions referenced

---

### Phase 7: Rewrite Behavioral Tests

Rewrite 39 behavioral scenarios (Post + Void + AccountBalance) as plain
xUnit.

**Pattern:** Same atomic function pattern, but these call real production
code (`post`, `voidEntry`, `getBalanceById`, etc.) and need full cleanup.

```fsharp
[<Fact>]
[<Trait("GherkinId", "FT-PJE-001")>]
let ``simple two-line entry posts successfully`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        // Arrange — insert prerequisite data
        let atId = TestHelpers.insertAccountType conn tracker "asset"
        let acct1 = TestHelpers.insertAccount conn tracker "1000" atId true
        let acct2 = TestHelpers.insertAccount conn tracker "2000" atId true
        let fpId = TestHelpers.insertFiscalPeriod conn tracker
                       (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

        // Act — call real production code
        let cmd = { entryDate = DateOnly(2026, 3, 15)
                    description = "Test entry"
                    source = Some "manual"
                    fiscalPeriodId = fpId
                    lines = [ { accountId = acct1; amount = 100m
                                entryType = EntryType.Debit; memo = None }
                              { accountId = acct2; amount = 100m
                                entryType = EntryType.Credit; memo = None } ]
                    references = [] }
        let result = JournalEntryService.post cmd

        // Assert
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            Assert.True(posted.entry.id > 0)
            Assert.Equal(2, List.length posted.lines)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally
        TestCleanup.deleteAll tracker
```

**Two connections per behavioral test:** The test opens a connection for
setup and cleanup. The production function being tested (`post`, `voidEntry`,
etc.) opens its own separate connection internally via
`DataSource.openConnection()`, does its work, commits, and returns. The
test then tracks the returned IDs for cleanup. This is unavoidable — the
whole point is that tests call the real production code path, which owns
its own connection lifecycle.

**File organization:**
- `Src/LeoBloom.Tests/PostJournalEntryTests.fs`
- `Src/LeoBloom.Tests/VoidJournalEntryTests.fs`
- `Src/LeoBloom.Tests/AccountBalanceTests.fs`

**Success criteria:**
- All 39 behavioral scenarios covered by passing xUnit tests
- Every test has GherkinId Traits
- Tests call real production functions (not InTransaction variants)
- Cleanup deletes all test data in FK-safe order

---

### Phase 8: Delete TickSpec Infrastructure

Remove everything related to TickSpec execution.

**From LeoBloom repo:**
- Entire `Src/LeoBloom.Dal.Tests/` project directory (already migrated to
  LeoBloom.Tests in Phase 3)
- Entire `Src/LeoBloom.Domain.Tests/` project directory (already migrated
  to LeoBloom.Tests in Phase 3)
- `Scripts/check-tickspec-ambiguity.sh`
- `BdsNotes/tickspec-step-patterns.md`

**From workspace (outside LeoBloom repo):**
- `/workspace/.claude/hooks/check-tickspec-ambiguity.sh`
- TickSpec hook wiring in `/workspace/.claude/settings.local.json`

**From LeoBloom repo git hooks:**
- `.git/hooks/pre-commit` (the ambiguity checker)

**Do NOT touch** the directories in `Src/` that aren't in the solution
(`LeoBloom.Ledger`, `LeoBloom.Ops`, `LeoBloom.Data`, etc.) — those are
future projects.

**Verify LeoBloom.Tests fsproj** has no TickSpec references and compile
order is clean:
1. `TestHelpers.fs`
2. `DomainTests.fs`
3. `LedgerConstraintTests.fs`
4. `OpsConstraintTests.fs`
5. `DeleteRestrictionTests.fs`
6. `PostJournalEntryTests.fs`
7. `VoidJournalEntryTests.fs`
8. `AccountBalanceTests.fs`

**Success criteria:**
- No TickSpec references anywhere in the solution
- `dotnet test` runs all tests without TickSpec
- No ambiguity checker hooks remain

---

### Phase 9: CI Coverage Check

Create a script that validates every Gherkin scenario ID has at least one
test, and every test references a valid Gherkin scenario ID.

**Mechanism:** `[<Trait("GherkinId", "FT-XXX-NNN")>]` on xUnit tests.

**The script:**
1. Parse all `.feature` files in `Specs/` for `@FT-*` tags → set of
   scenario IDs
2. Parse test source files for `Trait("GherkinId", "*")` → set of
   referenced IDs
3. Diff: scenarios without tests = FAIL. Tests referencing nonexistent
   scenarios = WARN (stale mapping).

**Location:** `Scripts/check-gherkin-coverage.fsx` (F# script preferred over
bash — structured parsing of both Gherkin and F# source is cleaner in F#
and the output can be JSON for machine consumption).

**Future-proofing:** This script is the foundation for a planned follow-on
project: a pre-push hook that invokes an agent to review test changes,
confirm the spirit of Gherkin scenarios is still honored, and validate
full BDD coverage. Design the script's output to be agent-consumable:
- Return structured data (JSON or clearly-delimited text), not just
  pass/fail
- Include the mapping: which test covers which scenario, and vice versa
- Include the scenario text alongside IDs so an agent can evaluate whether
  the test actually tests what the Gherkin describes
- Exit codes: 0 = full coverage, 1 = gaps found, 2 = stale references

**Success criteria:**
- Script passes when all scenarios are covered
- Script fails (exit 1) when a scenario has no test
- Script warns (exit 2) on stale test references
- Output is structured and agent-parseable

---

### Phase 10: Enable Parallelism + Final Verification

**Remove `DisableTestParallelization`** from the test assembly. With unique
test data per test and connection pooling, tests can safely run in parallel.

**Final verification:**
- Run full `dotnet test` — all tests pass
- Run full `dotnet test` 3x in a row — no flaky failures from leaked state
- Verify no orphaned test data in `leobloom_dev` after test runs (query
  for rows with test-prefixed unique data that shouldn't exist)

**Success criteria:**
- All ~148 tests pass (36 Domain + ~112 Dal)
- Suite completes in under 30 seconds
- No DisableTestParallelization attribute
- Database clean after test run (no orphaned rows)

---

## Acceptance Criteria

### Functional
- [ ] All 112 Dal scenarios covered by plain xUnit Fact/Theory tests
- [ ] Every Gherkin scenario has an `@FT-*` tag
- [ ] Every xUnit test has `[<Trait("GherkinId", "FT-*")>]` annotations
- [ ] CI script validates bidirectional Gherkin-to-test mapping
- [ ] Tests call real production functions, not test-only variants
- [ ] No test-only functions in production code
- [ ] No TickSpec dependency in the solution
- [ ] Domain and Dal tests consolidated into single LeoBloom.Tests project

### Non-Functional
- [ ] Full test suite completes in under 30 seconds
- [ ] Tests can run in parallel without interference
- [ ] No connection leaks (repeated runs don't degrade)
- [ ] DataSource module fails loud on init if config or DB is wrong

### Quality Gates
- [ ] `dotnet test` passes 3 consecutive runs
- [ ] Gherkin coverage check passes
- [ ] No orphaned test data after runs
- [ ] Cleanup failures are logged, not silently swallowed

## Dependencies & Risks

**Risk: Cleanup ordering bugs.** If the TestCleanup delete order is wrong,
tests leave orphaned data. Mitigation: cleanup failures are logged to stderr
(not silently swallowed). Run a "check for orphans" query after the test
suite. Worst case: `TRUNCATE` the dev tables and move on.

**Risk: Production signature change breaks future API work.** The `post`,
`voidEntry`, etc. functions lose their `basePath` parameter. If someone
builds API endpoints before this migration, they'll need updating.
Mitigation: no current callers exist outside tests. Migration happens first.

**Risk: Behavioral test setup is verbose.** Each behavioral test inlines
all setup (insert types, accounts, periods, entries). Could lead to long
test functions. Mitigation: TestHelpers provides concise helper functions
that handle both insertion and cleanup tracking.

**Dependency: Project 007 (AccountBalance) uncommitted work.** The
AccountBalance code is sitting uncommitted on main. This migration rewrites
its tests and modifies its service. Phase 2 must account for the
AccountBalance service changes. Phase 6 must port the AccountBalance
scenarios.

**Related projects:**
- Project 031 (Foundational Logging) — once logging infrastructure exists,
  TestCleanup should use it instead of `eprintfn`
- Project 032 (Test Author Agent Blueprint) — will codify the test
  conventions established here (unique data, query by ID, FK cleanup order,
  parallel isolation) so future test-building agents carry institutional
  knowledge

## Sources & References

### Origin
- **Brainstorm:** `Projects/Project030-UnFuckOurTestHarness/Project030-brainstorm.md`
  Key decisions carried forward: DataSource singleton with private internals,
  atomic test functions with commit+delete, one pattern for all tests,
  Gherkin as spec not framework.

### Internal References
- PersonalFinance PgContext (reference pattern): `/workspace/PersonalFinance/Lib/PgContext.cs:34-50`
- Domain.Tests (target xUnit pattern): `Src/LeoBloom.Domain.Tests/Tests.fs`
- Current connection resolver: `Src/LeoBloom.Dal/ConnectionString.fs`
- Current services: `Src/LeoBloom.Dal/JournalEntryService.fs`, `Src/LeoBloom.Dal/AccountBalanceService.fs`
- Wakeup context: `BdsNotes/wakeup-2026-04-04a.md`

### External References (from deepening research)
- [Npgsql DataSource API](https://www.npgsql.org/doc/basic-usage.html)
- [Npgsql Connection Pooling](https://www.npgsql.org/doc/api/Npgsql.NpgsqlConnectionStringBuilder.html)
- [xUnit Shared Context / Fixtures](https://xunit.net/docs/shared-context)
- [xUnit Parallel Test Execution](https://github.com/xunit/xunit.net/blob/main/site/docs/running-tests-in-parallel.md)
- [F# Lazy Initialization / Lazy<T>](https://learn.microsoft.com/en-us/dotnet/framework/performance/lazy-initialization)
