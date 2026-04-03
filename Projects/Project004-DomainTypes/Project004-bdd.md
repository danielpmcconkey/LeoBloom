# Project 4 — BDD Acceptance Criteria

BDD IDs are project-scoped and ephemeral. They start at 001 for each project
and live only in this document. Domain tests use plain xUnit, not TickSpec or
Gherkin. These criteria describe what the tests must prove, not behavioral
specifications.

---

## Deliverable 1: Ledger Domain Types (`Ledger.fs`)

**BDD-001** — `Ledger.fs` defines a record type `AccountType` with fields
`id: int`, `name: string`, `normalBalance: NormalBalance`.

**BDD-002** — `Ledger.fs` defines a record type `Account` with fields `id`,
`code`, `name`, `accountTypeId`, `parentCode`, `isActive`, `createdAt`,
`modifiedAt` matching the column-to-field mapping in the BRD.

**BDD-003** — `Account.parentCode` is typed as `string option` (nullable
column).

**BDD-004** — `Ledger.fs` defines a record type `FiscalPeriod` with fields
`id`, `periodKey`, `startDate`, `endDate`, `isOpen`, `createdAt` matching
the BRD mapping.

**BDD-005** — `FiscalPeriod.startDate` and `FiscalPeriod.endDate` are typed
as `DateOnly`.

**BDD-006** — `Ledger.fs` defines a record type `JournalEntry` with fields
`id`, `entryDate`, `description`, `source`, `fiscalPeriodId`, `voidedAt`,
`voidReason`, `createdAt`, `modifiedAt` matching the BRD mapping.

**BDD-007** — `JournalEntry.source`, `JournalEntry.voidedAt`, and
`JournalEntry.voidReason` are typed as option types (nullable columns).

**BDD-008** — `Ledger.fs` defines a record type `JournalEntryReference` with
fields `id`, `journalEntryId`, `referenceType`, `referenceValue`, `createdAt`.

**BDD-009** — `Ledger.fs` defines a record type `JournalEntryLine` with fields
`id`, `journalEntryId`, `accountId`, `amount`, `entryType`, `memo` matching the
BRD mapping.

**BDD-010** — `JournalEntryLine.amount` is typed as `decimal`.

**BDD-011** — `JournalEntryLine.entryType` is typed as `EntryType` (the DU),
not a raw string.

**BDD-012** — `JournalEntryLine.memo` is typed as `string option` (nullable
column).

**BDD-013** — `Ledger.fs` defines a discriminated union `EntryType` with
exactly two cases: `Debit` and `Credit`.

**BDD-014** — `Ledger.fs` defines a discriminated union `NormalBalance` with
exactly two cases: `Debit` and `Credit`.

**BDD-015** — `NormalBalance` and `EntryType` are separate types. Code cannot
pass a `NormalBalance` value where an `EntryType` is expected, or vice versa.

---

## Deliverable 2: Ops Domain Types (`Ops.fs`)

**BDD-016** — `Ops.fs` defines record types for all eight ops schema tables:
`ObligationType`, `ObligationStatus`, `Cadence`, `PaymentMethod`,
`ObligationAgreement`, `ObligationInstance`, `Transfer`, `Invoice`.

**BDD-017** — `ObligationAgreement` fields match the BRD mapping, including
option types for nullable columns: `counterparty`, `amount`, `expectedDay`,
`paymentMethodId`, `sourceAccountId`, `destAccountId`, `notes`.

**BDD-018** — `ObligationInstance` fields match the BRD mapping, including
option types for nullable columns: `amount`, `confirmedDate`, `dueDate`,
`documentPath`, `journalEntryId`, `notes`.

**BDD-019** — `Transfer` fields match the BRD mapping, including option types
for nullable columns: `expectedSettlement`, `confirmedDate`, `journalEntryId`,
`description`.

**BDD-020** — `Invoice` fields match the BRD mapping, including option types
for nullable columns: `documentPath`, `notes`.

**BDD-021** — `Ops.fs` defines a discriminated union `ObligationDirection` with
exactly two cases: `Receivable` and `Payable`.

**BDD-022** — `Ops.fs` defines a discriminated union `InstanceStatus` with
exactly six cases: `Expected`, `InFlight`, `Confirmed`, `Posted`, `Overdue`,
`Skipped`.

**BDD-023** — `Ops.fs` defines a discriminated union `RecurrenceCadence` with
exactly four cases: `Monthly`, `Quarterly`, `Annual`, `OneTime`.

