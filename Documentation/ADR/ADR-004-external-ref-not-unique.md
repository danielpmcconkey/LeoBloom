# ADR-004: External Account Reference Is Not Unique

**Date:** 2026-04-08
**Status:** Accepted
**Decision by:** Dan McConkey (product owner), Hobson (comptroller)

---

## Context

P075 proposes adding an `external_ref` column to `ledger.account` to store
financial institution account numbers (e.g., Fidelity Z08806967, Ally
1149850412). The question arose whether this column should carry a UNIQUE
constraint.

## Decision

`external_ref` is **not unique**. No UNIQUE constraint, no unique index.

## Rationale

Some financial institutions present a single account number for what
LeoBloom tracks as multiple ledger accounts. The known case today:

- **T. Rowe Price 401(k):** One account at the FI, but LeoBloom tracks
  two ledger accounts — pre-tax (planned 1320) and Roth (planned 1330) —
  because they have different tax treatment and Dan tracks balances
  separately. Both would share the same external reference.

- **HealthEquity HSA:** One account at the FI, but LeoBloom may track
  two ledger accounts — cash reserve and invested balance — because they
  have different subtypes (Cash vs Investment).

A UNIQUE constraint would force artificial disambiguation (appending
"-pretax" / "-roth" to the account number), which misrepresents the
actual FI account number and defeats the purpose of the field.

## Consequences

- **CSV/statement import:** External ref alone is not sufficient to
  identify a unique ledger account. Import workflows that match on
  external_ref may return multiple accounts and will need a secondary
  discriminator (account name, subtype, or explicit user selection).
  This is a reconciliation workflow concern, not a schema concern.

- **Lookups:** `WHERE external_ref = ?` may return multiple rows. Code
  that queries by external_ref must handle this (use `find` semantics,
  not `get`).

- **Nullability:** The column is also nullable. Header accounts, equity
  accounts, and the transfer clearing account have no external reference.
  Null does not mean "unknown" — it means "not applicable."
