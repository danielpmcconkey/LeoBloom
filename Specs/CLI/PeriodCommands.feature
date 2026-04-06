Feature: Period CLI Commands
    The period command group exposes list, close, reopen, and create
    subcommands for fiscal period management. Each command parses
    arguments, calls the corresponding FiscalPeriodService function,
    formats the result, and returns the correct exit code. These specs
    test CLI behavior only -- fiscal period close/reopen domain
    validation is covered by service-level tests in
    CloseReopenFiscalPeriod.feature.

    # ===================================================================
    # period (no subcommand)
    # ===================================================================

    @FT-PRD-001
    Scenario: Period with no subcommand prints usage to stderr
        When I run the CLI with "period"
        Then stderr contains usage information
        And the exit code is 2

    # ===================================================================
    # period list
    # ===================================================================

    # --- Happy Path ---

    @FT-PRD-010
    Scenario: List all fiscal periods
        Given a CLI-testable period environment with seeded data
        When I run the CLI with "period list"
        Then stdout contains fiscal period summary rows
        And the exit code is 0

    @FT-PRD-011
    Scenario: List fiscal periods with --json flag outputs valid JSON
        Given a CLI-testable period environment with seeded data
        When I run the CLI with "period list --json"
        Then stdout is valid JSON
        And the exit code is 0

    # ===================================================================
    # period close
    # ===================================================================

    # --- Happy Path ---

    @FT-PRD-020
    Scenario: Close an open period by numeric ID
        Given a CLI-testable period environment with an open period
        When I run the CLI with "period close <period-id>"
        Then stdout contains the closed period details
        And the exit code is 0

    @FT-PRD-021
    Scenario: Close an open period by period key
        Given a CLI-testable period environment with an open period
        When I run the CLI with "period close 2026-04"
        Then stdout contains the closed period details
        And the exit code is 0

    @FT-PRD-022
    Scenario: Close with --json flag outputs valid JSON
        Given a CLI-testable period environment with an open period
        When I run the CLI with "period close <period-id> --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-PRD-023
    Scenario: Close a nonexistent period prints error to stderr
        Given a CLI-testable period environment with seeded data
        When I run the CLI with "period close 999999"
        Then stderr contains an error message
        And the exit code is 1

    @FT-PRD-024
    Scenario: Close a nonexistent period by key prints error to stderr
        Given a CLI-testable period environment with seeded data
        When I run the CLI with "period close nonexistent-key"
        Then stderr contains an error message
        And the exit code is 1

    @FT-PRD-025
    Scenario: Close with no argument prints error to stderr
        When I run the CLI with "period close"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # period reopen
    # ===================================================================

    # --- Happy Path ---

    @FT-PRD-030
    Scenario: Reopen a closed period by ID with reason
        Given a CLI-testable period environment with a closed period
        When I run the CLI with "period reopen <period-id> --reason "audit adjustment""
        Then stdout contains the reopened period details
        And the exit code is 0

    @FT-PRD-031
    Scenario: Reopen a closed period by key with reason
        Given a CLI-testable period environment with a closed period
        When I run the CLI with "period reopen 2026-03 --reason "correction needed""
        Then stdout contains the reopened period details
        And the exit code is 0

    @FT-PRD-032
    Scenario: Reopen with --json flag outputs valid JSON
        Given a CLI-testable period environment with a closed period
        When I run the CLI with "period reopen <period-id> --reason "audit" --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-PRD-033
    Scenario: Reopen without --reason flag prints error to stderr
        When I run the CLI with "period reopen 1"
        Then stderr contains an error message
        And the exit code is 1 or 2

    @FT-PRD-034
    Scenario: Reopen a nonexistent period prints error to stderr
        Given a CLI-testable period environment with seeded data
        When I run the CLI with "period reopen 999999 --reason "test""
        Then stderr contains an error message
        And the exit code is 1

    @FT-PRD-035
    Scenario: Reopen with no arguments prints error to stderr
        When I run the CLI with "period reopen"
        Then stderr contains an error message
        And the exit code is 1 or 2

    # ===================================================================
    # period create
    # ===================================================================

    # --- Happy Path ---

    @FT-PRD-040
    Scenario: Create a new fiscal period with all required args
        Given a CLI-testable period environment with seeded data
        When I run the CLI with "period create --start 2026-05-01 --end 2026-05-31 --key "2026-05""
        Then stdout contains the created period details
        And the exit code is 0

    @FT-PRD-041
    Scenario: Create with --json flag outputs valid JSON
        Given a CLI-testable period environment with seeded data
        When I run the CLI with "period create --start 2026-06-01 --end 2026-06-30 --key "2026-06" --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-PRD-042
    Scenario: Create with no arguments prints error to stderr
        When I run the CLI with "period create"
        Then stderr contains an error message
        And the exit code is 1 or 2

    @FT-PRD-043
    Scenario Outline: Create missing a required argument is rejected
        When I run the CLI with "period create <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args                                | # missing  |
            | --end 2026-05-31 --key "2026-05"            | # no --start |
            | --start 2026-05-01 --key "2026-05"          | # no --end   |
            | --start 2026-05-01 --end 2026-05-31         | # no --key   |

    # --- Validation Errors ---

    @FT-PRD-044
    Scenario: Create with invalid date format prints error to stderr
        When I run the CLI with "period create --start not-a-date --end 2026-05-31 --key "2026-05""
        Then stderr contains an error message
        And the exit code is 1

    @FT-PRD-045
    Scenario: Create with start after end prints error to stderr
        Given a CLI-testable period environment with seeded data
        When I run the CLI with "period create --start 2026-05-31 --end 2026-05-01 --key "backwards""
        Then stderr contains an error message
        And the exit code is 1

    @FT-PRD-046
    Scenario: Create with duplicate period key prints error to stderr
        Given a CLI-testable period environment with seeded data
        And an existing period with key "2026-01"
        When I run the CLI with "period create --start 2026-01-01 --end 2026-01-31 --key "2026-01""
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # --json flag consistency
    # ===================================================================

    @FT-PRD-050
    Scenario Outline: --json flag produces valid JSON for all period subcommands
        Given a CLI-testable period environment with seeded data
        When I run the CLI with "period <subcommand>"
        Then stdout is valid JSON
        And the exit code is 0

        Examples:
            | subcommand                                                            |
            | list --json                                                           |
            | create --start 2026-07-01 --end 2026-07-31 --key "2026-07" --json     |
