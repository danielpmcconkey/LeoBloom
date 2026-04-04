# 022 — Balance Projection

**Epic:** I — Balance Projection
**Depends On:** 007, 015, 019
**Status:** Not started

---

Compute projected balance for an account over a future date range.

**The formula (from DataModelSpec key query #6):**

```
projected_balance(account, date) =
    current_balance(account, today)                           # Story 007
  + expected_inflows(account, today → date)                   # receivable instances
  − expected_outflows(account, today → date)                  # payable instances
  − in_flight_transfers_out(account, today → date)            # initiated, from_account = this
  + in_flight_transfers_in(account, today → date)             # initiated, to_account = this
```

**Expected inflows:** Active `obligation_instance` rows where:
- Parent agreement is `receivable`
- Parent agreement's `dest_account_id` = the target account
- Instance `status` is `expected` or `in_flight`
- Instance `expected_date` is between today and the projection date

**Expected outflows:** Same logic for `payable` agreements where
`source_account_id` = the target account.

**In-flight transfers:** Active `transfer` rows where `status = 'initiated'`
and either `from_account_id` or `to_account_id` = the target account.

**Inputs:** `account_id` (or code) + `projection_date` (how far forward).
**Output:** A daily or periodic series showing the projected balance at each
point. Not just the final number — Dan wants to see the curve.

**This is computed, not stored.** Every call recalculates from current state.

**Edge cases CE should address in the BRD:**

- Variable-amount obligations with null amount → flag as uncertainty. Show them
  in the output as "unknown outflow on [date]" rather than omitting them.
  Don't guess the amount.
- Account with no future obligations or transfers → flat line at current balance.
- Multiple obligations hitting on the same day → sum them. Show the itemized
  breakdown and the net effect.
- Projection date in the past → reject or return current balance? **Reject.** Use
  account balance (Story 007) for historical queries.

**DataModelSpec references:** Key query #6.
