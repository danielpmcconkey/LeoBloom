# GAAP Remediation Backlog

**Created:** 2026-04-05
**Source:** Hobson's GAAP assessment of Projects 001-018
**Full brief:** `BdsNotes/remediation-2026-04-05.md`
**Synthesis:** `HobsonsNotes/GaapAssessment/synthesis.md`

**Track:** Remediation (parallel to critical path, does not block Project 020)
**Process:** Lightweight — BA writes Gherkin, Builder implements, Governor
verifies. No brainstorm, no plan docs.

---

## Priority Tiers

- **Critical:** Gaps that could cause silent data corruption or leave
  completed work undocumented. Address first.
- **Significant:** Gaps that could produce wrong numbers or leave invariants
  unverified. Address before production reliance.
- **Cleanup:** Redundancy, weak assertions, minor hygiene. Do when convenient.

---

## Stories

### REM-001: Double-posting prevention

**Priority:** Critical
**Spec file:** `Specs/Behavioral/PostObligationToLedger.feature`
**Effort:** Small

**Acceptance criteria:**
1. A scenario posts a confirmed obligation instance, then attempts to post
   the same instance again.
2. The second post returns an error (not a success).
3. No second journal entry is created.
4. The original journal_entry_id on the instance is unchanged.

**Dependencies:** None

---

### REM-002: Complete invalid status transition coverage

**Priority:** Critical
**Spec file:** `Specs/Ops/StatusTransitions.feature`
**Effort:** Small

**Acceptance criteria:**
1. A scenario outline with an examples table covers every invalid from/to
   pair derived from the `allowedTransitions` map in `Ops.fs`.
2. The full invalid set includes (at minimum):
   - From posted: expected, in_flight, overdue, confirmed, skipped
   - From skipped: expected, in_flight, overdue, confirmed, posted
   - From confirmed: expected, in_flight, overdue, skipped
   - From overdue: expected, skipped, posted
   - From in_flight: expected, posted, skipped
   - From expected: posted
3. Every row asserts rejection with no state change.

**Dependencies:** None

---

### REM-003: Skipped transition sets is_active = false

**Priority:** Significant
**Spec file:** `Specs/Ops/StatusTransitions.feature`
**Effort:** Small

**Acceptance criteria:**
1. After transitioning an instance to "skipped" status, is_active is false.
2. This can be an additional assertion on the existing ST-009 scenario or
   a new companion scenario.

**Dependencies:** None (but group with REM-002 for efficiency)

---

### REM-004: ST-022 notes preservation assertion

**Priority:** Cleanup
**Spec file:** `Specs/Ops/StatusTransitions.feature`
**Effort:** Small

**Acceptance criteria:**
1. ST-022 (skip with existing notes) asserts that the original notes value
   survived the transition, not just that the status changed.

**Dependencies:** None (but group with REM-002/003 for efficiency)

---

### REM-005: Opening Balances spec

**Priority:** Critical
**Spec file:** NEW — `Specs/Behavioral/OpeningBalances.feature`
**Effort:** Medium

**Acceptance criteria:**
1. Locate existing test coverage for Project 010 (Opening Balances).
2. If test code exists without Gherkin, write the .feature file to match
   what's already tested.
3. If no test coverage exists, write both spec and test implementations.
4. The spec must cover the core behavior: posting opening balances creates
   journal entries that establish starting account balances for a period.

**Dependencies:** None

---

### REM-006: P&L by Account Subtree spec

**Priority:** Critical
**Spec file:** NEW — `Specs/Behavioral/SubtreePLReport.feature`
**Effort:** Medium

**Acceptance criteria:**
1. Locate existing test coverage for Project 013 (P&L by Account Subtree).
2. If test code exists without Gherkin, write the .feature file to match.
3. If no test coverage exists, write both spec and test implementations.
4. The spec must cover: given a parent account with child accounts that
   have revenue/expense entries, the subtree P&L report aggregates only
   the descendants of the specified root.

**Dependencies:** None

---

### REM-007: Atomicity of failed obligation posting

**Priority:** Significant
**Spec file:** `Specs/Behavioral/PostObligationToLedger.feature`
**Effort:** Small

**Acceptance criteria:**
1. A scenario attempts to post a confirmed obligation instance to a closed
   period.
2. The post fails (returns error).
3. Instance status remains "confirmed".
4. Instance journal_entry_id remains null.
5. No journal entry was created.

**Dependencies:** None (but group with REM-001 for efficiency)

---

### REM-008: Source = dest account rejection

**Priority:** Significant
**Spec file:** `Specs/Ops/ObligationAgreementCrud.feature`
**Effort:** Small

**Acceptance criteria:**
1. Creating an obligation agreement where source_account_id equals
   dest_account_id returns a validation error.
2. No agreement record is persisted.

**Dependencies:** None

---

### REM-009: Inactive dest account rejection on create

**Priority:** Significant
**Spec file:** `Specs/Ops/ObligationAgreementCrud.feature`
**Effort:** Small

**Acceptance criteria:**
1. Creating an obligation agreement with an inactive dest_account_id
   returns a validation error.
2. Mirrors the existing OA-010 behavior (inactive source rejection) but
   for the dest account.

**Dependencies:** None (but group with REM-008 for efficiency)

---

### REM-010: Income statement period-scoping verification

**Priority:** Significant
**Spec file:** `Specs/Behavioral/IncomeStatement.feature`
**Effort:** Small

**Acceptance criteria:**
1. Entries exist in two distinct fiscal periods (A and B).
2. Income statement generated for period A shows only period A's revenue
   and expenses.
3. Period B's activity does not appear.

**Dependencies:** None

---

### REM-011: Period-close side effects verification

