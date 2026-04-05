# Project 044 -- Plan: Database Indexes Migration

## Objective

Add six secondary indexes on frequently joined/filtered FK columns across
`ledger.journal_entry_line`, `ledger.journal_entry`, and
`ops.obligation_instance`. One SQL migration file, zero app code changes.
This prevents query degradation as data volume scales past 10x current size.

## Research Decision

Strong local context, skipping external research. The migration pattern is
well established (19 prior Migrondi SQL files), the SQL is textbook DDL, and
the backlog item specifies the exact index definitions.

## Single Phase: Create Migration File

**What:** One new Migrondi SQL migration file containing six
`CREATE INDEX IF NOT EXISTS` statements (UP) and six `DROP INDEX IF EXISTS`
statements (DOWN).

**File created:**

```
Src/LeoBloom.Migrations/Migrations/1712000020000_AddSecondaryIndexes.sql
```

**fsproj update:** Not needed. The existing glob
(`<None Include="Migrations/**/*.sql" CopyToOutputDirectory="PreserveNewest" />`)
picks up new SQL files automatically.

**File contents:**

```sql
-- MIGRONDI:NAME=1712000020000_AddSecondaryIndexes.sql
-- MIGRONDI:TIMESTAMP=1712000020000
-- ---------- MIGRONDI:UP ----------

CREATE INDEX IF NOT EXISTS idx_jel_journal_entry_id
    ON ledger.journal_entry_line (journal_entry_id);

CREATE INDEX IF NOT EXISTS idx_jel_account_id
    ON ledger.journal_entry_line (account_id);

CREATE INDEX IF NOT EXISTS idx_je_fiscal_period_id
    ON ledger.journal_entry (fiscal_period_id);

CREATE INDEX IF NOT EXISTS idx_je_entry_date
    ON ledger.journal_entry (entry_date);

CREATE INDEX IF NOT EXISTS idx_je_voided_null
    ON ledger.journal_entry (id) WHERE voided_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_oi_agreement_id
    ON ops.obligation_instance (obligation_agreement_id);

-- ---------- MIGRONDI:DOWN ----------

DROP INDEX IF EXISTS ledger.idx_jel_journal_entry_id;
DROP INDEX IF EXISTS ledger.idx_jel_account_id;
DROP INDEX IF EXISTS ledger.idx_je_fiscal_period_id;
DROP INDEX IF EXISTS ledger.idx_je_entry_date;
DROP INDEX IF EXISTS ledger.idx_je_voided_null;
DROP INDEX IF EXISTS ops.idx_oi_agreement_id;
```

**Notes on the SQL:**

- `IF NOT EXISTS` / `IF EXISTS` makes UP and DOWN idempotent.
- Schema qualification on DROP is required because PostgreSQL creates indexes
  in the same schema as the table. `idx_jel_*` and `idx_je_*` live in
  `ledger`, `idx_oi_*` lives in `ops`.
- The partial index `idx_je_voided_null` filters on `WHERE voided_at IS NULL`
  to support the void-exclusion predicate that every report query uses.
- No explicit schema prefix on CREATE because the `ON schema.table` clause
  already places the index in the correct schema.

## Verification

1. **Build:** `dotnet build` from solution root -- confirms the .sql file is
   copied to output.
2. **Run migration:** Execute `LeoBloom.Migrations` against `leobloom_dev`.
   Expect output showing `Applied: 1712000020000_AddSecondaryIndexes.sql`.
3. **Confirm indexes exist:** Run against `leobloom_dev`:
   ```sql
   SELECT indexname, tablename, indexdef
   FROM pg_indexes
   WHERE indexname IN (
       'idx_jel_journal_entry_id',
       'idx_jel_account_id',
       'idx_je_fiscal_period_id',
       'idx_je_entry_date',
       'idx_je_voided_null',
       'idx_oi_agreement_id'
   )
   ORDER BY indexname;
   ```
   Expect exactly 6 rows. Verify the partial index row includes
   `WHERE (voided_at IS NULL)`.
4. **Idempotency:** Run `LeoBloom.Migrations` again. Expect "No pending
   migrations." with zero errors.
5. **Full test suite:** `dotnet test` from solution root. All existing BDD
   tests pass (zero failures, zero new skips).

## Acceptance Criteria

- [ ] File `Src/LeoBloom.Migrations/Migrations/1712000020000_AddSecondaryIndexes.sql` exists and follows Migrondi format
- [ ] Migration applies cleanly against `leobloom_dev` with no errors
- [ ] All 6 indexes confirmed present via `pg_indexes` query
- [ ] Partial index `idx_je_voided_null` includes `WHERE (voided_at IS NULL)` filter
- [ ] Running migration a second time produces no errors (idempotent)
- [ ] `dotnet test` passes with zero failures
- [ ] No files outside `Src/LeoBloom.Migrations/Migrations/` are created or modified

## Risks

- **Migrondi DOWN section format.** The previous 19 migrations do not include
  DOWN sections (the `EliminateLookupTables` migration is the only one with
  a DOWN block). If the Builder sees that Migrondi chokes on an empty or
  minimal DOWN, they can omit it entirely -- but the preferred approach is
  to include it for reversibility. The UP section is what matters.

## Out of Scope

- Query rewrites or EXPLAIN ANALYZE benchmarking
- Composite indexes
- Any F# application code changes
- Gherkin scenarios (no behavioral change to specify)

---

## PO Gate 1 Approval

**Verdict:** APPROVED
**Date:** 2026-04-05
**Reviewer:** PO Agent

All checklist items pass. The plan is consistent with the PO brief and
backlog item 044. Six indexes match exactly, DDL is correct including the
partial index WHERE clause, naming convention follows the established
sequence, and scope boundaries are properly maintained. The inclusion of
a DOWN section with schema-qualified DROP statements and the associated
risk note about prior migration format is a useful addition. No behavioral
Gherkin scenarios are needed -- structural verification by QE is sufficient
for a migration-only project. Proceed to build.
