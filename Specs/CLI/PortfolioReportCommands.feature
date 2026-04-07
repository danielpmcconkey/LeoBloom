Feature: Portfolio Report CLI Commands
    The `leobloom report` command group exposes four portfolio analysis
    subcommands: allocation, portfolio-summary, portfolio-history, and gains.
    These commands use the investment portfolio schema from P057-P059 to
    produce tabular CLI output. All four commands support a --json flag.
    These specs test CLI behavior only -- domain logic, SQL queries, and
    percentage calculations are covered by service-level specs.

    # ===================================================================
    # report --help includes new subcommands
    # ===================================================================

    @FT-RPP-001
    Scenario: Report --help lists the four portfolio report subcommands
        When I run the CLI with "report --help"
        Then stdout contains "allocation"
        And stdout contains "portfolio-summary"
        And stdout contains "portfolio-history"
        And stdout contains "gains"
        And the exit code is 0

    # ===================================================================
    # report allocation
    # ===================================================================

    # --- Happy Path ---

    @FT-RPP-010
    Scenario: Allocation report with no flags defaults to account-group dimension
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report allocation"
        Then stdout contains account group names
        And stdout contains allocation values and percentages
        And the exit code is 0

    @FT-RPP-011
    Scenario: Allocation report --by sector groups by sector dimension
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report allocation --by sector"
        Then stdout contains sector category names
        And stdout contains allocation values and percentages
        And the exit code is 0

    @FT-RPP-012
    Scenario Outline: Each valid --by dimension produces output without error
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report allocation --by <dimension>"
        Then stdout contains allocation values and percentages
        And the exit code is 0

        Examples:
            | dimension        |
            | tax-bucket       |
            | account-group    |
            | account          |
            | investment-type  |
            | market-cap       |
            | index-type       |
            | sector           |
            | region           |
            | objective        |
            | symbol           |

    @FT-RPP-013
    Scenario: Allocation report percentages sum to 100%
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report allocation"
        Then the allocation percentages in stdout sum to 100.0 within rounding tolerance
        And the exit code is 0

    @FT-RPP-014
    Scenario: Allocation report with --json flag outputs valid JSON
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report allocation --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Portfolio ---

    @FT-RPP-015
    Scenario: Allocation report with no positions produces informative message
        Given a CLI-testable portfolio report environment with no positions
        When I run the CLI with "report allocation"
        Then stdout contains an informative empty-portfolio message
        And the exit code is 0

    # --- Invalid Args ---

    @FT-RPP-016
    Scenario: Allocation report with an invalid dimension value surfaces an error
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report allocation --by not-a-dimension"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # report portfolio-summary
    # ===================================================================

    # --- Happy Path ---

    @FT-RPP-020
    Scenario: Portfolio summary shows total value, cost basis, and unrealized gain/loss
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report portfolio-summary"
        Then stdout contains total portfolio value
        And stdout contains total cost basis
        And stdout contains unrealized gain/loss dollar amount
        And stdout contains unrealized gain/loss percentage
        And the exit code is 0

    @FT-RPP-021
    Scenario: Portfolio summary shows tax bucket breakdown
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report portfolio-summary"
        Then stdout contains a tax bucket allocation breakdown
        And the exit code is 0

    @FT-RPP-022
    Scenario: Portfolio summary shows top 5 holdings
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report portfolio-summary"
        Then stdout contains a top holdings section with up to 5 entries
        And the exit code is 0

    @FT-RPP-023
    Scenario: Portfolio summary with --json flag outputs valid JSON
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report portfolio-summary --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Portfolio ---

    @FT-RPP-024
    Scenario: Portfolio summary with no positions produces informative message
        Given a CLI-testable portfolio report environment with no positions
        When I run the CLI with "report portfolio-summary"
        Then stdout contains an informative empty-portfolio message
        And the exit code is 0

    # ===================================================================
    # report portfolio-history
    # ===================================================================

    # --- Happy Path ---

    @FT-RPP-030
    Scenario: Portfolio history defaults to tax-bucket dimension and shows one row per position date
        Given a CLI-testable portfolio report environment with positions at multiple dates
        When I run the CLI with "report portfolio-history"
        Then stdout contains one row per distinct position date
        And stdout contains tax bucket category columns
        And the exit code is 0

    @FT-RPP-031
    Scenario: Portfolio history --by account-group groups by account group
        Given a CLI-testable portfolio report environment with positions at multiple dates
        When I run the CLI with "report portfolio-history --by account-group"
        Then stdout contains account group category columns
        And the exit code is 0

    @FT-RPP-032
    Scenario: Portfolio history --from and --to restrict the date range
        Given a CLI-testable portfolio report environment with positions at multiple dates
        When I run the CLI with "report portfolio-history --from 2026-01-01 --to 2026-01-31"
        Then stdout contains only rows with position dates within January 2026
        And the exit code is 0

    @FT-RPP-033
    Scenario: Portfolio history --from alone restricts to dates on or after the start date
        Given a CLI-testable portfolio report environment with positions at multiple dates
        When I run the CLI with "report portfolio-history --from 2026-03-01"
        Then stdout contains only rows with position dates on or after 2026-03-01
        And the exit code is 0

    @FT-RPP-034
    Scenario: Portfolio history with --json flag outputs valid JSON
        Given a CLI-testable portfolio report environment with positions at multiple dates
        When I run the CLI with "report portfolio-history --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Portfolio ---

    @FT-RPP-035
    Scenario: Portfolio history with no positions produces informative message
        Given a CLI-testable portfolio report environment with no positions
        When I run the CLI with "report portfolio-history"
        Then stdout contains an informative empty-portfolio message
        And the exit code is 0

    # --- Invalid Args ---

    @FT-RPP-036
    Scenario: Portfolio history with invalid date format prints error to stderr
        When I run the CLI with "report portfolio-history --from 01/01/2026"
        Then stderr contains an error message
        And the exit code is 1 or 2

    # ===================================================================
    # report gains
    # ===================================================================

    # --- Happy Path ---

    @FT-RPP-040
    Scenario: Gains report shows per-fund unrealized gain/loss with dollar and percentage values
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report gains"
        Then stdout contains per-fund gain/loss rows with symbol, cost basis, current value, dollar gain/loss, and percentage gain/loss
        And the exit code is 0

    @FT-RPP-041
    Scenario: Gains report includes totals row
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report gains"
        Then stdout contains total cost basis, total current value, and total gain/loss
        And the exit code is 0

    @FT-RPP-042
    Scenario: Gains report --account filters to positions for that account only
        Given a CLI-testable portfolio report environment with multiple accounts
        When I run the CLI with "report gains --account 1"
        Then stdout contains only gain/loss rows for account 1
        And the exit code is 0

    @FT-RPP-043
    Scenario: Gains report with --json flag outputs valid JSON
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report gains --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Empty Portfolio ---

    @FT-RPP-044
    Scenario: Gains report with no positions produces informative message
        Given a CLI-testable portfolio report environment with no positions
        When I run the CLI with "report gains"
        Then stdout contains an informative empty-portfolio message
        And the exit code is 0

    # --- Invalid Args ---

    @FT-RPP-045
    Scenario: Gains report with non-numeric account ID prints error to stderr
        When I run the CLI with "report gains --account not-an-id"
        Then stderr contains an error message
        And the exit code is 1 or 2

    # ===================================================================
    # --json flag consistency across all four commands
    # ===================================================================

    @FT-RPP-050
    Scenario Outline: --json flag produces valid JSON for all portfolio report subcommands
        Given a CLI-testable portfolio report environment
        When I run the CLI with "report <subcommand> --json"
        Then stdout is valid JSON
        And the exit code is 0

        Examples:
            | subcommand         |
            | allocation         |
            | portfolio-summary  |
            | portfolio-history  |
            | gains              |

    # ===================================================================
    # --help for each subcommand
    # ===================================================================

    @FT-RPP-060
    Scenario Outline: Each portfolio report subcommand prints help with --help
        When I run the CLI with "report <subcommand> --help"
        Then stdout contains usage information
        And the exit code is 0

        Examples:
            | subcommand         |
            | allocation         |
            | portfolio-summary  |
            | portfolio-history  |
            | gains              |
