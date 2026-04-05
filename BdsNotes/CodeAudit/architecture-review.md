# LeoBloom Architecture Review

**Date:** 2026-04-05
**Reviewer:** BD (basement dweller)
**Scope:** Full codebase audit -- layering, dependencies, domain integrity, coupling, separation of concerns, anti-patterns.

---

## Summary Verdict

This is a surprisingly disciplined codebase for its size. The domain model is genuinely rich (not anemic), dependency direction is correct, and there is clear intentionality behind the architecture. The BDD test suite is enforcing architectural constraints programmatically, which is the kind of thing most teams talk about but never actually do.

That said, there is one structural problem that will bite hard at scale, and several lesser issues worth addressing before the codebase grows further.

**Overall grade: B+.** The bones are solid. The rot is localized and fixable.

---

## Critical Issues

### CRIT-1: LeoBloom.Utilities is a god project

This is the big one. `LeoBloom.Utilities` contains **22 source files** spanning repositories, services, data access, logging, and configuration. It is simultaneously:

- The persistence layer (all `*Repository.fs` files)
- The application/service layer (all `*Service.fs` files)
- The infrastructure layer (`DataSource.fs`, `Log.fs`)

Every service depends on every repository through module-level static calls. Every service depends on `DataSource` and `Log`. There is no separation between these concerns at the project level -- they are all one flat namespace.

**Why this matters:**
- You cannot swap or mock data access without replacing the entire project. Every integration test must hit a real database because `DataSource.openConnection()` is a static call with no seam.
- You cannot extract a CLI consumption layer that uses services without also dragging in the raw SQL repositories, Npgsql, Serilog, and configuration infrastructure.
- The project file order in the `.fsproj` is doing the work that project boundaries should be doing. If someone reorders the Compile includes, things break silently.

**Concrete evidence:**
- `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` -- 22 Compile includes, references Npgsql, Serilog, Microsoft.Extensions.Configuration all in one project.
- `ObligationPostingService.fs` calls `ObligationInstanceRepository`, `ObligationAgreementRepository`, `FiscalPeriodRepository`, `JournalEntryService`, and `ObligationInstanceService` -- five cross-cutting dependencies within the same project, invisible to the compiler's dependency analysis.

**What should happen:** Split into at minimum:
- `LeoBloom.Infrastructure` (DataSource, Log, configuration)
- `LeoBloom.Persistence` (repositories, raw SQL)
- `LeoBloom.Services` (orchestration, business logic that touches the DB)

Or keep two projects but introduce interface modules/types that create explicit seams.

### CRIT-2: Non-atomic multi-phase operations with no compensation

`ObligationPostingService.postToLedger` and `TransferService.confirm` both execute a multi-phase workflow across separate database connections/transactions:

1. Read + validate (connection 1, committed)
2. Post journal entry via `JournalEntryService.post` (connection 2, committed)
3. Update the source record (connection 3, committed)

If phase 3 fails after phase 2 succeeds, you have a journal entry in the ledger with no corresponding status update on the transfer/obligation instance. The code acknowledges this with a log warning ("retry is safe") but there is no actual retry mechanism, no outbox pattern, and no compensation transaction.

**Concrete evidence:**
- `Src/LeoBloom.Utilities/TransferService.fs` lines 152-165: phase 3 failure after journal entry creation.
- `Src/LeoBloom.Utilities/ObligationPostingService.fs` lines 120-127: same pattern.

**Why this matters:** In a bookkeeping system, a journal entry that exists but is not reflected in the operational record is a data integrity bug. The whole point of double-entry bookkeeping is that the books balance. An orphaned journal entry breaks that guarantee.

---

## Warnings

### WARN-1: Domain types are ID-coupled, not entity-coupled

Domain records reference related entities by integer ID (`accountTypeId: int`, `fiscalPeriodId: int`, `obligationAgreementId: int`). This is a persistence concern leaking into the domain. The domain types are essentially database row shapes, not domain objects.

**Concrete evidence:**
- `Src/LeoBloom.Domain/Ledger.fs` -- `Account` has `accountTypeId: int` instead of carrying the `AccountType` or at minimum the `NormalBalance` it needs.
- `Src/LeoBloom.Domain/Ops.fs` -- `ObligationInstance` has `obligationAgreementId: int`.

**Impact:** Every service that needs to work with an entity and its related data must make a separate repository call and manually join the results. This is exactly what `ObligationPostingService` does across 60+ lines of nested match expressions. The domain types are not telling you what an obligation instance *is* -- they are telling you what row it came from.

