# Project 3 — BDD Acceptance Criteria

BDD IDs are project-scoped and ephemeral. They start at 001 for each project
and live only in this document. Test-results will map these to permanent
`@FT-*` Feature IDs after the green run.

---

## Deliverable 1: Relocate Feature Files

**BDD-001** — `Specs/Structural/LedgerStructuralConstraints.feature` exists
and contains all 29 ledger structural constraint scenarios.

**BDD-002** — `Specs/Structural/OpsStructuralConstraints.feature` exists and
contains all 41 ops structural constraint scenarios.

**BDD-003** — `Specs/Structural/DeleteRestrictionConstraints.feature` exists
and contains all 18 delete restriction scenarios.

**BDD-004** — No `.feature` files exist under
`Projects/Project001-Database/Specs/`.

**BDD-005** — The directory `Projects/Project001-Database/Specs/` does not
exist.

**BDD-006** — `Projects/Project001-Database/DataModelSpec.md` exists (moved
from `Specs/`).

**BDD-007** — `Projects/Project001-Database/SampleCOA.md` exists (moved from
`Specs/`).

**BDD-008** — No `.md` files exist directly in `Specs/`. The `Specs/`
directory contains only capability-scoped subdirectories with `.feature` files.

---

## Deliverable 2: Tag All 88 Scenarios with Feature IDs

**BDD-009** — Every `Scenario:` in `LedgerStructuralConstraints.feature` has a
`@FT-LSC-NNN` tag on the line above it, numbered `001` through `029` in file
order.

**BDD-010** — Every `Scenario:` in `OpsStructuralConstraints.feature` has a
`@FT-OSC-NNN` tag on the line above it, numbered `001` through `041` in file
order.

**BDD-011** — Every `Scenario:` in `DeleteRestrictionConstraints.feature` has
a `@FT-DR-NNN` tag on the line above it, numbered `001` through `018` in file
order.

**BDD-012** — All 88 `@FT-*` tags across all three feature files are unique.
No duplicates.

---

## Deliverable 3: DeleteTarget Context Refactor

**BDD-013** — `ScenarioContext` in `SharedSteps.fs` includes a field for
specifying a delete target (table name, WHERE clause, parameters).

**BDD-014** — Each of the 18 Given steps in
`DeleteRestrictionStepDefinitions.fs` sets the delete target on
`ScenarioContext` instead of relying on a paired When step to hardcode the SQL.

**BDD-015** — When steps in `DeleteRestrictionStepDefinitions.fs` read the
delete target from `ScenarioContext` and execute the DELETE. No magic strings
in When steps.

**BDD-016** — The number of distinct When step definitions for delete
restriction scenarios is fewer than 18. Consolidation has occurred.

**BDD-017** — All 18 delete restriction scenarios still pass after the
refactor. Behavior is unchanged.

---

## Deliverable 4: xUnit Trait for Structural Tests

**BDD-018** — `FeatureFixture.fs` test methods carry the attribute
`[<Trait("Category", "Structural")>]`.

**BDD-019** — `dotnet test --filter Category=Structural` runs exactly 88
tests.

---

## Deliverable 5: Documentation Routing Table

**BDD-020** — `Documentation/README.md` exists at repo root.

**BDD-021** — `Documentation/README.md` describes the purpose of each
top-level directory: `Projects/`, `Specs/`, `Documentation/`, `Src/`,
`BdsNotes/`, `HobsonsNotes/`.

---

## Deliverable 6: Update `.fsproj` Embedded Resource Paths

**BDD-022** — `LeoBloom.Dal.Tests.fsproj` contains `<EmbeddedResource>` entries
pointing to `../../Specs/Structural/LedgerStructuralConstraints.feature`.

**BDD-023** — `LeoBloom.Dal.Tests.fsproj` contains `<EmbeddedResource>` entries
pointing to `../../Specs/Structural/OpsStructuralConstraints.feature`.

**BDD-024** — `LeoBloom.Dal.Tests.fsproj` contains `<EmbeddedResource>` entries
pointing to `../../Specs/Structural/DeleteRestrictionConstraints.feature`.

**BDD-025** — No `<EmbeddedResource>` entries in the `.fsproj` reference the
old `Projects/Project001-Database/Specs/` paths.

---

## Deliverable 7: Green Test Run & Test-Results Mapping

**BDD-026** — `dotnet test` runs all 88 scenarios against `leobloom_dev` and
all pass.

**BDD-027** — `Projects/Project003-BddInfrastructure/Project003-test-results.md`
exists and maps each BDD ID from this document to the corresponding `@FT-*`
ID(s), with commit hash, date, and pass/fail status.

**BDD-028** — `Projects/Project001-Database/Project001-test-results.md` exists
and maps all Project 1 scenarios to their `@FT-*` IDs retroactively.

---

## Deliverable 8: This Document

**BDD-029** — `Projects/Project003-BddInfrastructure/Project003-bdd.md` exists
and contains project-scoped BDD IDs covering all deliverables from the BRD.
