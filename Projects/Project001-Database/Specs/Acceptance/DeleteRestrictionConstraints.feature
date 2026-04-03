Feature: ON DELETE RESTRICT constraints

  All scenarios verify that ON DELETE RESTRICT prevents deletion of parent rows
  that have dependent children. Each FK relationship gets its own test because
  a migration could change any single FK to CASCADE independently.

  # Scope: Every FK with ON DELETE RESTRICT across both schemas.
  # Test pattern: insert parent + child within a transaction, attempt to DELETE
  # the parent, assert FK violation (SQLSTATE 23503), rollback.

  # --- ledger.account_type as parent ---

  Scenario: cannot delete account_type with dependent account
    Given an account_type with a dependent account exists
    When I delete the parent account_type
    Then the delete is rejected with a FK violation

  # --- ledger.account as parent ---

  Scenario: cannot delete account with dependent child account via parent_code
    Given an account with a dependent child account exists
    When I delete the parent account
    Then the delete is rejected with a FK violation

  Scenario: cannot delete account with dependent journal_entry_line
    Given an account with a dependent journal_entry_line exists
    When I delete the account referenced by journal_entry_line
    Then the delete is rejected with a FK violation

  Scenario: cannot delete account with dependent obligation_agreement source
    Given an account with a dependent obligation_agreement source exists
    When I delete the account referenced as source_account
    Then the delete is rejected with a FK violation

  Scenario: cannot delete account with dependent obligation_agreement dest
    Given an account with a dependent obligation_agreement dest exists
    When I delete the account referenced as dest_account
    Then the delete is rejected with a FK violation

  Scenario: cannot delete account with dependent transfer from
    Given an account with a dependent transfer from exists
    When I delete the account referenced as from_account
    Then the delete is rejected with a FK violation

  Scenario: cannot delete account with dependent transfer to
    Given an account with a dependent transfer to exists
    When I delete the account referenced as to_account
    Then the delete is rejected with a FK violation

  # --- ledger.fiscal_period as parent ---

  Scenario: cannot delete fiscal_period with dependent journal_entry
    Given a fiscal_period with a dependent journal_entry exists
    When I delete the parent fiscal_period
    Then the delete is rejected with a FK violation

  Scenario: cannot delete fiscal_period with dependent invoice
    Given a fiscal_period with a dependent invoice exists
    When I delete the parent fiscal_period
    Then the delete is rejected with a FK violation

  # --- ledger.journal_entry as parent ---

  Scenario: cannot delete journal_entry with dependent reference
    Given a journal_entry with a dependent reference exists
    When I delete the parent journal_entry
    Then the delete is rejected with a FK violation

  Scenario: cannot delete journal_entry with dependent line
    Given a journal_entry with a dependent line exists
    When I delete the parent journal_entry
    Then the delete is rejected with a FK violation

  Scenario: cannot delete journal_entry with dependent obligation_instance
    Given a journal_entry with a dependent obligation_instance exists
    When I delete the parent journal_entry
    Then the delete is rejected with a FK violation

  Scenario: cannot delete journal_entry with dependent transfer
    Given a journal_entry with a dependent transfer exists
    When I delete the parent journal_entry
    Then the delete is rejected with a FK violation

  # --- ops.obligation_type as parent ---

  Scenario: cannot delete obligation_type with dependent agreement
    Given an obligation_type with a dependent agreement exists
    When I delete the parent obligation_type
    Then the delete is rejected with a FK violation

  # --- ops.cadence as parent ---

  Scenario: cannot delete cadence with dependent agreement
    Given a cadence with a dependent agreement exists
    When I delete the parent cadence
    Then the delete is rejected with a FK violation

  # --- ops.payment_method as parent ---

  Scenario: cannot delete payment_method with dependent agreement
    Given a payment_method with a dependent agreement exists
    When I delete the parent payment_method
    Then the delete is rejected with a FK violation

  # --- ops.obligation_status as parent ---

  Scenario: cannot delete obligation_status with dependent instance
    Given an obligation_status with a dependent instance exists
    When I delete the parent obligation_status
    Then the delete is rejected with a FK violation

  # --- ops.obligation_agreement as parent ---

  Scenario: cannot delete obligation_agreement with dependent instance
    Given an obligation_agreement with a dependent instance exists
    When I delete the parent obligation_agreement
    Then the delete is rejected with a FK violation
