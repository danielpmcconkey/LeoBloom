# Project 081 — Plan

## Objective

Upgrade fiscal period close/reopen from a bare `is_open` boolean toggle to a
full metadata + audit trail system. Add `closed_at`, `closed_by`,
`reopened_count` to `fiscal_period`. Create `fiscal_period_audit` table. Upgrade
repository, service, and CLI layers. This is the foundation for Epic K
(P082-P084).

## Phases

### Phase 1: Schema Migration

**What:** Single migration that adds columns to `fiscal_period` and creates the
`fiscal_period_audit` table, with backfill for existing data.

**Files:**
- Create: `Src/LeoBloom.Migrations/Migrations/1712000026000_FiscalPeriodCloseMetadata.sql`

**Details:**

ALTER TABLE `ledger.fiscal_period`:
- Add `closed_at timestamptz NULL DEFAULT NULL`
- Add `closed_by varchar(50) NULL DEFAULT NULL`
- Add `reopened_count integer NOT NULL DEFAULT 0`
- Backfill: `UPDATE ledger.fiscal_period SET closed_at = now(), closed_by = 'migration' WHERE is_open = false`

CREATE TABLE `ledger.fiscal_period_audit`:
- `id serial PRIMARY KEY`
- `fiscal_period_id integer NOT NULL REFERENCES ledger.fiscal_period(id) ON DELETE RESTRICT`
- `action varchar(20) NOT NULL` — `'closed'` or `'reopened'`
- `actor varchar(50) NOT NULL`
- `occurred_at timestamptz NOT NULL DEFAULT now()`
- `note text`

DOWN migration: drop audit table, then drop the three columns.

**Verification:** `dotnet run --project Src/LeoBloom.Migrations -- up` succeeds.
Query `\d ledger.fiscal_period` shows new columns. Query
`\d ledger.fiscal_period_audit` shows table with FK.

---

### Phase 2: Domain Types

**What:** Extend `FiscalPeriod` record and command types. Add
`FiscalPeriodAuditEntry` type.

**Files:**
- Modify: `Src/LeoBloom.Domain/Ledger.fs`

**Details:**

Update `FiscalPeriod`:
```fsharp
type FiscalPeriod =
    { id: int
      periodKey: string
      startDate: DateOnly
      endDate: DateOnly
      isOpen: bool
      closedAt: DateTimeOffset option
      closedBy: string option
      reopenedCount: int
      createdAt: DateTimeOffset }
```

Update `CloseFiscalPeriodCommand`:
```fsharp
type CloseFiscalPeriodCommand =
    { fiscalPeriodId: int
      actor: string
      note: string option }
```

Update `ReopenFiscalPeriodCommand`:
```fsharp
type ReopenFiscalPeriodCommand =
    { fiscalPeriodId: int
      reason: string
      actor: string }
```

Add `FiscalPeriodAuditEntry`:
```fsharp
type FiscalPeriodAuditEntry =
    { id: int
      fiscalPeriodId: int
      action: string
      actor: string
      occurredAt: DateTimeOffset
      note: string option }
```

**Verification:** `dotnet build` compiles (expect downstream compile errors in
repo/service/CLI until those are updated — that's fine, just confirms types are
well-formed).

---

### Phase 3: Repository Layer

**What:** Update `FiscalPeriodRepository` for new columns. Replace `setIsOpen`
with dedicated `closePeriod` / `reopenPeriod`. Create
`FiscalPeriodAuditRepository`.

**Files:**
- Modify: `Src/LeoBloom.Ledger/FiscalPeriodRepository.fs`
- Create: `Src/LeoBloom.Ledger/FiscalPeriodAuditRepository.fs`

**Details:**

**FiscalPeriodRepository changes:**

1. Update `readPeriod` to map new columns (ordinal positions shift):
   - Add `closedAt` (index 5, nullable timestamptz)
   - Add `closedBy` (index 6, nullable varchar)
   - Add `reopenedCount` (index 7, int)
   - `createdAt` moves to index 8

2. Update all SELECT queries to include `closed_at, closed_by, reopened_count`
   in the column list (affects `findById`, `findByDate`, `findByKey`,
   `findOverlapping`, `listAll`, `create` RETURNING clause).

