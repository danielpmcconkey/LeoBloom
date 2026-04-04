# Project 011-012 — Income Statement & Balance Sheet: Implementation Plan

**Author:** Basement Dweller (Planner role)
**Date:** 2026-04-04
**Status:** Draft — Awaiting alignment
**Depends On:** Project 008 (Trial Balance) — complete

---

## Objective

Build two financial reports: the Income Statement (revenue minus expenses for
a fiscal period) and the Balance Sheet (assets, liabilities, equity at a point
in time). These are built together because the Balance Sheet's Retained
Earnings line is computed from the same revenue/expense data the Income
Statement reports on.

## Research Decision

Strong local context, skipping external research. The Trial Balance (Project
008) established the exact Repository + Service + domain type pattern we'll
follow. The only novel SQL is the Balance Sheet's cumulative query, and
`AccountBalanceRepository.getBalance` already demonstrates the
`entry_date <= @as_of_date` pattern we need.

---

## Architecture Overview

### Income Statement (Project 011)

Period-scoped, like the Trial Balance. The key difference: it filters to
revenue and expense accounts only, and instead of debit/credit columns, it
computes a single "balance" per account using the normal_balance formula
(revenue: credits - debits; expenses: debits - credits). The bottom line is
Net Income = Total Revenue - Total Expenses.

**Input:** `fiscal_period_id` or `period_key` (same dual-entry-point pattern
as Trial Balance).

### Balance Sheet (Project 012)

Point-in-time cumulative snapshot. NOT period-scoped — sums ALL non-voided
journal entry lines where `entry_date <= as_of_date`, filtered to asset,
liability, and equity accounts. Plus a computed Retained Earnings line that
sums all revenue and expense activity through the same date.

**Input:** `as_of_date: DateOnly` (no period dependency — this is a date,
not a fiscal period).

### Why They're One Project

Retained Earnings on the Balance Sheet = all-time Net Income. The Income
Statement computes period Net Income. Both need the same "sum activity by
account type with normal_balance awareness" logic. Building them together
avoids the dumb situation where you build 012 and realize the domain types
from 011 should have been designed differently.

### Shared vs. Separate

Despite the conceptual overlap, the repositories will be **separate files**.
The SQL queries are meaningfully different (period-scoped vs. cumulative,
different account type filters, different join strategies). Trying to share
a "generic" query function would create abstraction that serves nobody.
The domain types share `NormalBalance` from `Ledger.fs` — that's enough.

---

## Phases

### Phase 1: Domain Types

**What:** Add Income Statement and Balance Sheet result types to `Ledger.fs`.

**File:** `Src/LeoBloom.Domain/Ledger.fs` (modify — append after
`TrialBalanceReport`)

**New types:**

```fsharp
// --- Income Statement types ---

type IncomeStatementLine =
    { accountId: int
      accountCode: string
      accountName: string
      balance: decimal }

type IncomeStatementSection =
    { sectionName: string          // "revenue" or "expense"
      lines: IncomeStatementLine list
      sectionTotal: decimal }

type IncomeStatementReport =
    { fiscalPeriodId: int
      periodKey: string
      revenue: IncomeStatementSection
      expenses: IncomeStatementSection
      netIncome: decimal }

// --- Balance Sheet types ---

type BalanceSheetLine =
    { accountId: int
      accountCode: string
      accountName: string
      balance: decimal }

type BalanceSheetSection =
    { sectionName: string          // "asset", "liability", or "equity"
      lines: BalanceSheetLine list
      sectionTotal: decimal }

type BalanceSheetReport =
    { asOfDate: DateOnly
      assets: BalanceSheetSection
      liabilities: BalanceSheetSection
      equity: BalanceSheetSection
      retainedEarnings: decimal
      totalEquity: decimal         // equity.sectionTotal + retainedEarnings
      isBalanced: bool }           // assets.sectionTotal = liabilities.sectionTotal + totalEquity
```

**Design notes:**

- `IncomeStatementLine.balance` is the net activity for the period, computed
  using the normal_balance formula. Revenue accounts show positive when
  credits > debits. Expense accounts show positive when debits > credits.
  This means both sections show positive numbers in the normal case.

- `IncomeStatementReport` has explicit `revenue` and `expenses` sections
  (not a generic list of groups like Trial Balance). There are exactly two
  sections on an Income Statement, always. If one has no activity, it still
  exists with `sectionTotal = 0m` and `lines = []`.

