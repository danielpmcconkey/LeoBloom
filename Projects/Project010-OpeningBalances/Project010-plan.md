# Project 010 — Opening Balances: Implementation Plan

**Author:** Basement Dweller (PO + Planner)
**Date:** 2026-04-04
**Status:** Approved (Dan said go)
**Depends On:** Project 005 (Post Journal Entry) — complete

---

## Objective

Build a convenience service for entering opening account balances when the
system goes live. Dan's accounts already have balances (bank accounts have
money, mortgages have outstanding principal). This service takes a list of
account/balance pairs, figures out the debit/credit direction from each
account's normal balance, computes the balancing entry against a specified
equity account, and posts a single balanced journal entry.

## Why This Isn't Just "Post Journal Entry"

You CAN enter opening balances as manual journal entries via Project 005. But
that requires the user to:
- Know which side (debit/credit) each account goes on
- Manually compute the balancing equity entry
- Construct a multi-line journal entry command by hand

This service wraps all of that. You say "Cash is $10,000, Mortgage is $200,000"
and it does the rest.

## GAAP Notes

Opening balances are standard practice when migrating to a new accounting
system. The balancing account is typically "Opening Balance Equity" (a one-time
equity account). In Dan's COA, `3010 Owner's Investment` serves this purpose.
After opening balances are posted, the accounting equation holds from day one.

---

## Architecture

### Input

```fsharp
type OpeningBalanceEntry =
    { accountId: int
      balance: decimal }

type PostOpeningBalancesCommand =
    { entryDate: DateOnly
      fiscalPeriodId: int
      balancingAccountId: int
      entries: OpeningBalanceEntry list
      description: string option }
```

- `balance` is always positive, expressed in normal-balance terms. A $10,000
  cash balance is `10000m`. A $200,000 mortgage is `200000m`. The service
  figures out debit vs credit from the account type.
- `balancingAccountId` is the equity account that absorbs the difference (e.g.,
  Owner's Investment). This is explicit — we don't hardcode an account.
- `description` defaults to "Opening balances" if not provided.

### Processing

1. Validate: entries list non-empty, all balances > 0, no duplicate accounts,
   balancing account exists and is equity type.
2. Look up each account's normal balance direction.
3. Build journal entry lines:
   - Normal-debit accounts (assets, expenses): debit line
   - Normal-credit accounts (liabilities, equity, revenue): credit line
4. Compute imbalance: total debits - total credits.
   - If debits > credits: credit the balancing account for the difference
   - If credits > debits: debit the balancing account for the difference
   - If balanced already: no balancing line needed
5. Post via `JournalEntryService.post`.
6. Return the posted journal entry.

### Edge Cases

- **Balancing account also in entries list**: Error. The balancing account is
  computed, not user-specified.
- **Zero balance entries**: Error. If it's zero, don't include it.
- **Negative balances**: Not supported. If an account has a contra-normal
  balance (e.g., overdrawn checking), that's unusual enough to warrant a
  manual journal entry.
- **Empty entries list**: Error.
- **Single entry**: Valid. One account plus the balancing entry = two lines.

### Output

Returns `Result<PostedJournalEntry, string list>` — same type as
`JournalEntryService.post`. The caller gets back the full posted entry with
all lines, including the auto-generated balancing line.

---

## Phases

### Phase 1: Domain Types

**File:** `Src/LeoBloom.Domain/Ledger.fs` (modify)

Add `OpeningBalanceEntry` and `PostOpeningBalancesCommand` after the existing
command DTOs.

### Phase 2: Service

**File:** `Src/LeoBloom.Utilities/OpeningBalanceService.fs` (create)

Single public function: `post : PostOpeningBalancesCommand -> Result<PostedJournalEntry, string list>`

Delegates to `JournalEntryService.post` after building the command. Does NOT
open its own connection/transaction — it builds a `PostJournalEntryCommand`
and lets the existing write path handle atomicity.

**Project file:** Add after `SubtreePLService.fs` in Utilities `.fsproj`.

### Phase 3: Tests

**File:** `Src/LeoBloom.Tests/OpeningBalanceTests.fs` (create)

**Tag prefix:** `@FT-OB`

| ID | Scenario |
|----|----------|
| FT-OB-001 | Opening balances for asset and liability produce balanced entry |
| FT-OB-002 | Balancing line computed correctly when debits exceed credits |
| FT-OB-003 | Balancing line computed correctly when credits exceed debits |
| FT-OB-004 | Single account entry creates two-line journal entry |
| FT-OB-005 | Posted entry is retrievable and has correct line count |
| FT-OB-006 | Duplicate account in entries returns error |
| FT-OB-007 | Empty entries list returns error |
| FT-OB-008 | Zero balance entry returns error |
| FT-OB-009 | Balancing account in entries list returns error |
| FT-OB-010 | Nonexistent account returns error |
| FT-OB-011 | Nonexistent balancing account returns error |
| FT-OB-012 | Non-equity balancing account returns error |
| FT-OB-013 | Default description is "Opening balances" when not provided |
| FT-OB-014 | Custom description is used when provided |

**Test project file:** Add after `SubtreePLTests.fs`.

---

## Acceptance Criteria

- [ ] `OpeningBalanceEntry` and `PostOpeningBalancesCommand` types exist
- [ ] `OpeningBalanceService.post` works
- [ ] Normal-debit accounts produce debit lines
- [ ] Normal-credit accounts produce credit lines
- [ ] Balancing line auto-computed against specified equity account
- [ ] No balancing line when entries already balance
- [ ] Duplicate accounts rejected
- [ ] Zero balances rejected
- [ ] Balancing account in entries list rejected
- [ ] Non-equity balancing account rejected
- [ ] Delegates to JournalEntryService.post (no duplicate write path)
- [ ] All tests pass, no new warnings
- [ ] Existing tests still pass

---

## Out of Scope

- Negative/contra-normal balances (use manual JE)
- Multiple balancing accounts
- Opening balance reversal/correction (just void and re-enter)
- Batch import from external system

---

## File Summary

| Action | File |
|--------|------|
| Modify | `Src/LeoBloom.Domain/Ledger.fs` |
| Create | `Src/LeoBloom.Utilities/OpeningBalanceService.fs` |
| Modify | `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` |
| Create | `Src/LeoBloom.Tests/OpeningBalanceTests.fs` |
| Modify | `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` |
