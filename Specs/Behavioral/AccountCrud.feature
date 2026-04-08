Feature: Account CRUD
    Service-level behavioral specs for creating, updating, and deactivating
    chart-of-accounts entries. All scenarios exercise the service layer.
    Structural constraints (FK, unique index) are covered separately in
    structural specs; these scenarios verify the service rejects invalid
    inputs with meaningful error messages before any DB round-trip where
    possible.

    Background:
        Given the ledger schema exists for account management

    # --- Create Happy Path ---

    @FT-AC-001
    Scenario: Creating an account with valid data succeeds
        When I create an account with code "crud-1010", name "Test Cash", and type asset
        Then the account is created with a valid id, is_active true, and the given code and name

    # --- Create Validation ---

    @FT-AC-002
    Scenario: Creating an account with an invalid account type is rejected
        When I create an account with code "crud-1020", name "Bad Type Account", and type_id 99999
        Then the account creation fails with error containing "account type"

    @FT-AC-003
    Scenario: Creating an account with a duplicate code is rejected
        Given a crud-test active account with code "crud-1030" and name "Original"
        When I create an account with code "crud-1030", name "Duplicate", and type asset
        Then the account creation fails with error containing "crud-1030"

    @FT-AC-004
    Scenario: Creating an account with an invalid parent_id is rejected
        When I create an account with code "crud-1040", name "Orphan Account", type asset, and parent_id 99999
        Then the account creation fails with error containing "parent"

    @FT-AC-005
    Scenario: Creating an account under an inactive parent is rejected
        Given a crud-test inactive account with code "crud-1050" and name "Inactive Parent"
        When I create an account with code "crud-1051", name "Child Of Inactive", type asset, and parent "crud-1050"
        Then the account creation fails with error containing "inactive"

    # --- Update ---

    @FT-AC-006
    Scenario: Updating an account name succeeds and round-trips
        Given a crud-test active account with code "crud-2010" and name "Original Name"
        When I update account "crud-2010" name to "Updated Name"
        Then the account "crud-2010" name is "Updated Name"

    # --- Deactivate ---

    @FT-AC-007
    Scenario: Deactivating a leaf account succeeds
        Given a crud-test active account with code "crud-3010" and name "Leaf Account"
        When I deactivate account "crud-3010"
        Then account "crud-3010" is_active is false

    @FT-AC-008
    Scenario: Deactivating an account with active child accounts is rejected
        Given a crud-test active account with code "crud-4010" and name "Parent Account"
        And a crud-test active account with code "crud-4011" and name "Child Account" under parent "crud-4010"
        When I deactivate account "crud-4010"
        Then the account deactivation fails with error containing "children"

    @FT-AC-009
    Scenario: Deactivating an account with posted journal entries succeeds
        Given a crud-test active account with code "crud-5010" and name "Posted Account" of type asset
        And a crud-test active account with code "crud-5011" and name "Posted Contra" of type revenue
        And a crud-test open fiscal period
        And a journal entry posted to accounts "crud-5010" and "crud-5011"
        When I deactivate account "crud-5010"
        Then account "crud-5010" is_active is false

    # --- Create with SubType ---

    @FT-AC-010
    Scenario: Creating an account with a valid subtype succeeds and persists the subtype
        When I create an account with code "crud-6010", name "Cash Account", type asset, and subtype "Cash"
        Then the account is created with subtype Cash

    @FT-AC-011
    Scenario: Creating an account with an invalid subtype for its type is rejected
        When I create an account with code "crud-6020", name "Bad Subtype Account", type expense, and subtype "Cash"
        Then the account creation fails with error containing "subtype"
