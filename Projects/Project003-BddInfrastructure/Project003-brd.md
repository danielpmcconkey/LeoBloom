# Project 3 — BDD Infrastructure & Test Bench Overhaul

## Objective

Retrofit the BDD flow established after Projects 1-2 shipped. Feature files
move from their project-scoped home to capability-scoped directories at repo
root, every scenario gets a permanent `@FT-*` tag, and the delete-restriction
step definitions get a context-driven refactor to eliminate hardcoded magic
strings. When this project is done, the spec-driven development loop is fully
operational for all future projects.

No new features. No schema changes. Plumbing only.

---

## Deliverables

### 1. Relocate Feature Files

Move the three `.feature` files from their current project-scoped location to
a capability-scoped `Specs/Structural/` directory at repo root:

| From | To |
|---|---|
| `Projects/Project001-Database/Specs/Acceptance/LedgerStructuralConstraints.feature` | `Specs/Structural/LedgerStructuralConstraints.feature` |
| `Projects/Project001-Database/Specs/Acceptance/OpsStructuralConstraints.feature` | `Specs/Structural/OpsStructuralConstraints.feature` |
| `Projects/Project001-Database/Specs/Acceptance/DeleteRestrictionConstraints.feature` | `Specs/Structural/DeleteRestrictionConstraints.feature` |

Move design docs from `Specs/` to `Projects/Project001-Database/` — they are
project-1-era design artifacts, not enforced specs:

| From | To |
|---|---|
| `Specs/DataModelSpec.md` | `Projects/Project001-Database/DataModelSpec.md` |
| `Specs/SampleCOA.md` | `Projects/Project001-Database/SampleCOA.md` |

Remove `Projects/Project001-Database/Specs/` after the feature file move.
Remove any empty directories left under `Specs/` after the design doc move.
`Specs/` is pure Gherkin — nothing but `.feature` files organized by
capability. Feature files belong to the repo, not to a project.

### 2. Tag All 88 Scenarios with Feature IDs

Add permanent `@FT-{category}-{seq}` tags to every scenario across all three
feature files. Tags are applied as annotations on the line above each
`Scenario:` keyword per Gherkin convention.

| Feature File | Category | Count | Range |
|---|---|---|---|
| `LedgerStructuralConstraints.feature` | `LSC` | 29 | `@FT-LSC-001` through `@FT-LSC-029` |
| `OpsStructuralConstraints.feature` | `OSC` | 41 | `@FT-OSC-001` through `@FT-OSC-041` |
| `DeleteRestrictionConstraints.feature` | `DR` | 18 | `@FT-DR-001` through `@FT-DR-018` |

Sequence numbers are assigned in file order (top to bottom). These IDs are
permanent and global — they survive code wipes and project boundaries.

### 3. DeleteTarget Context Refactor

The 18 delete-restriction scenarios currently use unique When steps per
scenario (e.g., `I delete the parent account_type`, `I delete the parent
account`, `I delete the account referenced as source_account`) where each When
step hardcodes the SQL and parameter values for its specific target.

**Refactor:** Add a `DeleteTarget` field to `ScenarioContext` in
`SharedSteps.fs`. Given steps set the target (table, WHERE clause, parameters)
via context. When steps read it from context and execute.

This eliminates the 1:1 coupling between Given and When steps and makes the
pattern extensible — new delete-restriction scenarios only need a Given step
that sets the target, not a custom When step.

**Files touched:**
- `SharedSteps.fs` — add `DeleteTarget` (or equivalent) to `ScenarioContext`
- `DeleteRestrictionStepDefinitions.fs` — refactor all 18 Given steps to set
  the target in context; consolidate When steps where possible
- `DeleteRestrictionConstraints.feature` — update When step text if the
  consolidated step wording changes

### 4. Add xUnit Trait for Structural Tests

Add `[<Trait("Category", "Structural")>]` to the test methods in
`FeatureFixture.fs` so structural constraint tests can be filtered via
`dotnet test --filter Category=Structural`.

### 5. Documentation Routing Table

Create `Documentation/README.md` at repo root. Purpose: a routing table that
tells contributors (human or AI) where things go. Contents:

| Directory | Purpose |
|---|---|
| `Projects/` | Project artifacts — BRD, BDD doc, plan, test-results. One subfolder per project. |
| `Specs/` | Pure Gherkin. Feature files organized by capability. Enforced, executable, permanent. Nothing else. |
| `Documentation/` | This file. Meta-docs about repo organization. |
| `Src/` | All source code — production and test projects. |
| `BdsNotes/` | BD's working notes (context engineering, session state). |
| `HobsonsNotes/` | Hobson's working notes. |

