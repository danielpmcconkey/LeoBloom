# LeoBloom Security Review

**Auditor:** BD (basement dweller)
**Date:** 2026-04-05
**Scope:** Full codebase — /workspace/LeoBloom/Src
**Verdict:** Reasonably solid for a single-user CLI financial engine. No critical vulnerabilities found. Several medium-priority issues and a handful of structural observations worth addressing.

---

## Summary

This is a double-entry bookkeeping engine with PostgreSQL persistence, consumed via CLI (not a web API in practice — the Api project is a dead scaffold). The codebase is F# with raw SQL via Npgsql. The good news: all SQL queries use parameterized statements, there are no hardcoded credentials, and the domain validation layer is thorough. The bad news: error messages leak internal database details, the password handling has a silent-empty-string fallback, and there's a non-atomic two-phase write pattern in two services that could leave financial records in an inconsistent state.

---

## Critical Issues

**None found.** No SQL injection, no hardcoded secrets, no authentication bypass. For a personal-use CLI tool running in a Docker container, this is clean.

---

## High-Severity Warnings

### H-1: Non-Atomic Multi-Phase Writes Can Corrupt Financial State

**Files:**
- `Src/LeoBloom.Utilities/ObligationPostingService.fs` (lines 74-127)
- `Src/LeoBloom.Utilities/TransferService.fs` (lines 117-165)

**Description:** Both services perform a three-phase write pattern:
1. Read + validate (own transaction, committed)
2. Post journal entry via `JournalEntryService.post` (own transaction internally)
3. Update the source record (obligation instance or transfer) to reflect the posted state

Phases 2 and 3 run in separate transactions. If Phase 2 succeeds (journal entry is committed to the ledger) but Phase 3 fails (e.g., DB timeout, connection drop), the system ends up with:
- A committed journal entry that moved money in the ledger
- An obligation instance still in `confirmed` status (or a transfer still in `initiated` status) with no link to that journal entry

**Impact:** Orphaned journal entries. The ledger says money moved; the ops layer disagrees. For a bookkeeping system, this is the definition of data corruption. The TransferService even logs a warning about this explicitly (line 163-164), acknowledging the risk.

**Remediation:** Either:
- Refactor to run the journal entry insert and the status update in the same transaction (requires breaking the service encapsulation slightly), OR
- Implement an idempotent retry/reconciliation mechanism that detects orphaned journal entries by reference type and cleans up

### H-2: Empty Password Silently Accepted

**Files:**
- `Src/LeoBloom.Utilities/DataSource.fs` (lines 31-36)
- `Src/LeoBloom.Migrations/Program.fs` (lines 28-33)

**Description:** If the `LEOBLOOM_DB_PASSWORD` environment variable is unset, the code silently defaults to an empty string and proceeds to connect. There's no warning, no log entry, nothing.

```fsharp
let password =
    Environment.GetEnvironmentVariable "LEOBLOOM_DB_PASSWORD"
    |> Option.ofObj
    |> Option.defaultValue ""
```

**Impact:** In development this is probably fine (local pg_hba.conf likely allows it). In production, this means the app will attempt to connect with no password and either fail opaquely at connection time, or worse, succeed if the DB allows passwordless auth. A financial system should never silently degrade its auth posture.

**Remediation:** At minimum, log a warning when the password env var is missing. In production mode (`LEOBLOOM_ENV=Production`), fail hard — refuse to start.

---

## Medium-Severity Warnings

### M-1: Exception Messages Leaked to Callers

**Files:** Every service file in `Src/LeoBloom.Utilities/` — the pattern is consistent.

**Description:** All `catch` blocks return the raw exception message to the caller:

```fsharp
Error [ sprintf "Persistence error: %s" ex.Message ]
```

This includes `JournalEntryService.fs` (line 111), `FiscalPeriodService.fs` (line 32), `ObligationAgreementService.fs` (line 81), `ObligationInstanceService.fs` (line 115), `TransferService.fs` (line 78), `AccountBalanceService.fs` (line 24), and others.

**Impact:** Exception messages from Npgsql can include table names, column names, constraint names, SQL fragments, and connection details. For a CLI tool consumed by the developer, this is convenient. If this ever grows a real API surface, it's an information disclosure vulnerability (CWE-209).

