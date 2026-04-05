# Project 019 — Test Results

**Date:** 2026-04-05
**Commit:** 9ed2a85 (Merge pull request #18 — P018)
**Result:** 13/13 verified (14th item is a note, not a criterion)

## Test Execution

```
dotnet test LeoBloom.sln --verbosity normal --filter "FullyQualifiedName~TransferTests"
Total tests: 14
     Passed: 14
 Total time: 2.4756 Seconds
```

All 14 tests pass. Zero warnings, zero errors.

## Acceptance Criteria Verification

| # | Criterion | Verified | Evidence |
|---|-----------|----------|----------|
| 1 | Initiating a transfer between two active asset accounts succeeds and creates a record with status 'initiated' | Yes | FT-TRF-001 passes. Test asserts status=Initiated, correct fromAccountId/toAccountId/amount. Service validates both accounts exist, are active, and are asset type before inserting with status='initiated'. |
| 2 | Initiating with from_account = to_account returns error | Yes | FT-TRF-002 passes. Test creates one account, passes same id for both, asserts Error containing "same". Service checks `cmd.fromAccountId = cmd.toAccountId` in pure validation. |
| 3 | Initiating with non-asset account type returns error | Yes | FT-TRF-003 passes. Test creates an expense-type account via `insertAccountType`, asserts Error containing "asset". Service joins `ledger.account` to `ledger.account_type` and checks `typeName <> "asset"`. |
| 4 | Initiating with inactive account returns error | Yes | FT-TRF-004 passes. Test creates account with `isActive=false`, asserts Error containing "active". Service checks `Some (false, _)` from lookupAccountInfo. |
| 5 | Initiating with amount <= 0 returns error | Yes | FT-TRF-005 (zero) and FT-TRF-006 (negative) both pass. Tests assert Error containing "amount". Service checks `cmd.amount <= 0m`. Two Gherkin scenarios cover both zero and negative. |
| 6 | Confirming an initiated transfer creates correct journal entry (debit to, credit from) and sets status 'confirmed' | Yes | FT-TRF-007 passes. Test verifies status=Confirmed, 2 JE lines, debit line on toAccountId with 1000.00, credit line on fromAccountId with 1000.00. Service builds PostJournalEntryCommand with debit=toAccountId, credit=fromAccountId. |
| 7 | Confirming sets confirmed_date and journal_entry_id on the transfer | Yes | FT-TRF-008 passes. Test asserts confirmedDate=Some(2099-04-15), journalEntryId.IsSome, and cross-checks via direct DB query. updateConfirm SQL sets confirmed_date, journal_entry_id, modified_at. |
| 8 | Journal entry has source = "transfer" and reference type "transfer" with transfer id | Yes | FT-TRF-009 passes. Test queries JE and references, asserts source=Some "transfer", referenceType="transfer", referenceValue=string transfer.id. Service sets source=Some "transfer" and references=[{referenceType="transfer"; referenceValue=string transfer.id}]. |
| 9 | Journal entry entry_date = confirmed_date | Yes | FT-TRF-010 passes. Test confirms on 2099-06-20, queries JE, asserts entryDate=confirmedDate. Service sets entryDate=cmd.confirmedDate. |
| 10 | Journal entry uses transfer description when present | Yes | FT-TRF-011 passes. Test initiates with description=Some "Savings top-up", confirms, asserts JE description="Savings top-up". Service uses `transfer.description |> Option.defaultValue (...)`. |
| 11 | Journal entry auto-generates description when transfer has no description | Yes | FT-TRF-012 passes. Test initiates with description=None, confirms, asserts JE description starts with "Transfer". Service generates `sprintf "Transfer %d" transfer.id`. |
| 12 | Confirming a transfer not in 'initiated' status returns error | Yes | FT-TRF-013 passes. Test confirms a transfer, then attempts second confirm, asserts Error containing "initiated". Service checks `t.status <> TransferStatus.Initiated`. |
| 13 | Confirming when no fiscal period covers confirmed_date returns error | Yes | FT-TRF-014 passes. Test creates no fiscal period for target date, asserts Error containing "fiscal period". Service calls FiscalPeriodRepository.findByDate, returns Error on None. |
| -- | FiscalPeriodRepository.findByDate already tested in P018 | N/A | Not a testable criterion. Informational note in the plan. |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-TRF-001 | Initiating a transfer between two active asset accounts succeeds | Yes | Yes |
| @FT-TRF-002 | Initiating with from_account equal to to_account returns error | Yes | Yes |
| @FT-TRF-003 | Initiating with a non-asset account type returns error | Yes | Yes |
| @FT-TRF-004 | Initiating with an inactive account returns error | Yes | Yes |
| @FT-TRF-005 | Initiating with amount zero returns error | Yes | Yes |
| @FT-TRF-006 | Initiating with negative amount returns error | Yes | Yes |
| @FT-TRF-007 | Confirming an initiated transfer creates correct journal entry and sets status confirmed | Yes | Yes |
| @FT-TRF-008 | Confirming sets confirmed_date and journal_entry_id on the transfer | Yes | Yes |
| @FT-TRF-009 | Journal entry has transfer source and reference | Yes | Yes |
| @FT-TRF-010 | Journal entry entry_date equals confirmed_date | Yes | Yes |
| @FT-TRF-011 | Journal entry uses transfer description when present | Yes | Yes |
| @FT-TRF-012 | Journal entry auto-generates description when transfer has no description | Yes | Yes |
| @FT-TRF-013 | Confirming a transfer not in initiated status returns error | Yes | Yes |
| @FT-TRF-014 | Confirming when no fiscal period covers confirmed_date returns error | Yes | Yes |

All 14 Gherkin scenarios have corresponding tests tagged with `[<Trait("GherkinId", "@FT-TRF-NNN")>]`. All pass.

## Observations

1. **Feature file path deviation:** Plan specified `Specs/Ops/Transfers.feature`, actual file is at `Specs/Behavioral/Transfers.feature`. Not an acceptance criterion issue -- the spec exists and is complete.

2. **No fabrication detected.** Every claim is backed by actual code and live test output. No circular evidence, no stale results, no omitted failures.

3. **Test isolation is solid.** Every test uses `TestData.uniquePrefix()`, creates its own accounts/periods, and cleans up via `TestCleanup.deleteAll` in a `finally` block. JE cleanup is tracked for confirm tests.

4. **Non-atomic confirm flow matches design.** The service uses three separate transactions (read, post JE, update transfer) as documented in the plan's design decisions. The error handling for a failed Phase 3 (update after JE posted) logs a warning and returns an error, leaving the transfer in initiated status for safe retry.

## Verdict

**APPROVED.** Every acceptance criterion is verified against actual code and passing test output. The evidence chain is direct: plan criterion -> Gherkin scenario -> tagged test -> service/repository implementation -> passing test run. No gaps, no fabrication.
