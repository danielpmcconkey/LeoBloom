# Project 008 — Trial Balance: Implementation Plan

**Author:** Basement Dweller (Planner role)
**Date:** 2026-04-04
**Status:** Draft — Awaiting alignment
**Depends On:** Project 007 (Account Balance) — complete

---

## Objective

Produce a trial balance report for a fiscal period: sum all debit and credit
activity from non-voided journal entries in that period, grouped by account
type, with an integrity check that total debits equal total credits. This is
period-scoped activity only (not cumulative balance — that is Story 012).

## Research Decision

Strong local context, skipping external research. The repo has a clean
Repository + Service pattern from Project 007, raw Npgsql, and well-established
test conventions. Nothing here warrants looking outside the codebase.

---

## Architecture Overview

The trial balance is fundamentally different from the single-account balance
(Project 007) in two ways:

1. **Scope:** All accounts with activity in a period, not one account as-of a date.
2. **Grouping:** Results grouped by account type with subtotals, not a single scalar.

This means a new SQL query (not reuse of `AccountBalanceRepository.getBalance`),
new domain types, and a new Repository + Service pair. The pattern mirrors
Project 007 exactly.

### Input Resolution

The backlog says input is `fiscal_period_id` or `period_key` like `'2026-03'`.
The `fiscal_period` table has a unique `period_key` column (varchar(7)) and
an integer `id` PK. The service layer will offer both entry points (by ID and
by period key), same pattern as `getBalanceById` / `getBalanceByCode` in
`AccountBalanceService`.

---

## Phases

### Phase 1: Domain Types

**What:** Add trial balance result types to `Ledger.fs`.

**File:** `Src/LeoBloom.Domain/Ledger.fs` (modify — append after `AccountBalance`)

**New types:**

```fsharp
type TrialBalanceAccountLine =
    { accountId: int
      accountCode: string
      accountName: string
      accountTypeName: string
      normalBalance: NormalBalance
      debitTotal: decimal
      creditTotal: decimal
      netBalance: decimal }

type TrialBalanceGroup =
    { accountTypeName: string
      lines: TrialBalanceAccountLine list
      groupDebitTotal: decimal
      groupCreditTotal: decimal }

type TrialBalanceReport =
    { fiscalPeriodId: int
      periodKey: string
      groups: TrialBalanceGroup list
      grandTotalDebits: decimal
      grandTotalCredits: decimal
      isBalanced: bool }
```

**Design notes:**

- `TrialBalanceAccountLine.netBalance` uses the normal_balance formula from 007:
  for normal-debit accounts, `debitTotal - creditTotal`; for normal-credit,
  `creditTotal - debitTotal`. Computed in F# from the raw debit/credit sums,
  same pattern as `AccountBalanceRepository`.
- `isBalanced` is `grandTotalDebits = grandTotalCredits`. This is the integrity
  check. If false, the posting engine (005) has a bug.
- Groups are ordered: asset, liability, equity, revenue, expense (standard
  accounting order). This order is hardcoded in the service layer, not derived
  from the DB.
- `TrialBalanceGroup` carries its own subtotals so the caller doesn't have to
  recompute them.

**Verification:** Project compiles. Types are accessible from Utilities and Tests.

---

### Phase 2: Repository Layer

**What:** New module `TrialBalanceRepository` with a single query function.

**File:** `Src/LeoBloom.Utilities/TrialBalanceRepository.fs` (create)

**Function signature:**

```fsharp
module TrialBalanceRepository =
    let getActivityByPeriod
        (txn: NpgsqlTransaction)
        (fiscalPeriodId: int)
        : TrialBalanceAccountLine list
```

**The SQL query:**

```sql
SELECT
    a.id,
    a.code,
    a.name,
    at.name AS account_type_name,
    at.normal_balance,
    COALESCE(SUM(CASE WHEN jel.entry_type = 'debit'  THEN jel.amount ELSE 0 END), 0) AS debit_total,
    COALESCE(SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END), 0) AS credit_total
FROM ledger.journal_entry je
JOIN ledger.journal_entry_line jel ON jel.journal_entry_id = je.id
JOIN ledger.account a ON jel.account_id = a.id
JOIN ledger.account_type at ON a.account_type_id = at.id
WHERE je.fiscal_period_id = @fiscal_period_id
  AND je.voided_at IS NULL
GROUP BY a.id, a.code, a.name, at.name, at.normal_balance
ORDER BY at.name, a.code
```

**Design notes:**

