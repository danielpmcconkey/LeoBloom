Feature: Reversing Entries
    The `ledger reverse` command creates a new journal entry that mirrors
    an existing entry with debits and credits swapped. This is the GAAP-
    correct mechanism for correcting entries in closed periods: the reversal
    posts to the current open period, leaving the original intact.

    The original entry does not need to be in a closed period — reversals
    are valid for any non-voided, not-already-reversed JE.

    Background:
        Given the ledger schema exists for posting
        And a rve-test open fiscal period from 2101-01-01 to 2101-01-31
        And a rve-test active account 1010 of type asset
        And a rve-test active account 4010 of type revenue

    # --- Core Behavior ---

    @FT-RVE-001
    Scenario: Reversing a JE produces a new entry with swapped debits and credits
        Given a rve-test posted entry dated 2101-01-10 described as "January rent" with lines:
            | account | amount  | entry_type |
            | 1010    | 1200.00 | debit      |
            | 4010    | 1200.00 | credit     |
        When I reverse the journal entry
        Then the reversal succeeds and a new JE is returned
        And the new JE has 2 lines
        And the new JE has account 1010 with entry_type "credit" and amount 1200.00
        And the new JE has account 4010 with entry_type "debit" and amount 1200.00

    @FT-RVE-002
    Scenario: Reversal description is auto-generated from the original JE
        Given a rve-test posted entry dated 2101-01-10 described as "January rent" with lines:
            | account | amount  | entry_type |
            | 1010    | 1200.00 | debit      |
            | 4010    | 1200.00 | credit     |
        When I reverse the journal entry
        Then the new JE description is "Reversal of JE {original_id}: January rent"

    @FT-RVE-003
    Scenario: Reversal source is set to "reversal"
        Given a rve-test posted entry dated 2101-01-10 described as "January rent" with source "manual" and lines:
            | account | amount  | entry_type |
            | 1010    | 1200.00 | debit      |
            | 4010    | 1200.00 | credit     |
        When I reverse the journal entry
        Then the new JE has source "reversal"

    @FT-RVE-004
    Scenario: Reversal JE has reference type "reversal" pointing to the original JE id
        Given a rve-test posted entry dated 2101-01-10 described as "January rent" with lines:
            | account | amount  | entry_type |
            | 1010    | 1200.00 | debit      |
            | 4010    | 1200.00 | credit     |
        When I reverse the journal entry
        Then the new JE has 1 reference with type "reversal" and value equal to the original JE id

    @FT-RVE-005
    Scenario: Reversal posts to the fiscal period derived from the entry date
        Given a rve-test posted entry dated 2101-01-10 described as "January rent" with lines:
            | account | amount  | entry_type |
            | 1010    | 1200.00 | debit      |
            | 4010    | 1200.00 | credit     |
        When I reverse the journal entry with entry_date 2101-01-20
        Then the reversal succeeds
        And the new JE has fiscal_period_id equal to the 2101-01 period

    # --- Date Override ---

    @FT-RVE-006
    Scenario: --date override sets the reversal entry_date and routes to the open period for that date
        Given a rve-test open fiscal period from 2101-02-01 to 2101-02-28
        And a rve-test posted entry dated 2101-01-10 described as "January rent" with lines:
            | account | amount  | entry_type |
            | 1010    | 1200.00 | debit      |
            | 4010    | 1200.00 | credit     |
        When I reverse the journal entry with entry_date 2101-02-01
        Then the reversal succeeds
        And the new JE entry_date is 2101-02-01
        And the new JE has fiscal_period_id equal to the 2101-02 period

    @FT-RVE-007
    Scenario: --date override referencing a closed period is rejected
        Given a rve-test closed fiscal period from 2101-03-01 to 2101-03-31
        And a rve-test posted entry dated 2101-01-10 described as "January rent" with lines:
            | account | amount  | entry_type |
            | 1010    | 1200.00 | debit      |
            | 4010    | 1200.00 | credit     |
        When I reverse the journal entry with entry_date 2101-03-15
        Then the reversal fails with a closed-period error

    # --- Idempotency Guard ---

    @FT-RVE-008
    Scenario: Reversing an already-voided JE is rejected
        Given a rve-test posted entry dated 2101-01-10 described as "January rent" with lines:
            | account | amount  | entry_type |
            | 1010    | 500.00  | debit      |
            | 4010    | 500.00  | credit     |
        And the entry has been voided with reason "Manual void"
        When I reverse the journal entry
        Then the reversal fails with error containing "voided"

    @FT-RVE-009
    Scenario: Reversing a JE that already has a reversal is rejected
        Given a rve-test posted entry dated 2101-01-10 described as "January rent" with lines:
            | account | amount  | entry_type |
            | 1010    | 500.00  | debit      |
            | 4010    | 500.00  | credit     |
        And the entry has already been reversed
        When I reverse the journal entry again
        Then the reversal fails with error containing "already reversed"

    # --- Valid on Open-Period JEs ---

    @FT-RVE-010
    Scenario: Reversing a JE from an open period is valid
        Given a rve-test posted entry dated 2101-01-10 described as "Open period entry" with lines:
            | account | amount  | entry_type |
            | 1010    | 300.00  | debit      |
            | 4010    | 300.00  | credit     |
        When I reverse the journal entry
        Then the reversal succeeds and a new JE is returned
