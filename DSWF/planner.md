# Planner Addendum — LeoBloom

Read `DSWF/conventions.md` first — it defines naming conventions and artifact
inventory for all agents.

## Plan Location

`Projects/ProjectNNN-ShortName/ProjectNNN-plan.md`

Use the `ProjectNNN-` prefix matching the directory. Not `PNNN-`.

## Acceptance Criteria

Separate criteria into:
- **Behavioral** — will become Gherkin scenarios. Observable outcomes.
- **Structural** — Builder/QE verify directly. File existence, config
  changes, schema state.

Label each criterion so the Gherkin Writer and QE know which are theirs.

## GAAP Consideration

If the plan touches accounting logic (ledger writes, balance calculations,
report generation, fiscal periods), note any GAAP implications in the Risks
section. If unsure, flag for escalation.
