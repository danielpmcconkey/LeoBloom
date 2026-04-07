Feature: Obligation CLI Commands
    The obligation command group exposes agreement and instance subcommand
    groups plus top-level overdue and upcoming queries. Each command parses
    arguments, calls the corresponding obligation service function, formats
    the result, and returns the correct exit code. These specs test CLI
    behavior only -- obligation domain logic (status transitions, overdue
    detection, posting) is covered by service-level tests in the Ops specs.
    The upcoming query is new service logic tested here via CLI integration.

    # ===================================================================
    # obligation (no subcommand)
    # ===================================================================

    @FT-OBL-001
    Scenario: Obligation with no subcommand prints usage to stderr
        When I run the CLI with "obligation"
        Then stderr contains usage information
        And the exit code is 2

    @FT-OBL-002
    Scenario: Obligation agreement with no subcommand prints usage to stderr
        When I run the CLI with "obligation agreement"
        Then stderr contains usage information
        And the exit code is 2

    @FT-OBL-003
    Scenario: Obligation instance with no subcommand prints usage to stderr
        When I run the CLI with "obligation instance"
        Then stderr contains usage information
        And the exit code is 2

    # ===================================================================
    # obligation agreement list
    # ===================================================================

    # --- Happy Path ---

    @FT-OBL-010
    Scenario: List all active agreements with no filters
        Given a CLI-testable obligation environment with seeded agreements
        When I run the CLI with "obligation agreement list"
        Then stdout contains agreement summary rows
        And the exit code is 0

    @FT-OBL-011
    Scenario: List agreements filtered by type receivable
        Given a CLI-testable obligation environment with seeded agreements of mixed types
        When I run the CLI with "obligation agreement list --type receivable"
        Then stdout contains only agreements of type "receivable"
        And the exit code is 0

    @FT-OBL-012
    Scenario: List agreements filtered by cadence
        Given a CLI-testable obligation environment with seeded agreements of mixed cadences
        When I run the CLI with "obligation agreement list --cadence monthly"
        Then stdout contains only agreements with cadence "monthly"
        And the exit code is 0

    @FT-OBL-013
    Scenario: List agreements with --inactive includes inactive agreements
        Given a CLI-testable obligation environment with seeded agreements including inactive
        When I run the CLI with "obligation agreement list --inactive"
        Then stdout contains inactive agreement rows
        And the exit code is 0

    @FT-OBL-014
    Scenario: List agreements with --json flag outputs valid JSON
        Given a CLI-testable obligation environment with seeded agreements
        When I run the CLI with "obligation agreement list --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Results ---

    @FT-OBL-015
    Scenario: List agreements with no matching results prints empty output
        Given a CLI-testable obligation environment with seeded agreements
        When I run the CLI with "obligation agreement list --type payable --cadence annual"
        Then stdout is empty or contains only headers
        And the exit code is 0

    # ===================================================================
    # obligation agreement show
    # ===================================================================

    # --- Happy Path ---

    @FT-OBL-020
    Scenario: Show an existing agreement by ID
        Given a CLI-testable obligation environment with a seeded agreement
        When I run the CLI with "obligation agreement show <agreement-id>"
        Then stdout contains the agreement details
        And the exit code is 0

    @FT-OBL-021
    Scenario: Show agreement with --json flag outputs valid JSON
        Given a CLI-testable obligation environment with a seeded agreement
        When I run the CLI with "obligation agreement show <agreement-id> --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-OBL-022
    Scenario: Show a nonexistent agreement prints error to stderr
        Given a CLI-testable obligation environment with seeded agreements
        When I run the CLI with "obligation agreement show 999999"
        Then stderr contains an error message
        And the exit code is 1

    @FT-OBL-023
    Scenario: Show with no ID argument prints error to stderr
        When I run the CLI with "obligation agreement show"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # obligation agreement create
    # ===================================================================

    # --- Happy Path ---

    @FT-OBL-030
    Scenario: Create an agreement with all required args
        Given a CLI-testable obligation environment with seeded agreements
        When I run the CLI with "obligation agreement create --name "Rent Receivable" --type receivable --cadence monthly"
        Then stdout contains the created agreement details
        And the exit code is 0

    @FT-OBL-031
    Scenario: Create an agreement with optional args included in output
        Given a CLI-testable obligation environment with seeded agreements
        When I run the CLI with "obligation agreement create --name "Rent Receivable" --type receivable --cadence monthly --counterparty "Jeffrey" --amount 1200.00 --notes "Monthly rent""
        Then stdout contains "Jeffrey"
        And stdout contains "1200"
        And the exit code is 0

    @FT-OBL-032
    Scenario: Create agreement with --json flag outputs valid JSON
        Given a CLI-testable obligation environment with seeded agreements
        When I run the CLI with "obligation agreement create --name "Test Agreement" --type payable --cadence quarterly --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-OBL-033
    Scenario: Create with no arguments prints error to stderr
        When I run the CLI with "obligation agreement create"
        Then stderr contains an error message
        And the exit code is 1 or 2

    @FT-OBL-034
    Scenario Outline: Create missing a required argument is rejected
        When I run the CLI with "obligation agreement create <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args                               | # missing   |
            | --type receivable --cadence monthly        | # no --name |
            | --name "Test" --cadence monthly            | # no --type |
            | --name "Test" --type receivable            | # no --cadence |

    # ===================================================================
    # obligation agreement update
    # ===================================================================

    # --- Happy Path ---

    @FT-OBL-040
    Scenario: Update an agreement name
        Given a CLI-testable obligation environment with a seeded agreement
        When I run the CLI with "obligation agreement update <agreement-id> --name "Updated Name""
        Then stdout contains the updated agreement details
        And the exit code is 0

    @FT-OBL-041
    Scenario: Update agreement with --json flag outputs valid JSON
        Given a CLI-testable obligation environment with a seeded agreement
        When I run the CLI with "obligation agreement update <agreement-id> --name "Updated" --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-OBL-042
    Scenario: Update a nonexistent agreement prints error to stderr
        Given a CLI-testable obligation environment with seeded agreements
        When I run the CLI with "obligation agreement update 999999 --name "Test""
        Then stderr contains an error message
        And the exit code is 1

    @FT-OBL-043
    Scenario: Update with no ID argument prints error to stderr
        When I run the CLI with "obligation agreement update"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # obligation agreement deactivate
    # ===================================================================

    # --- Happy Path ---

    @FT-OBL-050
    Scenario: Deactivate an active agreement
        Given a CLI-testable obligation environment with an active seeded agreement
        When I run the CLI with "obligation agreement deactivate <agreement-id>"
        Then stdout contains the deactivated agreement details
        And the exit code is 0

    @FT-OBL-051
    Scenario: Deactivate with --json flag outputs valid JSON
        Given a CLI-testable obligation environment with an active seeded agreement
        When I run the CLI with "obligation agreement deactivate <agreement-id> --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-OBL-052
    Scenario: Deactivate a nonexistent agreement prints error to stderr
        Given a CLI-testable obligation environment with seeded agreements
        When I run the CLI with "obligation agreement deactivate 999999"
        Then stderr contains an error message
        And the exit code is 1

    @FT-OBL-053
    Scenario: Deactivate with no ID argument prints error to stderr
        When I run the CLI with "obligation agreement deactivate"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # obligation instance list
    # ===================================================================

    # --- Happy Path ---

    @FT-OBL-060
    Scenario: List all instances with no filters
        Given a CLI-testable obligation environment with seeded instances
        When I run the CLI with "obligation instance list"
        Then stdout contains instance summary rows
        And the exit code is 0

    @FT-OBL-061
    Scenario: List instances filtered by status
        Given a CLI-testable obligation environment with seeded instances of mixed statuses
        When I run the CLI with "obligation instance list --status expected"
        Then stdout contains only instances with status "expected"
        And the exit code is 0

    @FT-OBL-062
    Scenario: List instances filtered by due-before date
        Given a CLI-testable obligation environment with seeded instances across multiple dates
        When I run the CLI with "obligation instance list --due-before 2026-04-30"
        Then stdout contains only instances due on or before 2026-04-30
        And the exit code is 0

    @FT-OBL-063
    Scenario: List instances filtered by due-after date
        Given a CLI-testable obligation environment with seeded instances across multiple dates
        When I run the CLI with "obligation instance list --due-after 2026-04-01"
        Then stdout contains only instances due on or after 2026-04-01
        And the exit code is 0

    @FT-OBL-064
    Scenario: List instances with --json flag outputs valid JSON
        Given a CLI-testable obligation environment with seeded instances
        When I run the CLI with "obligation instance list --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Results ---

    @FT-OBL-065
    Scenario: List instances with no matching results prints empty output
        Given a CLI-testable obligation environment with seeded instances
        When I run the CLI with "obligation instance list --status posted --due-before 2020-01-01"
        Then stdout is empty or contains only headers
        And the exit code is 0

    # ===================================================================
    # obligation instance spawn
    # ===================================================================

    # --- Happy Path ---

    @FT-OBL-070
    Scenario: Spawn instances for an agreement over a date range
        Given a CLI-testable obligation environment with a seeded monthly agreement
        When I run the CLI with "obligation instance spawn <agreement-id> --from 2026-05-01 --to 2026-07-31"
        Then stdout contains the spawn result with created instance count
        And the exit code is 0

    @FT-OBL-071
    Scenario: Spawn with --json flag outputs valid JSON
        Given a CLI-testable obligation environment with a seeded monthly agreement
        When I run the CLI with "obligation instance spawn <agreement-id> --from 2026-05-01 --to 2026-05-31 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-OBL-072
    Scenario: Spawn for a nonexistent agreement prints error to stderr
        Given a CLI-testable obligation environment with seeded agreements
        When I run the CLI with "obligation instance spawn 999999 --from 2026-05-01 --to 2026-05-31"
        Then stderr contains an error message
        And the exit code is 1

    @FT-OBL-073
    Scenario Outline: Spawn missing a required argument is rejected
        Given a CLI-testable obligation environment with a seeded monthly agreement
        When I run the CLI with "obligation instance spawn <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args                                             | # missing       |
            | <agreement-id> --to 2026-05-31                           | # no --from     |
            | <agreement-id> --from 2026-05-01                         | # no --to       |
            | --from 2026-05-01 --to 2026-05-31                        | # no agreement  |

    # ===================================================================
    # obligation instance transition
    # ===================================================================

    # --- Happy Path ---

    @FT-OBL-080
    Scenario: Transition an instance to a new status
        Given a CLI-testable obligation environment with a seeded instance in status "expected"
        When I run the CLI with "obligation instance transition <instance-id> --to in_flight"
        Then stdout contains the transitioned instance details
        And the exit code is 0

    @FT-OBL-081
    Scenario: Transition with optional args included in result
        Given a CLI-testable obligation environment with a seeded instance in status "in_flight"
        When I run the CLI with "obligation instance transition <instance-id> --to confirmed --amount 1200.00 --date 2026-04-15 --notes "Payment received""
        Then stdout contains "1200"
        And stdout contains "2026-04-15"
        And the exit code is 0

    @FT-OBL-082
    Scenario: Transition with --json flag outputs valid JSON
        Given a CLI-testable obligation environment with a seeded instance in status "expected"
        When I run the CLI with "obligation instance transition <instance-id> --to in_flight --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-OBL-083
    Scenario: Transition a nonexistent instance prints error to stderr
        Given a CLI-testable obligation environment with seeded instances
        When I run the CLI with "obligation instance transition 999999 --to in_flight"
        Then stderr contains an error message
        And the exit code is 1

    @FT-OBL-084
    Scenario: Transition without --to flag prints error to stderr
        When I run the CLI with "obligation instance transition 1"
        Then stderr contains an error message
        And the exit code is 1 or 2

    @FT-OBL-085
    Scenario: Transition with no arguments prints error to stderr
        When I run the CLI with "obligation instance transition"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # obligation instance post
    # ===================================================================

    # --- Happy Path ---

    @FT-OBL-090
    Scenario: Post a confirmed instance to ledger
        Given a CLI-testable obligation environment with a seeded instance in status "confirmed"
        When I run the CLI with "obligation instance post <instance-id>"
        Then stdout contains the post result with journal entry reference
        And the exit code is 0

    @FT-OBL-091
    Scenario: Post with --json flag outputs valid JSON
        Given a CLI-testable obligation environment with a seeded instance in status "confirmed"
        When I run the CLI with "obligation instance post <instance-id> --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-OBL-092
    Scenario: Post a nonexistent instance prints error to stderr
        Given a CLI-testable obligation environment with seeded instances
        When I run the CLI with "obligation instance post 999999"
        Then stderr contains an error message
        And the exit code is 1

    @FT-OBL-093
    Scenario: Post with no ID argument prints error to stderr
        When I run the CLI with "obligation instance post"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # obligation overdue
    # ===================================================================

    # --- Happy Path ---

    @FT-OBL-100
    Scenario: Overdue detection with no flags defaults to today
        Given a CLI-testable obligation environment with overdue instances as of today
        When I run the CLI with "obligation overdue"
        Then stdout contains the overdue detection result with transitioned count
        And the exit code is 0

    @FT-OBL-101
    Scenario: Overdue detection with --as-of date uses that date as reference
        Given a CLI-testable obligation environment with instances expected before 2026-04-01
        When I run the CLI with "obligation overdue --as-of 2026-04-01"
        Then stdout contains the overdue detection result with transitioned count
        And the exit code is 0

    @FT-OBL-102
    Scenario: Overdue detection with no eligible instances reports zero transitioned
        Given a CLI-testable obligation environment with no overdue instances
        When I run the CLI with "obligation overdue --as-of 2026-01-01"
        Then stdout contains "0" transitioned
        And the exit code is 0

    @FT-OBL-103
    Scenario: Overdue detection with --json flag outputs valid JSON
        Given a CLI-testable obligation environment with seeded instances
        When I run the CLI with "obligation overdue --as-of 2026-04-01 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-OBL-104
    Scenario: Overdue with invalid date format prints error to stderr
        When I run the CLI with "obligation overdue --as-of not-a-date"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # obligation upcoming
    # ===================================================================

    # --- Happy Path ---

    @FT-OBL-110
    Scenario: Upcoming with no flags defaults to 30-day horizon
        Given a CLI-testable obligation environment with instances due within the next 30 days
        When I run the CLI with "obligation upcoming"
        Then stdout contains instance summary rows
        And the exit code is 0

    @FT-OBL-111
    Scenario: Upcoming with --days 7 returns only instances within 7 days
        Given a CLI-testable obligation environment with instances at various future dates
        When I run the CLI with "obligation upcoming --days 7"
        Then stdout contains only instances due within 7 days from today
        And the exit code is 0

    @FT-OBL-112
    Scenario: Upcoming includes instances in "expected" status
        Given a CLI-testable obligation environment with an instance in status "expected" due within 7 days
        When I run the CLI with "obligation upcoming --days 7"
        Then stdout contains that instance
        And the exit code is 0

    @FT-OBL-113
    Scenario: Upcoming includes instances in "in_flight" status
        Given a CLI-testable obligation environment with an instance in status "in_flight" due within 7 days
        When I run the CLI with "obligation upcoming --days 7"
        Then stdout contains that instance
        And the exit code is 0

    @FT-OBL-114
    Scenario: Upcoming excludes instances beyond the horizon
        Given a CLI-testable obligation environment with an instance due 31 days from today
        When I run the CLI with "obligation upcoming --days 30"
        Then stdout does not contain that instance
        And the exit code is 0

    @FT-OBL-115
    Scenario: Upcoming excludes confirmed instances
        Given a CLI-testable obligation environment with instances in mixed statuses due within 7 days
        When I run the CLI with "obligation upcoming --days 7"
        Then stdout does not contain instances in status "confirmed"
        And the exit code is 0

    @FT-OBL-116
    Scenario: Upcoming with no matching instances prints empty output
        Given a CLI-testable obligation environment with no upcoming instances
        When I run the CLI with "obligation upcoming --days 7"
        Then stdout is empty or contains only headers
        And the exit code is 0

    @FT-OBL-117
    Scenario: Upcoming with --json flag outputs valid JSON
        Given a CLI-testable obligation environment with instances due within the next 30 days
        When I run the CLI with "obligation upcoming --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-OBL-118
    Scenario: Upcoming with invalid --days value prints error to stderr
        When I run the CLI with "obligation upcoming --days notanumber"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # --json flag consistency
    # ===================================================================

    @FT-OBL-150
    Scenario Outline: --json flag produces valid JSON for read-only obligation subcommands
        Given a CLI-testable obligation environment with seeded agreements and instances
        When I run the CLI with "obligation <subcommand>"
        Then stdout is valid JSON
        And the exit code is 0

        Examples:
            | subcommand                        |
            | agreement list --json             |
            | agreement show <agreement-id> --json |
            | instance list --json              |
            | upcoming --days 30 --json         |
