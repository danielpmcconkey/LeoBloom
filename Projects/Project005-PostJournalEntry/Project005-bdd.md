# BDD-005: Post Journal Entry

**Project:** 005 — Post Journal Entry
**Author:** Basement Dweller (BA role)
**Date:** 2026-04-03
**Status:** Draft — Awaiting PO Approval
**BRD:** Project005-brd.md (Approved 2026-04-03)

---

## Scenario Index

| # | Scenario | Category | AC |
|---|----------|----------|----|
| 1 | Simple 2-line entry posts successfully | Happy path | AC-001, AC-007 |
| 2 | Compound 3-line entry posts successfully | Happy path | AC-002 |
| 3 | Entry with references posts successfully | Happy path | AC-003 |
| 4 | Entry with null source posts successfully | Happy path | AC-001 |
| 5 | Entry with memo on lines posts successfully | Happy path | AC-001 |
| 6 | Unbalanced entry is rejected | Validation | AC-004 |
| 7 | Zero amount is rejected | Validation | AC-004 |
| 8 | Negative amount is rejected | Validation | AC-004 |
| 9 | Single-line entry is rejected | Validation | AC-004 |
| 10 | Empty description is rejected | Validation | AC-004 |
| 11 | Invalid entry_type is rejected | Validation | AC-004 |
| 12 | Empty source string is rejected | Validation | AC-004 |
| 13 | Closed fiscal period is rejected | Validation | AC-005 |
| 14 | Entry date outside period range is rejected | Validation | AC-005 |
| 15 | Inactive account is rejected | Validation | AC-005 |
| 16 | Nonexistent fiscal period is rejected | Validation | AC-005 |
| 17 | Empty reference type is rejected | Validation | AC-004 |
| 18 | Empty reference value is rejected | Validation | AC-004 |
| 19 | Transaction rolls back on failure — no orphaned rows | Atomicity | AC-006 |
| 20 | Duplicate references across entries are allowed | Edge case | AC-003 |
| 21 | Future entry_date with valid open period succeeds | Edge case | AC-001 |

---

## Feature: Post Journal Entry — Happy Path

### Scenario 1: Simple 2-line entry posts successfully

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  | Brian March rent     |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 1000.00 | debit      |
  | 4010    | 1000.00 | credit     |
Then the result is Ok
And the returned entry has a database-assigned id > 0
And the returned entry has created_at and modified_at timestamps
And the journal_entry row exists in the database
And 2 journal_entry_line rows exist for the entry
And each line has the correct account_id, amount, and entry_type
```

**Verifies:** AC-001 (atomic 3-table persist), AC-007 (DB-assigned IDs/timestamps)

### Scenario 2: Compound 3-line entry posts successfully

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "5010" of type "expense"
And an active account "2010" of type "liability"
And an active account "1010" of type "asset"
When I post a journal entry with:
  | field        | value                    |
  | entry_date   | 2026-03-01               |
  | description  | Mortgage payment March   |
  | source       | manual                   |
  | period       | 2026-03                  |
And the entry has lines:
  | account | amount  | entry_type |
  | 5010    | 800.00  | debit      |
  | 2010    | 700.00  | debit      |
  | 1010    | 1500.00 | credit     |
Then the result is Ok
And 3 journal_entry_line rows exist for the entry
```

**Verifies:** AC-002 (compound entries)

### Scenario 3: Entry with references posts successfully

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  | Brian March rent     |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 1000.00 | debit      |
  | 4010    | 1000.00 | credit     |
And the entry has references:
  | reference_type       | reference_value |
  | cheque               | 1234            |
  | zelle_confirmation   | ZEL-9876        |
Then the result is Ok
And 2 journal_entry_reference rows exist for the entry
And the references have the correct type and value
```

**Verifies:** AC-003 (references persist)

### Scenario 4: Entry with null source posts successfully

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  | Brian March rent     |
  | source       |                      |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 1000.00 | debit      |
  | 4010    | 1000.00 | credit     |
Then the result is Ok
And the persisted entry has source = NULL
```

