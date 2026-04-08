Feature: Account Update CLI Command
    The `account update` subcommand modifies mutable fields on an existing
    account (name, subtype, external_ref). It requires at least one mutable
    flag, validates the subtype against the account's type, and outputs
    a before/after diff for every changed field.

    # ===================================================================
    # account update — happy path
    # ===================================================================

    @FT-ACT-070
    Scenario: Update account name shows before and after values
        Given a CLI-testable account environment with seeded data
        And a crud-test active account with code "upd-cli-001" and name "Original Name"
        When I run the CLI with "account update <id> --name Updated Name"
        Then stdout contains "Original Name"
        And stdout contains "Updated Name"
        And the exit code is 0

    @FT-ACT-071
    Scenario: Update account subtype with a valid subtype for the account type
        Given a CLI-testable account environment with seeded data
        And a crud-test active account with code "upd-cli-002" and name "Checking" of type "asset"
        When I run the CLI with "account update <id> --subtype Checking"
        Then stdout contains the account details
        And the exit code is 0

    @FT-ACT-072
    Scenario: Update account external_ref stores the new reference
        Given a CLI-testable account environment with seeded data
        And a crud-test active account with code "upd-cli-003" and name "Ally Savings"
        When I run the CLI with "account update <id> --external-ref x9999"
        Then stdout contains "x9999"
        And the exit code is 0

    @FT-ACT-073
    Scenario: Update multiple fields at once shows all changed values
        Given a CLI-testable account environment with seeded data
        And a crud-test active account with code "upd-cli-004" and name "Old Name"
        When I run the CLI with "account update <id> --name New Name --external-ref ref-001"
        Then stdout contains "Old Name"
        And stdout contains "New Name"
        And stdout contains "ref-001"
        And the exit code is 0

    @FT-ACT-074
    Scenario: Update with --json flag outputs valid JSON with before and after
        Given a CLI-testable account environment with seeded data
        And a crud-test active account with code "upd-cli-005" and name "JSON Target"
        When I run the CLI with "account update <id> --name JSON Updated --json"
        Then stdout is valid JSON
        And stdout contains "before"
        And stdout contains "after"
        And the exit code is 0

    # ===================================================================
    # account update — validation errors
    # ===================================================================

    @FT-ACT-075
    Scenario: Update with no mutable flags prints error to stderr
        Given a CLI-testable account environment with seeded data
        And a crud-test active account with code "upd-cli-006" and name "No Op Target"
        When I run the CLI with "account update <id>"
        Then stderr contains an error message
        And the exit code is 1

    @FT-ACT-076
    Scenario: Update with invalid subtype for account type prints error to stderr
        Given a CLI-testable account environment with seeded data
        And a crud-test active account with code "upd-cli-007" and name "Bad Subtype" of type "equity"
        When I run the CLI with "account update <id> --subtype Cash"
        Then stderr contains an error message
        And the exit code is 1

    @FT-ACT-077
    Scenario: Update a nonexistent account ID prints error to stderr
        Given a CLI-testable account environment with seeded data
        When I run the CLI with "account update 999999 --name Ghost"
        Then stderr contains an error message
        And the exit code is 1

    @FT-ACT-078
    Scenario: Update with no account ID argument prints error to stderr
        When I run the CLI with "account update"
        Then stderr contains an error message
        And the exit code is 2

    # ===================================================================
    # immutable field non-exposure
    # ===================================================================

    @FT-ACT-079
    Scenario: Update command does not expose a --code flag
        When I run the CLI with "account update --help"
        Then stdout does not contain "--code"
        And the exit code is 0

    @FT-ACT-080
    Scenario: Update command does not expose a --type flag
        When I run the CLI with "account update --help"
        Then stdout does not contain "--type"
        And the exit code is 0
