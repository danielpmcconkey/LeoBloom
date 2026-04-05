Feature: Spawn Obligation Instances
    Given an obligation agreement and a date range, materialize
    obligation_instance rows for each occurrence based on the agreement's
    cadence. No status transitions, no overdue detection, no ledger posting.
    Just spawning.

    # --- Pure Date Generation: Monthly ---

    @FT-SI-001
    Scenario: Monthly cadence produces correct dates for full range
        When I generate expected dates for monthly cadence with expectedDay 15 from 2026-01-01 to 2026-06-30
        Then 6 dates are generated: 2026-01-15, 2026-02-15, 2026-03-15, 2026-04-15, 2026-05-15, 2026-06-15

    @FT-SI-002
    Scenario: Monthly cadence clamps day 31 to February 28 in non-leap year
        When I generate expected dates for monthly cadence with expectedDay 31 from 2027-02-01 to 2027-02-28
        Then 1 date is generated: 2027-02-28

    @FT-SI-003
    Scenario: Monthly cadence clamps day 31 to February 29 in leap year
        When I generate expected dates for monthly cadence with expectedDay 31 from 2028-02-01 to 2028-02-29
        Then 1 date is generated: 2028-02-29

    @FT-SI-004
    Scenario: Monthly cadence defaults expectedDay to 1 when agreement has none
        When I generate expected dates for monthly cadence with no expectedDay from 2026-01-01 to 2026-03-31
        Then 3 dates are generated: 2026-01-01, 2026-02-01, 2026-03-01

    # --- Pure Date Generation: Quarterly ---

    @FT-SI-005
    Scenario: Quarterly cadence produces four dates for full-year range
        When I generate expected dates for quarterly cadence with expectedDay 15 from 2026-01-01 to 2026-12-31
        Then 4 dates are generated: 2026-01-15, 2026-04-15, 2026-07-15, 2026-10-15

    @FT-SI-006
    Scenario: Quarterly cadence for partial range includes only in-range dates
        When I generate expected dates for quarterly cadence with expectedDay 15 from 2026-03-15 to 2026-09-30
        Then 2 dates are generated: 2026-04-15, 2026-07-15

    # --- Pure Date Generation: Annual ---

    @FT-SI-007
    Scenario: Annual cadence produces one date per year across multi-year range
        When I generate expected dates for annual cadence with expectedDay 15 from 2026-06-01 to 2028-06-30
        Then 3 dates are generated: 2026-06-15, 2027-06-15, 2028-06-15

    # --- Pure Date Generation: OneTime ---

    @FT-SI-008
    Scenario: OneTime cadence produces exactly one date at startDate
        When I generate expected dates for one_time cadence from 2026-05-20 to 2026-12-31
        Then 1 date is generated: 2026-05-20

    # --- Pure Date Generation: Validation ---

    @FT-SI-009
    Scenario: Date range where startDate is after endDate is rejected
        When I generate expected dates for monthly cadence with expectedDay 15 from 2026-06-01 to 2026-01-01
        Then the spawn command is rejected with a validation error

    # --- Name Generation ---

    @FT-SI-010
    Scenario Outline: Instance names follow cadence-specific format
        When I generate an instance name for <cadence> cadence on date <date>
        Then the generated name is "<name>"

        Examples:
            | cadence   | date       | name      |
            | monthly   | 2026-01-15 | Jan 2026  |
            | monthly   | 2026-12-01 | Dec 2026  |
            | quarterly | 2026-01-15 | Q1 2026   |
            | quarterly | 2026-04-15 | Q2 2026   |
            | quarterly | 2026-07-15 | Q3 2026   |
            | quarterly | 2026-10-15 | Q4 2026   |
            | annual    | 2026-06-15 | 2026      |
            | one_time  | 2026-05-20 | One-time  |

    # --- Spawn Integration: Happy Path ---

    @FT-SI-011
    Scenario: Spawn monthly instances for a 3-month range
        Given the ops schema exists for agreement management
        And an active monthly obligation agreement with expectedDay 15 and amount 150.00
        When I spawn obligation instances from 2026-01-01 to 2026-03-31
        Then the spawn succeeds with 3 created instances and 0 skipped
        And each instance has status "expected" and isActive true
        And the instance dates are 2026-01-15, 2026-02-15, 2026-03-15
        And the instance names are "Jan 2026", "Feb 2026", "Mar 2026"
        And each instance has amount 150.00

    @FT-SI-012
    Scenario: Spawn for variable-amount agreement leaves instance amount empty
        Given the ops schema exists for agreement management
        And an active monthly obligation agreement with expectedDay 1 and no amount
        When I spawn obligation instances from 2026-01-01 to 2026-01-31
        Then the spawn succeeds with 1 created instance and 0 skipped
        And the created instance has no amount

    @FT-SI-013
    Scenario: Spawn OneTime creates a single instance
        Given the ops schema exists for agreement management
        And an active one_time obligation agreement with amount 500.00
        When I spawn obligation instances from 2026-05-20 to 2026-05-20
        Then the spawn succeeds with 1 created instance and 0 skipped
        And the created instance has name "One-time" and amount 500.00

    @FT-SI-014
    Scenario: All spawned instances have isActive true
        Given the ops schema exists for agreement management
        And an active quarterly obligation agreement with expectedDay 1 and amount 300.00
        When I spawn obligation instances from 2026-01-01 to 2026-12-31
        Then the spawn succeeds with 4 created instances and 0 skipped
        And each instance has isActive true

    # --- Spawn Integration: Overlap and Idempotency ---

    @FT-SI-015
    Scenario: Spawn overlapping range skips existing dates and creates new ones
        Given the ops schema exists for agreement management
        And an active monthly obligation agreement with expectedDay 15 and amount 100.00
        And instances already exist for the agreement on 2026-01-15 and 2026-02-15
        When I spawn obligation instances from 2026-01-01 to 2026-04-30
        Then the spawn succeeds with 2 created instances and 2 skipped
        And the created instance dates are 2026-03-15 and 2026-04-15

    @FT-SI-016
    Scenario: Spawn OneTime when instance already exists skips without error
        Given the ops schema exists for agreement management
        And an active one_time obligation agreement with amount 500.00
        And an instance already exists for the agreement on 2026-05-20
        When I spawn obligation instances from 2026-05-20 to 2026-05-20
        Then the spawn succeeds with 0 created instances and 1 skipped

    # --- Spawn Integration: Error Cases ---

    @FT-SI-017
    Scenario: Spawn for inactive agreement returns error
        Given the ops schema exists for agreement management
        And an inactive obligation agreement
        When I spawn obligation instances from 2026-01-01 to 2026-06-30
        Then the spawn fails with error containing "inactive"

    @FT-SI-018
    Scenario: Spawn for nonexistent agreement returns error
        Given the ops schema exists for agreement management
        When I spawn obligation instances for agreement ID 999999 from 2026-01-01 to 2026-06-30
        Then the spawn fails with error containing "does not exist"