- This is an INNER JOIN chain, not LEFT JOIN. We only want accounts that have
  activity. An empty period returns an empty list — the service layer handles
  that as `isBalanced = true` with zero totals.
- The `netBalance` is computed in F# after reading each row, not in SQL. Same
  reasoning as Project 007: keep sign convention in application code where it's
  testable.
- A second helper resolves `period_key` to `fiscal_period_id`:

```fsharp
    let resolvePeriodId
        (txn: NpgsqlTransaction)
        (periodKey: string)
        : int option
```

**Query:**

```sql
SELECT id FROM ledger.fiscal_period WHERE period_key = @period_key
```

- A third helper confirms a fiscal_period_id exists (for the by-ID entry point):

```fsharp
    let periodExists
        (txn: NpgsqlTransaction)
        (fiscalPeriodId: int)
        : (int * string) option  // (id, period_key)
```

**Query:**

```sql
SELECT id, period_key FROM ledger.fiscal_period WHERE id = @id
```

**Verification:** Compiles. Unit-testable through the service layer in Phase 4.

**Project file change:** `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` —
add `<Compile Include="TrialBalanceRepository.fs" />` after the
`AccountBalanceService.fs` entry. (F# compile order matters.)

---

### Phase 3: Service Layer

**What:** New module `TrialBalanceService` with two entry points.

**File:** `Src/LeoBloom.Utilities/TrialBalanceService.fs` (create)

**Function signatures:**

```fsharp
module TrialBalanceService =
    let getByPeriodId
        (fiscalPeriodId: int)
        : Result<TrialBalanceReport, string>

    let getByPeriodKey
        (periodKey: string)
        : Result<TrialBalanceReport, string>
```

**Logic (both entry points converge):**

1. Open connection, begin transaction (same pattern as `AccountBalanceService`).
2. Validate period exists (by ID or resolve by key). Return `Error` if not found.
3. Call `TrialBalanceRepository.getActivityByPeriod` with the resolved ID.
4. Group the `TrialBalanceAccountLine list` by `accountTypeName`.
5. Build `TrialBalanceGroup` list in standard accounting order:
   `["asset"; "liability"; "equity"; "revenue"; "expense"]`.
   Groups with no activity are omitted (not included as empty groups).
6. Compute `grandTotalDebits` and `grandTotalCredits` by summing across all lines.
7. Set `isBalanced = (grandTotalDebits = grandTotalCredits)`.
8. Commit transaction, return `Ok report`.
9. On exception: rollback, log, return `Error`.

**Design notes:**

- The standard account type order is a `let private` list in the service module.
  Not configurable, not in domain types. It's a presentation concern.
- Logging follows the existing pattern: `Log.info` at entry, `Log.errorExn` on
  failure.
- The service layer is the only place that assembles the full
  `TrialBalanceReport`. The repository returns flat rows; the service groups them.

**Project file change:** `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` —
add `<Compile Include="TrialBalanceService.fs" />` after
`TrialBalanceRepository.fs`.

**Verification:** Compiles. Callable from tests in Phase 4.

---

### Phase 4: Tests

**What:** New test file `TrialBalanceTests.fs` with behavioral tests mapped to
Gherkin scenarios. New Gherkin feature file `TrialBalance.feature`.

**Files:**
- `Src/LeoBloom.Tests/TrialBalanceTests.fs` (create)
- `Specs/Behavioral/TrialBalance.feature` (create)

**Tag prefix:** `@FT-TB` (Trial Balance)

**Test project file change:** `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` —
add `<Compile Include="TrialBalanceTests.fs" />` after
`AccountBalanceTests.fs`.

**Test scenarios:**

| Tag | Scenario | Setup | Assertion |
|-----|----------|-------|-----------|
| FT-TB-001 | Trial balance for period with balanced entries | Create 2 accounts (asset + revenue), 1 fiscal period, 1 journal entry (debit asset 500, credit revenue 500) | `isBalanced = true`, `grandTotalDebits = 500`, `grandTotalCredits = 500`, 2 account lines, grouped correctly |
| FT-TB-002 | Trial balance groups by account type with subtotals | Create 4 accounts across 3 types (asset, revenue, expense), 2 entries | Groups present in order, group subtotals match line sums |
| FT-TB-003 | Voided entries excluded from trial balance | Create 2 entries, void one | Report reflects only the non-voided entry |
| FT-TB-004 | Empty period returns balanced report with zero totals | Create a fiscal period with no entries | `isBalanced = true`, `grandTotalDebits = 0`, `grandTotalCredits = 0`, `groups = []` |
| FT-TB-005 | Closed period trial balance still works | Create a fiscal period with `is_open = false`, post entries before closing | Report returns normally |
| FT-TB-006 | Lookup by period_key returns same result as by ID | Create period, post entries, call both entry points | Results are identical |
| FT-TB-007 | Nonexistent period ID returns error | Call with ID 999999 | `Error` containing "does not exist" |
| FT-TB-008 | Nonexistent period key returns error | Call with key "9999-99" | `Error` containing "does not exist" |
| FT-TB-009 | Net balance uses normal_balance formula | Create normal-debit and normal-credit accounts, post entry | `netBalance` is positive for both (since each is on its normal side) |
| FT-TB-010 | Multiple entries in same period accumulate per account | Post 2 entries hitting the same accounts | Account lines show cumulative debit/credit totals |

**Test structure follows existing patterns:**
- Each test creates its own data with `TestData.uniquePrefix()`.
- Uses `InsertHelpers` for setup (account types, accounts, fiscal periods).
- Uses `JournalEntryService.post` for posting entries (same as AccountBalanceTests).
- Cleanup via `TestCleanup.track` / `TestCleanup.deleteAll` in `try/finally`.
- Calls `TrialBalanceService.getByPeriodId` / `getByPeriodKey` as the system
  under test.

**Note on FT-TB-005 (closed period):** The test will insert the fiscal period
with `is_open = true`, post the entry (since `JournalEntryService.post`
validates the period is open), then UPDATE the period to `is_open = false`
before running the trial balance query. Same pattern as the inactive account
test in AccountBalanceTests.

**Verification:** `dotnet test` passes. All 10 scenarios green.

---

## Acceptance Criteria

- [ ] `TrialBalanceReport` type exists in `Ledger.fs` with fields: `fiscalPeriodId`, `periodKey`, `groups`, `grandTotalDebits`, `grandTotalCredits`, `isBalanced`
- [ ] `TrialBalanceService.getByPeriodId` returns `Result<TrialBalanceReport, string>`
- [ ] `TrialBalanceService.getByPeriodKey` returns `Result<TrialBalanceReport, string>`
- [ ] Period with balanced entries: `isBalanced = true` and totals match
- [ ] Period with no entries: `isBalanced = true`, zero totals, empty groups
- [ ] Voided entries are excluded from the trial balance
- [ ] Closed periods return a valid trial balance (read-only operation)
- [ ] Report groups accounts by type in standard order (asset, liability, equity, revenue, expense)
- [ ] Each group has correct subtotals
- [ ] Net balance per account uses the normal_balance formula (debit-normal: debits - credits; credit-normal: credits - debits)
- [ ] Nonexistent period (by ID or key) returns descriptive error
- [ ] All 10 test scenarios pass via `dotnet test`
- [ ] No new warnings introduced

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Decimal precision mismatch between SQL `numeric(12,2)` and F# `decimal` | Low | Medium | SQL uses `numeric(12,2)`, F# `decimal` is 128-bit. No precision loss. But assert with exact `=` not epsilon. |
| `isBalanced` check fails on legitimate data due to floating-point | None | N/A | Both sides are `numeric(12,2)` sums. Decimal equality is exact. Not a real risk. |
| Account type names change or new types added | Low | Low | The grouping order list is hardcoded. If a new type is added to the DB, it would appear at the end (ungrouped accounts fall through). Flag this as a future concern, not a P008 problem. |
| F# compile order: new files must appear after their dependencies in .fsproj | Medium | High | Plan specifies exact insertion points. Builder must follow. |

## Out of Scope

- **Cumulative balance (balance sheet)** — that's Story 012.
- **API endpoints** — no HTTP layer. Service + Repository only.
- **Parent/subtree aggregation** — account 5000's trial balance line shows only
  direct activity on 5000, not child accounts.
- **Formatted output** — the service returns a data structure. Rendering to
  text/HTML/PDF is a future concern.
- **Performance optimization** — no indexes, no caching. The query is a
  single-pass aggregate. Optimize if it becomes a problem.

## File Summary

| Action | File |
|--------|------|
| Modify | `Src/LeoBloom.Domain/Ledger.fs` — append 3 types |
| Create | `Src/LeoBloom.Utilities/TrialBalanceRepository.fs` |
| Create | `Src/LeoBloom.Utilities/TrialBalanceService.fs` |
| Modify | `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` — add 2 Compile entries |
| Create | `Src/LeoBloom.Tests/TrialBalanceTests.fs` |
| Modify | `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` — add 1 Compile entry |
| Create | `Specs/Behavioral/TrialBalance.feature` |
