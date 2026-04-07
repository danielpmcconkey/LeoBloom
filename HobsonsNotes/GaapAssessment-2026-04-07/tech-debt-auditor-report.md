# Tech Debt Auditor Report -- LeoBloom

**Date:** 2026-04-07
**Codebase:** LeoBloom -- cash-basis GAAP personal finance / bookkeeping
**Stack:** F# / .NET 10 / PostgreSQL / Npgsql / Migrondi / Argu / Serilog
**Assessment scope:** Product ownership risk and operational risk

---

## Executive Summary

LeoBloom is a structurally sound codebase for its current scale (single user, single machine, CLI-only). The F# type system does a lot of heavy lifting -- discriminated unions for domain enums, Result types for error paths, immutable domain records. The project layering (Domain -> Ledger/Ops/Portfolio/Reporting -> CLI) is clean, with dependency flow going in the right direction. Migrations have proper up/down blocks. Tests hit a real database.

That said, there are several things I'd want addressed before this codebase grows, and two items that could cause real data problems today.

---

## Findings

### F1. Transfer/Obligation Confirm: Non-Atomic Multi-Phase Writes

**Classification: Career Risk**

`TransferService.confirm` and `ObligationPostingService.postToLedger` both perform a multi-phase write across separate database connections/transactions:

- Phase 1: Read + validate (connection 1, committed)
- Phase 2: Post journal entry via `JournalEntryService.post` (opens connection 2 internally)
- Phase 3: Update the transfer/obligation record (connection 3)

If Phase 2 succeeds but Phase 3 fails, the journal entry exists in the ledger but the transfer/obligation record is not updated. The code logs this and comments that "retry is safe" -- and the idempotency guard (checking for existing non-voided JE by reference) does mitigate the worst outcome on retry.

**But:** Between the failure and the retry, the system is in an inconsistent state: a journal entry exists with no corresponding transfer confirmation. Any report or diagnostic run in that window will see phantom entries. There is no automated recovery mechanism, no reconciliation job, and no alerting. The user has to notice, understand what happened, and manually retry.

**Trigger:** Phase 3 database error after Phase 2 commits. Network blip, connection pool exhaustion, or PostgreSQL restart.

**Why it's career risk:** In a financial system, the ledger is the source of truth. An orphaned journal entry that nobody knows about is how you get numbers that don't reconcile. The orphaned posting diagnostic (`OrphanedPostingService`) would catch some of these, but only if someone runs it -- and it checks for the inverse condition (dangling references), not for JE entries that exist without a matching transfer/obligation status update.

**Effort to address:** Significant. The root cause is that `JournalEntryService.post` opens its own connection. Fixing this properly means passing a transaction through the call chain so all three phases execute within a single transaction. This is a meaningful refactor of the service layer calling conventions.

---

### F2. Silent Error Swallowing in List Operations

**Classification: Regret**

`ObligationInstanceService.list`, `ObligationInstanceService.findUpcoming`, and `TransferService.list` catch exceptions and return empty lists:

```fsharp
with ex ->
    Log.errorExn ex "Failed to list transfers" [||]
    try txn.Rollback() with _ -> ()
    []
```

The caller (CLI) receives an empty list and displays "no results found" -- indistinguishable from a legitimate empty result. The error is logged to a file, but the CLI exit code is 0 (success).

**Trigger:** Any database connectivity issue, query error, or schema mismatch during a list operation.

**Why it's regret:** The user thinks they have no transfers/obligations when they actually have a database problem. In a financial system, "no data" and "I couldn't reach the database" are not the same thing. Someone will eventually run a list, see nothing, conclude they're caught up, and miss a payment.

**Effort to address:** Trivial. Change return types to `Result<T list, string list>` like every other service method already does. Propagate the error to the CLI.

---

### F3. Npgsql Version Skew Between Projects

**Classification: Friction**

`LeoBloom.Migrations` uses `Npgsql 10.0.1`. Every other project (Ledger, Ops, Portfolio, Reporting, Tests) uses `Npgsql 9.0.3`. These are different major versions.

**Trigger:** This is already happening. The two versions coexist because Migrations is a standalone executable with its own connection string and never shares a runtime with the other projects. But it means the Migrations project is testing against a different Npgsql wire protocol than the application uses. If Npgsql 10 has behavioral differences in how it handles DateOnly, numeric precision, or connection pooling, you won't find out from the migration runner.

