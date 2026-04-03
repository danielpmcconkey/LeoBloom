# BRD-005: Post Journal Entry

**Project:** 005 — Post Journal Entry
**Author:** Basement Dweller (BA role)
**Date:** 2026-04-03
**Status:** Draft — Awaiting PO Approval

---

## 1. Business Objective

Deliver the core write path for the LeoBloom ledger: a service that accepts a
validated journal entry (header + lines + references), persists it atomically to
PostgreSQL, and returns the created entry with database-assigned IDs. This is the
first project that crosses the Domain → Dal boundary, turning pure domain types
into persisted financial records.

Without this, nothing posts. Trial balances, account balances, voids, obligation
posting — everything downstream depends on journal entries existing in the
database.

## 2. Scope

### In Scope

1. **Command DTO types** — `PostJournalEntryCommand` carrying the validated
   inputs needed to create a journal entry, its lines (including optional
   `memo`), and its references. These are distinct from the domain read types
   (which have `id`, `createdAt`, etc.). Line commands carry `accountId`,
   `amount`, `entryType`, and `memo` (optional). Reference commands carry
   `referenceType` and `referenceValue`.

2. **Application-layer validation** — a composite validation function that
   enforces all business rules from the backlog item before any SQL executes:
   - At least two lines
   - All amounts positive
   - Entry types are `debit` or `credit`
   - Debits equal credits (the cardinal rule)
   - Description is non-empty
   - Source: optional (nullable per schema). When provided, must be non-empty.
     **PO deviation note:** The backlog says "validate as non-empty string" for
     source. Per PO ruling (2026-04-03), source is nullable (consistent with
     the schema). When provided, it must be a non-empty string.
   - `fiscal_period_id` references an open period
   - `fiscal_period_id` references a period whose date range contains `entry_date`
   - Every `account_id` references an active account
   - Reference type and value are non-empty strings (for each reference)

3. **Persistence layer** (in `LeoBloom.Dal`) — a module that:
   - Opens a transaction
   - Inserts the `journal_entry` row, capturing the returned `id`
   - Inserts all `journal_entry_line` rows with the parent `id`
   - Inserts all `journal_entry_reference` rows with the parent `id`
   - Commits the transaction (or rolls back on any failure)
   - Returns the created entry with all database-assigned fields (`id`,
     `created_at`, `modified_at`)

4. **Service layer** (in `LeoBloom.Ledger`) — a function that:
   - Accepts a `PostJournalEntryCommand`
   - Runs all validation (calling Domain validation + DB lookups for period/account checks)
   - Calls the persistence layer
   - Returns `Result<PostedJournalEntry, string list>` — either the persisted
     entry or a list of validation errors

5. **BDD tests** — integration tests in `LeoBloom.Dal.Tests` exercising:
   - Happy path: simple 2-line entry posts successfully
   - Happy path: compound entry (3+ lines) posts successfully
   - Happy path: entry with references posts successfully
   - Validation rejection: unbalanced entry
   - Validation rejection: zero/negative amounts
   - Validation rejection: fewer than 2 lines
   - Validation rejection: empty description
   - Validation rejection: closed fiscal period
   - Validation rejection: entry_date outside fiscal period date range
   - Validation rejection: inactive account
   - Validation rejection: nonexistent fiscal period for entry_date
   - Validation rejection: invalid entry_type value (not debit/credit)
   - Happy path: entry with null source posts successfully
   - Atomicity: if line insert fails, no journal_entry row persists

6. **DataModelSpec update** — add a "Key Mutations" section documenting the
   insert path, or update existing sections to note that 005 delivers the write
   path.

### Out of Scope

- **Void journal entry** — Project 006.
- **API endpoints** — no HTTP layer. This is service + Dal only.
- **Bulk import** — single entry at a time.
- **Entry modification** — append-only. Once posted, entries are immutable.
- **Ops integration** — no `obligation_instance.journal_entry_id` updates. The
  ledger is self-contained.
- **Auto-creating fiscal periods** — if no period exists, reject with error.

## 3. Dependencies

| Dependency | Status | Notes |
|-----------|--------|-------|
| 001 — Database schema | Done | Tables exist: `journal_entry`, `journal_entry_line`, `journal_entry_reference` |
| 002 — Test harness | Done | TickSpec/xUnit BDD infrastructure |
| 004 — Domain types | Done | `Ledger.JournalEntry`, `JournalEntryLine`, `JournalEntryReference`, validation functions |
| 029 — Lookup elimination | Done | String-based DU columns, no lookup table dependencies |

## 4. Technical Approach

### 4.1 Project Structure

- `LeoBloom.Domain/Ledger.fs` — add command DTOs and new validation functions
  (fiscal period date range, account active, description non-empty)
- `LeoBloom.Dal/JournalEntryRepository.fs` — new file, persistence module using
  Npgsql + raw SQL (consistent with existing Dal patterns)
- `LeoBloom.Ledger/JournalEntryService.fs` — new file, orchestrates validation
  and persistence
- `LeoBloom.Dal.Tests/` — new feature file and step definitions for 005 scenarios

### 4.2 Why Raw SQL (Not Dapper)

The existing Dal uses `Npgsql` directly (see `ConnectionString.fs`,
`SharedSteps.fs`). No ORM or micro-ORM is currently in the dependency graph.
Adding Dapper for one insert path would be premature. Raw `NpgsqlCommand` with
parameterized queries is safe, explicit, and consistent with the codebase.

If the pattern proves verbose across multiple projects, Dapper can be introduced
later as a cross-cutting concern.

### 4.3 Transaction Semantics

The entire post operation (entry + lines + references) executes within a single
`NpgsqlTransaction`. If any insert fails, the transaction rolls back. No partial
state. This is the atomicity guarantee.

### 4.4 Validation Strategy

Validation is split into two categories:

1. **Pure validation** (no DB access) — line count, amounts positive, balanced,
   description non-empty, entry type values, reference fields non-empty. These
   reuse existing `Ledger.validateBalanced`, `validateAmountsPositive`,
   `validateMinimumLineCount` and add new pure validators.

2. **DB-dependent validation** — fiscal period is open, fiscal period date range
   contains entry_date, accounts are active. These require SELECT queries within
   the same transaction before the inserts.

All validation errors are collected (not fail-fast) and returned as
`Result<_, string list>`.

## 5. Edge Cases (per Backlog)

| Case | Behavior |
|------|----------|
| `entry_date` in valid period but period is closed | Reject |
| Duplicate `reference_type` + `reference_value` on different entries | Allow |
| Inactive account | Reject |
| `entry_date` in the future | Allow (period must exist and be open) |
| No fiscal period for `entry_date` | Reject with clear error |
| Source is null | Allow — PO-approved deviation from backlog (see validation note above) |
| Source is empty string | Reject — when provided, must be non-empty |
| Zero references | Allow (references are optional) |

## 6. Acceptance Criteria Summary

See BDD document (Project005-bdd.md) for full Gherkin scenarios. The BRD
criteria are:

- AC-001: A valid 2-line journal entry persists all three tables atomically
- AC-002: Compound entries (3+ lines) persist correctly
- AC-003: References persist with the entry
- AC-004: All pure validation rules reject invalid input with descriptive errors
- AC-005: DB-dependent validation (period open, date range, account active) rejects invalid input
- AC-006: Transaction atomicity — partial failures leave no orphaned rows
- AC-007: Returned result includes database-assigned IDs and timestamps
- AC-008: DataModelSpec updated to document the write path
