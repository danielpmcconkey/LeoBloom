Feature: Ops schema structural constraints

  All scenarios verify database-level enforcement. No application logic.

  # Scope: NOT NULL constraints (user-defined, not DEFAULT-backed), UNIQUE constraints,
  # FK constraints (insert direction), and nullable FK/business-significant fields.
  # Intentionally untested: freeform nullable text/date columns (memo, notes, source,
  # description, etc.) and NOT NULL + DEFAULT columns (is_active, created_at, modified_at).

  # --- obligation_type ---

  @FT-OSC-001
  Scenario: obligation_type name must be unique
    Given an obligation_type "receivable" exists
    When I insert another obligation_type with name "receivable"
    Then the insert is rejected with a UNIQUE violation

  @FT-OSC-002
  Scenario: obligation_type requires a name
    Given the ops schema exists
    When I insert into obligation_type with a null name
    Then the insert is rejected with a NOT NULL violation

  # --- obligation_status ---

  @FT-OSC-003
  Scenario: obligation_status name must be unique
    Given an obligation_status "expected" exists
    When I insert another obligation_status with name "expected"
    Then the insert is rejected with a UNIQUE violation

  @FT-OSC-004
  Scenario: obligation_status requires a name
    Given the ops schema exists
    When I insert into obligation_status with a null name
    Then the insert is rejected with a NOT NULL violation

  # --- cadence ---

  @FT-OSC-005
  Scenario: cadence name must be unique
    Given a cadence "monthly" exists
    When I insert another cadence with name "monthly"
    Then the insert is rejected with a UNIQUE violation

  @FT-OSC-006
  Scenario: cadence requires a name
    Given the ops schema exists
    When I insert into cadence with a null name
    Then the insert is rejected with a NOT NULL violation

  # --- payment_method ---

  @FT-OSC-007
  Scenario: payment_method name must be unique
    Given a payment_method "zelle" exists
    When I insert another payment_method with name "zelle"
    Then the insert is rejected with a UNIQUE violation

  @FT-OSC-008
  Scenario: payment_method requires a name
    Given the ops schema exists
    When I insert into payment_method with a null name
    Then the insert is rejected with a NOT NULL violation

  # --- obligation_agreement ---

  @FT-OSC-009
  Scenario: obligation_agreement requires a name
    Given the ops schema exists
    When I insert into obligation_agreement with a null name
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-010
  Scenario: obligation_agreement requires an obligation_type_id
    Given the ops schema exists
    When I insert into obligation_agreement with a null obligation_type_id
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-011
  Scenario: obligation_agreement obligation_type_id must reference a valid obligation_type
    Given the ops schema exists
    When I insert into obligation_agreement with obligation_type_id 9999
    Then the insert is rejected with a FK violation

  @FT-OSC-012
  Scenario: obligation_agreement requires a cadence_id
    Given the ops schema exists
    When I insert into obligation_agreement with a null cadence_id
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-013
  Scenario: obligation_agreement cadence_id must reference a valid cadence
    Given the ops schema exists
    When I insert into obligation_agreement with cadence_id 9999
    Then the insert is rejected with a FK violation

  @FT-OSC-014
  Scenario: obligation_agreement payment_method_id must reference a valid payment_method
    Given the ops schema exists
    When I insert into obligation_agreement with payment_method_id 9999
    Then the insert is rejected with a FK violation

  @FT-OSC-015
  Scenario: obligation_agreement source_account_id must reference a valid ledger account
    Given the ops schema exists
    When I insert into obligation_agreement with source_account_id 9999
    Then the insert is rejected with a FK violation

  @FT-OSC-016
  Scenario: obligation_agreement dest_account_id must reference a valid ledger account
    Given the ops schema exists
    When I insert into obligation_agreement with dest_account_id 9999
    Then the insert is rejected with a FK violation

  @FT-OSC-017
  Scenario: obligation_agreement amount is nullable
    Given the ops schema exists
    When I insert a valid obligation_agreement with a null amount
    Then the insert succeeds

  # --- obligation_instance ---

  @FT-OSC-018
  Scenario: obligation_instance requires an obligation_agreement_id
    Given the ops schema exists
    When I insert into obligation_instance with a null obligation_agreement_id
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-019
  Scenario: obligation_instance obligation_agreement_id must reference a valid agreement
    Given the ops schema exists
    When I insert into obligation_instance with obligation_agreement_id 9999
    Then the insert is rejected with a FK violation

  @FT-OSC-020
  Scenario: obligation_instance requires a name
    Given the ops schema exists
    When I insert into obligation_instance with a null name
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-021
  Scenario: obligation_instance requires a status_id
    Given the ops schema exists
    When I insert into obligation_instance with a null status_id
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-022
  Scenario: obligation_instance status_id must reference a valid obligation_status
    Given the ops schema exists
    When I insert into obligation_instance with status_id 9999
    Then the insert is rejected with a FK violation

  @FT-OSC-023
  Scenario: obligation_instance requires an expected_date
    Given the ops schema exists
    When I insert into obligation_instance with a null expected_date
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-024
  Scenario: obligation_instance journal_entry_id must reference a valid journal_entry
    Given the ops schema exists
    When I insert into obligation_instance with journal_entry_id 9999
    Then the insert is rejected with a FK violation

  @FT-OSC-025
  Scenario: obligation_instance journal_entry_id is nullable
    Given the ops schema exists
    When I insert a valid obligation_instance with null journal_entry_id
    Then the insert succeeds

  # --- transfer ---

  @FT-OSC-026
  Scenario: transfer requires a from_account_id
    Given the ops schema exists
    When I insert into transfer with a null from_account_id
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-027
  Scenario: transfer from_account_id must reference a valid ledger account
    Given the ops schema exists
    When I insert into transfer with from_account_id 9999
    Then the insert is rejected with a FK violation

  @FT-OSC-028
  Scenario: transfer requires a to_account_id
    Given the ops schema exists
    When I insert into transfer with a null to_account_id
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-029
  Scenario: transfer to_account_id must reference a valid ledger account
    Given the ops schema exists
    When I insert into transfer with to_account_id 9999
    Then the insert is rejected with a FK violation

  @FT-OSC-030
  Scenario: transfer requires an amount
    Given the ops schema exists
    When I insert into transfer with a null amount
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-031
  Scenario: transfer requires a status
    Given the ops schema exists
    When I insert into transfer with a null status
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-032
  Scenario: transfer requires an initiated_date
    Given the ops schema exists
    When I insert into transfer with a null initiated_date
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-033
  Scenario: transfer journal_entry_id must reference a valid journal_entry
    Given the ops schema exists
    When I insert into transfer with journal_entry_id 9999
    Then the insert is rejected with a FK violation

  @FT-OSC-034
  Scenario: transfer journal_entry_id is nullable
    Given the ops schema exists
    When I insert a valid transfer with null journal_entry_id
    Then the insert succeeds

  # --- invoice ---

  @FT-OSC-035
  Scenario: invoice requires a tenant
    Given the ops schema exists
    When I insert into invoice with a null tenant
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-036
  Scenario: invoice requires a fiscal_period_id
    Given the ops schema exists
    When I insert into invoice with a null fiscal_period_id
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-037
  Scenario: invoice fiscal_period_id must reference a valid fiscal_period
    Given the ops schema exists
    When I insert into invoice with fiscal_period_id 9999
    Then the insert is rejected with a FK violation

  @FT-OSC-038
  Scenario: invoice requires rent_amount
    Given the ops schema exists
    When I insert into invoice with a null rent_amount
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-039
  Scenario: invoice requires utility_share
    Given the ops schema exists
    When I insert into invoice with a null utility_share
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-040
  Scenario: invoice requires total_amount
    Given the ops schema exists
    When I insert into invoice with a null total_amount
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-041
  Scenario: invoice tenant and fiscal_period_id must be unique together
    Given an invoice for tenant "Brian" and fiscal_period "2026-03" exists
    When I insert another invoice for tenant "Brian" and fiscal_period "2026-03"
    Then the insert is rejected with a UNIQUE violation
