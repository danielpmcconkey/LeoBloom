# Project 029 -- Test Results

**Date:** 2026-04-03
**Commit:** pre-merge
**Result:** 62/62 verified

## Verification Method

Every BDD criterion was checked against the actual repo state by the Governor.
Tests were executed independently via `dotnet test` and `dotnet build`. File
contents were read and grepped directly -- no Builder or Reviewer claims were
trusted.

### Test Execution Summary

| Test Project | Total | Passed | Failed |
|---|---|---|---|
| LeoBloom.Domain.Tests | 36 | 36 | 0 |
| LeoBloom.Dal.Tests | 72 | 72 | 0 |
| Solution build | -- | 0 warnings | 0 errors |

---

## BDD -> Verification Mapping

### Deliverable 1: Database Migration

| BDD ID | Description | Verified | Evidence |
|---|---|---|---|
| BDD-001 | UP drops `ops.obligation_type` | Yes | Migration L33: `DROP TABLE ops.obligation_type;` |
| BDD-002 | UP drops `ops.obligation_status` | Yes | Migration L34: `DROP TABLE ops.obligation_status;` |
| BDD-003 | UP drops `ops.cadence` | Yes | Migration L35: `DROP TABLE ops.cadence;` |
| BDD-004 | UP drops `ops.payment_method` | Yes | Migration L36: `DROP TABLE ops.payment_method;` |
| BDD-005 | `obligation_agreement.obligation_type varchar(20) NOT NULL` | Yes | Migration L6-8: ADD COLUMN varchar(20), SET NOT NULL |
| BDD-006 | `obligation_agreement.cadence varchar(20) NOT NULL` | Yes | Migration L13-15: ADD COLUMN varchar(20), SET NOT NULL |
| BDD-007 | `obligation_agreement.payment_method varchar(30)` nullable | Yes | Migration L20: ADD COLUMN varchar(30), no SET NOT NULL |
| BDD-008 | `obligation_instance.status varchar(20) NOT NULL` | Yes | Migration L26-28: ADD COLUMN varchar(20), SET NOT NULL |
| BDD-009 | No `obligation_type_id`, `cadence_id`, `payment_method_id` columns | Yes | Migration L10 drops obligation_type_id, L17 drops cadence_id, L23 drops payment_method_id |
| BDD-010 | No `status_id` column | Yes | Migration L30: `DROP COLUMN status_id` |
| BDD-011 | Data preserved: obligation_type from lookup name | Yes | Migration L7: `UPDATE ... SET obligation_type = (SELECT name FROM ops.obligation_type WHERE id = obligation_type_id)` |
| BDD-012 | Data preserved: cadence from lookup name | Yes | Migration L14: `UPDATE ... SET cadence = (SELECT name FROM ops.cadence WHERE id = cadence_id)` |
| BDD-013 | Data preserved: payment_method from lookup name or NULL | Yes | Migration L21: `UPDATE ... SET payment_method = (SELECT name FROM ops.payment_method WHERE id = payment_method_id) WHERE payment_method_id IS NOT NULL` |
| BDD-014 | Data preserved: status from lookup name | Yes | Migration L27: `UPDATE ... SET status = (SELECT name FROM ops.obligation_status WHERE id = status_id)` |
| BDD-015 | DOWN restores all four lookup tables with seed data | Yes | Migration L41-63: CREATE TABLE + INSERT for all four tables with correct seed values |
| BDD-016 | DOWN restores integer FK columns with correct values | Yes | Migration L66-90: ADD COLUMN, UPDATE from lookup, SET NOT NULL, ADD CONSTRAINT for all four FK columns |
| BDD-017 | DOWN removes varchar columns added by UP | Yes | Migration L70 (status), L76 (payment_method), L83 (cadence), L90 (obligation_type) all DROP COLUMN |
| BDD-018 | Migration UP succeeds | Yes | Dal.Tests pass against the migrated DB (72/72); the schema accepted all varchar-column INSERTs |

### Deliverable 2: Domain Layer Updates (Ops.fs)

