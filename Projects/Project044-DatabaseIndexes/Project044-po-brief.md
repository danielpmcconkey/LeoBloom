# Project 044 -- PO Brief: Database Indexes Migration

**Product Owner:** PO Agent
**Date:** 2026-04-05
**Backlog item:** 044-database-indexes.md
**Source:** Code audit SYNTHESIS.md, Tier 1 Finding #2

---

## Problem Statement

PostgreSQL does not automatically index foreign key columns. Every report
query in LeoBloom -- trial balance, income statement, balance sheet, account
balance, subtree P&L -- joins `journal_entry_line` on `journal_entry_id` and
`account_id`, neither of which is indexed. Same story for
`journal_entry.fiscal_period_id`, `journal_entry.entry_date`, and the
obligation instance FK columns.

At current scale (~200 entries, ~600 lines) this is invisible. At 10x it
gets sluggish. At 100x it breaks. Two of six independent code audit reviewers
flagged this. The audit rates it low effort, high impact, and recommends it
ship before any new feature work.

Fix: one migration file, six indexes, zero app code changes.

---

## Acceptance Criteria

### Behavioral (Gherkin scenarios)

For a migration-only project with no app code changes, there are no
behavioral scenarios to specify. The observable behavior of the application
does not change -- queries return the same results, just faster at scale.

### Structural (verified by QE/Governor)

| ID | Criterion | Verification |
|----|-----------|--------------|
| AC-S1 | Migration file exists | File `1712000020000_AddSecondaryIndexes.sql` exists in `Src/LeoBloom.Migrations/Migrations/` following the established naming convention. |
| AC-S2 | Six indexes created | After migration runs, all six indexes exist in the database: `idx_jel_journal_entry_id`, `idx_jel_account_id`, `idx_je_fiscal_period_id`, `idx_je_entry_date`, `idx_je_voided_null`, `idx_oi_agreement_id`. |
| AC-S3 | Idempotent | Migration uses `CREATE INDEX IF NOT EXISTS` for all six statements. Running the migration twice produces no errors. |
| AC-S4 | Correct tables and columns | Each index targets the correct schema.table(column) as specified in the backlog item. The partial index on `journal_entry(id) WHERE voided_at IS NULL` has the correct filter. |
| AC-S5 | No app code changes | No files outside the migration directory are added or modified. |
| AC-S6 | Existing tests pass | Full BDD test suite passes with no failures or skips after migration is applied. |

---

## Scope Boundaries

### In scope

- One SQL migration file with six `CREATE INDEX IF NOT EXISTS` statements
- Migration runs cleanly against `leobloom_dev`
- Verification that indexes exist post-migration

### Explicitly out of scope

- **No query optimization.** Queries are not being rewritten to take advantage of the indexes. The planner handles that automatically.
- **No app code changes.** Zero F# files touched.
- **No EXPLAIN ANALYZE benchmarking.** We are not measuring performance improvement. The indexes are correct by definition for the join patterns in the codebase.
- **No composite indexes.** The backlog item specifies six single-column indexes (plus one partial). If composite indexes would help specific queries, that is a future finding.
- **No Gherkin scenarios.** This project has no observable behavioral changes. Structural verification by QE is sufficient.

---

## Risk Notes

1. **Migration must run cleanly against `leobloom_dev`.** The Builder runs it in dev. Hobson runs it in prod later. If the migration fails in dev, it blocks.

2. **Naming convention.** The last migration is `1712000019000_EliminateLookupTables.sql`. The new file should be `1712000020000_AddSecondaryIndexes.sql` to maintain the sequence.

3. **Schema qualification.** The index DDL must use schema-qualified table names (`ledger.journal_entry_line`, `ledger.journal_entry`, `ops.obligation_instance`) to match the existing migration convention.

4. **Partial index syntax.** The `idx_je_voided_null` index is a partial index: `CREATE INDEX IF NOT EXISTS idx_je_voided_null ON ledger.journal_entry (id) WHERE voided_at IS NULL`. The `WHERE` clause must be present -- without it, the index is useless for the void-filtering queries that every report runs.

---

## Verdict

This is a straightforward, low-risk project. One file, six DDL statements, no app code. Ready for the Planner, though honestly this is simple enough that the Planner's job is mainly to not overthink it.
