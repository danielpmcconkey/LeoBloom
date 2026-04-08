Feature: Balance Projection
    Compute a projected daily balance series for an account from today through
    a future projection date. The projection formula is:

        projected_balance(account, date) =
            current_balance(account, today)
          + expected_inflows(account, today → date)          # receivable instances
          − expected_outflows(account, today → date)         # payable instances
          − in_flight_transfers_out(account, today → date)
          + in_flight_transfers_in(account, today → date)

    Output is a daily series (the curve), not just the terminal value.
    This is computed, not stored — every call recalculates from current state.

    # ===================================================================
    # Happy Path — Flat Line
    # ===================================================================

    @FT-BP-001
    Scenario: Account with no future obligations or transfers produces a flat line
        Given a projection-test account "checking" with current balance 5000.00
        And no obligation instances exist for "checking"
        And no initiated transfers exist for "checking"
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And every day in the series shows balance 5000.00
        And the series contains entries for 2026-04-07 through 2026-04-10

    # ===================================================================
    # Happy Path — Inflows
    # ===================================================================

    @FT-BP-002
    Scenario: Expected receivable inflow increases projected balance on expected_date
        Given a projection-test account "checking" with current balance 1000.00
        And a receivable obligation instance for "checking" of 500.00 expected on 2026-04-09
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series entry for 2026-04-08 shows balance 1000.00
        And the series entry for 2026-04-09 shows balance 1500.00
        And the series entry for 2026-04-10 shows balance 1500.00

    # ===================================================================
    # Happy Path — Outflows
    # ===================================================================

    @FT-BP-003
    Scenario: Expected payable outflow decreases projected balance on expected_date
        Given a projection-test account "checking" with current balance 2000.00
        And a payable obligation instance for "checking" of 400.00 expected on 2026-04-08
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series entry for 2026-04-07 shows balance 2000.00
        And the series entry for 2026-04-08 shows balance 1600.00
        And the series entry for 2026-04-10 shows balance 1600.00

    # ===================================================================
    # Happy Path — In-Flight Transfers
    # ===================================================================

    @FT-BP-004
    Scenario: Initiated transfer out decreases projected balance on settlement date
        Given a projection-test account "checking" with current balance 3000.00
        And an initiated transfer of 800.00 from "checking" with expected_settlement 2026-04-09
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series entry for 2026-04-08 shows balance 3000.00
        And the series entry for 2026-04-09 shows balance 2200.00

    @FT-BP-005
    Scenario: Initiated transfer in increases projected balance on settlement date
        Given a projection-test account "checking" with current balance 1000.00
        And an initiated transfer of 600.00 into "checking" with expected_settlement 2026-04-08
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series entry for 2026-04-07 shows balance 1000.00
        And the series entry for 2026-04-08 shows balance 1600.00

    @FT-BP-006
    Scenario: In-flight transfer with no expected_settlement falls back to initiated_date
        Given a projection-test account "checking" with current balance 2000.00
        And an initiated transfer of 300.00 from "checking" with no expected_settlement and initiated_date 2026-04-09
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series entry for 2026-04-08 shows balance 2000.00
        And the series entry for 2026-04-09 shows balance 1700.00

    # ===================================================================
    # Happy Path — Combined Formula
    # ===================================================================

    @FT-BP-007
    Scenario: All projection components combine correctly
        Given a projection-test account "checking" with current balance 5000.00
        And a receivable obligation instance for "checking" of 1000.00 expected on 2026-04-09
        And a payable obligation instance for "checking" of 400.00 expected on 2026-04-09
        And an initiated transfer of 200.00 from "checking" with expected_settlement 2026-04-09
        And an initiated transfer of 100.00 into "checking" with expected_settlement 2026-04-09
        When I compute the balance projection for "checking" through 2026-04-09
        Then the projection succeeds
        And the series entry for 2026-04-09 shows balance 5500.00

    # ===================================================================
    # Edge Case — Series Coverage
    # ===================================================================

    @FT-BP-008
    Scenario: Projection series includes every day from today through projection_date
        Given a projection-test account "checking" with current balance 1000.00
        And no obligation instances exist for "checking"
        And no initiated transfers exist for "checking"
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series contains an entry for each of: 2026-04-07, 2026-04-08, 2026-04-09, 2026-04-10

    # ===================================================================
    # Edge Case — Same-Day Obligations
    # ===================================================================

    @FT-BP-009
    Scenario: Multiple obligations on the same day are summed with itemized breakdown
        Given a projection-test account "checking" with current balance 3000.00
        And a receivable obligation instance for "checking" of 500.00 expected on 2026-04-08
        And a payable obligation instance for "checking" of 200.00 expected on 2026-04-08
        And a payable obligation instance for "checking" of 150.00 expected on 2026-04-08
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series entry for 2026-04-08 shows balance 3150.00
        And the series entry for 2026-04-08 includes an itemized breakdown with 3 items
        And the series entry for 2026-04-08 shows a net effect of +150.00

    # ===================================================================
    # Edge Case — Null-Amount Obligations
    # ===================================================================

    @FT-BP-010
    Scenario: Null-amount payable obligation surfaces as unknown outflow — not omitted
        Given a projection-test account "checking" with current balance 2000.00
        And a payable obligation instance for "checking" with no amount expected on 2026-04-09
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series entry for 2026-04-09 contains an unknown outflow marker for 2026-04-09
        And the series entry for 2026-04-09 balance is not reduced by a guessed amount

    @FT-BP-011
    Scenario: Null-amount receivable obligation surfaces as unknown inflow — not omitted
        Given a projection-test account "checking" with current balance 2000.00
        And a receivable obligation instance for "checking" with no amount expected on 2026-04-09
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series entry for 2026-04-09 contains an unknown inflow marker for 2026-04-09
        And the series entry for 2026-04-09 balance is not increased by a guessed amount

    # ===================================================================
    # Validation
    # ===================================================================

    @FT-BP-012
    Scenario: Projection date in the past is rejected
        Given a projection-test account "checking" with current balance 1000.00
        When I compute the balance projection for "checking" through 2026-04-06
        Then the projection fails with error containing "past"

    @FT-BP-013
    Scenario: Projection date equal to today is rejected
        Given a projection-test account "checking" with current balance 1000.00
        When I compute the balance projection for "checking" through 2026-04-07
        Then the projection fails with error containing "past"

    @FT-BP-014
    Scenario: Nonexistent account returns error
        When I compute the balance projection for account code "ZZZZ" through 2026-04-10
        Then the projection fails with error containing "does not exist"

    # ===================================================================
    # Status Filter — Excluded Statuses
    # ===================================================================

    @FT-BP-015
    Scenario: Confirmed obligation instance is excluded from balance projection
        Given a projection-test account "checking" with current balance 5000.00
        And a receivable obligation instance for "checking" of 800.00 expected on 2026-04-09
        And a receivable obligation instance for "checking" of 600.00 in status "confirmed" on 2026-04-09
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series entry for 2026-04-09 shows balance 5800.00

    @FT-BP-016
    Scenario: Posted obligation instance is excluded from balance projection
        Given a projection-test account "checking" with current balance 3000.00
        And a payable obligation instance for "checking" of 400.00 expected on 2026-04-09
        And a payable obligation instance for "checking" of 250.00 in status "posted" on 2026-04-09
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series entry for 2026-04-09 shows balance 2600.00

    @FT-BP-017
    Scenario: Confirmed transfer is excluded from balance projection
        Given a projection-test account "checking" with current balance 4000.00
        And an initiated transfer of 500.00 from "checking" with expected_settlement 2026-04-09
        And a confirmed transfer of 300.00 from "checking" with expected_settlement 2026-04-09
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series entry for 2026-04-09 shows balance 3500.00

    @FT-BP-018
    Scenario: Skipped obligation instance is excluded from balance projection
        Given a projection-test account "checking" with current balance 2000.00
        And a receivable obligation instance for "checking" of 1000.00 expected on 2026-04-09
        And a payable obligation instance for "checking" of 350.00 in status "skipped" on 2026-04-09
        When I compute the balance projection for "checking" through 2026-04-10
        Then the projection succeeds
        And the series entry for 2026-04-09 shows balance 3000.00
