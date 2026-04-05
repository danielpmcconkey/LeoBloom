Feature: Income Statement
    Produce an income statement for a fiscal period: sum revenue and expense
    activity from non-voided journal entries, compute a balance per account
    using the normal-balance formula, and report net income as total revenue
    minus total expenses. This is period-scoped activity only, not cumulative.

    # --- Happy Path ---

    @FT-IS-001
    Scenario: Period with revenue and expense activity produces correct net income
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 4010 of type revenue
        And an income-statement-test active account 5010 of type expense
        And an income-statement-test entry dated 2026-03-10 described as "Service income" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        And an income-statement-test entry dated 2026-03-20 described as "Office supplies" with lines:
            | account | amount | entry_type |
            | 5010    | 400.00 | debit      |
            | 1010    | 400.00 | credit     |
        When I request the income statement for period "2026-03"
        Then the revenue section total is 1000.00
        And the expenses section total is 400.00
        And the net income is 600.00

    # --- Section Filtering ---

    @FT-IS-002
    Scenario: Revenue only period shows revenue section with empty expenses
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 4010 of type revenue
        And an income-statement-test entry dated 2026-03-15 described as "Revenue only" with lines:
            | account | amount | entry_type |
            | 1010    | 750.00 | debit      |
            | 4010    | 750.00 | credit     |
        When I request the income statement for period "2026-03"
        Then the revenue section total is 750.00
        And the revenue section contains 1 line
        And the expenses section total is 0.00
        And the expenses section contains 0 lines

    @FT-IS-003
    Scenario: Expenses only period shows expense section with empty revenue
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 5010 of type expense
        And an income-statement-test entry dated 2026-03-15 described as "Expense only" with lines:
            | account | amount | entry_type |
            | 5010    | 300.00 | debit      |
            | 1010    | 300.00 | credit     |
        When I request the income statement for period "2026-03"
        Then the expenses section total is 300.00
        And the expenses section contains 1 line
        And the revenue section total is 0.00
        And the revenue section contains 0 lines

    # --- Filtering ---

    @FT-IS-004
    Scenario: Voided entries excluded from income statement
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 4010 of type revenue
        And an income-statement-test entry dated 2026-03-10 described as "Good income" with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        And an income-statement-test entry dated 2026-03-15 described as "Bad income" with lines:
            | account | amount | entry_type |
            | 1010    | 200.00 | debit      |
            | 4010    | 200.00 | credit     |
        And the income-statement-test entry "Bad income" has been voided
        When I request the income statement for period "2026-03"
        Then the revenue section total is 500.00
        And the net income is 500.00

    @FT-IS-005
    Scenario: Accounts with no activity in the period are omitted from sections
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 4010 of type revenue
        And an income-statement-test active account 4020 of type revenue
        And an income-statement-test entry dated 2026-03-15 described as "One revenue only" with lines:
            | account | amount | entry_type |
            | 1010    | 600.00 | debit      |
            | 4010    | 600.00 | credit     |
        When I request the income statement for period "2026-03"
        Then the revenue section contains 1 line
        And the revenue section does not contain account 4020

    @FT-IS-006
    Scenario: Inactive accounts with activity in the period still appear
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 4010 of type revenue
        And an income-statement-test entry dated 2026-03-15 described as "Before deactivation" with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        And income-statement-test account 4010 is now deactivated
        When I request the income statement for period "2026-03"
        Then the revenue section contains account 4010 with balance 500.00

    # --- Edge Cases ---

    @FT-IS-007
    Scenario: Empty period produces zero net income
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-04" from 2026-04-01 to 2026-04-30
        When I request the income statement for period "2026-04"
        Then the revenue section total is 0.00
        And the expenses section total is 0.00
        And the net income is 0.00
        And the revenue section contains 0 lines
        And the expenses section contains 0 lines

    @FT-IS-008
    Scenario: Net income is positive when revenue exceeds expenses
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 4010 of type revenue
        And an income-statement-test active account 5010 of type expense
        And an income-statement-test entry dated 2026-03-10 described as "Big income" with lines:
            | account | amount  | entry_type |
            | 1010    | 2000.00 | debit      |
            | 4010    | 2000.00 | credit     |
        And an income-statement-test entry dated 2026-03-20 described as "Small expense" with lines:
            | account | amount | entry_type |
            | 5010    | 500.00 | debit      |
            | 1010    | 500.00 | credit     |
        When I request the income statement for period "2026-03"
        Then the net income is 1500.00

    @FT-IS-009
    Scenario: Net loss when expenses exceed revenue
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 4010 of type revenue
        And an income-statement-test active account 5010 of type expense
        And an income-statement-test entry dated 2026-03-10 described as "Small income" with lines:
            | account | amount | entry_type |
            | 1010    | 300.00 | debit      |
            | 4010    | 300.00 | credit     |
        And an income-statement-test entry dated 2026-03-20 described as "Big expense" with lines:
            | account | amount  | entry_type |
            | 5010    | 800.00  | debit      |
            | 1010    | 800.00  | credit     |
        When I request the income statement for period "2026-03"
        Then the net income is -500.00

    # --- Multiple Accounts ---

    @FT-IS-010
    Scenario: Multiple revenue and expense accounts accumulate correctly
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 4010 of type revenue
        And an income-statement-test active account 4020 of type revenue
        And an income-statement-test active account 5010 of type expense
        And an income-statement-test active account 5020 of type expense
        And an income-statement-test entry dated 2026-03-05 described as "Sales revenue" with lines:
            | account | amount | entry_type |
            | 1010    | 600.00 | debit      |
            | 4010    | 600.00 | credit     |
        And an income-statement-test entry dated 2026-03-10 described as "Service revenue" with lines:
            | account | amount | entry_type |
            | 1010    | 400.00 | debit      |
            | 4020    | 400.00 | credit     |
        And an income-statement-test entry dated 2026-03-15 described as "Rent expense" with lines:
            | account | amount | entry_type |
            | 5010    | 200.00 | debit      |
            | 1010    | 200.00 | credit     |
        And an income-statement-test entry dated 2026-03-20 described as "Utility expense" with lines:
            | account | amount | entry_type |
            | 5020    | 150.00 | debit      |
            | 1010    | 150.00 | credit     |
        When I request the income statement for period "2026-03"
        Then the revenue section contains 2 lines
        And the revenue section total is 1000.00
        And the expenses section contains 2 lines
        And the expenses section total is 350.00
        And the net income is 650.00

    # --- Normal Balance Formula ---

    @FT-IS-011
    Scenario: Revenue balance equals credits minus debits
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 4010 of type revenue
        And an income-statement-test entry dated 2026-03-10 described as "Revenue credit" with lines:
            | account | amount | entry_type |
            | 1010    | 700.00 | debit      |
            | 4010    | 700.00 | credit     |
        And an income-statement-test entry dated 2026-03-20 described as "Revenue debit adjustment" with lines:
            | account | amount | entry_type |
            | 4010    | 100.00 | debit      |
            | 1010    | 100.00 | credit     |
        When I request the income statement for period "2026-03"
        Then the revenue section contains account 4010 with balance 600.00

    @FT-IS-012
    Scenario: Expense balance equals debits minus credits
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 5010 of type expense
        And an income-statement-test entry dated 2026-03-10 described as "Expense debit" with lines:
            | account | amount | entry_type |
            | 5010    | 500.00 | debit      |
            | 1010    | 500.00 | credit     |
        And an income-statement-test entry dated 2026-03-20 described as "Expense credit adjustment" with lines:
            | account | amount | entry_type |
            | 1010    | 150.00 | debit      |
            | 5010    | 150.00 | credit     |
        When I request the income statement for period "2026-03"
        Then the expenses section contains account 5010 with balance 350.00

    # --- Period Scoping (REM-010) ---

    @FT-IS-017
    Scenario: Income statement for one period excludes another period's activity
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test open fiscal period "2026-04" from 2026-04-01 to 2026-04-30
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 4010 of type revenue
        And an income-statement-test active account 5010 of type expense
        And an income-statement-test entry dated 2026-03-15 in period "2026-03" described as "March revenue" with lines:
            | account | amount | entry_type |
            | 1010    | 800.00 | debit      |
            | 4010    | 800.00 | credit     |
        And an income-statement-test entry dated 2026-04-10 in period "2026-04" described as "April expense" with lines:
            | account | amount | entry_type |
            | 5010    | 300.00 | debit      |
            | 1010    | 300.00 | credit     |
        When I request the income statement for period "2026-03"
        Then the revenue section total is 800.00
        And the expenses section total is 0.00
        And the net income is 800.00

    # --- Lookup Equivalence ---

    @FT-IS-013
    Scenario: Lookup by period key returns same result as by period ID
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 4010 of type revenue
        And an income-statement-test entry dated 2026-03-15 described as "Lookup test" with lines:
            | account | amount | entry_type |
            | 1010    | 250.00 | debit      |
            | 4010    | 250.00 | credit     |
        When I request the income statement for period "2026-03" by key
        And I request the income statement for the same period by ID
        Then both income statement results are identical

    # --- Validation ---

    @FT-IS-014
    Scenario: Nonexistent period ID returns error
        Given the ledger schema exists for income statement queries
        When I request the income statement for period ID 999999
        Then the income statement result is Error containing "does not exist"

    @FT-IS-015
    Scenario: Nonexistent period key returns error
        Given the ledger schema exists for income statement queries
        When I request the income statement for period key "9999-99"
        Then the income statement result is Error containing "does not exist"

    # --- Closed Period ---

    @FT-IS-016
    Scenario: Closed period income statement still works
        Given the ledger schema exists for income statement queries
        And an income-statement-test open fiscal period "2025-12" from 2025-12-01 to 2025-12-31
        And an income-statement-test active account 1010 of type asset
        And an income-statement-test active account 4010 of type revenue
        And an income-statement-test entry dated 2025-12-15 described as "December income" with lines:
            | account | amount | entry_type |
            | 1010    | 800.00 | debit      |
            | 4010    | 800.00 | credit     |
        And the income-statement-test fiscal period "2025-12" is now closed
        When I request the income statement for period "2025-12"
        Then the revenue section total is 800.00
        And the net income is 800.00