- `BalanceSheetLine` and `IncomeStatementLine` have identical shape. They
  are separate types anyway — they mean different things. No shared base
  type, no DU, no cleverness. If they diverge later (and they will when
  we add subcategories), we're not fighting a shared abstraction.

- `BalanceSheetReport.retainedEarnings` is computed, not from a GL account.
  It's the all-time sum of (revenue credits - revenue debits) minus
  (expense debits - expense credits) through `asOfDate`. In plain English:
  cumulative net income.

- `BalanceSheetReport.totalEquity` = equity section total + retained
  earnings. This is a convenience field so the caller can check the
  accounting equation without recomputing.

- `BalanceSheetReport.isBalanced` checks the accounting equation:
  `assets.sectionTotal = liabilities.sectionTotal + totalEquity`.

- Balance Sheet has no `fiscalPeriodId` because it's date-based, not
  period-based.

- **Zero-balance accounts with activity must display.** If an account has
  had transactions that net to zero, it still appears on the balance sheet
  with `balance = 0m`. A checking account at $0 is still a checking account
  you own — the zero balance is information, not absence. This is GAAP-
  aligned. Only accounts with NO activity at all are excluded (handled
  naturally by the INNER JOIN).

**Verification:** Project compiles. Types accessible from Utilities and Tests.

---

### Phase 2: Income Statement Repository

**What:** New module `IncomeStatementRepository` with period-scoped query
filtered to revenue and expense accounts.

**File:** `Src/LeoBloom.Utilities/IncomeStatementRepository.fs` (create)

**Functions:**

```fsharp
module IncomeStatementRepository =

    /// Get revenue and expense activity for a fiscal period.
    /// Returns raw lines — service layer splits into sections.
    let getActivityByPeriod
        (txn: NpgsqlTransaction)
        (fiscalPeriodId: int)
        : (string * IncomeStatementLine) list
        // Returns tuples of (accountTypeName, line) so the service
        // can split into revenue vs. expense sections.
```

**SQL query:**

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
  AND at.name IN ('revenue', 'expense')
GROUP BY a.id, a.code, a.name, at.name, at.normal_balance
ORDER BY at.name, a.code
```

**F# computation of `balance`:** Same normal_balance formula as Trial
Balance. Revenue (normal-credit): `creditTotal - debitTotal`. Expense
(normal-debit): `debitTotal - creditTotal`. The repo computes this and
returns it as `IncomeStatementLine.balance`.

**Period resolution:** Reuses `TrialBalanceRepository.resolvePeriodId` and
`TrialBalanceRepository.periodExists`. No duplication — these are generic
fiscal period lookups that don't belong to Trial Balance conceptually.
They just live there. If this bothers anyone, we can extract a
`FiscalPeriodLookup` module later, but not now.

**Project file change:** `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` —
add `<Compile Include="IncomeStatementRepository.fs" />` after
`TrialBalanceService.fs`.

**Verification:** Compiles.

---

### Phase 3: Income Statement Service

**What:** New module `IncomeStatementService` with dual entry points.

**File:** `Src/LeoBloom.Utilities/IncomeStatementService.fs` (create)

**Function signatures:**

```fsharp
module IncomeStatementService =

    let getByPeriodId
        (fiscalPeriodId: int)
        : Result<IncomeStatementReport, string>

    let getByPeriodKey
        (periodKey: string)
        : Result<IncomeStatementReport, string>
```

**Logic:**

1. Open connection, begin transaction.
2. Validate period exists (reuse `TrialBalanceRepository.periodExists` /
   `resolvePeriodId`). Return `Error` if not found.
3. Call `IncomeStatementRepository.getActivityByPeriod`.
4. Split results by `accountTypeName`:
   - Revenue lines -> `revenue` section
   - Expense lines -> `expenses` section
5. Build each section: lines list + `sectionTotal` (sum of `balance` values).
6. `netIncome = revenue.sectionTotal - expenses.sectionTotal`.
7. If a section has no lines, it's still present with total = 0 and
   empty lines list.
8. Accounts with zero activity don't appear (the INNER JOIN in the query
   handles this — no activity means no rows).
9. Commit, return `Ok report`.
10. On exception: rollback, log, return `Error`.

**Project file change:** Add `<Compile Include="IncomeStatementService.fs" />`
after `IncomeStatementRepository.fs` in `.fsproj`.

**Verification:** Compiles. Callable from tests.

---

### Phase 4: Balance Sheet Repository

**What:** New module `BalanceSheetRepository` with cumulative queries.

**File:** `Src/LeoBloom.Utilities/BalanceSheetRepository.fs` (create)

**Functions:**

```fsharp
module BalanceSheetRepository =

    /// Get cumulative balances for asset, liability, and equity accounts
    /// through as_of_date.
    let getCumulativeBalances
        (txn: NpgsqlTransaction)
        (asOfDate: DateOnly)
        : (string * BalanceSheetLine) list
        // Returns tuples of (accountTypeName, line)

    /// Get retained earnings (cumulative net income) through as_of_date.
    /// This is: sum of all revenue activity - sum of all expense activity.
    let getRetainedEarnings
        (txn: NpgsqlTransaction)
        (asOfDate: DateOnly)
        : decimal
