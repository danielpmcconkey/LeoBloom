# 068 — Fiscal Period Overlap Prevention

**Epic:** Audit Remediation (2026-04-07)
**Depends On:** None
**Status:** Not started
**Priority:** High
**Source:** Omission Hunter GAP-011/037

---

## Problem Statement

Nothing prevents two fiscal periods with overlapping date ranges from being
created. The `period_key` column is unique, but two periods with different
keys can have overlapping date ranges (e.g., `2026-03` covering Mar 1–31
and `2026-03-alt` covering Mar 15–Apr 15). `findByDate` would return
ambiguous results, causing entries to silently post to the wrong period.

There is no structural constraint, no service validation, and no spec
coverage for this condition.

## What Ships

1. **Service-layer validation** in `FiscalPeriodService` (or wherever
   periods are created): before inserting a new fiscal period, query for
   any existing period whose date range overlaps the proposed range. Reject
   with a clear error message if overlap is found.

2. **Gherkin scenario** in `Specs/Behavioral/CloseReopenFiscalPeriod.feature`
   (or a new `FiscalPeriodManagement.feature` if more appropriate):
   create period A, then attempt to create period B with overlapping dates.
   Assert rejection.

3. **Optional but recommended:** A PostgreSQL exclusion constraint using
   `daterange` and `&&` (overlap operator) as a structural backstop. This
   is referential integrity, not business logic.

## Acceptance Criteria

- AC-1: Creating a fiscal period whose date range overlaps an existing
  period is rejected at the service layer
- AC-2: A Gherkin scenario exercises the overlap rejection
- AC-3: The error message identifies the conflicting period