**Priority:** Significant
**Spec file:** `Specs/Behavioral/CloseReopenFiscalPeriod.feature`
**Effort:** Small

**Acceptance criteria:**
1. Post journal entries and record the trial balance totals.
2. Close the period.
3. Run the trial balance again.
4. Totals are identical — closing the period did not modify any balances.

**Dependencies:** None

---

### REM-012: Account-type validation for obligation agreements

**Priority:** Significant
**Spec file:** `Specs/Ops/ObligationAgreementCrud.feature` and/or
`Specs/Behavioral/PostObligationToLedger.feature`
**Effort:** Medium

**NOTE: This is the one story that requires new production code, not just
new scenarios and test implementations.**

**Acceptance criteria:**
1. Creating a payable agreement where source is not an asset account or
   dest is not an expense account returns a validation error.
2. Creating a receivable agreement where source is not a revenue account
   or dest is not an asset account returns a validation error.
3. Validation occurs at agreement creation time (fail early), not at
   post time.
4. Existing valid agreements continue to work.

**Dependencies:** REM-008 (source = dest rejection) should land first
since it's simpler validation in the same area.

---

### REM-013: CRUD persistence verification

**Priority:** Cleanup
**Spec file:** `Specs/Ops/ObligationAgreementCrud.feature`
**Effort:** Small

**Acceptance criteria:**
1. OA-001, OA-002, OA-019, OA-025, OA-026 each include an independent
   read (getById or list) after the create/update/deactivate/reactivate
   operation to verify persistence.
2. The read result matches the expected state (not just the returned object
   from the mutation).

**Dependencies:** None

---

### REM-014: Remove or differentiate redundant scenarios

**Priority:** Cleanup
**Spec files:**
- `Specs/Behavioral/IncomeStatement.feature` (IS-008 vs IS-001)
- `Specs/Ops/SpawnObligationInstances.feature` (SI-014 vs SI-011)
- `Specs/Behavioral/CloseReopenFiscalPeriod.feature` (CFP-009 vs CFP-001)
**Effort:** Small

**Acceptance criteria:**
1. Each redundant pair is either removed (if truly identical) or
   differentiated (if there's a meaningful distinction to draw).
2. No scenario loss — if removal, the surviving scenario must cover the
   deleted one's intent.

**Dependencies:** None

---

## Resolved Design Decision

### S8: Posted/voided entry reconciliation

**Status:** RESOLVED — 2026-04-05. Decision: Accept + detect.

**Design intent:** The obligation instance is a historical record. "Posted"
means "we posted this." A voided backing journal entry is a ledger correction
— it does not rewrite ops history. There is no reverse-sync between ledger
and ops, no new status, no new code.

- The instance retains its journal_entry_id as a historical reference.
- "Posted instance with voided JE" is a distinct condition, not "settled."

**Immediate (REM-015):** Document the design intent in the
PostObligationToLedger spec or domain types. No code changes.

**Future backlog:** Orphaned posting detection — a read-only diagnostic query
that returns all obligation instances in posted status whose journal_entry_id
references a voided entry. The nagging agent runs it and surfaces results as
"needs attention." No state changes. No priority — slot after current feature
work.

---

### REM-015: Document posted/voided design intent

**Priority:** Significant
**Spec file:** `Specs/Behavioral/PostObligationToLedger.feature` (comment/doc)
and/or domain types in `Ops.fs`
**Effort:** Small

**Acceptance criteria:**
1. A comment or doc note in the PostObligationToLedger spec states: a voided
   journal entry does not change the obligation instance's posted status. The
   instance retains its journal_entry_id as a historical reference.
2. Any future reporting or nagging agent treats "posted instance with voided
   JE" as a distinct condition requiring attention, not as a settled obligation.

---

## Recommended Execution Order

**Batch 1 — StatusTransitions (group for efficiency):**
REM-002, REM-003, REM-004

**Batch 2 — PostObligationToLedger:**
REM-001, REM-007, REM-015

**Batch 3 — Missing specs:**
REM-005, REM-006

**Batch 4 — ObligationAgreementCrud validations:**
REM-008, REM-009

**Batch 5 — Reporting edge cases:**
REM-010, REM-011

**Batch 6 — New code (account-type validation):**
REM-012

**Batch 7 — Cleanup (whenever):**
REM-013, REM-014

Within each batch, higher-numbered items can proceed in parallel. Batches
1-3 are Critical priority and should be addressed first. Batches 4-5 are
Significant and should complete before production reliance. Batch 6 is
Significant but separated because it requires new production code. Batch 7
is Cleanup — no urgency.

---

## Summary

| Story | Title | Priority | Effort | Batch |
|-------|-------|----------|--------|-------|
| REM-001 | Double-posting prevention | Critical | Small | 2 |
| REM-002 | Complete invalid transition coverage | Critical | Small | 1 |
| REM-003 | Skipped sets is_active = false | Significant | Small | 1 |
| REM-004 | ST-022 notes preservation | Cleanup | Small | 1 |
| REM-005 | Opening Balances spec | Critical | Medium | 3 |
| REM-006 | P&L by Account Subtree spec | Critical | Medium | 3 |
| REM-007 | Atomicity of failed posting | Significant | Small | 2 |
| REM-008 | Source = dest rejection | Significant | Small | 4 |
| REM-009 | Inactive dest account rejection | Significant | Small | 4 |
| REM-010 | Income statement period-scoping | Significant | Small | 5 |
| REM-011 | Period-close side effects | Significant | Small | 5 |
| REM-012 | Account-type validation (new code) | Significant | Medium | 6 |
| REM-013 | CRUD persistence verification | Cleanup | Small | 7 |
| REM-014 | Remove redundant scenarios | Cleanup | Small | 7 |
| REM-015 | Document posted/voided design intent | Significant | Small | 2 |