```

**SQL for `getCumulativeBalances`:**

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
WHERE je.entry_date <= @as_of_date
  AND je.voided_at IS NULL
  AND at.name IN ('asset', 'liability', 'equity')
GROUP BY a.id, a.code, a.name, at.name, at.normal_balance
ORDER BY at.name, a.code
```

**Key differences from Trial Balance query:**
- `je.entry_date <= @as_of_date` instead of `je.fiscal_period_id = @fp_id`
  (cumulative, not period-scoped)
- `at.name IN ('asset', 'liability', 'equity')` filter
- **No HAVING clause.** Accounts with activity that nets to zero still
  appear on the balance sheet with a zero balance. GAAP requires this —
  a checking account at $0 is still a checking account you own. The zero
  balance is information. The INNER JOIN already excludes accounts with
  NO activity at all, which is the correct filter.

**F# computation of `balance`:**
- Asset (normal-debit): `debitTotal - creditTotal`
- Liability (normal-credit): `creditTotal - debitTotal`
- Equity (normal-credit): `creditTotal - debitTotal`

**SQL for `getRetainedEarnings`:**

```sql
SELECT
    COALESCE(SUM(
        CASE WHEN at.normal_balance = 'credit'
             THEN (CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE -jel.amount END)
             ELSE (CASE WHEN jel.entry_type = 'debit'  THEN jel.amount ELSE -jel.amount END)
        END
    ), 0)
FROM ledger.journal_entry je
JOIN ledger.journal_entry_line jel ON jel.journal_entry_id = je.id
JOIN ledger.account a ON jel.account_id = a.id
JOIN ledger.account_type at ON a.account_type_id = at.id
WHERE je.entry_date <= @as_of_date
  AND je.voided_at IS NULL
  AND at.name IN ('revenue', 'expense')
```

**Wait — that's ugly.** Let me simplify. Retained Earnings = total revenue
balance - total expense balance. Revenue balance = credits - debits.
Expense balance = debits - credits. So:

```sql
SELECT
    COALESCE(SUM(
        CASE
            WHEN at.name = 'revenue' THEN
                CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE -jel.amount END
            WHEN at.name = 'expense' THEN
                -(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE -jel.amount END)
        END
    ), 0) AS retained_earnings
FROM ledger.journal_entry je
JOIN ledger.journal_entry_line jel ON jel.journal_entry_id = je.id
JOIN ledger.account a ON jel.account_id = a.id
JOIN ledger.account_type at ON a.account_type_id = at.id
WHERE je.entry_date <= @as_of_date
  AND je.voided_at IS NULL
  AND at.name IN ('revenue', 'expense')
```

**Actually, let's just do this in F# instead.** The SQL gymnastics aren't
worth it. The cleaner approach:

```fsharp
let getRetainedEarnings (txn: NpgsqlTransaction) (asOfDate: DateOnly) : decimal =
    // Query: get cumulative debit_total and credit_total for all
    // revenue and expense accounts through asOfDate.
    // Same query shape as getCumulativeBalances but filtered to
    // revenue/expense instead of asset/liability/equity.
    // Then compute in F#:
    //   revenueBalance = sum(creditTotal - debitTotal) for revenue accounts
    //   expenseBalance = sum(debitTotal - creditTotal) for expense accounts
    //   retainedEarnings = revenueBalance - expenseBalance
```

**SQL for `getRetainedEarnings` (simplified):**

```sql
SELECT
    at.name AS account_type_name,
    COALESCE(SUM(CASE WHEN jel.entry_type = 'debit'  THEN jel.amount ELSE 0 END), 0) AS debit_total,
    COALESCE(SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END), 0) AS credit_total
FROM ledger.journal_entry je
JOIN ledger.journal_entry_line jel ON jel.journal_entry_id = je.id
JOIN ledger.account a ON jel.account_id = a.id
JOIN ledger.account_type at ON a.account_type_id = at.id
WHERE je.entry_date <= @as_of_date
  AND je.voided_at IS NULL
  AND at.name IN ('revenue', 'expense')
GROUP BY at.name
```

