# Project 048 — Test Cleanup

**PO Kickoff**
**Date:** 2026-04-05
**Status:** In Progress
**Epic:** K — Code Audit Remediation
**Depends On:** None
**Blocks:** Nothing

---

## Project Summary

The 2026-04-05 test audit identified 26 dead tests across 4 files. These
tests fall into three categories:

1. **Tautologies** — `Assert.True(true)` with a comment explaining why it's
   true. These do not test anything.
2. **Ghost guards** — conditions that cannot recur because the one-time work
   they guarded is long complete (the Dal-to-Utilities rename shipped and
   the old directory/namespace no longer exists).
3. **Architectural assertions** — tests that verify implementation decisions
   (e.g., "Migrations builds its own connection string") rather than
   runtime behaviors. If the implementation changes, these tests become
   wrong, not helpful.

Leaving dead tests in the suite is actively harmful: they inflate the test
count (making it meaningless as a quality signal), they slow down
identification of real failures, and they create maintenance drag when
structural changes require updating tests that guard nothing.

This project removes all 26 dead tests, deletes one entire file
(DalToUtilitiesRenameTests.fs) plus its .feature spec, and removes
scenarios from three .feature files. For tests that trace to valid BDD
requirements, "verified by design" notes are added to the relevant
project test results documents.

**Current test count:** 427. **Post-cleanup target:** 401.

---

## Test Deletion Inventory

### DalToUtilitiesRenameTests.fs — DELETE ENTIRE FILE (8 tests)

| FT ID | Test Name | Reason |
|-------|-----------|--------|
| FT-DUR-001 | No LeoBloom.Dal directory exists under Src | Ghost guard — rename completed, directory cannot reappear |
| FT-DUR-002 | No namespace LeoBloom.Dal in any source file | Ghost guard |
| FT-DUR-003 | No project reference to LeoBloom.Dal in any fsproj | Ghost guard |
| FT-DUR-004 | Solution file does not reference LeoBloom.Dal | Ghost guard |
| FT-DUR-005 | LeoBloom.Utilities directory exists with infrastructure files | Ghost guard (updated in P045, still dead) |
| FT-DUR-006 | LeoBloom.Utilities.fsproj builds successfully | Tautology — if tests run, it built |
| FT-DUR-007 | Full solution builds with zero rename-related warnings | Tautology — Assert.True(true) |
| FT-DUR-008 | All tests pass after rename | Tautology — Assert.True(true) |

**Actions:**
- Delete `Src/LeoBloom.Tests/DalToUtilitiesRenameTests.fs`
- Remove `<Compile Include="DalToUtilitiesRenameTests.fs" />` from Tests.fsproj
- Delete `Specs/Structural/DalToUtilitiesRename.feature`

### DataSourceEncapsulationTests.fs — DELETE 6 TESTS (2 survive)

| FT ID | Test Name | Reason |
|-------|-----------|--------|
| FT-DSI-002 | No code outside Migrations references DataSource.connectionString | Ghost guard — binding was removed, symbol no longer exists |
| FT-DSI-003 | Migrations has no project reference to LeoBloom.Utilities | Architectural decision, not runtime invariant |
| FT-DSI-004 | Migrations builds its own connection string from its own appsettings | Architectural decision |
| FT-DSI-005 | Migrations opens its own NpgsqlConnection for schema bootstrap | Architectural decision |
| FT-DSI-006 | Full solution builds successfully | Tautology — Assert.True(true) |
| FT-DSI-007 | All existing tests pass | Tautology — Assert.True(true) |

**Actions:**
- Remove 6 test functions and their corresponding scenarios from the .feature file
- Remove helper code that becomes dead (repoRoot, srcDir) only if no surviving test uses them

### LogModuleStructureTests.fs — DELETE 9 TESTS (2 survive)

