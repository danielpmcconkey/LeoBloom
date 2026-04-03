# Project 001 — Test Results (Retroactive FT Mapping)

Retroactive mapping of Project 1 structural constraint scenarios to their
permanent `@FT-*` Feature IDs, assigned in Project 3.

**Date:** 2026-04-03
**Result:** 88/88 passed
**Runner:** `dotnet test` (TickSpec + xUnit, .NET 10)
**Database:** `leobloom_dev`

---

## Ledger Structural Constraints (LSC)

| FT ID | Scenario | Result |
|---|---|---|
| @FT-LSC-001 | account_type requires a name | Pass |
| @FT-LSC-002 | account_type name must be unique | Pass |
| @FT-LSC-003 | account_type requires a normal_balance | Pass |
| @FT-LSC-004 | account requires a code | Pass |
| @FT-LSC-005 | account code must be unique | Pass |
| @FT-LSC-006 | account requires a name | Pass |
| @FT-LSC-007 | account requires an account_type_id | Pass |
| @FT-LSC-008 | account account_type_id must reference a valid account_type | Pass |
| @FT-LSC-009 | account parent_code must reference a valid account code | Pass |
| @FT-LSC-010 | account parent_code is nullable | Pass |
| @FT-LSC-011 | fiscal_period requires a period_key | Pass |
| @FT-LSC-012 | fiscal_period period_key must be unique | Pass |
| @FT-LSC-013 | fiscal_period requires a start_date | Pass |
| @FT-LSC-014 | fiscal_period requires an end_date | Pass |
| @FT-LSC-015 | journal_entry requires an entry_date | Pass |
| @FT-LSC-016 | journal_entry requires a description | Pass |
| @FT-LSC-017 | journal_entry requires a fiscal_period_id | Pass |
| @FT-LSC-018 | journal_entry fiscal_period_id must reference a valid fiscal_period | Pass |
| @FT-LSC-019 | journal_entry voided_at is nullable | Pass |
| @FT-LSC-020 | journal_entry_reference requires a journal_entry_id | Pass |
| @FT-LSC-021 | journal_entry_reference journal_entry_id must reference a valid journal_entry | Pass |
| @FT-LSC-022 | journal_entry_reference requires reference_type | Pass |
| @FT-LSC-023 | journal_entry_reference requires reference_value | Pass |
| @FT-LSC-024 | journal_entry_line requires a journal_entry_id | Pass |
| @FT-LSC-025 | journal_entry_line journal_entry_id must reference a valid journal_entry | Pass |
| @FT-LSC-026 | journal_entry_line requires an account_id | Pass |
| @FT-LSC-027 | journal_entry_line account_id must reference a valid account | Pass |
| @FT-LSC-028 | journal_entry_line requires an amount | Pass |
| @FT-LSC-029 | journal_entry_line requires an entry_type | Pass |

## Ops Structural Constraints (OSC)

