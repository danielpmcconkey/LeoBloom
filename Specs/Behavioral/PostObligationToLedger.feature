Feature: Post Obligation to Ledger
    Bridge the ops and ledger domains: when a confirmed obligation instance
    is posted, create a double-entry journal entry and transition the instance
    to posted. This is cross-domain orchestration where ops data drives
    ledger writes.

    # --- Happy Path: Receivable ---

    @FT-POL-001
    Scenario: Posting a confirmed receivable instance creates correct journal entry and transitions to posted
        Given a receivable obligation agreement with source and destination accounts
        And an open fiscal period covering 2026-04-15
        And a confirmed instance with amount 1200.00 and confirmedDate 2026-04-15
        When I post the instance to the ledger
        Then the post succeeds
        And a journal entry exists with 2 lines
        And the debit line is for the destination account with amount 1200.00
        And the credit line is for the source account with amount 1200.00
        And the instance status is "posted"

    # --- Happy Path: Payable ---

    @FT-POL-002
    Scenario: Posting a confirmed payable instance creates correct journal entry and transitions to posted
        Given a payable obligation agreement with source and destination accounts
        And an open fiscal period covering 2026-04-15
        And a confirmed instance with amount 850.00 and confirmedDate 2026-04-15
        When I post the instance to the ledger
        Then the post succeeds
        And a journal entry exists with 2 lines
        And the debit line is for the destination account with amount 850.00
        And the credit line is for the source account with amount 850.00
        And the instance status is "posted"

    # --- Journal Entry Metadata ---

    @FT-POL-003
    Scenario: Journal entry description matches agreement and instance names
        Given a receivable obligation agreement named "Brian Rent" with source and destination accounts
        And an open fiscal period covering 2026-04-15
        And a confirmed instance named "April 2026" with amount 1000.00 and confirmedDate 2026-04-15
        When I post the instance to the ledger
        Then the post succeeds
        And the journal entry description is "Brian Rent — April 2026"

    @FT-POL-004
    Scenario: Journal entry has obligation source and reference
        Given a receivable obligation agreement with source and destination accounts
        And an open fiscal period covering 2026-04-15
        And a confirmed instance with amount 1000.00 and confirmedDate 2026-04-15
        When I post the instance to the ledger
        Then the post succeeds
        And the journal entry source is "obligation"
        And the journal entry has a reference with type "obligation" and value matching the instance ID

    @FT-POL-005
    Scenario: Journal entry entry_date equals the instance confirmed_date
        Given a receivable obligation agreement with source and destination accounts
        And an open fiscal period covering 2026-04-20
        And a confirmed instance with amount 500.00 and confirmedDate 2026-04-20
        When I post the instance to the ledger
        Then the post succeeds
        And the journal entry entry_date is 2026-04-20

    @FT-POL-006
    Scenario: Instance journal_entry_id is set after posting
        Given a receivable obligation agreement with source and destination accounts
        And an open fiscal period covering 2026-04-15
        And a confirmed instance with amount 1000.00 and confirmedDate 2026-04-15
        When I post the instance to the ledger
        Then the post succeeds
        And the instance journal_entry_id matches the created journal entry ID

    # --- Validation: Instance State ---

    @FT-POL-007
    Scenario: Posting an instance not in confirmed status returns error
        Given a receivable obligation agreement with source and destination accounts
        And an open fiscal period covering 2026-04-15
        And an instance in status "expected"
        When I post the instance to the ledger
        Then the post fails with error containing "confirmed"

    @FT-POL-008
    Scenario: Posting an instance with no amount returns error
        Given a receivable obligation agreement with source and destination accounts
        And an open fiscal period covering 2026-04-15
        And a confirmed instance with no amount and confirmedDate 2026-04-15
        When I post the instance to the ledger
        Then the post fails with error containing "amount"

    @FT-POL-009
    Scenario: Posting an instance with no confirmed_date returns error
        Given a receivable obligation agreement with source and destination accounts
        And an open fiscal period covering 2026-04-15
        And a confirmed instance with amount 1000.00 and no confirmedDate
        When I post the instance to the ledger
        Then the post fails with error containing "confirmed_date"

    # --- Validation: Agreement Accounts ---

    @FT-POL-010
    Scenario: Posting when agreement has no source_account_id returns error
        Given a receivable obligation agreement with no source account and a destination account
        And an open fiscal period covering 2026-04-15
        And a confirmed instance with amount 1000.00 and confirmedDate 2026-04-15
        When I post the instance to the ledger
        Then the post fails with error containing "source_account"

    @FT-POL-011
    Scenario: Posting when agreement has no dest_account_id returns error
        Given a receivable obligation agreement with a source account and no destination account
        And an open fiscal period covering 2026-04-15
        And a confirmed instance with amount 1000.00 and confirmedDate 2026-04-15
        When I post the instance to the ledger
        Then the post fails with error containing "dest_account"

    # --- Validation: Fiscal Period ---

    @FT-POL-012
    Scenario: Posting when no fiscal period covers confirmed_date returns error
        Given a receivable obligation agreement with source and destination accounts
        And no fiscal period covering 2026-07-15
        And a confirmed instance with amount 1000.00 and confirmedDate 2026-07-15
        When I post the instance to the ledger
        Then the post fails with error containing "fiscal period"

    @FT-POL-013
    Scenario: Posting when fiscal period is closed returns error
        Given a receivable obligation agreement with source and destination accounts
        And a closed fiscal period covering 2026-04-15
        And a confirmed instance with amount 1000.00 and confirmedDate 2026-04-15
        When I post the instance to the ledger
        Then the post fails with error containing "not open"

    # --- FiscalPeriodRepository.findByDate ---

    @FT-POL-014
    Scenario: FiscalPeriodRepository.findByDate returns correct period for a date within range
        Given a fiscal period from 2026-05-01 to 2026-05-31
        When I call findByDate with 2026-05-15
        Then a fiscal period is returned matching the created period

    @FT-POL-015
    Scenario: FiscalPeriodRepository.findByDate returns None for a date outside all periods
        Given a fiscal period from 2026-05-01 to 2026-05-31
        When I call findByDate with 2026-08-15
        Then no fiscal period is returned
