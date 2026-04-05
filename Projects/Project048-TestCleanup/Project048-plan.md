# Project 048 — Test Cleanup — Plan

## Objective

Delete 26 dead tests (tautologies, ghost guards, architectural assertions) across 4
files, clean up their corresponding .feature scenarios, remove dead helper code and
unused imports, and add verified-by-design notes to the originating projects' test
results docs. Pure deletion -- zero behavior changes.

---

## Phases

### Phase 1: DalToUtilitiesRenameTests.fs -- Full Deletion

**What:** Delete the entire file, remove from fsproj, delete the feature file.

**Steps:**

1. Delete file: `Src/LeoBloom.Tests/DalToUtilitiesRenameTests.fs`
2. Remove `<Compile Include="DalToUtilitiesRenameTests.fs" />` (line 24) from
   `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj`
3. Delete file: `Specs/Structural/DalToUtilitiesRename.feature`

**Verification:** `dotnet build` succeeds. File does not exist. No fsproj reference.

---

### Phase 2: DataSourceEncapsulationTests.fs -- Delete 6 Tests

**What:** Remove FT-DSI-002 through FT-DSI-007. Keep FT-DSI-001 and FT-DSI-008.
Clean up dead helpers and unused imports.

**Steps:**

1. Delete these 6 test functions (including their comment headers):
   - `No code outside Migrations references DataSource.connectionString` (FT-DSI-002, lines 66-91)
   - `Migrations has no project reference to LeoBloom.Utilities` (FT-DSI-003, lines 93-116)
   - `Migrations builds its own connection string from its own appsettings` (FT-DSI-004, lines 118-141)
   - `Migrations opens its own NpgsqlConnection for schema bootstrap` (FT-DSI-005, lines 143-161)
   - `Full solution builds successfully` (FT-DSI-006, lines 163-174)
   - `All existing tests pass` (FT-DSI-007, lines 176-184)

2. Delete helpers that are now dead (no surviving test uses them):
   - `repoRoot` binding (lines 15-25)
   - `srcDir` binding (line 27)

3. Remove unused imports. After deletion, surviving tests (DSI-001 and DSI-008) use:
   - `System` -- YES (AppDomain, StringComparison)
   - `System.IO` -- NO (only used by deleted tests for file I/O)
   - `System.Reflection` -- YES (BindingFlags in DSI-001)
   - `System.Xml.Linq` -- NO (only used by DSI-003 for XML parsing)
   - `Xunit` -- YES
   - `LeoBloom.Utilities` -- YES (DataSource.openConnection in both survivors)

   Remove: `open System.IO` and `open System.Xml.Linq`

4. Edit `Specs/Structural/DataSourceEncapsulation.feature`:
   - Remove 6 scenarios: @FT-DSI-002 through @FT-DSI-007 (lines 19-61)
   - Keep: @FT-DSI-001 (lines 13-17) and @FT-DSI-008 (lines 65-71)
   - Clean up any orphaned section comments (e.g. "# --- Project decoupling ---",
     "# --- Migrations self-sufficiency ---", "# --- Build integrity ---")

**Verification:** File has exactly 2 `[<Fact>]` attributes. `dotnet build` succeeds.

---

### Phase 3: LogModuleStructureTests.fs -- Delete 9 Tests

**What:** Remove FT-LMS-001 through FT-LMS-007, FT-LMS-010, FT-LMS-011. Keep
FT-LMS-008 and FT-LMS-009. Clean up dead helpers and unused imports.

**Steps:**

1. Delete these 9 test functions (including their comment headers):
   - `Serilog core package is referenced` (FT-LMS-001, lines 38-46)
   - `Serilog.Sinks.Console package is referenced` (FT-LMS-002, lines 48-56)
   - `Serilog.Sinks.File package is referenced` (FT-LMS-003, lines 58-66)
   - `Serilog.Settings.Configuration package is referenced` (FT-LMS-004, lines 68-76)
   - `Log.fs exists in LeoBloom.Utilities` (FT-LMS-005, lines 78-86)
   - `Log module exposes the required functions` (FT-LMS-006, lines 88-117)
   - `Log module does not expose a debug function` (FT-LMS-007, lines 119-139)
   - `Migrations has no reference to LeoBloom.Utilities` (FT-LMS-010, lines 183-201)
   - `Migrations source files have no changes from this project` (FT-LMS-011, lines 203-236)

2. Delete helpers that are now dead:
   - `utilitiesFsproj` binding (lines 28-29) -- only used by `hasPackageReference`
   - `hasPackageReference` function (lines 31-36) -- only used by LMS-001 through LMS-004

3. Keep helpers that surviving tests still need:
   - `repoRoot` (lines 14-24) -- used by LMS-008 and LMS-009 via srcDir
   - `srcDir` (line 26) -- used by LMS-008 and LMS-009

