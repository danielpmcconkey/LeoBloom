---
title: "refactor: Unfuck the test harness"
type: refactor
status: active
date: 2026-04-04
origin: Projects/Project030-UnFuckOurTestHarness/Project030-brainstorm.md
---

# Unfuck the Test Harness

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

## Technical Approach

### Phase 1: DataSource Singleton

Create `Src/LeoBloom.Dal/DataSource.fs`.

**What it does:**
- Private function resolves connection string from LEOBLOOM_ENV + appsettings
  (same logic as current `ConnectionString.resolve`, using
  `AppContext.BaseDirectory` to find the right config file)
- Private `lazy` NpgsqlDataSource — built once on first access, cached forever
- One public function: `DataSource.openConnection()` — returns a pooled
  `NpgsqlConnection`
- Nothing else is public. Connection string, data source instance, and
  resolution logic are all private to this module.

**fsproj placement:** `DataSource.fs` goes in the same position as
`ConnectionString.fs` (or immediately after it during the transition).

**Migrations note:** `Src/LeoBloom.Migrations/Program.fs` also calls
`ConnectionString.resolve`. Migrations runs as a separate executable, so
`AppContext.BaseDirectory` will resolve to its own output directory with its
own `appsettings.{env}.json` (which has `search_path=migrondi`). The
DataSource singleton handles this correctly — it reads from wherever the
current executable lives. Migrations switches to `DataSource.openConnection()`
like everything else.

**Success criteria:**
- DataSource module compiles in LeoBloom.Dal
- `DataSource.openConnection()` returns a working connection
- Existing code still works (ConnectionString.fs still exists temporarily)

---

### Phase 2: Refactor Production Services

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
- Do NOT move the old TickSpec step definition files — those are being
  rewritten, not migrated
- Remove `LeoBloom.Domain.Tests` and `LeoBloom.Dal.Tests` projects from
  the solution
- Update the sln file

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

Getting this wrong means silent cleanup failures (FK violations swallowed in
finally blocks) → orphaned test data → cross-test interference. This is the
same class of bug we're fixing, so the cleanup must be correct.

```fsharp
/// Tracks all IDs created during a test for ordered cleanup.
type TestCleanup(conn: NpgsqlConnection) =
    let mutable journalEntryIds: int list = []
    let mutable accountIds: int list = []
    let mutable accountTypeIds: int list = []
    let mutable fiscalPeriodIds: int list = []
    let mutable obligationAgreementIds: int list = []
    // ... etc for each table

    member this.TrackJournalEntry(id) = journalEntryIds <- id :: journalEntryIds
    member this.TrackAccount(id) = accountIds <- id :: accountIds
    // ... etc

    /// Delete all tracked rows in FK-safe order.
    /// Swallows exceptions per-table so one failure doesn't block the rest.
    member this.DeleteAll() =
        // Order matters — children before parents
        // 1. journal_entry_reference
        // 2. journal_entry_line
        // 3. ops tables
        // 4. journal_entry
        // 5. account
        // 6. account_type
        // 7. fiscal_period
        ...
```

**Prod safety guard (low priority — implement only if easy):**

The prod database password isn't in this container, and test projects only
have `appsettings.Development.json`. The risk is near-zero. But if it's
trivial to add a one-time `SELECT current_database()` check at assembly
load, do it. If it requires xUnit fixture plumbing or complicates the
architecture, skip it.

**Shared insert helpers:**

Consolidate the duplicated `insertAccountType`, `insertAccount`,
`insertFiscalPeriod` helpers into `TestHelpers.fs`. Each helper returns
the inserted ID and registers it with the `TestCleanup` tracker.

**Success criteria:**
- TestHelpers.fs compiles in LeoBloom.Tests
- TestCleanup deletes in correct FK order
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
    let cleanup = TestCleanup(conn)
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
        cleanup.DeleteAll()
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
    let cleanup = TestCleanup(conn)
    try
        // Arrange — insert prerequisite data
        let atId = TestHelpers.insertAccountType conn cleanup "asset"
        let acct1 = TestHelpers.insertAccount conn cleanup "1000" atId true
        let acct2 = TestHelpers.insertAccount conn cleanup "2000" atId true
        let fpId = TestHelpers.insertFiscalPeriod conn cleanup
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
            cleanup.TrackJournalEntry(posted.entry.id)
            Assert.True(posted.entry.id > 0)
            Assert.Equal(2, List.length posted.lines)
        | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
    finally
        cleanup.DeleteAll()
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
2. Parse test assemblies (or source files) for `Trait("GherkinId", "*")`
   → set of referenced IDs
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
- Compare runtime against current 10+ minute baseline
- Verify no orphaned test data in `leobloom_dev` after test runs

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
- [ ] Prod safety guard prevents test execution against wrong database (low priority — skip if not trivial)

### Quality Gates
- [ ] `dotnet test` passes 3 consecutive runs
- [ ] Gherkin coverage check passes
- [ ] No orphaned test data after runs

## Dependencies & Risks

**Risk: Cleanup ordering bugs.** If the TestCleanup delete order is wrong,
tests silently leave orphaned data. Mitigation: verify FK chain against
the actual schema constraints. Run a "check for orphans" query after the
test suite.

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
