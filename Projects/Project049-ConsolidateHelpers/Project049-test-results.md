# Project 049 -- Test Results

**Date:** 2026-04-05
**Branch:** feature/049-consolidate-helpers (uncommitted working tree)
**Base Commit:** e77b386
**Result:** 11/11 acceptance criteria verified

## Acceptance Criteria Verification

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| 1 | `optParam` exists exactly once in the entire `Src/` tree (in `DataHelpers.fs`) | Yes | Grep for `let optParam` and `let private optParam` across `Src/` returns only `DataHelpers.fs:11`. Repo files, test files: zero local definitions remain. |
| 2 | `DataHelpers.fs` is in `LeoBloom.Utilities` project, compiled after `DataSource.fs` | Yes | `LeoBloom.Utilities.fsproj` line 11: `<Compile Include="DataHelpers.fs" />` appears after `DataSource.fs` on line 10. |
| 3 | No `let private optParam` definitions remain in any repository file | Yes | Grep confirms zero hits in JournalEntryRepository.fs, ObligationAgreementRepository.fs, ObligationInstanceRepository.fs, TransferRepository.fs. |
| 4 | All 4 repository files call `DataHelpers.optParam` instead of local copies | Yes | Grep confirms `DataHelpers.optParam` calls in all 4 files: JournalEntryRepository.fs (lines 21, 52), ObligationAgreementRepository.fs (8 calls), ObligationInstanceRepository.fs (line 55), TransferRepository.fs (lines 49-50). |
| 5 | `repoRoot` logic exists exactly once in `TestHelpers.fs` (in `RepoPath` module) | Yes | TestHelpers.fs lines 308-318 define `RepoPath.repoRoot`. Grep for `let private repoRoot` across test files returns zero hits outside ConsolidatedHelpersTests.fs (which only references it in string literals). |
| 6 | `repoRoot` uses `[<CallerFilePath>]` attribute, not `AppContext.BaseDirectory` walkup | **Yes (deviation)** | Builder used `__SOURCE_DIRECTORY__` instead of `[<CallerFilePath>]`. The plan's Risks section (line 239) explicitly anticipated this fallback: "If it doesn't work with optional params in F#, fall back to a version that takes an explicit `__SOURCE_FILE__` or just hardcodes the path derivation from `__SOURCE_DIRECTORY__`." Zero hits for `AppContext.BaseDirectory` in the test project. The behavioral requirement (stable path resolution not dependent on runtime output directory) is met. |
| 7 | No `let private repoRoot` definitions remain in `LogModuleStructureTests.fs` or `LoggingInfrastructureTests.fs` | Yes | Both files read directly -- neither contains `let private repoRoot` or `let private srcDir`. LogModuleStructureTests.fs uses `RepoPath.srcDir` (line 15). LoggingInfrastructureTests.fs no longer references repoRoot at all. |
| 8 | Constraint test helpers exist exactly once in `TestHelpers.fs` | Yes | TestHelpers.fs lines 275-302 define `ConstraintAssert` module with `tryExec`, `tryInsert`, `assertSqlState`, `assertNotNull`, `assertUnique`, `assertFk`, `assertSuccess`. All 7 helpers present. |
| 9 | No duplicate constraint helper definitions remain in `LedgerConstraintTests.fs` or `OpsConstraintTests.fs` | Yes | Both files read directly. LedgerConstraintTests.fs opens with test functions at line 12 (no helper defs). OpsConstraintTests.fs diff confirms all 8 private helper definitions removed. Both files now use `ConstraintAssert.*` qualified calls. DeleteRestrictionTests.fs (scope extension) also uses `ConstraintAssert.*` with no local defs. |
| 10 | `dotnet build` succeeds with zero warnings for all projects | Yes | `dotnet build LeoBloom.sln` output: "Build succeeded. 0 Warning(s) 0 Error(s)" |
| 11 | `dotnet test Src/LeoBloom.Tests/` passes all existing tests (zero regressions) | Yes | 422 total, 420 passed, 2 failed. The 2 failures are pre-existing in `PostObligationToLedgerTests` (closed fiscal period tests) -- unrelated to P049. Zero new regressions. |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-CHL-001 | Journal entry with null source persists via consolidated optParam | Yes | Yes |
| @FT-CHL-002 | Journal entry with non-null source persists via consolidated optParam | Yes | Yes |
| @FT-CHL-003 | Journal entry with null memo persists via consolidated optParam | Yes | Yes |
| @FT-CHL-004 | Journal entry with non-null memo persists via consolidated optParam | Yes | Yes |
| @FT-CHL-005 | Repo root resolves correctly from test files | Yes | Yes |
| @FT-CHL-006 | Src directory resolves correctly from test files | Yes | Yes |
| @FT-CHL-007 | Shared constraint helpers detect violations (6 scenario outline rows) | Yes (6 tests) | Yes |
| @FT-CHL-008 | Shared assertSuccess confirms clean inserts | Yes | Yes |

All 21 tests in ConsolidatedHelpersTests.fs pass (21/21). Tests carry `[<Trait("GherkinId", "FT-CHL-NNN")>]` attributes matching the Gherkin scenario tags.

## Notes

- **Builder deviation (AC #6):** `__SOURCE_DIRECTORY__` used instead of `[<CallerFilePath>]`. This is the plan's documented fallback path. The behavioral intent is fully satisfied -- path resolution derives from the source file's compile-time location, not from `AppContext.BaseDirectory`.
- **Scope extension:** Reviewer identified `DeleteRestrictionTests.fs` also had duplicated constraint helpers. Builder fixed this. The file now uses `ConstraintAssert.*` from TestHelpers.fs with no local definitions.
- **Pre-existing failures:** 2 tests in `PostObligationToLedgerTests` fail on closed fiscal period logic. These exist on main before P049 and are unrelated to this refactoring.

## Verdict

**APPROVED.** All 11 acceptance criteria verified against the actual codebase. All 8 Gherkin scenarios (expanding to 13 test cases including outline rows, plus 8 structural tests = 21 total) pass. Evidence chain is solid -- every criterion checked by direct file reads, grep searches, and live test execution. No fabrication detected.
