Feature: Create and Confirm Transfers
    Move money between asset accounts. Initiation creates a transfer record
    in-flight; confirmation settles it by posting a double-entry journal entry
    via the existing posting engine.

    # --- Initiate: Happy Path ---

    @FT-TRF-001
    Scenario: Initiating a transfer between two active asset accounts succeeds
        Given two active asset accounts "checking" and "savings"
        When I initiate a transfer of 500.00 from "checking" to "savings" on 2026-04-01
        Then the initiate succeeds
        And the transfer status is "initiated"

    # --- Initiate: Validation ---

    @FT-TRF-002
    Scenario: Initiating with from_account equal to to_account returns error
        Given an active asset account "checking"
        When I initiate a transfer of 500.00 from "checking" to "checking" on 2026-04-01
        Then the initiate fails with error containing "same account"

    @FT-TRF-003
    Scenario: Initiating with a non-asset account type returns error
        Given an active asset account "checking"
        And an active non-asset account "rent-expense"
        When I initiate a transfer of 500.00 from "checking" to "rent-expense" on 2026-04-01
        Then the initiate fails with error containing "asset"

    @FT-TRF-004
    Scenario: Initiating with an inactive account returns error
        Given an active asset account "checking"
        And an inactive asset account "closed-savings"
        When I initiate a transfer of 500.00 from "checking" to "closed-savings" on 2026-04-01
        Then the initiate fails with error containing "active"

    @FT-TRF-005
    Scenario: Initiating with amount zero returns error
        Given two active asset accounts "checking" and "savings"
        When I initiate a transfer of 0.00 from "checking" to "savings" on 2026-04-01
        Then the initiate fails with error containing "amount"

    @FT-TRF-006
    Scenario: Initiating with negative amount returns error
        Given two active asset accounts "checking" and "savings"
        When I initiate a transfer of -100.00 from "checking" to "savings" on 2026-04-01
        Then the initiate fails with error containing "amount"

    # --- Confirm: Happy Path ---

    @FT-TRF-007
    Scenario: Confirming an initiated transfer creates correct journal entry and sets status confirmed
        Given two active asset accounts "checking" and "savings"
        And an open fiscal period covering 2026-04-15
        And an initiated transfer of 1000.00 from "checking" to "savings"
        When I confirm the transfer on 2026-04-15
        Then the confirm succeeds
        And the transfer status is "confirmed"
        And a journal entry exists with 2 lines
        And the debit line is for the "savings" account with amount 1000.00
        And the credit line is for the "checking" account with amount 1000.00

    @FT-TRF-008
    Scenario: Confirming sets confirmed_date and journal_entry_id on the transfer
        Given two active asset accounts "checking" and "savings"
        And an open fiscal period covering 2026-04-15
        And an initiated transfer of 750.00 from "checking" to "savings"
        When I confirm the transfer on 2026-04-15
        Then the confirm succeeds
        And the transfer confirmed_date is 2026-04-15
        And the transfer journal_entry_id matches the created journal entry ID

    # --- Confirm: Journal Entry Metadata ---

    @FT-TRF-009
    Scenario: Journal entry has transfer source and reference
        Given two active asset accounts "checking" and "savings"
        And an open fiscal period covering 2026-04-15
        And an initiated transfer of 500.00 from "checking" to "savings"
        When I confirm the transfer on 2026-04-15
        Then the confirm succeeds
        And the journal entry source is "transfer"
        And the journal entry has a reference with type "transfer" and value matching the transfer ID

    @FT-TRF-010
    Scenario: Journal entry entry_date equals confirmed_date
        Given two active asset accounts "checking" and "savings"
        And an open fiscal period covering 2026-04-20
        And an initiated transfer of 500.00 from "checking" to "savings"
        When I confirm the transfer on 2026-04-20
        Then the confirm succeeds
        And the journal entry entry_date is 2026-04-20

    @FT-TRF-011
    Scenario: Journal entry uses transfer description when present
        Given two active asset accounts "checking" and "savings"
        And an open fiscal period covering 2026-04-15
        And an initiated transfer of 500.00 from "checking" to "savings" described as "Savings top-up"
        When I confirm the transfer on 2026-04-15
        Then the confirm succeeds
        And the journal entry description is "Savings top-up"

    @FT-TRF-012
    Scenario: Journal entry auto-generates description when transfer has no description
        Given two active asset accounts "checking" and "savings"
        And an open fiscal period covering 2026-04-15
        And an initiated transfer of 500.00 from "checking" to "savings" with no description
        When I confirm the transfer on 2026-04-15
        Then the confirm succeeds
        And the journal entry description starts with "Transfer"

    # --- Confirm: Validation ---

    @FT-TRF-013
    Scenario: Confirming a transfer not in initiated status returns error
        Given two active asset accounts "checking" and "savings"
        And an open fiscal period covering 2026-04-15
        And a confirmed transfer of 500.00 from "checking" to "savings"
        When I confirm the transfer on 2026-04-15
        Then the confirm fails with error containing "initiated"

    @FT-TRF-014
    Scenario: Confirming when no fiscal period covers confirmed_date returns error
        Given two active asset accounts "checking" and "savings"
        And no fiscal period covering 2026-07-15
        And an initiated transfer of 500.00 from "checking" to "savings"
        When I confirm the transfer on 2026-07-15
        Then the confirm fails with error containing "fiscal period"

    # --- Idempotency Guard (P043) ---

    @FT-TRF-015
    Scenario: Retry after partial failure skips duplicate journal entry
        Given two active asset accounts "checking" and "savings"
        And an open fiscal period covering 2026-04-15
        And an initiated transfer of 1000.00 from "checking" to "savings"
        And a journal entry already exists with reference type "transfer" and value matching the transfer ID
        When I confirm the transfer on 2026-04-15
        Then the confirm succeeds
        And no new journal entry was created
        And the transfer status is "confirmed"
        And the transfer journal_entry_id matches the pre-existing journal entry ID

    @FT-TRF-016
    Scenario: Voided prior journal entry does not trigger the idempotency guard
        Given two active asset accounts "checking" and "savings"
        And an open fiscal period covering 2026-04-15
        And an initiated transfer of 1000.00 from "checking" to "savings"
        And a voided journal entry exists with reference type "transfer" and value matching the transfer ID
        When I confirm the transfer on 2026-04-15
        Then the confirm succeeds
        And a new journal entry was created (not the voided one)
        And the transfer status is "confirmed"

    # --- Confirm: Closed Period ---

    @FT-TRF-017
    Scenario: Failed confirmation against a closed fiscal period leaves transfer in initiated status with no journal entry
        Given two active asset accounts "checking" and "savings"
        And an open fiscal period covering 2026-04-15
        And an initiated transfer of 1000.00 from "checking" to "savings"
        And the fiscal period covering 2026-04-15 is now closed
        When I confirm the transfer on 2026-04-15
        Then the confirm fails with error containing "not open"
        And the transfer status is "initiated"
        And the transfer journal_entry_id is null
        And no journal entry was created for this transfer

    @FT-TRF-018
    Scenario: Confirming a transfer against a closed fiscal period is rejected
        Given two active asset accounts "checking" and "savings"
        And a closed fiscal period covering 2026-04-15
        And an initiated transfer of 500.00 from "checking" to "savings"
        When I confirm the transfer on 2026-04-15
        Then the confirm fails with error containing "not open"
