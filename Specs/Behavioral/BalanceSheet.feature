Feature: Balance Sheet
    Produce a balance sheet as of a given date: sum all cumulative non-voided
    journal entry activity through that date for asset, liability, and equity
    accounts, compute retained earnings from all-time revenue and expense
    activity, and verify the accounting equation (Assets = Liabilities + Total
    Equity). This is cumulative through a date, not period-scoped.

    # --- Happy Path ---

    @FT-BS-001
    Scenario: Balanced books produce isBalanced true
        Given the ledger schema exists for balance sheet queries
        And a balance-sheet-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a balance-sheet-test active account 1010 of type asset
        And a balance-sheet-test active account 2010 of type liability
        And a balance-sheet-test active account 3010 of type equity
        And a balance-sheet-test active account 4010 of type revenue
        And a balance-sheet-test active account 5010 of type expense
        And a balance-sheet-test entry dated 2026-03-05 described as "Owner investment" with lines:
            | account | amount  | entry_type |
            | 1010    | 5000.00 | debit      |
            | 3010    | 5000.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-10 described as "Service income" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-20 described as "Office rent" with lines:
            | account | amount | entry_type |
            | 5010    | 400.00 | debit      |
            | 1010    | 400.00 | credit     |
        When I request the balance sheet as of 2026-03-31
        Then the balance sheet is balanced

    # --- Accounting Equation ---

    @FT-BS-002
    Scenario: Assets equal liabilities plus total equity
        Given the ledger schema exists for balance sheet queries
        And a balance-sheet-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a balance-sheet-test active account 1010 of type asset
        And a balance-sheet-test active account 2010 of type liability
        And a balance-sheet-test active account 3010 of type equity
        And a balance-sheet-test active account 4010 of type revenue
        And a balance-sheet-test entry dated 2026-03-05 described as "Owner investment" with lines:
            | account | amount  | entry_type |
            | 1010    | 5000.00 | debit      |
            | 3010    | 5000.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-10 described as "Took a loan" with lines:
            | account | amount  | entry_type |
            | 1010    | 2000.00 | debit      |
            | 2010    | 2000.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-15 described as "Earned revenue" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        When I request the balance sheet as of 2026-03-31
        Then the assets section total is 8000.00
        And the liabilities section total is 2000.00
        And the equity section total is 5000.00
        And the retained earnings are 1000.00
        And the total equity is 6000.00
        And the balance sheet is balanced

    # --- Retained Earnings ---

    @FT-BS-003
    Scenario: Positive retained earnings when revenue exceeds expenses
        Given the ledger schema exists for balance sheet queries
        And a balance-sheet-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a balance-sheet-test active account 1010 of type asset
        And a balance-sheet-test active account 4010 of type revenue
        And a balance-sheet-test active account 5010 of type expense
        And a balance-sheet-test entry dated 2026-03-10 described as "Revenue" with lines:
            | account | amount  | entry_type |
            | 1010    | 3000.00 | debit      |
            | 4010    | 3000.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-20 described as "Expense" with lines:
            | account | amount  | entry_type |
            | 5010    | 1000.00 | debit      |
            | 1010    | 1000.00 | credit     |
        When I request the balance sheet as of 2026-03-31
        Then the retained earnings are 2000.00

    @FT-BS-004
    Scenario: Negative retained earnings when expenses exceed revenue
        Given the ledger schema exists for balance sheet queries
        And a balance-sheet-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a balance-sheet-test active account 1010 of type asset
        And a balance-sheet-test active account 2010 of type liability
        And a balance-sheet-test active account 4010 of type revenue
        And a balance-sheet-test active account 5010 of type expense
        And a balance-sheet-test entry dated 2026-03-05 described as "Borrowed to fund operations" with lines:
            | account | amount  | entry_type |
            | 1010    | 2000.00 | debit      |
            | 2010    | 2000.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-10 described as "Small revenue" with lines:
            | account | amount | entry_type |
            | 1010    | 200.00 | debit      |
            | 4010    | 200.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-20 described as "Large expense" with lines:
            | account | amount | entry_type |
            | 5010    | 800.00 | debit      |
            | 1010    | 800.00 | credit     |
        When I request the balance sheet as of 2026-03-31
        Then the retained earnings are -600.00
        And the balance sheet is balanced

    @FT-BS-005
    Scenario: Retained earnings zero when no revenue or expense activity
        Given the ledger schema exists for balance sheet queries
        And a balance-sheet-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a balance-sheet-test active account 1010 of type asset
        And a balance-sheet-test active account 3010 of type equity
        And a balance-sheet-test entry dated 2026-03-10 described as "Owner investment only" with lines:
            | account | amount  | entry_type |
            | 1010    | 5000.00 | debit      |
            | 3010    | 5000.00 | credit     |
        When I request the balance sheet as of 2026-03-31
        Then the retained earnings are 0.00
        And the balance sheet is balanced

    # --- Edge Cases ---

    @FT-BS-006
    Scenario: Before any entries all zeros and balanced
        Given the ledger schema exists for balance sheet queries
        When I request the balance sheet as of 2026-03-31
        Then the assets section total is 0.00
        And the liabilities section total is 0.00
        And the equity section total is 0.00
        And the retained earnings are 0.00
        And the total equity is 0.00
        And the balance sheet is balanced
        And the assets section contains 0 lines
        And the liabilities section contains 0 lines
        And the equity section contains 0 lines

    @FT-BS-007
    Scenario: Voided entries excluded from balance sheet
        Given the ledger schema exists for balance sheet queries
        And a balance-sheet-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a balance-sheet-test active account 1010 of type asset
        And a balance-sheet-test active account 3010 of type equity
        And a balance-sheet-test entry dated 2026-03-10 described as "Good investment" with lines:
            | account | amount  | entry_type |
            | 1010    | 5000.00 | debit      |
            | 3010    | 5000.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-15 described as "Bad investment" with lines:
            | account | amount  | entry_type |
            | 1010    | 2000.00 | debit      |
            | 3010    | 2000.00 | credit     |
        And the balance-sheet-test entry "Bad investment" has been voided
        When I request the balance sheet as of 2026-03-31
        Then the assets section total is 5000.00
        And the equity section total is 5000.00
        And the balance sheet is balanced

    # --- Cumulative Nature ---

    @FT-BS-008
    Scenario: Entries across multiple fiscal periods all contribute
        Given the ledger schema exists for balance sheet queries
        And a balance-sheet-test open fiscal period "2026-01" from 2026-01-01 to 2026-01-31
        And a balance-sheet-test open fiscal period "2026-02" from 2026-02-01 to 2026-02-28
        And a balance-sheet-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a balance-sheet-test active account 1010 of type asset
        And a balance-sheet-test active account 3010 of type equity
        And a balance-sheet-test active account 4010 of type revenue
        And a balance-sheet-test entry in period "2026-01" dated 2026-01-15 described as "January investment" with lines:
            | account | amount  | entry_type |
            | 1010    | 3000.00 | debit      |
            | 3010    | 3000.00 | credit     |
        And a balance-sheet-test entry in period "2026-02" dated 2026-02-15 described as "February income" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        And a balance-sheet-test entry in period "2026-03" dated 2026-03-15 described as "March income" with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        When I request the balance sheet as of 2026-03-31
        Then the assets section total is 4500.00
        And the equity section total is 3000.00
        And the retained earnings are 1500.00
        And the balance sheet is balanced

    # --- Multiple Accounts ---

    @FT-BS-009
    Scenario: Multiple accounts per section accumulate correctly
        Given the ledger schema exists for balance sheet queries
        And a balance-sheet-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a balance-sheet-test active account 1010 of type asset
        And a balance-sheet-test active account 1020 of type asset
        And a balance-sheet-test active account 2010 of type liability
        And a balance-sheet-test active account 2020 of type liability
        And a balance-sheet-test active account 3010 of type equity
        And a balance-sheet-test active account 3020 of type equity
        And a balance-sheet-test entry dated 2026-03-05 described as "Owner investment into checking" with lines:
            | account | amount  | entry_type |
            | 1010    | 5000.00 | debit      |
            | 3010    | 5000.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-10 described as "Equipment purchase on credit" with lines:
            | account | amount  | entry_type |
            | 1020    | 2000.00 | debit      |
            | 2010    | 2000.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-15 described as "Partner investment" with lines:
            | account | amount  | entry_type |
            | 1010    | 3000.00 | debit      |
            | 3020    | 3000.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-20 described as "Additional loan" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 2020    | 1000.00 | credit     |
        When I request the balance sheet as of 2026-03-31
        Then the assets section contains 2 lines
        And the assets section total is 11000.00
        And the liabilities section contains 2 lines
        And the liabilities section total is 3000.00
        And the equity section contains 2 lines
        And the equity section total is 8000.00
        And the balance sheet is balanced

    # --- GAAP: Zero-Balance with Activity ---

    @FT-BS-010
    Scenario: Account with activity netting to zero still appears with balance zero
        Given the ledger schema exists for balance sheet queries
        And a balance-sheet-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a balance-sheet-test active account 1010 of type asset
        And a balance-sheet-test active account 1020 of type asset
        And a balance-sheet-test active account 3010 of type equity
        And a balance-sheet-test entry dated 2026-03-05 described as "Investment" with lines:
            | account | amount  | entry_type |
            | 1010    | 5000.00 | debit      |
            | 3010    | 5000.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-10 described as "Transfer to savings" with lines:
            | account | amount  | entry_type |
            | 1020    | 1000.00 | debit      |
            | 1010    | 1000.00 | credit     |
        And a balance-sheet-test entry dated 2026-03-15 described as "Transfer back" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 1020    | 1000.00 | credit     |
        When I request the balance sheet as of 2026-03-31
        Then the assets section contains account 1020 with balance 0.00
        And the assets section contains 2 lines

    # --- Inactive Accounts ---

    @FT-BS-011
    Scenario: Inactive accounts with cumulative balances still appear
        Given the ledger schema exists for balance sheet queries
        And a balance-sheet-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a balance-sheet-test active account 1010 of type asset
        And a balance-sheet-test active account 3010 of type equity
        And a balance-sheet-test entry dated 2026-03-15 described as "Before deactivation" with lines:
            | account | amount  | entry_type |
            | 1010    | 5000.00 | debit      |
            | 3010    | 5000.00 | credit     |
        And balance-sheet-test account 1010 is now deactivated
        When I request the balance sheet as of 2026-03-31
        Then the assets section contains account 1010 with balance 5000.00
        And the balance sheet is balanced
