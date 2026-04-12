# P073 — Connection Injection and Test Isolation — Plan

## Objective

Refactor the service layer to accept `NpgsqlTransaction` from callers instead
of creating its own connections internally. This eliminates test isolation
failures by enabling a rollback-only test pattern where no test data is ever
committed. CLI handlers become the transaction boundary in production.

## Pre-Conditions

- Repositories already accept `NpgsqlTransaction` — no repo changes needed.
- `DataSource` module stays as-is (AC-S5 / DataSourceEncapsulationTests).
- Tax bucket baseline count = 5 (confirmed via SeedRunnerTests FT-SR-008).

## Nested Call Audit (Completed)

**4 cross-service call chains** identified — these are the non-mechanical parts:

| Caller | Calls | Risk |
|--------|-------|------|
| `OpeningBalanceService.post` | `JournalEntryService.post` | 2 separate connections today |
| `BalanceProjectionService.project` | `AccountBalanceService.showAccountByCode` + `getBalanceById` | 3 separate connections today |
| `ObligationPostingService.postToLedger` | `JournalEntryService.post` + `ObligationInstanceService.transition` | 3 separate connections today |
| `TransferService.confirm` | `JournalEntryService.post` | 3 separate connections today |

All 4 chains currently have atomicity bugs. After this refactor, they'll share
a single transaction — fixing the bugs as a side effect.

---

## Phases

### Phase 1: Refactor Leaf Services (Ledger + Reporting)

**What:** Change every service function that has NO cross-service callers to
accept `NpgsqlTransaction` as its first parameter. Remove internal
`DataSource.openConnection()`, `BeginTransaction()`, `Commit()`, and
`Rollback()` calls. The function body becomes: use the passed-in `txn`
(and `txn.Connection`) for all repo calls, return the result — no
lifecycle management.

**Files modified:**

_Ledger (leaf services):_
- `AccountBalanceService.fs` — `listAccounts`, `showAccountById`, `showAccountByCode`, `getBalanceById`, `getBalanceByCode`
- `BalanceSheetService.fs` — `getAsOfDate`
- `FiscalPeriodService.fs` — `listPeriods`, `createPeriod`, `findPeriodByKey`, `closePeriod`, `reopenPeriod`
- `IncomeStatementService.fs` — `getByPeriodId`, `getByPeriodKey`
- `JournalEntryService.fs` — `post`, `getEntry`, `voidEntry`
- `SubtreePLService.fs` — `getByAccountCodeAndPeriodId`, `getByAccountCodeAndPeriodKey`
- `TrialBalanceService.fs` — `getByPeriodId`, `getByPeriodKey`

_Reporting (all leaf):_
- `CashFlowReportService.fs` — `getReceipts`, `getDisbursements`
- `GeneralLedgerReportService.fs` — `generate`
- `ScheduleEService.fs` — `generate`

**Pattern — before:**
```fsharp
let post (cmd: PostJournalEntryCommand) : Result<PostedJournalEntry, string list> =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = JournalEntryRepository.insert txn ...
        txn.Commit()
        Ok result
    with ex ->
        try txn.Rollback() with _ -> ()
        Error [...]
```

**Pattern — after:**
```fsharp
let post (txn: NpgsqlTransaction) (cmd: PostJournalEntryCommand) : Result<PostedJournalEntry, string list> =
    try
        let result = JournalEntryRepository.insert txn ...
        Ok result
    with ex ->
        Error [...]
```

**Key rules:**
- `txn` is always the FIRST parameter (convention for consistency).
- Remove `use conn = DataSource.openConnection()` entirely.
- Remove `use txn = conn.BeginTransaction()` entirely.
- Remove `txn.Commit()` — caller decides.
- Remove `try txn.Rollback() with _ -> ()` — caller decides.
- Keep `try/with` for catching repo-level exceptions and returning `Error`.
- If the function needs `conn` for anything (e.g., creating NpgsqlCommand),
  use `txn.Connection` instead.
- Remove the `open LeoBloom.Utilities` import if `DataSource` was the only
  thing used from it (check first).