| BDD ID | Description | Verified | Evidence |
|---|---|---|---|
| BDD-019 | No record types ObligationType, ObligationStatus, Cadence, PaymentMethod | Yes | Grep for `type ObligationType\b\|type ObligationStatus\b\|type Cadence\b\|type PaymentMethod\b` returns no matches in Ops.fs |
| BDD-020 | `ObligationDirection.toString` Receivable->"receivable", Payable->"payable" | Yes | Ops.fs L32-34: match function with both cases |
| BDD-021 | `ObligationDirection.fromString` "receivable"->Ok Receivable, "payable"->Ok Payable | Yes | Ops.fs L36-40: match with both Ok cases |
| BDD-022 | `ObligationDirection.fromString` returns Error for invalid input | Yes | Ops.fs L40: `_ -> Error (sprintf ...)` |
| BDD-023 | `InstanceStatus.toString` all 6 cases | Yes | Ops.fs L43-50: Expected->"expected", InFlight->"in_flight", Confirmed->"confirmed", Posted->"posted", Overdue->"overdue", Skipped->"skipped" |
| BDD-024 | `InstanceStatus.fromString` all 6 valid strings to Ok | Yes | Ops.fs L52-59: all 6 match arms return Ok |
| BDD-025 | `InstanceStatus.fromString` returns Error for invalid | Yes | Ops.fs L60: `_ -> Error (sprintf ...)` |
| BDD-026 | `RecurrenceCadence.toString` all 4 cases | Yes | Ops.fs L63-67: Monthly->"monthly", Quarterly->"quarterly", Annual->"annual", OneTime->"one_time" |
| BDD-027 | `RecurrenceCadence.fromString` all 4 valid strings to Ok | Yes | Ops.fs L69-74: all 4 match arms return Ok |
| BDD-028 | `RecurrenceCadence.fromString` returns Error for invalid | Yes | Ops.fs L75: `_ -> Error (sprintf ...)` |
| BDD-029 | `PaymentMethodType.toString` all 6 cases | Yes | Ops.fs L78-84: AutopayPull->"autopay_pull", Ach->"ach", Zelle->"zelle", Cheque->"cheque", BillPay->"bill_pay", Manual->"manual" |
| BDD-030 | `PaymentMethodType.fromString` all 6 valid strings to Ok | Yes | Ops.fs L86-93: all 6 match arms return Ok |
| BDD-031 | `PaymentMethodType.fromString` returns Error for invalid | Yes | Ops.fs L94: `_ -> Error (sprintf ...)` |
| BDD-032 | `ObligationAgreement.obligationType: ObligationDirection` | Yes | Ops.fs L99: `obligationType: ObligationDirection` |
| BDD-033 | `ObligationAgreement.cadence: RecurrenceCadence` | Yes | Ops.fs L102: `cadence: RecurrenceCadence` |
| BDD-034 | `ObligationAgreement.paymentMethod: PaymentMethodType option` | Yes | Ops.fs L104: `paymentMethod: PaymentMethodType option` |
| BDD-035 | `ObligationInstance.status: InstanceStatus` | Yes | Ops.fs L116: `status: InstanceStatus` |

### Deliverable 3: Domain Tests (Tests.fs)

| BDD ID | Description | Verified | Evidence |
|---|---|---|---|
| BDD-036 | ObligationDirection round-trip test | Yes | Tests.fs L137-143: Theory with "receivable","payable" InlineData; fromString then toString |
| BDD-037 | ObligationDirection.fromString "invalid" returns Error | Yes | Tests.fs L145-149: Fact asserting Error |
| BDD-038 | InstanceStatus round-trip test for all 6 cases | Yes | Tests.fs L153-163: Theory with 6 InlineData values |
| BDD-039 | InstanceStatus.fromString "invalid" returns Error | Yes | Tests.fs L165-169: Fact asserting Error |
| BDD-040 | RecurrenceCadence round-trip test for all 4 cases | Yes | Tests.fs L173-181: Theory with 4 InlineData values |
| BDD-041 | RecurrenceCadence.fromString "invalid" returns Error | Yes | Tests.fs L183-187: Fact asserting Error |
| BDD-042 | PaymentMethodType round-trip test for all 6 cases | Yes | Tests.fs L191-201: Theory with 6 InlineData values |
| BDD-043 | PaymentMethodType.fromString "invalid" returns Error | Yes | Tests.fs L203-207: Fact asserting Error |
| BDD-044 | All new tests use [<Fact>] or [<Theory>], no TickSpec/Gherkin | Yes | Tests.fs contains only xUnit attributes; no TickSpec or Gherkin references |
| BDD-045 | `dotnet test` on Domain.Tests passes (all 36 green) | Yes | Independent run: 36/36 passed, 0 failed |

### Deliverable 4: Dal.Tests Updates

