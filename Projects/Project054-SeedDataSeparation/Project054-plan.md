# Project 054 — Seed Data Separation: Plan

## Objective

Extract environment-specific baseline data (chart of accounts, fiscal periods)
from the Migrondi migration chain into idempotent SQL seed scripts under a new
`Seeds/dev/` directory, add a minimal seed runner shell script, and document the
two-step fresh-DB workflow. This stops the "works in dev, breaks prod" bug class
at its root without touching any existing migrations.

---

## Research Notes

**Strong local context, skipped external research.** Everything needed is in
the codebase:

- **Migrondi file discovery** is safe. `Program.fs` sets
  `migrations = migrationsDir` where `migrationsDir` resolves to
  `{AppContext.BaseDirectory}/Migrations`. Seeds placed in a sibling `Seeds/`
  directory are completely outside Migrondi's scan path. No risk of accidental
  execution.

- **Migration 006 already contains the consolidated state.** P052 rewrote it
  to include `account_subtype` in the INSERT statements. The seed script can
  lift this data directly -- no need to replay migration 021's UPDATE history.

- **Migration 005 seeds fiscal periods.** Straightforward extraction -- three
  years of monthly periods (2026-2028).

- **Migrations 011-014 seeded lookup tables** (obligation_type, cadence,
  payment_method, obligation_status) that were **eliminated entirely** by
  migration 019 (EliminateLookupTables). Those tables no longer exist. There is
  nothing to extract from 011-014.

- **No obligation_agreement seed data** exists in any migration. The vision doc
  mentioned it hypothetically; no seed script needed for it.

- **Account table has a self-referential FK** (`parent_code` references
  `account.code`). Migration 006 already inserts in parent-first order. The
  seed script must preserve this discipline.

- **Connection string** uses `Host=172.18.0.1;Port=5432;Database=leobloom_dev`
  with password from `LEOBLOOM_DB_PASSWORD` env var. The seed runner shell
  script needs the same connection info via psql.

---

## Phases

### Phase 1: Directory Structure and Seed Scripts

**What:** Create the `Seeds/dev/` directory and two idempotent SQL seed scripts
that represent the current desired dev baseline.

**Files created:**
- `Src/LeoBloom.Migrations/Seeds/dev/010-fiscal-periods.sql`
- `Src/LeoBloom.Migrations/Seeds/dev/020-chart-of-accounts.sql`

**Design decisions:**

1. **Idempotency pattern: upsert (`INSERT ... ON CONFLICT DO UPDATE`).** This
   is cleaner than delete+insert because it preserves `id` serial values and
   `created_at` timestamps on existing rows. It also avoids FK violation issues
   during delete (journal_entry_line references account, so deleting accounts
   would fail if any transactions exist).

2. **Fiscal periods upsert** on `period_key` (unique). Updates `start_date`,
   `end_date` on conflict. Covers 2026-01 through 2028-12, matching migration
   005.

3. **Chart of accounts upsert** on `code` (unique). Updates `name`,
   `account_type_id`, `parent_code`, `account_subtype` on conflict. Must
   insert in parent-first order (parents before children) because the FK on
   `parent_code` is checked on insert. The upsert handles re-runs where
   parents already exist.

4. **Each seed script is wrapped in a transaction** (`BEGIN; ... COMMIT;`) for
   atomicity. If any row fails, the whole script rolls back.

5. **Numeric prefix convention** uses gaps of 10 (010, 020, 030...) for future
   insertions. This matches the vision doc.

**Verification:** Seed SQL files parse without syntax errors (`psql -f` with
`--set ON_ERROR_STOP=on` returns 0).

---

### Phase 2: Seed Runner

**What:** A shell script that executes all `.sql` files in a given environment's
seed directory, in numeric/alphabetical order, via psql.

**Files created:**
- `Src/LeoBloom.Migrations/Seeds/run-seeds.sh`

**Behavior:**
- Takes one argument: environment name (e.g., `dev`).
- Validates the target directory exists (`Seeds/{env}/`). Exits with a clear
  error message and non-zero code if not found.
- Reads database connection from environment variables:
  - `LEOBLOOM_DB_HOST` (default: `172.18.0.1`)
  - `LEOBLOOM_DB_PORT` (default: `5432`)
  - `LEOBLOOM_DB_NAME` (default: `leobloom_dev`)
  - `LEOBLOOM_DB_USER` (default: `claude`)
  - `LEOBLOOM_DB_PASSWORD` (no default, required)
- Iterates over `.sql` files in sorted order (`ls -1 *.sql | sort`).
- Runs each file via `psql` with `ON_ERROR_STOP=on`. If any script fails,
  the runner stops and exits non-zero.
- Prints each script name before execution and a success/failure summary.

