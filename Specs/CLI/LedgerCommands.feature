Feature: Ledger CLI Commands
    The ledger command group exposes post, void, and show subcommands.
    Each command parses arguments, calls the corresponding service
    function, formats the result, and returns the correct exit code.
    These specs test CLI behavior only -- service validation logic is
    covered by 400+ existing service-level tests.

    # ===================================================================
    # ledger post
    # ===================================================================

    # --- Happy Path ---

    @FT-LCD-001
    Scenario: Post a valid journal entry via CLI
        Given a CLI-testable ledger environment
        When I run the CLI with "ledger post --debit 1010:1000.00 --credit 4010:1000.00 --date 2026-03-15 --description "March rent" --fiscal-period-id <period-id>"
        Then stdout contains the posted entry details
        And the exit code is 0

    @FT-LCD-002
    Scenario: Post with --json flag outputs JSON to stdout
        Given a CLI-testable ledger environment
        When I run the CLI with "--json ledger post --debit 1010:1000.00 --credit 4010:1000.00 --date 2026-03-15 --description "March rent" --fiscal-period-id <period-id>"
        Then stdout is valid JSON
        And the exit code is 0

    @FT-LCD-003
    Scenario: Post with optional --source flag includes source in output
        Given a CLI-testable ledger environment
        When I run the CLI with "ledger post --debit 1010:1000.00 --credit 4010:1000.00 --date 2026-03-15 --description "Manual adj" --source "manual" --fiscal-period-id <period-id>"
        Then stdout contains "manual"
        And the exit code is 0

    @FT-LCD-004
    Scenario: Post with --ref flag includes references in output
        Given a CLI-testable ledger environment
        When I run the CLI with "ledger post --debit 1010:1000.00 --credit 4010:1000.00 --date 2026-03-15 --description "With ref" --fiscal-period-id <period-id> --ref cheque:1234"
        Then stdout contains "cheque"
        And stdout contains "1234"
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-LCD-005
    Scenario: Post with no arguments prints error to stderr
        When I run the CLI with "ledger post"
        Then stderr contains an error message
        And the exit code is 2

    @FT-LCD-006
    Scenario Outline: Post missing a required argument is rejected
        Given a CLI-testable ledger environment
        When I run the CLI with "ledger post <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args                                                                    | # missing        |
            | --credit 4010:1000.00 --date 2026-03-15 --description "X" --fiscal-period-id 1  | # no --debit     |
            | --debit 1010:1000.00 --date 2026-03-15 --description "X" --fiscal-period-id 1   | # no --credit    |
            | --debit 1010:1000.00 --credit 4010:1000.00 --description "X" --fiscal-period-id 1 | # no --date    |
            | --debit 1010:1000.00 --credit 4010:1000.00 --date 2026-03-15 --fiscal-period-id 1 | # no --desc    |
            # last row ("# no --fp-id") removed by P083: --fiscal-period-id is now optional (see FT-LCD-053)

    # --- Service Error Surfacing ---

    @FT-LCD-007
    Scenario: Post that triggers a service validation error surfaces it to stderr
        Given a CLI-testable ledger environment
        When I run the CLI with "ledger post --debit 1010:1000.00 --credit 4010:500.00 --date 2026-03-15 --description "Unbalanced" --fiscal-period-id <period-id>"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # ledger void
    # ===================================================================

    # --- Happy Path ---

    @FT-LCD-010
    Scenario: Void an existing entry via CLI
        Given a CLI-testable ledger environment
        And a posted journal entry with known ID
        When I run the CLI with "ledger void <entry-id> --reason "Duplicate posting""
        Then stdout contains the voided entry details
        And the exit code is 0

    @FT-LCD-011
    Scenario: Void with --json flag outputs JSON to stdout
        Given a CLI-testable ledger environment
        And a posted journal entry with known ID
        When I run the CLI with "--json ledger void <entry-id> --reason "Duplicate""
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-LCD-012
    Scenario: Void a nonexistent entry prints error to stderr
        When I run the CLI with "ledger void 999999 --reason "Does not exist""
        Then stderr contains an error message
        And the exit code is 1

    @FT-LCD-013
    Scenario: Void with no arguments prints error to stderr
        When I run the CLI with "ledger void"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # ledger show
    # ===================================================================

    # --- Happy Path ---

    @FT-LCD-020
    Scenario: Show an existing entry via CLI
        Given a CLI-testable ledger environment
        And a posted journal entry with known ID
        When I run the CLI with "ledger show <entry-id>"
        Then stdout contains the entry details with lines
        And the exit code is 0

    @FT-LCD-021
    Scenario: Show with --json flag outputs JSON to stdout
        Given a CLI-testable ledger environment
        And a posted journal entry with known ID
        When I run the CLI with "--json ledger show <entry-id>"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-LCD-022
    Scenario: Show a nonexistent entry prints error to stderr
        When I run the CLI with "ledger show 999999"
        Then stderr contains an error message
        And the exit code is 1

    @FT-LCD-023
    Scenario: Show with no entry ID prints error to stderr
        When I run the CLI with "ledger show"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # --json flag consistency
    # ===================================================================

    @FT-LCD-030
    Scenario Outline: --json flag produces valid JSON for all commands
        Given a CLI-testable ledger environment
        And a posted journal entry with known ID
        When I run the CLI with "--json ledger <subcommand>"
        Then stdout is valid JSON

        Examples:
            | subcommand                                          |
            | show <entry-id>                                     |
            | void <entry-id> --reason "JSON test"                |
            | reverse --journal-entry-id <entry-id>               |

    # ===================================================================
    # ledger reverse
    # ===================================================================

    # --- Happy Path ---

    @FT-LCD-040
    Scenario: Reverse an existing entry via CLI
        Given a CLI-testable ledger environment
        And a posted journal entry with known ID
        When I run the CLI with "ledger reverse --journal-entry-id <entry-id>"
        Then stdout contains the reversed entry details
        And the exit code is 0

    @FT-LCD-041
    Scenario: Reverse with --json flag outputs JSON to stdout
        Given a CLI-testable ledger environment
        And a posted journal entry with known ID
        When I run the CLI with "--json ledger reverse --journal-entry-id <entry-id>"
        Then stdout is valid JSON
        And the exit code is 0

    @FT-LCD-042
    Scenario: Reverse with --date flag uses the provided date
        Given a CLI-testable ledger environment
        And a posted journal entry with known ID
        When I run the CLI with "ledger reverse --journal-entry-id <entry-id> --date 2026-03-20"
        Then stdout contains the reversed entry details
        And the exit code is 0

    # --- Error Paths ---

    @FT-LCD-043
    Scenario: Reverse a nonexistent entry prints error to stderr
        When I run the CLI with "ledger reverse --journal-entry-id 999999"
        Then stderr contains an error message
        And the exit code is 1

    @FT-LCD-044
    Scenario: Reverse with no arguments prints error to stderr
        When I run the CLI with "ledger reverse"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # ledger post — new optional flags
    # ===================================================================

    @FT-LCD-050
    Scenario: Post with --adjustment-for-period flag includes adjustmentForPeriodId in output
        Given a CLI-testable ledger environment
        When I run the CLI with "--json ledger post --debit 1010:500.00 --credit 4010:500.00 --date 2026-03-15 --description "Adj entry" --adjustment-for-period <prior-period-id>"
        Then stdout is valid JSON
        And stdout contains "adjustmentForPeriodId"
        And the exit code is 0

    @FT-LCD-051
    Scenario: Post with --adjustment-for-period referencing a nonexistent period is rejected
        Given a CLI-testable ledger environment
        When I run the CLI with "ledger post --debit 1010:500.00 --credit 4010:500.00 --date 2026-03-15 --description "Bad adj" --adjustment-for-period 999999"
        Then stderr contains an error message
        And the exit code is 1

    @FT-LCD-052
    Scenario: Post with --fiscal-period-id override routes entry to the specified period
        Given a CLI-testable ledger environment
        When I run the CLI with "ledger post --debit 1010:1000.00 --credit 4010:1000.00 --date 2026-03-15 --description "Override" --fiscal-period-id <period-id>"
        Then stdout contains the posted entry details
        And the exit code is 0

    @FT-LCD-053
    Scenario: Post without --fiscal-period-id derives the period from entry_date
        Given a CLI-testable ledger environment
        When I run the CLI with "ledger post --debit 1010:1000.00 --credit 4010:1000.00 --date 2026-03-15 --description "Auto-period""
        Then stdout contains the posted entry details
        And the exit code is 0

    # Note: FT-LCD-006 last example row ("# no --fp-id") must be removed by the Builder
    # because --fiscal-period-id is now optional (derives from entry_date when omitted).