| FT ID | Test Name | Reason |
|-------|-----------|--------|
| FT-LMS-001 | Serilog core package is referenced | Build fails if removed — not a test |
| FT-LMS-002 | Serilog.Sinks.Console package is referenced | Build fails if removed |
| FT-LMS-003 | Serilog.Sinks.File package is referenced | Build fails if removed |
| FT-LMS-004 | Serilog.Settings.Configuration package is referenced | Build fails if removed |
| FT-LMS-005 | Log.fs exists in LeoBloom.Utilities | Build fails if deleted |
| FT-LMS-006 | Log module exposes the required functions | Compiler enforced — callers fail if signatures change |
| FT-LMS-007 | Log module does not expose a debug function | Architectural decision |
| FT-LMS-010 | Migrations has no reference to LeoBloom.Utilities | Duplicate of DSI-003 |
| FT-LMS-011 | Migrations source files have no changes from this project | Architectural decision |

**Actions:**
- Remove 9 test functions and their corresponding scenarios from the .feature file
- Clean up helpers (hasPackageReference, utilitiesFsproj) if no surviving test uses them

### LoggingInfrastructureTests.fs — DELETE 3 TESTS (8 survive)

| FT ID | Test Name | Reason |
|-------|-----------|--------|
| FT-LI-002 | Log.initialize is called in test infrastructure | Structural check — code review concern, not runtime |
| FT-LI-005 | Minimum log level is configurable via appsettings | Configuration assertion, not behavioral |
| FT-LI-006 | File sink base path is configurable via appsettings | Configuration assertion, not behavioral |

**Note:** FT-LI-001 was already deleted in P046 (it tested the deleted Api
project). It is NOT counted in this project's deletion total.

**Actions:**
- Remove 3 test functions and their corresponding scenarios from the .feature file
- Clean up helpers only if they become unused

---

## What Survives

### DataSourceEncapsulationTests.fs (2 tests remain)

| FT ID | Test Name | Why it stays |
|-------|-----------|-------------|
| FT-DSI-001 | DataSource exposes only openConnection as a public binding | Runtime reflection check — guards API surface |
| FT-DSI-008 | Migrations runs successfully against leobloom_dev | Runtime database check — real verification |

### LogModuleStructureTests.fs (2 tests remain)

| FT ID | Test Name | Why it stays |
|-------|-----------|-------------|
| FT-LMS-008 | TestHelpers uses Log.errorExn instead of eprintfn | Guards coding standard — real grep check |
| FT-LMS-009 | No printfn or eprintfn in any Src project except Migrations | Guards coding standard — real grep check |

### LoggingInfrastructureTests.fs (8 tests remain)

| FT ID | Test Name | Why it stays |
|-------|-----------|-------------|
| FT-LI-003 | Running tests creates a log file | Runtime — actually checks file exists |
| FT-LI-004 | Log filename follows the expected format | Runtime — pattern matching on real files |
| FT-LI-007 | Posting a journal entry emits Info-level log entries | Behavioral — verifies log output |
| FT-LI-008 | Voiding a journal entry emits Info-level log entries | Behavioral — verifies log output |
| FT-LI-009 | Querying account balance by id emits Info-level log entry | Behavioral — verifies log output |
| FT-LI-010 | Querying account balance by code emits Info-level log entry | Behavioral — verifies log output |
| FT-LI-011 | DataSource initialization emits Info-level log entry | Structural but verifies real source content |
| FT-LI-012 | Validation failure emits Warning-level log entry | Behavioral — verifies log output |

---

## Acceptance Criteria

### Structural Criteria (verified by build + QE, not Gherkin)

