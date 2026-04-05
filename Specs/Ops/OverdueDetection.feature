Feature: Overdue Detection
    Batch operation that finds obligation instances past their expected date
    and transitions them to overdue. Only targets active instances in status
    "expected". Reference date is a parameter for testability.

    # --- Happy Path ---

    @FT-OD-001
    Scenario: Single overdue instance is transitioned
        Given an active obligation instance in status "expected" with expected_date 2026-03-15
        When I run overdue detection with reference date 2026-04-01
        Then 1 instance is transitioned to overdue
        And the result has 0 errors

    @FT-OD-002
    Scenario: Multiple overdue instances are all transitioned
        Given 3 active obligation instances in status "expected" with expected_dates before 2026-04-01
        When I run overdue detection with reference date 2026-04-01
        Then 3 instances are transitioned to overdue
        And the result has 0 errors

    @FT-OD-003
    Scenario: Instance on the reference date is not overdue
        Given an active obligation instance in status "expected" with expected_date 2026-04-01
        When I run overdue detection with reference date 2026-04-01
        Then 0 instances are transitioned to overdue

    @FT-OD-004
    Scenario: Instance after the reference date is not overdue
        Given an active obligation instance in status "expected" with expected_date 2026-04-15
        When I run overdue detection with reference date 2026-04-01
        Then 0 instances are transitioned to overdue

    # --- Filtering ---

    @FT-OD-005
    Scenario: In-flight instances are not flagged as overdue
        Given an active obligation instance in status "in_flight" with expected_date 2026-03-15
        When I run overdue detection with reference date 2026-04-01
        Then 0 instances are transitioned to overdue

    @FT-OD-006
    Scenario: Already overdue instances are not re-transitioned
        Given an active obligation instance in status "overdue" with expected_date 2026-03-15
        When I run overdue detection with reference date 2026-04-01
        Then 0 instances are transitioned to overdue

    @FT-OD-007
    Scenario: Confirmed instances are not flagged as overdue
        Given an active obligation instance in status "confirmed" with expected_date 2026-03-15
        When I run overdue detection with reference date 2026-04-01
        Then 0 instances are transitioned to overdue

    @FT-OD-008
    Scenario: Inactive instances are not flagged as overdue
        Given an inactive obligation instance in status "expected" with expected_date 2026-03-15
        When I run overdue detection with reference date 2026-04-01
        Then 0 instances are transitioned to overdue

    # --- Idempotency ---

    @FT-OD-009
    Scenario: Running detection twice produces same result
        Given an active obligation instance in status "expected" with expected_date 2026-03-15
        When I run overdue detection with reference date 2026-04-01
        Then 1 instance is transitioned to overdue
        When I run overdue detection again with reference date 2026-04-01
        Then 0 instances are transitioned to overdue

    # --- Mixed Scenarios ---

    @FT-OD-010
    Scenario: Only eligible instances among a mixed set are transitioned
        Given the following obligation instances:
            | status    | expected_date | is_active |
            | expected  | 2026-03-01    | true      |
            | expected  | 2026-03-15    | true      |
            | expected  | 2026-04-15    | true      |
            | in_flight | 2026-03-01    | true      |
            | confirmed | 2026-03-01    | true      |
            | expected  | 2026-03-01    | false     |
        When I run overdue detection with reference date 2026-04-01
        Then 2 instances are transitioned to overdue
        And the result has 0 errors

    # --- No Candidates ---

    @FT-OD-011
    Scenario: No overdue instances returns zero transitioned
        Given no obligation instances exist
        When I run overdue detection with reference date 2026-04-01
        Then 0 instances are transitioned to overdue
        And the result has 0 errors
