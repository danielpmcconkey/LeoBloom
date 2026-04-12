# Project 083 — PO Kickoff

**Date:** 2026-04-12
**Backlog item:** P083 — Closed Period Enforcement, Reversing Entries & Adjustments
**Step:** PO Kickoff (pre-plan)

## Business Intent

Close the last hole in the closed-period safety net. Right now you can void
a JE in a closed period and nobody stops you — that's a GAAP violation
waiting to happen. This item blocks that void, gives users a proper
alternative (reversing entries), and adds the adjustment workflow so
post-close corrections are traceable and auditable.

## Spec Assessment

The spec is well-structured across four parts with clear acceptance criteria:

1. **Void enforcement** — Blocks void on closed periods with an actionable
   error. Three acceptance criteria, all testable. Good.

2. **Reversing entries** — New `ledger reverse` command. Eight acceptance
   criteria covering the happy path, idempotency guard, voided-JE guard,
   date override, and JSON output. Validation rules are explicit. Good.

3. **Post-close adjustments** — Schema migration (`adjustment_for_period_id`),
   domain type change, and CLI flag. Six acceptance criteria. The FK points
   to a period that can be closed — that's the whole point. Good.

4. **Fiscal period override** — `--fiscal-period-id` flag on `ledger post`.
   Three acceptance criteria. Clean escape hatch.

## Dependency Check

- **P081 (fiscal period close metadata):** Shipped (`3f8af5b`). Provides
  `closed_at`, `closed_by`, audit trail. P083 needs `is_open` checks — in
  place.
- **P082 (pre-close validation):** Shipped (`9197b8a`). Not a hard dependency
  for P083 but confirms the validation infrastructure is sound.

## GAAP Relevance

This is directly GAAP-relevant:
- Closed periods are sacrosanct. Allowing voids in closed periods violates
  the principle that closed books should not be silently altered.
- Reversing entries are the GAAP-correct mechanism for correcting closed-period
  errors — they create an auditable trail in the current period.
- The `adjustment_for_period_id` tag enables report disclosure (P084) of which
  period a correction relates to, supporting proper footnote disclosure.

No GAAP concerns with the spec as written. The spec correctly does NOT allow
posting into closed periods — all corrections happen in open periods with
a backward reference.

## Readiness

- Spec is complete with 20 testable acceptance criteria
- Dependencies shipped
- No ambiguity in business intent
- Out of scope is explicit (P084 handles reporting)
- Project directory created: `Projects/Project083-ClosedPeriodEnforcement/`

## Verdict

**READY FOR PLANNING.** Spec is clean, intent is clear, GAAP alignment is
solid. Ship it to the Planner.
