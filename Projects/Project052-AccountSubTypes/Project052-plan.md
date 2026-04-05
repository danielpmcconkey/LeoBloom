# Project 052 — Account Sub-Type Classification — Plan

## Objective

Add an `account_subtype` classification to `ledger.account` so reporting
(and future features) can identify account categories — starting with
cash vs non-cash assets for P040's cash flow reports. The subtype is a
nullable varchar column policed by an F# discriminated union and a
type-to-subtype validation function. No lookup table, no FK.

## Design Decisions

- **Nullable column.** Header/parent accounts get null. Leaf accounts
  get subtypes where meaningful. Null on a leaf means "unclassified."
- **No lookup table.** Same pattern as other constrained values in the
  codebase — the DU is the source of truth, the DB stores the string.
- **Flat DU with validation function.** The DU defines all valid
  subtypes across all account types. A separate function maps each
  account_type to its valid subtypes. Every write path calls it.
- **No equity subtypes yet.** Equity accounts stay null until there's a
  reporting need for Contributed Capital / Retained Earnings / Draws.
- **Subtype carries no semantic weight.** It's a classification label,
  not a behavioral flag like `normal_balance` on `account_type`.

### Valid Subtypes by Account Type

| account_type | Valid subtypes | Notes |
|---|---|---|
| Asset | Cash, FixedAsset, Investment | Cash = bank/checking/savings. FixedAsset = property. Investment = brokerage/securities. |
| Liability | CurrentLiability, LongTermLiability | Current = credit card, AP. Long-term = mortgage. |
| Equity | *(none — null only)* | Defer until reporting demands it. |
| Revenue | OperatingRevenue, OtherRevenue | Operating = rental income, reimbursements. Other = interest income. |
| Expense | OperatingExpense, OtherExpense | Operating = property/personal expenses. Other = bank fees, investment fees. |

### Seed Data Assignments

Verified against `1712000006000_SeedChartOfAccounts.sql`:

| Code | Name | Type | Subtype |
|---|---|---|---|
| 1000 | Assets | Asset | null (header) |
| 1100 | Property Assets | Asset | null (header) |
| 1110 | Bank A — Operating | Asset | Cash |
| 1120 | Bank B — Deposits | Asset | Cash |
| 1200 | Personal Assets | Asset | null (header) |
| 1210 | Brokerage Account | Asset | Investment |
| 1220 | Bank C — Checking | Asset | Cash |
| 2000 | Liabilities | Liability | null (header) |
| 2100 | Property Liabilities | Liability | null (header) |
| 2110 | Mortgage Payable — Rental | Liability | LongTermLiability |
| 2200 | Personal Liabilities | Liability | null (header) |
| 2210 | Mortgage Payable — Personal | Liability | LongTermLiability |
| 2220 | Credit Card | Liability | CurrentLiability |
| 3000 | Equity | Equity | null (header) |
| 3010 | Owner's Investment | Equity | null |
| 3020 | Owner's Draws | Equity | null |
| 3099 | Retained Earnings | Equity | null |
| 4000 | Revenue | Revenue | null (header) |
| 4100 | Property Revenue | Revenue | null (header) |
| 4110 | Rental Income — Tenant A | Revenue | OperatingRevenue |
| 4120 | Rental Income — Tenant B | Revenue | OperatingRevenue |
| 4130–4150 | Utility Reimbursements | Revenue | OperatingRevenue |
| 4200 | Personal Revenue | Revenue | null (header) |
| 4210 | Salary — Owner | Revenue | OperatingRevenue |
| 4220 | Salary — Spouse | Revenue | OperatingRevenue |
| 5000 | Expenses | Expense | null (header) |
| 5100 | Property Expenses | Expense | null (header) |
| 5110–5210 | Property expense leaves | Expense | OperatingExpense |
| 5300 | Personal Expenses | Expense | null (header) |
| 5310 | Housing | Expense | null (header) |
| 5311–6200 | Personal expense leaves | Expense | OperatingExpense |
| 7100 | Other Income | Revenue | null (header) |
| 7110 | Interest Income | Revenue | OtherRevenue |
| 7200 | Other Expense | Expense | null (header) |
| 7210 | Bank Fees | Expense | OtherExpense |
| 7220 | Investment Fees | Expense | OtherExpense |
| 9010 | Inter-Account Transfer | Expense | null (memo/non-posting) |

---

## Blast Radius Analysis

This touches every layer that reads or writes `ledger.account`:

### Database
- Migration: `ALTER TABLE ledger.account ADD COLUMN account_subtype varchar(25)`
- Seed data update: set subtypes on existing accounts

