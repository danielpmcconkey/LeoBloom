# Project 009 -- Close / Reopen Fiscal Period -- Plan

## Objective

Toggle `fiscal_period.is_open` via service functions. Close sets `is_open = false`;
reopen sets `is_open = true` and requires a reason string (logged via `Log.info`,
no audit table). No schema changes -- the `is_open` column already exists and the
posting engine (005) already rejects entries to closed periods.

## Scope

**In scope:** Two service operations (`closePeriod`, `reopenPeriod`), their
repository functions, domain command DTOs, and integration tests.

**Out of scope:** Audit event table, API endpoints, ops-layer warnings about
in-flight obligations, adding/removing fiscal periods.

---

## Phase 1: Domain Types

**What:** Add command DTOs to `Ledger.fs` and a pure validator for the reopen reason.

**File:** `Src/LeoBloom.Domain/Ledger.fs`

**Changes (append after the existing command DTOs, before the `EntryType` companion module around line 156):**

```fsharp
type CloseFiscalPeriodCommand =
    { fiscalPeriodId: int }

type ReopenFiscalPeriodCommand =
    { fiscalPeriodId: int
      reason: string }
```

Add a pure validator for the reopen reason (same pattern as `validateVoidReason`):

```fsharp
let validateReopenReason (reason: string) : Result<unit, string list> =
    if String.IsNullOrWhiteSpace reason then
        Error [ "Reopen reason is required and cannot be empty" ]
    else Ok ()
```

**Verification:** `dotnet build Src/LeoBloom.Domain`

---

## Phase 2: Repository

**What:** Add two functions to a new `FiscalPeriodRepository.fs` module in
`LeoBloom.Utilities`. Follows the same transaction-injection pattern as
`JournalEntryRepository`.

**File (new):** `Src/LeoBloom.Utilities/FiscalPeriodRepository.fs`

```fsharp
module FiscalPeriodRepository =

    /// Look up a fiscal period by ID. Returns None if not found.
    let findById (txn: NpgsqlTransaction) (periodId: int) : FiscalPeriod option

    /// Set is_open on the given period. Returns updated FiscalPeriod or None.
    let setIsOpen (txn: NpgsqlTransaction) (periodId: int) (isOpen: bool) : FiscalPeriod option
```

`setIsOpen` SQL:

```sql
UPDATE ledger.fiscal_period SET is_open = @is_open WHERE id = @id
RETURNING id, period_key, start_date, end_date, is_open, created_at
```

Map the RETURNING row to the existing `Ledger.FiscalPeriod` domain type.

**File (modify):** `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj`

