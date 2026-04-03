# Project 004 — Test Results

**Date:** 2026-04-03
**Commit:** 72526e79e7abcc11916f0ccca97cec74d97f5faa
**Branch:** feat/project4-domain-types
**Result:** 58/58 verified
**Runner:** `dotnet test` (xUnit, .NET 10) + manual code inspection

---

## BDD Verification Mapping

### Deliverable 1: Ledger Domain Types (Ledger.fs)

| BDD ID | Description | Verified | Evidence |
|---|---|---|---|
| BDD-001 | `AccountType` record: `id: int`, `name: string`, `normalBalance: NormalBalance` | Yes | Ledger.fs lines 13-16 |
| BDD-002 | `Account` record fields match BRD column mapping | Yes | Ledger.fs lines 18-26, all 8 fields present |
| BDD-003 | `Account.parentCode` is `string option` | Yes | Ledger.fs line 22 |
| BDD-004 | `FiscalPeriod` record fields match BRD mapping | Yes | Ledger.fs lines 28-34, all 6 fields present |
| BDD-005 | `FiscalPeriod.startDate` and `endDate` are `DateOnly` | Yes | Ledger.fs lines 31-32 |
| BDD-006 | `JournalEntry` record fields match BRD mapping | Yes | Ledger.fs lines 36-45, all 9 fields present |
| BDD-007 | `JournalEntry.source`, `voidedAt`, `voidReason` are option types | Yes | Ledger.fs lines 40, 42, 43 |
| BDD-008 | `JournalEntryReference` record with 5 fields | Yes | Ledger.fs lines 47-52 |
| BDD-009 | `JournalEntryLine` record fields match BRD mapping | Yes | Ledger.fs lines 54-60, all 6 fields present |
| BDD-010 | `JournalEntryLine.amount` is `decimal` | Yes | Ledger.fs line 57 |
| BDD-011 | `JournalEntryLine.entryType` is `EntryType` (DU, not raw string) | Yes | Ledger.fs line 59 |
| BDD-012 | `JournalEntryLine.memo` is `string option` | Yes | Ledger.fs line 60 |
| BDD-013 | `EntryType` DU with exactly `Debit` and `Credit` cases | Yes | Ledger.fs line 9 |
| BDD-014 | `NormalBalance` DU with exactly `Debit` and `Credit` cases | Yes | Ledger.fs line 11 |
| BDD-015 | `NormalBalance` and `EntryType` are separate types | Yes | Defined as distinct DUs on lines 9 and 11; F# type system prevents interchange |

### Deliverable 2: Ops Domain Types (Ops.fs)

| BDD ID | Description | Verified | Evidence |
|---|---|---|---|
| BDD-016 | All 8 ops record types defined | Yes | Ops.fs: ObligationType (31-33), ObligationStatus (35-37), Cadence (39-41), PaymentMethod (43-45), ObligationAgreement (47-61), ObligationInstance (63-77), Transfer (79-92), Invoice (94-106) |
| BDD-017 | `ObligationAgreement` fields match BRD with option types for nullable columns | Yes | Ops.fs lines 47-61; `counterparty`, `amount`, `expectedDay`, `paymentMethodId`, `sourceAccountId`, `destAccountId`, `notes` all typed as option |
| BDD-018 | `ObligationInstance` fields match BRD with option types for nullable columns | Yes | Ops.fs lines 63-77; `amount`, `confirmedDate`, `dueDate`, `documentPath`, `journalEntryId`, `notes` all typed as option |
| BDD-019 | `Transfer` fields match BRD with option types for nullable columns | Yes | Ops.fs lines 79-92; `expectedSettlement`, `confirmedDate`, `journalEntryId`, `description` all typed as option |
| BDD-020 | `Invoice` fields match BRD with option types for nullable columns | Yes | Ops.fs lines 94-106; `documentPath`, `notes` typed as option |
| BDD-021 | `ObligationDirection` DU: `Receivable`, `Payable` | Yes | Ops.fs line 9 |
| BDD-022 | `InstanceStatus` DU: 6 cases (`Expected`, `InFlight`, `Confirmed`, `Posted`, `Overdue`, `Skipped`) | Yes | Ops.fs lines 11-17 |
| BDD-023 | `RecurrenceCadence` DU: 4 cases (`Monthly`, `Quarterly`, `Annual`, `OneTime`) | Yes | Ops.fs line 19 |
| BDD-024 | `PaymentMethodType` DU: 6 cases (`AutopayPull`, `Ach`, `Zelle`, `Cheque`, `BillPay`, `Manual`) | Yes | Ops.fs lines 21-27 |
| BDD-025 | `TransferStatus` DU: `Initiated`, `Confirmed` | Yes | Ops.fs line 29 |
| BDD-026 | `.fsproj` lists `Ledger.fs` before `Ops.fs` | Yes | LeoBloom.Domain.fsproj lines 9-10 |

