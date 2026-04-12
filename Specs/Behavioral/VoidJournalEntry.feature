Feature: Void Journal Entry
    Mark an existing journal entry as void. The entry remains in the ledger
    (append-only) but is excluded from balance calculations.

    # --- Happy Path ---

    @FT-VJE-001
    Scenario: Void an active entry successfully
        Given the ledger schema exists for voiding
        And a void-test open fiscal period from 2026-03-01 to 2026-03-31
        And a void-test active account 1010 of type asset
        And a void-test active account 4010 of type revenue
        And a posted entry dated 2026-03-15 described as "March rent" with source "manual" and lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        When I void the entry with reason "Duplicate posting"
        Then the void succeeds with voided_at set and void_reason "Duplicate posting"

    # --- State Verification ---

    @FT-VJE-002
    Scenario: Voided entry remains in the database
        Given the ledger schema exists for voiding
        And a void-test open fiscal period from 2026-03-01 to 2026-03-31
        And a void-test active account 1010 of type asset
        And a void-test active account 4010 of type revenue
        And a posted entry dated 2026-03-15 described as "To be voided" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        When I void the entry with reason "Error correction"
        Then the void succeeds and the entry still exists in the database with description "To be voided"

    @FT-VJE-003
    Scenario: Lines and references are intact after void
        Given the ledger schema exists for voiding
        And a void-test open fiscal period from 2026-03-01 to 2026-03-31
        And a void-test active account 1010 of type asset
        And a void-test active account 4010 of type revenue
        And a posted entry with refs dated 2026-03-15 described as "Entry with refs" with source "manual" and refs:
            | reference_type | reference_value |
            | cheque         | 5678            |
        And void-test lines:
            | account | amount | entry_type |
            | 1010    | 750.00 | debit      |
            | 4010    | 750.00 | credit     |
        When I void the entry with reason "Wrong amount"
        Then the void succeeds and the entry has 2 lines and 1 reference with type "cheque" and value "5678"

    # --- Idempotency ---

    @FT-VJE-004
    Scenario: Void an already-voided entry is idempotent
        Given the ledger schema exists for voiding
        And a void-test open fiscal period from 2026-03-01 to 2026-03-31
        And a void-test active account 1010 of type asset
        And a void-test active account 4010 of type revenue
        And a posted entry dated 2026-03-15 described as "Double void test" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 200.00 | debit      |
            | 4010    | 200.00 | credit     |
        And the entry has been voided with reason "First void"
        When I void the entry with reason "Second void attempt"
        Then the void succeeds with the original voided_at and void_reason "First void" unchanged

    # --- Validation ---

    @FT-VJE-005
    Scenario: Empty void reason is rejected
        Given the ledger schema exists for voiding
        And a void-test open fiscal period from 2026-03-01 to 2026-03-31
        And a void-test active account 1010 of type asset
        And a void-test active account 4010 of type revenue
        And a posted entry dated 2026-03-15 described as "Reason test" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 100.00 | debit      |
            | 4010    | 100.00 | credit     |
        When I void the entry with reason ""
        Then the void fails with error containing "reason"

    @FT-VJE-006
    Scenario: Whitespace-only void reason is rejected
        Given the ledger schema exists for voiding
        And a void-test open fiscal period from 2026-03-01 to 2026-03-31
        And a void-test active account 1010 of type asset
        And a void-test active account 4010 of type revenue
        And a posted entry dated 2026-03-15 described as "Whitespace test" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 100.00 | debit      |
            | 4010    | 100.00 | credit     |
        When I void the entry with reason "   "
        Then the void fails with error containing "reason"

    @FT-VJE-007
    Scenario: Nonexistent entry ID is rejected
        Given the ledger schema exists for voiding
        When I void entry ID 999999 with reason "Does not exist"
        Then the void fails with error containing "does not exist"

    # FT-VJE-008 removed by P083: voiding in a closed period is now blocked.
    # See FT-CPE-001 in Specs/Behavioral/ClosedPeriodEnforcement.feature.
