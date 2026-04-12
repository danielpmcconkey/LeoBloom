# P062 — Consolidate Normal Balance Resolution — Plan

## Objective

Extract the duplicated normal-balance arithmetic (debit-normal: debits − credits;
credit-normal: credits − debits) into a single pure function in the Domain module,
then replace all 7 inline implementations with calls to it. Pure refactor — no
behavioral change.

## Function Definition

Add to `Src/LeoBloom.Domain/Ledger.fs` immediately after `type NormalBalance = Debit | Credit` (line 11):

```fsharp
    let resolveBalance (normalBalance: NormalBalance) (debits: decimal) (credits: decimal) : decimal =
        match normalBalance with
        | NormalBalance.Debit  -> debits - credits
        | NormalBalance.Credit -> credits - debits
```

## Phases

### Phase 1: Add `resolveBalance` to Domain

**What:** Add the function definition to `Src/LeoBloom.Domain/Ledger.fs`
**Files:** `Src/LeoBloom.Domain/Ledger.fs` (modified)
**Verification:** Project compiles. No tests break.

### Phase 2: Replace Standard Call Sites (5 files)

These 5 files all follow the identical pattern — string-match on `"credit"` /
default, with separate `debitTotal` / `creditTotal` decimals:

1. **TrialBalanceRepository.fs** (lines ~35-38)
   - Parse `nb` as `NormalBalance` (already done)
   - Replace 3-line match with `Ledger.resolveBalance nb debitTotal creditTotal`

2. **IncomeStatementRepository.fs** (lines ~37-40)
   - Convert string `normalBalance` → `NormalBalance` DU
   - Replace match with `Ledger.resolveBalance nb debitTotal creditTotal`

3. **BalanceSheetRepository.fs — getCumulativeBalances** (lines ~38-41)
   - Same as IncomeStatement: convert string, call `resolveBalance`

4. **SubtreePLRepository.fs** (lines ~56-59)
   - Same pattern: convert string, call `resolveBalance`

5. **ScheduleERepository.fs** (lines ~71-74)
   - Same pattern: convert string, call `resolveBalance`

**Files modified:** 5 repository files
**Verification:** All existing tests pass with zero test code changes.

### Phase 3: Replace Variant Call Sites (2 files)

These require slightly more care:

6. **AccountBalanceRepository.fs** (lines ~119-121)
   - SQL already computes `raw_balance = debits - credits`
   - Current code: `Debit -> rawBalance | Credit -> -rawBalance`
   - This is algebraically equivalent to `resolveBalance nb rawBalance 0m`:
     - Debit: `rawBalance - 0 = rawBalance` ✓
     - Credit: `0 - rawBalance = -rawBalance` ✓
   - Replace match with `Ledger.resolveBalance nb rawBalance 0m`

7. **GeneralLedgerReportService.fs** (lines ~43-46)
   - `normalBalance` is a string from a tuple return
   - Convert to `NormalBalance` DU, then call
     `Ledger.resolveBalance nb row.debitAmount row.creditAmount`

**Files modified:** 2 files
**Verification:** All existing tests pass with zero test code changes.

### Phase 4: Handle Retained Earnings (BalanceSheetRepository.fs, 2nd location)

**BalanceSheetRepository.fs — getRetainedEarnings** (lines ~74-76):
```fsharp
| "revenue" -> revenueNet <- creditTotal - debitTotal
| "expense" -> expenseNet <- debitTotal - creditTotal
```

This matches on account type name, not NormalBalance, but the arithmetic is
identical. Revenue is credit-normal, expense is debit-normal:
```fsharp
| "revenue" -> revenueNet <- Ledger.resolveBalance NormalBalance.Credit debitTotal creditTotal
| "expense" -> expenseNet <- Ledger.resolveBalance NormalBalance.Debit debitTotal creditTotal
```

**Files modified:** `BalanceSheetRepository.fs` (same file as Phase 2 item 3)
**Verification:** All existing tests pass. Retained earnings values unchanged.

## Acceptance Criteria

- [ ] AC-1: `Ledger.resolveBalance` exists in `Src/LeoBloom.Domain/Ledger.fs`
- [ ] AC-2: No repository or service contains inline normal-balance arithmetic
      (no `debitTotal - creditTotal` / `creditTotal - debitTotal` / `-rawBalance`
      patterns outside the Domain function)
- [ ] AC-3: All existing tests pass with zero changes to test code

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| String-to-DU conversion introduces new failure path | Low | Same `"debit" -> Debit \| _ -> Credit` pattern already used in TrialBalanceRepository; wildcard match prevents crashes |
| AccountBalanceRepository `resolveBalance nb rawBalance 0m` is confusing | Low | Add a brief comment explaining the SQL pre-computes debits − credits |
| Retained earnings site conflates account type with normal balance | Low | Hardcode the known mapping (revenue=Credit, expense=Debit) which is a GAAP invariant |

## Out of Scope

- Changing SQL queries (AccountBalanceRepository keeps its pre-computed raw_balance)
- Adding new tests (spec says pure refactor, AC-3 says no test changes)
- Contra-account handling (future concern, not in scope)
- Module reorganization beyond adding the function
