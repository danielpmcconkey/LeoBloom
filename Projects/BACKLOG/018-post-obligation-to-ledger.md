# 018 — Post Obligation to Ledger

**Epic:** F — Obligation → Ledger Posting
**Depends On:** 005, 016
**Status:** Not started

---

When an obligation instance transitions to `posted`, create a journal entry from
the agreement's terms and link it back.

**Mechanics:**

1. Instance must be in `confirmed` status with `amount` set and `confirmed_date`
   set.
2. Look up the parent `obligation_agreement` for `source_account_id` and
   `dest_account_id`.
3. Determine debit/credit sides:
   - **Receivable** (money coming in): debit `dest_account_id` (the asset
     receiving cash), credit `source_account_id` (the revenue account).
   - **Payable** (money going out): debit `dest_account_id` (the expense
     account), credit `source_account_id` (the asset paying cash).
4. Create a journal entry via Story 005's posting engine:
   - `entry_date` = `instance.confirmed_date` (cash basis — when the money moved)
   - `description` = `"{agreement.name} — {instance.name}"`
   - `source` = `'obligation'`
   - `fiscal_period_id` = the period containing `confirmed_date`
   - Two lines: one debit, one credit, both for `instance.amount`
5. Set `instance.journal_entry_id` to the new entry's ID.
6. Transition instance status to `posted`.

**This reuses the posting engine.** All of Story 005's validation applies: the
fiscal period must be open, accounts must be active, the entry must balance.
If any validation fails, the post-to-ledger operation fails and the instance
stays in `confirmed` status.

**Edge cases CE should address in the BRD:**

- Agreement with no source or dest account → cannot post. Reject with clear
  error ("agreement missing source/dest account, cannot create journal entry").
- Source or dest account is inactive → rejected by posting engine. The agreement
  needs updating before this instance can be posted.
- Fiscal period for confirmed_date is closed → rejected by posting engine. Reopen
  the period first.
- Should this auto-create references? E.g., if the instance has a
  `document_path` (bill scan), should the journal entry get a reference? **Yes —
  create a reference with `reference_type = 'obligation'` and `reference_value =
  instance.id`** so there's a traceable link from the ledger back to ops.

**DataModelSpec references:** `obligation_instance.journal_entry_id`, cross-schema
relationships section. The ops→ledger one-way dependency.
