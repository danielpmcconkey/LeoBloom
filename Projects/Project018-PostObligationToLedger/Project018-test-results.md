# Project 018 -- Test Results

**Date:** 2026-04-05
**Commit:** 7f9cf2f
**Result:** 15/15 verified

## Acceptance Criteria Verification

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| 1 | Posting a confirmed receivable instance creates a journal entry with correct debit/credit sides and transitions instance to posted | Yes | @FT-POL-001 passes. Test asserts debit=dest, credit=source, amounts=1200, status="posted". Service lines 89-96 build correct lines. |
| 2 | Posting a confirmed payable instance creates a journal entry with correct debit/credit sides and transitions instance to posted | Yes | @FT-POL-002 passes. Test asserts debit=dest, credit=source, amounts=850, status="posted". Same debit/credit mapping as receivable per plan lines 101-102. |
| 3 | Journal entry description matches "{agreement.name} -- {instance.name}" format | Yes | @FT-POL-003 passes. Service uses `sprintf "%s \u2014 %s"` (em-dash). Test constructs expected string with same character and asserts equality. |
| 4 | Journal entry has source = "obligation" and reference_type = "obligation" with instance id | Yes | @FT-POL-004 passes. Test verifies `source = Some "obligation"` on the entry and `referenceType = "obligation"` with `referenceValue = string instanceId` on the reference row. Service lines 86, 98-99 match. |
| 5 | Journal entry entry_date equals the instance's confirmed_date | Yes | @FT-POL-005 passes. Test uses DateOnly(2026,4,20) as confirmedDate, asserts `je.Value.entryDate = confirmedDate`. Service line 84 sets `entryDate = confirmedDate`. |
| 6 | Instance journal_entry_id is set after posting | Yes | @FT-POL-006 passes. Test re-queries instance from DB and asserts `journalEntryId = Some posted.journalEntryId`. Service passes `journalEntryId = Some posted.entry.id` in transition command. |
| 7 | Posting an instance not in confirmed status returns error | Yes | @FT-POL-007 passes. Test leaves instance in "expected" status, asserts Error result contains "confirmed". Service line 32 checks `inst.status <> Confirmed`. |
| 8 | Posting an instance with no amount returns error | Yes | @FT-POL-008 passes. Test inserts confirmed instance with null amount via raw SQL, asserts Error contains "amount". Service line 33 checks `inst.amount.IsNone`. |
| 9 | Posting an instance with no confirmed_date returns error | Yes | @FT-POL-009 passes. Test inserts confirmed instance with null confirmed_date, asserts Error contains "confirmed_date". Service line 39 checks `inst.confirmedDate.IsNone`. |
| 10 | Posting when agreement has no source_account_id returns error | Yes | @FT-POL-010 passes. Test creates agreement with `sourceAccountId = None`, asserts Error contains "source_account". Service line 49 checks `agr.sourceAccountId.IsNone`. |
| 11 | Posting when agreement has no dest_account_id returns error | Yes | @FT-POL-011 passes. Test creates agreement with `destAccountId = None`, asserts Error contains "dest_account". Service line 55 checks `agr.destAccountId.IsNone`. |
| 12 | Posting when no fiscal period covers confirmed_date returns error | Yes | @FT-POL-012 passes. Test uses July 2026 with no fiscal period inserted, asserts Error contains "fiscal period". Service line 64 returns error on `None` from `findByDate`. |
| 13 | Posting when fiscal period is closed returns error (from JournalEntryService) | Yes | @FT-POL-013 passes. Test inserts closed fiscal period (`isOpen = false`), asserts Error contains "not open". Error originates from JournalEntryService.post as designed. Log output confirms: `Fiscal period '8802' is not open`. |
| 14 | FiscalPeriodRepository.findByDate returns correct period for a date within range | Yes | @FT-POL-014 passes. Test inserts period May 1-31, queries May 15, asserts returned period id, startDate, endDate all match. |
| 15 | FiscalPeriodRepository.findByDate returns None for a date outside all periods | Yes | @FT-POL-015 passes. Test inserts period May 1-31, queries Aug 15, asserts `result.IsNone`. |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-POL-001 | Posting a confirmed receivable instance creates correct JE and transitions to posted | Yes | Yes |
| @FT-POL-002 | Posting a confirmed payable instance creates correct JE and transitions to posted | Yes | Yes |
| @FT-POL-003 | Journal entry description matches agreement and instance names | Yes | Yes |
| @FT-POL-004 | Journal entry has obligation source and reference | Yes | Yes |
| @FT-POL-005 | Journal entry entry_date equals the instance confirmed_date | Yes | Yes |
| @FT-POL-006 | Instance journal_entry_id is set after posting | Yes | Yes |
| @FT-POL-007 | Posting an instance not in confirmed status returns error | Yes | Yes |
| @FT-POL-008 | Posting an instance with no amount returns error | Yes | Yes |
| @FT-POL-009 | Posting an instance with no confirmed_date returns error | Yes | Yes |
| @FT-POL-010 | Posting when agreement has no source_account_id returns error | Yes | Yes |
| @FT-POL-011 | Posting when agreement has no dest_account_id returns error | Yes | Yes |
| @FT-POL-012 | Posting when no fiscal period covers confirmed_date returns error | Yes | Yes |
| @FT-POL-013 | Posting when fiscal period is closed returns error | Yes | Yes |
| @FT-POL-014 | findByDate returns correct period for a date within range | Yes | Yes |
| @FT-POL-015 | findByDate returns None for a date outside all periods | Yes | Yes |

## Artifact Verification

| Artifact | Required | Present | Notes |
|---|---|---|---|
| Domain types in Ops.fs | PostToLedgerCommand, PostToLedgerResult | Yes | Lines 349-354. Fields match plan spec exactly. |
| FiscalPeriodRepository.findByDate | New function | Yes | Lines 34-54. SQL uses `@date >= start_date AND @date <= end_date`. Returns `FiscalPeriod option`. |
| ObligationPostingService.fs | New file | Yes | Single public function `postToLedger`. Follows plan flow: read+validate, post JE, transition instance. |
| LeoBloom.Utilities.fsproj | Compile entry after ObligationInstanceService.fs | Yes | Line 29: `ObligationPostingService.fs` follows `ObligationInstanceService.fs` at line 28. |
| LeoBloom.Tests.fsproj | Compile entry for test file | Yes | Line 29: `PostObligationToLedgerTests.fs` present. |
| Gherkin spec | Feature file with 15 scenarios | Yes | Located at `Specs/Behavioral/PostObligationToLedger.feature` (plan said `Specs/Ops/` -- minor path discrepancy, non-functional). |

## Fabrication Check

- All 15 tests ran and passed in a single `dotnet test` invocation. Output shows `Total tests: 15, Passed: 15`.
- Test output log lines confirm actual DB operations (insert, transition, post) occurred -- not mocked.
- Each test creates its own data via `TestData.uniquePrefix()` and cleans up via `TestCleanup.deleteAll`.
- No circular evidence detected. Tests query raw DB state independently of the service under test.
- Gherkin scenario tags match test Trait attributes 1:1.

## Verdict

**APPROVED**

Every acceptance criterion is verified against actual code and passing test output. The evidence chain is solid: tests hit a real database, create isolated data, call the service, and verify outcomes by querying DB state directly. No fabrication detected.
