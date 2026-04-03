# Project 4 — Domain Types

## Objective

Deliver F# record and discriminated union types in `LeoBloom.Domain` that
mirror every table in the `ledger` and `ops` schemas, plus pure validation
functions for the fundamental accounting invariants. When this project is
done, the domain layer is a typed, testable representation of the data model
with core business rules enforced as pure functions — no I/O, no database
access, no persistence.

---

## Background

Projects 1-3 established the database schema, test harness, and BDD
infrastructure. The domain layer exists as placeholder files (`Ledger.fs`,
`Ops.fs`) with no types defined. Project 5 (Post Journal Entry) and all
downstream work depends on these types existing. This project fills the gap
between the database schema and the application layer.

**Source of truth for schema:** `Projects/Project001-Database/DataModelSpec.md`

**What "pure" means:** Every validation function in this project is a pure
function — deterministic, no side effects, no I/O, no database access. Input
in, result out. This makes them trivially testable with xUnit and keeps the
domain layer independent of infrastructure.

---

## Deliverables

### 1. Ledger Domain Types (`Ledger.fs`)

F# types mirroring the six `ledger` schema tables. Each type is a record
with fields matching the table's columns, using idiomatic F# types.

**Type-to-table mapping:**

| F# Type | Schema Table | Notes |
|---------|-------------|-------|
| `AccountType` | `ledger.account_type` | `id: int`, `name: string`, `normalBalance: string` |
| `Account` | `ledger.account` | `id: int`, `code: string`, `name: string`, `accountTypeId: int`, `parentCode: string option`, `isActive: bool`, `createdAt: DateTimeOffset`, `modifiedAt: DateTimeOffset` |
| `FiscalPeriod` | `ledger.fiscal_period` | `id: int`, `periodKey: string`, `startDate: DateOnly`, `endDate: DateOnly`, `isOpen: bool`, `createdAt: DateTimeOffset` |
| `JournalEntry` | `ledger.journal_entry` | `id: int`, `entryDate: DateOnly`, `description: string`, `source: string option`, `fiscalPeriodId: int`, `voidedAt: DateTimeOffset option`, `voidReason: string option`, `createdAt: DateTimeOffset`, `modifiedAt: DateTimeOffset` |
| `JournalEntryReference` | `ledger.journal_entry_reference` | `id: int`, `journalEntryId: int`, `referenceType: string`, `referenceValue: string`, `createdAt: DateTimeOffset` |
| `JournalEntryLine` | `ledger.journal_entry_line` | `id: int`, `journalEntryId: int`, `accountId: int`, `amount: decimal`, `entryType: string`, `memo: string option` |

**Discriminated union for entry type:**

```fsharp
type EntryType = Debit | Credit
```

The `JournalEntryLine` record uses `EntryType` (not a raw string) for the
`entryType` field. Conversion to/from the database string representation
(`"debit"`, `"credit"`) is the DAL's responsibility (Project 5+), but the
domain type is strongly typed now.

**Discriminated union for normal balance:**

```fsharp
type NormalBalance = Debit | Credit
```

The `AccountType` record uses `NormalBalance` for the `normalBalance` field.
Same DU as `EntryType` values but distinct semantic meaning — reuse the same
cases but as a separate type to prevent accidental mixing.

**Column-to-field conventions:**
- `serial PK` columns map to `int` (these are database-assigned; for
  creation inputs they'll be absent, but that's a Project 5 concern)
- `varchar` maps to `string`
- `numeric(12,2)` maps to `decimal`
- `boolean` maps to `bool`
- `date` maps to `DateOnly`
- `timestamptz` maps to `DateTimeOffset`
- Nullable columns map to `option` types
- `NOT NULL` columns are non-optional

### 2. Ops Domain Types (`Ops.fs`)

F# types mirroring the eight `ops` schema tables.

**Type-to-table mapping:**

