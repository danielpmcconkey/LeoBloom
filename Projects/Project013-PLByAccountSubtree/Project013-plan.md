# Project 013 — P&L by Account Subtree: Implementation Plan

**Author:** Basement Dweller (PO + Planner)
**Date:** 2026-04-04
**Status:** Approved (Dan gave the lead, doing dishes)
**Depends On:** Project 011 (Income Statement) — complete

---

## Objective

Build a filtered P&L report scoped to a specific account subtree. Given a root
account code and a fiscal period, return revenue and expense activity for only
the accounts that descend from (or are) the root account. Same section structure
as the Income Statement (revenue, expenses, net income), but limited to the
subtree.

## Why This Exists

The Income Statement shows ALL revenue and expense accounts for a period. In
practice, you want to ask: "What's the P&L for just my consulting accounts?"
or "Show me operating expenses only." The account hierarchy (`parent_code` FK)
gives us the tree — this project walks it.

## Research Decision

Strong local context. The Income Statement (Project 011) is the template. The
only novel SQL is a recursive CTE for subtree resolution, which is standard
PostgreSQL. No external research needed.

---

## Architecture

### Input
- `accountCode: string` — the root of the subtree
- Fiscal period: by `fiscalPeriodId: int` or `periodKey: string` (dual entry
  points, same pattern as IS)

### Subtree Resolution
Recursive CTE starting from the root account code, walking `parent_code` to
find all descendants. The root itself is included.

```sql
WITH RECURSIVE subtree AS (
    SELECT code FROM ledger.account WHERE code = @root_code
    UNION ALL
    SELECT a.code FROM ledger.account a
    JOIN subtree s ON a.parent_code = s.code
)
SELECT code FROM subtree
```

### Activity Query
Same as `IncomeStatementRepository.getActivityByPeriod`, but with an additional
filter: `AND a.code IN (SELECT code FROM subtree)`. The recursive CTE is
prepended to the existing query.

### Output
Uses the existing `IncomeStatementLine`, `IncomeStatementSection` types. The
report type is new but similar to `IncomeStatementReport`, adding:
- `rootAccountCode` / `rootAccountName` so the caller knows what subtree
  they're looking at
- Same `revenue` / `expenses` / `netIncome` structure

### What If the Root Isn't a Revenue/Expense Account?
If the root is an asset account with revenue/expense children (unlikely but
possible), the children show up in the report. The root itself doesn't, because
it's not revenue or expense. If the root is a revenue account with no children,
you get a one-line revenue section. If the subtree has no revenue/expense
accounts at all, you get an empty report (both sections zero).

### What If the Root Has No Children?
The subtree is just the root account. If it's revenue or expense, you get a
one-line report. If it's neither, you get an empty report. Both are correct.

### Flat Output
The output is flat within each section, same as the IS. No sub-grouping by
intermediate parents in this project. If hierarchical grouping is wanted,
that's a future enhancement — the subtree CTE is already there to build on.

---

## Phases

### Phase 1: Domain Types

**File:** `Src/LeoBloom.Domain/Ledger.fs` (modify — append after Balance Sheet types)

```fsharp
// --- Subtree P&L types ---

type SubtreePLReport =
    { rootAccountCode: string
      rootAccountName: string
      fiscalPeriodId: int
      periodKey: string
      revenue: IncomeStatementSection
      expenses: IncomeStatementSection
      netIncome: decimal }
```

Reuses `IncomeStatementLine` and `IncomeStatementSection` — the line items
are the same structure, just filtered to a subtree. No new line/section types.

### Phase 2: Repository

**File:** `Src/LeoBloom.Utilities/SubtreePLRepository.fs` (create)

```fsharp
module SubtreePLRepository =

    /// Resolve account code to (id, code, name). Returns None if not found.
    let resolveAccount (txn: NpgsqlTransaction) (accountCode: string)
        : (int * string * string) option

    /// Get revenue and expense activity for a fiscal period, filtered to
    /// accounts in the subtree rooted at rootAccountCode.
    let getSubtreeActivityByPeriod
        (txn: NpgsqlTransaction)
        (rootAccountCode: string)
        (fiscalPeriodId: int)
        : (string * IncomeStatementLine) list
```

The `getSubtreeActivityByPeriod` query is the IS query with a recursive CTE
prepended and an `AND a.code IN (SELECT code FROM subtree)` filter added.

**Project file:** Add `<Compile Include="SubtreePLRepository.fs" />` after
`BalanceSheetService.fs` in Utilities `.fsproj`.

### Phase 3: Service

