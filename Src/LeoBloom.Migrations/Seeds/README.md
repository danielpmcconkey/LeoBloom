# Seeds

Idempotent SQL scripts that populate environment-specific baseline data
(chart of accounts, fiscal periods) outside the Migrondi migration chain.

See the P054 vision doc for the full rationale behind separating seed data
from schema migrations.

## Why seeds exist

Migrations define schema and environment-agnostic structure. Seeds define
baseline **data** that differs between environments. Dev needs a sample chart
of accounts and fiscal periods; prod does not -- Hobson manages prod data
directly.

Keeping this data in migrations caused a "works in dev, breaks prod" bug
class. Seeds eliminate that by giving each environment its own data scripts.

## Usage

```bash
./run-seeds.sh dev
```

The runner executes every `.sql` file in `Seeds/dev/` in sorted
(numeric-prefix) order via `psql`.

### Prerequisites

- `psql` must be available in your PATH
- The `LEOBLOOM_DB_PASSWORD` environment variable must be set

Optional overrides (with defaults):

| Variable | Default |
|---|---|
| `LEOBLOOM_DB_HOST` | `172.18.0.1` |
| `LEOBLOOM_DB_PORT` | `5432` |
| `LEOBLOOM_DB_NAME` | `leobloom_dev` |
| `LEOBLOOM_DB_USER` | `claude` |

## Fresh database workflow

1. Run migrations: `dotnet run -- migrondi up`
2. Run seeds: `./Seeds/run-seeds.sh dev`

Migrations create the schema. Seeds populate the baseline data. Always run
migrations first.

## Migration hygiene decision tree

When adding a new INSERT or UPDATE to the codebase, ask:

```
Does this data differ between dev and prod?
  YES --> Seed script (Seeds/{env}/)
  NO  --> Migration (Migrations/)

Does this change the schema (CREATE TABLE, ALTER, etc.)?
  YES --> Migration (always)
```

Schema changes are always migrations. Data that varies by environment is
always a seed. Data that is identical everywhere (e.g., enum-like lookup
rows that the code depends on) belongs in a migration.

## Conventions

- **Numeric prefix ordering**: Files use gaps of 10 (010, 020, 030...) to
  allow future insertions without renaming.
- **Upsert pattern**: Every seed uses `INSERT ... ON CONFLICT DO UPDATE` so
  re-running is safe and idempotent.
- **Transaction wrapping**: Each seed file is wrapped in `BEGIN`/`COMMIT`
  for atomicity. If any row fails, the whole script rolls back.
- **Parent-first ordering**: The chart of accounts seed inserts parent
  accounts before children to satisfy the `parent_id` foreign key.

## Prod

There are no prod seed scripts. Prod data is managed by Hobson directly.
This is intentional -- prod baseline data is not a developer concern.
