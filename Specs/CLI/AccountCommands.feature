Feature: Account CLI Commands
    The account command group exposes list, show, and balance subcommands
    for querying the chart of accounts and account balances. Each command
    parses arguments, calls the corresponding AccountBalanceService
    function, formats the result, and returns the correct exit code.
    These specs test CLI behavior only -- account balance calculation
    logic is covered by service-level tests in AccountBalance.feature.

    # ===================================================================
    # account (no subcommand)
    # ===================================================================

    @FT-ACT-001
    Scenario: Account with no subcommand prints usage to stderr
        When I run the CLI with "account"
        Then stderr contains usage information
        And the exit code is 2

    # ===================================================================
    # account list
    # ===================================================================

    # --- Happy Path ---

    @FT-ACT-010
    Scenario: List all active accounts with no filters
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account list"
        Then stdout contains account summary rows
        And the exit code is 0

    @FT-ACT-011
    Scenario: List accounts filtered by type
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account list --type asset"
        Then stdout contains only accounts of type "asset"
        And the exit code is 0

    @FT-ACT-012
    Scenario: List accounts with --inactive includes inactive accounts
        Given a CLI-testable account environment with seeded data including inactive accounts
        When I run the CLI with "account list --inactive"
        Then stdout contains inactive account rows
        And the exit code is 0

    @FT-ACT-013
    Scenario: List accounts with --json flag outputs valid JSON
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account list --json"
        Then stdout is valid JSON
        And the exit code is 0

    @FT-ACT-014
    Scenario: List accounts with --type filter is case-insensitive
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account list --type Asset"
        Then stdout contains only accounts of type "asset"
        And the exit code is 0

    # --- Invalid Filter ---

    @FT-ACT-015
    Scenario: List with invalid account type prints error to stderr
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account list --type bogus"
        Then stderr contains an error message
        And the exit code is 1

    # --- Empty Results ---

    @FT-ACT-016
    Scenario: List with no matching results prints empty output
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account list --type equity"
        Then stdout is empty or contains only headers
        And the exit code is 0

    # ===================================================================
    # account show
    # ===================================================================

    # --- Happy Path ---

    @FT-ACT-020
    Scenario: Show an existing account by numeric ID
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account show 1"
        Then stdout contains the account details
        And the exit code is 0

    @FT-ACT-021
    Scenario: Show an existing account by code
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account show 1110"
        Then stdout contains the account details
        And the exit code is 0

    @FT-ACT-022
    Scenario: Show with --json flag outputs valid JSON
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account show 1110 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-ACT-023
    Scenario: Show a nonexistent account by ID prints error to stderr
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account show 999999"
        Then stderr contains an error message
        And the exit code is 1

    @FT-ACT-024
    Scenario: Show a nonexistent account by code prints error to stderr
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account show ZZZZ"
        Then stderr contains an error message
        And the exit code is 1

    @FT-ACT-025
    Scenario: Show with no account argument prints error to stderr
        When I run the CLI with "account show"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # account balance
    # ===================================================================

    # --- Happy Path ---

    @FT-ACT-030
    Scenario: Balance for an existing account by ID returns current balance
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account balance 1"
        Then stdout contains the balance amount
        And the exit code is 0

    @FT-ACT-031
    Scenario: Balance for an existing account by code returns current balance
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account balance 1110"
        Then stdout contains the balance amount
        And the exit code is 0

    @FT-ACT-032
    Scenario: Balance with --as-of returns historical balance
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account balance 1110 --as-of 2026-01-31"
        Then stdout contains the balance amount
        And the exit code is 0

    @FT-ACT-033
    Scenario: Balance with --json flag outputs valid JSON
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account balance 1110 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-ACT-034
    Scenario: Balance for nonexistent account prints error to stderr
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account balance 999999"
        Then stderr contains an error message
        And the exit code is 1

    @FT-ACT-035
    Scenario: Balance with invalid date format prints error to stderr
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account balance 1110 --as-of not-a-date"
        Then stderr contains an error message
        And the exit code is 1

    @FT-ACT-036
    Scenario: Balance with no account argument prints error to stderr
        When I run the CLI with "account balance"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # --json flag consistency
    # ===================================================================

    @FT-ACT-050
    Scenario Outline: --json flag produces valid JSON for all account subcommands
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account <subcommand>"
        Then stdout is valid JSON
        And the exit code is 0

        Examples:
            | subcommand                      |
            | list --json                     |
            | show 1110 --json                |
            | balance 1110 --json             |