This returns at most 2 rows (one for revenue, one for expense). F# computes:
```
revenue net = credit_total - debit_total (for the revenue row)
expense net = debit_total - credit_total (for the expense row)
retained_earnings = revenue_net - expense_net
```

If no revenue/expense activity exists, the query returns 0 rows and the
function returns `0m`.

**Project file change:** Add `<Compile Include="BalanceSheetRepository.fs" />`
after `IncomeStatementService.fs` in `.fsproj`.

**Verification:** Compiles.

---

### Phase 5: Balance Sheet Service

**What:** New module `BalanceSheetService` with a single entry point.

**File:** `Src/LeoBloom.Utilities/BalanceSheetService.fs` (create)

**Function signature:**

```fsharp
module BalanceSheetService =

    let getAsOfDate
        (asOfDate: DateOnly)
        : Result<BalanceSheetReport, string>
```

**Logic:**

1. Open connection, begin transaction.
2. Call `BalanceSheetRepository.getCumulativeBalances txn asOfDate`.
3. Split results by `accountTypeName` into asset, liability, equity sections.
4. Build each section: lines + `sectionTotal` (sum of `balance` values).
5. Call `BalanceSheetRepository.getRetainedEarnings txn asOfDate`.
6. `totalEquity = equity.sectionTotal + retainedEarnings`.
7. `isBalanced = (assets.sectionTotal = liabilities.sectionTotal + totalEquity)`.
8. Empty sections (no accounts with balances) still exist with total = 0
   and empty lines list.
9. Commit, return `Ok report`.
10. On exception: rollback, log, return `Error`.

**No period validation needed.** The Balance Sheet takes a date, not a period.
Any valid `DateOnly` is acceptable. Before any entries exist, you just get all
zeros (balanced).

**No "by period key" entry point.** The Balance Sheet is date-based. If the
caller wants the balance sheet as of the end of March, they pass
`DateOnly(2026, 3, 31)`, not a period key. This is intentional — balance
sheets don't belong to fiscal periods.

**Project file change:** Add `<Compile Include="BalanceSheetService.fs" />`
after `BalanceSheetRepository.fs` in `.fsproj`.

**Verification:** Compiles. Callable from tests.

---

### Phase 6: Gherkin Specs

**What:** Two new feature files for the Gherkin Writer to flesh out.

**Files:**
- `Specs/Behavioral/IncomeStatement.feature` (create)
- `Specs/Behavioral/BalanceSheet.feature` (create)

**Tag prefixes:** `@FT-IS` (Income Statement), `@FT-BS` (Balance Sheet)

**Income Statement scenario categories for Gherkin Writer:**

| Category | What to cover |
|----------|--------------|
| Happy path | Period with revenue and expense activity produces correct net income |
| Revenue only | Period with only revenue accounts having activity |
| Expenses only | Period with only expense accounts having activity |
| Voided entries | Voided entries excluded from income statement |
| Empty period | Period with no revenue or expense activity produces zero net income |
| Multiple accounts | Multiple revenue and expense accounts accumulate correctly |
| Net income positive | Revenue > Expenses |
| Net income negative (net loss) | Expenses > Revenue |
| Lookup by period key | Same result as by period ID |
| Nonexistent period | Returns error |
| Normal balance formula | Revenue balance = credits - debits; expense balance = debits - credits |
| Zero-activity accounts omitted | Accounts with no activity in the period don't appear in sections |
| Inactive accounts with activity | Inactive accounts that have entries in the period still appear |

**Balance Sheet scenario categories for Gherkin Writer:**

| Category | What to cover |
|----------|--------------|
| Happy path | Balanced books produce isBalanced = true |
| Accounting equation | Assets = Liabilities + Equity (including retained earnings) |
| Retained earnings positive | Revenue > expenses all-time |
| Retained earnings negative | Expenses > revenue all-time (net loss) |
| Retained earnings zero | No revenue/expense activity |
| Before any entries | All zeros, isBalanced = true |
| Voided entries excluded | Voided entries don't affect balances |
| Cumulative nature | Entries across multiple fiscal periods all contribute |
| Multiple accounts per section | Multiple assets, liabilities, equity accounts |
| Zero-balance accounts with activity | Accounts where activity nets to zero still appear with balance = 0 (GAAP) |
| Inactive accounts with balances | Inactive accounts that have cumulative balances still appear |

