Feature: Fiscal Period Management
    Fiscal period creation enforces date-range integrity. A new period cannot
    be created if its date range overlaps any existing period. Overlap is
    detected at the service layer before INSERT and the error message
    identifies the conflicting period by key and dates. Adjacent periods
    (where one ends the day before the other starts) are not overlapping
    and must be accepted.

    Background:
        Given the ledger schema exists for period management

    # --- Overlap Prevention ---

    # FPM-001: partial overlap; FPM-002: identical date range
    @FT-FPM-001
    @FT-FPM-002
    Scenario Outline: Creating a period that overlaps an existing period is rejected
        Given a period-test fiscal period "<existing_key>" from <existing_start> to <existing_end>
        When I create a fiscal period "<new_key>" from <new_start> to <new_end>
        Then the creation fails with error containing "<existing_key>"

        Examples:
            | existing_key | existing_start | existing_end | new_key     | new_start  | new_end    |
            | 2026-Q1-JAN  | 2026-01-01     | 2026-01-31   | 2026-Q1-MID | 2026-01-15 | 2026-02-15 |
            | 2026-Q1-JAN  | 2026-01-01     | 2026-01-31   | 2026-Q1-DUP | 2026-01-01 | 2026-01-31 |

    # --- Adjacent Periods ---

    @FT-FPM-003
    Scenario: Creating adjacent non-overlapping periods succeeds
        Given a period-test fiscal period "2026-Q1-JAN" from 2026-01-01 to 2026-01-31
        When I create a fiscal period "2026-Q1-FEB" from 2026-02-01 to 2026-02-28
        Then the creation succeeds
