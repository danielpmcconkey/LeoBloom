Feature: Account Sub-Type Classification
    Accounts carry an optional subtype that classifies them within their
    account type (e.g., Cash vs Investment within Asset). The subtype is
    validated against the account type on every write path and persisted
    as a nullable varchar column.

    # --- Domain Validation: Valid Combinations ---

    @FT-AST-001
    Scenario Outline: Valid subtype for account type is accepted
        When I validate subtype "<subtype>" for account type "<account_type>"
        Then the validation succeeds

        Examples:
            | account_type | subtype           |
            | Asset        | Cash              |
            | Asset        | FixedAsset        |
            | Asset        | Investment        |
            | Liability    | CurrentLiability  |
            | Liability    | LongTermLiability |
            | Revenue      | OperatingRevenue  |
            | Revenue      | OtherRevenue      |
            | Expense      | OperatingExpense  |
            | Expense      | OtherExpense      |

    # --- Domain Validation: Invalid Combinations ---

    @FT-AST-002
    Scenario Outline: Invalid subtype for account type is rejected
        When I validate subtype "<subtype>" for account type "<account_type>"
        Then the validation fails

        Examples:
            | account_type | subtype           |
            | Revenue      | Cash              |
            | Expense      | Cash              |
            | Liability    | Cash              |
            | Asset        | CurrentLiability  |
            | Asset        | OperatingExpense  |
            | Revenue      | LongTermLiability |
            | Expense      | Investment        |
            | Liability    | OperatingRevenue  |

    @FT-AST-003
    Scenario Outline: Equity accepts no subtypes
        When I validate subtype "<subtype>" for account type "Equity"
        Then the validation fails

        Examples:
            | subtype           |
            | Cash              |
            | FixedAsset        |
            | Investment        |
            | CurrentLiability  |
            | LongTermLiability |
            | OperatingRevenue  |
            | OtherRevenue      |
            | OperatingExpense  |
            | OtherExpense      |

    # --- Domain Validation: Null Subtype ---

    @FT-AST-004
    Scenario Outline: Null subtype is valid for any account type
        When I validate a null subtype for account type "<account_type>"
        Then the validation succeeds

        Examples:
            | account_type |
            | Asset        |
            | Liability    |
            | Equity       |
            | Revenue      |
            | Expense      |

    # --- String Round-Trip ---

    @FT-AST-005
    Scenario Outline: toDbString and fromDbString round-trip all subtypes
        Given a subtype value of "<subtype>"
        When I convert it to a DB string and back
        Then the round-trip result equals the original "<subtype>"

        Examples:
            | subtype           |
            | Cash              |
            | FixedAsset        |
            | Investment        |
            | CurrentLiability  |
            | LongTermLiability |
            | OperatingRevenue  |
            | OtherRevenue      |
            | OperatingExpense  |
            | OtherExpense      |

    @FT-AST-006
    Scenario: fromDbString rejects an unrecognized string
        When I parse subtype from DB string "Bogus"
        Then the parse result is an error

    # --- Persistence: Round-Trip ---

    @FT-AST-007
    Scenario: Account with subtype persists and reads back correctly
        Given the ledger schema exists for subtype operations
        And a subtype-test active account 1110 of type asset with subtype Cash
        When I read account 1110
        Then the account has subtype Cash

    @FT-AST-008
    Scenario: Account with null subtype persists and reads back correctly
        Given the ledger schema exists for subtype operations
        And a subtype-test active account 1000 of type asset with no subtype
        When I read account 1000
        Then the account has no subtype

    @FT-AST-009
    Scenario Outline: Subtypes round-trip through write and read paths
        Given the ledger schema exists for subtype operations
        And a subtype-test active account <code> of type <account_type> with subtype <subtype>
        When I read account <code>
        Then the account has subtype <subtype>

        Examples:
            | code | account_type | subtype           |
            | 1110 | asset        | Cash              |
            | 1120 | asset        | FixedAsset        |
            | 1210 | asset        | Investment        |
            | 2110 | liability    | CurrentLiability  |
            | 2210 | liability    | LongTermLiability |
            | 4110 | revenue      | OperatingRevenue  |
            | 4210 | revenue      | OtherRevenue      |
            | 5110 | expense      | OperatingExpense  |
            | 5210 | expense      | OtherExpense      |

    # --- Domain Validation: Write-Path Guards ---
    # Note: No account CRUD service exists yet. These scenarios validate the
    # domain function that will gate future create/update paths.

    @FT-AST-010
    Scenario: Invalid subtype for account type is rejected by domain validation
        When I validate subtype "OperatingExpense" for account type "Asset"
        Then the validation fails

    @FT-AST-011
    Scenario: Any subtype on equity is rejected by domain validation
        When I validate subtype "Cash" for account type "Equity"
        Then the validation fails

    @FT-AST-012
    Scenario: Invalid subtype change is rejected by domain validation
        When I validate subtype "OperatingRevenue" for account type "Asset"
        Then the validation fails

    @FT-AST-013
    Scenario: Updating an account to a valid subtype succeeds
        Given the ledger schema exists for subtype operations
        And a subtype-test active account 1110 of type asset with subtype Cash
        When I update account 1110 to subtype Investment
        Then the update succeeds
        And the account has subtype Investment

    @FT-AST-014
    Scenario: Updating an account to null subtype succeeds
        Given the ledger schema exists for subtype operations
        And a subtype-test active account 1110 of type asset with subtype Cash
        When I update account 1110 to no subtype
        Then the update succeeds
        And the account has no subtype

    # --- Seed Data Correctness ---

    @FT-AST-015
    Scenario Outline: Seed accounts have expected subtypes after migration
        Given the ledger schema exists with seed data
        When I read account <code>
        Then the account has subtype <subtype>

        Examples:
            | code | subtype           |
            | 1110 | Cash              |
            | 1120 | Cash              |
            | 1210 | Investment        |
            | 1220 | Cash              |
            | 2110 | LongTermLiability |
            | 2210 | LongTermLiability |
            | 2220 | CurrentLiability  |
            | 4110 | OperatingRevenue  |
            | 4120 | OperatingRevenue  |
            | 4210 | OperatingRevenue  |
            | 4220 | OperatingRevenue  |
            | 7110 | OtherRevenue      |
            | 7210 | OtherExpense      |
            | 7220 | OtherExpense      |

    @FT-AST-016
    Scenario Outline: Header accounts have null subtype after migration
        Given the ledger schema exists with seed data
        When I read account <code>
        Then the account has no subtype

        Examples:
            | code |
            | 1000 |
            | 1100 |
            | 1200 |
            | 2000 |
            | 2100 |
            | 2200 |
            | 3000 |
            | 4000 |
            | 4100 |
            | 4200 |
            | 5000 |
            | 5100 |
            | 5300 |
            | 7100 |
            | 7200 |

    @FT-AST-017
    Scenario Outline: Equity leaf accounts have null subtype after migration
        Given the ledger schema exists with seed data
        When I read account <code>
        Then the account has no subtype

        Examples:
            | code |
            | 3010 |
            | 3020 |
            | 3099 |
