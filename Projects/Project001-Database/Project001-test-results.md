# Project 001 — Database: Test Results

**Date:** 2026-04-03
**Executed by:** BD (Basement Dweller)
**Environment:** leobloom_dev (Docker sandbox, .NET 10.0.201)

## Acceptance Criteria Verification

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Schema exists with all ledger and ops tables | PASS | 14 tables confirmed: `ledger.account_type`, `ledger.account`, `ledger.fiscal_period`, `ledger.journal_entry`, `ledger.journal_entry_reference`, `ledger.journal_entry_line`, `ops.obligation_type`, `ops.obligation_status`, `ops.cadence`, `ops.payment_method`, `ops.obligation_agreement`, `ops.obligation_instance`, `ops.transfer`, `ops.invoice` |
| 2 | Migrations run successfully against leobloom_dev | PASS | `dotnet run --project Src/LeoBloom.Migrations` → "No pending migrations." (18 migrations applied) |
| 3 | Seed data loaded (account_type, fiscal_period, chart of accounts, ops lookups) | PASS | Verified via psql: 5 account_types, 12 fiscal_periods, 30 accounts, 2 obligation_types, 6 obligation_statuses, 4 cadences, 6 payment_methods |
| 4 | Structural constraints enforced (NOT NULL, UNIQUE, FK) | PASS | 70 insert-direction constraint tests pass (29 ledger + 41 ops) |
| 5 | ON DELETE RESTRICT enforced on all 18 FK relationships | PASS | 18 delete restriction tests pass |
| 6 | Configuration via appsettings.{env}.json + LEOBLOOM_ENV | PASS | Migrations and tests both use `ConnectionString.resolve` with `LEOBLOOM_ENV=Development` |

## Test Execution Summary

```
dotnet test LeoBloom.sln --verbosity minimal

Passed!  - Failed: 0, Passed: 88, Skipped: 0, Total: 88, Duration: 634 ms - LeoBloom.Dal.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed:  1, Skipped: 0, Total:  1, Duration:  21 ms - LeoBloom.Domain.Tests.dll (net10.0)
```

## Data Integrity Verification

```sql
-- After test run, zero rows persisted (transaction rollback):
SELECT count(*) FROM ledger.journal_entry;          -- 0
SELECT count(*) FROM ops.invoice;                   -- 0
SELECT count(*) FROM ops.transfer;                  -- 0
SELECT count(*) FROM ops.obligation_instance;       -- 0
```

## Notes

- Project 1 acceptance criteria were verified retroactively as part of Project 2 test harness work.
- The 88 Gherkin scenarios in Dal.Tests serve as the executable evidence for Project 1's structural constraint requirements.
- The original 69 scenarios from the BRD were expanded to 88: +1 missing nullable scenario, +18 ON DELETE RESTRICT scenarios.
