Feature: Data Extract Filtering and Content
    The four extract repository functions -- account-tree, balances,
    positions, and je-lines -- deliver raw data for external report
    generation. These scenarios verify the filtering contracts: void
    exclusion, as-of date cutoff, latest-snapshot selection, zero-value
    exclusion, fiscal-period scoping, and output ordering.

    Balance is always raw debit-minus-credit with no normal-balance
    adjustment. Void filtering uses INNER JOIN + WHERE on voided_at IS NULL,
    not a LEFT JOIN pattern -- voided entries produce no rows, not zero rows.

    # ===================================================================
    # account-tree
    # ===================================================================

    @FT-EXT-100
    Scenario: Account tree returns all accounts with required fields populated
        Given the ledger schema exists for extract queries
        And extract-test accounts exist: asset 1010, revenue 4010, expense 5010
        When I run the account-tree extract
        Then the result contains at least 3 accounts
        And every row has non-null code, name, and account_type
        And every row with a parent_id references a valid id in the result

    @FT-EXT-101
    Scenario: Account tree includes both active and inactive accounts
        Given the ledger schema exists for extract queries
        And extract-test account 1010 of type asset is active
        And extract-test account 1020 of type asset is inactive
        When I run the account-tree extract
        Then the result includes an account with code "1010" and is_active true
        And the result includes an account with code "1020" and is_active false

    @FT-EXT-102
    Scenario: Account tree result is ordered by account code
        Given the ledger schema exists for extract queries
        And extract-test accounts exist: asset 1010, asset 1020, revenue 4010
        When I run the account-tree extract
        Then the account codes in the result are in ascending order

    # ===================================================================
    # balances -- as-of date filtering
    # ===================================================================

    @FT-EXT-110
    Scenario: Balances respects the as-of date cutoff
        Given the ledger schema exists for extract queries
        And an extract-test open fiscal period from 2026-03-01 to 2026-04-30
        And extract-test accounts: asset 1010, revenue 4010
        And an extract-test journal entry dated 2026-03-15 with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        And an extract-test journal entry dated 2026-04-10 with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        When I run the balances extract as of 2026-03-31
        Then account 1010 has a balance of 1000.00
        And the later entry dated 2026-04-10 is not reflected

    @FT-EXT-111
    Scenario: Balances includes entries on the as-of date itself
        Given the ledger schema exists for extract queries
        And an extract-test open fiscal period from 2026-03-01 to 2026-03-31
        And extract-test accounts: asset 1010, revenue 4010
        And an extract-test journal entry dated 2026-03-31 with lines:
            | account | amount  | entry_type |
            | 1010    | 750.00  | debit      |
            | 4010    | 750.00  | credit     |
        When I run the balances extract as of 2026-03-31
        Then account 1010 has a balance of 750.00

    # ===================================================================
    # balances -- void filtering
    # ===================================================================

    @FT-EXT-112
    Scenario: Balances excludes voided entries
        Given the ledger schema exists for extract queries
        And an extract-test open fiscal period from 2026-03-01 to 2026-03-31
        And extract-test accounts: asset 1010, revenue 4010
        And an extract-test journal entry dated 2026-03-10 described as "Good entry" with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        And an extract-test journal entry dated 2026-03-15 described as "Voided entry" with lines:
            | account | amount | entry_type |
            | 1010    | 200.00 | debit      |
            | 4010    | 200.00 | credit     |
        And the extract-test entry "Voided entry" has been voided
        When I run the balances extract as of 2026-03-31
        Then account 1010 has a balance of 500.00

    @FT-EXT-113
    Scenario: A fully-voided account does not appear in balances output
        Given the ledger schema exists for extract queries
        And an extract-test open fiscal period from 2026-03-01 to 2026-03-31
        And extract-test accounts: asset 1010, revenue 4010
        And an extract-test journal entry dated 2026-03-10 described as "Only entry" with lines:
            | account | amount | entry_type |
            | 1010    | 300.00 | debit      |
            | 4010    | 300.00 | credit     |
        And the extract-test entry "Only entry" has been voided
        When I run the balances extract as of 2026-03-31
        Then account 1010 does not appear in the results
        And account 4010 does not appear in the results

    # ===================================================================
    # balances -- raw debit-minus-credit, no normal-balance adjustment
    # ===================================================================

    @FT-EXT-114
    Scenario: Balance is raw debit-minus-credit regardless of normal balance side
        Given the ledger schema exists for extract queries
        And an extract-test open fiscal period from 2026-03-01 to 2026-03-31
        And extract-test accounts: asset 1010, revenue 4010
        And an extract-test journal entry dated 2026-03-15 with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        When I run the balances extract as of 2026-03-31
        Then account 1010 balance is exactly +1000.00
        And account 4010 balance is exactly -1000.00

    # ===================================================================
    # balances -- zero-balance accounts omitted
    # ===================================================================

    @FT-EXT-115
    Scenario: Account with net-zero balance is omitted from balances output
        Given the ledger schema exists for extract queries
        And an extract-test open fiscal period from 2026-03-01 to 2026-03-31
        And extract-test accounts: asset 1010, revenue 4010, expense 5010
        And an extract-test journal entry dated 2026-03-10 described as "Income" with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        And an extract-test journal entry dated 2026-03-20 described as "Offset" with lines:
            | account | amount  | entry_type |
            | 4010    | 1000.00 | debit      |
            | 1010    | 1000.00 | credit     |
        When I run the balances extract as of 2026-03-31
        Then account 1010 does not appear in the results
        And account 4010 does not appear in the results

    # ===================================================================
    # positions -- latest snapshot per (account, symbol)
    # ===================================================================

    @FT-EXT-120
    Scenario: Positions returns the latest snapshot per account-symbol pair as of the given date
        Given the portfolio schema exists for extract queries
        And a portfolio investment account "Vanguard IRA" with tax bucket "Roth" exists
        And a portfolio fund "VTI" exists
        And a position for "Vanguard IRA" / "VTI" dated 2026-02-28 with current_value 23000.00 exists
        And a position for "Vanguard IRA" / "VTI" dated 2026-03-31 with current_value 24500.00 exists
        When I run the positions extract as of 2026-04-11
        Then the result contains exactly 1 row for account "Vanguard IRA" and symbol "VTI"
        And that row has current_value 24500.00

    @FT-EXT-121
    Scenario: Positions as-of date excludes snapshots taken after the cutoff
        Given the portfolio schema exists for extract queries
        And a portfolio investment account "Vanguard IRA" with tax bucket "Roth" exists
        And a portfolio fund "VTI" exists
        And a position for "Vanguard IRA" / "VTI" dated 2026-01-31 with current_value 22000.00 exists
        And a position for "Vanguard IRA" / "VTI" dated 2026-03-31 with current_value 24500.00 exists
        When I run the positions extract as of 2026-02-28
        Then the result contains exactly 1 row for account "Vanguard IRA" and symbol "VTI"
        And that row has current_value 22000.00

    @FT-EXT-122
    Scenario: Positions returns one row per distinct account-symbol pair across multiple accounts
        Given the portfolio schema exists for extract queries
        And a portfolio investment account "Vanguard IRA" with tax bucket "Roth" exists
        And a portfolio investment account "Schwab Taxable" with tax bucket "Taxable" exists
        And a portfolio fund "VTI" exists
        And a position for "Vanguard IRA" / "VTI" dated 2026-03-31 with current_value 24500.00 exists
        And a position for "Schwab Taxable" / "VTI" dated 2026-03-31 with current_value 10000.00 exists
        When I run the positions extract as of 2026-04-11
        Then the result contains exactly 2 rows
        And each row is for a distinct account-symbol pair

    # ===================================================================
    # positions -- zero-value exclusion
    # ===================================================================

    @FT-EXT-123
    Scenario: Position with current_value of zero is excluded from results
        Given the portfolio schema exists for extract queries
        And a portfolio investment account "Vanguard IRA" with tax bucket "Roth" exists
        And a portfolio fund "VTI" exists
        And a position for "Vanguard IRA" / "VTI" dated 2026-03-31 with current_value 0.00 exists
        When I run the positions extract as of 2026-04-11
        Then the result contains 0 rows for account "Vanguard IRA" and symbol "VTI"

    @FT-EXT-124
    Scenario: Latest-snapshot zero-value excludes position even when earlier snapshot had value
        Given the portfolio schema exists for extract queries
        And a portfolio investment account "Vanguard IRA" with tax bucket "Roth" exists
        And a portfolio fund "VTI" exists
        And a position for "Vanguard IRA" / "VTI" dated 2026-02-28 with current_value 24500.00 exists
        And a position for "Vanguard IRA" / "VTI" dated 2026-03-31 with current_value 0.00 exists
        When I run the positions extract as of 2026-04-11
        Then the result contains 0 rows for account "Vanguard IRA" and symbol "VTI"

    # ===================================================================
    # je-lines -- fiscal period scoping
    # ===================================================================

    @FT-EXT-130
    Scenario: JE lines returns only lines belonging to the requested fiscal period
        Given the ledger schema exists for extract queries
        And an extract-test open fiscal period 11 from 2026-03-01 to 2026-03-31
        And an extract-test open fiscal period 12 from 2026-04-01 to 2026-04-30
        And extract-test accounts: asset 1010, revenue 4010
        And an extract-test journal entry in period 11 dated 2026-03-15 with lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        And an extract-test journal entry in period 12 dated 2026-04-05 with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        When I run the je-lines extract for fiscal period 11
        Then the result contains exactly 2 lines
        And all lines belong to the journal entry dated 2026-03-15

    @FT-EXT-131
    Scenario: JE lines for a period with no entries returns an empty result
        Given the ledger schema exists for extract queries
        And an extract-test open fiscal period 11 from 2026-03-01 to 2026-03-31 with no entries
        When I run the je-lines extract for fiscal period 11
        Then the result is an empty list

    # ===================================================================
    # je-lines -- void filtering
    # ===================================================================

    @FT-EXT-132
    Scenario: JE lines excludes voided journal entries
        Given the ledger schema exists for extract queries
        And an extract-test open fiscal period 11 from 2026-03-01 to 2026-03-31
        And extract-test accounts: asset 1010, revenue 4010
        And an extract-test journal entry in period 11 dated 2026-03-10 described as "Good entry" with lines:
            | account | amount | entry_type |
            | 1010    | 500.00 | debit      |
            | 4010    | 500.00 | credit     |
        And an extract-test journal entry in period 11 dated 2026-03-15 described as "Voided entry" with lines:
            | account | amount | entry_type |
            | 1010    | 200.00 | debit      |
            | 4010    | 200.00 | credit     |
        And the extract-test entry "Voided entry" has been voided
        When I run the je-lines extract for fiscal period 11
        Then the result contains exactly 2 lines
        And all lines belong to the journal entry described as "Good entry"

    # ===================================================================
    # je-lines -- output ordering
    # ===================================================================

    @FT-EXT-133
    Scenario: JE lines are ordered by account_code ASC then entry_date ASC then journal_entry_id ASC
        Given the ledger schema exists for extract queries
        And an extract-test open fiscal period 11 from 2026-03-01 to 2026-03-31
        And extract-test accounts: asset 1010, revenue 4010, expense 5010
        And extract-test journal entries in period 11 with mixed accounts and dates
        When I run the je-lines extract for fiscal period 11
        Then the lines are sorted by account_code ascending
        And within the same account_code, lines are sorted by entry_date ascending
        And within the same account_code and entry_date, lines are sorted by journal_entry_id ascending
