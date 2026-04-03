# Project 029 — Eliminate Lookup Tables, Replace with DU-Backed String Columns

## Objective

Replace four pure lookup tables in the `ops` schema with DU-backed `varchar`
columns on their dependent tables. When this project is done, the
`obligation_agreement` and `obligation_instance` tables store human-readable
string values instead of integer foreign keys, the four lookup tables no longer
exist, the Domain layer's record types use DU fields instead of `int` FK fields,
and all existing tests are updated to reflect the new schema.

---

## Background

Project 004 delivered F# discriminated unions for every enumerated value in the
ops domain: `ObligationDirection`, `InstanceStatus`, `RecurrenceCadence`,
`PaymentMethodType`, and `TransferStatus`. It also delivered record types that
mirror the lookup tables (`ObligationType`, `ObligationStatus`, `Cadence`,
`PaymentMethod`) — simple `{id: int; name: string}` records.

Projects 005 (Post Journal Entry) and 006 (Void Journal Entry) are next on the
critical path. They will build service and persistence layers. If we let them
build write paths that resolve integer FKs against lookup tables, then rip those
tables out later, we're rewriting the Dal, the service layer, and every test
that touches them.

**Executive decision (Dan):** F# DUs are the source of truth. The database
stores human-readable strings. Do it now while the only consumers are Domain
types and raw SQL migrations.

**Source of truth for schema:**
`Projects/Project001-Database/DataModelSpec.md`

---

## Deliverables

### 1. Database Migration

A single migration that performs the following for all four lookup tables:

**Tables to eliminate:**

| Lookup Table | Dependent Table | Old FK Column | New VARCHAR Column | Max Length | NOT NULL? |
|---|---|---|---|---|---|
| `ops.obligation_type` | `ops.obligation_agreement` | `obligation_type_id` | `obligation_type` | `varchar(20)` | YES |
| `ops.cadence` | `ops.obligation_agreement` | `cadence_id` | `cadence` | `varchar(20)` | YES |
| `ops.payment_method` | `ops.obligation_agreement` | `payment_method_id` | `payment_method` | `varchar(30)` | NO (nullable FK stays nullable) |
| `ops.obligation_status` | `ops.obligation_instance` | `status_id` | `status` | `varchar(20)` | YES |

**Migration steps per lookup table:**

1. Add the new `varchar` column with a temporary default
2. Populate it from the existing FK join: `UPDATE ... SET <col> = (SELECT name FROM <lookup> WHERE id = <fk_col>)`
3. Drop the FK constraint
4. Drop the old integer column
5. Drop the lookup table

**Ordering matters:** `obligation_agreement` depends on `obligation_type`,
`cadence`, and `payment_method`. `obligation_instance` depends on
`obligation_status`. The dependent tables' FK columns must be replaced before
the lookup tables are dropped.

**DOWN migration:** Reverses all steps — recreates lookup tables, re-seeds
them, adds integer columns back, populates from string columns, re-adds FK
constraints, drops string columns.

**Migrondi conventions:** Follow the existing naming pattern
(`1712000019000_EliminateLookupTables.sql`) with `MIGRONDI:NAME`,
`MIGRONDI:TIMESTAMP`, `MIGRONDI:UP`, and `MIGRONDI:DOWN` markers.

### 2. Domain Layer Updates (`Ops.fs`)

- **Remove** four record types that mirror eliminated lookup tables:
  `ObligationType`, `ObligationStatus`, `Cadence`, `PaymentMethod`
- **Add** string conversion functions for each DU used as a database column:
  - `ObligationDirection.toString` / `ObligationDirection.fromString`
  - `InstanceStatus.toString` / `InstanceStatus.fromString`
  - `RecurrenceCadence.toString` / `RecurrenceCadence.fromString`
  - `PaymentMethodType.toString` / `PaymentMethodType.fromString`
  - `fromString` functions return `Result<'T, string>` for invalid inputs
  - String values match the database seed data exactly: `"receivable"`,
    `"payable"`, `"expected"`, `"in_flight"`, `"confirmed"`, `"posted"`,
    `"overdue"`, `"skipped"`, `"monthly"`, `"quarterly"`, `"annual"`,
    `"one_time"`, `"autopay_pull"`, `"ach"`, `"zelle"`, `"cheque"`,
    `"bill_pay"`, `"manual"`
