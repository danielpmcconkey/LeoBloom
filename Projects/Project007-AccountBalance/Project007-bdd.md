# BDD-007: Account Balance

**Project:** 007 — Account Balance
**Author:** Basement Dweller (BA role)
**Date:** 2026-04-03
**Status:** Draft — Awaiting PO Approval
**BRD:** Project007-brd.md (Approved 2026-04-03)

---

## Scenario Index

| # | Scenario | Category | AC |
|---|----------|----------|----|
| 1 | Normal-debit account balance after single entry | Happy path | AC-001, AC-011 |
| 2 | Normal-credit account balance after single entry | Happy path | AC-002, AC-011 |
| 3 | Balance accumulates across multiple entries | Accumulation | AC-003 |
| 4 | Voided entry excluded from balance | Filtering | AC-004 |
| 5 | Entry after as_of_date excluded from balance | Filtering | AC-005 |
| 6 | Account with no entries has zero balance | Zero balance | AC-006 |
| 7 | Inactive account balance is calculated | Edge case | AC-007 |
| 8 | Nonexistent account ID returns error | Validation | AC-008 |
| 9 | Nonexistent account code returns error | Validation | AC-009 |
| 10 | Lookup by account code matches lookup by ID | Code lookup | AC-010 |
| 11 | Mixed debits and credits net correctly | Accumulation | AC-001, AC-003 |

---

## Feature: Account Balance — Happy Path

### Scenario 1: Normal-debit account balance after single entry

```gherkin
Given the ledger schema exists for balance queries
And a balance-test open fiscal period from 2026-03-01 to 2026-03-31
And a balance-test active account 1010 of type asset
And a balance-test active account 4010 of type revenue
And a balance-test entry dated 2026-03-15 described as "March rent" with lines:
    | account | amount  | entry_type |
    | 1010    | 1000.00 | debit      |
    | 4010    | 1000.00 | credit     |
When I query the balance of account 1010 as of 2026-03-31
Then the balance result is Ok with balance 1000.00 for a normal-debit account with code "1010"
```

**Verifies:** AC-001 (normal-debit: debits minus credits), AC-011 (result includes metadata)

### Scenario 2: Normal-credit account balance after single entry

```gherkin
Given the ledger schema exists for balance queries
And a balance-test open fiscal period from 2026-03-01 to 2026-03-31
And a balance-test active account 1010 of type asset
And a balance-test active account 4010 of type revenue
And a balance-test entry dated 2026-03-15 described as "March rent" with lines:
    | account | amount  | entry_type |
    | 1010    | 1000.00 | debit      |
    | 4010    | 1000.00 | credit     |
When I query the balance of account 4010 as of 2026-03-31
Then the balance result is Ok with balance 1000.00 for a normal-credit account with code "4010"
```

**Verifies:** AC-002 (normal-credit: credits minus debits), AC-011 (metadata)

---

## Feature: Account Balance — Accumulation

### Scenario 3: Balance accumulates across multiple entries

```gherkin
Given the ledger schema exists for balance queries
And a balance-test open fiscal period from 2026-03-01 to 2026-03-31
And a balance-test active account 1010 of type asset
And a balance-test active account 4010 of type revenue
And a balance-test entry dated 2026-03-10 described as "First payment" with lines:
    | account | amount | entry_type |
    | 1010    | 500.00 | debit      |
    | 4010    | 500.00 | credit     |
And a balance-test entry dated 2026-03-20 described as "Second payment" with lines:
    | account | amount | entry_type |
    | 1010    | 300.00 | debit      |
    | 4010    | 300.00 | credit     |
When I query the balance of account 1010 as of 2026-03-31
Then the balance result is Ok with balance 800.00
```

**Verifies:** AC-003 (multiple entries accumulate)

### Scenario 11: Mixed debits and credits net correctly

```gherkin
Given the ledger schema exists for balance queries
And a balance-test open fiscal period from 2026-03-01 to 2026-03-31
And a balance-test active account 1010 of type asset
And a balance-test active account 4010 of type revenue
And a balance-test active account 5010 of type expense
And a balance-test entry dated 2026-03-10 described as "Income" with lines:
    | account | amount  | entry_type |
    | 1010    | 1000.00 | debit      |
    | 4010    | 1000.00 | credit     |
And a balance-test entry dated 2026-03-20 described as "Expense" with lines:
    | 5010    | 400.00  | debit      |
    | 1010    | 400.00  | credit     |
When I query the balance of account 1010 as of 2026-03-31
Then the balance result is Ok with balance 600.00
```

**Verifies:** AC-001 (normal-debit netting), AC-003 (accumulation)

---

## Feature: Account Balance — Filtering

