Feature: Report CLI Commands
    The report command group exposes tax report subcommands: schedule-e,
    general-ledger, cash-receipts, and cash-disbursements. Each command
    parses arguments, calls the corresponding Reporting service, formats
    the result as a human-readable table, and returns the correct exit
    code. These specs test CLI behavior only -- report assembly logic
    and COA-to-Schedule-E mapping are covered by service-level tests.
    None of these commands support --json output.

    # ===================================================================
    # report command group
    # ===================================================================

    @FT-RPT-001
    Scenario: Top-level --help includes the report command group
        When I run the CLI with "--help"
        Then stdout contains "report"
        And the exit code is 0

    @FT-RPT-002
    Scenario: Report --help prints available subcommands
        When I run the CLI with "report --help"
        Then stdout contains "schedule-e"
        And stdout contains "general-ledger"
        And stdout contains "cash-receipts"
        And stdout contains "cash-disbursements"
        And stdout contains "projection"
        And the exit code is 0

    @FT-RPT-003
    Scenario: Report with no subcommand prints error to stderr
        When I run the CLI with "report"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # report schedule-e
    # ===================================================================

    # --- Happy Path ---

    @FT-RPT-010
    Scenario: Schedule E report for a valid year produces formatted output
        Given a CLI-testable reporting environment with seeded data
        When I run the CLI with "report schedule-e --year 2026"
        Then stdout contains IRS Schedule E line item headers
        And stdout contains a line for rental income
        And stdout contains a line for depreciation
        And the exit code is 0

    @FT-RPT-011
    Scenario: Schedule E "Other" line 19 shows sub-detail breakdown
        Given a CLI-testable reporting environment with seeded data
        When I run the CLI with "report schedule-e --year 2026"
        Then stdout contains "Other" with sub-detail lines
        And the exit code is 0

    @FT-RPT-012
    Scenario: Schedule E depreciation line shows account 5190 balance
        Given a CLI-testable reporting environment with seeded data
        When I run the CLI with "report schedule-e --year 2026"
        Then stdout contains the depreciation line from account balance
        And the exit code is 0

    # --- Argument Errors ---

    @FT-RPT-013
    Scenario: Schedule E with no --year flag prints error to stderr
        When I run the CLI with "report schedule-e"
        Then stderr contains an error message
        And the exit code is 1 or 2

    @FT-RPT-014
    Scenario: Schedule E with non-numeric year prints error to stderr
        When I run the CLI with "report schedule-e --year not-a-year"
        Then stderr contains an error message
        And the exit code is 1

    # --- Service Error Surfacing ---

    @FT-RPT-015
    Scenario: Schedule E service validation error surfaces to stderr
        When I run the CLI with "report schedule-e --year -1"
        Then stderr contains an error message
        And the exit code is 1

    # --- No --json ---

    @FT-RPT-016
    Scenario: Schedule E does not accept --json flag
        When I run the CLI with "--json report schedule-e --year 2026"
        Then stderr contains an error message
        And the exit code is 1 or 2

    # ===================================================================
    # report general-ledger
    # ===================================================================

    # --- Happy Path ---

    @FT-RPT-020
    Scenario: General ledger for a valid account and date range produces formatted output
        Given a CLI-testable reporting environment with seeded data
        When I run the CLI with "report general-ledger --account 1110 --from 2026-01-01 --to 2026-12-31"
        Then stdout contains transaction detail with date, description, and amounts
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-RPT-021
    Scenario Outline: General ledger missing a required argument is rejected
        When I run the CLI with "report general-ledger <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args                                  | # missing     |
            | --from 2026-01-01 --to 2026-12-31             | # no --account |
            | --account 1110 --to 2026-12-31                | # no --from    |
            | --account 1110 --from 2026-01-01              | # no --to      |

    # --- Invalid Args ---

    @FT-RPT-022
    Scenario: General ledger with invalid date format prints error to stderr
        When I run the CLI with "report general-ledger --account 1110 --from not-a-date --to 2026-12-31"
        Then stderr contains an error message
        And the exit code is 1

    @FT-RPT-023
    Scenario: General ledger with nonexistent account surfaces service error
        Given a CLI-testable reporting environment with seeded data
        When I run the CLI with "report general-ledger --account 9999 --from 2026-01-01 --to 2026-12-31"
        Then stderr contains an error message
        And the exit code is 1

    # --- No --json ---

    @FT-RPT-024
    Scenario: General ledger does not accept --json flag
        When I run the CLI with "--json report general-ledger --account 1110 --from 2026-01-01 --to 2026-12-31"
        Then stderr contains an error message
        And the exit code is 1 or 2

    # ===================================================================
    # report cash-receipts
    # ===================================================================

    # --- Happy Path ---

    @FT-RPT-030
    Scenario: Cash receipts for a valid date range produces formatted output
        Given a CLI-testable reporting environment with seeded data
        When I run the CLI with "report cash-receipts --from 2026-01-01 --to 2026-12-31"
        Then stdout contains cash inflow entries with counterparty and date
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-RPT-031
    Scenario Outline: Cash receipts missing a required argument is rejected
        When I run the CLI with "report cash-receipts <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args          | # missing  |
            | --to 2026-12-31       | # no --from |
            | --from 2026-01-01     | # no --to   |

    # --- Invalid Args ---

    @FT-RPT-032
    Scenario: Cash receipts with invalid date format prints error to stderr
        When I run the CLI with "report cash-receipts --from 01/01/2026 --to 2026-12-31"
        Then stderr contains an error message
        And the exit code is 1

    # --- No --json ---

    @FT-RPT-033
    Scenario: Cash receipts does not accept --json flag
        When I run the CLI with "--json report cash-receipts --from 2026-01-01 --to 2026-12-31"
        Then stderr contains an error message
        And the exit code is 1 or 2

    # ===================================================================
    # report cash-disbursements
    # ===================================================================

    # --- Happy Path ---

    @FT-RPT-040
    Scenario: Cash disbursements for a valid date range produces formatted output
        Given a CLI-testable reporting environment with seeded data
        When I run the CLI with "report cash-disbursements --from 2026-01-01 --to 2026-12-31"
        Then stdout contains cash outflow entries with counterparty and date
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-RPT-041
    Scenario Outline: Cash disbursements missing a required argument is rejected
        When I run the CLI with "report cash-disbursements <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args          | # missing  |
            | --to 2026-12-31       | # no --from |
            | --from 2026-01-01     | # no --to   |

    # --- Invalid Args ---

    @FT-RPT-042
    Scenario: Cash disbursements with invalid date format prints error to stderr
        When I run the CLI with "report cash-disbursements --from not-a-date --to 2026-12-31"
        Then stderr contains an error message
        And the exit code is 1

    # --- No --json ---

    @FT-RPT-043
    Scenario: Cash disbursements does not accept --json flag
        When I run the CLI with "--json report cash-disbursements --from 2026-01-01 --to 2026-12-31"
        Then stderr contains an error message
        And the exit code is 1 or 2

    # ===================================================================
    # Shared patterns across all report subcommands
    # ===================================================================

    @FT-RPT-050
    Scenario Outline: Each report subcommand prints help with --help
        When I run the CLI with "report <subcommand> --help"
        Then stdout contains usage information
        And the exit code is 0

        Examples:
            | subcommand          |
            | schedule-e          |
            | general-ledger      |
            | cash-receipts       |
            | cash-disbursements  |
            | projection          |

    # ===================================================================
    # report projection
    # ===================================================================

    # --- Happy Path ---

    @FT-RPT-060
    Scenario: Projection for a valid account and future date produces formatted daily series
        Given a CLI-testable projection environment with seeded data
        When I run the CLI with "report projection --account 1110 --through 2026-04-30"
        Then stdout contains a table with daily balance entries
        And the exit code is 0

    @FT-RPT-061
    Scenario: Projection for an account with no future items shows flat balance line
        Given a CLI-testable projection environment with no future obligations or transfers
        When I run the CLI with "report projection --account 1110 --through 2026-04-10"
        Then stdout contains balance entries with the same amount for every day
        And the exit code is 0

    @FT-RPT-062
    Scenario: Projection output flags null-amount obligations as unknown outflow
        Given a CLI-testable projection environment with a null-amount payable for account 1110 on 2026-04-10
        When I run the CLI with "report projection --account 1110 --through 2026-04-12"
        Then stdout contains "unknown outflow"
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-RPT-063
    Scenario Outline: Projection missing a required argument is rejected
        When I run the CLI with "report projection <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args              | # missing      |
            | --through 2026-04-30      | # no --account |
            | --account 1110            | # no --through |

    # --- Invalid Args ---

    @FT-RPT-064
    Scenario: Projection with invalid date format prints error to stderr
        When I run the CLI with "report projection --account 1110 --through 04/30/2026"
        Then stderr contains an error message
        And the exit code is 1

    @FT-RPT-065
    Scenario: Projection with a past date is rejected
        Given a CLI-testable projection environment with seeded data
        When I run the CLI with "report projection --account 1110 --through 2026-01-01"
        Then stderr contains an error message
        And the exit code is 1

    @FT-RPT-066
    Scenario: Projection with nonexistent account surfaces service error
        Given a CLI-testable projection environment with seeded data
        When I run the CLI with "report projection --account 9999 --through 2026-04-30"
        Then stderr contains an error message
        And the exit code is 1

    # --- No --json ---

    @FT-RPT-067
    Scenario: Projection does not accept --json flag
        When I run the CLI with "--json report projection --account 1110 --through 2026-04-30"
        Then stderr contains an error message
        And the exit code is 1 or 2
