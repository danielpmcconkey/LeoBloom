# 074 — Npgsql Version Alignment

**Epic:** Infrastructure
**Depends On:** None
**Status:** Not started
**Priority:** Medium

---

## Problem Statement

Migrondi.Core 1.2.0 depends on Npgsql >= 10.0.1. The Migrations project was
pinned at 9.0.3, which caused a runtime assembly load failure when executing
migrations against prod (the NU1605 warning at build time became a hard crash
at runtime). Hobson bumped `LeoBloom.Migrations.fsproj` to Npgsql 10.0.1 to
unblock the prod migration run on 2026-04-08.

The remaining six projects are still on 9.0.3:

- LeoBloom.Utilities
- LeoBloom.Ledger
- LeoBloom.Ops
- LeoBloom.Portfolio
- LeoBloom.Reporting
- LeoBloom.Tests

## Acceptance Criteria

1. Assess whether the entire solution should move to Npgsql 10.x or whether
   the Migrations project should stay on 10 while the rest remain on 9.x.
2. If bumping to 10.x: identify any breaking API changes that affect existing
   code (connection handling, parameter binding, type mapping, etc.).
3. All projects reference a consistent Npgsql version (or the split is
   documented and justified).
4. All tests pass after the version change.
5. The NU1605 warning is resolved across the solution.
