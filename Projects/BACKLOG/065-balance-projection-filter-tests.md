# 065 — Balance Projection Status Filter Negative Tests

**Epic:** Audit Remediation (2026-04-07)
**Depends On:** None
**Status:** Not started
**Priority:** High
**Source:** Omission Hunter GAP-023/025, Domain Invariant Auditor BP-016/017

---

## Problem Statement

The balance projection calculates future cash flows from expected/in_flight
obligation instances and initiated transfers. No scenario verifies that
confirmed/posted obligations or confirmed transfers are *excluded* from the
projection. If the query doesn't filter by status, confirmed items (already
recorded in the ledger) would be double-counted.

## What Ships

New scenarios in `Specs/Ops/BalanceProjection.feature`:

1. **Confirmed obligation excluded:** Create obligation instances in both
   `expected` and `confirmed` status. Run projection. Assert only the
   expected instance appears. The confirmed amount must NOT be in the
   projection.
2. **Posted obligation excluded:** Same pattern with `posted` status.
3. **Confirmed transfer excluded:** Create transfers in both `initiated` and
   `confirmed` status. Run projection. Assert only the initiated transfer
   appears.
4. **Skipped obligation excluded:** Verify `skipped` instances don't appear.

## Acceptance Criteria

- AC-1: At least one scenario has both expected and confirmed obligation
  instances in the same projection window and asserts only the expected
  instance contributes to the projected balance
- AC-2: At least one scenario has both initiated and confirmed transfers
  and asserts only the initiated transfer contributes
- AC-3: A query that omitted the status filter would fail these tests
