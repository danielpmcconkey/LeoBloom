# 010 — Opening Balances

**Epic:** C — Fiscal Period Management
**Depends On:** 005, 007
**Status:** Not started

---

Bootstrap the system with initial account balances. This is the go-live moment
for the ledger.

**How it works:** Opening balances are just a journal entry. No special
mechanism — the double-entry system handles it natively.

For each account with a non-zero starting balance:
- If the account is normal-debit (asset, expense): debit the account.
- If the account is normal-credit (liability, revenue, equity): credit the account.
- The other side of every line goes to an "Opening Balance Equity" account
  (a 3xxx equity account — must exist in the COA).
- The entry balances because: all the account-side lines sum to X, and the
  Opening Balance Equity line is X on the opposite side.

**This is a one-time operation.** Run it once at go-live. If there's a mistake,
void the opening balance entry and create a new one.

**Inputs:** A list of `(account_id, balance_amount)` pairs + the `entry_date`
(probably the first day of the first fiscal period, e.g., 2026-04-01).

**Validation:**

- All accounts must be active.
- The fiscal period for entry_date must exist and be open.
- The resulting journal entry must balance (the Opening Balance Equity line is
  computed, not provided — it's the plug that makes it balance).
- The Opening Balance Equity account must exist in the COA.

**Edge cases CE should address in the BRD:**

- What if some accounts don't have opening balances? Leave them out. Zero-balance
  accounts don't need lines.
- What if Opening Balance Equity ends up with a non-zero balance after this?
  That's expected — it represents the net worth at go-live. Over time, as real
  entries accumulate, it becomes less significant.
- Can this be run more than once? Technically yes (it's just a journal entry),
  but the BRD should note it's designed as a one-time operation. Running it
  twice would double the balances. Void the first one before creating a second.

**Dependency on Story 007:** We need account balance calculation to *verify* the
opening balances are correct after posting. Post the entry (005), then check
each account's balance (007) matches the intended starting state.
