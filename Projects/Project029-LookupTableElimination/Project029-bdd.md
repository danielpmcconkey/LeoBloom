# Project 029 — BDD Acceptance Criteria

BDD IDs are project-scoped and ephemeral. They start at 001 for each project
and live only in this document.

---

## Deliverable 1: Database Migration

**BDD-001** — After running the UP migration, `ops.obligation_type` table does
not exist.

**BDD-002** — After running the UP migration, `ops.obligation_status` table
does not exist.

**BDD-003** — After running the UP migration, `ops.cadence` table does not
exist.

**BDD-004** — After running the UP migration, `ops.payment_method` table does
not exist.

**BDD-005** — After running the UP migration, `ops.obligation_agreement` has a
column `obligation_type varchar(20) NOT NULL`.

**BDD-006** — After running the UP migration, `ops.obligation_agreement` has a
column `cadence varchar(20) NOT NULL`.

**BDD-007** — After running the UP migration, `ops.obligation_agreement` has a
column `payment_method varchar(30)` (nullable).

**BDD-008** — After running the UP migration, `ops.obligation_instance` has a
column `status varchar(20) NOT NULL`.

**BDD-009** — After running the UP migration, `ops.obligation_agreement` does
NOT have columns `obligation_type_id`, `cadence_id`, or `payment_method_id`.

**BDD-010** — After running the UP migration, `ops.obligation_instance` does
NOT have column `status_id`.

**BDD-011** — Existing data is preserved: every `obligation_agreement` row has
its `obligation_type` set to the name from the former lookup row (e.g.,
`'receivable'` or `'payable'`).

**BDD-012** — Existing data is preserved: every `obligation_agreement` row has
its `cadence` set to the name from the former lookup row.

**BDD-013** — Existing data is preserved: every `obligation_agreement` row has
its `payment_method` set to the name from the former lookup row (or NULL if
the original FK was NULL).

**BDD-014** — Existing data is preserved: every `obligation_instance` row has
its `status` set to the name from the former lookup row.

**BDD-015** — The DOWN migration restores all four lookup tables with their
original seed data.

**BDD-016** — The DOWN migration restores the integer FK columns on
`obligation_agreement` and `obligation_instance` with correct values.

**BDD-017** — The DOWN migration removes the varchar columns added by UP.

**BDD-018** — `dotnet run --project Src/LeoBloom.Migrations -- up` succeeds
without errors.

---

## Deliverable 2: Domain Layer Updates (`Ops.fs`)

**BDD-019** — `Ops.fs` does NOT define record types `ObligationType`,
`ObligationStatus`, `Cadence`, or `PaymentMethod`.

**BDD-020** — `Ops.fs` defines a function (or module function)
`ObligationDirection.toString` that converts `Receivable` → `"receivable"`
and `Payable` → `"payable"`.

**BDD-021** — `Ops.fs` defines a function `ObligationDirection.fromString`
that converts `"receivable"` → `Ok Receivable` and `"payable"` →
`Ok Payable`.

**BDD-022** — `ObligationDirection.fromString` returns `Error` with a
descriptive message for any input other than `"receivable"` or `"payable"`.

**BDD-023** — `Ops.fs` defines `InstanceStatus.toString` converting each case:
`Expected` → `"expected"`, `InFlight` → `"in_flight"`, `Confirmed` →
`"confirmed"`, `Posted` → `"posted"`, `Overdue` → `"overdue"`, `Skipped` →
`"skipped"`.

**BDD-024** — `Ops.fs` defines `InstanceStatus.fromString` converting each
valid string back to `Ok <case>`.

**BDD-025** — `InstanceStatus.fromString` returns `Error` for invalid inputs.

**BDD-026** — `Ops.fs` defines `RecurrenceCadence.toString` converting each
case: `Monthly` → `"monthly"`, `Quarterly` → `"quarterly"`, `Annual` →
`"annual"`, `OneTime` → `"one_time"`.

**BDD-027** — `Ops.fs` defines `RecurrenceCadence.fromString` converting each
valid string back to `Ok <case>`.

**BDD-028** — `RecurrenceCadence.fromString` returns `Error` for invalid
inputs.

**BDD-029** — `Ops.fs` defines `PaymentMethodType.toString` converting each
case: `AutopayPull` → `"autopay_pull"`, `Ach` → `"ach"`, `Zelle` →
`"zelle"`, `Cheque` → `"cheque"`, `BillPay` → `"bill_pay"`, `Manual` →
`"manual"`.

**BDD-030** — `Ops.fs` defines `PaymentMethodType.fromString` converting each
valid string back to `Ok <case>`.

**BDD-031** — `PaymentMethodType.fromString` returns `Error` for invalid
inputs.

**BDD-032** — `ObligationAgreement` record has field `obligationType:
ObligationDirection` (not `obligationTypeId: int`).

**BDD-033** — `ObligationAgreement` record has field `cadence:
RecurrenceCadence` (not `cadenceId: int`).

