Feature: Portfolio schema structural constraints

  All scenarios verify database-level enforcement. No application logic.

  # Scope: NOT NULL constraints on core required columns, UNIQUE constraints,
  # FK constraints (insert direction), and the position composite UNIQUE constraint.
  # Fund symbol-as-PK enforcement is included because it is a deliberate design
  # choice (not serial) with a behavioral observable: duplicate symbols are rejected.

  # --- tax_bucket ---

  @FT-PSC-001
  Scenario: tax_bucket requires a name
    Given the portfolio schema exists
    When I insert into portfolio.tax_bucket with a null name
    Then the insert is rejected with a NOT NULL violation

  @FT-PSC-002
  Scenario: tax_bucket name must be unique
    Given a portfolio.tax_bucket "Tax deferred" exists
    When I insert another portfolio.tax_bucket with name "Tax deferred"
    Then the insert is rejected with a UNIQUE violation

  # --- account_group ---

  @FT-PSC-003
  Scenario: account_group requires a name
    Given the portfolio schema exists
    When I insert into portfolio.account_group with a null name
    Then the insert is rejected with a NOT NULL violation

  @FT-PSC-004
  Scenario: account_group name must be unique
    Given a portfolio.account_group "Retirement 401(k)" exists
    When I insert another portfolio.account_group with name "Retirement 401(k)"
    Then the insert is rejected with a UNIQUE violation

  # --- investment_account ---

  @FT-PSC-005
  Scenario: investment_account requires a name
    Given a valid portfolio.tax_bucket exists
    And a valid portfolio.account_group exists
    When I insert into portfolio.investment_account with a null name
    Then the insert is rejected with a NOT NULL violation

  @FT-PSC-006
  Scenario: investment_account tax_bucket_id must reference a valid tax_bucket
    Given the portfolio schema exists
    When I insert into portfolio.investment_account with tax_bucket_id 9999
    Then the insert is rejected with a FK violation

  @FT-PSC-007
  Scenario: investment_account account_group_id must reference a valid account_group
    Given the portfolio schema exists
    When I insert into portfolio.investment_account with account_group_id 9999
    Then the insert is rejected with a FK violation

  # --- fund ---

  @FT-PSC-008
  Scenario: fund symbol serves as the primary key — duplicate symbols are rejected
    Given a portfolio.fund with symbol "VTI" exists
    When I insert another portfolio.fund with symbol "VTI"
    Then the insert is rejected with a PK violation

  @FT-PSC-009
  Scenario: fund requires a name
    Given the portfolio schema exists
    When I insert into portfolio.fund with a null name
    Then the insert is rejected with a NOT NULL violation

  # --- position ---

  @FT-PSC-010
  Scenario: position rejects duplicate (investment_account, symbol, position_date) combination
    Given a portfolio.investment_account exists
    And a portfolio.fund with symbol "VTI" exists
    And a portfolio.position for that account, symbol "VTI", dated 2026-01-01 exists
    When I insert another portfolio.position for the same account, symbol "VTI", dated 2026-01-01
    Then the insert is rejected with a UNIQUE violation

  @FT-PSC-011
  Scenario: position investment_account_id must reference a valid investment_account
    Given the portfolio schema exists
    When I insert into portfolio.position with investment_account_id 9999
    Then the insert is rejected with a FK violation

  @FT-PSC-012
  Scenario: position symbol must reference a valid fund
    Given the portfolio schema exists
    When I insert into portfolio.position with symbol "FAKE"
    Then the insert is rejected with a FK violation

  @FT-PSC-013
  Scenario: position requires a position_date
    Given a valid portfolio.investment_account exists
    And a portfolio.fund with symbol "VTI" exists
    When I insert into portfolio.position with a null position_date
    Then the insert is rejected with a NOT NULL violation
