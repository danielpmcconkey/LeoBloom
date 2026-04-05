# Project 018 — Post Obligation to Ledger

## Goal

Bridge the ops and ledger domains: when a confirmed obligation instance is
posted, create a double-entry journal entry and transition the instance to
`posted`. This is the first cross-domain orchestration in LeoBloom — ops
data drives ledger writes.

## Behavior

Given a confirmed obligation instance (with `amount` and `confirmed_date`
set), posting it:

1. Looks up the instance and its parent agreement
2. Validates preconditions (confirmed status, accounts present on agreement,
   fiscal period exists for confirmed_date)
3. Determines debit/credit sides from obligation direction:
   - **Receivable**: debit `dest_account_id` (asset), credit `source_account_id` (revenue)
   - **Payable**: debit `dest_account_id` (expense), credit `source_account_id` (asset)
4. Posts a journal entry via `JournalEntryService.post`
5. Transitions instance to `posted` via `ObligationInstanceService.transition`

## Design Decisions

### Non-atomic orchestration is acceptable

`JournalEntryService.post` and `ObligationInstanceService.transition` each
open their own connection + transaction. The new `postToLedger` function
cannot wrap both in a single transaction without refactoring those services
(out of scope).

**Order**: post journal entry first, then transition instance.

If transition fails after journal entry succeeds:
- The journal entry exists with an `obligation` reference tracing to the instance
- The instance stays `confirmed` — retry is safe
- No phantom financial data (the JE is real and correct)

If journal entry fails, nothing happened — clean failure.

### Minimal command surface

`PostToLedgerCommand = { instanceId: int }` — everything else (accounts,
amount, fiscal period) is derived from the instance and agreement. The caller
shouldn't need to know or provide ledger details.

### New repository function needed

`FiscalPeriodRepository.findByDate` does not exist. We need it to resolve
`confirmed_date` → fiscal period. This is the only new repository function.

## Implementation

### Domain (Ops.fs)

Add at bottom of file:

- `PostToLedgerCommand = { instanceId: int }`
- `PostToLedgerResult = { journalEntryId: int; instanceId: int }`

Keep it lean. The caller gets back the IDs they need. No need to return
full `PostedJournalEntry` or `ObligationInstance` — the caller can look
those up if needed.

### Repository (FiscalPeriodRepository.fs)

Add `findByDate`:

```fsharp
let findByDate (txn: NpgsqlTransaction) (date: DateOnly) : FiscalPeriod option
```

SQL: `SELECT ... FROM ledger.fiscal_period WHERE @date >= start_date AND @date <= end_date`

Returns first match (periods should not overlap). Pattern follows existing
`findById` exactly.

### Service (ObligationPostingService.fs) — new file

New module `ObligationPostingService` in `LeoBloom.Utilities`.

Single public function:

```fsharp
let postToLedger (cmd: PostToLedgerCommand) : Result<PostToLedgerResult, string list>
```

**Flow:**

1. Open connection + transaction (read-only phase)
2. Load instance via `ObligationInstanceRepository.findById`
3. Validate: instance exists, is active, status = Confirmed, amount is Some,
   confirmedDate is Some
4. Load agreement via `ObligationAgreementRepository.findById`
5. Validate: agreement exists, `sourceAccountId` is Some, `destAccountId` is Some
6. Load fiscal period via `FiscalPeriodRepository.findByDate` using confirmedDate
7. Validate: fiscal period found
8. Commit/rollback read transaction
9. Determine debit/credit accounts from `agreement.obligationType`:
   - Receivable: debit = destAccountId, credit = sourceAccountId
   - Payable: debit = destAccountId, credit = sourceAccountId
10. Build `PostJournalEntryCommand`:
    - `entryDate` = instance.confirmedDate
    - `description` = `"{agreement.name} — {instance.name}"`
    - `source` = `Some "obligation"`
    - `fiscalPeriodId` = resolved period id
    - Two lines: debit line + credit line, both for instance.amount
    - One reference: `referenceType = "obligation"`, `referenceValue = string instance.id`
