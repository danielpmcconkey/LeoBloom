# LeoBloom Code Pattern Audit

**Date:** 2026-04-05
**Auditor:** Basement Dweller
**Scope:** Full codebase -- Domain, Utilities, Tests (54 non-generated .fs files)

---

## Summary Verdict

This codebase is **surprisingly consistent for its age and size**. The architecture
is clean: pure domain types in `LeoBloom.Domain`, a service/repository split in
`LeoBloom.Utilities`, and BDD-driven tests with Gherkin traceability. Naming
conventions are steady. The patterns repeat faithfully across modules.

That said, it's not all roses. There are real issues worth fixing: duplicated
helper code, inconsistent error types across the service layer, mutable state
in places where F# has idiomatic alternatives, and a handful of dead projects
taking up space. Nothing is on fire, but there's legitimate cleanup opportunity.

**Overall grade: B+.** Consistent and disciplined, with a short list of
concrete improvements that would tighten it up.

---

## Critical Issues

### 1. Inconsistent Error Type Across Services

The service layer can't agree on whether errors are `string` or `string list`.

**Write-path services** return `Result<T, string list>`:
- `JournalEntryService.post` -> `Result<PostedJournalEntry, string list>`
- `FiscalPeriodService.closePeriod` -> `Result<FiscalPeriod, string list>`
- `ObligationAgreementService.create` -> `Result<ObligationAgreement, string list>`
- `TransferService.initiate` -> `Result<Transfer, string list>`

**Read-path services** return `Result<T, string>` (singular):
- `AccountBalanceService.getBalanceById` -> `Result<AccountBalance, string>`
- `TrialBalanceService.getByPeriodId` -> `Result<TrialBalanceReport, string>`
- `IncomeStatementService.getByPeriodId` -> `Result<IncomeStatementReport, string>`
- `BalanceSheetService.getAsOfDate` -> `Result<BalanceSheetReport, string>`
- `SubtreePLService.getByAccountCodeAndPeriodId` -> `Result<SubtreePLReport, string>`

This means any caller composing write + read operations has to handle two
different error shapes. The write-path convention (`string list`) is the better
one -- it supports multiple validation errors. The read-path services should
adopt it too, or at minimum a shared `ServiceError` DU should unify them.

**Files:**
- `/workspace/LeoBloom/Src/LeoBloom.Utilities/AccountBalanceService.fs`
- `/workspace/LeoBloom/Src/LeoBloom.Utilities/TrialBalanceService.fs`
- `/workspace/LeoBloom/Src/LeoBloom.Utilities/IncomeStatementService.fs`
- `/workspace/LeoBloom/Src/LeoBloom.Utilities/BalanceSheetService.fs`
- `/workspace/LeoBloom/Src/LeoBloom.Utilities/SubtreePLService.fs`

### 2. EntryType.toDbString Exists But Is Not Used in the Repository

`Ledger.fs` defines `EntryType.toDbString` (line 241-244) which converts
`EntryType` to its database string representation. But `JournalEntryRepository.insertLines`
(line 54) hand-rolls its own inline match:

```fsharp
let etStr = match l.entryType with EntryType.Debit -> "debit" | EntryType.Credit -> "credit"
```

This is a textbook "why does the helper exist if nobody uses it" situation. If
someone changes the DB representation, they'd update `toDbString` and miss this
inline match.

**Files:**
- `/workspace/LeoBloom/Src/LeoBloom.Domain/Ledger.fs` (lines 241-244)
- `/workspace/LeoBloom/Src/LeoBloom.Utilities/JournalEntryRepository.fs` (line 54)

---

## Warnings

### 3. `optParam` Helper Duplicated Across Three Repositories

The `optParam` function for handling nullable SQL parameters is copy-pasted into
three separate repository modules, each `private`:

| File | Signature |
|------|-----------|
| `JournalEntryRepository.fs:11` | `(name: string) (value: string option) (cmd: NpgsqlCommand)` |
| `ObligationAgreementRepository.fs:16` | `(name: string) (value: obj option) (cmd: NpgsqlCommand)` |
| `ObligationInstanceRepository.fs:12` | `(name: string) (value: obj option) (cmd: NpgsqlCommand)` |
| `TransferRepository.fs:11` | `(name: string) (value: obj option) (cmd: NpgsqlCommand)` |

Three of the four have identical implementations (`obj option` variant).
`JournalEntryRepository` uses a narrower `string option` signature. This should
be a single shared utility function, with the `obj option` version being the
canonical one.

### 4. `buildSection` Duplicated Across Three Services

