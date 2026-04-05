Feature: Close / Reopen Fiscal Period
    Toggle a fiscal period between open and closed states via service
    functions. Closing sets is_open = false; reopening sets is_open = true
    and requires a reason string logged via Log.info. The posting engine
    already rejects entries to closed periods.

    # --- Happy Path ---

    @FT-CFP-001
    Scenario: Close an open fiscal period
        Given the ledger schema exists for period management
        And a period-test open fiscal period from 2026-04-01 to 2026-04-30
        When I close the fiscal period
        Then the close succeeds and the period has is_open = false

    @FT-CFP-002
    Scenario: Reopen a closed fiscal period with a reason
        Given the ledger schema exists for period management
        And a period-test open fiscal period from 2026-04-01 to 2026-04-30
        And the fiscal period has been closed
        When I reopen the fiscal period with reason "Quarter-end adjustment needed"
        Then the reopen succeeds and the period has is_open = true

    # --- Idempotency ---

    @FT-CFP-003
    Scenario: Closing an already-closed period is idempotent
        Given the ledger schema exists for period management
        And a period-test open fiscal period from 2026-04-01 to 2026-04-30
        And the fiscal period has been closed
        When I close the fiscal period
        Then the close succeeds and the period has is_open = false

    @FT-CFP-004
    Scenario: Reopening an already-open period is idempotent
        Given the ledger schema exists for period management
        And a period-test open fiscal period from 2026-04-01 to 2026-04-30
        When I reopen the fiscal period with reason "Already open, should still succeed"
        Then the reopen succeeds and the period has is_open = true

    # --- Reason Validation ---

    @FT-CFP-005
    Scenario Outline: Reopen with invalid reason is rejected
        Given the ledger schema exists for period management
        And a period-test open fiscal period from 2026-04-01 to 2026-04-30
        And the fiscal period has been closed
        When I reopen the fiscal period with reason "<reason>"
        Then the reopen fails with error containing "reason"

        Examples:
            | reason |
            |        |
            |        |

    # Note: first example is empty string, second is whitespace-only

    # --- Nonexistent Period ---

    @FT-CFP-006
    Scenario: Close a nonexistent fiscal period is rejected
        Given the ledger schema exists for period management
        When I close fiscal period ID 999999
        Then the close fails with error containing "does not exist"

    @FT-CFP-007
    Scenario: Reopen a nonexistent fiscal period is rejected
        Given the ledger schema exists for period management
        When I reopen fiscal period ID 999999 with reason "Nonexistent period"
        Then the reopen fails with error containing "does not exist"

    # --- Lifecycle ---

    @FT-CFP-008
    Scenario: Full close-reopen-close cycle completes without error
        Given the ledger schema exists for period management
        And a period-test open fiscal period from 2026-04-01 to 2026-04-30
        When I close the fiscal period
        And I reopen the fiscal period with reason "Missed an invoice"
        And I close the fiscal period again
        Then all three operations succeed and the period has is_open = false

    # --- Edge Cases ---

    @FT-CFP-009
    Scenario: Close a period with no journal entries
        Given the ledger schema exists for period management
        And a period-test open fiscal period from 2026-05-01 to 2026-05-31 with no entries
        When I close the fiscal period
        Then the close succeeds and the period has is_open = false

    # --- Integration ---

    @FT-CFP-010
    Scenario: Posting is rejected after closing a period via closePeriod
        Given the ledger schema exists for period management
        And a period-test open fiscal period from 2026-04-01 to 2026-04-30
        And a period-test active account 1010 of type asset
        And a period-test active account 4010 of type revenue
        And the fiscal period has been closed
        When I post a journal entry dated 2026-04-15 described as "Late entry" with source "manual" and lines:
            | account | amount | entry_type |
            | 1010    | 100.00 | debit      |
            | 4010    | 100.00 | credit     |
        Then the post fails with error containing "not open"

    # --- Side Effects (REM-011) ---

    @FT-CFP-012
    Scenario: Closing a period does not modify account balances
        Given the ledger schema exists for period management
        And a period-test open fiscal period from 2026-04-01 to 2026-04-30
        And a period-test active account 1010 of type asset
        And a period-test active account 4010 of type revenue
        And a period-test entry dated 2026-04-15 in the period with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        And I record the trial balance totals for the period
        When I close the fiscal period
        And I record the trial balance totals again
        Then the trial balance totals are identical before and after close

    # --- Logging ---

    @FT-CFP-011
    Scenario: Reopen reason is logged at Info level
        Given the ledger schema exists for period management
        And a period-test open fiscal period from 2026-04-01 to 2026-04-30
        And the fiscal period has been closed
        When I reopen the fiscal period with reason "Correcting prior-period error"
        Then the reopen succeeds and the log output contains "Correcting prior-period error"