11. Call `JournalEntryService.post` — if Error, return Error
12. Call `ObligationInstanceService.transition` with targetStatus = Posted,
    journalEntryId = posted entry id — if Error, log warning and return Error
    (journal entry exists but instance not transitioned; noted in error message)
13. Return `Ok { journalEntryId = ...; instanceId = ... }`

### Project file (LeoBloom.Utilities.fsproj)

Add `<Compile Include="ObligationPostingService.fs" />` after
`ObligationInstanceService.fs`. This file depends on both
`JournalEntryService` and `ObligationInstanceService`, so it must come
after both in compilation order.

## Artifacts

- Domain types: additions to `Src/LeoBloom.Domain/Ops.fs`
- Repository: addition to `Src/LeoBloom.Utilities/FiscalPeriodRepository.fs`
- Service: new file `Src/LeoBloom.Utilities/ObligationPostingService.fs`
- Project file: `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` (compile order)
- Gherkin: `Specs/Ops/PostObligationToLedger.feature` (tag prefix `@FT-POL`)
- Tests: `Src/LeoBloom.Tests/PostObligationToLedgerTests.fs`
- Test project file: `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` (add test file)

## Test Strategy

All tests hit real DB. Each test:
- Creates its own agreement, instance, accounts, fiscal period via `InsertHelpers`
- Uses `TestData.uniquePrefix()` for isolation
- Cleans up via `TestCleanup.track` + `TestCleanup.deleteAll`

Test helpers may need additions:
- Helper to insert a ledger account (if not already in `InsertHelpers`)
- Helper to insert a fiscal period (if not already in `InsertHelpers`)
- Helper to set up a "ready to post" instance (confirmed status with amount
  and confirmedDate, agreement with source/dest accounts)

## Acceptance Criteria

- [ ] Posting a confirmed receivable instance creates a journal entry with correct debit/credit sides and transitions instance to posted
- [ ] Posting a confirmed payable instance creates a journal entry with correct debit/credit sides and transitions instance to posted
- [ ] Journal entry description matches `"{agreement.name} — {instance.name}"` format
- [ ] Journal entry has source = "obligation" and reference_type = "obligation" with instance id
- [ ] Journal entry entry_date equals the instance's confirmed_date
- [ ] Instance journal_entry_id is set after posting
- [ ] Posting an instance not in confirmed status returns error
- [ ] Posting an instance with no amount returns error
- [ ] Posting an instance with no confirmed_date returns error
- [ ] Posting when agreement has no source_account_id returns error
- [ ] Posting when agreement has no dest_account_id returns error
- [ ] Posting when no fiscal period covers confirmed_date returns error
- [ ] Posting when fiscal period is closed returns error (from JournalEntryService)
- [ ] `FiscalPeriodRepository.findByDate` returns correct period for a date within range
- [ ] `FiscalPeriodRepository.findByDate` returns None for a date outside all periods

## Risks

- **Non-atomic post+transition**: If transition fails after journal entry
  succeeds, the JE exists without a posted instance. Mitigation: the JE has
  an obligation reference for traceability, and the instance stays confirmed
  so retry is safe. Log a warning if this happens.
- **Fiscal period gaps**: If no period covers the confirmed_date, posting
  fails. This is correct behavior — the user needs to create the period first.
- **Account validation delegation**: We rely on `JournalEntryService.post`
  to validate that accounts exist and are active. We don't duplicate that
  check. If the agreement points to a deleted/inactive account, the posting
  engine catches it.

## Out of Scope

- Refactoring existing services to accept external transactions (would enable
  full atomicity but is a bigger change)
- Batch posting (post multiple instances at once)
- Reversing a posted obligation (voiding the JE + transitioning instance back)
- Any UI/CLI integration (that's a consumption layer concern)
