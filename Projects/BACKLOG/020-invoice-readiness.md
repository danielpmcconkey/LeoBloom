# 020 — Invoice Readiness Check

**Epic:** H — Invoice Generation
**Depends On:** 015, 017
**Status:** Not started

---

Before generating invoices for a month, verify that all the inputs are ready.

**The check (from DataModelSpec key query #4):**

For a target month (fiscal_period):
1. Find all active `obligation_agreement` rows where:
   - `obligation_type` = payable
   - `amount IS NULL` (variable — these are the utility bills)
   - `dest_account_id` is under the investment property expense subtree (5xxx).
     Determined by walking `parent_code` up to a 5xxx root.
2. For each, find the corresponding `obligation_instance` with `expected_date`
   in the target month.
3. Each instance must have `amount` set (the bill has arrived and the amount is
   known).

**Output:**
- `ready: true/false`
- If not ready: list of unready obligations (agreement name, counterparty,
  missing data — usually "amount not set, bill not received")

**Edge cases CE should address in the BRD:**

- No variable-amount obligations exist → always ready (rent-only invoices).
- Some instances don't exist yet (not spawned) → not ready. The instance must
  exist AND have an amount.
- An obligation is overdue (bill never came) → not ready. The invoice can't be
  generated until the amount is known, even if it's late.
- What about fixed-amount payable obligations (e.g., insurance)? They don't
  block readiness — their amount is known from the agreement.

**DataModelSpec references:** Key query #4, invoice invariants.
