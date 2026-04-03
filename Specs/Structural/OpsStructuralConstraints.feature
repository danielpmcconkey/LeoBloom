Feature: Ops schema structural constraints

  All scenarios verify database-level enforcement. No application logic.

  # Scope: NOT NULL constraints (user-defined, not DEFAULT-backed), UNIQUE constraints,
  # FK constraints (insert direction), and nullable FK/business-significant fields.
  # Intentionally untested: freeform nullable text/date columns (memo, notes, source,
  # description, etc.) and NOT NULL + DEFAULT columns (is_active, created_at, modified_at).

  # --- obligation_agreement ---

  @FT-OSC-009
  Scenario: obligation_agreement requires a name
    Given the ops schema exists
    When I insert into obligation_agreement with a null name
    Then the insert is rejected with a NOT NULL violation

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

  # --- New NOT NULL constraints for varchar columns replacing lookup FKs ---

  @FT-OSC-042
  Scenario: obligation_agreement requires an obligation_type
    Given the ops schema exists
    When I insert into obligation_agreement with a null obligation_type
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-043
  Scenario: obligation_agreement requires a cadence
    Given the ops schema exists
    When I insert into obligation_agreement with a null cadence
    Then the insert is rejected with a NOT NULL violation

  @FT-OSC-044
  Scenario: obligation_instance requires a status
    Given the ops schema exists
    When I insert into obligation_instance with a null status
    Then the insert is rejected with a NOT NULL violation