- **Update** `ObligationAgreement` record:
  - `obligationTypeId: int` → `obligationType: ObligationDirection`
  - `cadenceId: int` → `cadence: RecurrenceCadence`
  - `paymentMethodId: int option` → `paymentMethod: PaymentMethodType option`
- **Update** `ObligationInstance` record:
  - `statusId: int` → `status: InstanceStatus`

### 3. Domain Tests (`Domain.Tests/Tests.fs`)

Add xUnit tests for the new string conversion functions:

- Round-trip test for each DU: `toString` then `fromString` returns `Ok` with
  original value
- `fromString` with invalid input returns `Error` for each DU
- `fromString` with every valid case for each DU (ensures exhaustive coverage
  of the string-to-DU mapping)

### 4. Dal.Tests Updates

**Feature files — scenarios to REMOVE (lookup table structural constraints):**

| Tag | Scenario | Why |
|---|---|---|
| @FT-OSC-001 | obligation_type name must be unique | Table eliminated |
| @FT-OSC-002 | obligation_type requires a name | Table eliminated |
| @FT-OSC-003 | obligation_status name must be unique | Table eliminated |
| @FT-OSC-004 | obligation_status requires a name | Table eliminated |
| @FT-OSC-005 | cadence name must be unique | Table eliminated |
| @FT-OSC-006 | cadence requires a name | Table eliminated |
| @FT-OSC-007 | payment_method name must be unique | Table eliminated |
| @FT-OSC-008 | payment_method requires a name | Table eliminated |

**Feature files — scenarios to REMOVE (FK constraints to eliminated tables):**

| Tag | Scenario | Why |
|---|---|---|
| @FT-OSC-010 | obligation_agreement requires an obligation_type_id | Column replaced with varchar |
| @FT-OSC-011 | obligation_agreement obligation_type_id must reference a valid obligation_type | FK eliminated |
| @FT-OSC-012 | obligation_agreement requires a cadence_id | Column replaced with varchar |
| @FT-OSC-013 | obligation_agreement cadence_id must reference a valid cadence | FK eliminated |
| @FT-OSC-014 | obligation_agreement payment_method_id must reference a valid payment_method | FK eliminated |
| @FT-OSC-021 | obligation_instance requires a status_id | Column replaced with varchar |
| @FT-OSC-022 | obligation_instance status_id must reference a valid obligation_status | FK eliminated |

**Feature files — scenarios to ADD (NOT NULL on new varchar columns):**

| New Tag | Scenario |
|---|---|
| @FT-OSC-042 | obligation_agreement requires an obligation_type |
| @FT-OSC-043 | obligation_agreement requires a cadence |
| @FT-OSC-044 | obligation_instance requires a status |

(`payment_method` is nullable on `obligation_agreement`, so no NOT NULL test.)

**Feature files — delete restriction scenarios to REMOVE:**

| Tag | Scenario | Why |
|---|---|---|
| @FT-DR-014 | cannot delete obligation_type with dependent agreement | Table eliminated |
| @FT-DR-015 | cannot delete cadence with dependent agreement | Table eliminated |
| @FT-DR-016 | cannot delete payment_method with dependent agreement | Table eliminated |
| @FT-DR-017 | cannot delete obligation_status with dependent instance | Table eliminated |

**Step definitions — updates required:**

- `SharedSteps.fs`: Remove helper functions that query/insert eliminated
  tables (`getValidObligationTypeId`, `getValidCadenceId`,
  `getValidObligationStatusId`, `insertObligationType`, `insertCadence`,
  `insertPaymentMethod`, `insertObligationStatus`). Update
  `insertObligationAgreement` to use string columns instead of FK IDs.
