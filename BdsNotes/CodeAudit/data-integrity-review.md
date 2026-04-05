# LeoBloom Data Integrity Audit

**Date:** 2026-04-05
**Auditor:** BD (basement dweller)
**Scope:** Full codebase review -- schema, migrations, domain logic, services, repositories

---

## Summary Verdict

The codebase is significantly better than average for a project at this stage. The
fundamentals are sound: transactions wrap writes, double-entry balance validation
exists in the domain layer, FK constraints use ON DELETE RESTRICT, and the
void-not-delete pattern is correct for a financial system. The test suite is real
and tests actual DB constraints, not just mocks.

That said, this is a financial system, so "better than average" is not the bar.
There is one critical issue (the obligation posting service's non-atomic
multi-phase write), several warnings that could cause real problems under load or
during recovery, and a handful of observations worth tracking.

**Overall: B+.** The architecture is right. The gaps are in enforcement edges.

---

## CRITICAL Issues

### CRIT-1: ObligationPostingService.postToLedger is not atomic across its three phases

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/ObligationPostingService.fs`

This is the single biggest data integrity risk in the codebase. The function
performs three separate operations in three separate transactions:

1. Phase 1 (lines 15-72): Read + validate instance/agreement/period -- commits and closes its transaction.
2. Phase 2 (line 102): Post journal entry via `JournalEntryService.post` -- opens its own connection + transaction internally.
3. Phase 3 (line 120): Transition instance to "posted" via `ObligationInstanceService.transition` -- opens yet another connection + transaction.

**The failure scenario:** If Phase 2 succeeds (journal entry created and committed)
but Phase 3 fails (instance not transitioned to "posted"), you have a committed
journal entry in the ledger with no corresponding status update on the obligation
instance. The instance stays "confirmed," and a retry would attempt to post a
second journal entry for the same obligation.

The code at lines 122-123 logs a warning about this but does not prevent
double-posting on retry. There is no idempotency guard (e.g., checking whether a
journal entry with a matching obligation reference already exists before posting).

**Why it matters:** This is the textbook definition of partial state leak in a
financial system. Money appears in the ledger from a ghost obligation.

**Fix:** Either:
- (a) Run all three phases in a single transaction (requires refactoring the service
  layer to accept an external transaction), or
- (b) Add an idempotency check at the start of `postToLedger` that looks for an
  existing journal entry with `reference_type = 'obligation'` and
  `reference_value = string instanceId`, and if found, skip Phase 2 and proceed
  directly to Phase 3.

### CRIT-2: TransferService.confirm has the same multi-phase atomicity gap

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/TransferService.fs`, lines 80-165

Same pattern as CRIT-1. Three phases, three transactions:

1. Read + validate the transfer and fiscal period.
2. Post journal entry via `JournalEntryService.post`.
3. Update transfer record to "confirmed" (lines 153-158).

If Phase 2 succeeds but Phase 3 fails, you get a committed journal entry with a
dangling transfer still in "initiated" status. The code at lines 161-164
acknowledges this ("retry is safe") but there is no idempotency guard to prevent a
retry from creating a second journal entry.

The log message at line 163 claims "retry is safe" -- it is not safe without a
duplicate-detection check.

**Fix:** Same approach as CRIT-1. Either single transaction or idempotency guard
on `reference_type = 'transfer'`.

---

## CRITICAL-ADJACENT: Race Conditions

