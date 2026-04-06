# 019 — Create and Confirm Transfers

**Epic:** G — Transfer Management
**Depends On:** 005, 007
**Status:** Not started

---

**Initiate a transfer:**

- Create a `transfer` record with `status = 'initiated'`.
- Both `from_account_id` and `to_account_id` must reference active ledger
  accounts of type `asset`. No transferring from a revenue account.
- `from_account_id` != `to_account_id`.
- `initiated_date` = when the transfer was started.
- `expected_settlement` = `initiated_date` + 3 business days (default for ACH).
  Nullable — some transfers settle instantly.
- **No journal entry is created.** In-flight money hasn't arrived yet. It doesn't
  affect balances.

**Confirm a transfer:**

- Set `status = 'confirmed'`, `confirmed_date` = when the money arrived.
- Create a journal entry via Story 005's posting engine:
  - Debit `to_account_id` (money arriving increases the asset).
  - Credit `from_account_id` (money leaving decreases the asset).
  - `entry_date` = `confirmed_date`.
  - `source` = `'transfer'`.
  - `description` = `transfer.description` or auto-generate.
- Set `transfer.journal_entry_id` to the new entry's ID.

**In-flight tracking (DataModelSpec key query #5):**

All active `transfer` rows where `status = 'initiated'`. This feeds into
balance projection (Epic I) — in-flight transfers reduce available cash even
though they haven't posted to the ledger yet.

**Edge cases CE should address in the BRD:**

- Confirm a transfer that was never initiated → reject. Must be `initiated`.
- Cancel a transfer? **Use `is_active = false`.** Set it inactive with a note.
  No status rollback.
- Transfer where from_account has insufficient balance → allow it. The ledger
  is a record of what happened, not a constraint engine. If the CMA goes
  negative, that's a real problem Dan deals with — Leo Bloom just records it.
- Should a reference be created linking the journal entry back to the transfer?
  **Yes — `reference_type = 'transfer'`, `reference_value = transfer.id`.**

**DataModelSpec references:** `transfer` table, key query #5.
