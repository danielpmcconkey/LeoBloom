Feature: CLI Shared Date Parsing (CliHelpers)

    The consolidated parseDate function in CliHelpers enforces strict
    yyyy-MM-dd format via TryParseExact. TransferCommands previously used
    lenient TryParse — accepting formats like 4/1/2026 or 2026/4/1 — while
    its own error message claimed "expected yyyy-MM-dd". All other command
    modules already used strict parsing. After consolidation, all command
    modules share the same strict parser.

    # Scope: This spec covers the behavioral change introduced by P071 —
    # specifically that TransferCommands now enforces strict date format.
    #
    # Structural criteria (AC-1: one parseDate definition; AC-3: one
    # parsePeriodArg definition) and the regression gate (AC-4: all
    # existing tests pass unchanged) are owned by the QE as structural
    # unit assertions, not Gherkin scenarios.
    #
    # parsePeriodArg behavior (numeric → ID, non-numeric → key) is already
    # covered by FT-PRD-020 and FT-PRD-021 in PeriodCommands.feature.

    # ===================================================================
    # TransferCommands strict date enforcement (AC-2)
    # ===================================================================

    # TransferCommands previously accepted lenient date input via
    # DateOnly.TryParse. After consolidation it uses the shared parseDate
    # (TryParseExact "yyyy-MM-dd"), matching every other CLI command module.

    @FT-CLH-001
    Scenario Outline: Transfer initiate rejects date formats that TryParse accepts but TryParseExact rejects
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer initiate --from-account 1010 --to-account 1020 --amount 500.00 --date <raw-date>"
        Then stderr contains an error message
        And the exit code is 1

        Examples:
            | raw-date   | # note                                      |
            | 4/1/2026   | # US locale M/d/yyyy (TryParse accepts)     |
            | 4-1-2026   | # M-d-yyyy with dash separators             |
            | 2026-4-1   | # unpadded month/day yyyy-M-d               |
            | 01/04/2026 | # day-first DD/MM/YYYY                      |

    @FT-CLH-002
    Scenario: Transfer confirm rejects a non-strict date format
        Given a CLI-testable transfer environment
        And an initiated transfer with known ID
        When I run the CLI with "transfer confirm <transfer-id> --date 4/1/2026"
        Then stderr contains an error message
        And the exit code is 1

    @FT-CLH-003
    Scenario: Transfer list rejects non-strict date in --from filter
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer list --from 4/1/2026"
        Then stderr contains an error message
        And the exit code is 1

    @FT-CLH-004
    Scenario: Transfer list rejects non-strict date in --to filter
        Given a CLI-testable transfer environment
        When I run the CLI with "transfer list --to 4/1/2026"
        Then stderr contains an error message
        And the exit code is 1
