# ADR-001: No Business Logic in the Database Layer

**Date:** 2026-04-05
**Status:** Accepted
**Decision by:** Dan McConkey (product owner)

---

## Context

The 2026-04-05 code audit (Data Integrity reviewer) flagged the absence of
CHECK constraints on six enum varchar columns and recommended adding them
as a defense-in-depth measure. The audit also noted the absence of database
triggers and stored procedures for enforcing business rules.

## Decision

CHECK constraints, triggers, and stored procedures are not used for
business logic enforcement. All validation and business rules are enforced
in the F# application layer.

The database schema provides structural integrity only:
- Primary keys and foreign keys
- NOT NULL constraints
- UNIQUE constraints
- Data type constraints

## Rationale

LeoBloom has a single entry point through the application. The CLI
(P036-P042) will be the only interface. There is no direct SQL access in
production, no REST API, no secondary consumers hitting the database.

In this architecture, duplicating validation between the application layer
and the database layer creates two sources of truth for business rules.
When rules change, both must be updated in lockstep. The F# type system
and domain validators are more expressive than SQL CHECK constraints and
easier to test.

If the architecture changes (multiple consumers, direct DB access), this
decision should be revisited.

## Consequences

- Enum columns (`entry_type`, `normal_balance`, `status`, etc.) accept any
  string at the DB level. Invalid values are rejected by the application
  before insertion.
- If someone bypasses the application (manual SQL, a migration, a future
  import pipeline), invalid data can enter the database.
- The orphaned posting detection diagnostic (P035) serves as a safety net
  for data that slips past the application layer.