### Domain (LeoBloom.Domain)
- New `AccountSubType` DU
- `AccountSubType.toDbString` / `AccountSubType.fromDbString` converters
- Validation function: `isValidSubTypeForAccountType : AccountType -> AccountSubType -> bool`
- Modify `Account` domain type to include `subType: AccountSubType option`

### Ledger (LeoBloom.Ledger)
- Every repository that reads `ledger.account` needs to read the new column
- Every repository that writes `ledger.account` needs to write the new column
- Account-related services need to validate subtype on create/update paths
- Existing queries that SELECT * or SELECT specific columns from account
  need the new column added

### Ops (LeoBloom.Ops)
- Any Ops code that reads accounts (if any) needs updating

### CLI (LeoBloom.CLI)
- Account display commands (P041, not yet built) — future, not in scope
- No current CLI code reads account subtypes

### Tests
- Every test that creates accounts needs to provide (or omit) subtype
- Test helpers that build account records need updating
- Seed data in test setup needs to match migration

### Reporting (on P040 branch)
- CashFlowRepository: replace hardcoded codes with
  `WHERE account_subtype = 'Cash'`
- This is the payoff — but it happens when P040 resumes, not in P052

---

## Phases

### Phase 1: Migration

**Files created:**
- `Src/LeoBloom.Migrations/Migrations/TIMESTAMP_AddAccountSubType.sql`
  - `ALTER TABLE ledger.account ADD COLUMN account_subtype varchar(25)`
  - `UPDATE` statements to set subtypes per the seed data table above
  - DOWN: `ALTER TABLE ledger.account DROP COLUMN account_subtype`

**Verification:** Migration runs. Column exists. Seed values correct.

### Phase 2: Domain Types

**Files modified:**
- Add `AccountSubType` DU to Domain (find the right file — likely where
  `AccountType` lives)
  - Cases: `Cash | FixedAsset | Investment | CurrentLiability |
    LongTermLiability | OperatingRevenue | OtherRevenue |
    OperatingExpense | OtherExpense`
  - `toDbString` / `fromDbString` converters
  - `validSubTypesForAccountType` function returning `AccountSubType list`
    for each `AccountType`
  - `isValidSubType` function using the above
- Modify the `Account` type (wherever it's defined) to add
  `subType: AccountSubType option`

**Verification:** Domain project compiles.

### Phase 3: Repository + Service Updates (Ledger)

This is the big phase. Every account read/write path in Ledger.

**Files modified:**
- All repositories that read from `ledger.account` — add
  `account_subtype` to SELECT, map through `fromDbString`
- All repositories that write to `ledger.account` — include
  `account_subtype` in INSERT/UPDATE, validate via `isValidSubType`
- Account-related services — enforce validation on create/update

The Builder needs to:
1. Find every file in LeoBloom.Ledger that touches `ledger.account`
2. Read each one to understand the current query/mapping
3. Add the column to reads and writes
4. Add validation to write paths

**Verification:** Ledger project compiles.

### Phase 4: Ops Updates

**Files modified:**
- Any Ops repository or service that reads accounts — same treatment
  as Phase 3

**Verification:** Ops project compiles.

### Phase 5: Test Updates

**Files modified:**
- Test helpers that create accounts — add optional subtype parameter
- Tests that assert on account records — update expected shapes
- Any test that inserts directly into `ledger.account` — add column

**Verification:** All tests compile and pass (baseline: 514 passing on
P040 branch, or 460 on main — depends which branch this runs on).

### Phase 6: Seed Data Migration Update

**Files modified:**
- `1712000006000_SeedChartOfAccounts.sql` — add `account_subtype` to
  all INSERT statements so fresh DB setups get correct subtypes without
  needing the Phase 1 migration

**Verification:** Fresh `dotnet test` run passes (tests recreate from
seed data).

---

## Execution Notes

- **Run on main, not the P040 branch.** P052 is a foundation project.
  P040 will rebase onto main after P052 merges.
- **Migration runs in dev during build.** Hobson confirms safety and
  runs in prod after merge.
- **The P040 cash flow fix (replacing hardcoded codes with subtype
  query) is NOT part of P052.** That happens when P040 resumes.

## Acceptance Criteria

- [ ] `account_subtype` column exists on `ledger.account` (nullable varchar)
- [ ] `AccountSubType` DU exists in Domain with 9 cases
- [ ] `toDbString` / `fromDbString` round-trip correctly for all 9 cases
- [ ] `isValidSubType` rejects invalid combinations (e.g., Cash on Revenue)
- [ ] `Account` domain type includes `subType: AccountSubType option`
- [ ] All account read paths populate the subtype field
- [ ] All account write paths persist and validate the subtype field
- [ ] Seed data sets correct subtypes on all leaf accounts
- [ ] All existing tests pass with no regressions
- [ ] Fresh DB setup (seed migration) includes subtypes
