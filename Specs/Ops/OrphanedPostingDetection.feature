Feature: Orphaned Posting Detection
    Read-only diagnostic that detects journal entries in inconsistent states
    relative to their source records (obligation instances and transfers).
    Three primary conditions are detected:

    - DanglingStatus: JE reference exists but the source record's
      journal_entry_id is NULL (JE posted, source never updated)
    - MissingSource: JE reference points to a source record ID that
      does not exist in the corresponding table
    - VoidedBackingEntry: Source record is in posted/confirmed status
      but its referenced journal entry has been voided

    A fourth condition, InvalidReference, is reported when
    reference_value is non-numeric for obligation/transfer types,
    preventing a silent cast failure.

    Results are reported only. No mutations.

    # ===================================================================
    # Clean Result
    # ===================================================================

    @FT-OPD-001
    Scenario: No orphaned postings returns a clean empty result
        Given no orphaned posting conditions exist in the database
        When I run the orphaned posting diagnostic
        Then the diagnostic succeeds
        And the result contains 0 orphaned postings

    # ===================================================================
    # Dangling Status (reference → source, journal_entry_id is NULL)
    # ===================================================================

    @FT-OPD-002
    Scenario Outline: Detects dangling status update for <source_type>
        Given a journal entry with reference_type "<source_type>" pointing to a <source_type> whose journal_entry_id is NULL
        When I run the orphaned posting diagnostic
        Then the diagnostic succeeds
        And the result contains 1 orphaned posting
        And the orphaned posting has source_type "<source_type>" and condition "DanglingStatus"

        Examples:
            | source_type  |
            | obligation   |
            | transfer     |

    # ===================================================================
    # Missing Source (reference → source row does not exist)
    # ===================================================================

    @FT-OPD-003
    Scenario Outline: Detects missing source record for <source_type>
        Given a journal entry with reference_type "<source_type>" pointing to a nonexistent <source_type> ID
        When I run the orphaned posting diagnostic
        Then the diagnostic succeeds
        And the result contains 1 orphaned posting
        And the orphaned posting has source_type "<source_type>" and condition "MissingSource"

        Examples:
            | source_type  |
            | obligation   |
            | transfer     |

    # ===================================================================
    # Voided Backing Entry (source → JE is voided)
    # ===================================================================

    @FT-OPD-004
    Scenario Outline: Detects <source_type> in <status> status backed by a voided journal entry
        Given a <source_type> in status "<status>" whose journal_entry_id references a voided journal entry
        When I run the orphaned posting diagnostic
        Then the diagnostic succeeds
        And the result contains 1 orphaned posting
        And the orphaned posting has source_type "<source_type>" and condition "VoidedBackingEntry"

        Examples:
            | source_type  | status     |
            | obligation   | posted     |
            | transfer     | confirmed  |

    # ===================================================================
    # Normal Postings Are Not Flagged
    # ===================================================================

    @FT-OPD-005
    Scenario: Properly completed obligation posting is not reported as an orphan
        Given a properly completed obligation posting with journal_entry_id set, a matching journal_entry_reference, and a non-voided journal entry
        When I run the orphaned posting diagnostic
        Then the diagnostic succeeds
        And the result contains 0 orphaned postings

    # ===================================================================
    # Invalid Reference (non-numeric reference_value — bonus condition)
    # ===================================================================

    @FT-OPD-006
    Scenario: Non-numeric reference_value for obligation type is reported as InvalidReference
        Given a journal entry with reference_type "obligation" and a non-numeric reference_value
        When I run the orphaned posting diagnostic
        Then the diagnostic succeeds
        And the result contains 1 orphaned posting
        And the orphaned posting has source_type "obligation" and condition "InvalidReference"

    # ===================================================================
    # JSON Output
    # ===================================================================

    @FT-OPD-007
    Scenario: JSON output mode returns valid JSON containing the orphan data
        Given a journal entry with reference_type "obligation" pointing to an obligation whose journal_entry_id is NULL
        When I run the orphaned posting diagnostic with JSON output
        Then the diagnostic succeeds
        And the output is valid JSON
        And the JSON output contains 1 orphaned posting entry with condition "DanglingStatus"