- `OpsStepDefinitions.fs`: Remove all step definitions for the four
  eliminated lookup tables. Update `obligation_agreement` step definitions to
  use varchar columns. Update `obligation_instance` step definitions to use
  `status` varchar column. Add new step definitions for the new NOT NULL
  scenarios.
- `DeleteRestrictionStepDefinitions.fs`: Remove Given steps for DR-014
  through DR-017. Update helper functions that insert `obligation_agreement`
  or `obligation_instance` rows (they currently use FK IDs).

**Impact on surviving tests that insert obligation_agreement/instance rows:**

Surviving scenarios that still need `obligation_agreement` or
`obligation_instance` rows as setup data (FT-OSC-009, FT-OSC-015 through
FT-OSC-025, FT-OSC-017, FT-DR-004, FT-DR-005, FT-DR-018) depend on
`SharedSteps.insertObligationAgreement` and inline SQL in step definitions.
All of these currently use `obligation_type_id`, `cadence_id`,
`payment_method_id`, and `status_id` integer FK columns. After migration,
they must use the replacement varchar columns (`obligation_type`, `cadence`,
`payment_method`, `status`) with string values. The Builder must update
every surviving INSERT statement that touches these two tables.

---

### 5. DataModelSpec Update

Update `Projects/Project001-Database/DataModelSpec.md` to reflect the new
schema. Specifically:

- Remove the four lookup table definitions (`obligation_type`,
  `obligation_status`, `cadence`, `payment_method`)
- Update `obligation_agreement` column definitions: replace `obligation_type_id
  integer NOT NULL REFERENCES ...` with `obligation_type varchar(20) NOT NULL`,
  etc.
- Update `obligation_instance` column definitions: replace `status_id integer
  NOT NULL REFERENCES ...` with `status varchar(20) NOT NULL`
- Remove eliminated tables from any relationship diagrams or FK listings

The DataModelSpec is cited as the source of truth for schema — it must stay
accurate after migration.

---

## What Does NOT Change

- `ledger` schema — completely untouched. `account_type` stays (it has
  `normal_balance`, making it 3NF, not a pure lookup).
- `transfer.status` — already a `varchar` column storing `'initiated'`/
  `'confirmed'`. No lookup table exists. No change. `TransferStatus`
  toString/fromString conversion functions are NOT needed — the `Transfer`
  record already uses `status: TransferStatus` as a DU field (Ops.fs line 84),
  and there is no integer-to-DU migration to perform.
- `journal_entry_line.entry_type` — already a `varchar(6)` storing `'debit'`/
  `'credit'`. No change.
- `ops.invoice` and `ops.transfer` tables — no FK columns reference eliminated
  lookup tables.
- `Ledger.fs` domain types — untouched.
- Domain.Tests existing validation tests — untouched.
- **No CHECK constraints on new varchar columns.** Database-level value
  validation is explicitly out of scope (executive decision). The Domain
  layer's `fromString` functions are the validation boundary. Invalid strings
  at the database level are a write-layer concern (Project 028).

---

## Dependencies

- **Project 004 (Done):** DU types already exist in `Ops.fs`.
- **Blocks 005, 006:** Critical path updated: 004 → 029 → 005.

---

## Acceptance Criteria

1. Migration runs successfully against `leobloom_dev` — all four lookup tables
   are dropped, dependent tables have varchar columns.
2. Migration DOWN section reverses cleanly — lookup tables recreated, FK columns
   restored.
3. `Ops.fs` has no record types for eliminated lookup tables.
4. `Ops.fs` has `toString`/`fromString` functions for all four DUs.
5. `ObligationAgreement` and `ObligationInstance` records use DU fields, not
   int FK fields.
6. `fromString` returns `Error` for invalid inputs.
7. All new domain tests pass.
8. Feature files updated — eliminated scenarios removed, new NOT NULL scenarios
   added.
9. All step definitions updated — no references to eliminated tables or FK
   columns.
10. `dotnet test` passes for the entire solution (Domain.Tests + Dal.Tests).
11. `dotnet build` succeeds with no warnings from modified projects.