4. Remove unused imports. After deletion, surviving tests use:
   - `System` -- YES (StringComparison in LMS-009)
   - `System.IO` -- YES (Path, File, Directory in both survivors)
   - `System.Reflection` -- NO (only used by LMS-006 for BindingFlags)
   - `System.Xml.Linq` -- NO (only used by hasPackageReference for XML parsing)
   - `Xunit` -- YES
   - `LeoBloom.Utilities` -- NO (only used by LMS-006 which calls Log.initialize())

   Remove: `open System.Reflection`, `open System.Xml.Linq`, `open LeoBloom.Utilities`

5. Edit `Specs/Structural/LogModuleStructure.feature`:
   - Remove 9 scenarios: @FT-LMS-001 through @FT-LMS-007 (lines 10-58),
     @FT-LMS-010 (lines 76-80), @FT-LMS-011 (lines 82-87)
   - Keep: @FT-LMS-008 (lines 63-66) and @FT-LMS-009 (lines 68-72)
   - Clean up orphaned section comments ("# --- Package references ---",
     "# --- Log module API surface ---", "# --- Migrations isolation ---")

**Verification:** File has exactly 2 `[<Fact>]` attributes. `dotnet build` succeeds.

---

### Phase 4: LoggingInfrastructureTests.fs -- Delete 3 Tests

**What:** Remove FT-LI-002, FT-LI-005, FT-LI-006. Keep 8 surviving tests.
No helper or import cleanup needed (all helpers and imports are used by survivors).

**Steps:**

1. Delete these 3 test functions (including their comment headers):
   - `Log.initialize is called in test infrastructure` (FT-LI-002, lines 60-72)
   - `Minimum log level is configurable via appsettings` (FT-LI-005, lines 113-133)
   - `File sink base path is configurable via appsettings` (FT-LI-006, lines 135-159)

2. The `// @FT-LI-001 removed` comment on line 58 can stay -- it documents the
   P046 deletion and costs nothing.

3. Helpers: ALL survive. `repoRoot`, `srcDir`, `logDir`, `getLatestLogFile`,
   `readLogContent`, `snapshotLogFiles` are all used by surviving tests
   (FT-LI-003/004 use logDir; FT-LI-007 through FT-LI-012 use readLogContent;
   FT-LI-011 uses srcDir).

4. Imports: ALL survive. `System`, `System.IO`, `System.Text.RegularExpressions`,
   `Xunit`, `Npgsql`, `LeoBloom.Domain.Ledger`, `LeoBloom.Utilities`,
   `LeoBloom.Ledger`, `LeoBloom.Tests.TestHelpers` are all used by surviving tests.

5. Edit `Specs/Behavioral/LoggingInfrastructure.feature`:
   - Remove 4 scenarios: @FT-LI-001 (lines 8-12 -- test was deleted in P046 but
     scenario was left behind), @FT-LI-002 (lines 14-18), @FT-LI-005 (lines 38-43),
     @FT-LI-006 (lines 45-49)
   - Keep: @FT-LI-003, @FT-LI-004, @FT-LI-007 through @FT-LI-012
   - Clean up orphaned section comments ("# --- Initialization ---",
     "# --- Configuration ---") if their sections are now empty

**Verification:** File has exactly 8 `[<Fact>]` attributes. `dotnet build` succeeds.

---

### Phase 5: Verified-by-Design Documentation

**What:** Add lightweight addenda to the two originating projects' test results docs.

**Steps:**

1. Append a "Verified by Design" section to
   `Projects/Project031-FoundationalLogging/Project031-test-results.md`:

   ```markdown
   ## Verified by Design (P048 Test Cleanup)

   The following tests were removed in Project 048. Their requirements are
   verified by the build, the compiler, or surviving runtime tests:

   - **FT-LMS-001 through FT-LMS-004** (Serilog package references): Build
     fails if any package is removed.
   - **FT-LMS-005** (Log.fs exists): Build fails if deleted.
   - **FT-LMS-006** (Log module API surface): Compiler enforced -- callers
     fail to build if signatures change.
   - **FT-LMS-007** (No debug function): Architectural decision, not a
     runtime invariant.
   - **FT-LMS-010, FT-LMS-011** (Migrations isolation): Architectural
     decision documented in ADR-002.
   - **FT-LI-002** (Log.initialize in test infra): Code review concern,
     not a runtime invariant.
   - **FT-LI-005, FT-LI-006** (Configuration assertions): Verified by
     the build (ReadFrom.Configuration) and by surviving runtime log tests
     (FT-LI-003, FT-LI-004, FT-LI-007 through FT-LI-012).
   - **FT-DUR-001 through FT-DUR-008** (Dal-to-Utilities rename guards):
     Ghost guards and tautologies. The rename shipped; the old name cannot
     recur.
   ```

