Feature: Seed Runner
    The seed runner shell script executes SQL seed files in sorted order
    against a target environment's database via psql. Seeds are idempotent
    upserts that populate baseline reference data (fiscal periods, chart of
    accounts) for a given environment.

    # --- Fresh database baseline ---

    @FT-SR-001
    Scenario: Seeds populate fiscal periods on a fresh database
        Given a fresh database with migrations applied
        When I run the seed runner for the dev environment
        Then the database contains 36 fiscal periods covering 2026-01 through 2028-12

    @FT-SR-002
    Scenario: Seeds populate chart of accounts on a fresh database
        Given a fresh database with migrations applied
        When I run the seed runner for the dev environment
        Then the database contains 69 accounts
        And every account with a parent_id references an existing account

    @FT-SR-003
    Scenario: Seeds apply account subtypes from the chart of accounts
        Given a fresh database with migrations applied
        When I run the seed runner for the dev environment
        Then accounts with leaf-level detail have non-null account_subtype values

    # --- Idempotency ---

    @FT-SR-004
    Scenario: Running seeds twice produces identical state
        Given a fresh database with migrations applied
        And I run the seed runner for the dev environment
        When I run the seed runner for the dev environment again
        Then the fiscal_period row count is 36
        And the account row count is 69
        And no errors are reported

    # --- Error handling ---

    @FT-SR-005
    Scenario: Runner stops on SQL error with non-zero exit
        Given a fresh database with migrations applied
        And a seed directory containing a script with a SQL syntax error
        When I run the seed runner against that directory
        Then the runner exits with a non-zero exit code
        And subsequent seed scripts are not executed

    @FT-SR-006
    Scenario: Runner exits non-zero for nonexistent environment directory
        Given the seed runner script exists
        When I run the seed runner for the prod environment
        Then the runner exits with a non-zero exit code
        And the output indicates the directory was not found

    # --- Execution order ---

    @FT-SR-007
    Scenario: Seeds execute in numeric filename order
        Given a fresh database with migrations applied
        When I run the seed runner for the dev environment
        Then 010-fiscal-periods.sql executes before 020-chart-of-accounts.sql

    # --- Portfolio reference data ---

    @FT-SR-008
    Scenario: Seeds populate the 5 tax buckets on a fresh database
        Given a fresh database with migrations applied
        When I run the seed runner for the dev environment
        Then the database contains 5 portfolio.tax_bucket rows

    @FT-SR-009
    Scenario Outline: Seeds populate each fund dimension table on a fresh database
        Given a fresh database with migrations applied
        When I run the seed runner for the dev environment
        Then portfolio.<table> contains <count> rows

        Examples:
            | table                | count |
            | dim_investment_type  | 8     |
            | dim_market_cap       | 4     |
            | dim_index_type       | 3     |
            | dim_sector           | 13    |
            | dim_region           | 5     |
            | dim_objective        | 6     |

    @FT-SR-010
    Scenario: Seeds populate sample funds with valid dimension FK references
        Given a fresh database with migrations applied
        When I run the seed runner for the dev environment
        Then portfolio.fund contains at least 4 rows
        And every fund row with a dim_investment_type_id references an existing dim_investment_type

    @FT-SR-011
    Scenario: Portfolio seed data survives a second run unchanged
        Given a fresh database with migrations applied
        And I run the seed runner for the dev environment
        When I run the seed runner for the dev environment again
        Then the portfolio.tax_bucket row count is 5
        And no errors are reported
