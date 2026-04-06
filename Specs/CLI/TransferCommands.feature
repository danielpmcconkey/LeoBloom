Feature: Transfer CLI Commands
    The transfer command group exposes initiate, confirm, show, and list
    subcommands. Each command parses arguments, calls the corresponding
    TransferService function, formats the result, and returns the correct
    exit code. These specs test CLI behavior only -- transfer domain logic
    (validation, journal entry posting) is covered by service-level tests
    in Transfers.feature.

    # ===================================================================
    # transfer initiate
    # ===================================================================

    # --- Happy Path ---

    @FT-TRC-001
    Scenario: Initiate a transfer with all required args
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer initiate --from-account 1010 --to-account 1020 --amount 500.00 --date 2026-04-01"
        Then stdout contains the initiated transfer details
        And the exit code is 0

    @FT-TRC-002
    Scenario: Initiate with optional args includes them in output
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer initiate --from-account 1010 --to-account 1020 --amount 500.00 --date 2026-04-01 --expected-settlement 2026-04-03 --description "Savings top-up""
        Then stdout contains "Savings top-up"
        And stdout contains "2026-04-03"
        And the exit code is 0

    @FT-TRC-003
    Scenario: Initiate with --json flag outputs valid JSON
        Given a CLI-testable transfer environment
        When I run the CLI with "--json transfer initiate --from-account 1010 --to-account 1020 --amount 500.00 --date 2026-04-01"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-TRC-004
    Scenario: Initiate with no arguments prints error to stderr
        When I run the CLI with "transfer initiate"
        Then stderr contains an error message
        And the exit code is 2

    @FT-TRC-005
    Scenario Outline: Initiate missing a required argument is rejected
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer initiate <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args                                                         | # missing        |
            | --to-account 1020 --amount 500.00 --date 2026-04-01                  | # no --from-account |
            | --from-account 1010 --amount 500.00 --date 2026-04-01                | # no --to-account   |
            | --from-account 1010 --to-account 1020 --date 2026-04-01              | # no --amount       |
            | --from-account 1010 --to-account 1020 --amount 500.00                | # no --date         |

    # --- Service Validation Error ---

    @FT-TRC-006
    Scenario: Initiate that triggers a service validation error surfaces it to stderr
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer initiate --from-account 1010 --to-account 1010 --amount 500.00 --date 2026-04-01"
        Then stderr contains an error message
        And the exit code is 1

    # --- Date Parse Error ---

    @FT-TRC-007
    Scenario: Initiate with invalid date format prints error to stderr
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer initiate --from-account 1010 --to-account 1020 --amount 500.00 --date not-a-date"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # transfer confirm
    # ===================================================================

    # --- Happy Path ---

    @FT-TRC-010
    Scenario: Confirm an initiated transfer via CLI
        Given a CLI-testable transfer environment
        And an initiated transfer with known ID
        When I run the CLI with "transfer confirm <transfer-id> --date 2026-04-15"
        Then stdout contains the confirmed transfer details
        And the exit code is 0

    @FT-TRC-011
    Scenario: Confirm with --json flag outputs valid JSON
        Given a CLI-testable transfer environment
        And an initiated transfer with known ID
        When I run the CLI with "--json transfer confirm <transfer-id> --date 2026-04-15"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-TRC-012
    Scenario: Confirm with no arguments prints error to stderr
        When I run the CLI with "transfer confirm"
        Then stderr contains an error message
        And the exit code is 2

    @FT-TRC-013
    Scenario: Confirm with missing --date flag prints error to stderr
        When I run the CLI with "transfer confirm 1"
        Then stderr contains an error message
        And the exit code is 1 or 2

    @FT-TRC-014
    Scenario: Confirm with invalid date format prints error to stderr
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer confirm 1 --date not-a-date"
        Then stderr contains an error message
        And the exit code is 1

    @FT-TRC-015
    Scenario: Confirm a nonexistent transfer prints error to stderr
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer confirm 999999 --date 2026-04-15"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # transfer show
    # ===================================================================

    # --- Happy Path ---

    @FT-TRC-020
    Scenario: Show an existing transfer via CLI
        Given a CLI-testable transfer environment
        And an initiated transfer with known ID
        When I run the CLI with "transfer show <transfer-id>"
        Then stdout contains the transfer details
        And the exit code is 0

    @FT-TRC-021
    Scenario: Show with --json flag outputs valid JSON
        Given a CLI-testable transfer environment
        And an initiated transfer with known ID
        When I run the CLI with "--json transfer show <transfer-id>"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-TRC-022
    Scenario: Show a nonexistent transfer prints error to stderr
        When I run the CLI with "transfer show 999999"
        Then stderr contains an error message
        And the exit code is 1

    @FT-TRC-023
    Scenario: Show with no transfer ID prints error to stderr
        When I run the CLI with "transfer show"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # transfer list
    # ===================================================================

    # --- Happy Path ---

    @FT-TRC-030
    Scenario: List all transfers with no filters
        Given a CLI-testable transfer environment
        And multiple transfers in various statuses
        When I run the CLI with "transfer list"
        Then stdout contains transfer summary rows
        And the exit code is 0

    @FT-TRC-031
    Scenario: List transfers filtered by status
        Given a CLI-testable transfer environment
        And multiple transfers in various statuses
        When I run the CLI with "transfer list --status initiated"
        Then stdout contains only transfers with status "initiated"
        And the exit code is 0

    @FT-TRC-032
    Scenario: List transfers filtered by date range
        Given a CLI-testable transfer environment
        And multiple transfers with different initiated dates
        When I run the CLI with "transfer list --from 2026-04-01 --to 2026-04-15"
        Then stdout contains only transfers within the date range
        And the exit code is 0

    @FT-TRC-033
    Scenario: List with --json flag outputs valid JSON
        Given a CLI-testable transfer environment
        And multiple transfers in various statuses
        When I run the CLI with "--json transfer list"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Results ---

    @FT-TRC-034
    Scenario: List with no matching results prints empty output
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer list --status initiated"
        Then stdout is empty or contains only headers
        And the exit code is 0

    # --- Invalid Filter ---

    @FT-TRC-035
    Scenario: List with invalid status value prints error to stderr
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer list --status bogus"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # transfer (no subcommand)
    # ===================================================================

    @FT-TRC-040
    Scenario: Transfer with no subcommand prints usage to stderr
        When I run the CLI with "transfer"
        Then stderr contains usage information
        And the exit code is 2

    # ===================================================================
    # --json flag consistency
    # ===================================================================

    @FT-TRC-050
    Scenario Outline: --json flag produces valid JSON for all subcommands
        Given a CLI-testable transfer environment
        And an initiated transfer with known ID
        When I run the CLI with "--json transfer <subcommand>"
        Then stdout is valid JSON

        Examples:
            | subcommand                                  |
            | show <transfer-id>                          |
            | confirm <transfer-id> --date 2026-04-15     |
            | list                                        |
