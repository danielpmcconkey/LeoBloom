# LeoBloom Simplicity Review

**Reviewer:** BD (basement dweller)
**Date:** 2026-04-05
**Codebase:** ~3,500 LOC production, ~8,900 LOC tests (12,417 total)

---

## Summary Verdict

The production code is **surprisingly clean for a BDD-driven project**. The domain
layer is tight, the service/repository split is consistent, and the F# module style
avoids the interface/DI ceremony that sinks most .NET projects. The main problems are
dead project shells, accumulated refactoring-proof tests that have outlived their
usefulness, duplicated boilerplate, and transactions wrapping read-only queries. This
is not an over-engineered codebase -- it's a codebase with residue.

**Overall complexity: Low-Medium.**
The production code is close to minimal. The test suite is where the bloat lives.

---

## Critical Issues

### 1. Five Empty Ghost Projects

**Files:** `Src/LeoBloom.Data/`, `Src/LeoBloom.Ledger/`, `Src/LeoBloom.Ops/`,
`Src/LeoBloom.Ledger.Tests/`, `Src/LeoBloom.Ops.Tests/`

Each contains only `bin/` and `obj/` directories. No source files, no `.fsproj` files.
They are not referenced in `LeoBloom.sln`. These are corpses from a previous
reorganization.

**Action:** Delete all five directories. They are pure noise.

**Estimated cleanup:** 5 directories removed.

### 2. The Entire LeoBloom.Api Project is Scaffolding

**Files:** `Src/LeoBloom.Api/WeatherForecast.fs`,
`Src/LeoBloom.Api/Controllers/WeatherForecastController.fs`,
`Src/LeoBloom.Api/Program.fs`, `Src/LeoBloom.Api/LeoBloom.Api.fsproj`

Per memory, [the CLI direction doc](../../../.claude/projects/-workspace/memory/project_leobloom_cli.md)
says the consumption layer is CLI, not REST API, and API projects 023-027 were
cancelled. Yet LeoBloom.Api still exists in the solution with the default .NET
WeatherForecast template code. It references `LeoBloom.Domain` and
`LeoBloom.Utilities` but does absolutely nothing with them.

`Program.fs` calls `Log.initialize()` and then serves a WeatherForecast endpoint
that has zero relationship to double-entry bookkeeping.

**Action:** Remove `LeoBloom.Api` from the solution and delete the directory. It is
a vestigial scaffold from a cancelled direction. ~90 LOC.

### 3. Invoice Type is Dead Code

**File:** `Src/LeoBloom.Domain/Ops.fs`, lines 143-155

The `Invoice` record type is defined but never referenced anywhere in the codebase
outside of its definition. No repository, no service, no test touches it. It's a
domain type for a feature that doesn't exist yet.

**Action:** Delete the `Invoice` type. When invoices are needed, add it then.
YAGNI. ~13 LOC.

---

## Warnings

### 4. Transactions Wrapping Read-Only Queries

**Files:** `AccountBalanceService.fs`, `TrialBalanceService.fs`,
`IncomeStatementService.fs`, `BalanceSheetService.fs`, `SubtreePLService.fs`,
`ObligationAgreementService.fs` (getById, list)

Every read-only service function opens a connection, starts a transaction, runs a
SELECT, commits the transaction, and has a rollback in the catch block. Example from
`AccountBalanceService.fs`:

```fsharp
use conn = DataSource.openConnection()
use txn = conn.BeginTransaction()
try
    let result = ...  // just a SELECT
    txn.Commit()
    result
with ex ->
    try txn.Rollback() with _ -> ()
    Error (...)
```

For pure reads, the transaction buys you nothing (Postgres already gives you
snapshot isolation at the statement level). The Commit/Rollback ceremony is noise. At
minimum ~12 service functions are affected.

**Severity:** Low -- it works, it's just unnecessary ceremony. If all repositories
already require a transaction parameter, this might be a deliberate consistency
choice. But if so, it's consistency for consistency's sake, not for correctness.

