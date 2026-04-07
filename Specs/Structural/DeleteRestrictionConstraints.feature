Feature: ON DELETE RESTRICT constraints

  All scenarios verify that ON DELETE RESTRICT prevents deletion of parent rows
  that have dependent children. Each FK relationship gets its own test because
  a migration could change any single FK to CASCADE independently.

  # Scope: Every FK with ON DELETE RESTRICT across both schemas.
  # Test pattern: insert parent + child within a transaction, attempt to DELETE
  # the parent, assert FK violation (SQLSTATE 23503), rollback.

  # --- ledger.account_type as parent ---

  @FT-DR-001
  Scenario: cannot delete account_type with dependent account
    Given an account_type with a dependent account exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  # --- ledger.account as parent ---

  @FT-DR-002
  Scenario: cannot delete account with dependent child account via parent_id
    Given an account with a dependent child account exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  @FT-DR-003
  Scenario: cannot delete account with dependent journal_entry_line
    Given an account with a dependent journal_entry_line exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  @FT-DR-004
  Scenario: cannot delete account with dependent obligation_agreement source
    Given an account with a dependent obligation_agreement source exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  @FT-DR-005
  Scenario: cannot delete account with dependent obligation_agreement dest
    Given an account with a dependent obligation_agreement dest exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  @FT-DR-006
  Scenario: cannot delete account with dependent transfer from
    Given an account with a dependent transfer from exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  @FT-DR-007
  Scenario: cannot delete account with dependent transfer to
    Given an account with a dependent transfer to exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  # --- ledger.fiscal_period as parent ---

  @FT-DR-008
  Scenario: cannot delete fiscal_period with dependent journal_entry
    Given a fiscal_period with a dependent journal_entry exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  @FT-DR-009
  Scenario: cannot delete fiscal_period with dependent invoice
    Given a fiscal_period with a dependent invoice exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  # --- ledger.journal_entry as parent ---

  @FT-DR-010
  Scenario: cannot delete journal_entry with dependent reference
    Given a journal_entry with a dependent reference exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  @FT-DR-011
  Scenario: cannot delete journal_entry with dependent line
    Given a journal_entry with a dependent line exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  @FT-DR-012
  Scenario: cannot delete journal_entry with dependent obligation_instance
    Given a journal_entry with a dependent obligation_instance exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  @FT-DR-013
  Scenario: cannot delete journal_entry with dependent transfer
    Given a journal_entry with a dependent transfer exists
    When I delete the parent record
    Then the delete is rejected with a FK violation

  # --- ops.obligation_agreement as parent ---

  @FT-DR-018
  Scenario: cannot delete obligation_agreement with dependent instance
    Given an obligation_agreement with a dependent instance exists
    When I delete the parent record
    Then the delete is rejected with a FK violation