| FT ID | Scenario | Result |
|---|---|---|
| @FT-OSC-001 | obligation_type name must be unique | Pass |
| @FT-OSC-002 | obligation_type requires a name | Pass |
| @FT-OSC-003 | obligation_status name must be unique | Pass |
| @FT-OSC-004 | obligation_status requires a name | Pass |
| @FT-OSC-005 | cadence name must be unique | Pass |
| @FT-OSC-006 | cadence requires a name | Pass |
| @FT-OSC-007 | payment_method name must be unique | Pass |
| @FT-OSC-008 | payment_method requires a name | Pass |
| @FT-OSC-009 | obligation_agreement requires a name | Pass |
| @FT-OSC-010 | obligation_agreement requires an obligation_type_id | Pass |
| @FT-OSC-011 | obligation_agreement obligation_type_id must reference a valid obligation_type | Pass |
| @FT-OSC-012 | obligation_agreement requires a cadence_id | Pass |
| @FT-OSC-013 | obligation_agreement cadence_id must reference a valid cadence | Pass |
| @FT-OSC-014 | obligation_agreement payment_method_id must reference a valid payment_method | Pass |
| @FT-OSC-015 | obligation_agreement source_account_id must reference a valid ledger account | Pass |
| @FT-OSC-016 | obligation_agreement dest_account_id must reference a valid ledger account | Pass |
| @FT-OSC-017 | obligation_agreement amount is nullable | Pass |
| @FT-OSC-018 | obligation_instance requires an obligation_agreement_id | Pass |
| @FT-OSC-019 | obligation_instance obligation_agreement_id must reference a valid agreement | Pass |
| @FT-OSC-020 | obligation_instance requires a name | Pass |
| @FT-OSC-021 | obligation_instance requires a status_id | Pass |
| @FT-OSC-022 | obligation_instance status_id must reference a valid obligation_status | Pass |
| @FT-OSC-023 | obligation_instance requires an expected_date | Pass |
| @FT-OSC-024 | obligation_instance journal_entry_id must reference a valid journal_entry | Pass |
| @FT-OSC-025 | obligation_instance journal_entry_id is nullable | Pass |
| @FT-OSC-026 | transfer requires a from_account_id | Pass |
| @FT-OSC-027 | transfer from_account_id must reference a valid ledger account | Pass |
| @FT-OSC-028 | transfer requires a to_account_id | Pass |
| @FT-OSC-029 | transfer to_account_id must reference a valid ledger account | Pass |
| @FT-OSC-030 | transfer requires an amount | Pass |
| @FT-OSC-031 | transfer requires a status | Pass |
| @FT-OSC-032 | transfer requires an initiated_date | Pass |
| @FT-OSC-033 | transfer journal_entry_id must reference a valid journal_entry | Pass |
| @FT-OSC-034 | transfer journal_entry_id is nullable | Pass |
| @FT-OSC-035 | invoice requires a tenant | Pass |
| @FT-OSC-036 | invoice requires a fiscal_period_id | Pass |
| @FT-OSC-037 | invoice fiscal_period_id must reference a valid fiscal_period | Pass |
| @FT-OSC-038 | invoice requires rent_amount | Pass |
| @FT-OSC-039 | invoice requires utility_share | Pass |
| @FT-OSC-040 | invoice requires total_amount | Pass |
| @FT-OSC-041 | invoice tenant and fiscal_period_id must be unique together | Pass |

## Delete Restriction Constraints (DR)

| FT ID | Scenario | Result |
|---|---|---|
| @FT-DR-001 | cannot delete account_type with dependent account | Pass |
| @FT-DR-002 | cannot delete account with dependent child account via parent_code | Pass |
| @FT-DR-003 | cannot delete account with dependent journal_entry_line | Pass |
| @FT-DR-004 | cannot delete account with dependent obligation_agreement source | Pass |
| @FT-DR-005 | cannot delete account with dependent obligation_agreement dest | Pass |
| @FT-DR-006 | cannot delete account with dependent transfer from | Pass |
| @FT-DR-007 | cannot delete account with dependent transfer to | Pass |
| @FT-DR-008 | cannot delete fiscal_period with dependent journal_entry | Pass |
| @FT-DR-009 | cannot delete fiscal_period with dependent invoice | Pass |
| @FT-DR-010 | cannot delete journal_entry with dependent reference | Pass |
| @FT-DR-011 | cannot delete journal_entry with dependent line | Pass |
| @FT-DR-012 | cannot delete journal_entry with dependent obligation_instance | Pass |
| @FT-DR-013 | cannot delete journal_entry with dependent transfer | Pass |
| @FT-DR-014 | cannot delete obligation_type with dependent agreement | Pass |
| @FT-DR-015 | cannot delete cadence with dependent agreement | Pass |
| @FT-DR-016 | cannot delete payment_method with dependent agreement | Pass |
| @FT-DR-017 | cannot delete obligation_status with dependent instance | Pass |
| @FT-DR-018 | cannot delete obligation_agreement with dependent instance | Pass |
