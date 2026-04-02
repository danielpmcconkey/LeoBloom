Feature: Ledger schema structural constraints

  All scenarios verify database-level enforcement. No application logic.

  # --- account_type ---

  Scenario: account_type requires a name
    Given the ledger schema exists
    When I insert into account_type with a null name
    Then the insert is rejected with a NOT NULL violation

  Scenario: account_type name must be unique
    Given an account_type "asset" exists
    When I insert another account_type with name "asset"
    Then the insert is rejected with a UNIQUE violation

  Scenario: account_type requires a normal_balance
    Given the ledger schema exists
    When I insert into account_type with a null normal_balance
    Then the insert is rejected with a NOT NULL violation

  # --- account ---

  Scenario: account requires a code
    Given the ledger schema exists
    When I insert into account with a null code
    Then the insert is rejected with a NOT NULL violation

  Scenario: account code must be unique
    Given an account with code "1010" exists
    When I insert another account with code "1010"
    Then the insert is rejected with a UNIQUE violation

  Scenario: account requires a name
    Given the ledger schema exists
    When I insert into account with a null name
    Then the insert is rejected with a NOT NULL violation

  Scenario: account requires an account_type_id
    Given the ledger schema exists
    When I insert into account with a null account_type_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: account account_type_id must reference a valid account_type
    Given the ledger schema exists
    When I insert into account with account_type_id 9999
    Then the insert is rejected with a FK violation

  Scenario: account parent_code must reference a valid account code
    Given the ledger schema exists
    When I insert into account with parent_code "XXXX" that does not exist
    Then the insert is rejected with a FK violation

  Scenario: account parent_code is nullable
    Given the ledger schema exists
    When I insert a valid account with a null parent_code
    Then the insert succeeds

  # --- fiscal_period ---

  Scenario: fiscal_period requires a period_key
    Given the ledger schema exists
    When I insert into fiscal_period with a null period_key
    Then the insert is rejected with a NOT NULL violation

  Scenario: fiscal_period period_key must be unique
    Given a fiscal_period "2026-03" exists
    When I insert another fiscal_period with period_key "2026-03"
    Then the insert is rejected with a UNIQUE violation

  Scenario: fiscal_period requires a start_date
    Given the ledger schema exists
    When I insert into fiscal_period with a null start_date
    Then the insert is rejected with a NOT NULL violation

  Scenario: fiscal_period requires an end_date
    Given the ledger schema exists
    When I insert into fiscal_period with a null end_date
    Then the insert is rejected with a NOT NULL violation

  # --- journal_entry ---

  Scenario: journal_entry requires an entry_date
    Given the ledger schema exists
    When I insert into journal_entry with a null entry_date
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry requires a description
    Given the ledger schema exists
    When I insert into journal_entry with a null description
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry requires a fiscal_period_id
    Given the ledger schema exists
    When I insert into journal_entry with a null fiscal_period_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry fiscal_period_id must reference a valid fiscal_period
    Given the ledger schema exists
    When I insert into journal_entry with fiscal_period_id 9999
    Then the insert is rejected with a FK violation

  Scenario: journal_entry voided_at is nullable
    Given the ledger schema exists
    When I insert a valid journal_entry with null voided_at
    Then the insert succeeds

  # --- journal_entry_reference ---

  Scenario: journal_entry_reference requires a journal_entry_id
    Given the ledger schema exists
    When I insert into journal_entry_reference with a null journal_entry_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry_reference journal_entry_id must reference a valid journal_entry
    Given the ledger schema exists
    When I insert into journal_entry_reference with journal_entry_id 9999
    Then the insert is rejected with a FK violation

  Scenario: journal_entry_reference requires reference_type
    Given the ledger schema exists
    When I insert into journal_entry_reference with a null reference_type
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry_reference requires reference_value
    Given the ledger schema exists
    When I insert into journal_entry_reference with a null reference_value
    Then the insert is rejected with a NOT NULL violation

  # --- journal_entry_line ---

  Scenario: journal_entry_line requires a journal_entry_id
    Given the ledger schema exists
    When I insert into journal_entry_line with a null journal_entry_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry_line journal_entry_id must reference a valid journal_entry
    Given the ledger schema exists
    When I insert into journal_entry_line with journal_entry_id 9999
    Then the insert is rejected with a FK violation

  Scenario: journal_entry_line requires an account_id
    Given the ledger schema exists
    When I insert into journal_entry_line with a null account_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry_line account_id must reference a valid account
    Given the ledger schema exists
    When I insert into journal_entry_line with account_id 9999
    Then the insert is rejected with a FK violation

  Scenario: journal_entry_line requires an amount
    Given the ledger schema exists
    When I insert into journal_entry_line with a null amount
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry_line requires an entry_type
    Given the ledger schema exists
    When I insert into journal_entry_line with a null entry_type
    Then the insert is rejected with a NOT NULL violation
