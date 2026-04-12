# 058 — Investment Domain Types and Repository Layer

**Epic:** L — Investment Portfolio Module
**Depends On:** 057 (investment schema)
**Status:** Not started
**Priority:** High

---

## Problem Statement

P057 creates the database schema. This project builds the F# domain types and
repository layer so that other projects (CLI, reporting, data migration) can
read and write portfolio data through a typed, tested interface.

## What It Does

Creates a new `LeoBloom.Portfolio` project (parallel to LeoBloom.Ledger and
LeoBloom.Ops) containing:

1. **Domain types** — F# records and discriminated unions for TaxBucket,
   AccountGroup, InvestmentAccount, Fund (with its six classification
   dimensions), and Position.
2. **Repository layer** — Npgsql-based data access for each entity:
   - CRUD for investment accounts, funds, and positions
   - List/filter queries (positions by account, by date range, by symbol;
     funds by dimension value; accounts by group or tax bucket)
   - Latest-position-per-fund query (for current portfolio snapshot)
3. **Service layer** — thin orchestration matching the Ledger/Ops pattern.
   Validation logic (e.g., position date must not be in the future, quantity
   and value must be non-negative, fund must exist before recording a
   position).

## Acceptance Criteria

### Behavioral (Gherkin scenarios)

| ID | Criterion | Description |
|----|-----------|-------------|
| AC-B1 | Create investment account | Given valid tax bucket and account group, inserting an investment account succeeds and is retrievable by ID. |
| AC-B2 | Create fund | Given a symbol and name, inserting a fund succeeds. Classification dimensions are optional. |
| AC-B3 | Record position | Given a valid investment account and fund, recording a position snapshot succeeds with price, quantity, current_value, and cost_basis. |
| AC-B4 | Reject duplicate position | Recording a position for the same account + symbol + date as an existing position returns an error. |
| AC-B5 | List positions by account | Positions can be filtered by investment account ID, returning all symbols for that account. |
| AC-B6 | List positions by date range | Positions can be filtered by a date range across all accounts. |
| AC-B7 | Latest positions snapshot | A query returns only the most recent position per fund per account (current portfolio state). |
| AC-B8 | List funds by dimension | Funds can be filtered by any single classification dimension (e.g., all funds where sector = "Technology"). |
| AC-B9 | Reject negative values | Position with negative quantity, price, or current_value is rejected at service layer. |
| AC-B10 | Fund must exist for position | Recording a position for a nonexistent fund symbol returns an error. |

### Structural

| ID | Criterion | Description |
|----|-----------|-------------|
| AC-S1 | New LeoBloom.Portfolio project | F# class library project added to the solution, referenced by LeoBloom.Tests. |
| AC-S2 | Follows existing patterns | Repository and service patterns match LeoBloom.Ledger and LeoBloom.Ops (connection string from config, Npgsql, async). |
| AC-S3 | Existing tests still pass | All pre-existing tests pass without modification. |

## Scope Boundaries

### In scope

- `LeoBloom.Portfolio` F# project with domain types, repositories, services
- Test coverage for all acceptance criteria
- fsproj wired into the solution

### Explicitly out of scope

- **No CLI commands.** P059 handles that.
- **No data migration.** P060 handles that.
- **No reporting or allocation analysis.** P061 handles that.
