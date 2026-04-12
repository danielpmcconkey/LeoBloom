Feature: Closed Period Enforcement on Void
    Voiding a journal entry that belongs to a closed fiscal period is now
    blocked. The error message names the period, includes its close date,
    and directs the user to the reversing-entry command. Voiding in an
    open period is unaffected.

    Note: FT-VJE-008 ("Void succeeds in a closed fiscal period") is
    superseded by FT-CPE-001 below and should be removed from
    VoidJournalEntry.feature.

    Background:
        Given the ledger schema exists for voiding

    # --- Enforcement ---

    @FT-CPE-001
    Scenario: Voiding a JE in a closed period is rejected with an actionable error
        Given a cpe-test open fiscal period named "March 2026" from 2100-03-01 to 2100-03-31
        And the fiscal period is now closed
        And a cpe-test active account 1010 of type asset
        And a cpe-test active account 4010 of type revenue
        And a posted entry dated 2100-03-15 described as "March rent" with source "manual" and lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        When I void the entry with reason "Error correction"
        Then the void fails with a closed-period error
        And the error names the period "March 2026"
        And the error includes the period close date
        And the error includes the reversal command "ledger reverse"

    @FT-CPE-002
    Scenario: Voiding a JE in an open period continues to work
        Given a cpe-test open fiscal period named "April 2026" from 2100-04-01 to 2100-04-30
        And a cpe-test active account 1010 of type asset
        And a cpe-test active account 4010 of type revenue
        And a posted entry dated 2100-04-15 described as "April rent" with source "manual" and lines:
            | account | amount  | entry_type |
            | 1010    | 800.00  | debit      |
            | 4010    | 800.00  | credit     |
        When I void the entry with reason "Duplicate posting"
        Then the void succeeds with voided_at set and void_reason "Duplicate posting"