### Scenario 4: Voided entry excluded from balance

```gherkin
Given the ledger schema exists for balance queries
And a balance-test open fiscal period from 2026-03-01 to 2026-03-31
And a balance-test active account 1010 of type asset
And a balance-test active account 4010 of type revenue
And a balance-test entry dated 2026-03-10 described as "Good entry" with lines:
    | account | amount | entry_type |
    | 1010    | 500.00 | debit      |
    | 4010    | 500.00 | credit     |
And a balance-test entry dated 2026-03-15 described as "Bad entry" with lines:
    | account | amount | entry_type |
    | 1010    | 200.00 | debit      |
    | 4010    | 200.00 | credit     |
And the entry "Bad entry" has been voided with reason "Duplicate"
When I query the balance of account 1010 as of 2026-03-31
Then the balance result is Ok with balance 500.00
```

**Verifies:** AC-004 (voided entries excluded)

### Scenario 5: Entry after as_of_date excluded from balance

```gherkin
Given the ledger schema exists for balance queries
And a balance-test open fiscal period from 2026-03-01 to 2026-03-31
And a balance-test active account 1010 of type asset
And a balance-test active account 4010 of type revenue
And a balance-test entry dated 2026-03-10 described as "Early entry" with lines:
    | account | amount | entry_type |
    | 1010    | 500.00 | debit      |
    | 4010    | 500.00 | credit     |
And a balance-test entry dated 2026-03-25 described as "Late entry" with lines:
    | account | amount | entry_type |
    | 1010    | 300.00 | debit      |
    | 4010    | 300.00 | credit     |
When I query the balance of account 1010 as of 2026-03-15
Then the balance result is Ok with balance 500.00
```

**Verifies:** AC-005 (entries after as_of_date excluded)

---

## Feature: Account Balance — Edge Cases

### Scenario 6: Account with no entries has zero balance

```gherkin
Given the ledger schema exists for balance queries
And a balance-test active account 1010 of type asset
When I query the balance of account 1010 as of 2026-03-31
Then the balance result is Ok with balance 0.00
```

**Verifies:** AC-006 (zero balance for no entries)

### Scenario 7: Inactive account balance is calculated

```gherkin
Given the ledger schema exists for balance queries
And a balance-test open fiscal period from 2026-03-01 to 2026-03-31
And a balance-test active account 1010 of type asset
And a balance-test active account 4010 of type revenue
And a balance-test entry dated 2026-03-15 described as "Before deactivation" with lines:
    | account | amount | entry_type |
    | 1010    | 500.00 | debit      |
    | 4010    | 500.00 | credit     |
And account 1010 is now deactivated
When I query the balance of account 1010 as of 2026-03-31
Then the balance result is Ok with balance 500.00
```

**Verifies:** AC-007 (inactive accounts still have balances)

---

## Feature: Account Balance — Validation

### Scenario 8: Nonexistent account ID returns error

```gherkin
Given the ledger schema exists for balance queries
When I query the balance of account ID 999999 as of 2026-03-31
Then the balance result is Error containing "does not exist"
```

**Verifies:** AC-008 (nonexistent ID error)

### Scenario 9: Nonexistent account code returns error

```gherkin
Given the ledger schema exists for balance queries
When I query the balance of account code "ZZZZ" as of 2026-03-31
Then the balance result is Error containing "does not exist"
```

**Verifies:** AC-009 (nonexistent code error)

---

## Feature: Account Balance — Code Lookup

### Scenario 10: Lookup by account code matches lookup by ID

```gherkin
Given the ledger schema exists for balance queries
And a balance-test open fiscal period from 2026-03-01 to 2026-03-31
And a balance-test active account 1010 of type asset
And a balance-test active account 4010 of type revenue
And a balance-test entry dated 2026-03-15 described as "Code lookup test" with lines:
    | account | amount | entry_type |
    | 1010    | 750.00 | debit      |
    | 4010    | 750.00 | credit     |
When I query the balance of account 1010 by code as of 2026-03-31
Then the balance result is Ok with balance 750.00 for a normal-debit account with code "1010"
```

**Verifies:** AC-010 (code lookup produces same result as ID lookup)

---

## Coverage Matrix

| AC | Scenarios | Covered |
|----|-----------|---------|
| AC-001 | 1, 11 | Yes |
| AC-002 | 2 | Yes |
| AC-003 | 3, 11 | Yes |
| AC-004 | 4 | Yes |
| AC-005 | 5 | Yes |
| AC-006 | 6 | Yes |
| AC-007 | 7 | Yes |
| AC-008 | 8 | Yes |
| AC-009 | 9 | Yes |
| AC-010 | 10 | Yes |
| AC-011 | 1, 2 | Yes |
