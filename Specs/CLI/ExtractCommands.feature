Feature: Extract CLI Commands
    The `extract` top-level command exposes four JSON data-feed subcommands:
    account-tree, balances, positions, and je-lines. All four always emit
    JSON to stdout -- there is no human-readable formatter. The --json flag
    is accepted for consistency with other commands but is a no-op. These
    specs test CLI argument parsing, routing, and exit codes. Data-filtering
    behavioral contracts are covered in Specs/Behavioral/DataExtracts.feature.

    # ===================================================================
    # extract --help
    # ===================================================================

    @FT-EXT-001
    Scenario: Top-level --help includes the extract command group
        When I run the CLI with "--help"
        Then stdout contains "extract"
        And the exit code is 0

    @FT-EXT-002
    Scenario: Extract --help lists all four subcommands
        When I run the CLI with "extract --help"
        Then stdout contains "account-tree"
        And stdout contains "balances"
        And stdout contains "positions"
        And stdout contains "je-lines"
        And the exit code is 0

    @FT-EXT-003
    Scenario: Extract with no subcommand prints error to stderr
        When I run the CLI with "extract"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # extract account-tree
    # ===================================================================

    # --- Happy Path ---

    @FT-EXT-010
    Scenario: Account tree with no flags outputs valid JSON to stdout
        Given a CLI-testable extract environment
        When I run the CLI with "extract account-tree"
        Then stdout is valid JSON
        And the exit code is 0

    @FT-EXT-011
    Scenario: Account tree with --json flag still outputs valid JSON
        Given a CLI-testable extract environment
        When I run the CLI with "extract account-tree --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Help ---

    @FT-EXT-012
    Scenario: Account tree --help prints usage information
        When I run the CLI with "extract account-tree --help"
        Then stdout contains usage information
        And the exit code is 0

    # ===================================================================
    # extract balances
    # ===================================================================

    # --- Happy Path ---

    @FT-EXT-020
    Scenario: Balances with a valid --as-of date outputs valid JSON to stdout
        Given a CLI-testable extract environment
        When I run the CLI with "extract balances --as-of 2026-04-11"
        Then stdout is valid JSON
        And the exit code is 0

    @FT-EXT-021
    Scenario: Balances with --json flag still outputs valid JSON
        Given a CLI-testable extract environment
        When I run the CLI with "extract balances --as-of 2026-04-11 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-EXT-022
    Scenario: Balances with no --as-of flag prints error to stderr
        When I run the CLI with "extract balances"
        Then stderr contains an error message
        And the exit code is 2

    # --- Invalid Args ---

    @FT-EXT-023
    Scenario: Balances with invalid date format prints error to stderr
        When I run the CLI with "extract balances --as-of not-a-date"
        Then stderr contains an error message
        And the exit code is 1

    # --- Help ---

    @FT-EXT-024
    Scenario: Balances --help prints usage information
        When I run the CLI with "extract balances --help"
        Then stdout contains usage information
        And the exit code is 0

    # ===================================================================
    # extract positions
    # ===================================================================

    # --- Happy Path ---

    @FT-EXT-030
    Scenario: Positions with a valid --as-of date outputs valid JSON to stdout
        Given a CLI-testable extract environment
        When I run the CLI with "extract positions --as-of 2026-04-11"
        Then stdout is valid JSON
        And the exit code is 0

    @FT-EXT-031
    Scenario: Positions with no --as-of flag defaults to today and outputs valid JSON
        Given a CLI-testable extract environment
        When I run the CLI with "extract positions"
        Then stdout is valid JSON
        And the exit code is 0

    @FT-EXT-032
    Scenario: Positions with --json flag still outputs valid JSON
        Given a CLI-testable extract environment
        When I run the CLI with "extract positions --as-of 2026-04-11 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Invalid Args ---

    @FT-EXT-033
    Scenario: Positions with invalid date format prints error to stderr
        When I run the CLI with "extract positions --as-of 04/11/2026"
        Then stderr contains an error message
        And the exit code is 1

    # --- Help ---

    @FT-EXT-034
    Scenario: Positions --help prints usage information
        When I run the CLI with "extract positions --help"
        Then stdout contains usage information
        And the exit code is 0

    # ===================================================================
    # extract je-lines
    # ===================================================================

    # --- Happy Path ---

    @FT-EXT-040
    Scenario: JE lines with a valid --fiscal-period-id outputs valid JSON to stdout
        Given a CLI-testable extract environment
        When I run the CLI with "extract je-lines --fiscal-period-id 7"
        Then stdout is valid JSON
        And the exit code is 0

    @FT-EXT-041
    Scenario: JE lines with --json flag still outputs valid JSON
        Given a CLI-testable extract environment
        When I run the CLI with "extract je-lines --fiscal-period-id 7 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-EXT-042
    Scenario: JE lines with no --fiscal-period-id prints error to stderr
        When I run the CLI with "extract je-lines"
        Then stderr contains an error message
        And the exit code is 2

    # --- Invalid Args ---

    @FT-EXT-043
    Scenario: JE lines with non-numeric fiscal period ID prints error to stderr
        When I run the CLI with "extract je-lines --fiscal-period-id not-an-id"
        Then stderr contains an error message
        And the exit code is 1 or 2

    # --- Help ---

    @FT-EXT-044
    Scenario: JE lines --help prints usage information
        When I run the CLI with "extract je-lines --help"
        Then stdout contains usage information
        And the exit code is 0

    # ===================================================================
    # JSON output shape -- field names (snake_case contract)
    # ===================================================================

    @FT-EXT-050
    Scenario: Account tree JSON output uses snake_case field names
        Given a CLI-testable extract environment with at least one account
        When I run the CLI with "extract account-tree"
        Then stdout is valid JSON
        And the JSON contains field "account_type"
        And the JSON contains field "normal_balance"
        And the JSON contains field "is_active"
        And the exit code is 0

    @FT-EXT-051
    Scenario: Balances JSON output uses snake_case field names
        Given a CLI-testable extract environment with at least one account with postings
        When I run the CLI with "extract balances --as-of 2026-04-11"
        Then stdout is valid JSON
        And the JSON contains field "account_id"
        And the exit code is 0

    @FT-EXT-052
    Scenario: Positions JSON output uses snake_case field names
        Given a CLI-testable extract environment with at least one portfolio position
        When I run the CLI with "extract positions --as-of 2026-04-11"
        Then stdout is valid JSON
        And the JSON contains field "investment_account_id"
        And the JSON contains field "tax_bucket"
        And the JSON contains field "current_value"
        And the exit code is 0

    @FT-EXT-053
    Scenario: JE lines JSON output uses snake_case field names
        Given a CLI-testable extract environment with journal entries in fiscal period 7
        When I run the CLI with "extract je-lines --fiscal-period-id 7"
        Then stdout is valid JSON
        And the JSON contains field "journal_entry_id"
        And the JSON contains field "entry_type"
        And the JSON contains field "account_code"
        And the exit code is 0
