Feature: Report Period Disclosure & As-Originally-Closed Mode
    Period-based reports (income statement, balance sheet with --period,
    P&L subtree, trial balance) emit a period provenance header showing
    open/closed status, close metadata, and a summary of any post-close
    adjustment JEs tagged for the period. A footer lists adjustment detail
    when adjustments exist.

    The --as-originally-closed flag filters JEs to those created at or
    before the period's closed_at timestamp, producing a snapshot of the
    period as it stood when originally closed. The je-lines extract gains
    --exclude-adjustments to opt out of the default behavior of including
    adjustment JEs.

    Disclosure is None when no period context is supplied (e.g. a balance
    sheet queried by date only).

    Background:
        Given the ledger schema exists for period disclosure

    # =======================================================================
    # Period Provenance Header — Closed Period
    # =======================================================================

    @FT-RPD-001
    Scenario Outline: Closed-period report includes period name, date range, close metadata, and reopened count
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test fiscal period "2098-03" from 2098-03-01 to 2098-03-31 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2098-03" dated 2098-03-15 described as "March income" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        When I request the <report> for period "2098-03"
        Then the report header shows period name "March 2098" and date range "2098-03-01 → 2098-03-31"
        And the report header shows status CLOSED with the close timestamp and actor "alice"
        And the report header shows reopened count 0

        Examples:
            | report                 |
            | income statement       |
            | trial balance          |
            | P&L subtree            |
            | balance sheet          |

    @FT-RPD-002
    Scenario: Closed-period header includes report generation timestamp
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test fiscal period "2098-04" from 2098-04-01 to 2098-04-30 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2098-04" dated 2098-04-10 described as "April income" with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        When I request the income statement for period "2098-04"
        Then the report header includes a report generation timestamp

    # =======================================================================
    # Period Provenance Header — Open Period
    # =======================================================================

    @FT-RPD-003
    Scenario Outline: Open-period report header shows OPEN status and omits close metadata
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test open fiscal period "2098-05" from 2098-05-01 to 2098-05-31
        And an rpd-test journal entry in period "2098-05" dated 2098-05-10 described as "May income" with lines:
            | account | amount | entry_type |
            | 1010    | 400.00 | debit      |
            | 4010    | 400.00 | credit     |
        When I request the <report> for period "2098-05"
        Then the report header shows status OPEN
        And the report header does not show a close timestamp or actor

        Examples:
            | report           |
            | income statement |
            | trial balance    |
            | P&L subtree      |
            | balance sheet    |

    # =======================================================================
    # Period Provenance Header — Adjustment Summary
    # =======================================================================

    @FT-RPD-004
    Scenario: Closed-period header shows adjustment count and net impact when post-close adjustments exist
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test open fiscal period "2098-06" from 2098-06-01 to 2098-06-30
        And an rpd-test fiscal period "2098-05" from 2098-05-01 to 2098-05-31 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2098-05" dated 2098-05-15 described as "May income" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        And an rpd-test adjustment JE in period "2098-06" dated 2098-06-10 for period "2098-05" described as "May catch-up" with lines:
            | account | amount | entry_type |
            | 5010    | 47.82  | debit      |
            | 1010    | 47.82  | credit     |
        And an rpd-test adjustment JE in period "2098-06" dated 2098-06-12 for period "2098-05" described as "Late vendor credit" with lines:
            | account | amount | entry_type |
            | 1010    |  4.35  | debit      |
            | 4010    |  4.35  | credit     |
        When I request the income statement for period "2098-05"
        Then the report header shows 2 post-close adjustments
        And the report header shows a non-zero net adjustment impact

    @FT-RPD-005
    Scenario: Closed-period header shows no adjustment summary when no adjustments exist
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test fiscal period "2098-07" from 2098-07-01 to 2098-07-31 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2098-07" dated 2098-07-15 described as "July income" with lines:
            | account | amount | entry_type |
            | 1010    | 800.00 | debit      |
            | 4010    | 800.00 | credit     |
        When I request the income statement for period "2098-07"
        Then the report header does not show an adjustment count or net impact

    # =======================================================================
    # Adjustment Footer — Detail List
    # =======================================================================

    @FT-RPD-006
    Scenario: Report footer lists each post-close adjustment JE with ID, date, description, and net amount
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 5010 of type expense
        And an rpd-test active account 1010 of type asset
        And an rpd-test open fiscal period "2098-08" from 2098-08-01 to 2098-08-31
        And an rpd-test fiscal period "2098-07" from 2098-07-01 to 2098-07-31 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2098-07" dated 2098-07-15 described as "July income" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        And an rpd-test adjustment JE in period "2098-08" dated 2098-08-05 for period "2098-07" described as "July utility" with lines:
            | account | amount | entry_type |
            | 5010    | 47.82  | debit      |
            | 1010    | 47.82  | credit     |
        When I request the income statement for period "2098-07"
        Then the report footer lists the adjustment JE with its date, description, and net amount
        And the footer shows a net adjustment total

    @FT-RPD-007
    Scenario: Report with no post-close adjustments has no adjustment footer
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test fiscal period "2098-09" from 2098-09-01 to 2098-09-30 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2098-09" dated 2098-09-10 described as "September income" with lines:
            | account | amount | entry_type |
            | 1010    | 600.00 | debit      |
            | 4010    | 600.00 | credit     |
        When I request the income statement for period "2098-09"
        Then the report has no adjustment footer

    # =======================================================================
    # Balance Sheet — Period Disclosure Is Optional
    # =======================================================================

    @FT-RPD-008
    Scenario: Balance sheet without --period shows no period provenance header
        Given an rpd-test active account 1010 of type asset
        And an rpd-test active account 4010 of type revenue
        And an rpd-test open fiscal period "2098-10" from 2098-10-01 to 2098-10-31
        And an rpd-test journal entry in period "2098-10" dated 2098-10-15 described as "October income" with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        When I request the balance sheet as of 2098-10-31
        Then the report has no period provenance header

    @FT-RPD-009
    Scenario: Balance sheet with --period shows the period provenance header
        Given an rpd-test active account 1010 of type asset
        And an rpd-test active account 4010 of type revenue
        And an rpd-test fiscal period "2098-11" from 2098-11-01 to 2098-11-30 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2098-11" dated 2098-11-10 described as "November income" with lines:
            | account | amount | entry_type |
            | 1010    | 700.00 | debit      |
            | 4010    | 700.00 | credit     |
        When I request the balance sheet as of 2098-11-30 with --period "2098-11"
        Then the report header shows period name "November 2098" and date range "2098-11-01 → 2098-11-30"
        And the report header shows status CLOSED with the close timestamp and actor "alice"

    # =======================================================================
    # Reopened Period — Header Reflects Latest Close
    # =======================================================================

    @FT-RPD-010
    Scenario: Header for a period that has been reopened and re-closed shows updated closed_at and reopened count
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test fiscal period "2098-12" from 2098-12-01 to 2098-12-31 closed by "alice" with 1 reopen
        And an rpd-test journal entry in period "2098-12" dated 2098-12-15 described as "December income" with lines:
            | account | amount  | entry_type |
            | 1010    | 1500.00 | debit      |
            | 4010    | 1500.00 | credit     |
        When I request the income statement for period "2098-12"
        Then the report header shows reopened count 1
        And the report header shows status CLOSED with the most recent close timestamp

    # =======================================================================
    # --as-originally-closed — Happy Path
    # =======================================================================

    @FT-RPD-011
    Scenario Outline: --as-originally-closed excludes JEs created after the period was closed
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test open fiscal period "2098-02" from 2098-02-01 to 2098-02-28
        And an rpd-test fiscal period "2099-01" from 2099-01-01 to 2099-01-31 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2099-01" dated 2099-01-10 described as "Jan income — pre-close" created before the period was closed with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        And an rpd-test journal entry in period "2099-01" dated 2099-01-20 described as "Jan income — post-close" created after the period was closed with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        When I request the <report> for period "2099-01" with --as-originally-closed
        Then the report reflects only the pre-close JE
        And the report does not reflect the post-close JE

        Examples:
            | report           |
            | income statement |
            | trial balance    |
            | P&L subtree      |
            | balance sheet    |

    @FT-RPD-012
    Scenario: --as-originally-closed includes adjustment JEs created before the period was closed
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 5010 of type expense
        And an rpd-test active account 1010 of type asset
        And an rpd-test open fiscal period "2098-02" from 2098-02-01 to 2098-02-28
        And an rpd-test fiscal period "2099-02" from 2099-02-01 to 2099-02-28 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2099-02" dated 2099-02-10 described as "Feb income" created before the period was closed with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        And an rpd-test adjustment JE in period "2098-02" for period "2099-02" described as "Pre-close adjustment" created before the period "2099-02" was closed with lines:
            | account | amount | entry_type |
            | 5010    | 200.00 | debit      |
            | 1010    | 200.00 | credit     |
        And an rpd-test adjustment JE in period "2098-02" for period "2099-02" described as "Post-close adjustment" created after the period "2099-02" was closed with lines:
            | account | amount | entry_type |
            | 5010    | 100.00 | debit      |
            | 1010    | 100.00 | credit     |
        When I request the income statement for period "2099-02" with --as-originally-closed
        Then the pre-close adjustment is included in the report
        And the post-close adjustment is not included in the report

    @FT-RPD-013
    Scenario: --as-originally-closed header shows an indicator that the view is pre-adjustment
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test fiscal period "2099-03" from 2099-03-01 to 2099-03-31 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2099-03" dated 2099-03-15 described as "March income" created before the period was closed with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        When I request the income statement for period "2099-03" with --as-originally-closed
        Then the report header indicates the view is as of close

    # =======================================================================
    # --as-originally-closed — Error Cases
    # =======================================================================

    @FT-RPD-014
    Scenario Outline: --as-originally-closed on an open period returns a clear error
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test open fiscal period "2099-04" from 2099-04-01 to 2099-04-30
        And an rpd-test journal entry in period "2099-04" dated 2099-04-10 described as "April income" with lines:
            | account | amount | entry_type |
            | 1010    | 300.00 | debit      |
            | 4010    | 300.00 | credit     |
        When I request the <report> for period "2099-04" with --as-originally-closed
        Then the command fails with an error indicating the period is open

        Examples:
            | report           |
            | income statement |
            | trial balance    |
            | P&L subtree      |
            | balance sheet    |

    @FT-RPD-015
    Scenario: --as-originally-closed on balance sheet without --period returns a clear error
        Given an rpd-test active account 1010 of type asset
        And an rpd-test open fiscal period "2099-05" from 2099-05-01 to 2099-05-31
        When I request the balance sheet as of 2099-05-31 with --as-originally-closed but without --period
        Then the command fails with an error indicating --period is required with --as-originally-closed

    # =======================================================================
    # Extract je-lines — Adjustment Inclusion (Default) and Exclusion
    # =======================================================================

    @FT-RPD-016
    Scenario: je-lines extract for a period includes adjustment JEs by default
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 5010 of type expense
        And an rpd-test active account 1010 of type asset
        And an rpd-test open fiscal period "2099-07" from 2099-07-01 to 2099-07-31
        And an rpd-test fiscal period "2099-06" from 2099-06-01 to 2099-06-30 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2099-06" dated 2099-06-15 described as "June income" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        And an rpd-test adjustment JE in period "2099-07" dated 2099-07-05 for period "2099-06" described as "June catch-up expense" with lines:
            | account | amount | entry_type |
            | 5010    | 47.82  | debit      |
            | 1010    | 47.82  | credit     |
        When I run the je-lines extract for period "2099-06"
        Then the extract result includes lines from the adjustment JE "June catch-up expense"

    @FT-RPD-017
    Scenario: je-lines extract with --exclude-adjustments omits adjustment JEs
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 5010 of type expense
        And an rpd-test active account 1010 of type asset
        And an rpd-test open fiscal period "2099-09" from 2099-09-01 to 2099-09-30
        And an rpd-test fiscal period "2099-08" from 2099-08-01 to 2099-08-31 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2099-08" dated 2099-08-10 described as "August income" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        And an rpd-test adjustment JE in period "2099-09" dated 2099-09-03 for period "2099-08" described as "August catch-up" with lines:
            | account | amount | entry_type |
            | 5010    | 50.00  | debit      |
            | 1010    | 50.00  | credit     |
        When I run the je-lines extract for period "2099-08" with --exclude-adjustments
        Then the extract result does not include lines from adjustment JE "August catch-up"
        And the extract result includes only lines from the direct period JE "August income"

    @FT-RPD-018
    Scenario: je-lines extract for a period with no adjustments returns only direct JEs regardless of flag
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test fiscal period "2099-10" from 2099-10-01 to 2099-10-31 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2099-10" dated 2099-10-15 described as "October income" with lines:
            | account | amount | entry_type |
            | 1010    | 600.00 | debit      |
            | 4010    | 600.00 | credit     |
        When I run the je-lines extract for period "2099-10"
        Then the extract result contains exactly 2 lines
        And all lines belong to the journal entry described as "October income"

    # =======================================================================
    # JSON Output — Period Metadata Envelope
    # =======================================================================

    @FT-RPD-019
    Scenario Outline: JSON output for period-based reports includes a disclosure object
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test fiscal period "2099-11" from 2099-11-01 to 2099-11-30 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2099-11" dated 2099-11-10 described as "November income" with lines:
            | account | amount | entry_type |
            | 1010    | 700.00 | debit      |
            | 4010    | 700.00 | credit     |
        When I request the <report> for period "2099-11" with --json
        Then stdout is valid JSON
        And the JSON output contains a "disclosure" object
        And the disclosure object includes "fiscalPeriodId", "isOpen", and "closedAt" fields

        Examples:
            | report           |
            | income statement |
            | trial balance    |
            | P&L subtree      |
            | balance sheet    |

    @FT-RPD-020
    Scenario: JSON output for je-lines extract includes period metadata envelope
        Given an rpd-test active account 4010 of type revenue
        And an rpd-test active account 1010 of type asset
        And an rpd-test fiscal period "2099-12" from 2099-12-01 to 2099-12-31 closed by "alice" with 0 reopens
        And an rpd-test journal entry in period "2099-12" dated 2099-12-10 described as "December income" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        When I run the je-lines extract for period "2099-12" with --json
        Then stdout is valid JSON
        And the JSON output contains a "periodKey" field at the envelope level
        And the JSON output contains a "status" field at the envelope level
        And the JSON output contains a "lines" array at the envelope level

    @FT-RPD-021
    Scenario: Balance sheet JSON without --period has no disclosure field
        Given an rpd-test active account 1010 of type asset
        And an rpd-test open fiscal period "2098-01" from 2098-01-01 to 2098-01-31
        And an rpd-test journal entry in period "2098-01" dated 2098-01-15 described as "January income" with lines:
            | account | amount | entry_type |
            | 1010    | 400.00 | debit      |
            | 4010    | 400.00 | credit     |
        When I request the balance sheet as of 2098-01-31 with --json
        Then stdout is valid JSON
        And the JSON disclosure field is null or absent
