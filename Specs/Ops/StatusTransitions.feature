Feature: Obligation Instance Status Transitions
    Given an obligation instance in a known status, advance it to a valid
    successor status with appropriate validation and field updates. The
    state machine enforces the lifecycle diagram from the DataModelSpec.

    # --- Happy Path: Forward Transitions ---

    @FT-ST-001
    Scenario: Transition from expected to in_flight
        Given an obligation instance in status "expected"
        When I transition it to "in_flight"
        Then the transition succeeds
        And the instance status is "in_flight"

    @FT-ST-002
    Scenario: Transition from expected to confirmed with amount and date
        Given an obligation instance in status "expected" with amount 500.00
        When I transition it to "confirmed" with confirmedDate 2026-04-01
        Then the transition succeeds
        And the instance status is "confirmed"
        And the instance confirmedDate is 2026-04-01
        And the instance amount is 500.00

    @FT-ST-003
    Scenario: Transition from expected to confirmed providing amount in command
        Given an obligation instance in status "expected" with no amount
        When I transition it to "confirmed" with confirmedDate 2026-04-01 and amount 750.00
        Then the transition succeeds
        And the instance status is "confirmed"
        And the instance amount is 750.00

    @FT-ST-004
    Scenario: Transition from in_flight to confirmed
        Given an obligation instance in status "in_flight" with amount 300.00
        When I transition it to "confirmed" with confirmedDate 2026-04-05
        Then the transition succeeds
        And the instance status is "confirmed"
        And the instance confirmedDate is 2026-04-05

    @FT-ST-005
    Scenario: Transition from expected to overdue
        Given an obligation instance in status "expected"
        When I transition it to "overdue"
        Then the transition succeeds
        And the instance status is "overdue"

    @FT-ST-006
    Scenario: Transition from in_flight to overdue
        Given an obligation instance in status "in_flight"
        When I transition it to "overdue"
        Then the transition succeeds
        And the instance status is "overdue"

    @FT-ST-007
    Scenario: Transition from overdue to confirmed
        Given an obligation instance in status "overdue" with amount 500.00
        When I transition it to "confirmed" with confirmedDate 2026-04-10
        Then the transition succeeds
        And the instance status is "confirmed"

    @FT-ST-008
    Scenario: Transition from confirmed to posted with journal entry
        Given an obligation instance in status "confirmed" with amount 500.00
        And a journal entry exists in the ledger
        When I transition it to "posted" with that journal entry ID
        Then the transition succeeds
        And the instance status is "posted"
        And the instance journalEntryId is set

    @FT-ST-009
    Scenario: Transition from expected to skipped with notes
        Given an obligation instance in status "expected"
        When I transition it to "skipped" with notes "Tenant on vacation, waived"
        Then the transition succeeds
        And the instance status is "skipped"
        And the instance notes contain "Tenant on vacation"
        And the instance isActive is false

    # --- Amount Handling ---

    @FT-ST-010
    Scenario: Confirmed transition updates amount when provided even if already set
        Given an obligation instance in status "expected" with amount 500.00
        When I transition it to "confirmed" with confirmedDate 2026-04-01 and amount 525.00
        Then the transition succeeds
        And the instance amount is 525.00

    @FT-ST-011
    Scenario: Confirmed transition fails when no amount on instance or in command
        Given an obligation instance in status "expected" with no amount
        When I transition it to "confirmed" with confirmedDate 2026-04-01
        Then the transition fails with error containing "amount"

    # --- Invalid Transitions ---

    @FT-ST-012
    Scenario: Transition from confirmed to expected is rejected
        Given an obligation instance in status "confirmed"
        When I transition it to "expected"
        Then the transition fails with error containing "invalid transition"

    @FT-ST-013
    Scenario: Transition from posted to confirmed is rejected
        Given an obligation instance in status "posted"
        When I transition it to "confirmed"
        Then the transition fails with error containing "invalid transition"

    @FT-ST-014
    Scenario: Transition from skipped to confirmed is rejected
        Given an obligation instance in status "skipped"
        When I transition it to "confirmed"
        Then the transition fails with error containing "invalid transition"

    @FT-ST-015
    Scenario: Transition from overdue to in_flight is rejected
        Given an obligation instance in status "overdue"
        When I transition it to "in_flight"
        Then the transition fails with error containing "invalid transition"

    # --- Guard Violations ---

    @FT-ST-016
    Scenario: Transition to posted without journal entry ID fails
        Given an obligation instance in status "confirmed"
        When I transition it to "posted" without a journal entry ID
        Then the transition fails with error containing "journal_entry_id"

    @FT-ST-017
    Scenario: Transition to posted with nonexistent journal entry fails
        Given an obligation instance in status "confirmed"
        When I transition it to "posted" with journal entry ID 999999
        Then the transition fails with error containing "does not exist"

    @FT-ST-018
    Scenario: Transition to skipped without notes fails
        Given an obligation instance in status "expected" with no notes
        When I transition it to "skipped" without notes
        Then the transition fails with error containing "notes"

    @FT-ST-019
    Scenario: Transition to confirmed without confirmedDate fails
        Given an obligation instance in status "expected" with amount 500.00
        When I transition it to "confirmed" without a confirmedDate
        Then the transition fails with error containing "confirmed_date"

    # --- Edge Cases ---

    @FT-ST-020
    Scenario: Transition on inactive instance fails
        Given an obligation instance in status "expected" that is inactive
        When I transition it to "in_flight"
        Then the transition fails with error containing "inactive"

    @FT-ST-021
    Scenario: Transition on nonexistent instance fails
        When I transition instance ID 999999 to "in_flight"
        Then the transition fails with error containing "does not exist"

    @FT-ST-022
    Scenario: Transition to skipped succeeds when instance already has notes
        Given an obligation instance in status "expected" with notes "existing note"
        When I transition it to "skipped" without providing new notes
        Then the transition succeeds
        And the instance status is "skipped"
        And the instance notes contain "existing note"

    # --- Field Passthrough (regression coverage for BUG-A and BUG-B) ---

    @FT-ST-024
    Scenario: Transition from expected to in_flight with amount preserves the amount
        Given an obligation instance in status "expected"
        When I transition it to "in_flight" with amount 62.03
        Then the transition succeeds
        And the instance status is "in_flight"
        And the instance amount is 62.03

    @FT-ST-025
    Scenario: Transition with notes preserves the notes on a non-skipped target
        Given an obligation instance in status "expected"
        When I transition it to "in_flight" with notes "autopay confirmed via portal"
        Then the transition succeeds
        And the instance status is "in_flight"
        And the instance notes contain "autopay confirmed via portal"

    @FT-ST-026
    Scenario Outline: Notes are preserved on every valid non-skipped transition
        Given an obligation instance in status "<from>" suitable for transition to "<to>"
        When I transition it to "<to>" with notes "note for <to>" and any required guard fields
        Then the transition succeeds
        And the instance notes contain "note for <to>"

        Examples:
            | from      | to        |
            | expected  | in_flight |
            | expected  | confirmed |
            | expected  | overdue   |
            | in_flight | confirmed |
            | in_flight | overdue   |
            | overdue   | confirmed |
            | confirmed | posted    |

    # --- Complete Invalid Transition Coverage (REM-002) ---

    @FT-ST-023
    Scenario Outline: Invalid transition from <from> to <to> is rejected
        Given an obligation instance in status "<from>"
        When I transition it to "<to>"
        Then the transition fails with error containing "invalid transition"

        Examples:
            | from      | to        |
            | expected  | posted    |
            | in_flight | expected  |
            | in_flight | posted    |
            | in_flight | skipped   |
            | overdue   | expected  |
            | overdue   | posted    |
            | overdue   | skipped   |
            | confirmed | in_flight |
            | confirmed | overdue   |
            | confirmed | skipped   |
            | posted    | expected  |
            | posted    | in_flight |
            | posted    | overdue   |
            | posted    | skipped   |
            | skipped   | expected  |
            | skipped   | in_flight |
            | skipped   | overdue   |
            | skipped   | posted    |
