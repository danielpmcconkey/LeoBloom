Feature: Account Balance
    Calculate the balance of a single account as of a given date.
    Balance = debits minus credits for normal-debit accounts,
    credits minus debits for normal-credit accounts.
    Voided entries and entries after the as-of date are excluded.

    # --- Happy Path ---

    Scenario: Normal-debit account balance after single entry
        Given the ledger schema exists for balance queries
        And a balance-test open fiscal period from 2026-03-01 to 2026-03-31
        And a balance-test active account 1010 of type asset
        And a balance-test active account 4010 of type revenue
        And a balance-test entry dated 2026-03-15 described as "March rent" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        When I query the balance of account 1010 as of 2026-03-31
        Then the balance is 1000.00 for a normal-debit account with code "1010"

    Scenario: Normal-credit account balance after single entry
        Given the ledger schema exists for balance queries
        And a balance-test open fiscal period from 2026-03-01 to 2026-03-31
        And a balance-test active account 1010 of type asset
        And a balance-test active account 4010 of type revenue
        And a balance-test entry dated 2026-03-15 described as "March rent" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        When I query the balance of account 4010 as of 2026-03-31
        Then the balance is 1000.00 for a normal-credit account with code "4010"

    # --- Accumulation ---

    Scenario: Balance accumulates across multiple entries
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
        Then the balance result is exactly 800.00

    Scenario: Mixed debits and credits net correctly
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
            | 5010    | 400.00 | debit      |
            | 1010    | 400.00 | credit     |
        When I query the balance of account 1010 as of 2026-03-31
        Then the balance result is exactly 600.00

    # --- Filtering ---

    Scenario: Voided entry excluded from balance
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
        And the balance-test entry "Bad entry" has been voided
        When I query the balance of account 1010 as of 2026-03-31
        Then the balance result is exactly 500.00

    Scenario: Entry after as_of_date excluded from balance
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
        Then the balance result is exactly 500.00

    # --- Edge Cases ---

    Scenario: Account with no entries has zero balance
        Given the ledger schema exists for balance queries
        And a balance-test active account 1010 of type asset
        When I query the balance of account 1010 as of 2026-03-31
        Then the balance result is exactly 0.00

    Scenario: Inactive account balance is calculated
        Given the ledger schema exists for balance queries
        And a balance-test open fiscal period from 2026-03-01 to 2026-03-31
        And a balance-test active account 1010 of type asset
        And a balance-test active account 4010 of type revenue
        And a balance-test entry dated 2026-03-15 described as "Before deactivation" with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        And balance-test account 1010 is now deactivated
        When I query the balance of account 1010 as of 2026-03-31
        Then the balance result is exactly 500.00

    # --- Validation ---

    Scenario: Nonexistent account ID returns error
        Given the ledger schema exists for balance queries
        When I query the balance of account ID 999999 as of 2026-03-31
        Then the balance result is Error containing "does not exist"

    Scenario: Nonexistent account code returns error
        Given the ledger schema exists for balance queries
        When I query the balance of account code "ZZZZ" as of 2026-03-31
        Then the balance result is Error containing "does not exist"

    # --- Code Lookup ---

    Scenario: Lookup by account code matches lookup by ID
        Given the ledger schema exists for balance queries
        And a balance-test open fiscal period from 2026-03-01 to 2026-03-31
        And a balance-test active account 1010 of type asset
        And a balance-test active account 4010 of type revenue
        And a balance-test entry dated 2026-03-15 described as "Code lookup test" with lines:
            | account | amount | entry_type |
            | 1010    | 750.00 | debit      |
            | 4010    | 750.00 | credit     |
        When I query the balance of account 1010 by code as of 2026-03-31
        Then the balance is 750.00 for a normal-debit account with code "1010"