3. Remove `setIsOpen`. Replace with:

   `closePeriod (txn) (periodId: int) (actor: string) : FiscalPeriod option`:
   ```sql
   UPDATE ledger.fiscal_period
   SET is_open = false, closed_at = now(), closed_by = @actor
   WHERE id = @id
   RETURNING id, period_key, start_date, end_date, is_open,
             closed_at, closed_by, reopened_count, created_at
   ```

   `reopenPeriod (txn) (periodId: int) : FiscalPeriod option`:
   ```sql
   UPDATE ledger.fiscal_period
   SET is_open = true, closed_at = NULL, closed_by = NULL,
       reopened_count = reopened_count + 1
   WHERE id = @id
   RETURNING id, period_key, start_date, end_date, is_open,
             closed_at, closed_by, reopened_count, created_at
   ```

**FiscalPeriodAuditRepository** (new file):

Module `LeoBloom.Ledger.FiscalPeriodAuditRepository`:

`readAuditEntry (reader) : FiscalPeriodAuditEntry` — column mapper

`insert (txn) (entry: {| fiscalPeriodId: int; action: string; actor: string; note: string option |}) : FiscalPeriodAuditEntry`:
```sql
INSERT INTO ledger.fiscal_period_audit
    (fiscal_period_id, action, actor, note)
VALUES (@fiscal_period_id, @action, @actor, @note)
RETURNING id, fiscal_period_id, action, actor, occurred_at, note
```

`listByPeriod (txn) (periodId: int) : FiscalPeriodAuditEntry list`:
```sql
SELECT id, fiscal_period_id, action, actor, occurred_at, note
FROM ledger.fiscal_period_audit
WHERE fiscal_period_id = @fiscal_period_id
ORDER BY occurred_at ASC
```

**Verification:** `dotnet build` compiles. Repo functions can be exercised
through the service layer tests in Phase 5.

---

### Phase 4: Service Layer

**What:** Update `FiscalPeriodService.closePeriod` and `reopenPeriod` to use new
repo methods, write audit rows, and accept actor/note params. Add `listAudit`.

**Files:**
- Modify: `Src/LeoBloom.Ledger/FiscalPeriodService.fs`

**Details:**

**`closePeriod` changes:**
- Signature: `closePeriod (txn) (cmd: CloseFiscalPeriodCommand) : Result<FiscalPeriod, string list>`
- On close (period was open): call `FiscalPeriodRepository.closePeriod txn periodId cmd.actor`, then `FiscalPeriodAuditRepository.insert` with action `"closed"`, actor `cmd.actor`, note `cmd.note`.
- Idempotent: if period already closed, return `Ok period` without writing another audit row or updating metadata. This preserves P009 contract.

**`reopenPeriod` changes:**
- Signature: `reopenPeriod (txn) (cmd: ReopenFiscalPeriodCommand) : Result<FiscalPeriod, string list>`
- Validate reason (existing `validateReopenReason`).
- On reopen (period was closed): call `FiscalPeriodRepository.reopenPeriod txn periodId`, then `FiscalPeriodAuditRepository.insert` with action `"reopened"`, actor `cmd.actor`, note `Some cmd.reason`.
- Idempotent: if period already open, return `Ok period` without audit row.

**`listAudit` (new):**
- Signature: `listAudit (txn) (periodId: int) : Result<FiscalPeriodAuditEntry list, string list>`
- Validates period exists via `findById`, then calls `FiscalPeriodAuditRepository.listByPeriod`.

**Verification:** `dotnet build` compiles. Full verification in Phase 5 tests.

---

### Phase 5: CLI Layer

**What:** Add `--actor` and `--note` flags to close/reopen. Add `audit`
subcommand. Update list/detail output to show new columns.

**Files:**
- Modify: `Src/LeoBloom.CLI/PeriodCommands.fs`
- Modify: `Src/LeoBloom.CLI/OutputFormatter.fs`

**Details:**

**Argu changes:**

`PeriodCloseArgs` — add:
- `Actor of string` (optional, default `"dan"`)
- `Note of string` (optional)

`PeriodReopenArgs` — add:
- `Actor of string` (optional, default `"dan"`)

New `PeriodAuditArgs`:
- `[<MainCommand; Mandatory>] Period of string`
- `Json`

`PeriodArgs` — add:
- `Audit of ParseResults<PeriodAuditArgs>`

**Handler changes:**

`handleClose`: build `CloseFiscalPeriodCommand` with actor (default `"dan"`)
and note from args.

`handleReopen`: build `ReopenFiscalPeriodCommand` with actor (default `"dan"`).

`handleAudit` (new): resolve period ID, call `FiscalPeriodService.listAudit`,
output via new `writeAuditList` formatter.