**Remediation:** Return generic error identifiers to callers; log the full exception details server-side only (which the code already does via `Log.errorExn`). The error returned to the caller should be something like `"A database error occurred. Check logs for details."` with a correlation ID.

### M-2: IncludeErrorDetail Enabled in Connection Builder

**File:** `Src/LeoBloom.Utilities/DataSource.fs` (line 40)

```fsharp
builder.ConnectionStringBuilder.IncludeErrorDetail <- true
```

**Description:** This Npgsql setting causes detailed internal error information to be included in exceptions. Combined with M-1 (exception messages leaking), this amplifies the information disclosure risk.

**Impact:** If error messages ever reach an untrusted consumer, this setting ensures they contain maximum detail about the database schema and query structure.

**Remediation:** Gate this behind a DEBUG conditional or the `LEOBLOOM_ENV` check. Only enable in Development.

### M-3: Npgsql Version Mismatch Between Projects

**Files:**
- `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj`: Npgsql 9.0.3
- `Src/LeoBloom.Migrations/LeoBloom.Migrations.fsproj`: Npgsql 10.0.1
- `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj`: Npgsql 9.0.3

**Description:** Two different major versions of Npgsql in the same solution. While they don't share a process, this creates confusion and means security patches need to be tracked independently. The Migrations project jumped to Npgsql 10.x while everything else is on 9.x.

**Impact:** Not a direct vulnerability, but a maintenance burden that increases the chance of missing a security patch on one of the versions.

**Remediation:** Align all projects on the same Npgsql major version. Use a `Directory.Packages.props` for centralized version management.

### M-4: `ObligationInstance.updateStatus` Builds Dynamic SQL from Internal State

**File:** `Src/LeoBloom.Utilities/ObligationInstanceRepository.fs` (lines 80-133)

**Description:** The `updateStatus` function dynamically builds SET clauses based on which optional parameters are provided. The `setClause` and `selectColumns` variables are concatenated into `CommandText` via string interpolation:

```fsharp
sql.CommandText <-
    $"UPDATE ops.obligation_instance SET {setClause} WHERE id = @id RETURNING {selectColumns}"
```

**However**, `setClause` is built from hardcoded string literals (e.g., `"status = @status"`, `"amount = @amount"`) and `selectColumns` is a module-level constant. No user input flows into the SQL structure — only into parameterized values. **This is not injectable**, but it looks sketchy at first glance and is worth calling out because the pattern could become dangerous if someone adds a clause built from user input later.

**Remediation:** Add a comment explaining why this dynamic SQL is safe. Consider refactoring to use a static SQL statement with COALESCE patterns instead.

### M-5: `lookupAccountActivity` in JournalEntryService Uses Dynamic IN Clause

**File:** `Src/LeoBloom.Utilities/JournalEntryService.fs` (lines 34-51)

**Description:** Same pattern as M-4 — parameter names are generated programmatically (`@a0`, `@a1`, etc.) and injected into the query via `sprintf`. Again, the parameter names are derived from list indices (integers), not from user input, so this is safe. But it's the kind of pattern that invites copy-paste mistakes.

The same pattern appears in `Src/LeoBloom.Utilities/OpeningBalanceService.fs` (lines 19-39).

**Remediation:** Use Npgsql's array parameter support (`= ANY(@ids)`) instead of building IN clauses dynamically. The `ObligationInstanceRepository.findExistingDates` function already demonstrates the correct pattern using `NpgsqlDbType.Array`.

---

## Low-Severity Observations

### L-1: Dead API Scaffold With No Auth

**Files:**
- `Src/LeoBloom.Api/Program.fs`
- `Src/LeoBloom.Api/Controllers/WeatherForecastController.fs`

**Description:** The Api project is a default ASP.NET Web API template (WeatherForecast controller). It calls `app.UseAuthorization()` but configures no authentication scheme. No CORS policy. No rate limiting. `AllowedHosts: "*"` in `appsettings.json`.

**Impact:** Currently zero — the CLI direction document confirms the API projects are cancelled and the consumption layer is CLI. But this dead code is a liability if someone ever runs it. It would expose the DataSource connection pool with no auth.

