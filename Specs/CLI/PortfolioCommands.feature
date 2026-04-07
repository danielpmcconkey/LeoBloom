Feature: Portfolio CLI Commands
    The portfolio command group exposes nested subcommands for managing investment
    accounts (account), funds (fund), and positions (position). This is the first
    nested command group in the CLI -- commands take the form
    `leobloom portfolio <subgroup> <subcommand> [args]`. Each command parses
    arguments, calls the corresponding service function, formats the result, and
    returns the correct exit code. These specs test CLI behavior only -- domain
    logic and persistence are covered by service-level specs in the Portfolio
    feature directory.

    # ===================================================================
    # portfolio (no subcommand)
    # ===================================================================

    @FT-PFC-001
    Scenario: Portfolio with no subcommand prints usage to stderr
        When I run the CLI with "portfolio"
        Then stderr contains usage information
        And the exit code is 2

    # ===================================================================
    # portfolio account create
    # ===================================================================

    # --- Happy Path ---

    @FT-PFC-010
    Scenario: Create an investment account with all required args
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio account create --name "Brokerage" --tax-bucket-id 1 --account-group-id 1"
        Then stdout contains the created account details
        And stdout contains "Brokerage"
        And the exit code is 0

    @FT-PFC-011
    Scenario: Create an account with --json flag outputs valid JSON
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio account create --name "Brokerage" --tax-bucket-id 1 --account-group-id 1 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing / Invalid Args ---

    @FT-PFC-012
    Scenario: Create account with no arguments prints error to stderr
        When I run the CLI with "portfolio account create"
        Then stderr contains an error message
        And the exit code is 2

    @FT-PFC-013
    Scenario Outline: Create account missing a required argument is rejected
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio account create <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args                                      | # missing           |
            | --tax-bucket-id 1 --account-group-id 1            | # no --name         |
            | --name "Brokerage" --account-group-id 1           | # no --tax-bucket-id |
            | --name "Brokerage" --tax-bucket-id 1              | # no --account-group-id |

    @FT-PFC-014
    Scenario: Create account with blank name surfaces validation error
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio account create --name "" --tax-bucket-id 1 --account-group-id 1"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # portfolio account list
    # ===================================================================

    # --- Happy Path ---

    @FT-PFC-020
    Scenario: List all investment accounts
        Given a CLI-testable portfolio environment
        And at least one investment account exists
        When I run the CLI with "portfolio account list"
        Then stdout contains account summary rows
        And the exit code is 0

    @FT-PFC-021
    Scenario: List accounts with --json flag outputs valid JSON
        Given a CLI-testable portfolio environment
        And at least one investment account exists
        When I run the CLI with "portfolio account list --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Results ---

    @FT-PFC-022
    Scenario: List accounts with no accounts in the database prints empty output
        Given a CLI-testable portfolio environment with no accounts
        When I run the CLI with "portfolio account list"
        Then stdout is empty or contains only headers
        And the exit code is 0

    # ===================================================================
    # portfolio fund create
    # ===================================================================

    # --- Happy Path ---

    @FT-PFC-030
    Scenario: Create a fund with only required args
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio fund create --symbol VTI --name "Vanguard Total Stock Market""
        Then stdout contains the created fund details
        And stdout contains "VTI"
        And the exit code is 0

    @FT-PFC-031
    Scenario: Create a fund with optional dimension IDs
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio fund create --symbol SPY --name "SPDR S&P 500" --investment-type-id 1 --market-cap-id 1"
        Then stdout contains the created fund details
        And stdout contains "SPY"
        And the exit code is 0

    @FT-PFC-032
    Scenario: Create a fund with --json flag outputs valid JSON
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio fund create --symbol VTI --name "Vanguard Total Stock Market" --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing / Invalid Args ---

    @FT-PFC-033
    Scenario: Create fund with no arguments prints error to stderr
        When I run the CLI with "portfolio fund create"
        Then stderr contains an error message
        And the exit code is 2

    @FT-PFC-034
    Scenario Outline: Create fund missing a required argument is rejected
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio fund create <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args                              | # missing    |
            | --name "Vanguard Total Stock Market"      | # no --symbol |
            | --symbol VTI                              | # no --name  |

    # --- Duplicate Symbol ---

    @FT-PFC-035
    Scenario: Create fund with a duplicate symbol surfaces an error
        Given a CLI-testable portfolio environment
        And a fund with symbol "VTI" already exists
        When I run the CLI with "portfolio fund create --symbol VTI --name "Duplicate Fund""
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # portfolio fund list
    # ===================================================================

    # --- Happy Path ---

    @FT-PFC-040
    Scenario: List all funds with no filters
        Given a CLI-testable portfolio environment
        And multiple funds exist
        When I run the CLI with "portfolio fund list"
        Then stdout contains fund summary rows
        And the exit code is 0

    @FT-PFC-041
    Scenario Outline: List funds filtered by a single dimension
        Given a CLI-testable portfolio environment
        And multiple funds exist with varying dimension attributes
        When I run the CLI with "portfolio fund list <filter>"
        Then stdout contains only matching fund rows
        And the exit code is 0

        Examples:
            | filter                  |
            | --investment-type-id 1  |
            | --market-cap-id 1       |
            | --index-type-id 1       |
            | --sector-id 1           |
            | --region-id 1           |
            | --objective-id 1        |

    @FT-PFC-042
    Scenario: List funds with --json flag outputs valid JSON
        Given a CLI-testable portfolio environment
        And multiple funds exist
        When I run the CLI with "portfolio fund list --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Filter Conflict ---

    @FT-PFC-043
    Scenario: List funds with multiple dimension filters surfaces an error
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio fund list --investment-type-id 1 --market-cap-id 1"
        Then stderr contains an error message
        And the exit code is 1

    # --- Empty Results ---

    @FT-PFC-044
    Scenario: List funds with no matching dimension produces empty output
        Given a CLI-testable portfolio environment
        And multiple funds exist
        When I run the CLI with "portfolio fund list --investment-type-id 999999"
        Then stdout is empty or contains only headers
        And the exit code is 0

    # ===================================================================
    # portfolio fund show
    # ===================================================================

    # --- Happy Path ---

    @FT-PFC-050
    Scenario: Show a fund by its symbol
        Given a CLI-testable portfolio environment
        And a fund with symbol "VTI" exists
        When I run the CLI with "portfolio fund show VTI"
        Then stdout contains the fund details
        And stdout contains "VTI"
        And the exit code is 0

    @FT-PFC-051
    Scenario: Show a fund with --json flag outputs valid JSON
        Given a CLI-testable portfolio environment
        And a fund with symbol "VTI" exists
        When I run the CLI with "portfolio fund show VTI --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-PFC-052
    Scenario: Show a nonexistent fund symbol prints error to stderr
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio fund show ZZZZ"
        Then stderr contains an error message
        And the exit code is 1

    @FT-PFC-053
    Scenario: Show fund with no symbol argument prints error to stderr
        When I run the CLI with "portfolio fund show"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # portfolio position record
    # ===================================================================

    # --- Happy Path ---

    @FT-PFC-060
    Scenario: Record a position with all required args
        Given a CLI-testable portfolio environment
        And investment account 1 exists
        And a fund with symbol "VTI" exists
        When I run the CLI with "portfolio position record --account-id 1 --symbol VTI --date 2026-04-01 --price 220.50 --quantity 10 --value 2205.00 --cost-basis 2100.00"
        Then stdout contains the recorded position details
        And stdout contains "VTI"
        And the exit code is 0

    @FT-PFC-061
    Scenario: Record a position with --json flag outputs valid JSON
        Given a CLI-testable portfolio environment
        And investment account 1 exists
        And a fund with symbol "VTI" exists
        When I run the CLI with "portfolio position record --account-id 1 --symbol VTI --date 2026-04-01 --price 220.50 --quantity 10 --value 2205.00 --cost-basis 2100.00 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing Args ---

    @FT-PFC-062
    Scenario: Record position with no arguments prints error to stderr
        When I run the CLI with "portfolio position record"
        Then stderr contains an error message
        And the exit code is 2

    @FT-PFC-063
    Scenario Outline: Record position missing a required argument is rejected
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio position record <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args                                                                                       | # missing        |
            | --symbol VTI --date 2026-04-01 --price 220.50 --quantity 10 --value 2205.00 --cost-basis 2100.00  | # no --account-id |
            | --account-id 1 --date 2026-04-01 --price 220.50 --quantity 10 --value 2205.00 --cost-basis 2100.00 | # no --symbol    |
            | --account-id 1 --symbol VTI --price 220.50 --quantity 10 --value 2205.00 --cost-basis 2100.00      | # no --date      |
            | --account-id 1 --symbol VTI --date 2026-04-01 --quantity 10 --value 2205.00 --cost-basis 2100.00   | # no --price     |
            | --account-id 1 --symbol VTI --date 2026-04-01 --price 220.50 --value 2205.00 --cost-basis 2100.00  | # no --quantity  |
            | --account-id 1 --symbol VTI --date 2026-04-01 --price 220.50 --quantity 10 --cost-basis 2100.00    | # no --value     |
            | --account-id 1 --symbol VTI --date 2026-04-01 --price 220.50 --quantity 10 --value 2205.00         | # no --cost-basis |

    # --- Validation Errors ---

    @FT-PFC-064
    Scenario: Record position with negative price surfaces validation error
        Given a CLI-testable portfolio environment
        And investment account 1 exists
        And a fund with symbol "VTI" exists
        When I run the CLI with "portfolio position record --account-id 1 --symbol VTI --date 2026-04-01 --price -1.00 --quantity 10 --value 2205.00 --cost-basis 2100.00"
        Then stderr contains an error message
        And the exit code is 1

    @FT-PFC-065
    Scenario: Record position referencing a nonexistent fund symbol surfaces an error
        Given a CLI-testable portfolio environment
        And investment account 1 exists
        When I run the CLI with "portfolio position record --account-id 1 --symbol ZZZZ --date 2026-04-01 --price 100.00 --quantity 5 --value 500.00 --cost-basis 480.00"
        Then stderr contains an error message
        And the exit code is 1

    @FT-PFC-066
    Scenario: Record position with invalid date format surfaces an error
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio position record --account-id 1 --symbol VTI --date not-a-date --price 220.50 --quantity 10 --value 2205.00 --cost-basis 2100.00"
        Then stderr contains an error message
        And the exit code is 1 or 2

    # ===================================================================
    # portfolio position list
    # ===================================================================

    # --- Happy Path ---

    @FT-PFC-070
    Scenario: List all positions with no filters
        Given a CLI-testable portfolio environment
        And multiple positions exist across accounts
        When I run the CLI with "portfolio position list"
        Then stdout contains position summary rows
        And the exit code is 0

    @FT-PFC-071
    Scenario: List positions filtered by account ID
        Given a CLI-testable portfolio environment
        And multiple positions exist across accounts
        When I run the CLI with "portfolio position list --account-id 1"
        Then stdout contains only position rows for account 1
        And the exit code is 0

    @FT-PFC-072
    Scenario: List positions filtered by date range
        Given a CLI-testable portfolio environment
        And multiple positions exist across different dates
        When I run the CLI with "portfolio position list --start-date 2026-01-01 --end-date 2026-03-31"
        Then stdout contains only position rows within the date range
        And the exit code is 0

    @FT-PFC-073
    Scenario: List positions with --json flag outputs valid JSON
        Given a CLI-testable portfolio environment
        And multiple positions exist across accounts
        When I run the CLI with "portfolio position list --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Results ---

    @FT-PFC-074
    Scenario: List positions for an account with no positions produces empty output
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio position list --account-id 999999"
        Then stdout is empty or contains only headers
        And the exit code is 0

    # ===================================================================
    # portfolio position latest
    # ===================================================================

    # --- Happy Path ---

    @FT-PFC-080
    Scenario: Latest positions for all accounts
        Given a CLI-testable portfolio environment
        And multiple positions exist across accounts
        When I run the CLI with "portfolio position latest"
        Then stdout contains the latest position rows for all accounts
        And the exit code is 0

    @FT-PFC-081
    Scenario: Latest positions filtered by account ID
        Given a CLI-testable portfolio environment
        And multiple positions exist across accounts
        When I run the CLI with "portfolio position latest --account-id 1"
        Then stdout contains only the latest position rows for account 1
        And the exit code is 0

    @FT-PFC-082
    Scenario: Latest positions with --json flag outputs valid JSON
        Given a CLI-testable portfolio environment
        And multiple positions exist across accounts
        When I run the CLI with "portfolio position latest --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Results ---

    @FT-PFC-083
    Scenario: Latest positions for an account with no positions produces empty output
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio position latest --account-id 999999"
        Then stdout is empty or contains only headers
        And the exit code is 0

    # ===================================================================
    # --json flag consistency
    # ===================================================================

    @FT-PFC-090
    Scenario Outline: --json flag produces valid JSON for all portfolio account subcommands
        Given a CLI-testable portfolio environment
        And at least one investment account exists
        When I run the CLI with "portfolio <subcommand>"
        Then stdout is valid JSON
        And the exit code is 0

        Examples:
            | subcommand                                                                             |
            | account list --json                                                                    |
            | account create --name "JsonTest" --tax-bucket-id 1 --account-group-id 1 --json        |

    @FT-PFC-091
    Scenario Outline: --json flag produces valid JSON for all portfolio fund subcommands
        Given a CLI-testable portfolio environment
        And a fund with symbol "VTI" exists
        When I run the CLI with "portfolio <subcommand>"
        Then stdout is valid JSON
        And the exit code is 0

        Examples:
            | subcommand                                                           |
            | fund list --json                                                     |
            | fund show VTI --json                                                 |
            | fund create --symbol JSON1 --name "JSON Fund" --json                 |

    @FT-PFC-092
    Scenario Outline: --json flag produces valid JSON for all portfolio position subcommands
        Given a CLI-testable portfolio environment
        And multiple positions exist across accounts
        When I run the CLI with "portfolio <subcommand>"
        Then stdout is valid JSON
        And the exit code is 0

        Examples:
            | subcommand                |
            | position list --json      |
            | position latest --json    |
