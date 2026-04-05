# GAAP Assessment Synthesis — LeoBloom Projects 001–018

**Date:** 2026-04-05
**Assessor:** Hobson
**Scope:** 9 feature files, 91 Gherkin scenarios, all test implementations, domain model
**Auditors:** Ledger Tracer, Omission Hunter, GAAP Mapper, Slop Detector

---

## Executive Summary

The accounting engine is sound. The arithmetic is clean, the core GAAP
principles are enforced, and 68% of scenarios are genuinely solid tests that
would fail if real logic were broken. This is not slop.

There are gaps — some significant — but they're the kind you'd expect at
this stage: missing edge cases and untested invariants, not fundamental
design errors. The foundation is trustworthy. The gaps are addressable
without rework.

---

## Verdict by Auditor

| Auditor | Verdict |
|---|---|
| **Ledger Tracer** | **Clean.** 38 scenarios traced. Zero arithmetic discrepancies. Every journal entry balances. Every report assertion matches the math. |
| **Omission Hunter** | **3 critical, 13 significant, 14 minor gaps.** The critical gaps are real: double-posting prevention, incomplete state machine coverage, and two missing spec files (Opening Balances, SubtreePL). |
| **GAAP Mapper** | **12 principles enforced, 6 implied, 4 absent.** The enforced list covers the essentials: double-entry, cash basis, period cutoff, financial statement articulation, state machine integrity. The absent items are real but narrow. |
| **Slop Detector** | **62 solid (68%), 23 thin (25%), 6 slop (7%).** Slop is concentrated in CRUD tests (ObligationAgreementCrud). The accounting-domain specs are strong. |

---

## Critical Findings (address before relying on the system)

### 1. Double-posting prevention is untested
**Source:** Omission Hunter 5.1, GAAP Mapper Domain 10
No scenario attempts to post an already-posted obligation instance. The state
machine provides indirect protection (posted has no outbound transitions), but
the post-to-ledger orchestration is a separate entry point. A missing guard
there means duplicate journal entries — silent data corruption.

**Fix:** Add one scenario: post a confirmed instance, then post it again.
Assert rejection.

### 2. State machine has 4 of 18+ invalid transitions tested
**Source:** Omission Hunter 9.1
The status transition spec tests expected→confirmed rejection and three
others, but leaves 14+ invalid paths untested. The `allowedTransitions` map
is the core integrity mechanism. If any entry is wrong, only those 4 tests
stand between you and a bad transition.

**Fix:** Add a scenario outline covering all invalid transitions. This is
mechanical — one outline, one examples table.

### 3. Opening Balances has no spec file
**Source:** Omission Hunter C.1
Backlog item 010 is marked Done. There is no OpeningBalances.feature. The
domain model defines the types. Either the coverage lives in a test file
without a corresponding spec, or it's untested.

**Fix:** Verify where the test coverage actually is. If it's in test code
but not in Gherkin, write the spec. If there's no test at all, write both.

---

## Significant Findings (address before tax season)

### Account-type validation for obligations
**Source:** GAAP Mapper Domain 6 and 10, Omission Hunter 6.1
No validation prevents a payable agreement with two revenue accounts. The
resulting journal entry would be balanced but semantically meaningless. This
is the highest-risk design gap — it's the one that would produce plausible
but wrong numbers on a Schedule E.

### Income statement period-scoping never verified
**Source:** Omission Hunter 4.2
The income statement claims to be period-scoped, not cumulative. No scenario
creates multi-period entries and verifies only one period's activity appears.
If the query accidentally uses cumulative logic, no test catches it.

### Atomicity of failed obligation-to-ledger post
**Source:** Omission Hunter 5.4
If posting fails (e.g., closed period), no test verifies the instance stays
in confirmed status with no partial journal entry. The transactional integrity
of the cross-domain operation is unverified.

### Skipped transition doesn't verify is_active = false
**Source:** Omission Hunter 9.2
The backlog explicitly requires skipped instances to become inactive. The
spec checks status but not the is_active flag.

### source = dest account not rejected
**Source:** Omission Hunter 6.1
The backlog explicitly calls this out as a required rejection. No scenario
tests it.

### SubtreePL has no spec file
**Source:** Omission Hunter C.2
Backlog item 013 marked Done. No corresponding .feature file.

### Posted/voided entry reconciliation
**Source:** GAAP Mapper Domain 8
If a journal entry created by obligation posting is later voided, the
instance still shows "posted" with a journal_entry_id pointing to a voided
entry. The ops and ledger layers can disagree about reality.

### Period-close side effects untested
**Source:** GAAP Mapper Domain 5
No test verifies that closing a period doesn't modify account balances.
The design computes retained earnings on-the-fly (no closing entries), which
is valid for cash-basis — but the assumption that close is a flag-only
toggle is never tested.

---

## Slop Assessment

The 7% slop rate is acceptable and concentrated in one area:
ObligationAgreementCrud. The CRUD tests use mirror assertions (assert the
returned object matches the input) without independent persistence
verification. Six scenarios are slop or redundant:

- OA-012: Get by ID is a trivial roundtrip
- OA-025: Reactivate asserts what it just set
- SI-014: Duplicate of SI-011
- IS-008: Duplicate of IS-001
- CFP-009: Identical to CFP-001
- ST-022: Missing notes-preservation assertion

The accounting-domain specs (Trial Balance, Balance Sheet, Income Statement,
PostObligationToLedger, Overdue Detection) are well-constructed. Retained
earnings derivation, normal-balance formula with contra-entries, void
exclusion, cumulative vs. period-scoped behaviour — these are genuine tests
that exercise real logic.

---

## What's Solid

Worth stating plainly: BD built a real accounting engine, not a toy.

- **Double-entry is enforced at every layer.** Domain validation,
  service-level checks, and report assertions all agree.
- **Cash basis is consistent.** entry_date = confirmed_date throughout.
  No accidental accrual-basis logic anywhere.
- **Financial statement articulation works.** Income flows into retained
  earnings on the balance sheet. This is tested with derived values, not
  echoed inputs.
- **The normal-balance formula is tested with contra-entries.** IS-011 and
  IS-012 test the formula with mixed debits/credits on revenue and expense
  accounts. This is the kind of test that only exists if you understand what
  you're testing.
- **Void exclusion is comprehensive.** Tested on trial balance, balance
  sheet, and income statement independently.

---

## Recommendations

### Immediate (before next feature work)
1. Add double-posting rejection scenario (1 scenario)
2. Add invalid-transition scenario outline (1 outline, ~18 examples)
3. Locate or create Opening Balances spec
4. Locate or create SubtreePL spec

### Before production reliance
5. Add account-type validation for obligation agreements
6. Add income statement multi-period scoping test
7. Add atomicity test for failed obligation posting
8. Add is_active assertion to skipped transition
9. Add source = dest rejection test

### Cleanup (low priority)
10. Add independent persistence read after CRUD creates/updates
11. Remove or differentiate redundant scenarios (IS-008, SI-014, CFP-009)
12. Add notes-preservation assertion to ST-022

---

*This synthesis was compiled from four independent audit reports. The
individual reports contain full per-scenario detail and are available in
this directory.*
