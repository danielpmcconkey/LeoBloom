Feature: Accounting Report CLI Commands
    The report command group is extended with 5 accounting report
    subcommands: trial-balance, balance-sheet, income-statement,
    pnl-subtree, and account-balance. Each is a thin CLI wrapper around
    an existing Ledger service. These specs test CLI behavior only --
    report calculation logic is covered by service-level tests in
    Specs/Behavioral/ (TrialBalance.feature, BalanceSheet.feature,
    IncomeStatement.feature, SubtreePLReport.feature,
    AccountBalance.feature).

    # ===================================================================
    # report --help includes new subcommands
    # ===================================================================

    @FT-RPT-100
    Scenario: Report --help lists all 5 new accounting subcommands
        When I run the CLI with "report --help"
        Then stdout contains "trial-balance"
        And stdout contains "balance-sheet"
        And stdout contains "income-statement"
        And stdout contains "pnl-subtree"
        And stdout contains "account-balance"
        And the exit code is 0

    @FT-RPT-101
    Scenario Outline: Each new report subcommand prints help with --help
        When I run the CLI with "report <subcommand> --help"
        Then stdout contains usage information
        And the exit code is 0

        Examples:
            | subcommand        |
            | trial-balance     |
            | balance-sheet     |
            | income-statement  |
            | pnl-subtree       |
            | account-balance   |

    # ===================================================================
    # report trial-balance
    # ===================================================================

    # --- Happy Path ---

    @FT-RPT-110
    Scenario: Trial balance by period ID produces human-readable output
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report trial-balance --period 7"
        Then stdout contains separate debit and credit columns
        And stdout contains a balanced/unbalanced status line
        And the exit code is 0

    @FT-RPT-111
    Scenario: Trial balance by period key produces human-readable output
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report trial-balance --period 2026-01"
        Then stdout contains separate debit and credit columns
        And stdout contains a balanced/unbalanced status line
        And the exit code is 0

    @FT-RPT-112
    Scenario: Trial balance with --json flag outputs valid JSON
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report trial-balance --period 7 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-RPT-113
    Scenario: Trial balance with no --period flag prints error to stderr
        When I run the CLI with "report trial-balance"
        Then stderr contains an error message
        And the exit code is 2

    # --- Invalid Args ---

    @FT-RPT-114
    Scenario: Trial balance with nonexistent period surfaces service error
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report trial-balance --period 999999"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # report balance-sheet
    # ===================================================================

    # --- Happy Path ---

    @FT-RPT-120
    Scenario: Balance sheet as of a valid date produces human-readable output
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report balance-sheet --as-of 2026-03-31"
        Then stdout contains "Assets"
        And stdout contains "Liabilities"
        And stdout contains "Equity"
        And stdout contains "Retained Earnings"
        And stdout contains a balanced/unbalanced status line
        And the exit code is 0

    @FT-RPT-121
    Scenario: Balance sheet with --json flag outputs valid JSON
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report balance-sheet --as-of 2026-03-31 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-RPT-122
    Scenario: Balance sheet with no --as-of flag prints error to stderr
        When I run the CLI with "report balance-sheet"
        Then stderr contains an error message
        And the exit code is 2

    # --- Invalid Args ---

    @FT-RPT-123
    Scenario: Balance sheet with invalid date format prints error to stderr
        When I run the CLI with "report balance-sheet --as-of not-a-date"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # report income-statement
    # ===================================================================

    # --- Happy Path ---

    @FT-RPT-130
    Scenario: Income statement by period ID produces human-readable output
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report income-statement --period 7"
        Then stdout contains "Revenue"
        And stdout contains "Expenses"
        And stdout contains "Net Income"
        And the exit code is 0

    @FT-RPT-131
    Scenario: Income statement by period key produces human-readable output
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report income-statement --period 2026-01"
        Then stdout contains "Revenue"
        And stdout contains "Expenses"
        And stdout contains "Net Income"
        And the exit code is 0

    @FT-RPT-132
    Scenario: Income statement with --json flag outputs valid JSON
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report income-statement --period 7 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-RPT-133
    Scenario: Income statement with no --period flag prints error to stderr
        When I run the CLI with "report income-statement"
        Then stderr contains an error message
        And the exit code is 2

    # --- Invalid Args ---

    @FT-RPT-134
    Scenario: Income statement with nonexistent period surfaces service error
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report income-statement --period 999999"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # report pnl-subtree
    # ===================================================================

    # --- Happy Path ---

    @FT-RPT-140
    Scenario: P&L subtree with valid account and period produces human-readable output
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report pnl-subtree --account 5000 --period 7"
        Then stdout contains the root account code and name
        And stdout contains "Net Income"
        And the exit code is 0

    @FT-RPT-141
    Scenario: P&L subtree with --json flag outputs valid JSON
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report pnl-subtree --account 5000 --period 7 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-RPT-142
    Scenario Outline: P&L subtree missing a required argument is rejected
        When I run the CLI with "report pnl-subtree <partial-args>"
        Then stderr contains an error message
        And the exit code is 1 or 2

        Examples:
            | partial-args    | # missing    |
            | --period 7      | # no --account |
            | --account 5000  | # no --period  |

    # --- Invalid Args ---

    @FT-RPT-143
    Scenario: P&L subtree with nonexistent account surfaces service error
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report pnl-subtree --account 9999 --period 7"
        Then stderr contains an error message
        And the exit code is 1

    @FT-RPT-144
    Scenario: P&L subtree with nonexistent period surfaces service error
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report pnl-subtree --account 5000 --period 999999"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # report account-balance
    # ===================================================================

    # --- Happy Path ---

    @FT-RPT-150
    Scenario: Account balance with explicit --as-of produces human-readable output
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report account-balance --account 1110 --as-of 2026-03-31"
        Then stdout contains the account code
        And stdout contains the normal balance type
        And stdout contains the balance amount
        And the exit code is 0

    @FT-RPT-151
    Scenario: Account balance defaults --as-of to today when omitted
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report account-balance --account 1110"
        Then stdout contains the account code
        And stdout contains the balance amount
        And the exit code is 0

    @FT-RPT-152
    Scenario: Account balance with --json flag outputs valid JSON
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report account-balance --account 1110 --as-of 2026-03-31 --json"
        Then stdout is valid JSON
        And the exit code is 0

    # --- Missing Required Args ---

    @FT-RPT-153
    Scenario: Account balance with no --account flag prints error to stderr
        When I run the CLI with "report account-balance"
        Then stderr contains an error message
        And the exit code is 2

    # --- Invalid Args ---

    @FT-RPT-154
    Scenario: Account balance with invalid date format prints error to stderr
        When I run the CLI with "report account-balance --account 1110 --as-of not-a-date"
        Then stderr contains an error message
        And the exit code is 1

    @FT-RPT-155
    Scenario: Account balance with nonexistent account surfaces service error
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report account-balance --account 9999"
        Then stderr contains an error message
        And the exit code is 1

    # ===================================================================
    # --json flag works per-command (not top-level)
    # ===================================================================

    @FT-RPT-160
    Scenario Outline: --json flag on each new report command produces valid JSON
        Given a CLI-testable ledger environment with seeded data
        When I run the CLI with "report <command-with-args>"
        Then stdout is valid JSON
        And the exit code is 0

        Examples:
            | command-with-args                                    |
            | trial-balance --period 7 --json                     |
            | balance-sheet --as-of 2026-03-31 --json             |
            | income-statement --period 7 --json                  |
            | pnl-subtree --account 5000 --period 7 --json        |
            | account-balance --account 1110 --as-of 2026-03-31 --json |
