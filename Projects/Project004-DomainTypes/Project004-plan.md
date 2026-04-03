# Project 004 — Domain Types: Implementation Plan

**Status:** Ready for builder
**Branch:** `feat/project4-domain-types`
**Date:** 2026-04-03

---

## Overview

Four files change: `Ledger.fs`, `Ops.fs`, `Tests.fs`, and `LeoBloom.Domain.Tests.fsproj`
(only if a test file split is needed). No new files. No new NuGet packages. No .fsproj
compile order changes needed -- `Ledger.fs` already precedes `Ops.fs`.

The work is ordered so the solution compiles after every step.

---

## Step 1: Ledger DUs and Record Types (`Ledger.fs`)

**File:** `/workspace/LeoBloom/Src/LeoBloom.Domain/Ledger.fs`
**Action:** Replace entire placeholder content with DUs + record types.

Write, in this order within the `Ledger` module:

1. `open System` (needed for `DateTimeOffset`, `DateOnly`)
2. DU `EntryType = Debit | Credit`
3. DU `NormalBalance = Debit | Credit`
4. Record `AccountType` -- `{ id: int; name: string; normalBalance: NormalBalance }`
5. Record `Account` -- `{ id: int; code: string; name: string; accountTypeId: int; parentCode: string option; isActive: bool; createdAt: DateTimeOffset; modifiedAt: DateTimeOffset }`
6. Record `FiscalPeriod` -- `{ id: int; periodKey: string; startDate: DateOnly; endDate: DateOnly; isOpen: bool; createdAt: DateTimeOffset }`
7. Record `JournalEntry` -- `{ id: int; entryDate: DateOnly; description: string; source: string option; fiscalPeriodId: int; voidedAt: DateTimeOffset option; voidReason: string option; createdAt: DateTimeOffset; modifiedAt: DateTimeOffset }`
8. Record `JournalEntryReference` -- `{ id: int; journalEntryId: int; referenceType: string; referenceValue: string; createdAt: DateTimeOffset }`
9. Record `JournalEntryLine` -- `{ id: int; journalEntryId: int; accountId: int; amount: decimal; entryType: EntryType; memo: string option }`

**Key decisions:**
- DUs come before records because records reference them. F# requires definition before use.
- `EntryType` and `NormalBalance` are separate types with identical cases. This is intentional
  -- the compiler prevents mixing them (BDD-015).
- `open System` at the module level, not the namespace level.

**Satisfies:** BDD-001 through BDD-015

**Verify:** `dotnet build /workspace/LeoBloom/Src/LeoBloom.Domain/LeoBloom.Domain.fsproj`
compiles with zero errors and zero warnings.

---

## Step 2: Ops DUs and Record Types (`Ops.fs`)

**File:** `/workspace/LeoBloom/Src/LeoBloom.Domain/Ops.fs`
**Action:** Replace entire placeholder content with DUs + record types.

Write, in this order within the `Ops` module:

1. `open System`
2. DU `ObligationDirection = Receivable | Payable`
3. DU `InstanceStatus = Expected | InFlight | Confirmed | Posted | Overdue | Skipped`
4. DU `RecurrenceCadence = Monthly | Quarterly | Annual | OneTime`
5. DU `PaymentMethodType = AutopayPull | Ach | Zelle | Cheque | BillPay | Manual`
6. DU `TransferStatus = Initiated | Confirmed`
7. Record `ObligationType` -- `{ id: int; name: string }`
8. Record `ObligationStatus` -- `{ id: int; name: string }`
9. Record `Cadence` -- `{ id: int; name: string }`
10. Record `PaymentMethod` -- `{ id: int; name: string }`
11. Record `ObligationAgreement` -- all fields per BRD, nullable columns as option types
12. Record `ObligationInstance` -- all fields per BRD, nullable columns as option types
13. Record `Transfer` -- all fields per BRD, nullable columns as option types
14. Record `Invoice` -- all fields per BRD, nullable columns as option types

