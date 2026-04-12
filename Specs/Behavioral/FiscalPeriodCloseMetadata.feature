Feature: Fiscal Period Close Metadata & Audit Trail
    When a fiscal period is closed or reopened, the system records who performed
    the action, when, and why. Close metadata (closed_at, closed_by) is persisted
    on the period and audit entries are written to fiscal_period_audit. Reopen
    clears close metadata and increments the reopen counter. An audit subcommand
    exposes the full chronological event history for a period.

    Background:
        Given the ledger schema exists for period management

    # --- Close Metadata ---

    @FT-CFM-001
    Scenario: Close sets closed_at and closed_by on the period
        Given a period-test open fiscal period from 2098-01-01 to 2098-01-31
        When I close the fiscal period with actor "alice"
        Then the close succeeds and the period has is_open = false
        And the period has closed_at set to a recent timestamp
        And the period has closed_by equal to "alice"

    @FT-CFM-002
    Scenario: Close writes an audit row with actor and note
        Given a period-test open fiscal period from 2098-01-01 to 2098-01-31
        When I close the fiscal period with actor "alice" and note "month end"
        Then the audit trail for the period contains 1 entry
        And the audit entry has action "closed", actor "alice", and note "month end"

    @FT-CFM-003
    Scenario: Closing an already-closed period is idempotent and returns a clear message
        Given a period-test open fiscal period from 2098-01-01 to 2098-01-31
        And the fiscal period has been closed with actor "alice"
        When I close the fiscal period with actor "bob"
        Then the close succeeds and the output indicates the period is already closed
        And the audit trail for the period contains 1 entry

    # --- Reopen Metadata ---

    @FT-CFM-004
    Scenario: Reopen clears close metadata and increments reopened_count
        Given a period-test open fiscal period from 2098-01-01 to 2098-01-31
        And the fiscal period has been closed with actor "alice"
        When I reopen the fiscal period with reason "correction needed" and actor "bob"
        Then the reopen succeeds and the period has is_open = true
        And the period has closed_at cleared
        And the period has closed_by cleared
        And the period has reopened_count equal to 1

    @FT-CFM-005
    Scenario: Reopen writes an audit row with actor and reason as note
        Given a period-test open fiscal period from 2098-01-01 to 2098-01-31
        And the fiscal period has been closed with actor "alice"
        When I reopen the fiscal period with reason "correction needed" and actor "bob"
        Then the audit trail for the period contains 2 entries
        And the most recent audit entry has action "reopened", actor "bob", and note "correction needed"

    # --- Audit Trail ---

    @FT-CFM-006
    Scenario: Full close-reopen-close cycle produces a correct three-entry audit trail
        Given a period-test open fiscal period from 2098-01-01 to 2098-01-31
        When I close the fiscal period with actor "alice"
        And I reopen the fiscal period with reason "missed invoice" and actor "bob"
        And I close the fiscal period with actor "alice"
        Then the audit trail for the period contains 3 entries in chronological order
        And the audit entries have actions "closed", "reopened", "closed" in that order

    # --- Audit CLI Subcommand ---

    @FT-CFM-007
    Scenario: fiscal-period audit lists all close/reopen events for a period
        Given a period-test open fiscal period from 2098-01-01 to 2098-01-31
        And the fiscal period has been closed with actor "alice"
        And the fiscal period has been reopened with reason "missed invoice" and actor "bob"
        When I run the fiscal-period audit command for the period
        Then the command exits with code 0
        And the output lists 2 audit entries
        And the first entry shows action "closed" and actor "alice"
        And the second entry shows action "reopened" and actor "bob"

    @FT-CFM-008
    Scenario: fiscal-period audit on a nonexistent period exits with code 1
        When I run the fiscal-period audit command for period id 999999
        Then the command exits with code 1
        And the output contains an error indicating the period does not exist

    # --- List Output ---

    @FT-CFM-009
    Scenario: fiscal-period list shows closed_at and reopened_count columns
        Given a period-test open fiscal period from 2098-01-01 to 2098-01-31
        And the fiscal period has been closed with actor "alice"
        When I run the fiscal-period list command
        Then the output includes a closed_at value for the period
        And the output includes a reopened_count value of 0 for the period

    # --- JSON Output ---

    @FT-CFM-010
    @FT-CFM-011
    @FT-CFM-012
    @FT-CFM-013
    Scenario Outline: --json flag produces valid JSON for all new and modified commands
        Given a period-test open fiscal period from 2098-01-01 to 2098-01-31
        And the fiscal period has been closed with actor "alice"
        When I run the fiscal-period <subcommand> with --json
        Then the command exits with code 0
        And the output is valid JSON

        Examples:
            | subcommand                                              |
            | close (idempotent — period already closed)              |
            | reopen with reason "correction" and actor "dan"         |
            | audit                                                   |
            | list                                                    |
