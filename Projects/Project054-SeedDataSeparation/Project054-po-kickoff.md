# Project 054 -- Seed Data Separation

**PO Kickoff**
**Date:** 2026-04-06
**Status:** In Progress
**Epic:** Foundation Cleanup
**Depends On:** P053 (Fix Pre-Existing Test Failures) -- Done
**Blocks:** Any future migration that touches baseline data

---

## Project Summary

Seed data (chart of accounts, fiscal periods, obligation agreement reference
rows if any exist) is embedded in the Migrondi migration chain. Dev and prod
get identical data, which is wrong -- prod has real account names and a
different COA structure that Hobson maintains directly. Migration 021
(AddAccountSubType) is the poster child: its UPDATE statements target
anonymized dev account codes that don't map to the real prod COA.

This is a class of bug that gets worse over time. Every new migration that
INSERTs or UPDATEs baseline data makes the divergence between dev and prod
harder to manage. We fix the architecture now, before more features go on
top.

Full rationale and design direction:
`BdsNotes/seed-data-vision-2026-04-05.md` (Hobson's vision doc, source of
truth for this project).

---

## What Gets Built

1. **Seeds/dev/ directory** under `Src/LeoBloom.Migrations/` containing
   idempotent SQL scripts that represent the current desired dev baseline.
   These are not tracked by Migrondi.

2. **Seed runner** -- a mechanism (shell script, migration binary subcommand,
   or similar) that executes all seed scripts in a given environment
   directory, in order. The vision doc explicitly says don't over-engineer
   this.

3. **Extracted baseline data** -- the current dev seed data from migrations
   005 (fiscal periods), 006 (chart of accounts), and the UPDATE statements
   from 021 (account subtypes) consolidated into seed scripts that include
   all columns as they exist today.

4. **Seeds README** -- documents what seeds exist, how to run them, and the
   migration hygiene rule going forward.

---

## What Does NOT Change

- **Existing migrations stay untouched.** They've already run in both
  environments. They're Migrondi history. We don't modify or remove them.
- **Tests are unaffected.** Tests use InsertHelpers and clean up after
  themselves. They don't depend on seed data. (P053 just fixed the last
  case where tests were accidentally colliding with seed data.)
- **Reference data stays in migrations.** Account types with normal
  balances are structural -- the ledger engine requires them. Those
  remain in migrations.
- **No prod seed scripts in the repo.** Dan's real financial data never
  touches git. Hobson manages prod data directly.

---

## Acceptance Criteria

### Behavioral Criteria (Gherkin-eligible)

| # | Criterion |
|---|-----------|
| B1 | Seed runner executes all .sql files in Seeds/dev/ in alphabetical/numeric order |
| B2 | Seed scripts are idempotent -- running them twice on the same database produces the same result with no errors |
| B3 | Running migrations on a fresh database followed by seed runner produces a working dev baseline (accounts, fiscal periods, subtypes all present and correct) |
| B4 | Seed runner exits with a clear error when pointed at a nonexistent environment directory |

### Structural Criteria (verified by QE/Governor, not Gherkin)

| # | Criterion |
|---|-----------|
| S1 | `Src/LeoBloom.Migrations/Seeds/dev/` directory exists with seed scripts |
| S2 | Seed scripts cover all baseline data currently in migrations 005, 006, and the UPDATE statements from 021 |
| S3 | Seed scripts include all columns as they exist in the current schema (including account_subtype) |
| S4 | Seeds/README.md documents usage, the migration hygiene rule, and the fresh-database workflow |
| S5 | No new migrations contain environment-specific INSERT/UPDATE statements |
| S6 | Full test suite continues to pass -- zero failures, zero skips |
| S7 | Migration 006 already includes account_subtype column values, so the seed script should represent the consolidated current state, not replay the migration history |

---

## Design Guidance (for the Planner)

The vision doc (`BdsNotes/seed-data-vision-2026-04-05.md`) is the
authoritative reference for design decisions. Key points the Planner should
internalize:

1. **Seed scripts are plain SQL**, not F# code. They run via psql or
   equivalent, not through the .NET build.

2. **Idempotency via upsert or delete+insert within a transaction.** The
   vision doc lists both patterns. The Planner picks one and sticks with it.

3. **Script ordering by numeric prefix** (010, 020, 030...) with gaps for
   future inserts. Same convention as the migration timestamps but simpler.

4. **The seed runner can be dead simple.** A shell script that iterates
   over .sql files in order and runs them via psql is perfectly acceptable.
   Don't build a .NET CLI subcommand unless there's a compelling reason.

5. **Migration 021's UPDATE statements are the grey area example.** The
   ALTER TABLE stays in the migration. The UPDATEs targeting seed accounts
   belong in the seed script. But since migration 021 has already run, the
   seed script just needs to represent the current desired state (INSERT
   with account_subtype included), not replay the UPDATE history.

6. **Migration 006 was retrofitted.** Looking at the actual SQL, migration
   006 already includes the account_subtype column in its INSERT statements.
   This happened because P052 rewrote it. The seed script should match this
   current state.

---

## Out of Scope

- Modifying or removing existing migration files
- Any prod-side seed scripts or prod data management
- Changing test infrastructure or InsertHelpers
- New accounting features or domain logic
- Refactoring the migration runner (Migrondi) itself
- COA restructuring -- the seed script reproduces the current dev COA as-is

---

## Risks

1. **Seed/migration ordering on fresh DB.** If a seed script references a
   table or column that hasn't been created yet by migrations, it fails.
   The Planner must verify that seeds run after all migrations complete.

2. **FK ordering within seed scripts.** The chart of accounts has
   parent_code foreign keys. Seed inserts must respect parent-first
   ordering (migration 006 already does this). The seed script must
   preserve that discipline.

3. **Migrondi's tracking.** Seeds must genuinely be outside Migrondi's
   purview. If Migrondi scans the Seeds/ directory and tries to run those
   files, that's a problem. The Planner should verify Migrondi's file
   discovery pattern.

4. **Fresh DB bootstrap.** The workflow becomes: run migrations, then run
   seeds. If someone runs only migrations, they get a database with schema
   and reference data but no accounts or fiscal periods. This is correct
   for prod (Hobson loads his own data), but the README must make the
   two-step process crystal clear for dev.

---

## Backlog Status Update

P054 status changed from "Not started" to **In Progress** as of 2026-04-06.