**Key decisions:**
- `Ops` module does NOT need to reference `Ledger` types in any record fields. The
  cross-schema relationships are via integer FKs (`sourceAccountId: int`), not type
  references. The BRD says "Ops.fs can reference Ledger types" but no field actually
  uses a Ledger type -- they're all `int` FK IDs. No `open LeoBloom.Domain.Ledger` needed.
- DUs before records, same reasoning as Step 1.
- The four lookup-table records (`ObligationType`, `ObligationStatus`, `Cadence`,
  `PaymentMethod`) exist alongside their DU counterparts. The records mirror the DB
  rows; the DUs provide type-safe values for application code. They serve different
  purposes and both belong here.

**Satisfies:** BDD-016 through BDD-026

**Verify:** `dotnet build /workspace/LeoBloom/Src/LeoBloom.Domain/LeoBloom.Domain.fsproj`
compiles with zero errors and zero warnings. (This also verifies Ledger.fs still compiles.)

---

## Step 3: Ledger Validation Functions (`Ledger.fs`)

**File:** `/workspace/LeoBloom/Src/LeoBloom.Domain/Ledger.fs`
**Action:** Add a `Validation` sub-module (or just functions) after the record types
in the `Ledger` module.

Four functions, all returning `Result<unit, string list>` (or `Result<'T, string list>`
where appropriate):

### 3a. `validateBalanced`
- **Signature:** `JournalEntryLine list -> Result<JournalEntryLine list, string list>`
- **Logic:** Sum amounts where `entryType = Debit`, sum amounts where `entryType = Credit`. If equal, `Ok lines`. Otherwise `Error ["Debits (X) do not equal credits (Y)"]`.
- **Satisfies:** BDD-027, BDD-028

### 3b. `validateAmountsPositive`
- **Signature:** `JournalEntryLine list -> Result<JournalEntryLine list, string list>`
- **Logic:** Check every line's `amount > 0m`. Collect all violations. If none, `Ok lines`. Otherwise `Error` with one message per bad line (e.g. `"Line for account X has non-positive amount: Y"`).
- **Satisfies:** BDD-029, BDD-030, BDD-031

### 3c. `validateMinimumLineCount`
- **Signature:** `JournalEntryLine list -> Result<JournalEntryLine list, string list>`
- **Logic:** `List.length lines >= 2`. If yes, `Ok lines`. Otherwise `Error ["Journal entry must have at least 2 lines, got N"]`.
- **Satisfies:** BDD-032, BDD-033

### 3d. `validateVoidReason`
- **Signature:** `DateTimeOffset option -> string option -> Result<unit, string list>`
- **Logic:** Pattern match on `(voidedAt, voidReason)`:
  - `(None, _)` -> `Ok ()`
  - `(Some _, Some reason)` when `reason |> String.IsNullOrWhiteSpace |> not` -> `Ok ()`
  - `(Some _, None)` -> `Error ["Void reason is required when entry is voided"]`
  - `(Some _, Some "")` or whitespace -> `Error ["Void reason cannot be empty when entry is voided"]`
- **Satisfies:** BDD-034, BDD-035, BDD-036, BDD-037

**All four functions satisfy:** BDD-038 (Result return type), BDD-039 (purity)

**Verify:** `dotnet build /workspace/LeoBloom/Src/LeoBloom.Domain/LeoBloom.Domain.fsproj`
compiles with zero errors and zero warnings.

---

## Step 4: Domain Tests (`Tests.fs`)

**File:** `/workspace/LeoBloom/Src/LeoBloom.Domain.Tests/Tests.fs`
**Action:** Replace entire placeholder content with real xUnit tests.

The test file should:

1. `module LeoBloom.Domain.Tests.LedgerValidationTests` (or similar)
2. `open System`, `open Xunit`, `open LeoBloom.Domain.Ledger`
3. Define a helper to create `JournalEntryLine` values with sensible defaults,
   so tests stay concise.