---

### Phase 7: Tests

**What:** Two new test files implementing the Gherkin scenarios.

**Files:**
- `Src/LeoBloom.Tests/IncomeStatementTests.fs` (create)
- `Src/LeoBloom.Tests/BalanceSheetTests.fs` (create)

**Test project file changes:**
- Add `<Compile Include="IncomeStatementTests.fs" />` after
  `TrialBalanceTests.fs`
- Add `<Compile Include="BalanceSheetTests.fs" />` after
  `IncomeStatementTests.fs`

**Test patterns (same as TrialBalanceTests):**
- Each test creates own data with `TestData.uniquePrefix()`
- `InsertHelpers` for accounts, fiscal periods
- `JournalEntryService.post` for posting entries
- `TestCleanup.track` / `TestCleanup.deleteAll` in `try/finally`
- Behavioral tests get `[<Trait("GherkinId", "FT-IS-NNN")>]` or
  `[<Trait("GherkinId", "FT-BS-NNN")>]`
- Structural tests verify type shapes and return types (no Gherkin mapping)

**Income Statement test notes:**

- Standard seeded type IDs: revenue = 4, expense = 5. Tests use these
  directly (same as TrialBalanceTests).
- To test "revenue only" and "expenses only," create entries that only
  touch accounts of one type. But wait — double-entry requires balanced
  debits and credits. So "revenue only" means the OTHER side is an asset
  account, but the income statement filters to revenue/expense only. The
  asset side won't appear on the income statement. This is the correct
  behavior.
- Net loss test: post more expenses than revenue in a period.

**Balance Sheet test notes:**

- Cumulative nature is the big new thing to test. Create entries across
  multiple fiscal periods, then call `getAsOfDate` with a date that
  captures all of them. This requires creating multiple fiscal periods
  with different date ranges.
- Retained Earnings test: create revenue and expense entries, then verify
  `retainedEarnings` on the balance sheet equals cumulative net income.
- `isBalanced` should ALWAYS be true if the posting engine (Project 005) is
  doing its job. A test that verifies `isBalanced = true` after posting
  balanced entries is a sanity check, not an edge case.
- "Before any entries" test: call `getAsOfDate` with any date when no
  entries exist. All sections should have zero totals, empty lines, and
  `isBalanced = true`.

**Structural tests for both reports:**
- Verify type shapes (construct instances, assert field access)
- Verify service return types are `Result<Report, string>`

**Verification:** `dotnet test` passes. All scenarios green.

---

## Acceptance Criteria

### Income Statement (Project 011)

- [ ] `IncomeStatementReport` type exists in `Ledger.fs` with fields: `fiscalPeriodId`, `periodKey`, `revenue`, `expenses`, `netIncome`
- [ ] `IncomeStatementService.getByPeriodId` returns `Result<IncomeStatementReport, string>`
- [ ] `IncomeStatementService.getByPeriodKey` returns `Result<IncomeStatementReport, string>`
- [ ] Revenue section contains only revenue accounts; expenses section contains only expense accounts
- [ ] Revenue balance positive when credits > debits; expense balance positive when debits > credits
- [ ] `netIncome = revenue.sectionTotal - expenses.sectionTotal`
- [ ] Period with no activity: both sections exist with zero totals and empty lines
- [ ] Accounts with zero activity in the period are omitted from their section
- [ ] Inactive accounts with activity in the period are included
- [ ] Voided entries are excluded
- [ ] Nonexistent period (by ID or key) returns descriptive error
- [ ] All Income Statement test scenarios pass

### Balance Sheet (Project 012)

- [ ] `BalanceSheetReport` type exists in `Ledger.fs` with fields: `asOfDate`, `assets`, `liabilities`, `equity`, `retainedEarnings`, `totalEquity`, `isBalanced`
- [ ] `BalanceSheetService.getAsOfDate` returns `Result<BalanceSheetReport, string>`
- [ ] Asset balances = debits - credits (normal-debit); liability and equity balances = credits - debits (normal-credit)
- [ ] `retainedEarnings` = cumulative (revenue credits - revenue debits) - (expense debits - expense credits) through `asOfDate`
- [ ] `totalEquity` = equity section total + retained earnings
- [ ] `isBalanced` = assets total equals liabilities total + total equity
- [ ] Before any entries: all zeros, `isBalanced = true`
- [ ] Negative retained earnings displayed correctly (not an error)
- [ ] Cumulative: entries across multiple periods contribute to balances
- [ ] Voided entries excluded
- [ ] Accounts with activity that nets to zero still appear with balance = 0 (GAAP-aligned)
- [ ] Inactive accounts with balances are included
- [ ] All Balance Sheet test scenarios pass