### Deliverable 3: Ledger Validation Functions (Ledger.fs)

| BDD ID | Description | Verified | Evidence |
|---|---|---|---|
| BDD-027 | Balance rule returns `Ok` when debits equal credits | Yes | `validateBalanced` at line 62; tested by `balanced two-line entry passes` |
| BDD-028 | Balance rule returns `Error` with message when unbalanced | Yes | Line 72 returns `Error` with sprintf message; tested by `unbalanced entry fails` |
| BDD-029 | Amount positivity returns `Ok` when all amounts > 0 | Yes | `validateAmountsPositive` at line 74; tested by `positive amounts pass` |
| BDD-030 | Amount positivity returns `Error` when any amount is zero | Yes | Line 77 filters `<= 0m`; tested by `zero amount fails` |
| BDD-031 | Amount positivity returns `Error` when any amount is negative | Yes | Line 77 filters `<= 0m`; tested by `negative amount fails` |
| BDD-032 | Min line count returns `Ok` when >= 2 lines | Yes | `validateMinimumLineCount` at line 82; tested by `two lines passes` |
| BDD-033 | Min line count returns `Error` when < 2 lines | Yes | Line 84 checks `< 2`; tested by `zero lines fails` and `one line fails` |
| BDD-034 | Void reason returns `Ok` when `voidedAt = None` regardless of `voidReason` | Yes | Line 88 matches `None, _` to `Ok ()`; tested by `voidedAt None needs no reason` |
| BDD-035 | Void reason returns `Ok` when `voidedAt = Some` and `voidReason = Some` non-empty | Yes | Line 89; tested by `voidedAt Some with non-empty reason passes` |
| BDD-036 | Void reason returns `Error` when `voidedAt = Some` and `voidReason = None` | Yes | Line 90; tested by `voidedAt Some with reason None fails` |
| BDD-037 | Void reason returns `Error` when `voidedAt = Some` and `voidReason = Some ""` | Yes | Line 91 catches `Some _` after the non-empty guard; tested by `voidedAt Some with empty reason fails` |
| BDD-038 | All validation functions return `Result<'T, string list>` | Yes | `validateBalanced`: `Result<JournalEntryLine list, string list>`, `validateAmountsPositive`: same, `validateMinimumLineCount`: same, `validateVoidReason`: `Result<unit, string list>` |
| BDD-039 | All validation functions are pure (no I/O, no mutable state, no exceptions) | Yes | All four functions: input values in, Result out. No `let mutable`, no `printfn`, no `raise`, no file/DB access. List operations and pattern matching only. |

### Deliverable 4: Domain Tests (Tests.fs)

