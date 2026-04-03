# Project 002 — Test Harness & Configuration: Test Results

**Date:** 2026-04-03
**Executed by:** BD (Basement Dweller)
**Environment:** leobloom_dev (Docker sandbox, .NET 10.0.201)

## Acceptance Criteria Verification

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | `dotnet test` runs all 88 Gherkin scenarios — all pass | PASS | `Passed: 88, Failed: 0` in LeoBloom.Dal.Tests.dll (634ms) |
| 2 | `LeoBloom.Migrations` uses `Dal.ConnectionString.resolve` | PASS | `Program.fs` calls `LeoBloom.Dal.ConnectionString.resolve AppContext.BaseDirectory`. Zero `ConfigurationBuilder` usage in Migrations. |
| 3 | `LEOBLOOM_ENV` is sole environment selection mechanism | PASS | Migrations no longer accepts args for env. All projects use `ConnectionString.resolve` which reads `LEOBLOOM_ENV`. |
| 4 | No test data persists after test run | PASS | Verified: 0 rows in journal_entry, invoice, transfer, obligation_instance after run |
| 5 | Solution builds with zero warnings | PASS | `Build succeeded. 0 Warning(s) 0 Error(s)` |
| 6 | `LeoBloom.Domain.Tests` builds and runs | PASS | `Passed: 1, Failed: 0` — placeholder test unchanged |
| 7 | Feature files in `Projects/Project001-Database/Specs/Acceptance/` | PASS | Linked as embedded resources via MSBuild, single source of truth |
| 8 | All Then steps use try/finally for cleanup | PASS | Every Then step wraps assertions in `try` with `finally cleanup ctx` |
| 9 | No `appsettings.Production.json` in Dal.Tests | PASS | Only `appsettings.Development.json` exists in Dal.Tests |
| 10 | Runtime database name guard | PASS | `SharedSteps.openContext()` runs `SELECT current_database()` and aborts if not `leobloom_dev` |

## Test Execution Summary

```
dotnet test LeoBloom.sln --verbosity minimal

Passed!  - Failed: 0, Passed: 88, Skipped: 0, Total: 88, Duration: 634 ms - LeoBloom.Dal.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed:  1, Skipped: 0, Total:  1, Duration:  21 ms - LeoBloom.Domain.Tests.dll (net10.0)

Total: 89 tests, 0 failures
Build: 0 warnings, 0 errors
```

## Scenario Breakdown

| Feature File | Scenarios | Status |
|---|---|---|
| LedgerStructuralConstraints.feature | 29 | All pass |
| OpsStructuralConstraints.feature | 41 | All pass |
| DeleteRestrictionConstraints.feature | 18 | All pass |
| Domain.Tests placeholder | 1 | Pass |
| **Total** | **89** | **All pass** |

## Migrations Refactor Verification

```
$ LEOBLOOM_ENV=Development LEOBLOOM_DB_PASSWORD=claude dotnet run --project Src/LeoBloom.Migrations
Leo Bloom Migrations — Environment: Development
No pending migrations.
```

## Data Integrity Verification

```sql
-- After test run, zero rows persisted (transaction rollback):
SELECT count(*) FROM ledger.journal_entry;          -- 0
SELECT count(*) FROM ops.invoice;                   -- 0
SELECT count(*) FROM ops.transfer;                  -- 0
SELECT count(*) FROM ops.obligation_instance;       -- 0
```

## Tech Stack Delivered

- **TickSpec** 2.0.4 — F# Gherkin step definition runner
- **TickSpec.Xunit** 2.0.4 — xUnit Theory integration
- **xUnit** 2.9.3 — test framework
- **Npgsql** 9.0.3 — PostgreSQL driver (parameterized queries throughout)
- **.NET 10** — all projects target net10.0
