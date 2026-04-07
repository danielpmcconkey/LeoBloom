# 073 — Connection Injection and Test Isolation

**Epic:** Infrastructure
**Depends On:** None
**Status:** Not started
**Priority:** High

---

## Problem Statement

Service functions (`JournalEntryService.post`, `InvestmentAccountService.createAccount`,
etc.) call `DataSource.openConnection()` internally, owning their own connection and
transaction lifecycle. Tests cannot inject a connection or transaction into the
code under test, so:

1. Test setup data must be **committed** for the service to see it (the service
   opens a separate connection that can only see committed rows).
2. Committed test data is **globally visible** to all parallel tests.
3. Cleanup runs after assertions, leaving a window where phantom rows are visible.
4. Cleanup failures are swallowed silently, causing data to leak across runs.

This produces intermittent test failures (~1 in 3-4 full suite runs) — always a
different test, always passes in isolation. Serial execution
(`xUnit.MaxParallelThreads=1`) is stable but 4x slower (~5:10 vs ~1:20).

See `BdsNotes/rca-test-isolation-2026-04-07.md` for full root cause analysis.

## What It Does

Refactor the service layer to accept an `NpgsqlConnection` and
`NpgsqlTransaction` from the caller instead of creating its own. This allows
tests to wrap all operations — setup, service call, and assertions — in a
single transaction that rolls back, leaving zero footprint in the database.

### 1. Service function signatures

Every service function changes from:

```fsharp
let createAccount name tbId agId =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    ...
    txn.Commit()
```

to:

```fsharp
let createAccount (txn: NpgsqlTransaction) name tbId agId =
    ...
    // no commit — caller decides
```

### 2. CLI command handlers become the transaction boundary

CLI handlers (the production caller) open the connection, begin the transaction,
call the service, commit on success, rollback on failure. This is where
`DataSource.openConnection()` lives in production.

### 3. Test pattern becomes rollback-only

```fsharp
use conn = DataSource.openConnection()
use txn = conn.BeginTransaction()
let tbId = insertTaxBucket conn txn "test_tb"
let result = InvestmentAccountService.createAccount txn name tbId agId
// Assert...
txn.Rollback()  // nothing persisted, no cleanup needed
```

### 4. Kill the tracker/cleanup pattern

`TestCleanup.Tracker`, `PortfolioTracker`, `deleteAll`, and all `try/finally`
cleanup blocks become unnecessary. Remove them.

## Scope of Impact

- `Src/LeoBloom.Ledger/` — all service files (~10)
- `Src/LeoBloom.Portfolio/` — all service files (6)
- `Src/LeoBloom.Ops/` — all service files
- `Src/LeoBloom.Reporting/` — all service files
- `Src/LeoBloom.CLI/` — all command handlers (transaction boundary moves here)
- `Src/LeoBloom.Tests/` — every test that calls a service. TestHelpers,
  PortfolioTestHelpers, cleanup infrastructure

## Acceptance Criteria

### Structural

| ID | Criterion |
|----|-----------|
| AC-S1 | No service function calls `DataSource.openConnection()` directly |
| AC-S2 | All service functions accept `NpgsqlTransaction` as their first parameter |
| AC-S3 | CLI command handlers own the connection + transaction lifecycle |
| AC-S4 | `TestCleanup.Tracker` and `PortfolioTracker` cleanup infrastructure removed |
| AC-S5 | Repository functions continue to accept `NpgsqlTransaction` (no change needed there) |

### Behavioral

| ID | Criterion |
|----|-----------|
| AC-B1 | Full test suite passes with `xUnit.MaxParallelThreads=1` (serial baseline) |
| AC-B2 | Full test suite passes with default parallel execution (no intermittent failures) |
| AC-B3 | 5 consecutive full suite runs produce 0 failures |
| AC-B4 | No test data remains in the database after a test run (`portfolio.tax_bucket` count = 5, no orphaned test rows in any table) |
| AC-B5 | CLI commands still commit data in production use (smoke test: `leobloom account list` works) |

## Risks

- **Large surface area.** Every service function signature changes, every caller
  changes. High line count, but each change is mechanical.
- **Nested service calls.** If service A calls service B internally, both now
  need the same transaction threaded through. Audit call chains before starting.
- **Repository functions already take `NpgsqlTransaction`.** This is good — the
  repositories don't need to change. The refactor is service-layer only (plus
  callers).

## Out of Scope

- Changing the `DataSource` module itself (it stays as-is, CLI handlers use it)
- Adding a DI container or IoC framework (F# function parameters are sufficient)
- Database-per-test-class isolation (overkill given rollback strategy)
