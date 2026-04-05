# Project 045 — Domain-Based Project Reorganization — Plan

## Objective

Split LeoBloom.Utilities (24 Compile includes) into three projects: Utilities
(2 infrastructure files), LeoBloom.Ledger (15 domain files), and LeoBloom.Ops
(7 domain files). Purely structural -- no behavioral changes. All 428 tests
must pass with only `open` statement changes.

## Pre-flight

**Branch:** `feature/045-domain-project-reorg` (create from main)

**Working directory:** `/workspace/LeoBloom`

---

## Phase 1: Create New Project Files

### Step 1.1 — Create LeoBloom.Ledger.fsproj

Create `/workspace/LeoBloom/Src/LeoBloom.Ledger/LeoBloom.Ledger.fsproj` with
this exact content:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="JournalEntryRepository.fs" />
    <Compile Include="FiscalPeriodRepository.fs" />
    <Compile Include="FiscalPeriodService.fs" />
    <Compile Include="JournalEntryService.fs" />
    <Compile Include="AccountBalanceRepository.fs" />
    <Compile Include="AccountBalanceService.fs" />
    <Compile Include="TrialBalanceRepository.fs" />
    <Compile Include="TrialBalanceService.fs" />
    <Compile Include="IncomeStatementRepository.fs" />
    <Compile Include="IncomeStatementService.fs" />
    <Compile Include="BalanceSheetRepository.fs" />
    <Compile Include="BalanceSheetService.fs" />
    <Compile Include="SubtreePLRepository.fs" />
    <Compile Include="SubtreePLService.fs" />
    <Compile Include="OpeningBalanceService.fs" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\LeoBloom.Domain\LeoBloom.Domain.fsproj" />
    <ProjectReference Include="..\LeoBloom.Utilities\LeoBloom.Utilities.fsproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Npgsql" Version="9.0.3" />
  </ItemGroup>

</Project>
```

**Compile order rationale:** Preserves the exact order from Utilities.fsproj.
Repositories come before services that use them. FiscalPeriodService before
JournalEntryService (JournalEntryService may depend on fiscal period lookups).
OpeningBalanceService last (depends on multiple other services/repos).

**PackageReference:** Only Npgsql. The Ledger files use `open Npgsql` for DB
access. They use `DataSource` and `Log` from Utilities via ProjectReference
(those modules are in the `LeoBloom.Utilities` namespace, accessed through
the project reference).

**No Serilog/Configuration packages:** Those are only needed by Log.fs and
DataSource.fs, which stay in Utilities. Ledger files call `Log.info` etc., but
that resolves through the Utilities project reference, not a direct Serilog
package dependency.

### Step 1.2 — Create LeoBloom.Ops.fsproj

Create `/workspace/LeoBloom/Src/LeoBloom.Ops/LeoBloom.Ops.fsproj` with
this exact content:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="ObligationAgreementRepository.fs" />
    <Compile Include="ObligationAgreementService.fs" />
    <Compile Include="ObligationInstanceRepository.fs" />
    <Compile Include="ObligationInstanceService.fs" />
    <Compile Include="ObligationPostingService.fs" />
    <Compile Include="TransferRepository.fs" />
    <Compile Include="TransferService.fs" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\LeoBloom.Domain\LeoBloom.Domain.fsproj" />
    <ProjectReference Include="..\LeoBloom.Utilities\LeoBloom.Utilities.fsproj" />
    <ProjectReference Include="..\LeoBloom.Ledger\LeoBloom.Ledger.fsproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Npgsql" Version="9.0.3" />
  </ItemGroup>

</Project>
```

**Compile order rationale:** Same as Utilities ordering. Agreement repos/services
before instance repos/services. ObligationPostingService after instance (it
orchestrates posting). TransferRepository before TransferService.

**Ledger ProjectReference:** Required because ObligationPostingService.fs opens
`LeoBloom.Domain.Ledger` and calls Ledger services (JournalEntryService).
TransferService.fs also opens both `Domain.Ops` and `Domain.Ledger`.

---

## Phase 2: Move Source Files

### Step 2.1 — Move Ledger files

