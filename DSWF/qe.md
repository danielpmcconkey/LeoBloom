# QE Addendum — LeoBloom

## Stack

- F# / .NET 10
- xUnit test framework
- PostgreSQL (NpgsqlDataSource)
- No mocking. All tests hit the real database.

## Connection Pattern

Use `DataSource.openConnection()` for all DB access. Never construct
connections from raw strings. Never use `ConnectionString.fs` (deleted).

```fsharp
use conn = DataSource.openConnection()
```

The `DataSource` singleton initializes eagerly at startup with:
- `IncludeErrorDetail = true`
- `ApplicationName = "LeoBloom"`
- 5-second connect/command timeouts
- `#if DEBUG` safety guard that verifies `current_database() = leobloom_dev`

## Test Data Isolation

Every test creates its own unique data using `TestData.uniquePrefix()` —
a 4-character random string. Use this prefix in all entity names to avoid
collisions under parallel execution.

```fsharp
let prefix = TestData.uniquePrefix ()
let accountName = $"{prefix}_checking"
```

Never rely on shared mutable state, row counts, or "find by status" queries.
Always query by the specific IDs your test created.

## Cleanup

Use `TestCleanup.track` to register entities for cleanup, then call
`TestCleanup.deleteAll` in a `finally` block or xUnit `IDisposable`.

Cleanup deletes in FK order:
1. transfers (by journal_entry_id, then by account_id)
2. journal_entry_lines (by journal_entry_id)
3. journal_entries (by id)
4. obligation_instances (by journal_entry_id, then by agreement_id)
5. obligation_agreements (by id, then by account_id)
6. accounts (by id)
7. account_types (by id)

**Never swallow cleanup errors.** Log them to stderr with `eprintfn`.
(Will switch to structured logging when Project 031 lands.)

## Test Structure

Tests are plain xUnit `[<Fact>]` methods, not TickSpec/BDD step definitions.
TickSpec was removed in Project 030.

**Behavioral tests** (mapped to Gherkin scenarios):
- Tag with `[<Trait("GherkinId", "@FT-XXX-NNN")>]`
- Call real production functions (service layer)
- Assert observable outcomes via DB queries

**Structural tests** (no Gherkin mapping):
- Verify constraints, FK relationships, schema invariants
- Can use raw SQL

## Naming Conventions

- Test files: `{FeatureName}Tests.fs`
- Test classes: `{FeatureName}Tests`
- Test methods: descriptive, sentence-style
  - Behavioral: `Post journal entry with balanced lines succeeds`
  - Structural: `Journal entry line requires valid journal entry FK`

## Tag Conventions

Gherkin scenario tags follow the pattern `@FT-{CATEGORY}-{NNN}`:
- `@FT-PJE` — Post Journal Entry
- `@FT-VJE` — Void Journal Entry
- `@FT-AB` — Account Balance

New features get a new category prefix. Check existing tags in
`Specs/Behavioral/` before creating a new one.

## File Locations

- Test project: `Src/LeoBloom.Tests/`
- Test helpers: `Src/LeoBloom.Tests/TestHelpers.fs`
- Feature specs: `Specs/Behavioral/`
- Coverage script: `Scripts/check-gherkin-coverage.fsx`

## Anti-Patterns

- **Don't mock the database.** We got burned when mocked tests passed but
  the prod query had a real bug (voided entry LEFT JOIN issue, Project 030).
- **Don't use transactions for test isolation.** Queries inside the
  transaction see uncommitted state that real callers won't see.
- **Don't share test data across tests.** Each test creates and cleans up
  its own data.
- **Don't query by mutable state flags** (like `is_active`). Query by the
  specific IDs your test inserted.
- **Don't leave orphaned test data.** If your test creates transfers or
  obligation instances without journal entries, add cleanup paths for those
  FK relationships.
