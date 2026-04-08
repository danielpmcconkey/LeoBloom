Feature: Position Recording, Querying, and Validation
    Record point-in-time portfolio positions (price, quantity, current value,
    cost basis) and query them by account, date range, or as a latest snapshot.
    The service layer enforces non-negative numeric values and rejects positions
    referencing unknown fund symbols before touching the database.

    # --- Record: Happy Path ---

    @FT-PF-020
    Scenario: Record position with all fields returns a complete record
        Given the portfolio schema exists for position management
        And a portfolio investment account "Vanguard IRA" exists
        And a portfolio fund "VTI" exists
        When I record a position for account "Vanguard IRA", symbol "VTI", dated 2026-03-31, with price 245.10, quantity 100.0000, current_value 24510.00, cost_basis 20000.00
        Then the record succeeds with a generated id
        And the returned position has symbol "VTI", price 245.10, quantity 100.0000, current_value 24510.00, cost_basis 20000.00

    # --- Record: Pure Validation ---

    @FT-PF-021
    Scenario Outline: Record position with negative numeric field is rejected
        Given the portfolio schema exists for position management
        And a portfolio investment account "Test Account" exists
        And a portfolio fund "VTI" exists
        When I record a position with <field> set to <value>
        Then the record fails with error containing "<field>"

        Examples:
            | field         | value    |
            | price         | -0.01    |
            | quantity      | -1.0000  |
            | current_value | -100.00  |
            | cost_basis    | -5000.00 |

    # --- Record: Date Validation ---

    @FT-PF-029
    Scenario: Record position with a future date is rejected
        Given the portfolio schema exists for position management
        And a portfolio investment account "Vanguard IRA" exists
        And a portfolio fund "VTI" exists
        When I record a position for account "Vanguard IRA", symbol "VTI", dated 2099-01-01, with price 245.10, quantity 100.0000, current_value 24510.00, cost_basis 20000.00
        Then the record fails with error containing "date"

    # --- Record: DB Validation ---

    @FT-PF-022
    Scenario: Record position for nonexistent fund symbol is rejected
        Given the portfolio schema exists for position management
        And a portfolio investment account "Vanguard IRA" exists
        And no fund with symbol "FAKE" exists
        When I record a position for account "Vanguard IRA", symbol "FAKE", dated 2026-03-31, with price 100.00, quantity 10.0000, current_value 1000.00, cost_basis 900.00
        Then the record fails with error containing "fund"

    @FT-PF-023
    Scenario: Record duplicate position for same account, symbol, and date is rejected
        Given the portfolio schema exists for position management
        And a portfolio investment account "Vanguard IRA" exists
        And a portfolio fund "VTI" exists
        And a position for account "Vanguard IRA", symbol "VTI", dated 2026-03-31 already exists
        When I record a position for account "Vanguard IRA", symbol "VTI", dated 2026-03-31, with price 245.10, quantity 100.0000, current_value 24510.00, cost_basis 20000.00
        Then the record fails with error containing "already exists"

    # --- List by Account ---

    @FT-PF-024
    Scenario: List positions filtered by account returns only that account's positions
        Given the portfolio schema exists for position management
        And a portfolio investment account "Vanguard IRA" exists
        And a portfolio investment account "Schwab Taxable" exists
        And a portfolio fund "VTI" exists
        And a position for account "Vanguard IRA", symbol "VTI", dated 2026-03-31 exists
        And a position for account "Schwab Taxable", symbol "VTI", dated 2026-03-31 exists
        When I list positions filtered by account "Vanguard IRA"
        Then the result contains 1 position
        And the returned position belongs to account "Vanguard IRA"

    @FT-PF-025
    Scenario: List positions filtered by account returns empty when account has no positions
        Given the portfolio schema exists for position management
        And a portfolio investment account "Empty Account" exists
        When I list positions filtered by account "Empty Account"
        Then the result is an empty list

    # --- List by Date Range ---

    @FT-PF-026
    Scenario: List positions filtered by date range returns only positions within the range
        Given the portfolio schema exists for position management
        And a portfolio investment account "Vanguard IRA" exists
        And a portfolio fund "VTI" exists
        And a position for account "Vanguard IRA", symbol "VTI", dated 2026-01-31 exists
        And a position for account "Vanguard IRA", symbol "VTI", dated 2026-02-28 exists
        And a position for account "Vanguard IRA", symbol "VTI", dated 2026-03-31 exists
        When I list positions with start date 2026-02-01 and end date 2026-02-28
        Then the result contains 1 position
        And the returned position is dated 2026-02-28

    # --- Latest Snapshot ---

    @FT-PF-027
    Scenario: Latest positions for an account returns the most recent position per fund
        Given the portfolio schema exists for position management
        And a portfolio investment account "Vanguard IRA" exists
        And a portfolio fund "VTI" exists
        And a position for account "Vanguard IRA", symbol "VTI", dated 2026-02-28 exists with current_value 23000.00
        And a position for account "Vanguard IRA", symbol "VTI", dated 2026-03-31 exists with current_value 24510.00
        When I request the latest positions for account "Vanguard IRA"
        Then the result contains 1 position for symbol "VTI"
        And that position is dated 2026-03-31 with current_value 24510.00

    @FT-PF-028
    Scenario: Latest positions across all accounts returns one entry per account-and-fund pair
        Given the portfolio schema exists for position management
        And a portfolio investment account "Vanguard IRA" exists
        And a portfolio investment account "Schwab Taxable" exists
        And a portfolio fund "VTI" exists
        And a position for account "Vanguard IRA", symbol "VTI", dated 2026-02-28 exists
        And a position for account "Vanguard IRA", symbol "VTI", dated 2026-03-31 exists
        And a position for account "Schwab Taxable", symbol "VTI", dated 2026-03-31 exists
        When I request the latest positions for all accounts
        Then the result contains exactly 2 positions
        And each position is the most recent for its account and symbol