| F# Type | Schema Table | Notes |
|---------|-------------|-------|
| `ObligationType` | `ops.obligation_type` | `id: int`, `name: string` |
| `ObligationStatus` | `ops.obligation_status` | `id: int`, `name: string` |
| `Cadence` | `ops.cadence` | `id: int`, `name: string` |
| `PaymentMethod` | `ops.payment_method` | `id: int`, `name: string` |
| `ObligationAgreement` | `ops.obligation_agreement` | `id: int`, `name: string`, `obligationTypeId: int`, `counterparty: string option`, `amount: decimal option`, `cadenceId: int`, `expectedDay: int option`, `paymentMethodId: int option`, `sourceAccountId: int option`, `destAccountId: int option`, `isActive: bool`, `notes: string option`, `createdAt: DateTimeOffset`, `modifiedAt: DateTimeOffset` |
| `ObligationInstance` | `ops.obligation_instance` | `id: int`, `obligationAgreementId: int`, `name: string`, `statusId: int`, `amount: decimal option`, `expectedDate: DateOnly`, `confirmedDate: DateOnly option`, `dueDate: DateOnly option`, `documentPath: string option`, `journalEntryId: int option`, `notes: string option`, `isActive: bool`, `createdAt: DateTimeOffset`, `modifiedAt: DateTimeOffset` |
| `Transfer` | `ops.transfer` | `id: int`, `fromAccountId: int`, `toAccountId: int`, `amount: decimal`, `status: string`, `initiatedDate: DateOnly`, `expectedSettlement: DateOnly option`, `confirmedDate: DateOnly option`, `journalEntryId: int option`, `description: string option`, `isActive: bool`, `createdAt: DateTimeOffset`, `modifiedAt: DateTimeOffset` |
| `Invoice` | `ops.invoice` | `id: int`, `tenant: string`, `fiscalPeriodId: int`, `rentAmount: decimal`, `utilityShare: decimal`, `totalAmount: decimal`, `generatedAt: DateTimeOffset`, `documentPath: string option`, `notes: string option`, `isActive: bool`, `createdAt: DateTimeOffset`, `modifiedAt: DateTimeOffset` |

**Discriminated unions for ops lookup values:**

```fsharp
type ObligationDirection = Receivable | Payable

type InstanceStatus =
    | Expected | InFlight | Confirmed | Posted | Overdue | Skipped

type RecurrenceCadence = Monthly | Quarterly | Annual | OneTime

type PaymentMethodType =
    | AutopayPull | Ach | Zelle | Cheque | BillPay | Manual

type TransferStatus = Initiated | Confirmed
```

The `Ops.fs` module can reference `Ledger` types (the `.fsproj` compile
order ensures `Ledger.fs` appears before `Ops.fs`). The `Ledger` module
knows nothing about `Ops` — one-way dependency, mirroring the schema design
(ref: DataModelSpec "Cross-schema relationships" section).

### 3. Ledger Validation Functions (`Ledger.fs`)

Pure functions enforcing the fundamental ledger invariants documented in
DataModelSpec. These are the business rules that Project 5 will call before
persisting a journal entry.

**Required validation functions:**

| Function | Rule | Reference |
|----------|------|-----------|
| Balance rule | For a list of `JournalEntryLine`, `SUM(amount) WHERE entryType = Debit` must equal `SUM(amount) WHERE entryType = Credit` | DataModelSpec `journal_entry_line` invariants |
| Amount positivity | Every `JournalEntryLine.amount` must be > 0 | DataModelSpec: "amount must be > 0. No negative amounts." |
| Entry type validity | `entryType` must be `Debit` or `Credit` | DataModelSpec: "entry_type must be debit or credit" — enforced structurally by the `EntryType` DU, so this is satisfied by the type system |
| Minimum line count | A journal entry must have at least two lines | DataModelSpec: "A journal entry must have at least two lines" |
| Void reason required | When `voidedAt` is `Some`, `voidReason` must be `Some` and non-empty | DataModelSpec: "void_reason must be non-empty when voided_at is set" |

**Return type convention:** Validation functions return `Result<'T, string list>`
(or equivalent) — `Ok` with the validated value on success, `Error` with a
list of human-readable failure reasons on failure. Multiple validations can
be composed to accumulate all errors rather than failing on the first one.

**Purity contract:** These functions accept values and return results. No
database calls, no file I/O, no mutable state, no exceptions for control
flow. If a validation needs data it doesn't have (e.g., "does this account
exist?"), that's the caller's responsibility to provide — these functions
only validate what they're given.

### 4. Domain Tests (`Domain.Tests`)

xUnit tests in `Src/LeoBloom.Domain.Tests/` exercising every validation
function from Deliverable 3. Replace the placeholder test in `Tests.fs`
with real tests.

**Test coverage requirements:**

- Balance rule: balanced entry passes, unbalanced entry fails, single-line
  fails (caught by minimum line count), compound entry (3+ lines) passes
  when balanced
- Amount positivity: positive amount passes, zero amount fails, negative
  amount fails
- Minimum line count: zero lines fails, one line fails, two lines passes,
  three lines passes
