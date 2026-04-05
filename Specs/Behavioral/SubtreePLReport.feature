Feature: P&L by Account Subtree
    Generate a profit & loss report scoped to the descendants of a specified
    root account. Revenue and expense accounts within the subtree are aggregated
    into their respective sections. Accounts outside the subtree are excluded.
    Voided entries are excluded.

    # --- Happy Path ---

    @FT-SPL-001
    Scenario: Subtree with revenue and expense children produces correct P&L
        Given a parent account with a revenue child and an expense child
        And an open fiscal period
        And a revenue entry of 1000.00 and an expense entry of 400.00 in the subtree
        When I generate a subtree P&L for the parent account and period
        Then revenue total is 1000.00
        And expense total is 400.00
        And net income is 600.00

    @FT-SPL-002
    Scenario: Subtree with only revenue descendants shows empty expense section
        Given a parent account with only a revenue child
        And an open fiscal period
        And a revenue entry of 750.00 in the subtree
        When I generate a subtree P&L for the parent account and period
        Then revenue total is 750.00 with 1 line
        And expense section has 0 lines and 0.00 total

    @FT-SPL-003
    Scenario: Subtree with only expense descendants shows empty revenue section
        Given a parent account with only an expense child
        And an open fiscal period
        And an expense entry of 300.00 in the subtree
        When I generate a subtree P&L for the parent account and period
        Then expense total is 300.00 with 1 line
        And revenue section has 0 lines and 0.00 total

    @FT-SPL-004
    Scenario: Root account with no children returns single-account P&L
        Given a standalone revenue account with no children
        And an open fiscal period
        And a revenue entry of 500.00 on that account
        When I generate a subtree P&L for that account and period
        Then revenue has 1 line totaling 500.00
        And net income is 500.00

    @FT-SPL-005
    Scenario: Root account not revenue or expense with no rev/exp descendants returns empty report
        Given a parent asset account with an asset child (no revenue/expense in subtree)
        And an open fiscal period
        When I generate a subtree P&L for the parent account and period
        Then revenue total is 0.00 with 0 lines
        And expense total is 0.00 with 0 lines
        And net income is 0.00

    # --- Exclusions ---

    @FT-SPL-006
    Scenario: Voided entries excluded from subtree P&L
        Given a parent account with a revenue child
        And an open fiscal period
        And a revenue entry of 500.00 and a voided revenue entry of 200.00
        When I generate a subtree P&L for the parent account and period
        Then revenue total is 500.00
        And net income is 500.00

    @FT-SPL-007
    Scenario: Accounts outside the subtree are excluded
        Given a parent account with a revenue child
        And a separate revenue account outside the subtree
        And an open fiscal period
        And 600.00 revenue inside the subtree and 400.00 outside
        When I generate a subtree P&L for the parent account and period
        Then revenue total is 600.00 with 1 line
        And the outside account does not appear in the report

    # --- Hierarchy ---

    @FT-SPL-008
    Scenario: Multi-level hierarchy includes grandchildren in subtree
        Given a root account with a child account and a grandchild expense account
        And an open fiscal period
        And an expense entry of 250.00 on the grandchild account
        When I generate a subtree P&L for the root account and period
        Then expense section has 1 line totaling 250.00
        And the line references the grandchild account

    # --- Lookup Methods ---

    @FT-SPL-009
    Scenario: Lookup by period key returns same result as by period ID
        Given a parent account with a revenue child
        And a fiscal period with a known key
        And a revenue entry in that period
        When I generate subtree P&L by period ID and by period key
        Then both results have the same revenue total, expense total, and net income

    # --- Validation ---

    @FT-SPL-010
    Scenario: Nonexistent account code returns error
        Given an open fiscal period
        When I generate a subtree P&L for a nonexistent account code
        Then the lookup fails with error containing "does not exist"

    @FT-SPL-011
    Scenario: Nonexistent period returns error
        Given a revenue account
        When I generate a subtree P&L with a nonexistent period ID
        Then the lookup fails with error containing "does not exist"

    @FT-SPL-012
    Scenario: Empty period for subtree produces zero net income
        Given a parent account with a revenue child
        And an open fiscal period with no entries
        When I generate a subtree P&L for the parent account and period
        Then revenue total is 0.00 with 0 lines
        And expense total is 0.00 with 0 lines
        And net income is 0.00