**Verifies:** AC-001 (PO-approved nullable source deviation)

### Scenario 5: Entry with memo on lines posts successfully

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  | Brian March rent     |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type | memo            |
  | 1010    | 1000.00 | debit      | Cash received   |
  | 4010    | 1000.00 | credit     | Rent income     |
Then the result is Ok
And the persisted lines have the correct memo values
```

**Verifies:** AC-001 (memo carried through write path)

---

## Feature: Post Journal Entry — Pure Validation

### Scenario 6: Unbalanced entry is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  | Bad entry            |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 1000.00 | debit      |
  | 4010    | 500.00  | credit     |
Then the result is Error
And the error messages contain "do not equal"
And no journal_entry row was persisted
```

**Verifies:** AC-004 (balance rule)

### Scenario 7: Zero amount is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  | Bad entry            |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount | entry_type |
  | 1010    | 0.00   | debit      |
  | 4010    | 0.00   | credit     |
Then the result is Error
And the error messages contain "non-positive amount"
```

**Verifies:** AC-004 (amount positivity)

### Scenario 8: Negative amount is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  | Bad entry            |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | -100.00 | debit      |
  | 4010    | -100.00 | credit     |
Then the result is Error
And the error messages contain "non-positive amount"
```

**Verifies:** AC-004 (amount positivity)

### Scenario 9: Single-line entry is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  | Bad entry            |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 100.00  | debit      |
Then the result is Error
And the error messages contain "at least 2 lines"
```

**Verifies:** AC-004 (minimum line count)

### Scenario 10: Empty description is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  |                      |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 100.00  | debit      |
  | 4010    | 100.00  | credit     |
Then the result is Error
And the error messages contain "Description" and "required" or "empty"
```

**Verifies:** AC-004 (description non-empty)

### Scenario 11: Invalid entry_type is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I attempt to create a line command with entry_type "foo"
Then the entry_type parsing fails with an error
```

**Verifies:** AC-004 (entry_type must be debit or credit)

**Implementation note:** Since `EntryType` is an F# DU, the command DTO will
accept string entry types and parse them. The test validates that invalid strings
are rejected at parse time before the command is even constructed. If the command
uses the DU directly, this scenario tests the string→DU conversion at the
boundary.

### Scenario 12: Empty source string is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  | Test entry           |
  | source       | (empty string)       |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 100.00  | debit      |
  | 4010    | 100.00  | credit     |
Then the result is Error
And the error messages contain "Source" and "empty"
```

**Verifies:** AC-004 (source, when provided, must be non-empty)

---

## Feature: Post Journal Entry — DB-Dependent Validation

### Scenario 13: Closed fiscal period is rejected

```gherkin
Given a fiscal period "2025-12" from 2025-12-01 to 2025-12-31 that is closed
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2025-12-15           |
  | description  | Late entry           |
  | source       | manual               |
  | period       | 2025-12              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 100.00  | debit      |
  | 4010    | 100.00  | credit     |
Then the result is Error
And the error messages contain "closed" or "not open"
And no journal_entry row was persisted
```

**Verifies:** AC-005 (period must be open)

### Scenario 14: Entry date outside period range is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-04-15           |
  | description  | Wrong period         |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 100.00  | debit      |
  | 4010    | 100.00  | credit     |
Then the result is Error
And the error messages contain "date" and "period" or "range"
```

**Verifies:** AC-005 (entry_date must fall within period's date range)

### Scenario 15: Inactive account is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an inactive account "9999" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  | Inactive acct entry  |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 9999    | 100.00  | debit      |
  | 4010    | 100.00  | credit     |
Then the result is Error
And the error messages contain "inactive" or "not active"
```

**Verifies:** AC-005 (accounts must be active)

### Scenario 16: Nonexistent fiscal period is rejected

