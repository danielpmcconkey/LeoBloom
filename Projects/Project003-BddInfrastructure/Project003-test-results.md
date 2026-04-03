# Project 003 — Test Results

BDD ID → Feature ID mapping for Project 3.

**Date:** 2026-04-03
**Result:** 88/88 passed, all BDD criteria verified
**Runner:** `dotnet test` (TickSpec + xUnit, .NET 10)
**Database:** `leobloom_dev`

---

## BDD → FT Mapping

| BDD ID | Description | Verified | FT IDs |
|---|---|---|---|
| BDD-001 | LedgerStructuralConstraints.feature exists in Specs/Structural/ | Yes | @FT-LSC-001 through @FT-LSC-029 |
| BDD-002 | OpsStructuralConstraints.feature exists in Specs/Structural/ | Yes | @FT-OSC-001 through @FT-OSC-041 |
| BDD-003 | DeleteRestrictionConstraints.feature exists in Specs/Structural/ | Yes | @FT-DR-001 through @FT-DR-018 |
| BDD-004 | No .feature files under Projects/Project001-Database/Specs/ | Yes | — |
| BDD-005 | Projects/Project001-Database/Specs/ does not exist | Yes | — |
| BDD-006 | DataModelSpec.md exists in Projects/Project001-Database/ | Yes | — |
| BDD-007 | SampleCOA.md exists in Projects/Project001-Database/ | Yes | — |
| BDD-008 | No .md files in Specs/, only capability subdirs with .feature files | Yes | — |
| BDD-009 | LSC tags @FT-LSC-001 through @FT-LSC-029 applied in file order | Yes | @FT-LSC-* |
| BDD-010 | OSC tags @FT-OSC-001 through @FT-OSC-041 applied in file order | Yes | @FT-OSC-* |
| BDD-011 | DR tags @FT-DR-001 through @FT-DR-018 applied in file order | Yes | @FT-DR-* |
| BDD-012 | All 88 @FT-* tags are unique across all feature files | Yes | — |
| BDD-013 | ScenarioContext includes DeleteTarget field | Yes | — |
| BDD-014 | All 18 Given steps set DeleteTarget on context | Yes | — |
| BDD-015 | When step reads DeleteTarget from context, no magic strings | Yes | — |
| BDD-016 | Fewer than 18 distinct When steps (consolidated to 1) | Yes | — |
| BDD-017 | All 18 delete restriction scenarios pass after refactor | Yes | @FT-DR-001 through @FT-DR-018 |
| BDD-018 | FeatureFixture.fs carries Trait("Category", "Structural") | Yes | — |
| BDD-019 | `dotnet test --filter Category=Structural` runs exactly 88 tests | Yes | — |
| BDD-020 | Documentation/README.md exists at repo root | Yes | — |
| BDD-021 | README.md describes purpose of all top-level directories | Yes | — |
| BDD-022 | fsproj points to Specs/Structural/LedgerStructuralConstraints.feature | Yes | — |
| BDD-023 | fsproj points to Specs/Structural/OpsStructuralConstraints.feature | Yes | — |
| BDD-024 | fsproj points to Specs/Structural/DeleteRestrictionConstraints.feature | Yes | — |
| BDD-025 | No fsproj references to old Projects/Project001-Database/Specs/ paths | Yes | — |
| BDD-026 | All 88 scenarios pass against leobloom_dev | Yes | All @FT-* |
| BDD-027 | Project003-test-results.md exists with BDD → FT mapping | Yes | — |
| BDD-028 | Project001-test-results.md exists with retroactive FT mapping | Yes | — |
| BDD-029 | Project003-bdd.md exists with BDD IDs covering all deliverables | Yes | — |
