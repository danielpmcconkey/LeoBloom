# Remediation Brief — GAAP Assessment Findings

**From:** Hobson (PO)
**Date:** 2026-04-05
**Source:** Independent GAAP assessment of Projects 001–018 specs.
Full reports in `HobsonsNotes/GaapAssessment/`.

---

## Context

Four auditors reviewed every Gherkin spec in the repo against GAAP
principles, arithmetic correctness, completeness, and AI-generated slop
patterns. The engine is sound — zero arithmetic errors, core principles
enforced, 68% of scenarios rated solid.

This brief covers what needs fixing. Most items are adding scenarios to
existing spec files — not new features. The one exception is noted.

**This is not a new project.** Don't spin up the full pipeline for each
item. The BA writes the Gherkin, the Builder implements tests, the
Governor verifies. No brainstorm, no plan docs. These are patches to
completed work.

---

## Critical (block further feature work)

### C1. Double-posting prevention — PostObligationToLedger.feature

**What's missing:** No scenario attempts to post an already-posted
obligation instance.

**What the scenario should prove:** Posting an instance that is already
in "posted" status returns an error. No second journal entry is created.
The existing journal_entry_id is unchanged.

**Why it matters:** Without this, a code bug or race condition creates
duplicate journal entries. Silent data corruption.

**Spec file:** `Specs/Behavioral/PostObligationToLedger.feature`

---

### C2. Invalid status transitions — StatusTransitions.feature

**What's missing:** Only 4 of 18+ invalid transitions are tested. The
spec proves the state machine rejects `confirmed→expected`,
`posted→confirmed`, `skipped→confirmed`, and `overdue→in_flight`. The
remaining 14+ invalid paths are untested.

**What the scenario should prove:** A scenario outline with an examples
table covering every invalid from→to pair. Each row asserts rejection.
The full invalid set (derive from the `allowedTransitions` map in
`Ops.fs`):

- From posted: →expected, →in_flight, →overdue, →confirmed, →skipped
- From skipped: →expected, →in_flight, →overdue, →confirmed, →posted
- From confirmed: →expected, →in_flight, →overdue, →skipped
- From overdue: →expected, →skipped, →posted
- From in_flight: →expected, →posted, →skipped
- From expected: →posted

**Why it matters:** The state machine is the core integrity mechanism of
the ops domain. A bug in the transition map for any untested path goes
undetected.

**Spec file:** `Specs/Ops/StatusTransitions.feature`

---

### C3. Missing spec files for completed work

Two backlog items are marked Done with no corresponding .feature file:

**C3a. Opening Balances (Project 010)**
Domain types exist (`PostOpeningBalancesCommand`, `OpeningBalanceEntry`).
If test coverage exists in code but not in Gherkin, write the spec to
match. If there's no test coverage, write both spec and tests.

**C3b. P&L by Account Subtree (Project 013)**
Domain type exists (`SubtreePLReport`). Same situation — locate existing
test coverage or create it.

**Why it matters:** Spec-driven development means the spec is the
product. Completed work without specs is undocumented behaviour that
can't be rebuilt from the manifest.

---

## Significant (address before production reliance)

### S1. Account-type validation for obligations — NEW FUNCTIONALITY

**This is the one item that requires new code, not just new scenarios.**

**What's missing:** No validation prevents creating an obligation
agreement where source and dest account types are inappropriate for the
obligation direction. A payable with two revenue accounts is accepted.
The resulting journal entry is balanced but semantically wrong.

**What the validation should enforce:**
- Payable: source should be asset (where money comes from), dest should
  be expense (what it pays for)
- Receivable: source should be revenue (what earned it), dest should be
  asset (where money lands)
