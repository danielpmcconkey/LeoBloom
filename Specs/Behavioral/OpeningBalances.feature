Feature: Opening Balances
    Post opening balances for a set of accounts in a fiscal period. Each account
    gets a journal entry line using its normal balance direction, with a balancing
    entry on a designated equity account. The result is a balanced journal entry
    that establishes starting balances for the period.

    # --- Happy Path ---

    @FT-OB-001
    Scenario: Opening balances for asset and liability produce balanced entry
        Given a Cash account (asset), Mortgage account (liability), and Equity account (equity)
        And an open fiscal period covering January 2026
        When I post opening balances with Cash 10000.00 and Mortgage 200000.00 using Equity as balancing
        Then the post succeeds
        And the journal entry has 3 lines
        And total debits equal total credits

    @FT-OB-002
    Scenario: Balancing line computed correctly when debits exceed credits
        Given a Cash account (asset) and Equity account (equity)
        And an open fiscal period covering January 2026
        When I post opening balances with Cash 5000.00 using Equity as balancing
        Then the post succeeds
        And the Equity line is a credit of 5000.00

    @FT-OB-003
    Scenario: Balancing line computed correctly when credits exceed debits
        Given a Mortgage account (liability) and Equity account (equity)
        And an open fiscal period covering January 2026
        When I post opening balances with Mortgage 200000.00 using Equity as balancing
        Then the post succeeds
        And the Equity line is a debit of 200000.00

    @FT-OB-004
    Scenario: Single account entry creates two-line journal entry
        Given a Cash account (asset) and Equity account (equity)
        And an open fiscal period covering January 2026
        When I post opening balances with Cash 1000.00 using Equity as balancing
        Then the post succeeds
        And the journal entry has 2 lines

    @FT-OB-005
    Scenario: Posted entry is retrievable and has correct line count and metadata
        Given Cash, Brokerage (assets), Mortgage (liability), and Equity accounts
        And an open fiscal period covering January 2026
        When I post opening balances with Cash 10000.00, Brokerage 50000.00, and Mortgage 200000.00
        Then the post succeeds
        And the journal entry has 4 lines
        And the journal entry description is "Opening balances"
        And the journal entry source is "opening_balance"

    # --- Validation ---

    @FT-OB-006
    Scenario: Duplicate account in entries returns error
        Given a Cash account (asset) and Equity account (equity)
        And an open fiscal period covering January 2026
        When I post opening balances with Cash listed twice
        Then the post fails with error containing "Duplicate"

    @FT-OB-007
    Scenario: Empty entries list returns error
        Given an Equity account (equity)
        And an open fiscal period covering January 2026
        When I post opening balances with no entries using Equity as balancing
        Then the post fails with error containing "empty"

    @FT-OB-008
    Scenario: Zero balance entry returns error
        Given a Cash account (asset) and Equity account (equity)
        And an open fiscal period covering January 2026
        When I post opening balances with Cash 0.00 using Equity as balancing
        Then the post fails with error containing "non-positive"

    @FT-OB-009
    Scenario: Balancing account in entries list returns error
        Given a Cash account (asset) and Equity account (equity)
        And an open fiscal period covering January 2026
        When I post opening balances that include Equity in the entries list
        Then the post fails with error containing "Balancing account"

    @FT-OB-010
    Scenario: Nonexistent account returns error
        Given an Equity account (equity)
        And an open fiscal period covering January 2026
        When I post opening balances referencing a nonexistent account ID
        Then the post fails with error containing "does not exist"

    @FT-OB-011
    Scenario: Nonexistent balancing account returns error
        Given a Cash account (asset)
        And an open fiscal period covering January 2026
        When I post opening balances with a nonexistent balancing account ID
        Then the post fails with error containing "does not exist"

    @FT-OB-012
    Scenario: Non-equity balancing account returns error
        Given a Cash account (asset) and a Bank account (asset)
        And an open fiscal period covering January 2026
        When I post opening balances with Bank as the balancing account
        Then the post fails with error containing "equity"

    # --- Description ---

    @FT-OB-013
    Scenario: Default description is "Opening balances" when not provided
        Given a Cash account (asset) and Equity account (equity)
        And an open fiscal period covering January 2026
        When I post opening balances with no custom description
        Then the post succeeds
        And the journal entry description is "Opening balances"

    @FT-OB-014
    Scenario: Custom description is used when provided
        Given a Cash account (asset) and Equity account (equity)
        And an open fiscal period covering January 2026
        When I post opening balances with description "Migration from QuickBooks"
        Then the post succeeds
        And the journal entry description is "Migration from QuickBooks"