### CRIT-3: TOCTOU gap in ObligationPostingService between read and write phases

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/ObligationPostingService.fs`

Phase 1 reads the instance, validates it is "confirmed," then commits the read
transaction. Between Phase 1 committing and Phase 2 starting, another process
could transition the same instance (e.g., another `postToLedger` call). Both
callers would pass Phase 1 validation and both would create journal entries.

This is a classic Time-of-Check-Time-of-Use (TOCTOU) race. Even in a single-user
CLI scenario, it becomes relevant if batch operations or background jobs are ever
introduced.

**Fix:** Use `SELECT ... FOR UPDATE` in the read phase and keep the transaction
open through the write phase, or use a unique constraint / advisory lock to
prevent duplicate postings.

---

## Warnings

### WARN-1: No CHECK constraint on journal_entry_line.entry_type

**File:** `/workspace/LeoBloom/Src/LeoBloom.Migrations/Migrations/1712000009000_CreateJournalEntryLine.sql`

The `entry_type` column is `varchar(6) NOT NULL` with no CHECK constraint. The
database will happily accept `'foo'`, `'DEBIT'`, or any string up to 6 chars.
Validation exists only in the application layer (`Ledger.fs`, `EntryType.fromString`).

For a financial system, this should be enforced at the DB level:

```sql
CONSTRAINT chk_entry_type CHECK (entry_type IN ('debit', 'credit'))
```

The same applies to:
- `ledger.account_type.normal_balance` -- no CHECK, accepts any varchar(6)
- `ops.obligation_agreement.obligation_type` -- no CHECK after migration 019
- `ops.obligation_agreement.cadence` -- no CHECK after migration 019
- `ops.obligation_instance.status` -- no CHECK after migration 019
- `ops.transfer.status` -- no CHECK, accepts any varchar(20)

**Impact:** If any code path writes a malformed value, the repositories will
`failwith` on read (e.g., `TransferRepository.mapReader` line 20: "Corrupt
transfer status in DB"), which is a crash, not a graceful error.

### WARN-2: No CHECK constraint on journal_entry_line.amount

**File:** `/workspace/LeoBloom/Src/LeoBloom.Migrations/Migrations/1712000009000_CreateJournalEntryLine.sql`

The `amount` column is `numeric(12,2) NOT NULL` but has no `CHECK (amount > 0)`.
Application-layer validation (`validateAmountsPositive` in `Ledger.fs`) rejects
zero and negative amounts, but nothing stops a direct SQL insert or a future code
path from inserting non-positive amounts.

Same applies to `ops.transfer.amount`.

### WARN-3: No DB-level enforcement of double-entry balance

The fundamental invariant of this system -- that every journal entry must balance
(sum of debits = sum of credits) -- is enforced only in application code
(`Ledger.validateBalanced`). There is no database trigger or constraint that
prevents an unbalanced entry from being committed.

If any code path bypasses `JournalEntryService.post` (e.g., a future bulk import,
a migration, or manual SQL), unbalanced entries can enter the system silently.

**Mitigation options:**
- A PostgreSQL trigger on `journal_entry_line` that checks the sum after each
  statement (AFTER INSERT trigger with CONSTRAINT TRIGGER deferred to end of
  transaction).
- Or a periodic reconciliation query that flags unbalanced entries.

### WARN-4: Fiscal period date ranges are not validated for overlaps or gaps

**File:** `/workspace/LeoBloom/Src/LeoBloom.Migrations/Migrations/1712000004000_CreateFiscalPeriod.sql`

There is no exclusion constraint or CHECK preventing overlapping fiscal periods.
`FiscalPeriodRepository.findByDate` uses `LIMIT 1` with `ORDER BY start_date`,
meaning if two periods overlap, it silently picks one. There is also no constraint
preventing `start_date > end_date`.

For a ledger, overlapping periods mean a single date could map to multiple periods,
and the system would silently pick one. This can cause entries to land in the wrong
period.

```sql
-- Prevent overlap:
ALTER TABLE ledger.fiscal_period
  ADD CONSTRAINT no_overlap
  EXCLUDE USING gist (daterange(start_date, end_date, '[]') WITH &&);

-- Prevent inverted ranges:
ALTER TABLE ledger.fiscal_period
  ADD CONSTRAINT valid_range CHECK (start_date <= end_date);
```

### WARN-5: Void does not check fiscal period open status

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/JournalEntryService.fs`, `voidEntry` function

When posting a journal entry, the service correctly checks that the fiscal period
is open. But when voiding an entry, there is no check. This means you can void
entries in closed fiscal periods, which changes the effective balances for that
period without the period being re-opened.

Whether this is intentional depends on the business rules, but in most accounting
systems, voiding in a closed period is prohibited -- you would instead create a
reversing entry in the current open period.

### WARN-6: Migration 019 (EliminateLookupTables) is destructive and not idempotent

**File:** `/workspace/LeoBloom/Src/LeoBloom.Migrations/Migrations/1712000019000_EliminateLookupTables.sql`

This migration drops four tables (`obligation_type`, `obligation_status`,
`cadence`, `payment_method`). The DOWN migration recreates them but relies on
hardcoded ID sequences matching the original inserts. If the sequences have
advanced (e.g., someone added and deleted a row), the restored IDs won't match
the original foreign keys.

Since migration 019 already dropped the FK columns, this is somewhat moot -- but
the DOWN path would create lookup tables with auto-generated IDs that may not
match what the UPDATE statements expect in the reverse direction.

In practice, rolling back migration 019 on a populated database would silently
produce NULL `status_id` / `obligation_type_id` values (the UPDATE subqueries
would return no rows for any values that were manually inserted after the original
seed data).