**Action:** Consider letting read-only repository functions accept
`NpgsqlConnection` instead of `NpgsqlTransaction`, or at least document why you're
paying transaction overhead on reads.

### 5. Duplicated `optParam` Helper (4 copies)

**Files:**
- `JournalEntryRepository.fs:11` (takes `string option`)
- `ObligationAgreementRepository.fs:16` (takes `obj option`)
- `TransferRepository.fs:11` (takes `obj option`)
- `ObligationInstanceRepository.fs:12` (takes `obj option`)

Three of these are identical (`obj option` variant). The fourth takes `string option`
specifically. This is a small helper function copy-pasted into each module.

**Action:** Extract to a shared module (e.g., top of a `DbHelpers.fs` or add to
`DataSource.fs`). Or live with it -- it's small and private. Not a hill to die on.

**Estimated cleanup:** ~12 LOC if consolidated.

### 6. Duplicated `buildSection` Helper (3 copies)

**Files:**
- `IncomeStatementService.fs:8`
- `SubtreePLService.fs:8`
- `BalanceSheetService.fs:9`

All three are identical 3-line functions that build a section record from a name and
line list. The IncomeStatementService and SubtreePLService versions both return
`IncomeStatementSection`; the BalanceSheetService version returns
`BalanceSheetSection` (which has the same shape but different type).

**Action:** The two `IncomeStatementSection` versions could easily share code. The
`BalanceSheetSection` one can't without a generic type or shared base, which isn't
worth it. Consolidating the first two is a 30-second change.

### 7. Duplicated `repoRoot` Resolver (4 copies in test files)

**Files:** `DalToUtilitiesRenameTests.fs:13`, `DataSourceEncapsulationTests.fs:15`,
`LogModuleStructureTests.fs:14`, `LoggingInfrastructureTests.fs:16`

The exact same `walkUp` function for finding the repo root directory is copied into
four test files. Should be in `TestHelpers.fs`.

**Action:** Move to `TestHelpers.fs`, import from there.

**Estimated cleanup:** ~40 LOC.

### 8. `FiscalPeriodCheck` Type is Redundant with `FiscalPeriod`

**File:** `JournalEntryService.fs:13-14`

`FiscalPeriodCheck` is a private subset of `FiscalPeriod` (missing only `periodKey`
and `createdAt`). It exists solely for the `lookupFiscalPeriod` function inside
`JournalEntryService`. You could use `FiscalPeriodRepository.findById` and the
existing `FiscalPeriod` type instead.

**Action:** Replace with `FiscalPeriodRepository.findById`. Eliminates the private
type and its dedicated query. ~18 LOC.

### 9. `lookupAccountActivity` in JournalEntryService Duplicates Pattern from ObligationAgreementService

**File:** `JournalEntryService.fs:34-51`

`lookupAccountActivity` builds parameterized IN-clause queries for account
existence/activity checks. `ObligationAgreementService.lookupAccount` does the same
thing for a single account. The account-lookup-by-id pattern appears in at least 3
places across the utilities layer.

**Severity:** Low. These are small, focused queries. Consolidation would create a
shared helper but also add a new dependency. F# module-per-feature isolation is
arguably more important here.

---

## Observations (Not Issues)

### 10. Refactoring-Proof Tests Have Diminishing Returns

**Files:** `DalToUtilitiesRenameTests.fs` (152 LOC), `DataSourceEncapsulationTests.fs`
(199 LOC), `LogModuleStructureTests.fs` (236 LOC)

These are BDD-driven tests that verify structural properties of the codebase itself:
"no namespace LeoBloom.Dal exists," "Serilog package is referenced," "no printfn in
source files," "Migrations has no reference to LeoBloom.Utilities." Several are
tautological:

- `FT-DUR-007`: "Full solution builds with zero rename-related warnings" --
  `Assert.True(true)`. If the test compiles, it passes. The test adds nothing.