**Verification:** `dotnet build Src/LeoBloom.Ledger/` and
`dotnet build Src/LeoBloom.Reporting/` compile with 0 errors. (CLI and tests
WILL have errors — that's expected, they get fixed in later phases.)

---

### Phase 2: Refactor Nested-Call Services (Ops + Portfolio)

**What:** Same mechanical change as Phase 1, but for services that call OTHER
services. The nested calls must thread the same `txn` through. Also includes
all remaining services without nested calls in these projects.

**Files modified:**

_Ops — leaf services (no nested calls):_
- `InvoiceService.fs` — `recordInvoice`, `showInvoice`, `listInvoices`
- `ObligationAgreementService.fs` — `create`, `getById`, `list`, `update`, `deactivate`
- `ObligationInstanceService.fs` — `list`, `findUpcoming`, `transition`, `detectOverdue`, `spawn`
- `OrphanedPostingService.fs` — `findOrphanedPostings`

_Ops — services with nested calls:_
- `ObligationPostingService.fs` — `postToLedger`
  - Calls `JournalEntryService.post` → pass `txn` through
  - Calls `ObligationInstanceService.transition` → pass `txn` through
  - **Collapse the 3-phase pattern into a single function body using the shared `txn`**
- `TransferService.fs` — `initiate`, `confirm`, `show`, `list`
  - `confirm` calls `JournalEntryService.post` → pass `txn` through
  - **Collapse the multi-phase pattern in `confirm` into single txn**
- `BalanceProjectionService.fs` — `project`
  - Calls `AccountBalanceService.showAccountByCode` → pass `txn` through
  - Calls `AccountBalanceService.getBalanceById` → pass `txn` through
  - **Remove the separate connection it opens for its own repo calls**

_Portfolio (all leaf — no nested calls):_
- `FundService.fs` — `createFund`, `findFundBySymbol`, `listFunds`, `listFundsByDimension`
- `InvestmentAccountService.fs` — `createAccount`, `listAccounts`
- `PortfolioReportService.fs` — `getAllocation`, `getPortfolioSummary`, `getPortfolioHistory`, `getGains`
- `PositionService.fs` — `recordPosition`, `listPositions`, `latestPositionsByAccount`, `latestPositionsAll`

**Special attention for nested calls:**

`ObligationPostingService.postToLedger` becomes:
```fsharp
let postToLedger (txn: NpgsqlTransaction) (cmd: PostToLedgerCommand) =
    // All 3 phases now use the same txn — atomic
    let instance = ObligationInstanceRepository.findById txn cmd.instanceId
    // ... validation ...
    match JournalEntryService.post txn jeCmd with
    | Error errs -> Error errs
    | Ok posted ->
        match ObligationInstanceService.transition txn transitionCmd with
        | Error errs -> Error errs
        | Ok _ -> Ok { ... }
```

**Verification:** `dotnet build Src/LeoBloom.Ops/` and
`dotnet build Src/LeoBloom.Portfolio/` compile with 0 errors.

---

### Phase 3: Refactor CLI Command Handlers

**What:** CLI handlers become the transaction boundary. Each handler opens a
connection, begins a transaction, calls the service, and commits on success
or rolls back on failure.

**Files modified:**
- `LedgerCommands.fs`
- `AccountCommands.fs`
- `InvoiceCommands.fs`
- `TransferCommands.fs`
- `PeriodCommands.fs`
- `ObligationCommands.fs`
- `DiagnosticCommands.fs`
- `PortfolioCommands.fs`
- `PortfolioReportCommands.fs`
- `ReportCommands.fs`

**Pattern — before:**
```fsharp
let private handlePost (isJson: bool) (args: ParseResults<LedgerPostArgs>) : int =
    let cmd = { ... }
    let result = JournalEntryService.post cmd
    write isJson (result |> Result.map (fun v -> v :> obj))
```

**Pattern — after:**
```fsharp
let private handlePost (isJson: bool) (args: ParseResults<LedgerPostArgs>) : int =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let cmd = { ... }
        let result = JournalEntryService.post txn cmd
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        write isJson (result |> Result.map (fun v -> v :> obj))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()
```

**Design decisions:**
- Read-only handlers (list, show, reports) still get a transaction for
  consistency and read-snapshot isolation, but could use a lighter pattern.
  For simplicity and consistency, use the same pattern everywhere.
- `write` call happens AFTER commit/rollback since it only formats output.
- The `with ex -> reraise()` ensures unhandled exceptions still propagate
  after rollback.

**Verification:** `dotnet build Src/LeoBloom.CLI/` compiles with 0 errors.
Smoke test: `dotnet run --project Src/LeoBloom.CLI -- account list` returns
expected output.

---

### Phase 4: Refactor Test Infrastructure

**What:** Update test helpers to accept `NpgsqlTransaction` instead of bare
connections. Remove all cleanup infrastructure.

**Files modified:**
- `TestHelpers.fs`
- `PortfolioTestHelpers.fs`

**Changes to TestHelpers.fs:**

1. **Delete `TestCleanup` module entirely** — the Tracker type, `create`,
   all `track*` functions, and `deleteAll`.

2. **Update `InsertHelpers` module** — change every insert function from
   `(conn: NpgsqlConnection) (tracker: TestCleanup.Tracker)` to
   `(txn: NpgsqlTransaction)`. Remove tracker tracking calls. Use
   `txn.Connection` where `conn` was used for NpgsqlCommand.

   Before:
   ```fsharp
   let insertAccountType (conn: NpgsqlConnection) (tracker: TestCleanup.Tracker) (name: string) (normalBalance: string) : int =
       use cmd = new NpgsqlCommand("INSERT ...", conn)
       ...
       let id = cmd.ExecuteScalar() :?> int
       TestCleanup.trackAccountType id tracker
       id
   ```

   After:
   ```fsharp
   let insertAccountType (txn: NpgsqlTransaction) (name: string) (normalBalance: string) : int =
       use cmd = new NpgsqlCommand("INSERT ...", txn.Connection)
       cmd.Transaction <- txn
       ...
       cmd.ExecuteScalar() :?> int
   ```

3. **Keep `TestData` module** unchanged (unique prefix generation is still useful).

4. **Keep `ConstraintAssert` module** unchanged (SQL constraint testing still needed).

**Changes to PortfolioTestHelpers.fs:**

1. **Delete `PortfolioTracker` type** and all `track*`/`deletePortfolioAll` functions.

2. **Update `PortfolioInsertHelpers`** — same pattern as above. Change from
   `(conn: NpgsqlConnection) (tracker: PortfolioTracker)` to
   `(txn: NpgsqlTransaction)`.

**Verification:** `dotnet build Src/LeoBloom.Tests/` — test helper files
compile. (Individual test files will still have errors until Phase 5.)

---

### Phase 5: Refactor All Test Files

**What:** Update every test to use the rollback-only pattern. Remove all
`try/finally` cleanup blocks.

**Test files to modify (all in Src/LeoBloom.Tests/):**

_Ledger tests:_
- `PostJournalEntryTests.fs`
- `VoidJournalEntryTests.fs`
- `OpeningBalanceTests.fs`
- `AccountBalanceTests.fs`
- `BalanceSheetTests.fs`
- `TrialBalanceTests.fs`
- `IncomeStatementTests.fs`
- `FiscalPeriodTests.fs`
- `SubtreePLTests.fs`
- `LedgerConstraintTests.fs`
- `DeleteRestrictionTests.fs`
- `AccountSubTypeTests.fs`

_Ops tests:_
- `ObligationAgreementTests.fs`
- `SpawnObligationInstanceTests.fs`
- `StatusTransitionTests.fs`
- `OverdueDetectionTests.fs`
- `PostObligationToLedgerTests.fs`
- `TransferTests.fs`
- `InvoiceTests.fs`
- `OpsConstraintTests.fs`
- `OrphanedPostingDetectionTests.fs`
- `BalanceProjectionTests.fs`

_Portfolio tests:_
- `FundTests.fs`
- `InvestmentAccountTests.fs`
- `PositionTests.fs`
- `PortfolioStructuralConstraintsTests.fs`

_CLI tests:_
- `LedgerCommandsTests.fs`
- `AccountCommandsTests.fs`
- `InvoiceCommandsTests.fs`
- `TransferCommandsTests.fs`
- `PeriodCommandsTests.fs`
- `ObligationCommandsTests.fs`
- `ReportCommandsTests.fs`
- `PortfolioCommandsTests.fs`
- `PortfolioReportCommandsTests.fs`

_Infrastructure tests (no change needed):_
- `DomainTests.fs` — pure domain logic, no DB
- `AcctAmountParsingTests.fs` — pure parsing, no DB
- `LoggingInfrastructureTests.fs` — no service calls
- `LogModuleStructureTests.fs` — no service calls
- `ConsolidatedHelpersTests.fs` — may need update if it tests helpers
- `CliFrameworkTests.fs` — may need update if it calls services
- `DataSourceEncapsulationTests.fs` — no change (tests DataSource API surface)
- `SeedRunnerTests.fs` — no change (tests seed scripts, not services)
- `ReportingServiceTests.fs` — needs update (calls reporting services)

**Pattern — before:**
```fsharp
[<Fact>]
let ``posts a valid journal entry`` () =
    use conn = DataSource.openConnection()
    let tracker = TestCleanup.create conn
    try
        let prefix = TestData.uniquePrefix()
        let atId = InsertHelpers.insertAccountType conn tracker (prefix + "_at") "Debit"
        let acctId = InsertHelpers.insertAccount conn tracker (prefix + "_acct") "1999" atId
        let fpId = InsertHelpers.insertFiscalPeriod conn tracker (prefix + "_fp") ...
        let cmd = { ... fiscalPeriodId = fpId; ... }
        let result = JournalEntryService.post cmd
        match result with
        | Ok posted ->
            TestCleanup.trackJournalEntry posted.entry.id tracker
            Assert.True(posted.entry.id > 0)
        | Error errs -> Assert.Fail(...)
    finally TestCleanup.deleteAll tracker
```

**Pattern — after:**
```fsharp
[<Fact>]
let ``posts a valid journal entry`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "Debit"
    let acctId = InsertHelpers.insertAccount txn (prefix + "_acct") "1999" atId
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "_fp") ...
    let cmd = { ... fiscalPeriodId = fpId; ... }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok posted ->
        Assert.True(posted.entry.id > 0)
    | Error errs -> Assert.Fail(...)
    // txn disposes via `use` → auto-rollback, zero footprint
