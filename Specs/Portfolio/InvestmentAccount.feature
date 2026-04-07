Feature: Investment Account CRUD
    Create and retrieve investment accounts. Each account is associated with a
    tax bucket (e.g. tax-deferred, taxable) and an account group (e.g.
    retirement, brokerage). Accounts are the top-level container for positions
    and transactions in the portfolio domain.

    # --- Create: Happy Path ---

    @FT-PF-001
    Scenario: Create investment account with required fields returns a complete record
        Given the portfolio schema exists for account management
        And a portfolio tax bucket "Tax Deferred" exists
        And a portfolio account group "Retirement" exists
        When I create an investment account with name "Vanguard IRA", tax bucket "Tax Deferred", and account group "Retirement"
        Then the create succeeds with a generated id
        And the returned account has name "Vanguard IRA"
        And a subsequent findById returns the same account with matching fields

    # --- Create: Pure Validation ---

    @FT-PF-002
    Scenario: Create investment account with empty name is rejected
        Given the portfolio schema exists for account management
        And a portfolio tax bucket "Tax Deferred" exists
        And a portfolio account group "Retirement" exists
        When I create an investment account with name "", tax bucket "Tax Deferred", and account group "Retirement"
        Then the create fails with error containing "name"

    # --- List ---

    @FT-PF-003
    Scenario: List investment accounts returns all created accounts
        Given the portfolio schema exists for account management
        And a portfolio tax bucket "Taxable" exists
        And a portfolio account group "Brokerage" exists
        And an existing investment account named "Fidelity Taxable"
        And an existing investment account named "Schwab Roth"
        When I list all investment accounts
        Then the result contains "Fidelity Taxable"
        And the result contains "Schwab Roth"

    @FT-PF-004
    Scenario: List investment accounts returns empty when none exist
        Given the portfolio schema exists for account management
        And no investment accounts exist
        When I list all investment accounts
        Then the result is an empty list
