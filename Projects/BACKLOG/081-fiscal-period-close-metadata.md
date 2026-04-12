# 081 — Fiscal Period Close Metadata & Audit Trail

**Epic:** K — Fiscal Period Closure
**Depends On:** 009 (close/reopen), 055 (closed-period posting guard)
**Status:** Not started

---

Upgrade the close/reopen mechanism from a bare `is_open` toggle to a full
metadata + audit trail system. This is the foundation for everything else
in the fiscal period closure epic.

**Origin:** Hobson brief `fiscal-period-closure.md` §1, §6, §7.

## What exists today

- `FiscalPeriod` has `isOpen: bool` toggled by `FiscalPeriodRepository.setIsOpen`
- `FiscalPeriodService.closePeriod` / `reopenPeriod` exist (P009)
- CLI `fiscal-period close --id N` / `fiscal-period reopen --id N --reason R` exist
- Close is idempotent, reopen requires reason (logged, not persisted)
- No `closed_at`, `closed_by`, `reopened_count` on the period
- No audit table

## New work

### Schema migration

**`ledger.fiscal_period` — add columns:**

| Column | Type | Nullable | Default | Notes |
|---|---|---|---|---|
| `closed_at` | `timestamptz` | yes | NULL | Set on close, cleared on reopen |
| `closed_by` | `varchar(50)` | yes | NULL | Free text actor identifier |
| `reopened_count` | `integer` | no | 0 | Incremented on each reopen |

Backfill: all existing periods get `closed_at = NULL`, `closed_by = NULL`,
`reopened_count = 0`. Periods currently closed (`is_open = false`) get
`closed_at = now()`, `closed_by = 'migration'`.

**New table: `ledger.fiscal_period_audit`**

| Column | Type | Notes |
|---|---|---|
| `id` | `serial` | PK |
| `fiscal_period_id` | `integer` | FK to `fiscal_period(id)` ON DELETE RESTRICT |
| `action` | `varchar(20)` | `'closed'` or `'reopened'` |
| `actor` | `varchar(50)` | Free text |
| `occurred_at` | `timestamptz` | Default `now()` |
| `note` | `text` | For close: `--note` value; for reopen: `--reason` (required) |

### Domain type changes

Update `FiscalPeriod` record to include new fields:
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

Add `FiscalPeriodAuditEntry` type:
```fsharp
type FiscalPeriodAuditEntry =
    { id: int
      fiscalPeriodId: int
      action: string
      actor: string
      occurredAt: DateTimeOffset
      note: string option }
```

### Repository changes

- `FiscalPeriodRepository.setIsOpen` → upgrade to `closePeriod` that sets
  `is_open = false`, `closed_at = now()`, `closed_by = @actor`
- Add `FiscalPeriodRepository.reopenPeriod` that sets `is_open = true`,
  `closed_at = NULL`, `closed_by = NULL`, increments `reopened_count`
- Add `FiscalPeriodAuditRepository.insert` and `listByPeriod`
- Update `mapReader` in repo to handle new columns

### Service changes

- `closePeriod` gains `actor: string` and optional `note: string` params.
  Writes audit row on close.
- `reopenPeriod` writes audit row with reason as note. Increments
  `reopened_count`.
- Add `listAudit (periodId: int)` → returns audit trail for a period.

### CLI changes

- `fiscal-period close --id N` gains optional `--actor` (default "dan")
  and `--note` flags
- `fiscal-period reopen --id N --reason R` gains optional `--actor` flag
- Add `fiscal-period audit --id N` subcommand — lists all close/reopen
  events for a period
- `fiscal-period list` / `fiscal-period status` output updated to show
  `closed_at`, `closed_by`, `reopened_count`

## Acceptance criteria

- [ ] Migration adds `closed_at`, `closed_by`, `reopened_count` to `ledger.fiscal_period`
- [ ] Migration creates `ledger.fiscal_period_audit` table with FK constraint
- [ ] Backfill: currently-closed periods get `closed_at = now()`, `closed_by = 'migration'`
- [ ] Backfill: all periods get `reopened_count = 0`
- [ ] `fiscal-period close --id N` sets `closed_at`, `closed_by`, writes audit row
- [ ] Closing an already-closed period is idempotent, returns clear message
- [ ] `fiscal-period reopen --id N --reason "..."` clears `closed_at`, increments `reopened_count`, writes audit row
- [ ] `fiscal-period audit --id N` lists all close/reopen events for the period
- [ ] `fiscal-period list` output shows `closed_at`, `reopened_count` columns
- [ ] All existing fiscal period tests continue to pass
- [ ] `FiscalPeriod` domain type includes new fields
- [ ] `--json` supported on all new/modified commands
- [ ] Exit codes follow `ExitCodes` convention

## Out of scope

- Pre-close validation (P082)
- Void enforcement on closed periods (P083)
- Reversing entries (P083)
- Post-close adjustments (P083)
- Report disclosure (P084)
