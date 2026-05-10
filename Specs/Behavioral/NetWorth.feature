Feature: Net Worth Report
    Produce a net worth report as of a given date. The report sums all assets
    (securities, real estate, cash, frozen assets) and liabilities to compute
    total net worth. The report is hierarchical, rolling up securities by tax 
    bucket and displaying individual fund positions and ledger account balances.

    # ===================================================================
    # Behavioral Specs (Service Level)
    # ===================================================================

    @FT-NW-001
    Scenario: Net worth is the difference between total assets and total liabilities
        Given the net worth reporting schema exists
        And a net-worth-test portfolio account "Investment Account A" in "Tax deferred"
        And a net-worth-test fund "FUND1" of type "Stock"
        And a net-worth-test position for "Investment Account A" in "FUND1" dated 2026-05-01 with value 100000.00
        And a net-worth-test ledger account "Cash Account B" of subtype "Cash" with balance 5000.00
        And a net-worth-test ledger account "Liability C" of type "liability" with balance 40000.00
        When I request the net worth report as of 2026-05-01
        Then the total assets are 105000.00
        And the total liabilities are 40000.00
        And the total net worth is 65000.00

    @FT-NW-002
    Scenario: Real estate is separated from securities based on fund investment type
        Given the net worth reporting schema exists
        And a net-worth-test portfolio account "Brokerage A" in "Tax deferred"
        And a net-worth-test fund "STK1" of type "Stock"
        And a net-worth-test position for "Brokerage A" in "STK1" dated 2026-05-01 with value 10000.00
        And a net-worth-test portfolio account "Property B" in "Real Estate"
        And a net-worth-test fund "RE1" of type "Real estate"
        And a net-worth-test position for "Property B" in "RE1" dated 2026-05-01 with value 500000.00
        When I request the net worth report as of 2026-05-01
        Then the securities total is 10000.00
        And the real estate total is 500000.00
        And "Property B" appears under the "Real Estate" section

    @FT-NW-003
    Scenario: Frozen assets (code 1150) are included in total assets
        Given the net worth reporting schema exists
        And a net-worth-test ledger account "Frozen Asset E" with code "1150" and balance 1200.00
        When I request the net worth report as of 2026-05-01
        Then the frozen assets total is 1200.00
        And the assets section includes a "Frozen" subsection totaling 1200.00

    @FT-NW-004
    Scenario: Securities are grouped and totaled by tax bucket
        Given the net worth reporting schema exists
        And a net-worth-test portfolio account "IRA" in "Tax deferred"
        And a net-worth-test fund "F1" of type "Stock"
        And a net-worth-test position for "IRA" in "F1" with value 50000.00
        And a net-worth-test portfolio account "Roth" in "Tax free Roth"
        And a net-worth-test position for "Roth" in "F1" with value 30000.00
        When I request the net worth report as of today
        Then the "Securities" section contains a "Tax deferred" subsection with 50000.00
        And the "Securities" section contains a "Tax free Roth" subsection with 30000.00
        And each subsection contains its respective fund positions

    @FT-NW-005
    Scenario: Only the latest position per account and symbol is included
        Given the net worth reporting schema exists
        And a net-worth-test portfolio account "Acct A"
        And a net-worth-test fund "F1"
        And a net-worth-test position for "Acct A" in "F1" dated 2026-04-01 with value 100.00
        And a net-worth-test position for "Acct A" in "F1" dated 2026-05-01 with value 150.00
        When I request the net worth report as of 2026-05-10
        Then the report includes "F1" with value 150.00
        And the 100.00 position is ignored

    @FT-NW-006
    Scenario: Voided ledger entries are excluded from cash and liability balances
        Given the net worth reporting schema exists
        And a net-worth-test ledger account "Cash A" of subtype "Cash"
        And a net-worth-test entry for "Cash A" of 1000.00
        And a net-worth-test voided entry for "Cash A" of 500.00
        When I request the net worth report as of today
        Then "Cash A" has a balance of 1000.00

    @FT-NW-007
    Scenario: Future entries are excluded from the report
        Given the net worth reporting schema exists
        And a net-worth-test ledger account "Cash A" of subtype "Cash"
        And a net-worth-test entry for "Cash A" dated 2026-05-01 of 1000.00
        And a net-worth-test entry for "Cash A" dated 2026-06-01 of 500.00
        When I request the net worth report as of 2026-05-31
        Then "Cash A" has a balance of 1000.00

    @FT-NW-008
    Scenario: Report output is a flat list of line items with hierarchical structure
        Given the net worth reporting schema exists
        And a net-worth-test portfolio account "IRA" in "Tax deferred"
        And a net-worth-test fund "VTSAX" of type "Stock" named "Total Stock Market"
        And a net-worth-test position for "IRA" in "VTSAX" dated 2026-05-01 with value 80000.00
        And a net-worth-test portfolio account "Roth" in "Tax free Roth"
        And a net-worth-test fund "VXUS" of type "Stock" named "International Stock"
        And a net-worth-test position for "Roth" in "VXUS" dated 2026-05-01 with value 20000.00
        And a net-worth-test portfolio account "House" in "Real Estate"
        And a net-worth-test fund "RE1" of type "Real estate" named "Primary Residence"
        And a net-worth-test position for "House" in "RE1" dated 2026-05-01 with value 350000.00
        And a net-worth-test ledger account "Checking" of subtype "Cash" with balance 5000.00
        And a net-worth-test ledger account "Escrow" with code "1150" and balance 2000.00
        And a net-worth-test ledger account "Mortgage" of type "liability" with balance 200000.00
        When I request the net worth report as of 2026-05-01
        Then the report lines are in this exact order:
            | level | label                            | amount    |
            | 0     | Total Net Worth                  | 257000.00 |
            | 1     | Assets                           | 457000.00 |
            | 2     | Securities                       | 100000.00 |
            | 3     | Tax deferred                     | 80000.00  |
            | 4     | VTSAX — Total Stock Market       | 80000.00  |
            | 3     | Tax free Roth                    | 20000.00  |
            | 4     | VXUS — International Stock       | 20000.00  |
            | 2     | Real Estate                      | 350000.00 |
            | 3     | House                            | 350000.00 |
            | 2     | Cash                             | 5000.00   |
            | 3     | Checking                         | 5000.00   |
            | 2     | Frozen                           | 2000.00   |
            | 3     | Escrow                           | 2000.00   |
            | 1     | Liabilities                      | 200000.00 |
            | 2     | Mortgage                         | 200000.00 |
        And each line has a sequential ordinal starting at 1
        And fund positions use the format "{symbol} — {fund name}" as label
        And real estate entries use the investment account name as label

    @FT-NW-009
    Scenario: Empty asset categories are omitted from the report
        Given the net worth reporting schema exists
        And a net-worth-test ledger account "Cash Only" of subtype "Cash" with balance 1000.00
        And a net-worth-test ledger account "Credit Card" of type "liability" with balance 500.00
        When I request the net worth report as of 2026-05-01
        Then the report lines are in this exact order:
            | level | label           | amount  |
            | 0     | Total Net Worth | 500.00  |
            | 1     | Assets          | 1000.00 |
            | 2     | Cash            | 1000.00 |
            | 3     | Cash Only       | 1000.00 |
            | 1     | Liabilities     | 500.00  |
            | 2     | Credit Card     | 500.00  |
        And the report does not contain "Securities"
        And the report does not contain "Real Estate"
        And the report does not contain "Frozen"

    # ===================================================================
    # CLI Specs
    # ===================================================================

    @FT-NW-010
    Scenario: Running net-worth report via CLI produces hierarchical table
        When I run the CLI with "report net-worth --as-of 2026-05-09"
        Then stdout contains "Net Worth as of 2026-05-09"
        And stdout contains "Total Net Worth" at level 0
        And stdout contains "Assets" at level 1
        And stdout contains "Securities" at level 2
        And stdout contains "Liabilities" at level 1
        And the exit code is 0

    @FT-NW-011
    Scenario: Running net-worth report with --json flag produces full data model
        When I run the CLI with "report net-worth --as-of 2026-05-09 --json"
        Then stdout is a valid JSON object
        And the JSON contains "asOfDate" set to "2026-05-09"
        And the "lines" list contains hierarchical items with ordinal, label, amount, and level
        And the exit code is 0
