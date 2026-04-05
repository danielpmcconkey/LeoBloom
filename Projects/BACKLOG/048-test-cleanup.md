# 048 — Test Cleanup

**Epic:** K — Code Audit Remediation
**Depends On:** None
**Status:** Not started

---

Remove dead tests identified by the 2026-04-05 test audit. These are
tautologies (Assert.True(true)), ghost guards (conditions that cannot recur
after completed one-time work), and tests that assert architectural decisions
rather than runtime behavior.

**Nuke entirely:**

- `DalToUtilitiesRenameTests.fs` — all 8 tests (FT-DUR-001 through
  FT-DUR-008). Ghost guards and tautologies for a completed rename.

**Nuke individual tests from DataSourceEncapsulationTests.fs:**

- FT-DSI-002 — `DataSource.connectionString` no longer exists as a public
  binding. The test guards against a symbol that was already sealed.
- FT-DSI-003 — Migrations referencing Utilities is not a real problem.
  Architectural decision, not a runtime invariant.
- FT-DSI-004 — Architectural decision (Migrations builds its own connection
  string). Not a testable runtime behavior.
- FT-DSI-005 — Architectural decision (Migrations opens its own connection).
  Same rationale as DSI-004.
- FT-DSI-006 — Tautology (Assert.True(true)).
- FT-DSI-007 — Tautology (Assert.True(true)).

**Nuke individual tests from LogModuleStructureTests.fs:**

- FT-LMS-001 through FT-LMS-004 — Serilog package reference checks. If a
  package is removed, the build fails. These are documentation, not protection.
- FT-LMS-005 — Log.fs exists check. Accidental deletion breaks the build.
- FT-LMS-006, FT-LMS-007 — Log module API surface tests. Covered by the
  compiler (callers would fail to build if signatures changed).
- FT-LMS-010 — Migrations no reference to Utilities. Duplicate of DSI-003.
- FT-LMS-011 — Migrations no Serilog contamination. Architectural decision.

**Nuke individual tests from LoggingInfrastructureTests.fs:**

- FT-LI-001, FT-LI-002 — Log.initialize called at startup / test infra.
  Structural checks better left to code review.
- FT-LI-005, FT-LI-006 — Log level / file path configurable via
  appsettings. Configuration assertions, not behavioral tests.

**Verified-by-design documentation:**

For tests that trace to valid BDD requirements but don't need runtime
assertions, add "verified by design" sections to the relevant project test
results docs (Project031, Project033) with ADR citations explaining why the
runtime assertion was removed.

**Source:** Code audit test-audit.md, SYNTHESIS.md Tier 2 Finding #6