**Effort to address:** Trivial. Pin all projects to the same major version.

---

### F4. Connection String Missing `portfolio` Schema in Search Path

**Classification: Friction**

The CLI and Tests `appsettings.Development.json` connection strings specify `Search Path=ledger,ops,public` -- the `portfolio` schema is not included. All portfolio SQL uses fully-qualified table names (`portfolio.position`, etc.), so this doesn't cause runtime failures today. But:

1. If anyone writes a query without the schema prefix, it will silently resolve against `ledger`, `ops`, or `public` -- potentially finding a different table or no table.
2. The search path inconsistency is a trap for the next developer.

**Trigger:** A new query or migration that doesn't fully-qualify the `portfolio` schema prefix.

**Effort to address:** Trivial. Add `portfolio` to the search path.

---

### F5. Transactions for Read-Only Operations

**Classification: Friction**

Every service method -- including pure reads like `FiscalPeriodService.listPeriods`, `TransferService.show`, `PortfolioReportService.getAllocation` -- opens a transaction (`conn.BeginTransaction()`), then commits or rolls back. For read-only operations, this is unnecessary overhead. PostgreSQL's default `READ COMMITTED` isolation already provides adequate consistency for single-statement reads.

More importantly, the pattern creates cognitive noise: when reading the code, you have to examine each method to determine whether the transaction is structurally necessary (writes, multi-statement read consistency) or ceremonial. This makes it harder to spot the methods where transactions genuinely matter.

**Trigger:** Not a runtime risk. It's a code comprehension and maintenance tax.

**Effort to address:** Moderate. Introduce a read-only path (just `openConnection()`, no transaction) for query-only service methods. Leave transactions for writes and multi-statement reads that need snapshot consistency.

---

### F6. Ghost `LeoBloom.Api` Project

**Classification: Friction**

`Src/LeoBloom.Api/` contains compiled artifacts (bin/obj for both net8.0 and net10.0) but has no `.fsproj` file and is not referenced in the solution. It appears to be a dead project from before the "no API -- CLI only" decision. The bin/obj directories contain stale DLLs, including one compiled against net8.0 (the rest of the solution targets net10.0).

**Trigger:** Confusion for anyone exploring the repo. A build script or IDE that auto-discovers project directories could pick this up.

**Effort to address:** Trivial. Delete `Src/LeoBloom.Api/` entirely. The `.gitignore` should have excluded `bin/` and `obj/`, but the directory structure itself is the debris.

---

### F7. Debug-Only Database Safety Guard is Hardcoded

**Classification: Acceptable Debt**

`DataSource.fs` has a compile-time `#if DEBUG` guard that verifies the connected database is `leobloom_dev`. This is hardcoded to the string `"leobloom_dev"`. In a Release build, this guard is compiled out entirely, which is by design. But there's no equivalent guard for production -- meaning a misconfigured `LEOBLOOM_ENV` in production could connect to dev, and the safety net is absent.

**Trigger:** Deploying with `LEOBLOOM_ENV=Development` in a production context, or a missing/empty `LEOBLOOM_ENV` (which defaults to `"Development"`).

**Shelf life:** Acceptable for a single-user, single-machine system. Becomes a real risk if this ever runs on a server or in CI with access to production credentials.

**Effort to address:** Trivial. Log the connected database name at startup in all builds. The guard already does the query; just make the info-level log unconditional.

---

### F8. No Database Backup Strategy in the Codebase

**Classification: Acceptable Debt**

There's no backup script, no pg_dump wrapper, no documentation of a backup strategy. For a financial system that's the source of truth for bookkeeping data, this is the kind of thing that's fine until it isn't.

**Trigger:** Disk failure, accidental `DROP`, or a bad migration against production.

**Shelf life:** Acceptable as long as the system is in early development. Once there's real financial data in `leobloom_prod`, this becomes career risk. A single `pg_dump` cron job would cover it.

**Effort to address:** Trivial.

---

### F9. Migration Rollback Story is Manual

**Classification: Acceptable Debt**

