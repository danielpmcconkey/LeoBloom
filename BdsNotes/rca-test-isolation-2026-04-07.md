# RCA: Test Isolation and DB Connection Ownership

**Date:** 2026-04-07
**Triggered by:** Nightshift P058/P059/P061 delivery left 2 deterministic + intermittent test failures

## What we fixed

SeedRunnerTests FT-SR-008 and FT-SR-011 hardcoded `SELECT COUNT(*) FROM portfolio.tax_bucket` expecting exactly 5 rows. Portfolio test classes running in parallel (xUnit default) temporarily insert test tax buckets, inflating the count. Fixed by scoping the query to `WHERE name IN (...)` for the 5 known seed names.

## What we didn't fix (and why)

### Symptom

Full suite (973 tests) fails intermittently — roughly 1 in 3-4 runs drops a single random test. Always a different test. Errors include:

- `23505: duplicate key value violates unique constraint` (fiscal period collisions)
- `No resultset is currently being` (Npgsql connection state corruption)
- `Obligation instance with id X does not exist` (data deleted by concurrent test cleanup)

Tests always pass in isolation. Serial execution (`xUnit.MaxParallelThreads=1`) is stable but takes ~5:10 vs ~1:20 parallel.

### Root cause: services own their connections

Every service function (`JournalEntryService.post`, `InvestmentAccountService.createAccount`, etc.) calls `DataSource.openConnection()` internally, opens its own transaction, commits, and disposes. The test has no way to inject its connection or transaction.

This means:

1. **Test setup data must be committed** — the service opens a *separate* connection, so it can only see committed rows. If the test inserts a tax bucket inside an uncommitted transaction, the service's FK lookup would fail.

2. **Committed test data is globally visible** — every other parallel test can see it via `READ COMMITTED` isolation.

3. **Cleanup is best-effort** — the tracker pattern (`try/finally deletePortfolioAll`) runs after the test, but there's a window between commit and cleanup where data is visible. If a parallel test queries during that window, it sees phantom rows.

4. **Cleanup failures are swallowed** — `deletePortfolioAll` wraps each DELETE in `try/with` that logs but doesn't rethrow. If an FK prevents deletion (e.g., another test created a reference during the window), the row leaks silently and accumulates across runs.

### The architectural gap

```
TEST CONNECTION                     SERVICE CONNECTION
================                    ==================
INSERT setup data (auto-commit)
                                    openConnection()
                                    BEGIN
                                    SELECT ... (sees committed setup data)
                                    INSERT ...
                                    COMMIT  <-- data now globally visible
                                    dispose
DELETE setup data (cleanup)
```

The test and the code-under-test operate on **independent connections with independent transaction lifecycles**. The test cannot wrap the service's work in a rollback-only transaction because the service refuses to accept an external connection.

### What the fix looks like

The service layer needs to accept a connection/transaction from the caller instead of (or in addition to) opening its own. Something like:

```fsharp
// Current: service owns connection
let createAccount name tbId agId =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    ...

// Fixed: caller can inject connection
let createAccount (conn: NpgsqlConnection) (txn: NpgsqlTransaction) name tbId agId =
    ...
```

Then tests wrap everything in a single transaction that rolls back:

```fsharp
use conn = DataSource.openConnection()
use txn = conn.BeginTransaction()
let tbId = insertTaxBucket conn txn "test_tb"
let result = InvestmentAccountService.createAccount conn txn name tbId agId
// Assert...
txn.Rollback()  // nothing ever hits the DB
```

This is a significant refactor — every service function signature changes, every caller changes. But it's the only way to get true test isolation without serialization.

### Scope of impact

- `Src/LeoBloom.Ledger/` — 10+ service files, all use `DataSource.openConnection()` internally
- `Src/LeoBloom.Portfolio/` — 6 service files, same pattern
- `Src/LeoBloom.Ops/` — same pattern
- `Src/LeoBloom.Reporting/` — same pattern
- `Src/LeoBloom.CLI/` — all command handlers would pass their connection down
- `Src/LeoBloom.Tests/` — TestHelpers, cleanup patterns, every test that calls a service

### Prior art in this codebase

P053 (FixPreExistingTestFailures) addressed a subset of this — fiscal period date collisions with seed data. The fix was to move test dates to 2099. That's the same band-aid pattern: avoid collisions by convention rather than by isolation.

### Decision needed

1. **Do the refactor** — connection injection across all services. True isolation, parallel-safe, no cleanup needed. Big.
2. **Serialize tests** — `xUnit.MaxParallelThreads=1`. Simple, reliable, 4x slower.
3. **Live with it** — current state. Fast, occasionally flaky. Band-aid fixes for specific collisions as they arise.
4. **Hybrid** — serialize only the DB-touching test classes via `[<Collection("Database")>]`, let pure-logic tests stay parallel.

No decision made yet. Waiting for Hobson's GAAP audit results to see if this intersects with broader architectural recommendations.
