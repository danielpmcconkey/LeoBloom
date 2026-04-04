Feature: Trial Balance
    Produce a trial balance report for a fiscal period: sum all debit and
    credit activity from non-voided journal entries, grouped by account type,
    with an integrity check that total debits equal total credits.
    This is period-scoped activity only, not cumulative balance.

    # --- Happy Path ---

    @FT-TB-001
    Scenario: Balanced entries produce a balanced trial balance
        Given the ledger schema exists for trial balance queries
        And a trial-balance-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a trial-balance-test active account 1010 of type asset
        And a trial-balance-test active account 4010 of type revenue
        And a trial-balance-test entry dated 2026-03-15 described as "March rent" with lines:
            | account | amount  | entry_type |
            | 1010    | 500.00  | debit      |
            | 4010    | 500.00  | credit     |
        When I request the trial balance for period "2026-03"
        Then the trial balance is balanced
        And the grand total debits are 500.00
        And the grand total credits are 500.00
        And the report contains 2 account lines

    # --- Grouping and Subtotals ---

    @FT-TB-002
    Scenario: Report groups accounts by type with correct subtotals
        Given the ledger schema exists for trial balance queries
        And a trial-balance-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a trial-balance-test active account 1010 of type asset
        And a trial-balance-test active account 1020 of type asset
        And a trial-balance-test active account 4010 of type revenue
        And a trial-balance-test active account 5010 of type expense
        And a trial-balance-test entry dated 2026-03-10 described as "Income" with lines:
            | account | amount  | entry_type |
            | 1010    | 600.00  | debit      |
            | 1020    | 400.00  | debit      |
            | 4010    | 1000.00 | credit     |
        And a trial-balance-test entry dated 2026-03-20 described as "Supplies" with lines:
            | account | amount | entry_type |
            | 5010    | 200.00 | debit      |
            | 1010    | 200.00 | credit     |
        When I request the trial balance for period "2026-03"
        Then the trial balance groups appear in order: asset, revenue, expense
        And the asset group debit total is 1000.00 and credit total is 200.00
        And the revenue group debit total is 0.00 and credit total is 1000.00
        And the expense group debit total is 200.00 and credit total is 0.00

    @FT-TB-003
    Scenario: Groups with no activity are omitted
        Given the ledger schema exists for trial balance queries
        And a trial-balance-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a trial-balance-test active account 1010 of type asset
        And a trial-balance-test active account 4010 of type revenue
        And a trial-balance-test entry dated 2026-03-15 described as "Simple entry" with lines:
            | account | amount | entry_type |
            | 1010    | 300.00 | debit      |
            | 4010    | 300.00 | credit     |
        When I request the trial balance for period "2026-03"
        Then the trial balance contains exactly 2 groups
        And no group exists for liability, equity, or expense

    # --- Filtering ---

    @FT-TB-004
    Scenario: Voided entries excluded from trial balance
        Given the ledger schema exists for trial balance queries
        And a trial-balance-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a trial-balance-test active account 1010 of type asset
        And a trial-balance-test active account 4010 of type revenue
        And a trial-balance-test entry dated 2026-03-10 described as "Good entry" with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        And a trial-balance-test entry dated 2026-03-15 described as "Bad entry" with lines:
            | account | amount | entry_type |
            | 1010    | 200.00 | debit      |
            | 4010    | 200.00 | credit     |
        And the trial-balance-test entry "Bad entry" has been voided
        When I request the trial balance for period "2026-03"
        Then the grand total debits are 500.00
        And the grand total credits are 500.00

    # --- Edge Cases ---

    @FT-TB-005
    Scenario: Empty period returns balanced report with zero totals
        Given the ledger schema exists for trial balance queries
        And a trial-balance-test open fiscal period "2026-04" from 2026-04-01 to 2026-04-30
        When I request the trial balance for period "2026-04"
        Then the trial balance is balanced
        And the grand total debits are 0.00
        And the grand total credits are 0.00
        And the report contains 0 groups

    @FT-TB-006
    Scenario: Closed period trial balance still works
        Given the ledger schema exists for trial balance queries
        And a trial-balance-test open fiscal period "2025-12" from 2025-12-01 to 2025-12-31
        And a trial-balance-test active account 1010 of type asset
        And a trial-balance-test active account 4010 of type revenue
        And a trial-balance-test entry dated 2025-12-15 described as "December entry" with lines:
            | account | amount | entry_type |
            | 1010    | 800.00 | debit      |
            | 4010    | 800.00 | credit     |
        And the trial-balance-test fiscal period "2025-12" is now closed
        When I request the trial balance for period "2025-12"
        Then the trial balance is balanced
        And the grand total debits are 800.00

    @FT-TB-007
    Scenario: Multiple entries in same period accumulate per account
        Given the ledger schema exists for trial balance queries
        And a trial-balance-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a trial-balance-test active account 1010 of type asset
        And a trial-balance-test active account 4010 of type revenue
        And a trial-balance-test entry dated 2026-03-10 described as "First payment" with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        And a trial-balance-test entry dated 2026-03-20 described as "Second payment" with lines:
            | account | amount | entry_type |
            | 1010    | 300.00 | debit      |
            | 4010    | 300.00 | credit     |
        When I request the trial balance for period "2026-03"
        Then account 1010 shows debit total 800.00 and credit total 0.00
        And account 4010 shows debit total 0.00 and credit total 800.00

    # --- Net Balance Formula ---

    @FT-TB-008
    Scenario: Net balance uses normal_balance formula
        Given the ledger schema exists for trial balance queries
        And a trial-balance-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a trial-balance-test active account 1010 of type asset
        And a trial-balance-test active account 4010 of type revenue
        And a trial-balance-test entry dated 2026-03-15 described as "Net test" with lines:
            | account | amount | entry_type |
            | 1010    | 700.00 | debit      |
            | 4010    | 700.00 | credit     |
        When I request the trial balance for period "2026-03"
        Then account 1010 has net balance 700.00 as a normal-debit account
        And account 4010 has net balance 700.00 as a normal-credit account

    # --- Lookup Equivalence ---

    @FT-TB-009
    Scenario: Lookup by period key returns same result as by period ID
        Given the ledger schema exists for trial balance queries
        And a trial-balance-test open fiscal period "2026-03" from 2026-03-01 to 2026-03-31
        And a trial-balance-test active account 1010 of type asset
        And a trial-balance-test active account 4010 of type revenue
        And a trial-balance-test entry dated 2026-03-15 described as "Lookup test" with lines:
            | account | amount | entry_type |
            | 1010    | 250.00 | debit      |
            | 4010    | 250.00 | credit     |
        When I request the trial balance for period "2026-03" by key
        And I request the trial balance for the same period by ID
        Then both trial balance results are identical

    # --- Validation ---

    @FT-TB-010
    Scenario: Nonexistent period ID returns error
        Given the ledger schema exists for trial balance queries
        When I request the trial balance for period ID 999999
        Then the trial balance result is Error containing "does not exist"

    @FT-TB-011
    Scenario: Nonexistent period key returns error
        Given the ledger schema exists for trial balance queries
        When I request the trial balance for period key "9999-99"
        Then the trial balance result is Error containing "does not exist"