Move these 15 files from `Src/LeoBloom.Utilities/` to `Src/LeoBloom.Ledger/`:

```
JournalEntryRepository.fs
JournalEntryService.fs
FiscalPeriodRepository.fs
FiscalPeriodService.fs
AccountBalanceRepository.fs
AccountBalanceService.fs
TrialBalanceRepository.fs
TrialBalanceService.fs
IncomeStatementRepository.fs
IncomeStatementService.fs
BalanceSheetRepository.fs
BalanceSheetService.fs
SubtreePLRepository.fs
SubtreePLService.fs
OpeningBalanceService.fs
```

Command pattern: `mv Src/LeoBloom.Utilities/{file} Src/LeoBloom.Ledger/{file}`

### Step 2.2 — Move Ops files

Move these 7 files from `Src/LeoBloom.Utilities/` to `Src/LeoBloom.Ops/`:

```
ObligationAgreementRepository.fs
ObligationAgreementService.fs
ObligationInstanceRepository.fs
ObligationInstanceService.fs
ObligationPostingService.fs
TransferRepository.fs
TransferService.fs
```

Command pattern: `mv Src/LeoBloom.Utilities/{file} Src/LeoBloom.Ops/{file}`

### Step 2.3 — Verify Utilities only has 2 files left

After moves, `Src/LeoBloom.Utilities/` should contain only:
- `Log.fs`
- `DataSource.fs`

Verify with: `ls Src/LeoBloom.Utilities/*.fs`

---

## Phase 3: Update Namespace Declarations

### Step 3.1 — Update Ledger file namespaces

In each of the 15 files now in `Src/LeoBloom.Ledger/`, change:
```
namespace LeoBloom.Utilities
```
to:
```
namespace LeoBloom.Ledger
```

This is line 1 of every file. All 15 files follow the same pattern.

Additionally, every Ledger file needs to add:
```
open LeoBloom.Utilities
```
after any existing `open` statements (or as the first `open`), because these
files call `DataSource.openConnection()` and/or `Log.info`/`Log.warn`/etc.

**Verified usage:** Every Ledger service file uses `DataSource.openConnection()`
and/or `Log.*`. Every Ledger repository file uses Npgsql directly (already has
`open Npgsql`). Some repository files may not use DataSource/Log directly, but
adding the open is harmless and consistent.

Specifically, add `open LeoBloom.Utilities` to these files that use DataSource
or Log (confirmed from grep):

| File | Uses DataSource | Uses Log |
|------|----------------|----------|
| JournalEntryRepository.fs | via Npgsql param | no |
| JournalEntryService.fs | yes | yes |
| FiscalPeriodRepository.fs | via Npgsql param | no |
| FiscalPeriodService.fs | no | no |
| AccountBalanceRepository.fs | via Npgsql param | no |
| AccountBalanceService.fs | yes (implicit) | yes (implicit) |
| TrialBalanceRepository.fs | via Npgsql param | no |
| TrialBalanceService.fs | yes | yes |
| IncomeStatementRepository.fs | via Npgsql param | no |
| IncomeStatementService.fs | no | no |
| BalanceSheetRepository.fs | via Npgsql param | no |
| BalanceSheetService.fs | no | no |
| SubtreePLRepository.fs | via Npgsql param | no |
| SubtreePLService.fs | no | no |
| OpeningBalanceService.fs | yes (implicit) | no |

**Strategy:** The safe approach is to add `open LeoBloom.Utilities` to ALL 15
files. The compiler will warn if an open is unused (via `GenerateDocumentationFile`),
but that is not a build error. However, to keep things clean:

