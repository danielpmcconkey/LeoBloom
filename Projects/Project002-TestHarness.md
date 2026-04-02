# Project 2 — Test Harness & Configuration

## Objective

Stand up the executable test infrastructure for Leo Bloom. When this project
is done, the 68+ Gherkin structural constraint scenarios from Project 1 run
automatically against `leobloom_dev`, and the shared configuration strategy
is in place for all projects.

---

## Deliverables

### 1. Shared Connection String Resolver

Already scaffolded in `LeoBloom.Dal.ConnectionString`. Needs:

- **`LEOBLOOM_ENV`** environment variable drives everything. `Production` on
  the host, `Development` in BD's container.
- Each project that needs a database connection includes its own
  `appsettings.{env}.json` (connection string template, no secrets).
- `ConnectionString.resolve` reads the env var, loads the matching config,
  substitutes `LEOBLOOM_DB_PASSWORD` from the environment, returns a
  ready-to-use connection string.
- Migrations project (`LeoBloom.Migrations`) should be refactored to use
  `ConnectionString.resolve` from Dal instead of its own inline config
  loading. One resolver, one way to get a connection string.

### 2. Executable Gherkin Tests (TickSpec + xUnit)

- **`LeoBloom.Dal.Tests`** — structural constraint tests. These are the
  Gherkin scenarios from `Specs/Acceptance/`.
- **Test framework:** TickSpec (F# Gherkin runner) + xUnit.
- **Target database:** `leobloom_dev` via `ConnectionString.resolve`.
- Each `.feature` file gets a corresponding step definition file.
- Tests connect to the real database, attempt inserts that should
  succeed or fail, and assert the expected outcome (NOT NULL violation,
  FK violation, UNIQUE violation, or success).
- Tests must clean up after themselves — no test data left behind.
  Use transactions that roll back, or explicit DELETE in teardown.

**Feature files (already written):**
- `Specs/Acceptance/LedgerStructuralConstraints.feature`
- `Specs/Acceptance/OpsStructuralConstraints.feature`

Step definitions go in `Src/LeoBloom.Dal.Tests/`.

### 3. Prod Safety Gate

The `claude` Postgres role has no permissions on `leobloom_prod`. This is
the primary gate — BD physically cannot run anything against prod regardless
of configuration.

Additionally:
- `LEOBLOOM_ENV` defaults to `Development` if unset. Prod is opt-in, never
  accidental.
- Migrations against prod are a manual Hobson operation. BD writes migration
  SQL, Hobson reviews and runs it.
- **Future consideration:** if prod-specific seed data (like the real COA)
  diverges from dev, we may need a migration tagging strategy to mark
  migrations as "dev-only" or "shared." Not needed yet — flag it when it
  becomes a problem.

### 4. `LeoBloom.Domain.Tests` Scaffolding

Not populated in this project — business logic tests come in Project 3 when
domain types are defined. But the project exists and builds, ready for
TickSpec + xUnit when needed.

---

## Project Structure After Completion

```
LeoBloom/
├── LeoBloom.sln
├── Specs/
│   ├── DataModelSpec.md
│   ├── SampleCOA.md
│   └── Acceptance/
│       ├── LedgerStructuralConstraints.feature
│       └── OpsStructuralConstraints.feature
├── Projects/
│   ├── Project001-Database.md
│   └── Project002-TestHarness.md
├── Src/
│   ├── LeoBloom.Domain/              # Pure domain types (Ledger.fs, Ops.fs)
│   ├── LeoBloom.Domain.Tests/        # Business logic BDD tests (future)
│   ├── LeoBloom.Dal/                 # Data access + ConnectionString resolver
│   ├── LeoBloom.Dal.Tests/           # Structural constraint Gherkin tests ← NEW
│   ├── LeoBloom.Migrations/          # Uses Dal.ConnectionString.resolve
│   └── LeoBloom.Api/                 # Refs Domain + Dal
└── .gitignore
```

---

## Tech Stack

- **TickSpec** — F# Gherkin step definition runner
- **xUnit** — test framework (already in use)
- **Npgsql** — direct PostgreSQL connection for test assertions
- **.NET 10** — consistent with all other projects

BD to discuss any alternative Gherkin runners with Dan before committing.

---

## Acceptance Criteria

1. `dotnet test` from the solution root runs all Gherkin scenarios against
   `leobloom_dev` and they pass.
2. `LeoBloom.Migrations` uses `Dal.ConnectionString.resolve` — no duplicated
   config loading.
3. `LEOBLOOM_ENV` is the sole mechanism for environment selection across all
   projects.
4. No test data persists in the database after a test run.
5. Solution builds clean with zero warnings.

---

## Out of Scope

- Domain types (Project 3)
- Business logic tests (Project 3)
- API layer (future)
- UI (future)

---

## Dependencies

- Project 1 complete (merged ✓)
- Project restructure complete (merged ✓)
- BD: `LEOBLOOM_ENV=Development` set in container
- Dan: `LEOBLOOM_ENV=Production` set on host (done ✓, in `.bashrc`)