### Shared

- [ ] No new compiler warnings
- [ ] All existing tests still pass (regression)
- [ ] F# compile order correct in both .fsproj files

---

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Retained earnings computation disagrees with income statement for same period | Medium | High | Test explicitly: post entries, run income statement for the period, run balance sheet as of period end date, verify retained earnings equals net income. This is the integration test that ties the two reports together. |
| Accounts with offsetting activity shown at zero balance | Low | Low | GAAP-aligned: accounts with activity that nets to zero still appear on the balance sheet. The INNER JOIN excludes accounts with no activity. Verify with a test that posts offsetting entries and confirms the account appears with balance = 0. |
| Balance sheet `entry_date <= @as_of_date` may include entries in future fiscal periods | Low | Low | This is correct behavior. The balance sheet is date-based, not period-based. If someone posted an entry dated 2026-03-15 into a Q2 fiscal period, the March 31 balance sheet should include it. The entry_date is what matters. |
| Reusing `TrialBalanceRepository.periodExists` / `resolvePeriodId` creates a cross-module dependency | Low | Low | These are simple lookup functions. If it becomes a problem, extract to a shared module. Not worth the ceremony now. |
| F# compile order in .fsproj | Medium | High | Plan specifies exact insertion points. Builder must add files in the correct order. Income Statement files must appear after Trial Balance (because they call `TrialBalanceRepository`). Balance Sheet files must appear after Income Statement files. |

---

## Out of Scope

- **API endpoints** — no HTTP layer. Service + Repository only.
- **Closing entries** — retained earnings is computed on the fly, not via
  closing journal entries. Closing entries are a future project.
- **Comparative statements** — no "this period vs. last period" comparison.
- **Subcategories** — no current assets vs. non-current assets, no COGS
  vs. operating expenses. Flat list within each section.
- **Formatted output** — returns data structures. Rendering is future work.
- **Parent/subtree aggregation** — each account shows its own balance only.

---

## File Summary

| Action | File | Notes |
|--------|------|-------|
| Modify | `Src/LeoBloom.Domain/Ledger.fs` | Append 6 new types after `TrialBalanceReport` |
| Create | `Src/LeoBloom.Utilities/IncomeStatementRepository.fs` | Period-scoped revenue/expense query |
| Create | `Src/LeoBloom.Utilities/IncomeStatementService.fs` | Dual entry points, section building |
| Create | `Src/LeoBloom.Utilities/BalanceSheetRepository.fs` | Cumulative balance + retained earnings queries |
| Create | `Src/LeoBloom.Utilities/BalanceSheetService.fs` | Single entry point, accounting equation check |
| Modify | `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` | Add 4 Compile entries after `TrialBalanceService.fs` |
| Create | `Specs/Behavioral/IncomeStatement.feature` | Gherkin Writer fills in scenarios |
| Create | `Specs/Behavioral/BalanceSheet.feature` | Gherkin Writer fills in scenarios |
| Create | `Src/LeoBloom.Tests/IncomeStatementTests.fs` | Behavioral + structural tests |
| Create | `Src/LeoBloom.Tests/BalanceSheetTests.fs` | Behavioral + structural tests |
| Modify | `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` | Add 2 Compile entries after `TrialBalanceTests.fs` |

### .fsproj Compile Order (Utilities)

After modification, the Compile items should be:
```xml
<Compile Include="Log.fs" />
<Compile Include="DataSource.fs" />
<Compile Include="JournalEntryRepository.fs" />
<Compile Include="FiscalPeriodRepository.fs" />
<Compile Include="FiscalPeriodService.fs" />
<Compile Include="JournalEntryService.fs" />
<Compile Include="AccountBalanceRepository.fs" />
<Compile Include="AccountBalanceService.fs" />
<Compile Include="TrialBalanceRepository.fs" />
<Compile Include="TrialBalanceService.fs" />
<Compile Include="IncomeStatementRepository.fs" />
<Compile Include="IncomeStatementService.fs" />
<Compile Include="BalanceSheetRepository.fs" />
<Compile Include="BalanceSheetService.fs" />
```

### .fsproj Compile Order (Tests)

After modification, add after `TrialBalanceTests.fs`:
```xml
<Compile Include="IncomeStatementTests.fs" />
<Compile Include="BalanceSheetTests.fs" />
```