2. Append a "Verified by Design" section to
   `Projects/Project033-SealDataSourceInternals/Project033-test-results.md`:

   ```markdown
   ## Verified by Design (P048 Test Cleanup)

   The following tests were removed in Project 048. Their requirements are
   verified by surviving tests or are architectural decisions:

   - **FT-DSI-002** (No external connectionString references): The binding
     no longer exists. FT-DSI-001 (surviving) verifies the complete public
     API surface via reflection.
   - **FT-DSI-003 through FT-DSI-005** (Migrations self-sufficiency):
     Architectural decision. If Migrations adds a reference to Utilities,
     it is a code review concern, not a runtime failure.
   - **FT-DSI-006, FT-DSI-007** (Build integrity tautologies): The build
     either works or it does not.
   ```

**Verification:** Both files have the new section appended. Content matches
the PO kickoff's verified-by-design inventory.

---

### Phase 6: Build and Test Verification

**What:** Run the full build and test suite to confirm nothing broke.

**Steps:**

1. Run `dotnet build` from solution root. Expect 0 warnings, 0 errors.
2. Run `dotnet test`. Expect 401 tests passing, 0 failures, 0 skipped.
3. Verify diff contains ONLY deletions and import cleanup -- no surviving
   test logic was modified.

---

## Acceptance Criteria

- [ ] DalToUtilitiesRenameTests.fs does not exist
- [ ] DalToUtilitiesRenameTests.fs removed from Tests.fsproj Compile includes
- [ ] DalToUtilitiesRename.feature does not exist
- [ ] 6 tests removed from DataSourceEncapsulationTests.fs (FT-DSI-002 through FT-DSI-007)
- [ ] 6 scenarios removed from DataSourceEncapsulation.feature
- [ ] 9 tests removed from LogModuleStructureTests.fs (FT-LMS-001 through FT-LMS-007, FT-LMS-010, FT-LMS-011)
- [ ] 9 scenarios removed from LogModuleStructure.feature
- [ ] 3 tests removed from LoggingInfrastructureTests.fs (FT-LI-002, FT-LI-005, FT-LI-006)
- [ ] 4 scenarios removed from LoggingInfrastructure.feature (FT-LI-001, FT-LI-002, FT-LI-005, FT-LI-006)
- [ ] Dead helper code removed (repoRoot/srcDir in DataSourceEncapsulation; hasPackageReference/utilitiesFsproj in LogModuleStructure)
- [ ] Unused imports cleaned up in DataSourceEncapsulationTests.fs and LogModuleStructureTests.fs
- [ ] Verified-by-design sections added to Project031-test-results.md and Project033-test-results.md
- [ ] `dotnet build` succeeds with 0 warnings
- [ ] `dotnet test` passes 401 tests, 0 failures, 0 skipped
- [ ] No surviving test logic was modified (diff is deletions and import cleanup only)

## Risks

1. **Test count delta.** The 26 `[<Fact>]` deletions should reduce from 427 to 401.
   If the actual count doesn't match, it means Theory/InlineData expansion is in
   play for one of the deleted tests. Unlikely (all 26 are `[<Fact>]`) but verify
   with `dotnet test` output.

2. **LogModuleStructure.feature path.** The PO kickoff references
   `Specs/Behavioral/LogModuleStructure.feature` but the actual file is at
   `Specs/Structural/LogModuleStructure.feature`. Builder must use the correct
   path.

## Out of Scope

- Refactoring surviving tests
- Deduplicating the `repoRoot`/`srcDir` helper pattern across files
- Consolidating the 3 surviving structural test files into one
- Any new tests or behavioral changes

---

## PO Approval

**Verdict:** APPROVED
**Date:** 2026-04-05
**Reviewed by:** Product Owner

**Gate 1 Checklist:**

- [x] Objective is clear -- pure deletion of 26 dead tests, zero behavior changes
- [x] Deliverables are concrete and enumerable -- every FT ID listed, every file path specified
- [x] Acceptance criteria are testable -- binary checks (file exists/doesn't, count matches, build passes)
- [x] Acceptance criteria correctly distinguish structural (all of them) from behavioral (none -- this is a deletion project)
- [x] Phases are ordered logically with verification at each step
- [x] Out of scope is explicit -- no refactoring, no consolidation, no new tests
- [x] Dependencies are accurate -- none needed, none claimed
- [x] No deliverable duplicates prior work
- [x] Consistent with PO kickoff

**Verification notes:**

- All 26 deletions cross-checked against source files. FT IDs, line numbers, and test names are accurate.
- All 12 surviving tests confirmed present and correctly excluded from deletion scope.
- All 4 feature files confirmed to exist at the paths specified in the plan.
- Helper and import analysis verified against actual source code. The plan correctly identifies which helpers die and which survive in each file.
- The plan catches the orphaned FT-LI-001 scenario (deleted in P046, scenario left behind) -- good housekeeping.
- The plan catches the feature file path discrepancy from the kickoff (Risk #2) -- correct path used throughout.
- Both test results docs (P031, P033) confirmed to exist for the verified-by-design addenda.
- No Gherkin scenarios to write -- this is a pure structural cleanup project. All acceptance criteria are structural/build-based, which is correct for this scope.
