# ADR-002: Domain-Based Project Structure

**Date:** 2026-04-05
**Status:** Accepted
**Decision by:** Dan McConkey (product owner)

---

## Context

The 2026-04-05 code audit (Architecture and Simplicity reviewers) flagged
LeoBloom.Utilities as a god project: 22 files spanning infrastructure,
persistence, and orchestration services. The audit recommended splitting
into three layer-based projects: Infrastructure, Persistence, Services.

## Decision

Projects are split by business domain (Ledger, Ops), not by architectural
layer (Infrastructure, Persistence, Services).

**Target structure:**

- **LeoBloom.Utilities** — generic cross-cutting infrastructure only:
  DataSource, Log, configuration, DataHelpers.
- **LeoBloom.Ledger** — ledger-domain services and repositories (journal
  entries, accounts, fiscal periods, reporting).
- **LeoBloom.Ops** — ops-domain services and repositories (obligations,
  transfers, posting).

## Rationale

The domain boundaries (ledger vs. ops) are the natural fault lines in the
system. They match how the business thinks about the problem: "ledger
stuff" and "ops stuff." Layer-based splits (Infrastructure / Persistence /
Services) add indirection without matching these mental models.

With domain-based projects:
- Adding a new ledger feature touches one project.
- Adding a new ops feature touches one project.
- Cross-domain operations (posting an obligation to the ledger) have a
  clear dependency direction: Ops references Ledger.

With layer-based projects:
- Every feature touches three projects.
- The dependency graph becomes a lattice instead of a tree.
- The "where does this go" question has no intuitive answer for mixed
  concerns.

## Consequences

- Each domain project contains both its repositories and its services.
  There is no separate persistence layer.
- Utilities is a dependency of both domain projects. It must contain only
  generic infrastructure — no domain-specific code.
- The Ops -> Ledger dependency is explicit in the project references.
  Ledger must not reference Ops.
- All tests remain in LeoBloom.Tests. Separate test projects per domain
  are not needed at current scale.
