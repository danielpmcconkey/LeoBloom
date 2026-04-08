Feature: Output Formatter — Empty List Messages
    When a list command returns no results, the formatter produces a
    descriptive message rather than empty output. This matches the dominant
    pattern already used by other list commands throughout the CLI.

    These scenarios pin the new empty-list behavior introduced in P072.
    FT-ICD-024 and the equivalent transfer scenario ("stdout is empty or
    contains only headers") remain valid — QE should broaden those step
    definitions to accept empty-list notice messages without touching the
    .feature files.

    # ===================================================================
    # invoice list — empty result
    # ===================================================================

    @FT-FMT-001
    Scenario: Invoice list with no matching results prints a descriptive message
        Given a CLI-testable invoice environment
        When I run the CLI with "invoice list --tenant "NonexistentTenant""
        Then stdout contains "(no invoices found)"
        And the exit code is 0

    # ===================================================================
    # transfer list — empty result
    # ===================================================================

    @FT-FMT-002
    Scenario: Transfer list with no matching results prints a descriptive message
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer list --status pending"
        Then stdout contains "(no transfers found)"
        And the exit code is 0
