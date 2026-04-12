# 067 — Portfolio Validation Gaps (cost_basis, future dates)

**Epic:** Audit Remediation (2026-04-07)
**Depends On:** 058 (portfolio domain types and repository)
**Status:** Not started
**Priority:** Medium
**Source:** Domain Invariant Auditor D-014/D-015, Omission Hunter GAP-033

---

## Problem Statement

Two portfolio validation rules are stated in the P058 backlog but have no
spec coverage:

1. **Negative cost_basis accepted silently.** The validation scenario outline
   (FT-PF-021) covers negative price, quantity, and current_value but omits
   cost_basis. A position with cost_basis = -5000.00 would pass all specs.
2. **Future-dated positions accepted.** P058 states "position date must not
   be in the future" as a service-layer validation rule, but no scenario
   tests it.

## What Ships

1. Add `cost_basis` to the existing FT-PF-021 scenario outline (one new
   example row), plus the service-layer validation if not already present.
2. New scenario: attempt to record a position with a future date. Assert
   rejection with appropriate error message. Add service-layer validation
   if not already present.

## Acceptance Criteria

- AC-1: Negative cost_basis is rejected by the service layer
- AC-2: A scenario exercises the negative cost_basis rejection path
- AC-3: A future-dated position is rejected by the service layer
- AC-4: A scenario exercises the future-date rejection path