The exact same `buildSection` function appears in three places:

```fsharp
let private buildSection (sectionName: string) (lines: SomeLineType list) : SomeSection =
    { sectionName = sectionName
      lines = lines
      sectionTotal = lines |> List.sumBy (fun l -> l.balance) }
```

| File | Line Type |
|------|-----------|
| `IncomeStatementService.fs:8` | `IncomeStatementLine` -> `IncomeStatementSection` |
| `SubtreePLService.fs:8` | `IncomeStatementLine` -> `IncomeStatementSection` |
| `BalanceSheetService.fs:9` | `BalanceSheetLine` -> `BalanceSheetSection` |

The first two are literally identical (same types). That's copy-paste.

### 5. `repoRoot` Helper Duplicated Across Four Test Files

The `repoRoot` computation (a recursive `walkUp` function to find the solution root)
is identically copy-pasted into four structural test files:

- `/workspace/LeoBloom/Src/LeoBloom.Tests/DataSourceEncapsulationTests.fs` (line 15)
- `/workspace/LeoBloom/Src/LeoBloom.Tests/DalToUtilitiesRenameTests.fs` (line 13)
- `/workspace/LeoBloom/Src/LeoBloom.Tests/LogModuleStructureTests.fs` (line 14)
- `/workspace/LeoBloom/Src/LeoBloom.Tests/LoggingInfrastructureTests.fs` (line 16)

This belongs in `TestHelpers.fs`.

### 6. Constraint Test Helpers Duplicated Between LedgerConstraintTests and OpsConstraintTests

Both files define their own `tryExec`, `tryInsert`, `assertNotNull`, `assertFk`,
and `assertUnique` helpers -- but with **different signatures**.

`LedgerConstraintTests.fs` uses a more parameterized approach:
```fsharp
let private assertSqlState (expected: string) (ex: PostgresException option) (failMsg: string) =
```

`OpsConstraintTests.fs` hard-codes the failure message per function:
```fsharp
let private assertNotNull (ex: PostgresException option) =
    match ex with
    | Some pgEx -> Assert.Equal("23502", pgEx.SqlState)
    | None -> Assert.Fail("Expected NOT NULL violation")
```

The LedgerConstraint version is strictly better (accepts a custom fail message).
OpsConstraint was likely written later and simplified. These should be shared.

### 7. `lookupAccount` Variants Scattered Across Services

Four different services each define their own inline account-lookup SQL query:

| Service | Function | Returns |
|---------|----------|---------|
| `JournalEntryService.fs:34` | `lookupAccountActivity` | `(int * bool) list` |
| `ObligationAgreementService.fs:10` | `lookupAccount` | `(int * bool) option` |
| `TransferService.fs:11` | `lookupAccountInfo` | `(bool * string) option` |
| `OpeningBalanceService.fs:16` | `lookupAccounts` | `AccountInfo list` |

Each hits `ledger.account` with slight variations. The queries are close enough
that a shared "resolve account by ID with type info" repository function would
eliminate all four.

### 8. Mutable State in Domain Logic (generateExpectedDates)

`ObligationInstanceSpawning.generateExpectedDates` in `Ops.fs` (lines 249-290)
uses `let mutable` with imperative while-loops to build date lists for Monthly,
Quarterly, and Annual cadences. This is the domain layer -- the one place
immutability should be non-negotiable.

These are textbook `List.unfold` or `Seq.unfold` candidates. The OneTime case
already returns `[ startDate ]` immutably, making the inconsistency more obvious.

**File:** `/workspace/LeoBloom/Src/LeoBloom.Domain/Ops.fs` (lines 254-290)

### 9. IncomeStatementLine and BalanceSheetLine Are Structurally Identical

Both types have the exact same shape:

```fsharp
type IncomeStatementLine =
    { accountId: int; accountCode: string; accountName: string; balance: decimal }

type BalanceSheetLine =
    { accountId: int; accountCode: string; accountName: string; balance: decimal }
```

Defined in `Ledger.fs` at lines 181-185 and 201-205. Having two types with
identical fields suggests they should be unified into a single
`ReportAccountLine` type (or similar), with the report section determining
the semantic meaning.

---

## Observations (Low Priority)

### 10. Dead/Scaffold Projects

Several projects under `Src/` contain zero source files (only `obj/` artifacts):

- `LeoBloom.Ledger` -- empty
- `LeoBloom.Ops` -- empty
- `LeoBloom.Ledger.Tests` -- empty
- `LeoBloom.Ops.Tests` -- empty
- `LeoBloom.Data` -- empty (only `bin/` and `obj/`)
- `LeoBloom.Api` -- contains only the default ASP.NET `WeatherForecast`
  scaffold (temperature converter, untouched)

