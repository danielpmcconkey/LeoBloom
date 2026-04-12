Feature: Post-Close Adjustments and Fiscal Period Override
    Two related enhancements to `ledger post`:

    1. `--adjustment-for-period N` tags a new JE as an adjustment for a
       specific prior period. The JE itself posts normally to its own open
       fiscal period. The tag enables P084 report disclosure of post-close
       corrections.

    2. `--fiscal-period-id N` (now optional) overrides the date-derived
       period assignment. When omitted, the period is derived from
       entry_date as before.

    Background:
        Given the ledger schema exists for posting

    # --- Post-Close Adjustment Tagging ---

    @FT-PCA-001
    Scenario: --adjustment-for-period tags the JE with adjustment_for_period_id
        Given a pca-test open fiscal period from 2102-01-01 to 2102-01-31
        And a pca-test closed fiscal period from 2101-12-01 to 2101-12-31
        And a pca-test active account 1010 of type asset
        And a pca-test active account 4010 of type revenue
        When I post a journal entry dated 2102-01-15 described as "Dec correction" with source "manual"
            and --adjustment-for-period set to the December 2101 period
            and lines:
            | account | amount  | entry_type |
            | 1010    | 500.00  | debit      |
            | 4010    | 500.00  | credit     |
        Then the post succeeds
        And the new JE has adjustment_for_period_id equal to the December 2101 period

    @FT-PCA-002
    Scenario: Adjustment target period can be closed
        Given a pca-test open fiscal period from 2102-01-01 to 2102-01-31
        And a pca-test closed fiscal period from 2101-12-01 to 2101-12-31
        And a pca-test active account 1010 of type asset
        And a pca-test active account 4010 of type revenue
        When I post a journal entry dated 2102-01-15 described as "Adj for closed period"
            with source "manual"
            and --adjustment-for-period set to the closed December 2101 period
            and lines:
            | account | amount  | entry_type |
            | 1010    | 200.00  | debit      |
            | 4010    | 200.00  | credit     |
        Then the post succeeds
        And the new JE has adjustment_for_period_id set

    @FT-PCA-003
    Scenario: Adjustment target period must exist
        Given a pca-test open fiscal period from 2102-01-01 to 2102-01-31
        And a pca-test active account 1010 of type asset
        And a pca-test active account 4010 of type revenue
        When I post a journal entry dated 2102-01-15 described as "Adj for nonexistent period"
            with source "manual"
            and --adjustment-for-period set to 999999
            and lines:
            | account | amount  | entry_type |
            | 1010    | 100.00  | debit      |
            | 4010    | 100.00  | credit     |
        Then the post fails with error containing "does not exist"

    @FT-PCA-004
    Scenario: JE with adjustment tag posts to its own open fiscal period normally
        Given a pca-test open fiscal period named "January 2102" from 2102-01-01 to 2102-01-31
        And a pca-test closed fiscal period from 2101-12-01 to 2101-12-31
        And a pca-test active account 1010 of type asset
        And a pca-test active account 4010 of type revenue
        When I post a journal entry dated 2102-01-15 described as "Dec correction"
            with source "manual"
            and --adjustment-for-period set to the December 2101 period
            and lines:
            | account | amount  | entry_type |
            | 1010    | 300.00  | debit      |
            | 4010    | 300.00  | credit     |
        Then the post succeeds
        And the new JE has fiscal_period_id equal to the January 2102 period

    @FT-PCA-005
    Scenario: --json output includes adjustmentForPeriodId
        Given a pca-test open fiscal period from 2102-02-01 to 2102-02-28
        And a pca-test closed fiscal period from 2101-12-01 to 2101-12-31
        And a pca-test active account 1010 of type asset
        And a pca-test active account 4010 of type revenue
        When I post a journal entry with --adjustment-for-period and --json flag
        Then stdout is valid JSON
        And the JSON output contains "adjustmentForPeriodId"

    # --- Fiscal Period Override ---

    @FT-PCA-006
    Scenario: --fiscal-period-id override assigns the entry to the specified period
        Given a pca-test open fiscal period from 2102-03-01 to 2102-03-31
        And a pca-test open fiscal period from 2102-04-01 to 2102-04-30
        And a pca-test active account 1010 of type asset
        And a pca-test active account 4010 of type revenue
        When I post a journal entry dated 2102-03-15 described as "Override test"
            with source "manual"
            and --fiscal-period-id set to the April 2102 period
            and lines:
            | account | amount  | entry_type |
            | 1010    | 400.00  | debit      |
            | 4010    | 400.00  | credit     |
        Then the post succeeds
        And the new JE has fiscal_period_id equal to the April 2102 period

    @FT-PCA-007
    Scenario: --fiscal-period-id override to a closed period is rejected
        Given a pca-test open fiscal period from 2102-05-01 to 2102-05-31
        And a pca-test closed fiscal period from 2102-06-01 to 2102-06-30
        And a pca-test active account 1010 of type asset
        And a pca-test active account 4010 of type revenue
        When I post a journal entry dated 2102-05-15 described as "Closed override test"
            with source "manual"
            and --fiscal-period-id set to the closed June 2102 period
            and lines:
            | account | amount  | entry_type |
            | 1010    | 400.00  | debit      |
            | 4010    | 400.00  | credit     |
        Then the post fails with a closed-period error

    @FT-PCA-008
    Scenario: Omitting --fiscal-period-id derives the period from entry_date
        Given a pca-test open fiscal period from 2102-07-01 to 2102-07-31
        And a pca-test active account 1010 of type asset
        And a pca-test active account 4010 of type revenue
        When I post a journal entry dated 2102-07-10 described as "Date-derived period"
            with source "manual"
            and no --fiscal-period-id flag
            and lines:
            | account | amount  | entry_type |
            | 1010    | 600.00  | debit      |
            | 4010    | 600.00  | credit     |
        Then the post succeeds
        And the new JE has fiscal_period_id equal to the July 2102 period
