# 014 — Obligation Agreements

**Epic:** E — Obligation Lifecycle
**Depends On:** 004
**Status:** Not started

---

CRUD for obligation agreements. These are the standing arrangements: "Jeffrey pays
$1,000 rent monthly," "Enbridge sends a gas bill monthly (variable amount),"
"Property insurance is $1,500 annually."

**Create:** Persist an `obligation_agreement` with all fields from the
DataModelSpec. Validate:

- `obligation_type_id` must reference a valid obligation_type (receivable or
  payable).
- `cadence_id` must reference a valid cadence.
- `payment_method_id` (if set) must reference a valid payment_method.
- `source_account_id` (if set) must reference an active ledger account.
- `dest_account_id` (if set) must reference an active ledger account.
- `amount` is nullable — null means variable (metered utilities, etc.).
- `counterparty` is a free-form string. No validation beyond non-empty for
  readability.

**Update:** All fields are mutable. When `amount` changes (e.g., escrow
adjustment, new contract rate), future instances get the new amount. Past
instances are untouched — they represent what actually happened.

**Deactivate:** Set `is_active = false`. Never delete. Existing instances remain.

**Edge cases CE should address in the BRD:**

- Agreement with no source or dest account → valid. Some agreements are tracked
  for nagging purposes before the accounting is wired up.
- Agreement where source and dest are the same account → reject? Or allow?
  **Decision:** reject. That's meaningless.
- Changing obligation_type (receivable ↔ payable) on an existing agreement with
  instances → allowed, but the BRD should note that existing instances retain
  their semantics. This is a "you know what you're doing" operation.

**DataModelSpec references:** `obligation_agreement` table, all columns.
