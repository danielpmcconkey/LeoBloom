# P054 — Seed Data Separation

**Priority:** HIGH
**Status:** Not started
**Depends on:** P053 (fix pre-existing test failures)
**Blocks:** Any future migration that touches baseline data

---

## Problem

Seed data (chart of accounts, fiscal periods, obligation agreements) is
baked into the Migrondi migration chain alongside schema DDL. Dev and prod
get identical data, which is wrong — prod has real account names/numbers
that Hobson maintains directly. Migration 021 (P052's AddAccountSubType)
is the clearest example: its UPDATE statements target anonymized seed
accounts, but prod has a different COA structure entirely.

This is a growing class of "works in dev, breaks prod" bugs. Every new
migration that INSERTs or UPDATEs baseline data makes it worse.

## Solution Approach

1. **Seeds/ directory** — `Src/LeoBloom.Migrations/Seeds/dev/` for
   idempotent SQL scripts that represent the current desired dev baseline.
   No prod seeds in the repo.

2. **Idempotent scripts** — upsert or delete+insert within a transaction.
   Safe to re-run after schema changes that add columns.

3. **Seed runner** — CLI command or shell script to execute seed files in
   order. Keep it simple.

4. **Retrofit existing migrations** — leave old seed migrations (005, 006,
   011-014, 021) in place (they've run, they're history). Extract current
   baseline into seed scripts that include all columns from later
   migrations.

5. **Migration hygiene rule** — going forward, if a migration INSERTs or
   UPDATEs rows that differ between dev and prod, that data goes in a
   seed script, not a migration.

## What This Does NOT Change

- **Tests are unaffected.** Tests use InsertHelpers and clean up after
  themselves. They do not depend on seed data. This is correct and must
  stay that way.
- **Reference data stays in migrations.** The five account types with
  normal balances are structural — the ledger engine requires them. Those
  remain in migrations.
- **Existing migrations are untouched.** They've already run. They're
  history tracked by Migrondi.

## Acceptance Criteria (preliminary)

Behavioral:
- Seed runner executes all scripts in Seeds/dev/ in order
- Seed scripts are idempotent (re-running produces same result)
- Running migrations on a fresh DB + seeds produces a working dev baseline
- Seed runner errors cleanly when pointed at a nonexistent environment

Structural:
- Seeds/dev/ directory exists with extracted baseline scripts
- No new migrations contain environment-specific INSERT/UPDATE statements
- Seeds README documents usage

## Authoritative Reference

`BdsNotes/seed-data-vision-2026-04-05.md` — Hobson's full vision doc.
The brief above is a summary. The vision doc is the source of truth for
design decisions and rationale.