| # | Criterion | Verification |
|---|-----------|--------------|
| S1 | DalToUtilitiesRenameTests.fs is deleted | File does not exist |
| S2 | DalToUtilitiesRenameTests.fs removed from Tests.fsproj Compile includes | Inspect fsproj |
| S3 | DalToUtilitiesRename.feature is deleted | File does not exist |
| S4 | 6 specified tests removed from DataSourceEncapsulationTests.fs (FT-DSI-002 through FT-DSI-007) | Inspect file |
| S5 | 6 scenarios removed from DataSourceEncapsulation.feature (FT-DSI-002 through FT-DSI-007) | Inspect file |
| S6 | 9 specified tests removed from LogModuleStructureTests.fs (FT-LMS-001 through FT-LMS-007, FT-LMS-010, FT-LMS-011) | Inspect file |
| S7 | 9 scenarios removed from LogModuleStructure.feature (same IDs) | Inspect file |
| S8 | 3 specified tests removed from LoggingInfrastructureTests.fs (FT-LI-002, FT-LI-005, FT-LI-006) | Inspect file |
| S9 | 3 scenarios removed from LoggingInfrastructure.feature (FT-LI-001, FT-LI-002, FT-LI-005, FT-LI-006) | Inspect file — note FT-LI-001 scenario should also be removed from the .feature since its test was already deleted in P046 |
| S10 | Dead helper code (unused functions, unused imports) cleaned up in surviving test files | Code inspection |
| S11 | "Verified by design" sections added to Project031-test-results.md and Project033-test-results.md | Inspect files |

### Build and Test Criteria (verified by Governor)

| # | Criterion | Verification |
|---|-----------|--------------|
| B1 | `dotnet build` succeeds with zero warnings related to this change | Build output |
| B2 | `dotnet test` — 401 tests pass, zero failures, zero skips | Test output |
| B3 | No surviving test logic was modified — only deletions and import cleanup | Diff review |

### Behavioral Criteria

**None.** This is a pure deletion project. No new behaviors. No changed
behaviors. No Gherkin scenarios to write. All verification is structural
and build-based.

---

## Verified-by-Design Documentation

The following deleted tests trace to valid BDD requirements from their
originating projects. The Builder must add lightweight "verified by design"
notes to the relevant test results documents explaining why the runtime
assertion was removed:

**Project031-test-results.md (Foundational Logging):**
- FT-LMS-001 through FT-LMS-004 — Serilog package references are verified
  by the build. If a package is removed, compilation fails.
- FT-LMS-005 — Log.fs existence is verified by the build.
- FT-LMS-006, FT-LMS-007 — Log module API surface is verified by the
  compiler. Callers fail to build if signatures change.
- FT-LMS-010, FT-LMS-011 — Migrations isolation is an architectural
  decision documented in ADR-002.
- FT-LI-002 — Log.initialize call in test infra is verified by code review.
- FT-LI-005, FT-LI-006 — Configuration assertions are verified by the
  build (ReadFrom.Configuration) and by runtime log output tests (FT-LI-003,
  FT-LI-004, FT-LI-007 through FT-LI-012).

**Project033-test-results.md (Seal DataSource Internals):**
- FT-DSI-002 — The binding no longer exists. DSI-001 (which survives)
  verifies the complete public API surface via reflection.
- FT-DSI-003 through FT-DSI-005 — Migrations self-sufficiency is an
  architectural decision. If Migrations adds a reference to Utilities,
  it's a code review concern, not a runtime failure.
- FT-DSI-006, FT-DSI-007 — Build integrity tautologies. The build either
  works or it doesn't.

---

## Risks and Assumptions

### Risks

1. **Test count mismatch.** The backlog says 427 tests. My `[<Fact>]` count
   shows 378, which means some tests expand via `[<Theory>]`/`[<InlineData>]`
   into multiple test cases at runtime. The post-cleanup count should be
   verified by `dotnet test` output, not by counting attributes. The 26
   deletion count is based on `[<Fact>]` attributes removed.

2. **Helper code entanglement.** The `repoRoot` and `srcDir` helpers are
   duplicated across all 4 files. In DataSourceEncapsulationTests.fs and
   LogModuleStructureTests.fs, surviving tests may still need them. The
   Builder must check before removing.

3. **Feature file scenario removal.** The FT-LI-001 scenario still exists
   in LoggingInfrastructure.feature even though its test was deleted in
   P046. This project should clean that up while we're in the file.

### Assumptions

1. No other test file references tests in the deleted files (no
   cross-file test dependencies in xUnit).

2. The 12 surviving tests in the affected files require zero logic changes.
   Only imports and dead helper removal may be needed.

3. The "verified by design" documentation is a lightweight addendum
   (a few lines per test results file), not a full write-up.

---

## Backlog Status Update

P048 status changed from "Not started" to **In Progress** as of 2026-04-05.