- At minimum: reject source_account_id = dest_account_id (backlog item
  014 explicitly requires this and it's untested)

**Where to enforce:** Either in agreement creation/update validation, or
in the post-to-ledger path. Agreement creation is better — fail early.

**Why it matters:** This is the gap most likely to produce wrong numbers
on a Schedule E. A misconfigured agreement silently records incorrect
transactions.

**Spec files:** `Specs/Ops/ObligationAgreementCrud.feature` and/or
`Specs/Behavioral/PostObligationToLedger.feature`

---

### S2. Income statement period-scoping — IncomeStatement.feature

**What's missing:** No scenario creates entries in multiple periods and
verifies the income statement for one period excludes the other's
activity.

**What the scenario should prove:** Entries exist in periods A and B.
Income statement for period A shows only period A's revenue and expenses.

**Spec file:** `Specs/Behavioral/IncomeStatement.feature`

---

### S3. Atomicity of failed obligation posting — PostObligationToLedger.feature

**What's missing:** No scenario verifies that a failed post (e.g.,
closed period) leaves the obligation instance in confirmed status with
no partial journal entry.

**What the scenario should prove:** Attempt to post to a closed period.
Assert: instance status is still "confirmed", instance journal_entry_id
is still null, no journal entry was created.

**Spec file:** `Specs/Behavioral/PostObligationToLedger.feature`

---

### S4. Skipped sets is_active = false — StatusTransitions.feature

**What's missing:** ST-009 tests the skipped transition but only asserts
status. Backlog item 016 requires is_active = false.

**What the scenario should prove:** After transitioning to skipped,
is_active is false.

**Spec file:** `Specs/Ops/StatusTransitions.feature`

---

### S5. source = dest account rejection — ObligationAgreementCrud.feature

**What's missing:** Backlog item 014 says "Agreement where source and
dest are the same account → reject. That's meaningless." No scenario
tests this.

**What the scenario should prove:** Creating an agreement with
source_account_id = dest_account_id returns a validation error.

**Spec file:** `Specs/Ops/ObligationAgreementCrud.feature`

---

### S6. Inactive dest account on create — ObligationAgreementCrud.feature

**What's missing:** OA-010 tests inactive source account rejection.
OA-011 tests nonexistent dest. No scenario tests create with an inactive
dest account.

**What the scenario should prove:** Creating an agreement with an
inactive dest_account_id returns an error.

**Spec file:** `Specs/Ops/ObligationAgreementCrud.feature`

---

### S7. Period-close side effects — CloseReopenFiscalPeriod.feature

**What's missing:** No test verifies that closing a period doesn't
modify account balances. The system uses computed retained earnings
(no closing entries), which is valid — but the assumption that close
is flag-only is untested.

**What the scenario should prove:** Post entries, record the trial
balance. Close the period. Run the trial balance again. Totals are
identical.

**Spec file:** `Specs/Behavioral/CloseReopenFiscalPeriod.feature`

---

### S8. Posted/voided entry reconciliation — design decision

**What's missing:** If a journal entry created by obligation posting is
voided, the obligation instance still shows "posted" with a
journal_entry_id pointing to a voided entry. Ops and ledger disagree.

**This is a design question, not a spec gap.** Options:
- Accept the inconsistency (document it)
- Add a "void posted obligation" operation that reverts the instance
- Add a query that flags posted instances whose journal entries are voided

**Raise with Dan.** No action until a decision is made.

---

## Cleanup (low priority, do when convenient)

### L1. CRUD persistence verification

OA-001, OA-002, OA-019, OA-025, OA-026 assert on returned objects
without an independent read. Add a `getById` or `list` call after
create/update/deactivate/reactivate to verify persistence.

### L2. Remove redundant scenarios

- IS-008 is identical to IS-001 with different numbers
- SI-014 is a subset of SI-011
- CFP-009 is identical to CFP-001

Either remove or differentiate these.

### L3. ST-022 notes preservation

ST-022 (skip with existing notes) doesn't assert the notes survived.
Add the assertion.

---

## Sequencing Suggestion

1. C1, C2, C3 first — these are mechanical and fast
2. S1 next — it's the only new code and the highest-risk gap
3. S2–S7 can be done in any order
4. S8 needs a Dan decision
5. L1–L3 whenever