### Test inventory (maps 1:1 to BDD criteria):

**Balance rule tests:**
- `balanced two-line entry passes` -- BDD-042
- `unbalanced entry fails` -- BDD-043
- `balanced compound entry (3+ lines) passes` -- BDD-044

**Amount positivity tests:**
- `positive amounts pass` -- BDD-045
- `zero amount fails` -- BDD-046
- `negative amount fails` -- BDD-047

**Minimum line count tests:**
- `zero lines fails` -- BDD-048
- `one line fails` -- BDD-049
- `two lines passes` -- BDD-050
- `three lines passes` -- BDD-051

**Void reason tests:**
- `voidedAt None needs no reason` -- BDD-052
- `voidedAt Some with non-empty reason passes` -- BDD-053
- `voidedAt Some with reason None fails` -- BDD-054
- `voidedAt Some with empty reason fails` -- BDD-055

All tests use `[<Fact>]` (BDD-056). Assert using pattern matching on the Result:
`match result with Ok _ -> () | Error msgs -> Assert.Fail(...)` for happy path, and
`match result with Error _ -> () | Ok _ -> Assert.Fail("Expected Error")` for failure path.

**Satisfies:** BDD-040 through BDD-057 (all test BDDs), BDD-041 (placeholder removed)

**Verify:** `dotnet test /workspace/LeoBloom/Src/LeoBloom.Domain.Tests/LeoBloom.Domain.Tests.fsproj`
-- all tests green.

---

## Step 5: Full Solution Build

**Action:** No file changes. Verification only.

**Verify:**
1. `dotnet build /workspace/LeoBloom/LeoBloom.sln` -- zero errors, zero warnings
   from `LeoBloom.Domain` and `LeoBloom.Domain.Tests` (BDD-058).
2. `dotnet test /workspace/LeoBloom/LeoBloom.sln` -- all tests pass (BDD-057).

---

## BDD Traceability Matrix

| BDD ID | Step | What it covers |
|--------|------|----------------|
| 001-015 | Step 1 | Ledger DUs + record types |
| 016-026 | Step 2 | Ops DUs + record types |
| 027-039 | Step 3 | Validation functions |
| 040-058 | Step 4 | Tests + build verification |

Every BDD criterion is addressed by at least one step. No gaps.

---

## Risks and Notes

1. **F# compile order is already correct.** The .fsproj lists `Ledger.fs` before
   `Ops.fs`. No change needed. If the builder adds files, they must go in the
   right position in the `<ItemGroup>`.

2. **No new files expected.** The BRD explicitly says "No file additions unless the
   builder determines splitting is necessary." For this scope, splitting is not
   necessary. Everything fits in the existing files.

3. **`module` vs `namespace` pattern.** The existing files use `namespace LeoBloom.Domain`
   with `module Ledger =` / `module Ops =`. Keep this pattern. Do not switch to
   standalone module declarations.

4. **The `open System` placement.** Must be inside the module, not at the namespace level.
   F# namespace blocks don't allow `open` at the top level in certain configurations.
   Place it as the first line inside `module Ledger =` and `module Ops =`.

5. **Validation function naming.** The BRD doesn't prescribe function names. The names
   above (`validateBalanced`, etc.) are suggestions. The builder should use whatever
   reads cleanly as long as the signatures and behavior match.

6. **No `Validation` module split.** Validation functions go directly in the `Ledger`
   module alongside the types. A separate `Validation.fs` would require .fsproj changes
   and is out of scope. If the builder wants namespacing within the module, a nested
   `module Validation =` inside `Ledger` is fine but not required.

7. **Test helper for JournalEntryLine construction.** The builder should create a
   `makeLine` or similar helper that defaults `id`, `journalEntryId`, and `memo` so
   each test only specifies the fields that matter (amount, entryType, accountId).
   This keeps tests readable and reduces noise.