Keep it short. If it's longer than a single screen, it's too long.

### 6. Update `.fsproj` Embedded Resource Paths

Update the `<EmbeddedResource>` entries in
`Src/LeoBloom.Dal.Tests/LeoBloom.Dal.Tests.fsproj` to point to the new
`Specs/Structural/` locations:

```xml
<EmbeddedResource Include="..\..\Specs\Structural\LedgerStructuralConstraints.feature"
                  Link="LedgerStructuralConstraints.feature" />
<EmbeddedResource Include="..\..\Specs\Structural\OpsStructuralConstraints.feature"
                  Link="OpsStructuralConstraints.feature" />
<EmbeddedResource Include="..\..\Specs\Structural\DeleteRestrictionConstraints.feature"
                  Link="DeleteRestrictionConstraints.feature" />
```

### 7. Green Test Run & Test-Results Mapping

Run all 88 tests against `leobloom_dev`. All must pass.

Write `Projects/Project003-BddInfrastructure/Project003-test-results.md`
mapping the new `@FT-*` IDs to the scenarios they tag, with commit hash, date,
and pass/fail status. This is the one-time bridge artifact for Project 3.

Also update `Projects/Project001-Database/` with a
`Project001-test-results.md` that retroactively maps the original Project 1
scenarios to their new FT IDs.

### 8. Project 3 BDD Doc

Write `Projects/Project003-BddInfrastructure/Project003-bdd.md` with
project-scoped BDD IDs (`BDD-001` through `BDD-NNN`) covering each
deliverable's acceptance criteria. Separate deliverable from this BRD.

---

## Project Structure After Completion

```
LeoBloom/
├── LeoBloom.sln
├── Documentation/
│   └── README.md                        ← NEW: routing table
├── Specs/                               ← Pure Gherkin, nothing else
│   └── Structural/                      ← NEW: capability-scoped
│       ├── LedgerStructuralConstraints.feature   ← MOVED + tagged
│       ├── OpsStructuralConstraints.feature       ← MOVED + tagged
│       └── DeleteRestrictionConstraints.feature   ← MOVED + tagged
├── Projects/
│   ├── BACKLOG.md
│   ├── Project001-Database/
│   │   ├── Project001-brd.md
│   │   ├── DataModelSpec.md             ← MOVED from Specs/
│   │   ├── SampleCOA.md                ← MOVED from Specs/
│   │   └── Project001-test-results.md   ← NEW: retroactive FT mapping
│   ├── Project002-TestHarness/
│   │   └── Project002-brd.md
│   └── Project003-BddInfrastructure/
│       ├── Project003-brd.md            ← THIS FILE
│       ├── Project003-bdd.md            ← deliverable 8
│       └── Project003-test-results.md   ← deliverable 7
├── Src/
│   ├── LeoBloom.Dal.Tests/
│   │   ├── SharedSteps.fs               ← MODIFIED: DeleteTarget in context
│   │   ├── DeleteRestrictionStepDefinitions.fs  ← MODIFIED: refactored
│   │   ├── FeatureFixture.fs            ← MODIFIED: Trait added
│   │   └── LeoBloom.Dal.Tests.fsproj    ← MODIFIED: new paths
│   └── ...
└── .gitignore
```

`Projects/Project001-Database/Specs/` is deleted after the move.

---

## Acceptance Criteria

1. `dotnet test` runs all 88 scenarios against `leobloom_dev` and all pass.
2. Feature files live in `Specs/Structural/`, not under any project directory.
3. Every scenario in all three feature files has a unique `@FT-*` tag.
4. `dotnet test --filter Category=Structural` runs exactly the 88 structural tests.
5. `DeleteRestrictionStepDefinitions.fs` Given steps set the delete target via
   `ScenarioContext`, not via magic strings baked into per-scenario When steps.
6. `.fsproj` `<EmbeddedResource>` paths resolve correctly to `Specs/Structural/`.
7. `Documentation/README.md` exists and describes the purpose of each top-level directory.
8. `Project001-test-results.md` maps all Project 1 scenarios to their `@FT-*` IDs.
9. `Project003-test-results.md` maps Project 3 BDD IDs to `@FT-*` IDs with commit hash and date.
10. No feature files remain under `Projects/Project001-Database/Specs/`.

---

## Out of Scope

- New database tables, migrations, or schema changes
- New domain types or business logic
- New Gherkin scenarios (reorganizing and tagging existing ones only)
- API work
- UI work
- Retroactive BDD docs for Projects 1 or 2

---

## Dependencies

- Projects 1 and 2 complete (merged)
- All 88 existing tests passing before work begins
- `leobloom_dev` database accessible from BD's container
