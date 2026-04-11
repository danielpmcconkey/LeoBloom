# Procedure: Run Migration Against Prod

Applies pending Migrondi migrations to `leobloom_prod`. Three-stage
pipeline — review, approve, execute — enforced by a Claude Code guard
hook that blocks direct `dotnet build/run/test` on the Migrations
project when `LEOBLOOM_ENV=Production`.

## Prerequisites

- `LEOBLOOM_ENV=Production` set in shell (Dan's `.bashrc`)
- `LEOBLOOM_DB_PASSWORD` set in shell
- Scripts: `~/penthouse-pete/leobloom-ops/`

## Steps

### 1. Review

```bash
~/penthouse-pete/leobloom-ops/review-migration.sh
```

Queries `migrondi.__migrondi_migrations` on prod, diffs against migration
files on disk, displays pending SQL with SHA-256 hashes.

**Read every statement.** Classify as schema-only, seed data, data
mutation, or destructive. Check for real names, hardcoded IDs, missing
idempotency guards. See the review protocol in the blueprint if
uncertain.

### 2. Approve

```bash
~/penthouse-pete/leobloom-ops/approve-migration.sh
```

Writes `migration-approval.json` with the combined hash. Only one
approval artifact can exist at a time.

### 3. Execute

```bash
~/penthouse-pete/leobloom-ops/execute-migration.sh
```

Re-computes hashes, compares against the approval artifact, and runs
Migrondi if they match. Deletes the artifact on success.

### 4. Verify

Connect to prod and confirm:
- New tables/columns exist with correct types
- Existing data is unmodified (spot-check row counts)
- Migration journal count is correct

```bash
psql "host=localhost port=5432 dbname=leobloom_prod user=leobloom_hobson password=$LEOBLOOM_DB_PASSWORD" -c "\d ledger.account"
```

## Guard hook

`~/.claude/hooks/guard.sh` blocks any `dotnet build/run/test` on
`LeoBloom.Migrations` when `LEOBLOOM_ENV=Production` unless the command
is routed through `execute-migration.sh`.

## Note on execute-migration.sh

The execute script currently uses `dotnet run --project` (Debug build)
rather than the pre-built Release binary. Functionally equivalent but
slower. Could be tightened to use the Release binary if desired.