```

**Key changes per test:**
1. Replace `let tracker = TestCleanup.create conn` with `use txn = conn.BeginTransaction()`
2. Replace `InsertHelpers.foo conn tracker` with `InsertHelpers.foo txn`
3. Replace service calls `Service.foo args` with `Service.foo txn args`
4. Remove ALL `TestCleanup.track*` calls
5. Remove ALL `try/finally TestCleanup.deleteAll tracker` blocks
6. Remove ALL `try/finally deletePortfolioAll tracker` blocks
7. Remove `PortfolioCliEnv` or similar cleanup wrapper types
8. Transaction auto-rolls-back on `Dispose` via `use` — no explicit rollback needed

**CLI test files (special case):**
CLI tests that call command handlers need a different approach. The handler
now owns the transaction, so CLI tests may need to:
- Call the handler directly (it opens its own conn+txn and commits)
- Verify via a separate read connection
- Clean up committed data, OR refactor handlers to accept an optional txn

**Decision:** CLI command tests test the full CLI flow including commit
behavior. These tests should continue to commit and use cleanup — BUT since
the service no longer owns the connection, the CLI handler IS the service
boundary. For CLI tests that need isolation, we have two options:
1. Keep cleanup for CLI tests only (pragmatic)
2. Make handlers accept an optional `txn` parameter (over-engineered)

**Recommendation:** Option 1 — keep minimal cleanup for CLI integration tests.
These tests verify the full stack including commit behavior. The intermittent
failures were caused by service-level tests, not CLI tests. Review the CLI
test files during implementation to determine which pattern fits.

**Verification:** `dotnet build Src/LeoBloom.Tests/` compiles with 0 errors.

---

### Phase 6: Verification

**What:** Run the full acceptance criteria battery.

**Steps:**
1. `dotnet build` — full solution compiles
2. Grep verification for structural ACs:
   - AC-S1: `grep -r "DataSource.openConnection" Src/LeoBloom.Ledger/ Src/LeoBloom.Ops/ Src/LeoBloom.Portfolio/ Src/LeoBloom.Reporting/` → 0 hits
   - AC-S2: Verify every service function has `(txn: NpgsqlTransaction)` as first param
   - AC-S3: `grep -r "DataSource.openConnection" Src/LeoBloom.CLI/` → hits in every command file
   - AC-S4: `grep -r "TestCleanup.Tracker\|PortfolioTracker\|deleteAll\|deletePortfolioAll" Src/LeoBloom.Tests/` → 0 hits (excluding CLI test cleanup if kept)
   - AC-S5: Repos unchanged (no repo files modified)
3. `dotnet test -- xUnit.MaxParallelThreads=1` → AC-B1
4. `dotnet test` (default parallel) → AC-B2
5. Run 5 consecutive times → AC-B3
6. Check DB state → AC-B4 (tax_bucket count = 5, no orphan rows)
7. `dotnet run --project Src/LeoBloom.CLI -- account list` → AC-B5

---

## Acceptance Criteria

- [ ] AC-S1 — No service function calls `DataSource.openConnection()` directly
- [ ] AC-S2 — All service functions accept `NpgsqlTransaction` as first parameter
- [ ] AC-S3 — CLI command handlers own connection + transaction lifecycle
- [ ] AC-S4 — `TestCleanup.Tracker` and `PortfolioTracker` cleanup infrastructure removed
- [ ] AC-S5 — Repository functions unchanged (still accept `NpgsqlTransaction`)
- [ ] AC-B1 — Full test suite passes with serial execution
- [ ] AC-B2 — Full test suite passes with parallel execution
- [ ] AC-B3 — 5 consecutive runs produce 0 failures
- [ ] AC-B4 — No test data remains after a run (tax_bucket count = 5)
- [ ] AC-B5 — CLI commands commit data in production use

## Risks

| Risk | Mitigation |
|------|------------|
| Large surface area (~50+ files) | Each change is mechanical; pattern is identical |
| Missing a nested call | Audit completed — 4 chains identified and documented |
| CLI test isolation | CLI tests may keep minimal cleanup; they test commit behavior |
| F# compilation order | Phase ordering respects project dependency graph |
| `cmd.Transaction <- txn` forgotten in helpers | Builder must set transaction on every NpgsqlCommand in InsertHelpers |

## Out of Scope

- Changing the `DataSource` module
- Adding DI container or IoC framework
- Database-per-test isolation
- Changing repository function signatures
- Refactoring the `ErrorHandler` or `ExitCodes` modules
