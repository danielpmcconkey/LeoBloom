# BRD-007: Account Balance

**Project:** 007 — Account Balance
**Author:** Basement Dweller (BA role)
**Date:** 2026-04-03
**Status:** Draft — Awaiting PO Approval

---

## 1. Business Objective

Deliver the first read path for the LeoBloom ledger: a function that calculates
the balance of a single account as of a given date. This is the fundamental
financial query — every report, projection, and integration downstream depends
on it. Trial balance (008), P&L by subtree, balance projection — all built on
this.

## 2. Scope

### In Scope

1. **Result type** — `AccountBalance` record carrying:
   - `accountId: int`
   - `accountCode: string`
   - `accountName: string`
   - `normalBalance: NormalBalance` (debit or credit — provides context for
     interpreting the sign)
   - `balance: decimal` (signed — positive means balance is in the normal
     direction, negative means inverted)
   - `asOfDate: DateOnly`

2. **Repository layer** — a `getAccountBalance` function in a new
   `AccountBalanceRepository` module (or added to `JournalEntryRepository`,
   though a separate module is cleaner for a read concern). The query:
   - Joins `journal_entry_line` → `journal_entry` → `account` → `account_type`
   - Filters: `journal_entry.voided_at IS NULL` AND
     `journal_entry.entry_date <= @as_of_date` AND
     `journal_entry_line.account_id = @account_id`
   - Computes:
     - For normal-debit accounts: `SUM(debit amounts) - SUM(credit amounts)`
     - For normal-credit accounts: `SUM(credit amounts) - SUM(debit amounts)`
   - Returns `Result<AccountBalance, string>` — `Error` if account doesn't exist

3. **Service layer** — a `AccountBalanceService` module with two entry points:
   - `getBalanceById: account_id + as_of_date` — looks up by integer ID
   - `getBalanceByCode: account_code + as_of_date` — resolves code to ID, then
     calls the same query. Per backlog: "Inputs: account_id (or account_code)."
   - Both validate account exists (returns error if not)
   - Both return `Result<AccountBalance, string>`

4. **BDD tests** — integration tests in `LeoBloom.Dal.Tests` exercising:
   - Happy path: simple 2-line entry, check balance of debit-side account
   - Happy path: check balance of credit-side account (normal-credit)
   - Multiple entries: balance accumulates correctly
   - Voided entries excluded from balance
   - Date filtering: entry after as_of_date excluded
   - Zero balance: account with no entries returns 0.00
   - Inactive account: balance still calculated (is_active controls posting,
     not reading)
   - Nonexistent account (by ID): returns error
   - Nonexistent account (by code): returns error
   - Lookup by account code: same result as lookup by ID
   - Mixed entry types: account with both debits and credits nets correctly

### Out of Scope

- **Parent/subtree aggregation** — calling balance on account 5000 returns only
  lines posted directly to 5000. Child accounts are NOT included. Subtree
  aggregation is Epic D (P&L by subtree).
- **Trial balance** — that's 008, built on top of this.
- **API endpoints** — no HTTP layer. Service + Dal only.
- **Caching** — calculate fresh every time. Materialized views or caching are
  future optimization if needed.
- **Period-scoped balance** — this is "all entries up to date X", not "entries
  within period Y". Period-scoped activity is trial balance (008).

## 3. Dependencies

| Dependency | Status | Notes |
|-----------|--------|-------|
| 001 — Database schema | Done | Tables exist, joins established |
| 005 — Post journal entry | Done | Entries must exist to have balances |
| 004 — Domain types | Done | `Account`, `AccountType`, `NormalBalance`, `EntryType` |

## 4. Technical Approach

### 4.1 Project Structure

- `LeoBloom.Domain/Ledger.fs` — add `AccountBalance` result type
- `LeoBloom.Dal/AccountBalanceRepository.fs` — new file, single query function
- `LeoBloom.Dal/AccountBalanceService.fs` — new file, thin orchestration
- `LeoBloom.Dal.Tests/` — new feature file and step definitions

### 4.2 The Query

The balance calculation is a single SQL query with conditional aggregation:

```sql
SELECT
    a.id, a.code, a.name, at.normal_balance,
    COALESCE(SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE 0 END), 0)
        - COALESCE(SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END), 0)
        AS raw_balance
FROM ledger.account a
JOIN ledger.account_type at ON a.account_type_id = at.id
LEFT JOIN ledger.journal_entry_line jel ON jel.account_id = a.id
LEFT JOIN ledger.journal_entry je ON jel.journal_entry_id = je.id
    AND je.voided_at IS NULL
    AND je.entry_date <= @as_of_date
WHERE a.id = @account_id
GROUP BY a.id, a.code, a.name, at.normal_balance
```

The `raw_balance` is always `debits - credits`. For normal-debit accounts,
that's the final answer. For normal-credit accounts, negate it:
`balance = raw_balance * (if normal_balance = 'debit' then 1 else -1)`.

This keeps the SQL simple (one aggregation direction) and the sign convention
in application code where it's testable.

### 4.3 Why LEFT JOIN

An account with no entries (or no non-voided entries before as_of_date) should
return balance 0.00, not "not found". The LEFT JOIN + COALESCE ensures this.
The account row is always returned if it exists; the line aggregation just
produces zeros.

### 4.4 Why a Separate Module

`JournalEntryRepository` owns the write path. Account balance is a read concern
with a different shape (aggregate query, not row-level CRUD). Keeping them
separate prevents the repository from becoming a grab-bag.

## 5. Edge Cases (per Backlog)

| Case | Behavior |
|------|----------|
| Account with no entries | Balance is 0.00 |
| Inactive account | Calculate normally — is_active controls posting, not reading |
| as_of_date in the future | Valid — returns balance of all entries up to that date |
| Account ID doesn't exist | Error: "Account with id X does not exist" |
| Account code doesn't exist | Error: "Account with code X does not exist" |
| All entries voided | Balance is 0.00 (voided entries excluded) |
| Mixed debits and credits on same account | Net correctly per normal_balance direction |

## 6. Acceptance Criteria Summary

See BDD document (Project007-bdd.md) for full Gherkin scenarios. The BRD
criteria are:

- AC-001: Balance of a normal-debit account is calculated as debits minus credits
- AC-002: Balance of a normal-credit account is calculated as credits minus debits
- AC-003: Multiple entries accumulate correctly
- AC-004: Voided entries are excluded from the balance
- AC-005: Entries after as_of_date are excluded
- AC-006: Account with no entries returns balance 0.00
- AC-007: Inactive account balance is still calculated
- AC-008: Nonexistent account (by ID) returns a descriptive error
- AC-009: Nonexistent account (by code) returns a descriptive error
- AC-010: Lookup by account code returns the same result as lookup by ID
- AC-011: Result includes account metadata (code, name, normal_balance direction)
