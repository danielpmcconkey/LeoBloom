# Project 010 — Opening Balances: Test Results

**Author:** Basement Dweller (Governor role)
**Date:** 2026-04-04
**Verdict:** PASS

---

## Test Suite

285 total tests, all passing, zero warnings.
Baseline was 269 (from Projects 001-013).
16 new tests added (14 behavioral, 2 structural).

## Behavioral Scenarios

| ID | Scenario | Status |
|----|----------|--------|
| FT-OB-001 | Asset and liability produce balanced entry | PASS |
| FT-OB-002 | Balancing line correct when debits exceed credits | PASS |
| FT-OB-003 | Balancing line correct when credits exceed debits | PASS |
| FT-OB-004 | Single account entry creates two-line JE | PASS |
| FT-OB-005 | Posted entry retrievable with correct line count | PASS |
| FT-OB-006 | Duplicate account in entries returns error | PASS |
| FT-OB-007 | Empty entries list returns error | PASS |
| FT-OB-008 | Zero balance entry returns error | PASS |
| FT-OB-009 | Balancing account in entries list returns error | PASS |
| FT-OB-010 | Nonexistent account returns error | PASS |
| FT-OB-011 | Nonexistent balancing account returns error | PASS |
| FT-OB-012 | Non-equity balancing account returns error | PASS |
| FT-OB-013 | Default description "Opening balances" | PASS |
| FT-OB-014 | Custom description used when provided | PASS |

## Structural Tests

| Scenario | Status |
|----------|--------|
| OpeningBalanceEntry type has required fields | PASS |
| PostOpeningBalancesCommand type has required fields | PASS |

## Acceptance Criteria

- [x] `OpeningBalanceEntry` and `PostOpeningBalancesCommand` types exist
- [x] `OpeningBalanceService.post` works
- [x] Normal-debit accounts produce debit lines
- [x] Normal-credit accounts produce credit lines
- [x] Balancing line auto-computed against specified equity account
- [x] Duplicate accounts rejected
- [x] Zero balances rejected
- [x] Balancing account in entries list rejected
- [x] Non-equity balancing account rejected
- [x] Delegates to JournalEntryService.post (no duplicate write path)
- [x] All tests pass, no new warnings
- [x] Existing 269 tests still pass (regression)