**BDD-034** — `ObligationAgreement` record has field `paymentMethod:
PaymentMethodType option` (not `paymentMethodId: int option`).

**BDD-035** — `ObligationInstance` record has field `status: InstanceStatus`
(not `statusId: int`).

---

## Deliverable 3: Domain Tests (`Domain.Tests/Tests.fs`)

**BDD-036** — A test verifies `ObligationDirection.toString` then
`ObligationDirection.fromString` round-trips for both cases.

**BDD-037** — A test verifies `ObligationDirection.fromString "invalid"`
returns `Error`.

**BDD-038** — A test verifies `InstanceStatus.toString` then
`InstanceStatus.fromString` round-trips for all six cases.

**BDD-039** — A test verifies `InstanceStatus.fromString "invalid"` returns
`Error`.

**BDD-040** — A test verifies `RecurrenceCadence.toString` then
`RecurrenceCadence.fromString` round-trips for all four cases.

**BDD-041** — A test verifies `RecurrenceCadence.fromString "invalid"` returns
`Error`.

**BDD-042** — A test verifies `PaymentMethodType.toString` then
`PaymentMethodType.fromString` round-trips for all six cases.

**BDD-043** — A test verifies `PaymentMethodType.fromString "invalid"` returns
`Error`.

**BDD-044** — All new tests use plain xUnit attributes (`[<Fact>]` or
`[<Theory>]`). No TickSpec, no Gherkin.

**BDD-045** — `dotnet test` on `LeoBloom.Domain.Tests` passes with all tests
green (existing + new).

---

## Deliverable 4: Dal.Tests Updates

**BDD-046** — Feature file `OpsStructuralConstraints.feature` does NOT contain
scenarios tagged @FT-OSC-001 through @FT-OSC-008.

**BDD-047** — Feature file `OpsStructuralConstraints.feature` does NOT contain
scenarios tagged @FT-OSC-010, @FT-OSC-011, @FT-OSC-012, @FT-OSC-013,
@FT-OSC-014, @FT-OSC-021, @FT-OSC-022.

**BDD-048** — Feature file `OpsStructuralConstraints.feature` contains a new
scenario @FT-OSC-042: obligation_agreement requires an obligation_type (NOT
NULL on varchar column).

**BDD-049** — Feature file `OpsStructuralConstraints.feature` contains a new
scenario @FT-OSC-043: obligation_agreement requires a cadence (NOT NULL on
varchar column).

**BDD-050** — Feature file `OpsStructuralConstraints.feature` contains a new
scenario @FT-OSC-044: obligation_instance requires a status (NOT NULL on
varchar column).

**BDD-051** — Feature file `DeleteRestrictionConstraints.feature` does NOT
contain scenarios tagged @FT-DR-014, @FT-DR-015, @FT-DR-016, @FT-DR-017.

**BDD-052** — `OpsStepDefinitions.fs` does NOT contain step definitions for
eliminated lookup tables (no references to `obligation_type` table inserts,
`obligation_status` table inserts, `cadence` table inserts, `payment_method`
table inserts).

**BDD-053** — `SharedSteps.fs` does NOT contain `getValidObligationTypeId`,
`getValidCadenceId`, `getValidObligationStatusId`, `insertObligationType`,
`insertCadence`, `insertPaymentMethod`, or `insertObligationStatus` helper
functions.

**BDD-054** — `SharedSteps.insertObligationAgreement` uses varchar columns
(`obligation_type`, `cadence`, `payment_method`) with string values instead
of integer FK columns.

**BDD-055** — All surviving obligation_agreement and obligation_instance
INSERT statements in step definitions use varchar columns, not integer FK
columns.

**BDD-056** — `dotnet test` on `LeoBloom.Dal.Tests` passes with all
surviving + new scenarios green.

---

## Deliverable 5: DataModelSpec Update

**BDD-057** — `DataModelSpec.md` does NOT contain table definitions for
`obligation_type`, `obligation_status`, `cadence`, or `payment_method`.

**BDD-058** — `DataModelSpec.md` `obligation_agreement` table definition shows
`obligation_type varchar(20) NOT NULL`, `cadence varchar(20) NOT NULL`, and
`payment_method varchar(30)` instead of the former integer FK columns.

**BDD-059** — `DataModelSpec.md` `obligation_instance` table definition shows
`status varchar(20) NOT NULL` instead of `status_id integer NOT NULL
REFERENCES ...`.

**BDD-060** — `DataModelSpec.md` does NOT list FK relationships from
`obligation_agreement` or `obligation_instance` to the eliminated lookup
tables.

---

## Cross-cutting

**BDD-061** — `dotnet build` succeeds for the entire solution with no warnings
from `LeoBloom.Domain`, `LeoBloom.Domain.Tests`, or `LeoBloom.Dal.Tests`.

**BDD-062** — `dotnet test` succeeds for the entire solution — all Domain.Tests
and Dal.Tests pass.