### WARN-7: OpeningBalanceService reads accounts outside a transaction

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/OpeningBalanceService.fs`, line 73-75

`lookupAccounts` uses a bare connection (no transaction). This is a read-only
operation so it does not corrupt data, but it means the account lookup is not
serializable with the subsequent journal entry post. An account could be
deactivated between the lookup and the post. The post's own validation would catch
this (it re-validates accounts), so the practical risk is low -- but it is
inconsistent with the transaction discipline used elsewhere.

---

## Observations

### OBS-1: No audit trail for data changes

There is no `modified_by` column, no change history table, and no audit log for
who changed what and when. The `modified_at` timestamp exists on mutable tables
but only records the last change, not a history. For a personal finance tool this
is probably fine, but worth noting for future requirements.

### OBS-2: Seed data migration (006) DOWN path will fail with FK violations

**File:** `/workspace/LeoBloom/Src/LeoBloom.Migrations/Migrations/1712000006000_SeedChartOfAccounts.sql`

The DOWN migration uses `DELETE FROM ledger.account WHERE code IN (...)` but does
not delete in child-first order. Accounts like '5000' have children ('5100',
'5300', etc.) that reference it via `parent_code`. The DELETE will fail with FK
violations unless the database happens to process them in the right order (which
it won't -- the IN clause does not guarantee deletion order).

The DOWN migration also doesn't account for journal entry lines that reference
these accounts, which would also cause FK violations.

### OBS-3: The account hierarchy uses parent_code (varchar FK) not parent_id (integer FK)

**File:** `/workspace/LeoBloom/Src/LeoBloom.Migrations/Migrations/1712000003000_CreateAccount.sql`

The self-referential FK is `parent_code varchar(10) REFERENCES ledger.account(code)`.
This works because `code` has a UNIQUE constraint, but it means renaming an
account code would require cascading updates to all children. Currently there is
no account rename functionality, so this is not a problem yet, but it is an
unusual design choice that could bite later. The more conventional approach is
`parent_id integer REFERENCES ledger.account(id)`.

### OBS-4: invoice.total_amount is not enforced as rent_amount + utility_share

**File:** `/workspace/LeoBloom/Src/LeoBloom.Migrations/Migrations/1712000018000_CreateInvoice.sql`

The `total_amount` column could drift from `rent_amount + utility_share` if
updated independently. A generated column or CHECK constraint would prevent this:

```sql
CONSTRAINT chk_total CHECK (total_amount = rent_amount + utility_share)
```

### OBS-5: No index on journal_entry.entry_date or journal_entry.fiscal_period_id

The balance sheet, income statement, trial balance, and account balance queries
all filter on `entry_date` and/or `fiscal_period_id` with joins to
`journal_entry_line`. As data grows, these queries will slow down without
indexes. Currently fine for a personal finance tool, but worth adding proactively:

```sql
CREATE INDEX idx_je_entry_date ON ledger.journal_entry (entry_date);
CREATE INDEX idx_je_fiscal_period ON ledger.journal_entry (fiscal_period_id);
CREATE INDEX idx_jel_account ON ledger.journal_entry_line (account_id);
CREATE INDEX idx_jel_journal_entry ON ledger.journal_entry_line (journal_entry_id);
```

### OBS-6: Test cleanup is best-effort, not transactional

**File:** `/workspace/LeoBloom/Src/LeoBloom.Tests/TestHelpers.fs`, `TestCleanup.deleteAll`

The cleanup catches exceptions per-table and continues, which means a test failure
can leave orphaned data in the database. This is mitigated by the unique prefix
strategy (TestData.uniquePrefix), so orphaned data won't collide with other tests.
It is a pragmatic choice but worth noting.

### OBS-7: No ON DELETE behavior specified for journal_entry_line -> journal_entry

All FK constraints use `ON DELETE RESTRICT`, which is the right call for a
financial system. This means you cannot accidentally delete a journal entry that
has lines, references, or obligation instances pointing to it. Good.

### OBS-8: The overdue detection loop (ObligationInstanceService.detectOverdue) opens N separate transactions

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/ObligationInstanceService.fs`, lines 117-164

Each candidate instance is transitioned in its own transaction (via the `transition`
function). This is actually correct behavior -- if one transition fails, the others
should still proceed. But it means overdue detection for 100 instances opens 100
separate connections. At personal-finance scale this is fine; at any larger scale
it would need batching.

---

## What is done well

To be clear about what does NOT need fixing:

- **Transaction boundaries on core writes.** `JournalEntryService.post` correctly
  wraps DB validation + all inserts (entry, lines, references) in a single
  transaction. If any insert fails, the entire entry is rolled back.

- **Void-not-delete pattern.** Journal entries are voided with a timestamp and
  reason, never deleted. All balance queries correctly filter `WHERE voided_at IS NULL`.
  This is textbook correct for a ledger.

- **ON DELETE RESTRICT everywhere.** No cascading deletes. You cannot accidentally
  nuke data through a parent deletion.

- **Pure validation before DB validation.** The two-phase validation pattern
  (pure checks first, then DB-dependent checks) means you never open a DB
  connection for obviously invalid input. Clean separation.

- **Domain types are immutable F# records.** No mutable state in the domain layer.
  The Ops.fs status transition state machine is explicit and well-defined.

- **Test suite validates actual DB constraints.** The LedgerConstraintTests and
  OpsConstraintTests verify NOT NULL, UNIQUE, and FK violations against the real
  database. This is the right way to test schema integrity.

- **DataSource safety guard.** The `#if DEBUG` check that verifies you are connected
  to `leobloom_dev` is a great safety net.

---

## Priority recommendation

1. **Fix CRIT-1 and CRIT-2 first.** These are the only paths where committed data
   can become inconsistent. Add idempotency guards at minimum.
2. **Add CHECK constraints (WARN-1, WARN-2).** Low effort, high value. One migration.
3. **Add fiscal period overlap exclusion constraint (WARN-4).** One migration.
4. **Consider whether voiding in closed periods is intentional (WARN-5).**
5. **Everything else is "fix when it itches."**
