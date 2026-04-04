# 021 — Generate Invoice

**Epic:** H — Invoice Generation
**Depends On:** 020
**Status:** Not started

---

Create the invoice record for a tenant + fiscal period.

**Mechanics:**

1. Verify readiness (Story 020). If not ready, reject.
2. For the target `fiscal_period_id`, calculate total utilities:
   - Sum the `amount` of all confirmed/posted `obligation_instance` rows for
     variable-amount payable agreements with dest accounts under 5xxx, with
     `expected_date` in the target month.
3. `utility_share` = total_utilities / 3 (three tenants, equal split).
4. `rent_amount` = from the tenant's receivable agreement (the agreement where
   `counterparty` = tenant name, `obligation_type` = receivable, `cadence` =
   monthly). This is the fixed amount from the agreement.
5. `total_amount` = `rent_amount` + `utility_share`.
6. Persist the `invoice` record.

**Unique constraint:** One invoice per `(tenant, fiscal_period_id)`. Reject
duplicates.

**Current tenants (from DataModelSpec):** Jeffrey, Alice, Matthew. Matthew's rent is
$0 (living arrangement — the invoice still exists for the utility share).

**Edge cases CE should address in the BRD:**

- Utility share results in fractional cents (e.g., $150.01 / 3) → round to
  nearest cent. Rounding differences (total of rounded shares != original total)
  are a real thing. **Decision:** round each share normally. Accept the
  penny discrepancy. Don't over-engineer.
- Matthew's rent is $0 but utility share is non-zero → valid invoice. `total_amount`
  = `utility_share`.
- What if a tenant has no receivable agreement? → error. Every tenant must have
  an agreement for invoicing to work.
- Regenerating an invoice (same tenant + period) → reject. Void the existing
  invoice (set `is_active = false`) and create a new one if needed.
- `document_path` is null at creation time. PDF generation is beyond the horizon.

**DataModelSpec references:** `invoice` table, all columns and invariants.