**Mitigation:** For a system this size with no ORM, this is a defensible trade-off. It becomes indefensible when you add a CLI or any read model that needs to present denormalized views. Watch it.

### WARN-2: Mutable accumulation patterns in F# code

Several files use `let mutable` with list accumulation where idiomatic F# would use folds, sequences, or computation expressions.

**Concrete evidence:**
- `Src/LeoBloom.Utilities/ObligationInstanceService.fs` lines 142-157: `let mutable transitioned = 0; let mutable errors = []` in `detectOverdue`.
- `Src/LeoBloom.Utilities/ObligationAgreementRepository.fs` lines 97-134: `let mutable clauses = []; let mutable paramList = []` in `list`.
- `Src/LeoBloom.Domain/Ops.fs` lines 259-290: `let mutable dates = []` in `generateExpectedDates` (multiple branches).
- `Src/LeoBloom.Utilities/JournalEntryService.fs` lines 46-51: `let mutable results = []` in `lookupAccountActivity`.
- `Src/LeoBloom.Utilities/OpeningBalanceService.fs` lines 78, 97-98: `let mutable errors = []; let mutable totalDebits = 0m`.

**Impact:** These are not bugs, but they are code smells in F#. Mutable state makes reasoning about correctness harder and is the kind of thing that invites bugs during future modifications. The domain module (`Ops.fs`) should be especially clean since it is the pure core.

### WARN-3: `ListAgreementsFilter` type defined in the repository, not the domain

`ListAgreementsFilter` is defined at the top of `Src/LeoBloom.Utilities/ObligationAgreementRepository.fs` (line 7), outside the module, in the `LeoBloom.Utilities` namespace. This is a query/filter DTO that references domain types (`ObligationDirection`, `RecurrenceCadence`).

**Impact:** It should live in the domain or in a shared types location. Defining it in the repository file means the service layer has a dependency on the repository file's namespace for a type that has nothing to do with persistence. It also breaks the pattern where all Ops-related types live in `LeoBloom.Domain.Ops`.

### WARN-4: Cross-repository calls within services blur boundaries

`IncomeStatementService` and `SubtreePLService` both call `TrialBalanceRepository.periodExists` and `TrialBalanceRepository.resolvePeriodId` for fiscal period lookups. This means the income statement service depends on the trial balance repository -- a completely unrelated reporting concern.

**Concrete evidence:**
- `Src/LeoBloom.Utilities/IncomeStatementService.fs` lines 36, 54: calls to `TrialBalanceRepository`.
- `Src/LeoBloom.Utilities/SubtreePLService.fs` lines 41, 63: same.

**Impact:** The period resolution logic should live in `FiscalPeriodRepository` (which already has `findById` and `findByDate` but lacks `findByPeriodKey`). Three services reaching into an unrelated repository for a utility function is a cohesion smell.

### WARN-5: Tests are 100% integration, 0% unit (for services)

The domain tests (`DomainTests.fs`) are proper unit tests. Good. But every service test (`PostJournalEntryTests.fs`, `TransferTests.fs`, etc.) requires a live Postgres connection. There is no way to test service orchestration logic without the database because:

1. Repositories are static module functions with no abstraction.
2. `DataSource.openConnection()` is hardcoded with no injection point.

**Impact:** Test suite speed degrades linearly with test count. Any CI/CD pipeline needs a live Postgres instance. You cannot test edge cases in service logic without actually setting up the full database state.

### WARN-6: Inconsistent error return types across services

Some services return `Result<T, string list>` (journal entry, obligations, transfers) while others return `Result<T, string>` (account balance, trial balance, income statement, balance sheet). There is no unified error type.

**Concrete evidence:**
- `JournalEntryService.post` returns `Result<PostedJournalEntry, string list>`
- `AccountBalanceService.getBalanceById` returns `Result<AccountBalance, string>`
- `TrialBalanceService.getByPeriodId` returns `Result<TrialBalanceReport, string>`

**Impact:** Every consumer must handle two different error shapes. A CLI layer will need to pattern-match on both. This is exactly the kind of inconsistency that compounds as the system grows.

---

## Observations

### OBS-1: Ghost projects in the solution tree

`LeoBloom.Ledger`, `LeoBloom.Ops`, `LeoBloom.Data`, `LeoBloom.Ledger.Tests`, and `LeoBloom.Ops.Tests` exist as directories under `Src/` but contain no source files and are not referenced in the solution file. They appear to be scaffolding from a planned but unexecuted restructuring.