Add `<Compile Include="FiscalPeriodRepository.fs" />` after `JournalEntryRepository.fs`
and before `JournalEntryService.fs` (F# compilation order matters).

**Verification:** `dotnet build Src/LeoBloom.Utilities`

---

## Phase 3: Service

**What:** Add `FiscalPeriodService.fs` with `closePeriod` and `reopenPeriod`.
Follows the `JournalEntryService` pattern: opens its own connection + transaction,
validates, persists, returns `Result<FiscalPeriod, string list>`.

**File (new):** `Src/LeoBloom.Utilities/FiscalPeriodService.fs`

```fsharp
module FiscalPeriodService =

    /// Close a fiscal period. Idempotent -- closing an already-closed period succeeds.
    let closePeriod (cmd: CloseFiscalPeriodCommand) : Result<FiscalPeriod, string list>

    /// Reopen a fiscal period. Requires a non-empty reason. Idempotent.
    /// Logs the reason via Log.info.
    let reopenPeriod (cmd: ReopenFiscalPeriodCommand) : Result<FiscalPeriod, string list>
```

Logic for `closePeriod`:
1. Open connection + transaction.
2. `FiscalPeriodRepository.findById` -- if None, return `Error ["Fiscal period with id X does not exist"]`.
3. `FiscalPeriodRepository.setIsOpen txn id false`.
4. Commit. `Log.info "Closed fiscal period {PeriodId}"`.
5. Return `Ok period`.

Logic for `reopenPeriod`:
1. Pure validation: `validateReopenReason cmd.reason` -- fail fast.
2. Open connection + transaction.
3. `FiscalPeriodRepository.findById` -- if None, return error.
4. `FiscalPeriodRepository.setIsOpen txn id true`.
5. Commit. **Log the reason:** `Log.info "Reopened fiscal period {PeriodId}. Reason: {Reason}"`.
6. Return `Ok period`.

**File (modify):** `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj`

Add `<Compile Include="FiscalPeriodService.fs" />` after `FiscalPeriodRepository.fs`
and before `AccountBalanceRepository.fs`.

**Verification:** `dotnet build Src/LeoBloom.Utilities`

---

## Phase 4: Tests

**What:** Integration tests in a new `FiscalPeriodTests.fs`. Pattern matches
`VoidJournalEntryTests.fs` exactly: `use conn`, tracker, try/finally cleanup.

**File (new):** `Src/LeoBloom.Tests/FiscalPeriodTests.fs`

**File (modify):** `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj`

Add `<Compile Include="FiscalPeriodTests.fs" />` after `TrialBalanceTests.fs`.

**Test cases (GherkinId prefixes TBD by Gherkin Writer):**

| # | Scenario | Key assertion |
|---|----------|---------------|
| 1 | Close an open period | `result = Ok`, `period.isOpen = false` |
| 2 | Close an already-closed period (idempotent) | `result = Ok`, `period.isOpen = false` |
| 3 | Reopen a closed period with reason | `result = Ok`, `period.isOpen = true` |
| 4 | Reopen an already-open period (idempotent) | `result = Ok`, `period.isOpen = true` |
| 5 | Reopen with empty reason rejected | `result = Error`, message contains "reason" |
| 6 | Reopen with whitespace-only reason rejected | `result = Error`, message contains "reason" |
| 7 | Close nonexistent period rejected | `result = Error`, message contains "does not exist" |
| 8 | Reopen nonexistent period rejected | `result = Error`, message contains "does not exist" |
| 9 | Close then reopen then close again (full cycle) | All three operations succeed, final `isOpen = false` |
| 10 | Posting rejected after close | Post a JE targeting a closed period, get `Error` with "not open" |
| 11 | Close an empty period | Insert a period with no entries, close succeeds |

Each test creates its own fiscal period via `InsertHelpers.insertFiscalPeriod` and
cleans up via `TestCleanup.deleteAll`. Test 10 also creates accounts and uses
`JournalEntryService.post`.

**Verification:** `dotnet test Src/LeoBloom.Tests --filter "FullyQualifiedName~FiscalPeriod"`

---

## Acceptance Criteria

- [ ] `FiscalPeriodService.closePeriod` sets `is_open = false` and returns `Ok FiscalPeriod`
- [ ] `FiscalPeriodService.reopenPeriod` sets `is_open = true` and returns `Ok FiscalPeriod`
- [ ] Reopen with empty/whitespace reason returns `Error` with descriptive message
- [ ] Nonexistent period ID returns `Error` for both close and reopen
- [ ] Both operations are idempotent (closing a closed period is Ok, reopening an open period is Ok)
- [ ] Reopen reason is logged via `Log.info` (visible in test console output)
- [ ] `JournalEntryService.post` still rejects entries to a period closed by `closePeriod` (integration proof)
- [ ] Close/reopen/close cycle works with no errors
- [ ] Empty period (no journal entries) can be closed
- [ ] All 11 tests pass: `dotnet test --filter "FullyQualifiedName~FiscalPeriod"`
- [ ] Full test suite passes: `dotnet test Src/LeoBloom.Tests`

## Risks

- **Concurrency:** Two callers could race to close/reopen the same period.
  Mitigation: the UPDATE is atomic at the DB level and we're not doing
  read-check-write (just UPDATE...RETURNING). Acceptable for a single-user app.
- **Compilation order:** F# fsproj is order-sensitive. FiscalPeriodRepository must
  appear before FiscalPeriodService, and both before any file that references them.
  The plan specifies exact insertion points.

## Out of Scope

- Audit event table (backlog says "even just a note field or an event" -- we chose Log.info)
- API controller endpoints
- Ops-layer close/reopen warnings
- Adding or deleting fiscal periods
- Any schema migrations