These are dead weight in the solution file. If they represent future plans, they
should be documented. Otherwise they're noise.

### 11. Mutable State in Repository While-Loops

The repository layer uses `let mutable results = []` with `while reader.Read()`
loops to accumulate results from database readers. This appears in:

- `TrialBalanceRepository.getActivityByPeriod`
- `IncomeStatementRepository.getActivityByPeriod`
- `BalanceSheetRepository.getCumulativeBalances`
- `BalanceSheetRepository.getRetainedEarnings`
- `ObligationAgreementRepository.list`
- `ObligationInstanceRepository.findOverdueCandidates`
- `ObligationInstanceRepository.findExistingDates`
- `SubtreePLRepository.getSubtreeActivityByPeriod`

This is a pragmatic choice -- `DbDataReader` is inherently imperative, so
mutable accumulation is arguably the clearest way to consume it. Not a bug.
But if consistency with functional style is a goal, these could use a
`Seq.unfold`-style helper that wraps the reader.

This is explicitly NOT the same issue as #8 -- domain logic should be pure;
repository code touching an imperative API gets more leeway.

### 12. `Log.initialize()` Called from TestCleanup.create

`TestCleanup.create` (TestHelpers.fs:39) calls `Log.initialize()`. This is the
only initialization path for test runs, meaning the logger depends on test
infrastructure setup rather than having its own test bootstrap. It works because
`Log.initialize()` is idempotent, but the coupling is implicit.

### 13. Service Error Messages Prefix Inconsistency

Error messages from the `with ex` catch blocks use three different prefixes:
- `"Persistence error: %s"` (write-path services)
- `"Query error: %s"` (read-path services)
- `"Read phase error: %s"` (multi-phase services like TransferService.confirm)

The distinction is meaningful, but the caller can't programmatically distinguish
between "the DB was down" and "the query was bad." If this ever needs to be
machine-readable, a typed error DU would be necessary.

### 14. Explicit `reader.Close()` Calls Despite `use` Binding

Every repository function calls `reader.Close()` explicitly, even though the
reader is bound with `use` (which calls `Dispose()` -> `Close()`). The 50+
explicit `reader.Close()` calls are technically redundant.

This appears to be a deliberate defensive pattern -- and it's consistently
applied everywhere, so it's not a bug or inconsistency. Just noise.

### 15. Naming Conventions Are Consistent

F# naming conventions are followed throughout:
- **camelCase** for record fields, function parameters, local bindings
- **PascalCase** for types, DU cases, module names
- **camelCase** for public module functions (idiomatic F#)
- Database column names use **snake_case** in SQL strings (correct PostgreSQL convention)
- No deviations found in any file

### 16. Test Pattern Consistency

Tests follow a rigid pattern across all 22 test files:
- `[<Fact>]` or `[<Theory>]` with `[<Trait("GherkinId", "FT-XXX-NNN")>]` traceability
- Double-backtick test names describing behavior in plain English
- `use conn = DataSource.openConnection()` / `let tracker = TestCleanup.create conn`
- `try ... finally TestCleanup.deleteAll tracker`
- `match result with | Ok ... | Error ...` assertion pattern

This is rock-solid. The only variance is pure domain tests in `DomainTests.fs`
which don't need database setup, and structural tests which use reflection/filesystem.

---

## Recommendations (Priority Order)

1. **Unify error type** to `Result<T, string list>` across all services. This is
   the single highest-value change because it affects the public API contract.

2. **Use `EntryType.toDbString`** in `JournalEntryRepository.insertLines` instead
   of the inline match. One-line fix.

3. **Extract shared utilities**: `optParam`, `buildSection`, `repoRoot` walkUp,
   and constraint test helpers into shared modules. Medium effort, high hygiene value.

4. **Consolidate account-lookup queries** into a shared repository function.
   Four services each rolling their own SQL to check if an account exists is
   a maintenance hazard.

5. **Refactor `generateExpectedDates`** to use immutable constructs. Domain
   layer should be the showcase for functional style.

6. **Delete or document empty projects.** If `LeoBloom.Ledger`, `LeoBloom.Ops`,
   etc. are planned future homes, add a one-line comment or README. Otherwise
   remove them from the solution.

7. **Consider unifying `IncomeStatementLine`/`BalanceSheetLine`** into a single
   `ReportAccountLine` type. Lower priority since the current separate types
   don't cause bugs, just duplication.