**OutputFormatter changes:**

`formatFiscalPeriod`: add lines for `Closed At`, `Closed By`, `Reopened Count`
(only show Closed At / Closed By when period is closed).

`formatFiscalPeriodList`: add `Closed At` and `Reopen#` columns to the table.

`formatAuditEntry` (new): human-readable single audit entry.

`formatAuditList` (new): tabular list of audit entries.

`writeAuditList` (new): dedicated write function (same pattern as
`writePeriodList`).

Add `FiscalPeriodAuditEntry` case to the `formatHuman` match expression.

**Verification:** CLI commands work end-to-end:
- `fiscal-period close 1 --actor dan --note "month end" --json`
- `fiscal-period reopen 1 --reason "correction needed" --actor dan --json`
- `fiscal-period audit 1 --json`
- `fiscal-period list --json`

---

### Phase 6: Tests & Regression

**What:** Ensure all existing fiscal period tests pass. New behavioral tests
will be defined by Gherkin Writer and implemented by QE — this phase is about
confirming no regressions.

**Files:**
- Possibly modify: `Src/LeoBloom.Tests/FiscalPeriodTests.fs` (if compile
  errors from type changes)
- Possibly modify: `Src/LeoBloom.Tests/FiscalPeriodManagementTests.fs`
- Possibly modify: other test files that construct `FiscalPeriod` records

**Details:**

The `FiscalPeriod` type gains three fields. Any test that constructs a
`FiscalPeriod` record literal will need the new fields added. The `readPeriod`
path (from DB) will work automatically, but record literals in assertions or
setup code need updating.

Similarly, `CloseFiscalPeriodCommand` gains `actor` and `note` fields, and
`ReopenFiscalPeriodCommand` gains `actor`. All call sites need updating.

Run `dotnet test` and fix any compilation or assertion failures.

**Verification:** `dotnet test` passes with zero failures.

---

## Acceptance Criteria

### Behavioral (Gherkin candidates)
- [ ] `fiscal-period close --id N` sets `closed_at`, `closed_by`, writes audit row
- [ ] Closing an already-closed period is idempotent, returns clear message
- [ ] `fiscal-period reopen --id N --reason "..."` clears `closed_at`, increments `reopened_count`, writes audit row
- [ ] `fiscal-period audit --id N` lists all close/reopen events for the period
- [ ] `fiscal-period list` output shows `closed_at`, `reopened_count` columns
- [ ] `--json` supported on all new/modified commands
- [ ] Exit codes follow `ExitCodes` convention

### Structural (Builder/QE verify directly)
- [ ] Migration adds `closed_at`, `closed_by`, `reopened_count` to `ledger.fiscal_period`
- [ ] Migration creates `ledger.fiscal_period_audit` table with FK constraint (ON DELETE RESTRICT)
- [ ] Backfill: currently-closed periods get `closed_at = now()`, `closed_by = 'migration'`
- [ ] Backfill: all periods get `reopened_count = 0`
- [ ] `FiscalPeriod` domain type includes `closedAt`, `closedBy`, `reopenedCount`
- [ ] All existing fiscal period tests continue to pass

## Risks

| Risk | Mitigation |
|------|------------|
| `readPeriod` ordinal shift breaks all queries | Single pass: update all SELECT column lists and `readPeriod` together in Phase 3. Named columns, not `SELECT *`. |
| Existing tests construct `FiscalPeriod` record literals | Phase 6 explicitly addresses. Compiler will catch every site. |
| `setIsOpen` callers outside fiscal period service | Grep for `setIsOpen` — it's only called from `FiscalPeriodService`. Single call site replacement. |
| Migration backfill on empty DB (no closed periods) | `UPDATE ... WHERE is_open = false` is a no-op on empty DB. Safe. |
| Audit FK with ON DELETE RESTRICT blocks period deletion | Intentional per spec. Period deletion is not a supported operation today. |

## Out of Scope

- Pre-close validation (P082)
- Void enforcement on closed periods (P083)
- Reversing entries (P083)
- Post-close adjustments (P083)
- Report disclosure (P084)
- New behavioral test implementation (Gherkin Writer + QE responsibility)

## Test Year Reservation

New P081 tests (if any are needed for structural verification) should use year
**2098** to avoid conflicts with existing reservations (2091-2097 taken).

## Dependencies

- P009 (close/reopen) — Done
- P055 (closed-period posting guard) — Done
- No external library additions needed