**BDD-024** — `Ops.fs` defines a discriminated union `PaymentMethodType` with
exactly six cases: `AutopayPull`, `Ach`, `Zelle`, `Cheque`, `BillPay`,
`Manual`.

**BDD-025** — `Ops.fs` defines a discriminated union `TransferStatus` with
exactly two cases: `Initiated` and `Confirmed`.

**BDD-026** — `Ops.fs` can reference types from the `Ledger` module. The
`.fsproj` compile order lists `Ledger.fs` before `Ops.fs`.

---

## Deliverable 3: Ledger Validation Functions (`Ledger.fs`)

**BDD-027** — A balance rule validation function exists in `Ledger.fs` that
accepts a list of `JournalEntryLine` and returns `Ok` when total debit amounts
equal total credit amounts.

**BDD-028** — The balance rule validation function returns `Error` with a
human-readable message when total debit amounts do not equal total credit
amounts.

**BDD-029** — An amount positivity validation function exists in `Ledger.fs`
that returns `Ok` when every `JournalEntryLine.amount` is greater than zero.

**BDD-030** — The amount positivity validation function returns `Error` when
any `JournalEntryLine.amount` is zero.

**BDD-031** — The amount positivity validation function returns `Error` when
any `JournalEntryLine.amount` is negative.

**BDD-032** — A minimum line count validation function exists in `Ledger.fs`
that returns `Ok` when a journal entry has two or more lines.

**BDD-033** — The minimum line count validation function returns `Error` when
a journal entry has fewer than two lines (zero or one).

**BDD-034** — A void reason validation function exists in `Ledger.fs` that
returns `Ok` when `voidedAt` is `None` regardless of `voidReason`.

**BDD-035** — The void reason validation function returns `Ok` when `voidedAt`
is `Some` and `voidReason` is `Some` with a non-empty string.

**BDD-036** — The void reason validation function returns `Error` when
`voidedAt` is `Some` and `voidReason` is `None`.

**BDD-037** — The void reason validation function returns `Error` when
`voidedAt` is `Some` and `voidReason` is `Some ""` (empty string).

**BDD-038** — All validation functions return `Result<'T, string list>` (or
equivalent). Error cases contain a list of human-readable failure messages.

**BDD-039** — All validation functions are pure: no I/O, no database access,
no mutable state, no exceptions for control flow. They accept values and
return results.

---

## Deliverable 4: Domain Tests (`Domain.Tests`)

**BDD-040** — `Tests.fs` in `LeoBloom.Domain.Tests` contains xUnit tests
exercising every validation function from Deliverable 3.

**BDD-041** — The placeholder test previously in `Tests.fs` is removed. It
does not exist alongside the real tests.

**BDD-042** — A test verifies that a balanced two-line journal entry (equal
debit and credit totals) passes the balance rule validation.

**BDD-043** — A test verifies that an unbalanced journal entry fails the
balance rule validation.

**BDD-044** — A test verifies that a compound entry (3+ lines) passes the
balance rule validation when balanced.

**BDD-045** — A test verifies that a positive amount passes the amount
positivity validation.

**BDD-046** — A test verifies that a zero amount fails the amount positivity
validation.

**BDD-047** — A test verifies that a negative amount fails the amount
positivity validation.

**BDD-048** — A test verifies that zero lines fails the minimum line count
validation.

**BDD-049** — A test verifies that one line fails the minimum line count
validation.

**BDD-050** — A test verifies that two lines passes the minimum line count
validation.

**BDD-051** — A test verifies that three lines passes the minimum line count
validation.

**BDD-052** — A test verifies that `voidedAt = None` needs no void reason
(passes regardless of `voidReason` value).

**BDD-053** — A test verifies that `voidedAt = Some` with a non-empty
`voidReason` passes the void reason validation.

**BDD-054** — A test verifies that `voidedAt = Some` with `voidReason = None`
fails the void reason validation.

**BDD-055** — A test verifies that `voidedAt = Some` with
`voidReason = Some ""` (empty string) fails the void reason validation.

**BDD-056** — All tests use plain xUnit attributes (`[<Fact>]` or
`[<Theory>]`). No TickSpec, no Gherkin, no BDD infrastructure is used.

**BDD-057** — `dotnet test` on `LeoBloom.Domain.Tests` passes with all tests
green.

**BDD-058** — `dotnet build` succeeds for the entire solution with no warnings
from `LeoBloom.Domain` or `LeoBloom.Domain.Tests`.
