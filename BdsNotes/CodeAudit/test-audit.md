# Test Audit: Dead Weight Analysis

**Date:** 2026-04-05
**Scope:** DalToUtilitiesRenameTests, DataSourceEncapsulationTests, LogModuleStructureTests, LoggingInfrastructureTests

---

## Summary

- **39 total tests** across 4 files
- **2 tautologies** (pure `Assert.True(true)`)
- **8 ghost guards** (test conditions that cannot recur without reverting completed work)
- **29 living tests** (verify conditions that could still be violated)

---

## DalToUtilitiesRenameTests.fs

File: `Src/LeoBloom.Tests/DalToUtilitiesRenameTests.fs`
Referencing project: **Project031-FoundationalLogging**

| Gherkin ID | Test Name | Classification | Rationale | Recommendation |
|---|---|---|---|---|
| FT-DUR-001 | No LeoBloom.Dal directory exists under Src | Ghost guard | Dal directory was deleted in Project031. Nobody is going to accidentally recreate `LeoBloom.Dal/`. | Nuke |
| FT-DUR-002 | No namespace LeoBloom.Dal in any source file | Ghost guard | All references were mechanically renamed. New code would use `LeoBloom.Utilities`. No path leads back to `LeoBloom.Dal`. | Nuke |
| FT-DUR-003 | No project reference to LeoBloom.Dal in any fsproj | Ghost guard | Same rationale as DUR-002. The fsproj was deleted; new references would target `LeoBloom.Utilities`. | Nuke |
| FT-DUR-004 | Solution file does not reference LeoBloom.Dal | Ghost guard | The solution entry was removed. No tooling would re-add a nonexistent project. | Nuke |
| FT-DUR-005 | LeoBloom.Utilities directory exists with all original Dal files | Ghost guard | Directory exists, files are in active use by many other tests. Deleting them would break the build long before this test catches it. | Nuke |
| FT-DUR-006 | LeoBloom.Utilities.fsproj builds successfully | Ghost guard | Checks assembly is loaded. If it didn't build, the entire test project wouldn't compile. Redundant with the build system. | Nuke |
| FT-DUR-007 | Full solution builds with zero rename-related warnings | Tautology | Body is `Assert.True(true)`. Project031 test results even call this out as a weak test. | Nuke |
| FT-DUR-008 | All tests pass after rename | Tautology | Body is `Assert.True(true)`. Same note from Project031 test results. | Nuke |

**Verdict: Nuke the entire file.** All 8 tests are either tautologies or ghost guards for a completed, one-time mechanical rename. The rename shipped in Project031. There is zero realistic scenario where `LeoBloom.Dal` resurfaces.

---

## DataSourceEncapsulationTests.fs

File: `Src/LeoBloom.Tests/DataSourceEncapsulationTests.fs`
Referencing project: **Project033-SealDataSourceInternals**

| Gherkin ID | Test Name | Classification | Rationale | Recommendation |
|---|---|---|---|---|
| FT-DSI-001 | DataSource exposes only openConnection as a public binding | Living | Reflection-based API surface test. Someone could add a public binding to DataSource and violate encapsulation. This is the entire point of Project033. | Keep |
| FT-DSI-002 | No code outside Migrations references DataSource.connectionString | Living | Scans source files for `DataSource.connectionString` usage. New code could introduce this dependency. | Keep |
| FT-DSI-003 | Migrations has no project reference to LeoBloom.Utilities | Living | Verifies Migrations stays decoupled from Utilities. Future work could accidentally add this reference. | Keep |
| FT-DSI-004 | Migrations builds its own connection string from its own appsettings | Living | Structural check on Migrations/Program.fs. The pattern could be broken during future Migrations changes. | Keep |
| FT-DSI-005 | Migrations opens its own NpgsqlConnection for schema bootstrap | Living | Verifies Migrations creates its own connection, not via DataSource. Same rationale as DSI-004. | Keep |
| FT-DSI-006 | Full solution builds successfully | Tautology | Body is `Assert.True(true)`. | Nuke |
| FT-DSI-007 | All existing tests pass | Tautology | Body is `Assert.True(true)`. | Nuke |
| FT-DSI-008 | Migrations runs successfully against leobloom_dev | Living | Actually queries the DB for the migrondi schema. Verifies runtime behavior. | Keep |

**Verdict: Nuke DSI-006 and DSI-007. Keep the other 6.** The two tautologies add nothing. The rest are legitimate architectural guardrails that prevent DataSource encapsulation from eroding.

---

## LogModuleStructureTests.fs

File: `Src/LeoBloom.Tests/LogModuleStructureTests.fs`
Referencing project: **Project031-FoundationalLogging**