Migrondi supports up/down migrations, and every migration file has a `MIGRONDI:DOWN` section. Good. However, there's no scripted or documented rollback procedure. The `Migrations/Program.fs` only runs `migrondi.RunUp()`. Rolling back requires manually invoking Migrondi's rollback API or running the down SQL by hand.

Migration 19 (`EliminateLookupTables`) is particularly hairy -- it drops tables after migrating data. The down migration recreates them with seed data and attempts to restore FK columns. This is the kind of migration where a partial failure during rollback could leave the schema in an unrecoverable state.

**Trigger:** A bad migration against production that needs to be rolled back.

**Shelf life:** Fine for now. The migration history is clean and forward-only. But once production has real data, you want a tested rollback path.

**Effort to address:** Moderate. Add a `--down` flag to the migration runner, and test rollback of the most recent migration in CI.

---

### F10. Hardcoded Schedule E Account Code Mappings

**Classification: Acceptable Debt**

`ScheduleEMapping.fs` contains a hardcoded mapping from COA account codes to IRS Schedule E line numbers. If the chart of accounts changes (new expense categories, code renumbering), this file must be updated in lockstep. There's no validation that the mapped codes actually exist in the database.

**Trigger:** Adding a new expense account to the COA without updating the mapping. The expense would be invisible on Schedule E.

**Shelf life:** Fine for a stable COA. Becomes friction if the account structure evolves. A validation check at startup (or in tests) that confirms all mapped codes exist in the database would be cheap insurance.

**Effort to address:** Trivial (for validation). Moderate (to make data-driven).

---

### F11. Log File Path Defaults to Docker Sandbox Path

**Classification: Friction**

`Log.fs` defaults the log file base path to `/workspace/application_logs/leobloom` when `Logging:FileBasePath` is not set in config. This path exists inside the Docker sandbox but not on the host machine. If the config key is missing, logging initialization will fail silently or write to a path that doesn't exist.

In practice, the Development appsettings includes this key, so it works. But the default is a trap.

**Trigger:** Running the CLI or tests without the appsettings file, or with a production config that omits the key.

**Effort to address:** Trivial. Change the default to a relative or temp path, or fail loud if the directory doesn't exist.

---

### F12. Tests Run Against a Shared Development Database

**Classification: Acceptable Debt**

All tests connect to `leobloom_dev` on `172.18.0.1:5432`. Tests insert real data, execute service operations, then clean up via `TestCleanup.deleteAll`. The cleanup is FK-aware and catches exceptions per-table.

This is fine for a single developer. But:

1. If two test runs execute concurrently (e.g., IDE + CLI), they share the same database and could interfere.
2. The cleanup tracker uses mutable lists on a mutable record. Parallel xUnit test execution within a single run could cause race conditions on these lists.
3. If a test fails before cleanup, rows are left behind. The unique prefix strategy (`TestData.uniquePrefix()`) mitigates collision but not accumulation.

**Shelf life:** Acceptable for solo development. Would need a per-run ephemeral database if a second developer or CI pipeline is added.

**Effort to address:** Moderate (for ephemeral DB). The test infrastructure itself is well-designed for its current constraints.

---

### F13. Decimal Precision Mismatch: Ledger vs Portfolio

**Classification: Acceptable Debt**

Ledger amounts use `numeric(12,2)` -- appropriate for cash-basis bookkeeping (dollars and cents). Portfolio positions use `numeric(18,4)` -- appropriate for share prices and quantities. This is a conscious, correct design choice.

However, the F# domain types use `decimal` for both, with no type-level distinction. A developer could accidentally pass a portfolio quantity (4 decimal places) into a ledger amount field without any compile-time warning. The database constraint will truncate or reject it at runtime.

**Shelf life:** Fine as long as the two domains remain separate. If there's ever a need to bridge portfolio values into the ledger (e.g., posting unrealized gains), the precision mismatch will need careful handling.

**Effort to address:** Trivial (add a wrapper type). But also possibly over-engineering for a single-user system.

---

### S1. Domain Model Design

**Classification: Solid**

The domain model in `Ledger.fs`, `Ops.fs`, and `Portfolio.fs` is clean. Discriminated unions for enums (`EntryType`, `InstanceStatus`, `AccountSubType`) with explicit `toDbString`/`fromDbString` conversions. Result types for validation. Command/response DTOs separated from entity types. Pure validation functions separated from DB-dependent validation.