| BDD ID | Description | Verified | Evidence |
|---|---|---|---|
| BDD-046 | No @FT-OSC-001 through @FT-OSC-008 in feature file | Yes | Grep returns no matches |
| BDD-047 | No @FT-OSC-010/011/012/013/014/021/022 in feature file | Yes | Grep returns no matches |
| BDD-048 | @FT-OSC-042 scenario exists (obligation_type NOT NULL) | Yes | OpsStructuralConstraints.feature L177-179 |
| BDD-049 | @FT-OSC-043 scenario exists (cadence NOT NULL) | Yes | OpsStructuralConstraints.feature L182-186 |
| BDD-050 | @FT-OSC-044 scenario exists (status NOT NULL) | Yes | OpsStructuralConstraints.feature L188-192 |
| BDD-051 | No @FT-DR-014/015/016/017 in DeleteRestriction feature | Yes | Grep returns no matches |
| BDD-052 | No lookup table insert step defs in OpsStepDefinitions | Yes | Grep for table inserts returns no matches |
| BDD-053 | No lookup helper functions in SharedSteps | Yes | Grep for getValidObligationTypeId, getValidCadenceId, getValidObligationStatusId, insertObligationType, insertCadence, insertPaymentMethod, insertObligationStatus returns no matches |
| BDD-054 | `insertObligationAgreement` uses varchar columns | Yes | SharedSteps.fs L131-135: INSERT uses `obligation_type, cadence` with string values 'receivable', 'monthly' |
| BDD-055 | All INSERT statements use varchar columns, not integer FKs | Yes | Grep for obligation_type_id, cadence_id, payment_method_id, status_id across Dal.Tests returns no matches |
| BDD-056 | `dotnet test` on Dal.Tests passes (all 72 green) | Yes | Independent run: 72/72 passed, 0 failed |

### Deliverable 5: DataModelSpec Update

| BDD ID | Description | Verified | Evidence |
|---|---|---|---|
| BDD-057 | No table definitions for the 4 eliminated lookup tables | Yes | Grep for `### obligation_type/status/cadence/payment_method` headings returns no matches |
| BDD-058 | obligation_agreement shows varchar columns | Yes | DataModelSpec.md L226: `obligation_type varchar(20) NOT NULL`, L229: `cadence varchar(20) NOT NULL`, L231: `payment_method varchar(30)` |
| BDD-059 | obligation_instance shows `status varchar(20) NOT NULL` | Yes | DataModelSpec.md L264: `status varchar(20) NOT NULL` |
| BDD-060 | No FK relationships to eliminated lookup tables | Yes | Grep for `-> obligation_type/status/cadence/payment_method` returns no matches; cross-schema section (L383-391) lists only ledger FKs |

### Cross-cutting

| BDD ID | Description | Verified | Evidence |
|---|---|---|---|
| BDD-061 | `dotnet build` succeeds, 0 warnings from Domain/Domain.Tests/Dal.Tests | Yes | Independent run: Build succeeded, 0 Warning(s), 0 Error(s) |
| BDD-062 | `dotnet test` succeeds for entire solution | Yes | Domain.Tests 36/36 passed + Dal.Tests 72/72 passed = 108/108 all green |

---

## Cross-Artifact Traceability Spot-Checks

Five randomly selected BDD criteria traced end-to-end:

### 1. BDD-005 (obligation_agreement.obligation_type varchar(20) NOT NULL)
- BDD-005 -> BRD Deliverable 1 (migration) -> Migration L6-8 (ADD COLUMN + SET NOT NULL) -> @FT-OSC-042 tests NOT NULL constraint -> Dal.Tests pass (72/72)
- Chain: INTACT

### 2. BDD-023 (InstanceStatus.toString all 6 cases)
- BDD-023 -> BRD Deliverable 2 (domain layer) -> Ops.fs L43-50 (6-case match) -> BDD-038 round-trip test (Tests.fs L153-163, Theory with 6 InlineData) -> Domain.Tests pass (36/36)
- Chain: INTACT

### 3. BDD-054 (insertObligationAgreement uses varchar columns)
- BDD-054 -> BRD Deliverable 4 (Dal.Tests updates) -> SharedSteps.fs L131-135 (INSERT with obligation_type, cadence string values) -> Dal.Tests pass using these helpers (72/72)
- Chain: INTACT

### 4. BDD-015 (DOWN restores lookup tables with seed data)
- BDD-015 -> BRD Deliverable 1 (migration) -> Migration L41-63 (CREATE TABLE + INSERT for all 4 tables) -> Verified SQL contains correct seed values (2+6+4+6 = 18 seed rows)
- Chain: INTACT

### 5. BDD-058 (DataModelSpec shows varchar columns for obligation_agreement)
- BDD-058 -> BRD Deliverable 5 (DataModelSpec update) -> DataModelSpec.md L226/229/231 (varchar columns with DU-backed notes) -> Consistent with Ops.fs types and migration DDL
- Chain: INTACT

---

## Fabrication Detection

- No citations to nonexistent files detected.
- No circular reasoning found -- all evidence traces to actual file contents or independent test execution.
- Test results are from this session's independent `dotnet test` runs, not stale artifacts.
- No omitted failures: 36/36 Domain.Tests + 72/72 Dal.Tests = 108/108 total, all passing.

---

## Verdict: APPROVED

Every BDD criterion (62/62) is independently verified against the actual repo state. All tests pass. All five spot-check traceability chains are intact. No fabrication detected.
