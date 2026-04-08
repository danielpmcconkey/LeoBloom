Feature: Connection Injection and Test Isolation
    Service functions accept NpgsqlTransaction from the caller rather than
    creating their own connections internally. CLI command handlers become
    the transaction boundary in production. Tests use a rollback-only
    pattern: all setup, service calls, and assertions share a single
    transaction that rolls back on dispose, leaving zero database footprint.

    # Scope: Observable outcomes of the connection injection architecture.
    # Structural acceptance criteria (S1-S5) — no service calls
    # DataSource.openConnection(), all services accept NpgsqlTransaction as
    # first param, CLI handlers own the lifecycle, cleanup infrastructure
    # removed, repos unchanged — are grep-verifiable and owned by the
    # Builder/QE as structural checks, not Gherkin scenarios.

    # --- Test Suite Reliability ---

    @FT-CI-001
    Scenario: Full test suite passes with serial execution
        Given the leobloom_dev database is accessible
        And LEOBLOOM_ENV and LEOBLOOM_DB_PASSWORD are set
        When I run the full test suite with xUnit.MaxParallelThreads=1
        Then all tests pass with 0 failures

    @FT-CI-002
    Scenario: Full test suite passes with default parallel execution
        Given the leobloom_dev database is accessible
        And LEOBLOOM_ENV and LEOBLOOM_DB_PASSWORD are set
        When I run the full test suite with default parallel settings
        Then all tests pass with 0 failures

    @FT-CI-003
    Scenario: Five consecutive parallel test runs produce zero failures
        Given the leobloom_dev database is accessible
        And LEOBLOOM_ENV and LEOBLOOM_DB_PASSWORD are set
        When I run the full test suite 5 times consecutively with default parallel settings
        Then every run produces 0 failures

    # --- Data Isolation ---

    @FT-CI-004
    Scenario: Test run leaves no data in the database
        Given the leobloom_dev database is in its seeded baseline state
        And portfolio.tax_bucket contains exactly 5 rows
        When I run the full test suite
        Then portfolio.tax_bucket still contains exactly 5 rows
        And no orphaned test rows exist in any service table

    # --- Production Commit Behavior ---

    @FT-CI-005
    Scenario: CLI account list command completes successfully after refactor
        Given the leobloom_dev database is accessible
        And LEOBLOOM_ENV and LEOBLOOM_DB_PASSWORD are set
        When I run "leobloom account list"
        Then the command exits with code 0
        And the output lists the seeded accounts
