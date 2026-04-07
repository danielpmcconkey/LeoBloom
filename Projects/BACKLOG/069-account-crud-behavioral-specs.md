# 069 — Account CRUD Behavioral Specs

**Epic:** Audit Remediation (2026-04-07)
**Depends On:** None
**Status:** Not started
**Priority:** Medium
**Source:** Omission Hunter GAP-038

---

## Problem Statement

The chart of accounts is a load-bearing data structure. Structural specs
test DB constraints. CLI specs test `account list`, `account show`, and
`account balance`. But there are zero service-level behavioral specs for
creating or updating an account. The write path is untested at the service
layer.

## What Ships

New spec file `Specs/Behavioral/AccountCrud.feature` (or added to an
existing file) covering:

1. **Create account happy path** — valid type, code, name, optional parent
2. **Create with invalid account_type_id** — rejected
3. **Create with duplicate code** — rejected
4. **Create with invalid parent_id** — rejected
5. **Create with inactive parent** — behavior defined and tested
6. **Update account name** — succeeds, round-trips
7. **Deactivate account** — sets is_active = false
8. **Deactivate account with child accounts** — behavior defined (reject
   or cascade deactivate)
9. **Deactivate account with posted journal entries** — behavior defined

Plus corresponding test implementations.

## Acceptance Criteria

- AC-1: Service-level create account with valid data succeeds
- AC-2: Service-level create with invalid type, duplicate code, or invalid
  parent is rejected with appropriate error messages
- AC-3: Deactivation behavior is defined and tested
- AC-4: All scenarios exercise the service layer, not just DB constraints

## Scope Boundary

This covers the service-layer behavioral tests only. CLI tests for account
create/update (if the CLI supports them) are a separate card. The existing
`account list`, `account show`, and `account balance` CLI specs are not in
scope.
