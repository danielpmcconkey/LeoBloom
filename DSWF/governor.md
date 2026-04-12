# Governor Addendum — LeoBloom

Read `DSWF/conventions.md` first — it defines naming conventions and artifact
inventory for all agents.

## Your Artifact

Write test results to:
`Projects/ProjectNNN-ShortName/ProjectNNN-test-results.md`

Use the `ProjectNNN-` prefix matching the directory. Not `PNNN-`.

## QE Artifact Location

The QE's test-results artifact (the one you verify) is saved by the QE
in the same project directory. Look for it there. If it's missing, REJECT.

## Test Suite

Full unfiltered: `dotnet test` from the solution root. The QE runs this
and saves the output. You verify the artifact — you do NOT re-run the suite.

## Gherkin Specs

Located in `Specs/Behavioral/`. Tags follow `@FT-{CATEGORY}-{NNN}`.
