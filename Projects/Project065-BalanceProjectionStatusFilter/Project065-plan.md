# Project 065 — Balance Projection Status Filter Tests — Plan

## Objective

Add four negative-test scenarios to `Specs/Ops/BalanceProjection.feature`
proving that the projection query excludes obligation instances in terminal
statuses (`confirmed`, `posted`, `skipped`) and transfers in `confirmed`
status. Without these tests, a missing `WHERE status IN (...)` filter would
silently double-count amounts already recorded in the ledger.

## Phase 1: Append Gherkin Scenarios (Single File Change)

**What:** Append four scenarios (FT-BP-015 through FT-BP-018) to the end of
`Specs/Ops/BalanceProjection.feature` under a new section header:
`# Status Filter — Excluded Statuses`.

**Files modified:** `Specs/Ops/BalanceProjection.feature` (append only)

**Key design constraint (from PO):** Every scenario sets up BOTH an included
item AND an excluded item on the same date, in the same projection window.
The assertion pins the projected balance to the included item only. If the
status filter were dropped, the excluded item would shift the balance and
the assertion would fail — satisfying AC-3.

### Scenario FT-BP-015: Confirmed obligation excluded from projection

- Account "checking" with current balance 5000.00
- A receivable obligation instance for "checking" of 800.00 **expected** on
  2026-04-09 (included — this is the control)
- A receivable obligation instance for "checking" of 600.00 **confirmed** on
  2026-04-09 (excluded — already in the ledger)
- Projection through 2026-04-10
- Assert: 2026-04-09 balance is 5800.00 (5000 + 800, NOT 5000 + 800 + 600)
- **Falsification:** Without filter → balance would be 6400.00

### Scenario FT-BP-016: Posted obligation excluded from projection

- Account "checking" with current balance 3000.00
- A payable obligation instance for "checking" of 400.00 **expected** on
  2026-04-09 (included)
- A payable obligation instance for "checking" of 250.00 **posted** on
  2026-04-09 (excluded — already journaled)
- Projection through 2026-04-10
- Assert: 2026-04-09 balance is 2600.00 (3000 − 400, NOT 3000 − 400 − 250)
- **Falsification:** Without filter → balance would be 2350.00

### Scenario FT-BP-017: Confirmed transfer excluded from projection

- Account "checking" with current balance 4000.00
- An initiated transfer of 500.00 from "checking" with expected_settlement
  2026-04-09 (included)
- A confirmed transfer of 300.00 from "checking" on 2026-04-09 (excluded —
  already settled)
- Projection through 2026-04-10
- Assert: 2026-04-09 balance is 3500.00 (4000 − 500, NOT 4000 − 500 − 300)
- **Falsification:** Without filter → balance would be 3200.00

### Scenario FT-BP-018: Skipped obligation excluded from projection

- Account "checking" with current balance 2000.00
- A receivable obligation instance for "checking" of 1000.00 **expected** on
  2026-04-09 (included — the control)
- A payable obligation instance for "checking" of 350.00 **skipped** on
  2026-04-09 (excluded — waived, should not affect projection at all)
- Projection through 2026-04-10
- Assert: 2026-04-09 balance is 3000.00 (2000 + 1000, NOT 2000 + 1000 − 350)
- **Falsification:** Without filter → balance would be 2650.00

### New Step Definitions

The existing steps (`a receivable obligation instance for ... of ... expected on ...`)
imply status = expected. The new scenarios need steps that create instances
in non-expected statuses. Two approaches, in order of preference:

1. **Parameterized status step** — e.g.,
   `And a receivable obligation instance for "checking" of 600.00 in status "confirmed" on 2026-04-09`
   This requires a new step definition but is cleaner.

2. **Two-step setup** — create the instance as expected, then transition it.
   Reuses existing steps but is verbose.

The builder should check which approach the existing step infrastructure
supports and pick accordingly. Either way, the confirmed transfer step
already has precedent in `Specs/Behavioral/Transfers.feature` line 118:
`And a confirmed transfer of 500.00 from "checking" to "savings"`.

**Verification:** Each scenario's math is designed so that including the
excluded item would change the projected balance by a distinct, non-zero
amount. A broken filter cannot accidentally pass.

## Acceptance Criteria

- [ ] AC-1: At least one scenario has both expected and confirmed obligation
  instances in the same projection window and asserts only the expected
  instance contributes to the projected balance → **FT-BP-015**
- [ ] AC-2: At least one scenario has both initiated and confirmed transfers
  and asserts only the initiated transfer contributes → **FT-BP-017**
- [ ] AC-3: A query that omitted the status filter would fail these tests →
  all four scenarios have falsification amounts that differ from assertions

## Risks

- **Step definition gap:** If no step exists for creating an obligation
  instance in a non-expected status, one must be added. Risk: medium.
  Mitigation: the builder can use two-step setup (create + transition) as
  fallback.
- **Transfer status step:** Need a step for "confirmed transfer" in the
  projection context. Precedent exists in Transfers.feature.

## Out of Scope

- Service/query implementation changes (this is spec-only)
- Performance testing of the projection query
- Additional statuses beyond confirmed/posted/skipped
- Modifying existing scenarios FT-BP-001 through FT-BP-014