- Files that call `DataSource.*` or `Log.*` directly: MUST have the open
- Repository files that only take `NpgsqlConnection` parameters: do NOT need
  the open (they receive connections, they don't create them)

**Builder should:** Check each file for direct `DataSource.` or `Log.` usage
and add `open LeoBloom.Utilities` only where needed. If in doubt, add it --
unused opens are not errors.

### Step 3.2 — Update Ops file namespaces

In each of the 7 files now in `Src/LeoBloom.Ops/`, change:
```
namespace LeoBloom.Utilities
```
to:
```
namespace LeoBloom.Ops
```

Same `open LeoBloom.Utilities` consideration applies. Add it to files that
use `DataSource.*` or `Log.*` directly.

Additionally, ObligationPostingService.fs and TransferService.fs need:
```
open LeoBloom.Ledger
```
because they call Ledger services (e.g., JournalEntryService.post). These
files already open `LeoBloom.Domain.Ledger` for domain types, but they also
need the Ledger project namespace for service access.

**Builder should verify:** Check whether ObligationPostingService.fs and
TransferService.fs call any functions from the `LeoBloom.Ledger` namespace
(formerly `LeoBloom.Utilities` -- e.g., `JournalEntryService.post`). If yes,
add `open LeoBloom.Ledger`.

---

## Phase 4: Trim Utilities.fsproj

### Step 4.1 — Remove moved files from Utilities.fsproj

Edit `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` to remove all 22
moved Compile includes, leaving only:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Log.fs" />
    <Compile Include="DataSource.fs" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\LeoBloom.Domain\LeoBloom.Domain.fsproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Npgsql" Version="9.0.3" />
    <PackageReference Include="Serilog" Version="4.3.1" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
    <PackageReference Include="Serilog.Settings.Configuration" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.0" />
  </ItemGroup>

</Project>
```

All PackageReferences stay -- they're used by Log.fs (Serilog) and
DataSource.fs (Npgsql, Configuration).

---

## Phase 5: Update Tests

### Step 5.1 — Add ProjectReferences to Tests.fsproj

Add these two ProjectReferences to `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj`:

```xml
<ProjectReference Include="..\LeoBloom.Ledger\LeoBloom.Ledger.fsproj" />
<ProjectReference Include="..\LeoBloom.Ops\LeoBloom.Ops.fsproj" />
```

Add them to the existing ProjectReference ItemGroup, after the Utilities reference.

### Step 5.2 — Update test file `open` declarations

For each test file, change `open LeoBloom.Utilities` to the appropriate new
namespace(s). The mapping is based on what each test file actually uses from
the moved code.

**Group A: Change `open LeoBloom.Utilities` to `open LeoBloom.Ledger`**
(These tests call Ledger service functions like JournalEntryService.post,
AccountBalanceService.*, etc.)

| File | Add | Remove |
|------|-----|--------|
| PostJournalEntryTests.fs | `open LeoBloom.Ledger` | replace `open LeoBloom.Utilities` |
| VoidJournalEntryTests.fs | `open LeoBloom.Ledger` | replace `open LeoBloom.Utilities` |
| AccountBalanceTests.fs | `open LeoBloom.Ledger` | replace `open LeoBloom.Utilities` |
| TrialBalanceTests.fs | `open LeoBloom.Ledger` | replace `open LeoBloom.Utilities` |
| IncomeStatementTests.fs | `open LeoBloom.Ledger` | replace `open LeoBloom.Utilities` |
| BalanceSheetTests.fs | `open LeoBloom.Ledger` | replace `open LeoBloom.Utilities` |
| SubtreePLTests.fs | `open LeoBloom.Ledger` | replace `open LeoBloom.Utilities` |
| OpeningBalanceTests.fs | `open LeoBloom.Ledger` | replace `open LeoBloom.Utilities` |
| FiscalPeriodTests.fs | `open LeoBloom.Ledger` | replace `open LeoBloom.Utilities` |

**WAIT -- these test files also use TestHelpers, which uses DataSource from
LeoBloom.Utilities.** Since TestHelpers.fs opens `LeoBloom.Utilities` and
exposes `DataSource.openConnection()` through helper functions, the test files
that call `DataSource.openConnection()` directly also need `open LeoBloom.Utilities`.

**Revised approach:** Do NOT remove `open LeoBloom.Utilities` from any test
file. Instead, ADD the new namespace opens alongside the existing Utilities open.

**Group A revised: ADD `open LeoBloom.Ledger` (keep `open LeoBloom.Utilities`)**

- PostJournalEntryTests.fs
- VoidJournalEntryTests.fs
- AccountBalanceTests.fs
- TrialBalanceTests.fs
- IncomeStatementTests.fs
- BalanceSheetTests.fs
- SubtreePLTests.fs
- OpeningBalanceTests.fs
- FiscalPeriodTests.fs

**Group B: ADD `open LeoBloom.Ops` (keep `open LeoBloom.Utilities`)**

- ObligationAgreementTests.fs
- SpawnObligationInstanceTests.fs
- StatusTransitionTests.fs
- OverdueDetectionTests.fs

**Group C: ADD both `open LeoBloom.Ledger` AND `open LeoBloom.Ops` (keep `open LeoBloom.Utilities`)**

- PostObligationToLedgerTests.fs
- TransferTests.fs

**Group D: ADD `open LeoBloom.Ledger` (keep `open LeoBloom.Utilities`)**

- LoggingInfrastructureTests.fs (calls JournalEntryService.post, AccountBalanceService.*)

**Group E: No changes needed (keep `open LeoBloom.Utilities` as-is)**

- TestHelpers.fs (uses DataSource only -- stays in Utilities)
- DataSourceEncapsulationTests.fs (tests DataSource -- stays in Utilities)
- DalToUtilitiesRenameTests.fs (tests Utilities namespace -- stays in Utilities)
- LogModuleStructureTests.fs (tests Log module -- stays in Utilities)
- LedgerConstraintTests.fs (raw SQL only, uses DataSource via open LeoBloom.Utilities)
- OpsConstraintTests.fs (raw SQL only, uses DataSource via open LeoBloom.Utilities)
- DeleteRestrictionTests.fs (raw SQL only, uses DataSource via open LeoBloom.Utilities)
- DomainTests.fs (only opens Domain.Ledger and Domain.Ops, no Utilities open)

---

## Phase 6: Update Solution File

### Step 6.1 — Add Ledger and Ops to the solution

```bash
cd /workspace/LeoBloom
dotnet sln add Src/LeoBloom.Ledger/LeoBloom.Ledger.fsproj --solution-folder Src
dotnet sln add Src/LeoBloom.Ops/LeoBloom.Ops.fsproj --solution-folder Src
```

### Step 6.2 — Verify solution contents

```bash
dotnet sln list
```

Should show: Migrations, Api, Domain, Utilities, Ledger, Ops, Tests.

---

## Phase 7: Build Verification

### Step 7.1 — Clean and build

```bash
cd /workspace/LeoBloom
dotnet clean
dotnet build
```

**Expected:** Zero errors, zero warnings related to this change.

**Common failure modes and fixes:**
1. Missing `open LeoBloom.Utilities` in a moved file that uses DataSource/Log
   -- add the open
2. Missing `open LeoBloom.Ledger` in Ops files that call Ledger services
   -- add the open
3. F# compile order wrong -- reorder Compile includes in fsproj
4. Missing Npgsql PackageReference -- add it

---

## Phase 8: Test Verification

### Step 8.1 — Run all tests

```bash
cd /workspace/LeoBloom
dotnet test
```

**Expected:** 428 tests passed, 0 failed, 0 skipped.

### Step 8.2 — Structural verification

```bash
# Verify Utilities only has 2 .fs files
ls Src/LeoBloom.Utilities/*.fs | wc -l  # should be 2

# Verify Ledger has 15 .fs files
ls Src/LeoBloom.Ledger/*.fs | wc -l  # should be 15

# Verify Ops has 7 .fs files
ls Src/LeoBloom.Ops/*.fs | wc -l  # should be 7

# Verify no moved file still has LeoBloom.Utilities namespace
grep -l "namespace LeoBloom.Utilities" Src/LeoBloom.Ledger/*.fs Src/LeoBloom.Ops/*.fs
# should return nothing

# Verify Ledger files have correct namespace
grep -c "namespace LeoBloom.Ledger" Src/LeoBloom.Ledger/*.fs  # 15 matches

# Verify Ops files have correct namespace
grep -c "namespace LeoBloom.Ops" Src/LeoBloom.Ops/*.fs  # 7 matches
```

---

## Acceptance Criteria

- [ ] S1: LeoBloom.Utilities.fsproj contains only Log.fs and DataSource.fs as Compile includes
- [ ] S2: LeoBloom.Ledger.fsproj exists with all 15 ledger files as Compile includes
- [ ] S3: LeoBloom.Ops.fsproj exists with all 7 ops files as Compile includes
- [ ] S4: Namespace declarations in moved files updated to LeoBloom.Ledger / LeoBloom.Ops
- [ ] S5: LeoBloom.Ledger references LeoBloom.Utilities and LeoBloom.Domain
- [ ] S6: LeoBloom.Ops references LeoBloom.Utilities, LeoBloom.Domain, and LeoBloom.Ledger
- [ ] S7: LeoBloom.Tests references LeoBloom.Ledger and LeoBloom.Ops
- [ ] S8: Test file `open` declarations include new namespaces
- [ ] S9: Solution includes LeoBloom.Ledger and LeoBloom.Ops projects
- [ ] S10: No .fs file in Utilities contains domain-specific service/repo code
- [ ] B1: `dotnet build` succeeds with zero errors
- [ ] B2: `dotnet test` -- all 428 tests pass, zero failures, zero skips
- [ ] B3: No behavioral changes -- test assertions identical pre and post

## Risks

1. **F# compile order sensitivity.** Mitigated by preserving the exact
   Utilities.fsproj order within each new project. If a file depends on another
   file within the same project, it must come after. The existing order already
   respects this.

2. **Transitive open resolution.** Test files may need both `open LeoBloom.Utilities`
   (for DataSource) AND `open LeoBloom.Ledger` (for services). The plan keeps
   all existing `open` statements and adds new ones. This is safe -- extra opens
   that happen to be unused will not cause build errors in F#.

3. **ObligationPostingService cross-domain dependency.** This file calls
   JournalEntryService (Ledger) from within Ops. Mitigated by Ops having a
   ProjectReference to Ledger and the file having `open LeoBloom.Ledger`.

4. **DalToUtilitiesRenameTests.fs — FT-DUR-005 WILL FAIL.** Test
   `LeoBloom.Utilities directory exists with all original Dal files` (line 106)
   asserts that JournalEntryRepository.fs, JournalEntryService.fs,
   AccountBalanceRepository.fs, and AccountBalanceService.fs exist in the
   Utilities directory. After our move, they won't. Builder MUST update this
   test: change the expected file list to `["DataSource.fs"; "Log.fs"]` and
   update the test name/description to reflect the post-reorg state. The other
   7 tests in this file (FT-DUR-001 through 004, 006-008) will pass as-is.

## Out of Scope

- LeoBloom.Api changes (dead code, slated for P046 deletion)
- DataHelpers.fs (P049 concern, does not exist yet)
- Any behavioral changes to services or repositories
- Gherkin scenarios (this is a structural-only project)
- Renaming DalToUtilitiesRenameTests.fs (the file rename is out of scope; updating FT-DUR-005's expected file list IS in scope per Risk #4)

## PO Approval

**APPROVED** -- 2026-04-05

**Gate 1 checklist -- all passed:**

- Objective is clear and scoped: split the god project, no behavioral changes
- Deliverables are concrete: 2 new fsproj files, 22 file moves, namespace updates, test import updates, solution registration
- Acceptance criteria are testable: 10 structural (S1-S10) + 3 build/test (B1-B3), all binary pass/fail
- All criteria are structural -- no Gherkin scenarios needed, which is correct for a refactoring project
- Phases are ordered logically with verification at each step
- Out of scope is explicit and consistent with Risk #4 (FT-DUR-005 fix)
- File classification verified against actual Utilities.fsproj: 2 + 15 + 7 = 24
- fsproj templates have correct ProjectReferences and PackageReferences
- Test update strategy is conservative and safe (add opens, don't remove)
- Cross-domain dependency (Ops -> Ledger) correctly handled

**Notes for Builder:**

1. FT-DUR-005 is a known required fix -- do not skip it. Update the expected file list and test name per Risk #4.
2. The "revised approach" in Phase 5.2 is the one to follow (keep all existing `open LeoBloom.Utilities`, add new opens alongside). Ignore the initial Group A table that says "replace."
3. Phase 4.1 says "remove all 22 moved Compile includes" -- the actual count is 22 (15 Ledger + 7 Ops). This is correct.

Proceed to build.