| BDD ID | Description | Verified | Evidence |
|---|---|---|---|
| BDD-040 | Tests.fs contains xUnit tests for every validation function | Yes | 14 tests covering validateBalanced (3), validateAmountsPositive (3), validateMinimumLineCount (4), validateVoidReason (4) |
| BDD-041 | Placeholder test removed | Yes | grep for "placeholder", "sanity", "smoke", "hello" returns no matches in Tests.fs |
| BDD-042 | Test: balanced two-line entry passes balance validation | Yes | `balanced two-line entry passes` test, lines 17-24 |
| BDD-043 | Test: unbalanced entry fails balance validation | Yes | `unbalanced entry fails` test, lines 26-33 |
| BDD-044 | Test: compound entry (3+ lines) passes when balanced | Yes | `balanced compound entry (3+ lines) passes` test, lines 35-43 |
| BDD-045 | Test: positive amount passes amount positivity | Yes | `positive amounts pass` test, lines 47-54 |
| BDD-046 | Test: zero amount fails amount positivity | Yes | `zero amount fails` test, lines 56-63 |
| BDD-047 | Test: negative amount fails amount positivity | Yes | `negative amount fails` test, lines 65-72 |
| BDD-048 | Test: zero lines fails minimum line count | Yes | `zero lines fails` test, lines 76-80 |
| BDD-049 | Test: one line fails minimum line count | Yes | `one line fails` test, lines 82-87 |
| BDD-050 | Test: two lines passes minimum line count | Yes | `two lines passes` test, lines 89-96 |
| BDD-051 | Test: three lines passes minimum line count | Yes | `three lines passes` test, lines 98-106 |
| BDD-052 | Test: `voidedAt = None` needs no void reason | Yes | `voidedAt None needs no reason` test, lines 110-114 |
| BDD-053 | Test: `voidedAt = Some` with non-empty reason passes | Yes | `voidedAt Some with non-empty reason passes` test, lines 116-120 |
| BDD-054 | Test: `voidedAt = Some` with `voidReason = None` fails | Yes | `voidedAt Some with reason None fails` test, lines 122-126 |
| BDD-055 | Test: `voidedAt = Some` with `voidReason = Some ""` fails | Yes | `voidedAt Some with empty reason fails` test, lines 128-132 |
| BDD-056 | All tests use plain xUnit (`[<Fact>]` or `[<Theory>]`), no TickSpec/Gherkin | Yes | Only `[<Fact>]` attributes used; no TickSpec, Gherkin, or BDD infrastructure references |
| BDD-057 | `dotnet test` passes all tests green | Yes | 14/14 passed, 0 failed. Output: "Test Run Successful. Total tests: 14, Passed: 14" |
| BDD-058 | `dotnet build` succeeds with no warnings from Domain or Domain.Tests | Yes | Build succeeded. 0 Warning(s), 0 Error(s). |

---

## Cross-Artifact Traceability (5 spot checks)

| BDD ID | BRD Deliverable | Implementation | Test | Result |
|---|---|---|---|---|
| BDD-011 | D1: JournalEntryLine.entryType uses EntryType DU | Ledger.fs line 59: `entryType: EntryType` | Tests create lines via `makeLine` with `EntryType.Debit`/`Credit` | Chain intact |
| BDD-022 | D2: InstanceStatus DU with 6 cases | Ops.fs lines 11-17: all 6 cases defined | N/A (type definition, no validation test) | Chain intact |
| BDD-030 | D3: Amount positivity Error for zero | Ledger.fs line 77: `l.amount <= 0m` | `zero amount fails` test passes | Chain intact |
| BDD-037 | D3: Void reason Error for empty string | Ledger.fs lines 89-91: `Some _` catch-all after non-empty guard | `voidedAt Some with empty reason fails` test passes | Chain intact |
| BDD-044 | D4: Compound entry (3+ lines) balanced | validateBalanced sums debits/credits regardless of line count | `balanced compound entry (3+ lines) passes` with 3 lines, 14 tests green | Chain intact |

---

## Fabrication Detection

- No citations to nonexistent files found.
- All test output captured from live `dotnet test` run on commit 72526e79.
- Test count matches: 14 tests listed individually in output, 14 total reported by runner.
- Build output shows 0 warnings, 0 errors across all projects.
- No circular evidence: all verifications trace to actual file contents or live command output.

---

## Verdict: APPROVED

All 58/58 BDD criteria verified against actual code and live test output. Evidence chain is solid across all 5 spot checks. No fabrication detected.