- `FT-DUR-008`: "All tests pass after rename" -- `Assert.True(true)`. Same.
- `FT-DSI-006`: "Full solution builds successfully" -- `Assert.True(true)`.
- `FT-DSI-007`: "All existing tests pass" -- `Assert.True(true)`.

The non-tautological structural tests (checking for namespace references, package
references, etc.) are useful guards during a rename but become dead weight after. The
rename happened. The old name is gone. These tests will never fail again unless
someone deliberately reintroduces `LeoBloom.Dal`.

**Cost:** ~587 LOC of tests that guard against ghosts.

**Action:** This is a judgment call, not a clear-cut delete. If the BDD process
requires these to exist for traceability, keep them. If they're just tests, they're
maintenance burden for no future value. At minimum, the `Assert.True(true)` tests
should be deleted -- they are literally asserting that `true` is `true`.

### 11. `reader.Close()` Calls Are Unnecessary

**Files:** Every repository file. 50 occurrences across 15 files.

Every `NpgsqlCommand` is wrapped in `use`, which means the reader gets disposed
(and closed) at the end of the scope. The explicit `reader.Close()` calls are
belt-and-suspenders. They're not harmful, but they're noise.

**Severity:** Informational. This is a style choice, not a bug. The code is
consistent about it, which matters more than whether it's technically necessary.

### 12. The `Ledger.validateVoidReason` Function is Unused

**File:** `Src/LeoBloom.Domain/Ledger.fs:86-91`

`validateVoidReason` is defined in the domain but only referenced in
`DomainTests.fs`. The actual void path in `JournalEntryService.validateVoidCommand`
does its own inline validation. This domain function is orphaned.

**Action:** Either use it in `JournalEntryService` (replacing the inline check) or
delete it. Currently it's tested but never called in production code.

### 13. Connection String Construction is Duplicated Between DataSource.fs and Migrations/Program.fs

**Files:** `Src/LeoBloom.Utilities/DataSource.fs:12-36`,
`Src/LeoBloom.Migrations/Program.fs:10-33`

Nearly identical code: read env, build config, get template, replace password
placeholder. This is intentionally duplicated (the comment in Migrations says so),
and the `DataSourceEncapsulationTests` specifically verify that Migrations does NOT
reference LeoBloom.Utilities. So this is a deliberate design decision, not an
oversight. Just noting it for completeness.

### 14. Service-per-Domain-Concept Creates Many Small Files

The Utilities project has 22 source files. Most service files are 40-70 lines. Most
repository files are 50-90 lines. This is not over-engineering -- each file does one
thing -- but it's worth noting that the file count is high relative to the LOC. The
alternative (fewer, larger files) has its own problems. I'd leave this alone.

---

## YAGNI Violations

| Item | Severity | LOC |
|------|----------|-----|
| Invoice type (no consumer) | Medium | 13 |
| LeoBloom.Api project (cancelled direction) | High | 90 |
| 5 empty project directories | High | 0 (dirs) |
| `Assert.True(true)` tautology tests | Low | ~20 |
| `validateVoidReason` domain function (unused in prod) | Low | 6 |

---

## Final Assessment

| Metric | Value |
|--------|-------|
| Total potential LOC reduction (production) | ~120 (Api + Invoice + dead helpers) |
| Total potential LOC reduction (tests) | ~600 (structural guard tests + tautologies) |
| Directory cleanup | 6 directories (5 empty + Api) |
| Complexity score | Low-Medium |
| Recommended action | Clean up the dead weight, leave the production code mostly alone |

The production code is honest. It does what it says, says what it does, and doesn't
try to be clever. The domain types are plain records, the validators are pure
functions, the repository layer is raw SQL without an ORM, and the service layer is
thin orchestration. That's a good architecture for a bookkeeping engine.

The mess is in the margins: abandoned project shells, template scaffolding from a
cancelled direction, and BDD structural tests that have outlived their purpose. Clean
those out and this is a tight codebase.
