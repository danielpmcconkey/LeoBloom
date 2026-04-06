Feature: Invoice CLI Commands
    The invoice command group exposes record, show, and list subcommands.
    Each command parses arguments, calls the corresponding InvoiceService
    function, formats the result, and returns the correct exit code.
    These specs test CLI behavior only -- invoice domain logic and
    persistence are covered by service-level tests (P021).

    # ===================================================================
    # invoice record
    # ===================================================================

    # --- Happy Path ---

    @FT-ICD-001
    Scenario: Record an invoice with all required and optional args
        Given a CLI-testable invoice environment
        When I run the CLI with "invoice record --tenant "Jeffrey" --fiscal-period-id <period-id> --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50 --generated-at "2026-04-01T12:00Z" --document-path "/docs/inv-001.pdf" --notes "April charges""
        Then stdout contains the recorded invoice details
        And the exit code is 0

    @FT-ICD-002
    Scenario: Record an invoice with --json flag outputs valid JSON
        Given a CLI-testable invoice environment
        When I run the CLI with "--json invoice record --tenant "Jeffrey" --fiscal-period-id <period-id> --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50 --generated-at "2026-04-01T12:00Z""
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-ICD-003
    Scenario: Record with no arguments prints error to stderr
        When I run the CLI with "invoice record"
        Then stderr contains an error message
        And the exit code is 2

    @FT-ICD-004
    Scenario Outline: Record missing a required argument is rejected
        Given a CLI-testable invoice environment
        When I run the CLI with "invoice record <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args                                                                                                                                  | # missing             |
            | --fiscal-period-id 1 --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50 --generated-at "2026-04-01T12:00Z"                    | # no --tenant         |
            | --tenant "Jeffrey" --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50 --generated-at "2026-04-01T12:00Z"                      | # no --fiscal-period-id |
            | --tenant "Jeffrey" --fiscal-period-id 1 --utility-share 85.50 --total-amount 1285.50 --generated-at "2026-04-01T12:00Z"                       | # no --rent-amount    |
            | --tenant "Jeffrey" --fiscal-period-id 1 --rent-amount 1200.00 --total-amount 1285.50 --generated-at "2026-04-01T12:00Z"                       | # no --utility-share  |
            | --tenant "Jeffrey" --fiscal-period-id 1 --rent-amount 1200.00 --utility-share 85.50 --generated-at "2026-04-01T12:00Z"                        | # no --total-amount   |
            | --tenant "Jeffrey" --fiscal-period-id 1 --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50                                    | # no --generated-at   |

    # --- Service Validation Error ---

    @FT-ICD-005
    Scenario: Record that triggers a service validation error surfaces it to stderr
        Given a CLI-testable invoice environment
        When I run the CLI with "invoice record --tenant "Jeffrey" --fiscal-period-id 999999 --rent-amount 1200.00 --utility-share 85.50 --total-amount 1285.50 --generated-at "2026-04-01T12:00Z""
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # invoice show
    # ===================================================================

    # --- Happy Path ---

    @FT-ICD-010
    Scenario: Show an existing invoice via CLI
        Given a CLI-testable invoice environment
        And a recorded invoice with known ID
        When I run the CLI with "invoice show <invoice-id>"
        Then stdout contains the invoice details
        And the exit code is 0

    @FT-ICD-011
    Scenario: Show with --json flag outputs valid JSON
        Given a CLI-testable invoice environment
        And a recorded invoice with known ID
        When I run the CLI with "--json invoice show <invoice-id>"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Error Paths ---

    @FT-ICD-012
    Scenario: Show a nonexistent invoice prints error to stderr
        When I run the CLI with "invoice show 999999"
        Then stderr contains an error message
        And the exit code is 1

    @FT-ICD-013
    Scenario: Show with no invoice ID prints error to stderr
        When I run the CLI with "invoice show"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # invoice list
    # ===================================================================

    # --- Happy Path ---

    @FT-ICD-020
    Scenario: List all invoices with no filters
        Given a CLI-testable invoice environment
        And multiple recorded invoices
        When I run the CLI with "invoice list"
        Then stdout contains invoice summary rows
        And the exit code is 0

    @FT-ICD-021
    Scenario: List invoices filtered by tenant
        Given a CLI-testable invoice environment
        And multiple recorded invoices for different tenants
        When I run the CLI with "invoice list --tenant "Jeffrey""
        Then stdout contains only invoices for tenant "Jeffrey"
        And the exit code is 0

    @FT-ICD-022
    Scenario: List invoices filtered by fiscal period
        Given a CLI-testable invoice environment
        And multiple recorded invoices across different fiscal periods
        When I run the CLI with "invoice list --fiscal-period-id <period-id>"
        Then stdout contains only invoices for the specified fiscal period
        And the exit code is 0

    @FT-ICD-023
    Scenario: List with --json flag outputs valid JSON
        Given a CLI-testable invoice environment
        And multiple recorded invoices
        When I run the CLI with "--json invoice list"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Results ---

    @FT-ICD-024
    Scenario: List with no matching results prints empty output
        Given a CLI-testable invoice environment
        When I run the CLI with "invoice list --tenant "NonexistentTenant""
        Then stdout is empty or contains only headers
        And the exit code is 0
