# Project 064 — Balance Sheet Equation Independent Verification — Plan

## Objective

Add scenarios to `Specs/Behavioral/BalanceSheet.feature` and corresponding
F# tests to `Src/LeoBloom.Tests/BalanceSheetTests.fs` that independently
verify the accounting equation `A = L + E + RE` by computing it from section
totals — never referencing the `isBalanced` flag. This closes GAP-008: if
`isBalanced` were broken, existing tests would pass silently.

## Phase 1: Add Gherkin Scenarios (Feature File)

**What:** Append three new scenarios to `Specs/Behavioral/BalanceSheet.feature`
under a new `# --- Accounting Equation: Independent Verification ---` section
header. Each scenario sets up a ledger, retrieves the balance sheet, and
asserts the equation `assets.sectionTotal = liabilities.sectionTotal +
equity.sectionTotal + retainedEarnings` as direct arithmetic. None of them
reference `isBalanced`.

### Scenario FT-BS-012: Equation holds with positive retained earnings
- Accounts: asset (1010), liability (2010), equity (3010), revenue (4010),
  expense (5010)
- Entries: owner investment 10000 (dr asset, cr equity), loan 3000
  (dr asset, cr liability), revenue 2000 (dr asset, cr revenue), expense
  500 (dr expense, cr asset)
- Expected: assets = 14500, liabilities = 3000, equity = 10000, RE = 1500
- Assert: `assets.sectionTotal = liabilities.sectionTotal + equity.sectionTotal + retainedEarnings`
  → 14500 = 3000 + 10000 + 1500

### Scenario FT-BS-013: Equation holds with negative retained earnings
- Accounts: asset (1010), liability (2010), equity (3010), revenue (4010),
  expense (5010)
- Entries: owner investment 5000 (dr asset, cr equity), loan 4000
  (dr asset, cr liability), revenue 300 (dr asset, cr revenue), expense
  1200 (dr expense, cr asset)
- Expected: assets = 8100, liabilities = 4000, equity = 5000, RE = -900
- Assert: `assets.sectionTotal = liabilities.sectionTotal + equity.sectionTotal + retainedEarnings`
  → 8100 = 4000 + 5000 + (-900)

### Scenario FT-BS-014: Equation holds with zero equity section
- Accounts: asset (1010), liability (2010), revenue (4010)
- No equity accounts created. Funded entirely by liabilities + revenue.
- Entries: loan 6000 (dr asset, cr liability), revenue 1000 (dr asset,
  cr revenue)
- Expected: assets = 7000, liabilities = 6000, equity = 0, RE = 1000
- Assert: `assets.sectionTotal = liabilities.sectionTotal + equity.sectionTotal + retainedEarnings`
  → 7000 = 6000 + 0 + 1000

### New Gherkin step
A new step definition is needed:

```gherkin
Then the accounting equation is verified independently
```

This step computes `assets.sectionTotal` and compares it to
`liabilities.sectionTotal + equity.sectionTotal + retainedEarnings` using
the response fields — it never checks `isBalanced`.

**Files modified:**
- `Specs/Behavioral/BalanceSheet.feature` — append 3 scenarios

**Verification:** Feature file parses without syntax errors; scenarios are
well-formed and internally consistent.

## Phase 2: Add F# Tests (Test File)

**What:** Append three new `[<Fact>]` tests to
`Src/LeoBloom.Tests/BalanceSheetTests.fs`, each tagged with the
corresponding GherkinId trait. Each test:

1. Sets up accounts and entries within a transaction (rolls back on dispose)
2. Calls `BalanceSheetService.getAsOfDate`
3. Asserts section totals match expected values
4. Asserts `report.assets.sectionTotal = report.liabilities.sectionTotal + report.equity.sectionTotal + report.retainedEarnings`
5. Does **NOT** reference `report.isBalanced` anywhere

**Date isolation:** Use far-past years (1953, 1954, 1955) for the three
tests respectively, consistent with the existing pattern (FT-BS-003 uses
1950, FT-BS-004 uses 1951, FT-BS-005 uses 1952). This avoids retained
earnings pollution from parallel test runs. These dates don't conflict with
the fiscal period year reservations (2091–2097 range) because these tests
use transaction-scoped data that never commits.

### Test: FT-BS-012 — equation holds with positive retained earnings

```fsharp
let prefix = TestData.uniquePrefix()
// Create asset, liability, equity, revenue, expense accounts
// FP in year 1953
// Post: owner investment 10000, loan 3000, revenue 2000, expense 500
// Assert section totals: assets=14500, liab=3000, eq=10000, RE=1500
// Assert: report.assets.sectionTotal = report.liabilities.sectionTotal + report.equity.sectionTotal + report.retainedEarnings
```

### Test: FT-BS-013 — equation holds with negative retained earnings

```fsharp
// FP in year 1954
// Post: owner investment 5000, loan 4000, revenue 300, expense 1200
// Assert section totals: assets=8100, liab=4000, eq=5000, RE=-900
// Assert the equation
```

### Test: FT-BS-014 — equation holds with zero equity section

```fsharp
// FP in year 1955
// No equity account. Post: loan 6000, revenue 1000
// Assert section totals: assets=7000, liab=6000, eq=0, RE=1000
// Assert the equation
```

**Files modified:**
- `Src/LeoBloom.Tests/BalanceSheetTests.fs` — append 3 tests (before the
  structural tests section)

**Verification:** `dotnet test --filter "GherkinId=FT-BS-012|GherkinId=FT-BS-013|GherkinId=FT-BS-014"` — all three pass.

## Acceptance Criteria

- [x] AC-1: At least one scenario asserts A = L + E + RE as a computed
  equality (all three do)
- [x] AC-2: None of the new scenarios reference `isBalanced` — they compute
  the equation from section totals
- [x] AC-3: A broken `isBalanced` implementation would not cause these tests
  to pass — they never read `isBalanced`; they assert the equation from raw
  section totals and retained earnings. Even if `isBalanced` returned `true`
  on an unbalanced sheet, these tests would still fail if the arithmetic
  doesn't hold.

## Risks

- **Retained earnings pollution from parallel tests:** Mitigated by using
  far-past years (1953–1955) and transaction isolation (no commits). The
  existing tests follow this same pattern.
- **Section total includes accounts from other tests:** Since all tests run
  inside a transaction that rolls back, the balance sheet query only sees
  data created within that transaction. No risk.

## Out of Scope

- Refactoring existing tests to remove `isBalanced` usage — those tests
  serve their own purpose (verifying the flag works correctly).
- Changes to `BalanceSheetService` or any production code — this is pure
  test additions.
- Gherkin step definition implementation — that's the Gherkin Writer / QE
  responsibility.
