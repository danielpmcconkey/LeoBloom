# 063 — Portfolio Schema Delete Restriction Tests

**Epic:** Audit Remediation (2026-04-07)
**Depends On:** 057 (portfolio schema must exist)
**Status:** Not started
**Priority:** High
**Source:** Domain Invariant Auditor DR-002, Omission Hunter GAP-039

---

## Problem Statement

The ledger and ops schemas have ON DELETE RESTRICT on every FK, with 14
scenarios in `DeleteRestrictionConstraints.feature` proving it. The portfolio
schema has zero delete restriction tests. If a migration accidentally set
ON DELETE CASCADE, you could delete a tax_bucket and silently cascade-delete
every investment account classified under it.

## What Ships

New scenarios in `Specs/Structural/DeleteRestrictionConstraints.feature`
(or a new `PortfolioDeleteRestrictions.feature` if the file is too large)
covering every FK in the portfolio schema:

1. Delete `tax_bucket` with dependent `investment_account` — RESTRICT
2. Delete `account_group` with dependent `investment_account` — RESTRICT
3. Delete `investment_account` with dependent `position` — RESTRICT
4. Delete `fund` with dependent `position` — RESTRICT
5. Delete each `dim_*` table row with dependent `fund` — RESTRICT (6 dimensions)

Plus the corresponding test implementations.

## Acceptance Criteria

- AC-1: Every FK parent-child relationship in the portfolio schema has a
  scenario that attempts to delete the parent when a child exists
- AC-2: Every such scenario asserts the delete is rejected (RESTRICT)
- AC-3: Tests clean up after themselves using PortfolioTracker (not manual
  cleanup)
