Feature: Pre-Close Validation
    Before a fiscal period can be closed, four GAAP-informed validation checks
    run automatically: trial balance equilibrium, balance sheet equation, data
    hygiene (voided JEs and out-of-range entry dates), and open obligations.
    Close is blocked unless all checks pass or --force is used with a mandatory
    --note. A dry-run validate command lets operators review results without
    closing. Multiple failures are all reported together, not fail-fast.

    Background:
        Given the ledger schema exists for period management

    # --- Happy Path ---

    @FT-PCV-001
    Scenario: Clean period passes all checks and closes
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period has only balanced journal entries with no data hygiene issues
        And there are no in-flight obligation instances in the period
        When I close the fiscal period
        Then the close succeeds and the period has is_open = false

    # --- Trial Balance Equilibrium ---

    @FT-PCV-002
    Scenario: Trial balance disequilibrium blocks close with debit and credit totals
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period has a journal entry with mismatched debit and credit totals
        When I close the fiscal period
        Then the close fails with a validation error
        And the error output identifies the trial balance check as failed
        And the error output shows the debit total and credit total

    # --- Balance Sheet Equation ---

    @FT-PCV-003
    Scenario: Balance sheet equation failure blocks close with asset and equity values
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period's journal entries produce assets that do not equal liabilities plus equity as of 2099-01-31
        When I close the fiscal period
        Then the close fails with a validation error
        And the error output identifies the balance sheet equation check as failed
        And the error output shows the asset total and the liabilities-plus-equity total

    # --- Data Hygiene: Voided JE with null void_reason ---

    @FT-PCV-004
    Scenario: Voided JE with null void_reason blocks close listing offending JE IDs
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period has a voided journal entry with no void reason recorded
        When I close the fiscal period
        Then the close fails with a validation error
        And the error output identifies the data hygiene check as failed
        And the error output lists the IDs of the offending voided journal entries

    # --- Data Hygiene: entry_date outside period range ---

    @FT-PCV-005
    Scenario: JE with entry_date outside period range blocks close listing offending JE IDs
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period has a journal entry with entry_date 2099-02-01 (outside the period range)
        When I close the fiscal period
        Then the close fails with a validation error
        And the error output identifies the data hygiene check as failed
        And the error output lists the IDs of the journal entries with out-of-range entry dates

    # --- Open Obligations ---

    @FT-PCV-006
    Scenario: In-flight obligation instance blocks close listing instance IDs and agreement names
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period has an obligation instance due 2099-01-15 in status "in_flight"
        When I close the fiscal period
        Then the close fails with a validation error
        And the error output identifies the open obligations check as failed
        And the error output lists the in-flight instance ID and its agreement name

    @FT-PCV-007
    Scenario Outline: Non-blocking obligation statuses do not prevent close
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period has an obligation instance due 2099-01-15 in status "<status>"
        And there are no other validation failures in the period
        When I close the fiscal period
        Then the close succeeds and the period has is_open = false

        Examples:
            | status    |
            | expected  |
            | overdue   |
            | confirmed |
            | posted    |

    # --- Force Bypass ---

    @FT-PCV-008
    Scenario: --force --note bypasses validation failures and logs the bypass in the audit entry
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period has a journal entry with mismatched debit and credit totals
        When I close the fiscal period with --force and --note "CFO approved manual close"
        Then the close succeeds and the period has is_open = false
        And the audit trail for the period contains an entry with the force note "CFO approved manual close"

    @FT-PCV-009
    Scenario: --force without --note is rejected with a clear error
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period has a journal entry with mismatched debit and credit totals
        When I close the fiscal period with --force but no --note
        Then the close fails with error containing "--note"

    # --- Dry-Run (validate command) ---

    @FT-PCV-010
    Scenario: fiscal-period validate reports check results without closing the period
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period has a voided journal entry with no void reason recorded
        When I run the fiscal-period validate command for the period
        Then the command exits with a non-zero code
        And the output reports the data hygiene check as failed
        And the period remains open

    @FT-PCV-010b
    Scenario: fiscal-period validate on a clean period reports all checks passed without closing
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period has only balanced journal entries with no data hygiene issues
        And there are no in-flight obligation instances in the period
        When I run the fiscal-period validate command for the period
        Then the command exits with code 0
        And the output reports all checks as passed
        And the period remains open

    # --- Logging Under Force ---

    @FT-PCV-011
    Scenario: Validation results are computed and logged even when force-closing
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period has a journal entry with mismatched debit and credit totals
        When I close the fiscal period with --force and --note "emergency close"
        Then the close succeeds and the period has is_open = false
        And the log output contains the trial balance check result

    # --- Composability ---

    @FT-PCV-012
    Scenario: Multiple validation failures are all reported together
        Given a period-test open fiscal period from 2099-01-01 to 2099-01-31
        And the period has a journal entry with mismatched debit and credit totals
        And the period has a voided journal entry with no void reason recorded
        And the period has an obligation instance due 2099-01-15 in status "in_flight"
        When I close the fiscal period
        Then the close fails with a validation error
        And the error output identifies the trial balance check as failed
        And the error output identifies the data hygiene check as failed
        And the error output identifies the open obligations check as failed