```gherkin
Given an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
And no fiscal period exists for "2030-01"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2030-01-15           |
  | description  | Future entry         |
  | source       | manual               |
  | period       | 2030-01              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 100.00  | debit      |
  | 4010    | 100.00  | credit     |
Then the result is Error
And the error messages contain "fiscal period" and "not found" or "does not exist"
```

**Verifies:** AC-005 (period must exist)

---

## Feature: Post Journal Entry — Reference Validation

### Scenario 17: Empty reference type is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  | Test entry           |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 100.00  | debit      |
  | 4010    | 100.00  | credit     |
And the entry has references:
  | reference_type | reference_value |
  |                | ABC-123         |
Then the result is Error
And the error messages contain "reference_type" and "empty"
```

**Verifies:** AC-004 (reference fields non-empty)

### Scenario 18: Empty reference value is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-15           |
  | description  | Test entry           |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 100.00  | debit      |
  | 4010    | 100.00  | credit     |
And the entry has references:
  | reference_type | reference_value |
  | cheque         |                 |
Then the result is Error
And the error messages contain "reference_value" and "empty"
```

**Verifies:** AC-004 (reference fields non-empty)

---

## Feature: Post Journal Entry — Atomicity

### Scenario 19: Transaction rolls back on failure — no orphaned rows

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry that passes validation but fails during line insert
  (simulated by referencing a nonexistent account_id in the SQL layer)
Then the result is Error
And no journal_entry row was persisted for this attempt
And no journal_entry_line rows were persisted for this attempt
```

**Verifies:** AC-006 (atomicity — all or nothing)

**Implementation note:** The cleanest way to test this is to have validation
pass (using valid account IDs in the command) but then have the SQL insert fail
due to a FK violation on `account_id`. This can be achieved by passing an
account ID that was valid at validation time but gets deleted (within the test
transaction) before the insert. Alternatively, the test can verify that after
a validation failure, no rows exist — which is simpler and still proves the
service doesn't partially persist.

---

## Feature: Post Journal Entry — Edge Cases

### Scenario 20: Duplicate references across entries are allowed

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
And an existing journal entry with a reference of type "cheque" and value "1234"
When I post a new journal entry with:
  | field        | value                |
  | entry_date   | 2026-03-20           |
  | description  | Replacement entry    |
  | source       | manual               |
  | period       | 2026-03              |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 500.00  | debit      |
  | 4010    | 500.00  | credit     |
And the entry has references:
  | reference_type | reference_value |
  | cheque         | 1234            |
Then the result is Ok
And 2 journal_entry_reference rows with type "cheque" and value "1234" exist across all entries
```

**Verifies:** AC-003 (duplicate references across entries allowed — backlog edge case)

### Scenario 21: Future entry_date with valid open period succeeds

```gherkin
Given a valid fiscal period "2026-06" from 2026-06-01 to 2026-06-30 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
When I post a journal entry with:
  | field        | value                    |
  | entry_date   | 2026-06-15               |
  | description  | Pre-dated expected pmt   |
  | source       | manual                   |
  | period       | 2026-06                  |
And the entry has lines:
  | account | amount  | entry_type |
  | 1010    | 200.00  | debit      |
  | 4010    | 200.00  | credit     |
Then the result is Ok
And the persisted entry has entry_date = 2026-06-15
```

**Verifies:** AC-001 (future dates allowed — backlog edge case)

---

## Coverage Matrix

| AC | Scenarios | Covered |
|----|-----------|---------|
| AC-001 | 1, 4, 5 | Yes |
| AC-002 | 2 | Yes |
| AC-003 | 3 | Yes |
| AC-004 | 6, 7, 8, 9, 10, 11, 12, 17, 18 | Yes |
| AC-005 | 13, 14, 15, 16 | Yes |
| AC-006 | 19 | Yes |
| AC-007 | 1 | Yes |
| AC-008 | Manual verification — DataModelSpec updated | Yes |