**File:** `Src/LeoBloom.Utilities/SubtreePLService.fs` (create)

```fsharp
module SubtreePLService =

    let getByAccountCodeAndPeriodId
        (accountCode: string)
        (fiscalPeriodId: int)
        : Result<SubtreePLReport, string>

    let getByAccountCodeAndPeriodKey
        (accountCode: string)
        (periodKey: string)
        : Result<SubtreePLReport, string>
```

**Logic:**
1. Open connection, begin transaction.
2. Resolve root account — Error if not found.
3. Validate period exists (reuse `TrialBalanceRepository.periodExists` /
   `resolvePeriodId`) — Error if not found.
4. Call `SubtreePLRepository.getSubtreeActivityByPeriod`.
5. Split into revenue/expense sections (same logic as `IncomeStatementService`).
6. Build `SubtreePLReport`.
7. Commit, return Ok.

**Project file:** Add `<Compile Include="SubtreePLService.fs" />` after
`SubtreePLRepository.fs`.

### Phase 4: Test Helper Enhancement

**File:** `Src/LeoBloom.Tests/TestHelpers.fs` (modify)

Add `insertAccountWithParent` to `InsertHelpers`:

```fsharp
let insertAccountWithParent
    (conn: NpgsqlConnection)
    (tracker: TestCleanup.Tracker)
    (code: string) (name: string) (accountTypeId: int)
    (parentCode: string) (isActive: bool) : int
```

Needed because `insertAccount` doesn't support `parent_code`. Used by all
013 tests to build hierarchies.

### Phase 5: Tests

**File:** `Src/LeoBloom.Tests/SubtreePLTests.fs` (create)

**Tag prefix:** `@FT-SPL`

**Scenarios:**

| ID | Scenario |
|----|----------|
| FT-SPL-001 | Subtree with revenue and expense children produces correct P&L |
| FT-SPL-002 | Subtree with only revenue descendants shows empty expense section |
| FT-SPL-003 | Subtree with only expense descendants shows empty revenue section |
| FT-SPL-004 | Root account with no children returns single-account P&L |
| FT-SPL-005 | Root account that is not revenue/expense with no rev/exp descendants returns empty report |
| FT-SPL-006 | Voided entries excluded from subtree P&L |
| FT-SPL-007 | Accounts outside the subtree are excluded |
| FT-SPL-008 | Multi-level hierarchy (grandchildren) included in subtree |
| FT-SPL-009 | Lookup by period key returns same result as by period ID |
| FT-SPL-010 | Nonexistent account code returns error |
| FT-SPL-011 | Nonexistent period returns error |
| FT-SPL-012 | Empty period for the subtree produces zero net income |

**Structural tests:**
- `SubtreePLReport` type has required fields
- Service functions return `Result<SubtreePLReport, string>`

**Test project file:** Add `<Compile Include="SubtreePLTests.fs" />` after
`BalanceSheetTests.fs`.

---

## Acceptance Criteria

- [ ] `SubtreePLReport` type exists in `Ledger.fs`
- [ ] Reuses `IncomeStatementLine` and `IncomeStatementSection` (no duplication)
- [ ] `SubtreePLService.getByAccountCodeAndPeriodId` works
- [ ] `SubtreePLService.getByAccountCodeAndPeriodKey` works
- [ ] Recursive CTE correctly resolves multi-level subtrees
- [ ] Only revenue/expense accounts in the subtree appear in sections
- [ ] Accounts outside the subtree are excluded even if same account type
- [ ] Voided entries excluded
- [ ] Nonexistent account code returns descriptive error
- [ ] Nonexistent period returns descriptive error
- [ ] Empty subtree activity returns zero net income with empty sections
- [ ] `insertAccountWithParent` helper added to TestHelpers
- [ ] All tests pass, no new warnings
- [ ] Existing 254 tests still pass

---

## Out of Scope

- Hierarchical output (sub-grouping by intermediate parents) — flat within
  each section, same as IS
- Subtree balance sheet — this is P&L only
- API endpoints — cancelled per backlog decision
- Cross-period subtree P&L (comparing periods)

---

## File Summary

| Action | File |
|--------|------|
| Modify | `Src/LeoBloom.Domain/Ledger.fs` |
| Create | `Src/LeoBloom.Utilities/SubtreePLRepository.fs` |
| Create | `Src/LeoBloom.Utilities/SubtreePLService.fs` |
| Modify | `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` |
| Modify | `Src/LeoBloom.Tests/TestHelpers.fs` |
| Create | `Src/LeoBloom.Tests/SubtreePLTests.fs` |
| Modify | `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` |