**Remediation:** Delete the Api project from the solution, or at minimum add a README warning that it's non-functional scaffold code.

### L-2: Debug Safety Guard Compiled Out in Release

**File:** `Src/LeoBloom.Utilities/DataSource.fs` (lines 46-55)

**Description:** The safety guard that verifies the connected database is `leobloom_dev` is wrapped in `#if DEBUG`. In release builds, there's no protection against connecting to the wrong database.

**Impact:** If someone misconfigures the connection string in production, the app will happily write financial data to whatever database it connects to. The guard is well-intentioned but only protects during development.

**Remediation:** Consider making the guard unconditional but configurable — e.g., read an expected database name from config and verify at startup regardless of build configuration.

### L-3: No Audit Trail for Fiscal Period Reopen

**File:** `Src/LeoBloom.Utilities/FiscalPeriodService.fs` (line 55)

**Description:** When a fiscal period is reopened, the reason is logged via Serilog but not persisted to the database. If logs rotate or are lost, the audit trail for why a closed period was reopened disappears.

**Impact:** In a financial system, reopening a closed period is a sensitive operation. Auditors would want to know who reopened it and why. A log line is not durable enough.

**Remediation:** Add a `reopen_reason` and `reopened_at` column to the fiscal_period table, or create a separate audit events table.

### L-4: No Length Validation on Several String Inputs

**Description:** The domain validation layer validates `name` (100 chars) and `counterparty` (100 chars) on obligation agreements, but several other string fields have no length validation in the application layer:

- `JournalEntry.description` — validated for non-empty but no max length (DB column is varchar(500))
- `JournalEntry.source` — no max length (DB column is varchar(50))
- `JournalEntryReference.referenceType` / `referenceValue` — no max length validation
- `VoidJournalEntryCommand.voidReason` — no max length (DB column is varchar(500))
- `Transfer.description` — no max length

The database will enforce the varchar limits and throw an exception, which will be caught and returned as a persistence error. But it's better to catch these at the validation layer with clear error messages.

**Remediation:** Add max-length checks in the domain validators matching the DB column sizes.

### L-5: Log Module Uses Mutable Static State

**File:** `Src/LeoBloom.Utilities/Log.fs` (line 12)

```fsharp
let mutable private initialized = false
```

**Description:** The idempotent initialization guard uses a mutable boolean with no thread-safety mechanism. In a CLI single-threaded context this is fine. Under concurrent access (e.g., if the API project were used), this is a race condition.

**Impact:** Minimal for current usage pattern (CLI). Theoretical double-initialization if called concurrently.

**Remediation:** Use `System.Threading.LazyInitializer` or `Lazy<unit>` for thread-safe initialization.

---

## Security Checklist

| Check | Status | Notes |
|-------|--------|-------|
| SQL queries parameterized | PASS | Every query uses `@param` placeholders with `AddWithValue` |
| No hardcoded secrets | PASS | Password comes from env var; connection string uses placeholder |
| No credentials in git | PASS | `.gitignore` excludes seed-prod; no `.env` files found |
| Input validation on write paths | PASS | Thorough domain validators on all commands |
| Transaction integrity | WARN | H-1: two services have non-atomic multi-phase writes |
| Error messages sanitized | WARN | M-1: raw exception messages returned to callers |
| Authentication on endpoints | N/A | API project is dead scaffold; consumption is CLI |
| CSRF protection | N/A | No web interface |
| Dependencies up-to-date | WARN | M-3: Npgsql version mismatch |
| Sensitive data in logs | PASS | No passwords or secrets logged; structured logging with Serilog |
| Database safety guards | PASS (dev) | Debug-only guard verifies correct database; missing in release |

---

## Files Reviewed

All `.fs` source files (non-test, non-obj):
- `Src/LeoBloom.Domain/Ledger.fs`, `Ops.fs`
- `Src/LeoBloom.Utilities/` — all 22 files (DataSource, Log, 10 repositories, 10 services)
- `Src/LeoBloom.Migrations/Program.fs`
- `Src/LeoBloom.Api/Program.fs`, `Controllers/WeatherForecastController.fs`
- All `.fsproj` files, all `appsettings*.json` files, `.gitignore`
- Migration SQL files (schema definitions + the lookup table elimination migration)
