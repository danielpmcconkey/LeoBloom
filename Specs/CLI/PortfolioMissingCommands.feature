Feature: Portfolio CLI Missing Commands
    Four portfolio CLI commands specified in P059 but never implemented:
    `portfolio account show <id>`, `portfolio account list --group <name>`,
    `portfolio account list --tax-bucket <name>`, and `portfolio dimensions`.
    Each command follows the same layered pattern as the rest of the portfolio
    CLI: parse args, call service, format output, return exit code. These specs
    test CLI behavior only -- domain logic and persistence are covered by
    service-level specs in the Portfolio feature directory.

    # ===================================================================
    # portfolio account show
    # ===================================================================

    # --- Happy Path ---

    @FT-PFC-100
    Scenario: Show an account by ID displays account detail
        Given a CLI-testable portfolio environment
        And an investment account exists
        When I run the CLI with "portfolio account show 1"
        Then stdout contains the account detail
        And the exit code is 0

    @FT-PFC-101
    Scenario: Show an account with recorded positions includes latest position summary
        Given a CLI-testable portfolio environment
        And investment account 1 exists
        And a fund with symbol "VTI" exists
        And a position for account 1 in fund "VTI" is recorded
        When I run the CLI with "portfolio account show 1"
        Then stdout contains the account detail
        And stdout contains the latest position summary
        And the exit code is 0

    @FT-PFC-102
    Scenario: Show an account with --json flag outputs valid JSON
        Given a CLI-testable portfolio environment
        And an investment account exists
        When I run the CLI with "portfolio account show 1 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-PFC-103
    Scenario: Show a nonexistent account ID prints error to stderr
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio account show 999999"
        Then stderr contains an error message
        And the exit code is 1

    @FT-PFC-104
    Scenario: Show account with no ID argument prints error to stderr
        When I run the CLI with "portfolio account show"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # portfolio account list --group
    # ===================================================================

    # --- Happy Path ---

    @FT-PFC-110
    Scenario: List accounts filtered by group name returns only matching accounts
        Given a CLI-testable portfolio environment
        And investment accounts exist in multiple groups
        When I run the CLI with "portfolio account list --group "Taxable""
        Then stdout contains only accounts in group "Taxable"
        And the exit code is 0

    @FT-PFC-111
    Scenario: List accounts by group with --json flag outputs valid JSON
        Given a CLI-testable portfolio environment
        And investment accounts exist in multiple groups
        When I run the CLI with "portfolio account list --group "Taxable" --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Results ---

    @FT-PFC-112
    Scenario: List accounts by group with no matching name produces empty output
        Given a CLI-testable portfolio environment
        And at least one investment account exists
        When I run the CLI with "portfolio account list --group "NonexistentGroup""
        Then stdout is empty or contains only headers
        And the exit code is 0

    # ===================================================================
    # portfolio account list --tax-bucket
    # ===================================================================

    # --- Happy Path ---

    @FT-PFC-120
    Scenario: List accounts filtered by tax bucket name returns only matching accounts
        Given a CLI-testable portfolio environment
        And investment accounts exist in multiple tax buckets
        When I run the CLI with "portfolio account list --tax-bucket "Traditional IRA""
        Then stdout contains only accounts in tax bucket "Traditional IRA"
        And the exit code is 0

    @FT-PFC-121
    Scenario: List accounts by tax bucket with --json flag outputs valid JSON
        Given a CLI-testable portfolio environment
        And investment accounts exist in multiple tax buckets
        When I run the CLI with "portfolio account list --tax-bucket "Traditional IRA" --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Results ---

    @FT-PFC-122
    Scenario: List accounts by tax bucket with no matching name produces empty output
        Given a CLI-testable portfolio environment
        And at least one investment account exists
        When I run the CLI with "portfolio account list --tax-bucket "NonexistentBucket""
        Then stdout is empty or contains only headers
        And the exit code is 0

    # ===================================================================
    # portfolio dimensions
    # ===================================================================

    # --- Happy Path ---

    @FT-PFC-130
    Scenario: List all dimension tables with their values
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio dimensions"
        Then stdout contains entries for all 8 dimension tables
        And the exit code is 0

    @FT-PFC-131
    Scenario: Dimensions with --json flag outputs valid JSON
        Given a CLI-testable portfolio environment
        When I run the CLI with "portfolio dimensions --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Tables ---

    @FT-PFC-132
    Scenario: Dimensions command with empty tables still exits 0
        Given a CLI-testable portfolio environment with no dimension data
        When I run the CLI with "portfolio dimensions"
        Then stdout contains entries for all 8 dimension tables
        And stdout shows empty values for each dimension table
        And the exit code is 0
