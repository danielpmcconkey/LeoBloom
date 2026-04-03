# BDD-006: Void Journal Entry

**Project:** 006 — Void Journal Entry
**Author:** Basement Dweller (BA role)
**Date:** 2026-04-03
**Status:** Draft — Awaiting PO Approval
**BRD:** Project006-brd.md (Approved 2026-04-03)

---

## Scenario Index

| # | Scenario | Category | AC |
|---|----------|----------|----|
| 1 | Void an active entry successfully | Happy path | AC-001, AC-008 |
| 2 | Voided entry remains in the database | State verification | AC-002 |
| 3 | Lines and references are intact after void | State verification | AC-003 |
| 4 | Void an already-voided entry is idempotent | Idempotency | AC-004 |
| 5 | Empty void reason is rejected | Validation | AC-005 |
| 6 | Whitespace-only void reason is rejected | Validation | AC-005 |
| 7 | Nonexistent entry ID is rejected | Validation | AC-006 |
| 8 | Void succeeds in a closed fiscal period | Edge case | AC-007 |

---

## Feature: Void Journal Entry — Happy Path

### Scenario 1: Void an active entry successfully

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
And a posted journal entry with description "March rent" in period "2026-03"
  | account | amount  | entry_type |
  | 1010    | 1000.00 | debit      |
  | 4010    | 1000.00 | credit     |
When I void the entry with reason "Duplicate posting"
Then the result is Ok
And the returned entry has voided_at set to a recent timestamp
And the returned entry has void_reason = "Duplicate posting"
And the returned entry has modified_at updated
```

**Verifies:** AC-001 (voided_at and void_reason set), AC-008 (returns Result<JournalEntry, string list>)

---

## Feature: Void Journal Entry — State Verification

### Scenario 2: Voided entry remains in the database

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
And a posted journal entry with description "To be voided" in period "2026-03"
  | account | amount  | entry_type |
  | 1010    | 500.00  | debit      |
  | 4010    | 500.00  | credit     |
When I void the entry with reason "Error correction"
Then the result is Ok
And the journal_entry row still exists in the database
And the entry has voided_at IS NOT NULL
And the entry has description = "To be voided"
```

**Verifies:** AC-002 (entry not deleted, remains readable)

### Scenario 3: Lines and references are intact after void

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
And a posted journal entry with description "Entry with refs" in period "2026-03"
  | account | amount  | entry_type |
  | 1010    | 750.00  | debit      |
  | 4010    | 750.00  | credit     |
And the entry has references:
  | reference_type | reference_value |
  | cheque         | 5678            |
When I void the entry with reason "Wrong amount"
Then the result is Ok
And 2 journal_entry_line rows exist for the entry
And 1 journal_entry_reference row exists for the entry
And the lines have unchanged account_id, amount, and entry_type
And the reference has type "cheque" and value "5678"
```

**Verifies:** AC-003 (lines and references unaffected)

---

## Feature: Void Journal Entry — Idempotency

### Scenario 4: Void an already-voided entry is idempotent

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
And a posted journal entry with description "Double void test" in period "2026-03"
  | account | amount  | entry_type |
  | 1010    | 200.00  | debit      |
  | 4010    | 200.00  | credit     |
And the entry has been voided with reason "First void"
And I record the voided_at timestamp and modified_at timestamp
When I void the entry again with reason "Second void attempt"
Then the result is Ok
And the returned entry has void_reason = "First void"
And the returned entry has the original voided_at timestamp (unchanged)
And the returned entry has the original modified_at timestamp (unchanged)
```

**Verifies:** AC-004 (idempotent — no-op on re-void, preserves original timestamps and reason)

---

## Feature: Void Journal Entry — Validation

### Scenario 5: Empty void reason is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
And a posted journal entry with description "Reason test" in period "2026-03"
  | account | amount  | entry_type |
  | 1010    | 100.00  | debit      |
  | 4010    | 100.00  | credit     |
When I void the entry with reason ""
Then the result is Error
And the error messages contain "reason" and "required" or "empty"
And the entry remains un-voided in the database
```

**Verifies:** AC-005 (empty reason rejected)

### Scenario 6: Whitespace-only void reason is rejected

```gherkin
Given a valid fiscal period "2026-03" from 2026-03-01 to 2026-03-31 that is open
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
And a posted journal entry with description "Whitespace test" in period "2026-03"
  | account | amount  | entry_type |
  | 1010    | 100.00  | debit      |
  | 4010    | 100.00  | credit     |
When I void the entry with reason "   "
Then the result is Error
And the error messages contain "reason" and "required" or "empty"
And the entry remains un-voided in the database
```

**Verifies:** AC-005 (whitespace-only reason rejected)

### Scenario 7: Nonexistent entry ID is rejected

```gherkin
When I void entry ID 999999 with reason "Does not exist"
Then the result is Error
And the error messages contain "does not exist"
```

**Verifies:** AC-006 (nonexistent entry rejected)

---

## Feature: Void Journal Entry — Edge Cases

### Scenario 8: Void succeeds in a closed fiscal period

```gherkin
Given a fiscal period "2025-12" from 2025-12-01 to 2025-12-31 that is closed
And an active account "1010" of type "asset"
And an active account "4010" of type "revenue"
And a posted journal entry with description "Old entry" in period "2025-12"
  | account | amount  | entry_type |
  | 1010    | 300.00  | debit      |
  | 4010    | 300.00  | credit     |
When I void the entry with reason "Late correction"
Then the result is Ok
And the returned entry has voided_at set to a recent timestamp
And the returned entry has void_reason = "Late correction"
```

**Verifies:** AC-007 (voiding in closed period is allowed — metadata update, not new posting)

**Implementation note:** This scenario requires posting the entry while the period
is still open, then closing the period, then voiding. The setup steps must handle
this sequence. Alternatively, the entry can be inserted via raw SQL with the
closed period's ID to avoid needing the period-open-then-close dance.

---

## Coverage Matrix

| AC | Scenarios | Covered |
|----|-----------|---------|
| AC-001 | 1 | Yes |
| AC-002 | 2 | Yes |
| AC-003 | 3 | Yes |
| AC-004 | 4 | Yes |
| AC-005 | 5, 6 | Yes |
| AC-006 | 7 | Yes |
| AC-007 | 8 | Yes |
| AC-008 | 1 | Yes |
