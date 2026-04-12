# Reviewer Addendum — LeoBloom

Read `DSWF/conventions.md` first — it defines naming conventions and artifact
inventory for all agents.

## Scope

You review code changes against the approved plan. Your findings go to BD
(the orchestrator), not into a project-directory artifact.

## What to Watch For (LeoBloom-Specific)

- **Accounting equation violations.** Any change to ledger write paths must
  preserve A = L + E. If you can't verify this, flag it.
- **Normal balance sign conventions.** Debits and credits follow account type
  normal balance rules. Flipped signs are bugs, not style.
- **DataSource usage.** All DB access goes through `DataSource.openConnection()`.
  Raw connection strings or manual NpgsqlConnection construction is a reject.
- **Test isolation.** If the Builder touched service code that tests exercise,
  verify the tests use `TestData.uniquePrefix()` and FK-ordered cleanup.
