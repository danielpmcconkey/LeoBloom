Feature: Post Journal Entry
    The core write path for the ledger. A journal entry with its lines
    and optional references is validated and persisted atomically.

    # --- Happy Path ---

    Scenario: Simple 2-line entry posts successfully
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-03-15 described as "Brian March rent" with source "manual" and lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        Then the post succeeds with 2 lines and valid id and timestamps

    Scenario: Compound 3-line entry posts successfully
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 5010 of type expense
        And an active account 2010 of type liability
        And an active account 1010 of type asset
        When I post a journal entry dated 2026-03-01 described as "Mortgage payment March" with source "manual" and lines:
            | account | amount  | entry_type |
            | 5010    | 800.00  | debit      |
            | 2010    | 700.00  | debit      |
            | 1010    | 1500.00 | credit     |
        Then the post succeeds with 3 lines

    Scenario: Entry with references posts successfully
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        And pending references:
            | reference_type     | reference_value |
            | cheque             | 1234            |
            | zelle_confirmation | ZEL-9876        |
        When I post a journal entry dated 2026-03-15 described as "Brian March rent" with source "manual" and lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        Then the post succeeds with 2 references

    Scenario: Entry with null source posts successfully
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-03-15 described as "Brian March rent" with no source and lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        Then the post succeeds with null source

    Scenario: Entry with memo on lines posts successfully
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-03-15 described as "Brian March rent" with source "manual" and lines with memo:
            | account | amount  | entry_type | memo          |
            | 1010    | 1000.00 | debit      | Cash received |
            | 4010    | 1000.00 | credit     | Rent income   |
        Then the post succeeds with memo values on all lines

    # --- Pure Validation ---

    Scenario: Unbalanced entry is rejected
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-03-15 described as "Bad entry" with source "manual" and lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 500.00  | credit     |
        Then the post fails with error containing "do not equal"

    Scenario: Zero amount is rejected
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-03-15 described as "Bad entry" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 0.00   | debit      |
            | 4010    | 0.00   | credit     |
        Then the post fails with error containing "non-positive amount"

    Scenario: Negative amount is rejected
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-03-15 described as "Bad entry" with source "manual" and lines:
            | account | amount  | entry_type |
            | 1010    | -100.00 | debit      |
            | 4010    | -100.00 | credit     |
        Then the post fails with error containing "non-positive amount"

    Scenario: Single-line entry is rejected
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        When I post a journal entry dated 2026-03-15 described as "Bad entry" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 100.00 | debit      |
        Then the post fails with error containing "at least 2 lines"

    Scenario: Empty description is rejected
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-03-15 described as "" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 100.00 | debit      |
            | 4010    | 100.00 | credit     |
        Then the post fails with error containing "Description"

    Scenario: Invalid entry_type is rejected
        Given the ledger schema exists for posting
        When I attempt to parse entry_type "foo"
        Then the entry_type parse result is Error

    Scenario: Empty source string is rejected
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-03-15 described as "Test entry" with source "" and lines:
            | account | amount | entry_type |
            | 1010    | 100.00 | debit      |
            | 4010    | 100.00 | credit     |
        Then the post fails with error containing "Source"

    # --- DB-Dependent Validation ---

    Scenario: Closed fiscal period is rejected
        Given the ledger schema exists for posting
        And a closed fiscal period from 2025-12-01 to 2025-12-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2025-12-15 described as "Late entry" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 100.00 | debit      |
            | 4010    | 100.00 | credit     |
        Then the post fails with error containing "not open"

    Scenario: Entry date outside period range is rejected
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-04-15 described as "Wrong period" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 100.00 | debit      |
            | 4010    | 100.00 | credit     |
        Then the post fails with error containing "outside"

    Scenario: Inactive account is rejected
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an inactive account 9999 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-03-15 described as "Inactive acct entry" with source "manual" and lines:
            | account | amount | entry_type |
            | 9999    | 100.00 | debit      |
            | 4010    | 100.00 | credit     |
        Then the post fails with error containing "inactive"

    Scenario: Nonexistent fiscal period is rejected
        Given the ledger schema exists for posting
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry with nonexistent period 99999 dated 2030-01-15 described as "Future entry" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 100.00 | debit      |
            | 4010    | 100.00 | credit     |
        Then the post fails with error containing "does not exist"

    # --- Reference Validation ---

    Scenario: Empty reference type is rejected
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        And pending references:
            | reference_type | reference_value |
            |                | ABC-123         |
        When I post a journal entry dated 2026-03-15 described as "Test entry" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 100.00 | debit      |
            | 4010    | 100.00 | credit     |
        Then the post fails with error containing "reference_type"

    Scenario: Empty reference value is rejected
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        And pending references:
            | reference_type | reference_value |
            | cheque         |                 |
        When I post a journal entry dated 2026-03-15 described as "Test entry" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 100.00 | debit      |
            | 4010    | 100.00 | credit     |
        Then the post fails with error containing "reference_value"

    # --- Atomicity ---

    Scenario: Validation failure leaves no persisted rows
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-03-15 described as "Bad entry" with source "manual" and lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 500.00  | credit     |
        Then the post fails and no rows persisted for "Bad entry"

    # --- Edge Cases ---

    Scenario: Duplicate references across entries are allowed
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        And an existing journal entry with reference type "cheque" and value "1234"
        And pending references:
            | reference_type | reference_value |
            | cheque         | 1234            |
        When I post a journal entry dated 2026-03-20 described as "Replacement entry" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        Then the post succeeds

    Scenario: Future entry_date with valid open period succeeds
        Given the ledger schema exists for posting
        And a valid open fiscal period from 2026-06-01 to 2026-06-30
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-06-15 described as "Pre-dated expected pmt" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 200.00 | debit      |
            | 4010    | 200.00 | credit     |
        Then the post succeeds
