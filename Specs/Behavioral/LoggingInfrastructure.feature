Feature: Logging Infrastructure
    Serilog-based logging is initialized at startup, writes structured log
    entries to both console and file, and is configurable via appsettings.
    Service layer functions emit Info-level entries for traceability.

    # --- Initialization ---

    @FT-LI-001
    Scenario: Log.initialize is called at Api startup
        Given the file Src/LeoBloom.Api/Program.fs exists
        When I inspect Program.fs for logging initialization
        Then Log.initialize is called before the application starts

    @FT-LI-002
    Scenario: Log.initialize is called in test infrastructure
        Given the file Src/LeoBloom.Tests/TestHelpers.fs exists
        When I inspect TestHelpers.fs for logging initialization
        Then Log.initialize is called at module initialization

    # --- File output ---

    @FT-LI-003
    Scenario: Running tests creates a log file
        Given the LeoBloom solution exists
        And the log output directory /workspace/application_logs/leobloom exists
        When I run dotnet test on the solution
        Then a new log file appears in /workspace/application_logs/leobloom

    @FT-LI-004
    Scenario: Log filename follows the expected format
        Given the LeoBloom solution exists
        And the log output directory /workspace/application_logs/leobloom exists
        When I run dotnet test on the solution
        Then the new log file matches the pattern leobloom-yyyyMMdd.HH.mm.ss.log

    # --- Configuration ---

    @FT-LI-005
    Scenario: Minimum log level is configurable via appsettings
        Given an appsettings file with Serilog:MinimumLevel set to "Warning"
        When Log.initialize is called with that configuration
        Then Info-level messages are not written to the log file
        And Warning-level messages are written to the log file

    @FT-LI-006
    Scenario: File sink base path is configurable via appsettings
        Given an appsettings file with Logging:FileBasePath set to a custom directory
        When Log.initialize is called with that configuration
        Then the log file is created in the custom directory

    # --- Service layer logging ---

    @FT-LI-007
    Scenario: Posting a journal entry emits Info-level log entries
        Given the ledger schema exists for posting
        And logging is initialized
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-03-15 described as "Log test entry" with source "manual" and lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 1000.00 | credit     |
        Then the log output contains an Info-level entry for the post operation

    @FT-LI-008
    Scenario: Voiding a journal entry emits Info-level log entries
        Given the ledger schema exists for posting
        And logging is initialized
        And an existing posted journal entry
        When I void the journal entry
        Then the log output contains an Info-level entry for the void operation

    @FT-LI-009
    Scenario: Querying account balance by id emits Info-level log entry
        Given the ledger schema exists for posting
        And logging is initialized
        And an active account 1010 of type asset
        When I query the balance for account id 1010
        Then the log output contains an Info-level entry for the balance query

    @FT-LI-010
    Scenario: Querying account balance by code emits Info-level log entry
        Given the ledger schema exists for posting
        And logging is initialized
        And an active account 1010 of type asset
        When I query the balance for account code 1010
        Then the log output contains an Info-level entry for the balance query

    @FT-LI-011
    Scenario: DataSource initialization emits Info-level log entry
        Given logging is initialized
        When the DataSource module is accessed
        Then the log output contains an Info-level entry for DataSource initialization

    # --- Error path logging ---

    @FT-LI-012
    Scenario: Validation failure emits Warning-level log entry
        Given the ledger schema exists for posting
        And logging is initialized
        And a valid open fiscal period from 2026-03-01 to 2026-03-31
        And an active account 1010 of type asset
        And an active account 4010 of type revenue
        When I post a journal entry dated 2026-03-15 described as "Bad entry" with source "manual" and lines:
            | account | amount  | entry_type |
            | 1010    | 1000.00 | debit      |
            | 4010    | 500.00  | credit     |
        Then the log output contains a Warning-level entry for the validation failure
