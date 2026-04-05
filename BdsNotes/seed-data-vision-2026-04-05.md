# Seed Data Separation — Vision

**From:** Hobson (PO)
**Date:** 2026-04-05

---

## The Problem

Seed data lives inside the migration chain. Migrations 002, 005, 006,
011–014, and 021 all INSERT or UPDATE baseline data alongside schema
changes. This has three consequences:

1. **Dev and prod get identical data.** The anonymized COA ("Bank A —
   Operating") lands in prod. Hobson then manually overwrites it with
   real account names and numbers. Every migration that touches account
   rows risks undoing that work.

2. **Migration 021 (AddAccountSubType) demonstrates the fragility.**
   It updates subtypes on account codes like 5350, 5400, 5450 — which
   are leaf accounts in the anonymized seed data but grouping headers
   in the real COA. The migration is correct for dev, wrong for prod.

3. **Future COA changes are painful.** Adding new accounts, renaming
   existing ones, adjusting subtypes — these are data operations, not
   schema changes. But the only tool available is a migration, which
   means every data change gets a numbered, ordered, irreversible
   migration file. That's the wrong shape.

---

## Guiding Principles

**Schema migrations are for structure.** DDL only: CREATE TABLE, ALTER
TABLE, ADD COLUMN, DROP COLUMN, CREATE INDEX. They define what the
database *can* hold. They run everywhere, always, in order.

**Reference data is structural.** The five account types with their
normal balances are not optional configuration — the ledger engine
cannot function without them. These stay in migrations. Same for any
future data that the code assumes exists (e.g., if a DU case maps to a
row that must be present).

**Baseline data is environment-specific.** The chart of accounts,
fiscal periods, obligation agreements, and any future operational data
are different in dev vs prod. Dev gets anonymized sample data for
testing. Prod gets real data managed by Hobson. These must not be in
the migration chain.

---

## Proposed Approach

### 1. Seed Scripts (not migrations)

Create a `Seeds/` directory alongside `Migrations/` in the migrations
project. Seed scripts are plain SQL files, but they are **not** tracked
by Migrondi. They are run manually or by a CLI command.

```
Src/LeoBloom.Migrations/
  Migrations/          ← Migrondi-managed, schema + reference data only
  Seeds/
    dev/               ← Anonymized baseline for dev/test
      010-chart-of-accounts.sql
      020-fiscal-periods.sql
      030-obligation-agreements.sql
    README.md          ← Documents what seeds exist and how to run them
```

Prod has no seed scripts in the repo. Hobson manages prod data via
direct SQL or a future CLI account management command.

### 2. Seeds Are Idempotent

Every seed script must be safe to run repeatedly. Use
`INSERT ... ON CONFLICT DO UPDATE` (upsert) or `DELETE + INSERT` with
a transaction. This means re-running seeds after a schema migration
that adds a column (like account_subtype) just works — the seed script
includes the new column.

### 3. Seed Runner

Add a command to the migration binary (or a separate script) that runs
all seed scripts in a given directory, in order. Something like:

```
LeoBloom.Migrations seed --env dev
LeoBloom.Migrations seed --env prod   ← errors: no prod seeds in repo
```

Or simpler — just a shell script that runs each .sql file in the
target directory via psql. Don't over-engineer this. The seeds run
rarely and manually.

### 4. Retrofit Existing Migrations

The existing seed migrations (005, 006, 011–014) have already run in
both dev and prod. They can't be removed from the migration chain —
Migrondi tracks them by timestamp. But they should be considered
**dead code going forward**. The approach:

- Leave the existing seed migrations in place. They've run. They're
  history.
- Create seed scripts in `Seeds/dev/` that represent the **current**
  desired dev baseline, including all columns added by later migrations
  (like account_subtype).
- Any future baseline data changes go in seed scripts, not migrations.
- Document in the Seeds README that after running migrations on a fresh
  database, you run `seed --env dev` to get a working dev baseline.

### 5. Test Data Is Unaffected

Tests already create their own data via InsertHelpers and clean up
after themselves. They don't depend on seed data. This is correct and
should not change. The seed scripts are for **human-usable dev
environments**, not test fixtures.

---

## What Changes for Each Environment

| Environment | Schema Migrations | Reference Data | Baseline Data |
|---|---|---|---|
| **Dev/Test DB** | Migrondi (all) | In migrations (account_type) | Seed scripts (`Seeds/dev/`) |
| **Prod DB** | Migrondi (all) | In migrations (account_type) | Hobson manages directly |
| **CI (if ever)** | Migrondi (all) | In migrations (account_type) | Seed scripts or test-only inserts |

---

## Migration Hygiene Going Forward

When writing a new migration, ask: "Does this INSERT or UPDATE rows
that differ between dev and prod?"

- **No** (e.g., adding a column, creating a table, adding an index) →
  migration.
- **Yes** (e.g., adding accounts, changing subtypes on existing
  accounts, seeding fiscal periods) → seed script.
- **Grey area** (e.g., a migration adds a column and needs to backfill
  it on existing rows) → the migration does the ALTER. A seed script
  update handles the dev backfill. Hobson handles the prod backfill
  separately.

Migration 021 (AddAccountSubType) is a good example of the grey area
done wrong. The ALTER TABLE belongs in a migration. The UPDATE
statements setting subtypes on seed accounts belong in a seed script.

---

## Scope

This is infrastructure work. It unblocks clean COA management and
prevents a growing class of "works in dev, breaks prod" bugs. It
should be done before any more migrations that touch baseline data.

Suggested backlog:
- Extract current seed data into `Seeds/dev/` scripts (idempotent)
- Add a seed runner (shell script or migration binary subcommand)
- Update the dev setup docs
- Update migration 021's UPDATE statements to a seed script (the ALTER
  stays)

The prod COA, fiscal periods, and obligation agreements remain
Hobson's responsibility. No seed scripts for prod go in the repo.
Dan's real financial data never touches git.
