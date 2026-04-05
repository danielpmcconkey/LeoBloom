Feature: Account:Amount Argument Parsing
    The ledger post command accepts --debit and --credit arguments in
    acct:amount format (e.g., "1010:500.00"). The CLI must parse these
    correctly and produce clear error messages for malformed input.
    These are CLI-layer parse errors, not service validation errors.

    # --- Valid Formats ---

    @FT-AAP-001
    Scenario: Integer account ID with decimal amount parses correctly
        Given a CLI-testable ledger environment
        When I run the CLI with "ledger post --debit 1010:1000.00 --credit 4010:1000.00 --date 2026-03-15 --description "Parse test" --fiscal-period-id <period-id>"
        Then the exit code is 0

    @FT-AAP-002
    Scenario: Whole number amount without decimal parses correctly
        Given a CLI-testable ledger environment
        When I run the CLI with "ledger post --debit 1010:1000 --credit 4010:1000 --date 2026-03-15 --description "No decimal" --fiscal-period-id <period-id>"
        Then the exit code is 0

    # --- Malformed acct:amount ---

    @FT-AAP-003
    Scenario Outline: Malformed acct:amount values are rejected with clear errors
        When I run the CLI with "ledger post --debit <bad-value> --credit 4010:1000.00 --date 2026-03-15 --description "Bad parse" --fiscal-period-id 1"
        Then stderr contains an error message about the invalid format
        And the exit code is 1

        Examples:
            | bad-value     | # reason                          |
            | 1010          | # missing colon separator         |
            | :1000.00      | # missing account ID              |
            | 1010:         | # missing amount                  |
            | 1010:abc      | # non-numeric amount              |
            | abc:1000.00   | # non-numeric account ID          |
            | 1010:100:00   | # extra colon (ambiguous split)   |

    @FT-AAP-004
    Scenario: Negative amount in acct:amount is rejected
        When I run the CLI with "ledger post --debit 1010:-500.00 --credit 4010:500.00 --date 2026-03-15 --description "Negative" --fiscal-period-id 1"
        Then stderr contains an error message
        And the exit code is 1

    @FT-AAP-005
    Scenario: Zero amount in acct:amount is rejected
        When I run the CLI with "ledger post --debit 1010:0.00 --credit 4010:0.00 --date 2026-03-15 --description "Zero" --fiscal-period-id 1"
        Then stderr contains an error message
        And the exit code is 1

    # --- Date Parsing ---

    @FT-AAP-006
    Scenario: Invalid date format is rejected
        When I run the CLI with "ledger post --debit 1010:100.00 --credit 4010:100.00 --date not-a-date --description "Bad date" --fiscal-period-id 1"
        Then stderr contains an error message
        And the exit code is 1

    @FT-AAP-007
    Scenario: Date in wrong format is rejected
        When I run the CLI with "ledger post --debit 1010:100.00 --credit 4010:100.00 --date 03/15/2026 --description "US format" --fiscal-period-id 1"
        Then stderr contains an error message
        And the exit code is 1
