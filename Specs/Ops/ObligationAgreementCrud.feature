Feature: Obligation Agreement CRUD
    Create, retrieve, list, update, and deactivate obligation agreements.
    An obligation agreement defines the terms of a recurring financial
    obligation -- what, how much, how often, and between whom.

    # --- Create: Happy Path ---

    @FT-OA-001
    Scenario: Create agreement with all fields provided
        Given the ops schema exists for agreement management
        And an active account 1010 of type asset
        And an active account 5010 of type expense
        When I create an obligation agreement with:
            | field           | value              |
            | name            | Enbridge gas bill  |
            | obligationType  | payable            |
            | counterparty    | Enbridge           |
            | amount          | 150.00             |
            | cadence         | monthly            |
            | expectedDay     | 15                 |
            | paymentMethod   | autopay_pull       |
            | sourceAccountId | 1010               |
            | destAccountId   | 5010               |
            | notes           | Variable in winter |
        Then the create succeeds with a generated id and timestamps
        And the returned agreement has name "Enbridge gas bill" and obligationType "payable"

    @FT-OA-002
    Scenario: Create agreement with only required fields
        Given the ops schema exists for agreement management
        When I create an obligation agreement with:
            | field          | value             |
            | name           | Jeffrey rent      |
            | obligationType | receivable        |
            | cadence        | monthly           |
        Then the create succeeds with a generated id and timestamps
        And the returned agreement has null counterparty, amount, expectedDay, paymentMethod, sourceAccountId, destAccountId, and notes

    # --- Create: Pure Validation ---

    @FT-OA-003
    Scenario: Create with empty name is rejected
        Given the ops schema exists for agreement management
        When I create an obligation agreement with name ""
        Then the create fails with error containing "name"

    @FT-OA-004
    Scenario: Create with name exceeding 100 characters is rejected
        Given the ops schema exists for agreement management
        When I create an obligation agreement with a 101-character name
        Then the create fails with error containing "name"

    @FT-OA-005
    Scenario: Create with counterparty exceeding 100 characters is rejected
        Given the ops schema exists for agreement management
        When I create an obligation agreement with a 101-character counterparty
        Then the create fails with error containing "counterparty"

    @FT-OA-006
    Scenario Outline: Create with non-positive amount is rejected
        Given the ops schema exists for agreement management
        When I create an obligation agreement with amount <amount>
        Then the create fails with error containing "amount"

        Examples:
            | amount |
            | 0.00   |
            | -50.00 |

    @FT-OA-007
    Scenario Outline: Create with expected day outside 1-31 is rejected
        Given the ops schema exists for agreement management
        When I create an obligation agreement with expectedDay <day>
        Then the create fails with error containing "expected"

        Examples:
            | day |
            | 0   |
            | 32  |
            | -1  |

    @FT-OA-008
    Scenario: Create with multiple validation errors collects all errors
        Given the ops schema exists for agreement management
        When I create an obligation agreement with name "", amount -10.00, and expectedDay 0
        Then the create fails with at least 3 errors

    # --- Create: DB Validation ---

    @FT-OA-009
    Scenario: Create with nonexistent source account is rejected
        Given the ops schema exists for agreement management
        When I create an obligation agreement with sourceAccountId referencing nonexistent account 99999
        Then the create fails with error containing "source"

    @FT-OA-010
    Scenario: Create with inactive source account is rejected
        Given the ops schema exists for agreement management
        And an inactive account 8888 of type asset
        When I create an obligation agreement with sourceAccountId 8888
        Then the create fails with error containing "inactive"

    @FT-OA-011
    Scenario: Create with nonexistent dest account is rejected
        Given the ops schema exists for agreement management
        When I create an obligation agreement with destAccountId referencing nonexistent account 99999
        Then the create fails with error containing "dest"

    # --- Get by ID ---

    @FT-OA-012
    Scenario: Get agreement by ID returns the agreement
        Given the ops schema exists for agreement management
        And an existing obligation agreement named "Rocket Mortgage"
        When I get the agreement by its ID
        Then the result contains the agreement with name "Rocket Mortgage"

    @FT-OA-013
    Scenario: Get agreement by nonexistent ID returns none
        Given the ops schema exists for agreement management
        When I get agreement by ID 999999
        Then the result is none

    # --- List ---

    @FT-OA-014
    Scenario: List with default filter returns only active agreements
        Given the ops schema exists for agreement management
        And an existing active obligation agreement named "Active Agreement"
        And an existing deactivated obligation agreement named "Inactive Agreement"
        When I list agreements with default filter
        Then the result contains "Active Agreement"
        And the result does not contain "Inactive Agreement"

    @FT-OA-015
    Scenario: List filtered by obligation type
        Given the ops schema exists for agreement management
        And an existing active obligation agreement named "Rent Income" of type receivable
        And an existing active obligation agreement named "Gas Bill" of type payable
        When I list agreements filtered by obligationType "payable"
        Then the result contains "Gas Bill"
        And the result does not contain "Rent Income"

    @FT-OA-016
    Scenario: List filtered by cadence
        Given the ops schema exists for agreement management
        And an existing active obligation agreement named "Monthly Bill" with cadence monthly
        And an existing active obligation agreement named "Annual Insurance" with cadence annual
        When I list agreements filtered by cadence "monthly"
        Then the result contains "Monthly Bill"
        And the result does not contain "Annual Insurance"

    @FT-OA-017
    Scenario: List with no filter returns all including inactive
        Given the ops schema exists for agreement management
        And an existing active obligation agreement named "Active One"
        And an existing deactivated obligation agreement named "Inactive One"
        When I list agreements with no isActive filter
        Then the result contains both "Active One" and "Inactive One"

    @FT-OA-018
    Scenario: List returns empty when no agreements match
        Given the ops schema exists for agreement management
        And no obligation agreements exist
        When I list agreements with default filter
        Then the result is an empty list

    # --- Update: Happy Path ---

    @FT-OA-019
    Scenario: Update agreement with all fields
        Given the ops schema exists for agreement management
        And an active account 1010 of type asset
        And an existing obligation agreement named "Old Name" with amount 100.00
        When I update the agreement with name "New Name" and amount 200.00
        Then the update succeeds and the returned agreement has name "New Name" and amount 200.00
        And the modified_at timestamp is later than created_at

    # --- Update: Errors ---

    @FT-OA-020
    Scenario: Update nonexistent agreement is rejected
        Given the ops schema exists for agreement management
        When I update agreement ID 999999 with name "Ghost"
        Then the update fails with error containing "does not exist"

    @FT-OA-021
    Scenario: Update with empty name is rejected
        Given the ops schema exists for agreement management
        And an existing obligation agreement named "Valid Name"
        When I update the agreement with name ""
        Then the update fails with error containing "name"

    @FT-OA-022
    Scenario: Update with non-positive amount is rejected
        Given the ops schema exists for agreement management
        And an existing obligation agreement named "Test Agreement"
        When I update the agreement with amount 0.00
        Then the update fails with error containing "amount"

    @FT-OA-023
    Scenario: Update with expected day outside 1-31 is rejected
        Given the ops schema exists for agreement management
        And an existing obligation agreement named "Test Agreement"
        When I update the agreement with expectedDay 32
        Then the update fails with error containing "expected"

    @FT-OA-024
    Scenario: Update with inactive source account is rejected
        Given the ops schema exists for agreement management
        And an inactive account 8888 of type asset
        And an existing obligation agreement named "Test Agreement"
        When I update the agreement with sourceAccountId 8888
        Then the update fails with error containing "inactive"

    @FT-OA-025
    Scenario: Reactivate a previously deactivated agreement
        Given the ops schema exists for agreement management
        And an existing deactivated obligation agreement named "Dormant Agreement"
        When I update the agreement with isActive true
        Then the update succeeds and the returned agreement has isActive = true

    # --- Deactivate ---

    @FT-OA-026
    Scenario: Deactivate an active agreement
        Given the ops schema exists for agreement management
        And an existing active obligation agreement named "Soon Inactive"
        When I deactivate the agreement
        Then the deactivate succeeds and the returned agreement has isActive = false

    @FT-OA-027
    Scenario: Deactivate a nonexistent agreement is rejected
        Given the ops schema exists for agreement management
        When I deactivate agreement ID 999999
        Then the deactivate fails with error containing "does not exist"

    @FT-OA-028
    Scenario: Deactivate blocked by active obligation instances
        Given the ops schema exists for agreement management
        And an existing active obligation agreement named "Has Instances"
        And the agreement has an active obligation instance
        When I deactivate the agreement
        Then the deactivate fails with error containing "active obligation instances"
