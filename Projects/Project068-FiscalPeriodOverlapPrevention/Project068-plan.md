# P068 — Fiscal Period Overlap Prevention Plan

## Objective

Prevent creation of fiscal periods with overlapping date ranges. Today, `FiscalPeriodService.createPeriod` validates key format and date ordering but explicitly skips overlap detection (documented comment at line 22). A new period with dates that overlap an existing period would cause `findByDate` to return ambiguous results, silently posting entries to the wrong period.

## Phases

### Phase 1: Repository — Add `findOverlapping` Query

**What:** Add a new function `findOverlapping` to `FiscalPeriodRepository` that queries for any existing fiscal period whose date range overlaps a proposed `(startDate, endDate)` range.

**Files:**
- `Src/LeoBloom.Ledger/FiscalPeriodRepository.fs` — add `findOverlapping` function

**SQL logic:**
```sql
SELECT id, period_key, start_date, end_date, is_open, created_at
FROM ledger.fiscal_period
WHERE start_date <= @proposedEnd AND end_date >= @proposedStart
LIMIT 1
```
This is the standard overlap test: two ranges `[A, B]` and `[C, D]` overlap iff `A <= D AND B >= C`.

**Verification:** Function compiles and is callable from the service layer.

### Phase 2: Service — Wire Overlap Check into `createPeriod`

**What:** In `FiscalPeriodService.createPeriod`, after the existing validation (non-blank key, start <= end) and before the INSERT, call `FiscalPeriodRepository.findOverlapping`. If a conflicting period is found, return an error that identifies the conflicting period by key and dates.

**Files:**
- `Src/LeoBloom.Ledger/FiscalPeriodService.fs` — modify `createPeriod`

**Error format:**
```
"Date range overlaps existing period '{periodKey}' ({startDate} to {endDate})"
```

**Details:**
- Remove or update the comment "Does NOT check for date overlaps (see F3 resolution)" since this is now resolved.
- The overlap check runs inside the same transaction, so it's consistent with the subsequent INSERT.

**Verification:** Calling `createPeriod` with overlapping dates returns `Error` with a message containing the conflicting period's key.

### Phase 3: Migration — PostgreSQL Exclusion Constraint (Structural Backstop)

**What:** Add a database-level exclusion constraint as defense-in-depth. This catches overlap even if code bypasses the service layer (e.g., direct SQL, future code paths).

**Files:**
- `Src/LeoBloom.Migrations/Migrations/1712000024000_FiscalPeriodOverlapExclusion.sql` — new migration

**SQL:**
```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE ledger.fiscal_period
    ADD CONSTRAINT excl_fiscal_period_date_overlap
    EXCLUDE USING gist (
        daterange(start_date, end_date, '[]') WITH &&
    );
```

**Notes:**
- `btree_gist` extension is required for exclusion constraints on range types. `CREATE EXTENSION IF NOT EXISTS` is idempotent.
- The `'[]'` makes both bounds inclusive, matching the application's semantics (start_date and end_date are both inclusive).
- The service-layer check (Phase 2) gives a friendly error message; this constraint is the structural backstop that returns a PostgreSQL error if the service check is ever bypassed.

**Verification:** Attempting a direct `INSERT` of overlapping periods via SQL fails with an exclusion violation.

### Phase 4: Gherkin Spec — Overlap Rejection Scenarios

**What:** Create a new feature file for fiscal period management concerns beyond close/reopen. The PO noted (correctly) that overlap prevention is a creation concern, not a close/reopen concern.

**Files:**
- `Specs/Behavioral/FiscalPeriodManagement.feature` — new file

**Scenarios:**

1. **FPM-001: Creating a period that overlaps an existing period is rejected** — Create period A (2026-01-01 to 2026-01-31), then attempt to create period B (2026-01-15 to 2026-02-15). Assert rejection. Assert error message contains period A's key.

2. **FPM-002: Creating a period with identical dates to an existing period is rejected** — Create period A, attempt to create period B with the exact same start/end dates but different key. Assert rejection.

3. **FPM-003: Creating adjacent (non-overlapping) periods succeeds** — Create period A (2026-01-01 to 2026-01-31), then create period B (2026-02-01 to 2026-02-28). Assert both succeed. This is the negative test proving the guard isn't over-aggressive.

**Verification:** Scenarios are parseable Gherkin and map directly to AC-1, AC-2, AC-3.

## Acceptance Criteria

- [x] AC-1: Creating a fiscal period whose date range overlaps an existing period is rejected at the service layer → Phase 2
- [x] AC-2: A Gherkin scenario exercises the overlap rejection → Phase 4 (FPM-001, FPM-002)
- [x] AC-3: The error message identifies the conflicting period → Phase 2 (error format includes key + dates)

## Risks

- **btree_gist extension:** Requires superuser or the extension to be allowlisted. In local dev (Docker Postgres) this is fine. For managed Postgres (e.g., RDS), `btree_gist` is in the default allowlist so no issue. If the extension can't be created in some environment, the migration will fail — but the service-layer check (Phase 2) is the primary guard. **Mitigation:** The migration uses `IF NOT EXISTS` so it's idempotent. If it fails in an environment, the service-layer check still provides full protection.
- **Existing overlapping data:** If any seeded or manually-created periods already overlap, the exclusion constraint will fail to apply. **Mitigation:** Check the seed data in `1712000005000_SeedFiscalPeriods.sql` — these are monthly periods with non-overlapping ranges, so this is safe.

## Out of Scope

- Modifying `findByDate` to handle ambiguous results (overlap prevention eliminates the ambiguity).
- Overlap detection for period *updates* (no update API exists today).
- CLI-level messaging (the CLI already surfaces service-layer errors).