The one-way dependency (`Ops` can reference `Ledger` types, `Ledger` cannot reference `Ops`) is enforced by the `.fsproj` compile order and project references. This is good architectural discipline.

Worth calling out so nobody "simplifies" this into a single module.

---

### S2. Idempotent Seed Data

**Classification: Solid**

The seed SQL scripts use `ON CONFLICT ... DO UPDATE` (upsert) patterns, and the seed runner tests explicitly verify that running seeds twice produces identical state. The runner stops on first error (`ON_ERROR_STOP`). Seeds are separated by environment (`dev/` directory). This is materially better than most seed strategies I've seen.

---

### S3. Test Infrastructure

**Classification: Solid**

The test helpers (`TestHelpers.fs`, `PortfolioTestHelpers.fs`) provide unique-prefix data generation, FK-aware cleanup, and constraint assertion utilities. Tests are tagged with Gherkin IDs for traceability. The cleanup tracker's approach of catching per-table errors and logging rather than silently swallowing is the right call.

The tests hit a real database rather than mocking the repository layer. For a financial system, this is the correct trade-off -- you want to know that your SQL actually works.

---

### S4. CLI Output Architecture

**Classification: Solid**

The `OutputFormatter` module provides dual-mode output (human-readable and JSON) via a single `write` function that branches on an `isJson` flag. The JSON path uses `FSharp.SystemTextJson` for proper F# type serialization. The `--json` flag is available at both the global and subcommand level. This is a CLI that was designed to be called by other programs (COYS bots), and the output contract reflects that.

---

### S5. Void Semantics

**Classification: Solid**

Journal entries are voided, not deleted. The `voided_at` / `void_reason` columns create an audit trail. The void operation is idempotent (voiding an already-voided entry returns the existing state). Non-voided entries are efficiently queryable via the partial index `idx_je_voided_null`. This is correct bookkeeping behavior.

---

## Risk Summary Table

| # | Finding | Classification | Effort |
|---|---------|---------------|--------|
| F1 | Non-atomic multi-phase writes (Transfer/Obligation confirm) | Career Risk | Significant |
| F2 | Silent error swallowing in list operations | Regret | Trivial |
| F3 | Npgsql major version skew (9.x vs 10.x) | Friction | Trivial |
| F4 | Missing `portfolio` schema in connection string search path | Friction | Trivial |
| F5 | Unnecessary transactions for read-only operations | Friction | Moderate |
| F6 | Ghost `LeoBloom.Api` project directory | Friction | Trivial |
| F7 | Debug-only database safety guard | Acceptable Debt | Trivial |
| F8 | No database backup strategy | Acceptable Debt | Trivial |
| F9 | Migration rollback is manual-only | Acceptable Debt | Moderate |
| F10 | Hardcoded Schedule E mappings | Acceptable Debt | Trivial (validate) |
| F11 | Log file path defaults to Docker sandbox | Friction | Trivial |
| F12 | Tests share a dev database | Acceptable Debt | Moderate |
| F13 | Decimal precision gap between domains | Acceptable Debt | Trivial |
| S1 | Domain model design | Solid | -- |
| S2 | Idempotent seed data | Solid | -- |
| S3 | Test infrastructure | Solid | -- |
| S4 | CLI output architecture (JSON + human) | Solid | -- |
| S5 | Void semantics (soft delete with audit trail) | Solid | -- |

---

## Priority Recommendation

If I were taking ownership tomorrow, I'd address in this order:

1. **F2 (silent error swallowing)** -- Trivial fix, immediate safety improvement. Do it today.
2. **F1 (non-atomic writes)** -- The biggest structural risk. Design the transaction-passing pattern, then apply it to TransferService.confirm and ObligationPostingService.postToLedger. This is the one that will land someone in front of their boss.
3. **F3 + F4 + F6 + F11** -- Housekeeping. Batch them into a single cleanup PR. Half an hour.
4. **F8 (backup)** -- Write a pg_dump cron job before putting real financial data in production.

Everything else can wait until the trigger conditions approach.

---

*Signed: Tech Debt Auditor*
*Assessment date: 2026-04-07*