| Gherkin ID | Test Name | Classification | Rationale | Recommendation |
|---|---|---|---|---|
| FT-LMS-001 | Serilog core package is referenced | Living | Verifies package reference exists in fsproj. Someone could remove it during dependency cleanup. | Keep |
| FT-LMS-002 | Serilog.Sinks.Console package is referenced | Living | Same rationale. | Keep |
| FT-LMS-003 | Serilog.Sinks.File package is referenced | Living | Same rationale. | Keep |
| FT-LMS-004 | Serilog.Settings.Configuration package is referenced | Living | Same rationale. | Keep |
| FT-LMS-005 | Log.fs exists in LeoBloom.Utilities | Living | File existence check. Weak but not zero-value -- catches accidental deletion or move. | Keep |
| FT-LMS-006 | Log module exposes the required functions | Living | Reflection-based API surface test. Catches accidental removal or rename of public logging functions. Strong test. | Keep |
| FT-LMS-007 | Log module does not expose a debug function | Living | Design constraint: no debug-level logging. Could be violated if someone adds `let debug`. | Keep |
| FT-LMS-008 | TestHelpers uses Log.errorExn instead of eprintfn | Living | Guards against regression to raw `eprintfn` in test infrastructure. | Keep |
| FT-LMS-009 | No printfn or eprintfn in any Src project except Migrations | Living | Codebase-wide hygiene guard. Active protection against bypassing the logging infrastructure. | Keep |
| FT-LMS-010 | Migrations has no reference to LeoBloom.Utilities | Living | Duplicate of FT-DSI-003 (same assertion, different test file). Both live in different feature suites. | Keep (but note overlap with DSI-003) |
| FT-LMS-011 | Migrations source files have no changes from this project | Living | Verifies Migrations has no Serilog/Utilities contamination. Broader than DSI-003 since it also checks package refs. | Keep |

**Verdict: Keep all 11.** Every test guards a condition that could genuinely be violated by future development. Note the overlap between FT-LMS-010 and FT-DSI-003 -- they're identical checks in different feature suites. Not urgent to deduplicate but worth noting.

---

## LoggingInfrastructureTests.fs

File: `Src/LeoBloom.Tests/LoggingInfrastructureTests.fs`
Referencing project: **Project031-FoundationalLogging**

| Gherkin ID | Test Name | Classification | Rationale | Recommendation |
|---|---|---|---|---|
| FT-LI-001 | Log.initialize is called at Api startup | Living | Structural check that Program.fs calls `Log.initialize` before `app.Run`. Could regress during Api changes. | Keep |
| FT-LI-002 | Log.initialize is called in test infrastructure | Living | Structural check on TestHelpers.fs. | Keep |
| FT-LI-003 | Running tests creates a log file | Living | Runtime verification that log files are actually written. | Keep |
| FT-LI-004 | Log filename follows the expected format | Living | Validates `leobloom-yyyyMMdd.HH.mm.ss.log` naming convention. | Keep |
| FT-LI-005 | Minimum log level is configurable via appsettings | Living | Structural + runtime check that Serilog reads from config. | Keep |
| FT-LI-006 | File sink base path is configurable via appsettings | Living | Verifies FileBasePath config key flows through to actual log file creation. | Keep |
| FT-LI-007 | Posting a journal entry emits Info-level log entries | Living | Integration test: posts a real journal entry and checks log output. | Keep |
| FT-LI-008 | Voiding a journal entry emits Info-level log entries | Living | Integration test: voids a real entry and checks log output. | Keep |
| FT-LI-009 | Querying account balance by id emits Info-level log entry | Living | Integration test: queries balance and checks log output. | Keep |
| FT-LI-010 | Querying account balance by code emits Info-level log entry | Living | Integration test: queries balance by code and checks log output. | Keep |
| FT-LI-011 | DataSource initialization emits Info-level log entry | Living | Structural check that DataSource.fs contains a `Log.info` call. | Keep |
| FT-LI-012 | Validation failure emits Warning-level log entry | Living | Integration test: triggers validation failure and checks warn-level log output. | Keep |

**Verdict: Keep all 12.** Every test validates live logging behavior or structural patterns that could genuinely regress.

---

## Kill List Summary

| Action | Count | Tests |
|---|---|---|
| **Nuke** | 10 | FT-DUR-001 through FT-DUR-008 (entire file), FT-DSI-006, FT-DSI-007 |
| **Keep** | 29 | Everything else |

### Notes

- **FT-LMS-010 overlaps with FT-DSI-003**: Both verify that Migrations has no project reference to LeoBloom.Utilities. They live in different feature suites (Log Module Structure vs DataSource Encapsulation), so both have traceable provenance. Not worth deduplicating unless you're consolidating test files later.
- **FT-LMS-001 through FT-LMS-004** (package reference checks) are the weakest of the "keep" tests. If a Serilog package gets removed, the build would fail anyway since `Log.fs` uses Serilog types. They're more documentation than protection. Could revisit later.
