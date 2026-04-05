Feature: CLI Framework
    The top-level CLI entry point handles argument routing, help output,
    unknown commands, and exit code mapping. Framework behavior is
    independent of any specific command group.

    # --- Help Output ---

    @FT-CLF-001
    Scenario: Top-level --help prints usage with available command groups
        When I run the CLI with "--help"
        Then stdout contains "ledger"
        And the exit code is 0

    @FT-CLF-002
    Scenario: Ledger --help prints available subcommands
        When I run the CLI with "ledger --help"
        Then stdout contains "post"
        And stdout contains "void"
        And stdout contains "show"
        And the exit code is 0

    # --- Unknown / Invalid Commands ---

    @FT-CLF-003
    Scenario: Unknown top-level command prints error to stderr
        When I run the CLI with "garbage"
        Then stderr contains an error message
        And the exit code is 2

    @FT-CLF-004
    Scenario: No arguments prints usage or error to stderr
        When I run the CLI with no arguments
        Then stderr contains an error message
        And the exit code is 2

    # --- Exit Code Mapping ---

    @FT-CLF-005
    Scenario Outline: Exit codes follow the documented convention
        When I run the CLI with "<args>"
        Then the exit code is <code>

        Examples:
            | args                | code | # reason                     |
            | --help              | 0    | # help is success            |
            | garbage             | 2    | # parse error / unknown cmd  |
            | ledger show 999999  | 1    | # business error (not found) |