**Impact:** Clutter. Either execute the restructuring or delete the directories.

### OBS-2: The Api project is a scaffold with no real endpoints

`Src/LeoBloom.Api/` contains only the default `WeatherForecastController` from the .NET template. The MEMORY.md mentions API projects 023-027 were cancelled in favor of a CLI direction.

**Impact:** Dead code. Should be removed from the solution or at minimum noted as deprecated.

### OBS-3: Architectural constraint tests are genuinely good

The `DataSourceEncapsulationTests.fs` and `LogModuleStructureTests.fs` files enforce architectural rules via reflection and file scanning -- ensuring `DataSource` only exposes `openConnection`, that `Migrations` has no reference to `Utilities`, that no code uses `printfn` outside `Migrations`, etc.

This is the right approach. These tests survive refactoring better than code review checklists.

### OBS-4: Domain validation is clean and well-separated

The domain module has a clear separation: types at the top, pure validators in the middle, command DTOs at the bottom. Validators are composable (`validateCommand` collects errors from multiple validators). The Ledger/Ops split with the explicit one-way dependency ("Ops can reference Ledger, Ledger cannot reference Ops") is documented and enforced by file ordering.

### OBS-5: Transaction management is consistent

Every service follows the same pattern: open connection, begin transaction, try/catch with rollback on failure. Repositories take `NpgsqlTransaction` as a parameter, never own their own connections. This is the right pattern for the current architecture.

### OBS-6: The `optParam` helper is duplicated

`JournalEntryRepository.fs`, `ObligationAgreementRepository.fs`, `ObligationInstanceRepository.fs`, and `TransferRepository.fs` each define their own private `optParam` function. These are nearly identical (minor signature differences in whether value is `string option` or `obj option`).

**Impact:** Minor. Not a bug. But when you do split the Utilities project, consolidate these into a shared persistence helper.

### OBS-7: `DataSource` eagerly initializes at module load

`Src/LeoBloom.Utilities/DataSource.fs` creates the connection pool as a module-level `let` binding. This means any code that references *any* type in `LeoBloom.Utilities` triggers the database connection. The `#if DEBUG` safety guard that verifies the database name is good defensive programming.

### OBS-8: `Log` uses mutable state for idempotent initialization

`Src/LeoBloom.Utilities/Log.fs` line 14: `let mutable private initialized = false`. This is a reasonable pragmatic choice for a static logging module, but it is not thread-safe. If two threads call `Log.initialize()` simultaneously during startup, you could get double initialization. In practice this probably never matters because `initialize()` is called once at startup, but it is technically a race condition.

---

## Dependency Map

```
LeoBloom.Domain          (pure types + validators, zero dependencies)
    ^
    |
LeoBloom.Utilities       (repos + services + infrastructure, depends on Domain + Npgsql + Serilog)
    ^
    |
LeoBloom.Tests           (depends on Domain + Utilities + Npgsql + xUnit)
LeoBloom.Api             (depends on Domain + Utilities, scaffold only)

LeoBloom.Migrations      (standalone, no project references, own Npgsql + Migrondi)
```

Dependency direction is correct: nothing depends upward, Domain is pure, Migrations is isolated. The problem is not direction -- it is that Utilities is one undifferentiated blob where direction *within* the project is enforced only by file ordering.

---

## Prioritized Recommendations

1. **Split Utilities** (addresses CRIT-1, WARN-4, WARN-5). This is the single highest-leverage change. At minimum, extract infrastructure (`DataSource`, `Log`) into its own project so services and repositories can be tested in isolation.

2. **Wrap multi-phase operations in a single transaction** (addresses CRIT-2). `ObligationPostingService.postToLedger` and `TransferService.confirm` should do all three phases in one transaction, or implement an outbox/saga pattern. For the current scale, single transaction is the right call.

3. **Unify error types** (addresses WARN-6). Define a `ServiceError` type in Domain. `Result<T, ServiceError>` everywhere.

4. **Move `ListAgreementsFilter` to Domain** (addresses WARN-3). Trivial fix, do it now.

5. **Add `resolvePeriodId`/`periodExists` to `FiscalPeriodRepository`** (addresses WARN-4). Stop reaching into `TrialBalanceRepository` for period lookups.

6. **Clean up ghost projects and Api scaffold** (addresses OBS-1, OBS-2). Remove noise from the solution.