- Void reason: `voidedAt = None` needs no reason, `voidedAt = Some` with
  reason passes, `voidedAt = Some` with empty reason fails, `voidedAt = Some`
  with no reason fails

These are pure unit tests — no database, no BDD infrastructure, no TickSpec.
Plain xUnit `[<Fact>]` or `[<Theory>]` attributes. Fast, isolated,
deterministic.

**Project reference:** `LeoBloom.Domain.Tests.fsproj` already references
`LeoBloom.Domain.fsproj` (confirmed in the existing `.fsproj` file).

---

## Project Structure After Completion

```
LeoBloom/
├── Src/
│   ├── LeoBloom.Domain/
│   │   ├── LeoBloom.Domain.fsproj          (unchanged)
│   │   ├── Ledger.fs                       MODIFIED: types + validation
│   │   └── Ops.fs                          MODIFIED: types
│   └── LeoBloom.Domain.Tests/
│       ├── LeoBloom.Domain.Tests.fsproj    (unchanged or minor updates)
│       └── Tests.fs                        MODIFIED: real tests replace placeholder
└── Projects/
    └── Project004-DomainTypes/
        ├── Project004-brd.md               THIS FILE
        └── Project004-bdd.md               BDD acceptance criteria (separate)
```

---

## Acceptance Criteria

1. `Ledger.fs` defines record types for all six `ledger` schema tables
   (`AccountType`, `Account`, `FiscalPeriod`, `JournalEntry`,
   `JournalEntryReference`, `JournalEntryLine`).
2. `Ops.fs` defines record types for all eight `ops` schema tables
   (`ObligationType`, `ObligationStatus`, `Cadence`, `PaymentMethod`,
   `ObligationAgreement`, `ObligationInstance`, `Transfer`, `Invoice`).
   Note: the four lookup tables also have DU representations.
3. `EntryType` and `NormalBalance` discriminated unions exist in `Ledger.fs`
   and are used by `JournalEntryLine` and `AccountType` respectively.
4. Ops DUs exist for `ObligationDirection`, `InstanceStatus`,
   `RecurrenceCadence`, `PaymentMethodType`, and `TransferStatus`.
5. Nullable columns map to `option` types; non-nullable columns are
   non-optional.
6. Balance rule validation function exists and returns `Error` for unbalanced
   entries.
7. Amount positivity validation function exists and returns `Error` for
   zero or negative amounts.
8. Minimum line count validation function exists and returns `Error` for
   entries with fewer than two lines.
9. Void reason validation function exists and returns `Error` when
   `voidedAt` is set but `voidReason` is missing or empty.
10. All validation functions are pure — no I/O, no database access,
    no mutable state.
11. `dotnet test` on `LeoBloom.Domain.Tests` passes with all validation
    functions exercised (happy path and failure cases).
12. The placeholder test in `Tests.fs` is replaced, not left alongside
    the real tests.
13. `dotnet build` succeeds for the entire solution with no warnings
    from `LeoBloom.Domain` or `LeoBloom.Domain.Tests`.

---

## Out of Scope

- **Persistence / DAL mapping.** How these types get to/from the database is
  Project 5+. No Dapper, no SQL, no connection strings in the domain.
- **Status machine.** The obligation instance lifecycle (`expected` ->
  `in_flight` -> `confirmed` -> `posted`) is a state machine concern for a
  later project. The DU defines the states; it does not enforce transitions.
- **Cross-entity validation.** "Does this account exist?" or "Is this fiscal
  period open?" require database lookups. Those validations belong in the
  application/service layer (Project 5+), not in pure domain functions.
- **BDD / TickSpec / Gherkin.** Domain tests use plain xUnit. Feature files
  and BDD infrastructure are not touched.
- **New `.fs` files.** Types go in the existing `Ledger.fs` and `Ops.fs`.
  Tests go in the existing `Tests.fs`. No file additions unless the builder
  determines splitting is necessary for F# compile order reasons.
- **API, UI, or CLI.** No consumer of these types is built in this project.

---

## Dependencies

- **Project 1 (Done):** DataModelSpec defines the schema these types mirror.
- **Project 2 (Done):** Test harness infrastructure (xUnit, test SDK).
- **Project 3 (Done):** BDD infrastructure (not used directly, but ensures
  the repo's test patterns are established).
- `LeoBloom.Domain.fsproj` and `LeoBloom.Domain.Tests.fsproj` exist with
  the correct project references (confirmed: already in place).
