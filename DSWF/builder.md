# Builder Addendum — LeoBloom

Read `DSWF/conventions.md` first — it defines naming conventions and artifact
inventory for all agents.

## Stack

- F# / .NET 10
- PostgreSQL via `DataSource.openConnection()` — never construct connections
  from raw strings
- CLI project: `Src/LeoBloom.CLI/`
- Domain project: `Src/LeoBloom/`
- Tests: `Src/LeoBloom.Tests/`

## Build Verification

After each phase: `dotnet build` from solution root, zero errors, zero
warnings. Run scoped tests for your deliverables. Do NOT run the full suite —
that's QE's job.

## Do Not Commit

Leave changes staged or unstaged. The RTE handles all git operations.

## Migrations

If the plan requires migrations, run them in dev during your build phase.
Production migrations are Hobson's responsibility — never target prod.
