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

    @FT-ACT-026
    Scenario: Show an account with a parent displays Parent ID field
        Given a CLI-testable account environment with seeded data
        And account 1110 has a parent account
        When I run the CLI with "account show 1110"
        Then stdout contains "Parent ID:"
        And stdout does not contain "Parent Code:"
        And the exit code is 0

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

    # ===================================================================
    # account create
    # ===================================================================

    # --- Happy Path ---

    @FT-ACT-060
    Scenario: Create an account with all flags outputs account details
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account create --code crud-cli-001 --name CLI Cash --type 1 --subtype Cash"
        Then stdout contains the account details
        And the exit code is 0

    @FT-ACT-061
    Scenario: Create an account with --json flag outputs valid JSON
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account create --code crud-cli-002 --name CLI Cash JSON --type 1 --subtype Cash --json"
        Then stdout is valid JSON
        And the exit code is 0

    @FT-ACT-062
    Scenario: Create an account without --parent succeeds
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account create --code crud-cli-003 --name No Parent Account --type 1"
        Then stdout contains the account details
        And the exit code is 0

    # --- Error Paths ---

    @FT-ACT-063
    Scenario: Create with invalid subtype for account type prints error to stderr
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account create --code crud-cli-004 --name Bad Subtype --type 5 --subtype Cash"
        Then stderr contains an error message
        And the exit code is 1

    @FT-ACT-064
    Scenario Outline: Create with missing mandatory flag prints error to stderr
        When I run the CLI with "account create <args>"
        Then stderr contains an error message
        And the exit code is 2

        Examples:
            | args                                        |
            | --name No Code Here --type 1                |
            | --code crud-cli-005 --type 1                |
            | --code crud-cli-006 --name No Type Here     |

    @FT-ACT-065
    Scenario: Create with a duplicate account code prints error to stderr
        Given a CLI-testable account environment with seeded data
        And a crud-test active account with code "crud-cli-dup" and name "Original"
        When I run the CLI with "account create --code crud-cli-dup --name Duplicate --type 1"
        Then stderr contains an error message
        And the exit code is 1
