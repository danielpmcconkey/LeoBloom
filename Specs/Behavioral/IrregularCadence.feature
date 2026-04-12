Feature: Irregular Recurrence Cadence
    Agreements with non-computable schedules are modelled with the "irregular"
    cadence. Instance spawning is manual — irregular agreements are skipped by
    the auto-spawn process. The cadence must round-trip through the domain
    without error.

    # --- Agreement Listing ---

    @FT-IRC-001
    Scenario: Listing agreements when one has irregular cadence does not crash
        Given an obligation agreement with cadence "irregular" exists
        When I list obligation agreements
        Then the list succeeds and includes the irregular-cadence agreement

    # --- Agreement Display ---

    @FT-IRC-002
    Scenario: Showing an irregular-cadence agreement displays cadence as "irregular"
        Given an obligation agreement with cadence "irregular" exists
        When I show that agreement
        Then the agreement cadence is displayed as "irregular"

    # --- Spawn Skips Irregular ---

    @FT-IRC-003
    Scenario: Spawning instances for an irregular-cadence agreement produces zero instances and no error
        Given an obligation agreement with cadence "irregular" exists
        When I spawn obligation instances for that agreement
        Then the spawn succeeds
        And 0 instances are created