**Why shell, not F#:** The vision doc says "don't over-engineer this." A shell
script is transparent, has no build step, and psql is already available in the
dev environment. The migration binary stays focused on Migrondi.

**Verification:** `run-seeds.sh dev` runs both seed scripts against the dev
database without error. `run-seeds.sh prod` exits with a clear "directory not
found" error.

---

### Phase 3: Documentation

**What:** A README in the Seeds directory documenting usage, conventions, and
the migration hygiene rule.

**Files created:**
- `Src/LeoBloom.Migrations/Seeds/README.md`

**Content covers:**
1. What seed scripts are and why they exist (brief, links to vision doc)
2. How to run seeds (`./run-seeds.sh dev`)
3. Fresh database workflow: run migrations first, then seeds
4. The migration hygiene rule (decision tree from the vision doc: does this
   INSERT/UPDATE differ between dev and prod? yes = seed, no = migration)
5. Conventions: numeric prefix ordering, upsert pattern, transaction wrapping
6. Note that prod has no seed scripts -- Hobson manages prod data directly

**Verification:** README exists and contains the above sections.

---

## File Summary

| Action | Path |
|--------|------|
| Create | `Src/LeoBloom.Migrations/Seeds/dev/010-fiscal-periods.sql` |
| Create | `Src/LeoBloom.Migrations/Seeds/dev/020-chart-of-accounts.sql` |
| Create | `Src/LeoBloom.Migrations/Seeds/run-seeds.sh` |
| Create | `Src/LeoBloom.Migrations/Seeds/README.md` |

No files modified. No files deleted.

---

## Acceptance Criteria

### Behavioral (Gherkin-eligible)

- [ ] B1: `run-seeds.sh dev` executes `010-fiscal-periods.sql` then
  `020-chart-of-accounts.sql`, in that order
- [ ] B2: Running `run-seeds.sh dev` twice on the same database produces
  identical data with no errors (idempotency)
- [ ] B3: On a fresh database, running migrations followed by `run-seeds.sh dev`
  produces a working dev baseline: all 36 fiscal periods, all 68 accounts with
  correct subtypes
- [ ] B4: `run-seeds.sh prod` exits non-zero with a message indicating the
  directory does not exist
- [ ] B5: If a seed script contains a SQL error, the runner stops execution and
  exits non-zero (does not silently continue to the next script)

### Structural (verified by QE/Governor)

- [ ] S1: `Src/LeoBloom.Migrations/Seeds/dev/` directory exists with two seed
  scripts
- [ ] S2: Seed scripts cover all baseline data from migrations 005 and 006,
  including the `account_subtype` values from migration 021 (already present
  in 006's current state)
- [ ] S3: Chart of accounts seed inserts in parent-first order to satisfy FK
- [ ] S4: Each seed script uses `INSERT ... ON CONFLICT DO UPDATE` for
  idempotency
- [ ] S5: Each seed script is wrapped in a transaction (`BEGIN`/`COMMIT`)
- [ ] S6: `Seeds/README.md` documents usage, fresh-DB workflow, and migration
  hygiene rule
- [ ] S7: No existing migration files are modified or deleted
- [ ] S8: Full test suite passes with zero failures, zero skips
- [ ] S9: Seed scripts are NOT in Migrondi's scan path (verified: `Program.fs`
  sets `migrations` to `Migrations/` directory only)

---

## Risks

1. **psql availability.** The seed runner depends on `psql` being installed in
   the execution environment. It is present in the Docker container. If someone
   tries to run seeds from a host that lacks psql, they get a clear "command
   not found" error. **Mitigation:** README documents the psql dependency.

2. **Parent-first ordering in upsert context.** On a fresh DB (no existing
   accounts), the upsert degrades to plain INSERT, so parent-first order is
   mandatory. On a re-run where parents exist, the order is technically
   irrelevant but we maintain it for safety and readability. **Mitigation:**
   Copy the exact ordering from migration 006, which is already correct.

3. **Account code collisions with test data.** Tests use InsertHelpers with
   their own account codes and clean up after themselves. Seeds use the
   anonymized COA codes (1000-9010). These should not collide. **Mitigation:**
   P053 already fixed the last case of test/seed collision. The test suite
   passing (S8) confirms no regression.

4. **Serial ID values.** The upsert pattern preserves existing `id` values on
   re-runs (ON CONFLICT updates non-key columns, doesn't re-insert). First
   runs will allocate new serial values. This is fine -- nothing in the
   codebase references accounts by `id`; everything uses `code`.

---

## Out of Scope

- Modifying or removing existing migration files
- Prod seed scripts or prod data management
- Changing test infrastructure or InsertHelpers
- Obligation agreement seed data (none exists in migrations)
- Refactoring Migrondi or the migration runner (`Program.fs`)
- COA restructuring (seeds reproduce the current dev COA as-is)
- Adding a seed subcommand to the .NET migration binary
