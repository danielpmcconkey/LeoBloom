Feature: Orphaned Posting Detection
    Read-only diagnostic command that detects journal entries orphaned by
    pre-P043 posting failures. Three conditions are detected:

      1. DanglingStatus — a journal_entry_reference points to a source record
         whose journal_entry_id is NULL (the JE posted, but the source record
         was never updated).

      2. MissingSource — a journal_entry_reference points to a source record
         ID that does not exist in the corresponding table.

      3. VoidedBackingEntry — an obligation instance in "posted" status or a
         transfer in "confirmed" status whose journal_entry_id references a
         voided journal entry. Per REM-015 this is a reportable condition, not
         an error.

    A fourth bonus condition (InvalidReference) is detected when
    reference_value cannot be cast to integer — a defensive guard for data
    anomalies noted in the spec's risk section.

    No mutations occur. Exit code 0 on success regardless of findings.

    # --- Clean State (AC-B1) ---

    @FT-OPD-001
    Scenario: No orphaned postings returns clean summary
        Given the ledger and ops tables contain no orphaned posting conditions
        When I run the orphaned-postings diagnostic
        Then the command exits with code 0
        And the output contains "No orphaned postings found"

    # --- Dangling Status (AC-B2) ---

    @FT-OPD-002
    Scenario: Detects obligation reference where instance journal_entry_id is NULL
        Given an obligation instance with journal_entry_id NULL
        And a journal_entry_reference with type "obligation" and value matching that instance ID
        When I run the orphaned-postings diagnostic
        Then the command exits with code 0
        And the results include a "DanglingStatus" condition for source type "obligation"
        And the result row includes the obligation instance ID

    # --- Dangling Status (AC-B3) ---

    @FT-OPD-003
    Scenario: Detects transfer reference where transfer journal_entry_id is NULL
        Given a transfer with journal_entry_id NULL
        And a journal_entry_reference with type "transfer" and value matching that transfer ID
        When I run the orphaned-postings diagnostic
        Then the command exits with code 0
        And the results include a "DanglingStatus" condition for source type "transfer"
        And the result row includes the transfer ID

    # --- Missing Source (AC-B4) ---

    @FT-OPD-004
    Scenario: Detects obligation reference pointing to nonexistent obligation instance
        Given a journal_entry_reference with type "obligation" and a reference_value for a nonexistent obligation instance ID
        When I run the orphaned-postings diagnostic
        Then the command exits with code 0
        And the results include a "MissingSource" condition for source type "obligation"

    # --- Missing Source (AC-B5) ---

    @FT-OPD-005
    Scenario: Detects transfer reference pointing to nonexistent transfer
        Given a journal_entry_reference with type "transfer" and a reference_value for a nonexistent transfer ID
        When I run the orphaned-postings diagnostic
        Then the command exits with code 0
        And the results include a "MissingSource" condition for source type "transfer"

    # --- Voided Backing Entry (AC-B6) ---

    @FT-OPD-006
    Scenario: Detects posted obligation instance whose backing journal entry is voided
        Given an obligation instance in "posted" status with a non-null journal_entry_id
        And that journal entry has voided_at set
        When I run the orphaned-postings diagnostic
        Then the command exits with code 0
        And the results include a "VoidedBackingEntry" condition for source type "obligation"
        And the result row includes the obligation instance ID and journal entry ID

    # --- Voided Backing Entry (AC-B7) ---

    @FT-OPD-007
    Scenario: Detects confirmed transfer whose backing journal entry is voided
        Given a transfer in "confirmed" status with a non-null journal_entry_id
        And that journal entry has voided_at set
        When I run the orphaned-postings diagnostic
        Then the command exits with code 0
        And the results include a "VoidedBackingEntry" condition for source type "transfer"
        And the result row includes the transfer ID and journal entry ID

    # --- False Positive Guard (AC-B8) ---

    @FT-OPD-008
    Scenario: Properly completed obligation posting is not flagged
        Given an obligation instance in "posted" status with a non-null journal_entry_id
        And a non-voided journal entry with a reference of type "obligation" matching that instance ID
        When I run the orphaned-postings diagnostic
        Then the command exits with code 0
        And the output contains "No orphaned postings found"

    # --- JSON Output (AC-B9) ---

    @FT-OPD-009
    Scenario: JSON flag produces valid JSON array with the same findings
        Given an obligation instance with journal_entry_id NULL
        And a journal_entry_reference with type "obligation" and value matching that instance ID
        When I run the orphaned-postings diagnostic with --json
        Then the command exits with code 0
        And the output is valid JSON
        And the JSON array contains a result with sourceType "obligation" and condition "DanglingStatus"

    # --- Invalid Reference (bonus condition, approved by PO) ---
    # Handles the varchar-to-integer casting risk called out in the spec's Risk Notes.

    @FT-OPD-010
    Scenario: Non-numeric reference_value is reported as InvalidReference
        Given a journal_entry_reference with type "obligation" and a non-numeric reference_value
        When I run the orphaned-postings diagnostic
        Then the command exits with code 0
        And the results include an "InvalidReference" condition for source type "obligation"
