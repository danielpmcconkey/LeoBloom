# 075 — External Account Reference on ledger.account

**Epic:** Ledger
**Depends On:** None
**Status:** Not started
**Priority:** Medium

---

## Problem Statement

`ledger.account` has no field for the external financial institution account
number (e.g., Fidelity Z08806967, Ally x0412). These numbers are needed for:

- Mapping imported CSVs/statements to ledger accounts
- Reconciliation
- Unambiguous identification when account names are similar

Currently tracked in a manual reference doc (`HobsonsNotes/procedures/fi-account-numbers.md`).

## GAAP Considerations

GAAP does not prescribe chart of accounts structure at this level of detail.
An external reference field is an operational convenience, not an accounting
requirement. It has no bearing on debits, credits, normal balance, or
financial statement presentation. It is metadata — similar to `notes` or
`counterparty` on obligation agreements.

The field should be:

- **Nullable** — not all accounts have external references (header accounts,
  equity accounts, the transfer clearing account).
- **Not unique** — some FIs use a single account number for what we track as
  multiple ledger accounts (e.g., T. Rowe Price shows one 401(k) but we split
  into pre-tax and Roth).
- **Varchar, not integer** — account numbers contain letters, dashes, and
  leading zeros (Z08806967, SECU-3015584).

## Acceptance Criteria

1. `ledger.account` has a new nullable `external_ref varchar(50)` column.
2. Domain type updated with `externalRef: string option`.
3. DU validation: no type-level constraints (any account type can have one).
4. Migration adds the column. No seed data — Hobson populates prod directly.
5. Existing tests unaffected (field is optional).
