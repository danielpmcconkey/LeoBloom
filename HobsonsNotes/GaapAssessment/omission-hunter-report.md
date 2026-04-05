# Omission Hunter Report -- LeoBloom Spec Suite

**Date:** 2026-04-05
**Scope:** All 9 assigned spec files, cross-referenced against domain model (Ledger.fs, Ops.fs) and backlog (items 010--022)

---

## Method

For each spec file I catalogued every scenario and what it exercises, then derived expected invariants and edge cases from the domain model and backlog requirements from first principles. The diff between those two lists is this report.

I did not audit PostJournalEntry.feature, VoidJournalEntry.feature, or AccountBalance.feature -- those were not in my assignment -- but I read them to understand what coverage already exists elsewhere when evaluating cross-cutting concerns.

---

## 1. TrialBalance.feature

### What IS tested (11 scenarios)

| Tag | Tests |
|-----|-------|
| TB-001 | Balanced entries produce balanced TB |
| TB-002 | Grouping by account type with subtotals |
| TB-003 | Groups with no activity omitted |
| TB-004 | Voided entries excluded |
| TB-005 | Empty period, zero totals |
| TB-006 | Closed period TB still works |
| TB-007 | Multiple entries accumulate per account |
| TB-008 | Net balance uses normal_balance formula |
| TB-009 | Lookup by key vs ID equivalence |
| TB-010 | Nonexistent period ID error |
| TB-011 | Nonexistent period key error |

### Gaps

**1.1 -- Accounts with contra-normal activity (debit on a credit-normal account)**
The net balance formula test (TB-008) only tests accounts receiving entries on their natural side. There is no scenario where a revenue account (credit-normal) receives a debit, or an asset account (debit-normal) receives a credit, and the net balance is verified. The income statement covers this (IS-011, IS-012), but the trial balance should independently verify its own net balance calculation handles mixed-side activity.
**Severity: Significant**

**1.2 -- Trial balance with only voided entries in a period**
TB-004 tests a mix of good and voided entries. There is no scenario where ALL entries in a period are voided. The expected result is the same as an empty period (TB-005), but the distinction matters: accounts exist in the COA and had activity that was all voided. Does the TB show zero-balance account lines, or does it collapse to empty like TB-005?
**Severity: Minor**

**1.3 -- Account appearing in multiple entries where some are voided**
TB-004 has two entries hitting the same accounts, one voided. But it only checks grand totals, not per-account lines. A scenario should verify that the per-account debit/credit totals are correct after partial voiding within a single account.
**Severity: Minor**

---

## 2. CloseReopenFiscalPeriod.feature

### What IS tested (11 scenarios)

| Tag | Tests |
|-----|-------|
| CFP-001 | Close open period |
| CFP-002 | Reopen closed period with reason |
| CFP-003 | Close already-closed (idempotent) |
| CFP-004 | Reopen already-open (idempotent) |
| CFP-005 | Reopen with invalid reason rejected |
| CFP-006 | Close nonexistent period rejected |
| CFP-007 | Reopen nonexistent period rejected |
| CFP-008 | Full close-reopen-close lifecycle |
| CFP-009 | Close period with no journal entries |
| CFP-010 | Posting rejected after close |
| CFP-011 | Reopen reason logged at Info level |

### Gaps

**2.1 -- Posting succeeds after reopen**
CFP-010 tests that posting fails after close. There is no scenario that closes a period, reopens it, and then verifies that posting succeeds again. The close-reopen-close lifecycle (CFP-008) tests the state toggle but never exercises the posting engine after reopening.
**Severity: Significant**

**2.2 -- Void succeeds/fails relative to period close state**
VoidJournalEntry.feature (VJE-008) covers voiding in a closed period and it succeeds. But the close/reopen spec itself never addresses how voiding interacts with period state. If the design intent is that voids are allowed in closed periods (as VJE-008 suggests), a scenario in the close/reopen spec confirming this intent would make the policy explicit. Not strictly a gap since VJE-008 exists, but the close/reopen spec should own the "what operations are allowed on a closed period" story.
**Severity: Minor**

---

## 3. BalanceSheet.feature

### What IS tested (11 scenarios)

| Tag | Tests |
|-----|-------|
| BS-001 | Balanced books, isBalanced true |
| BS-002 | A = L + E equation verified numerically |
| BS-003 | Positive retained earnings |
| BS-004 | Negative retained earnings |
| BS-005 | Zero retained earnings (no revenue/expense) |
| BS-006 | All zeros before any entries |
| BS-007 | Voided entries excluded |
| BS-008 | Cross-period cumulative entries |
| BS-009 | Multiple accounts per section |
| BS-010 | Zero-balance account with activity still appears |
| BS-011 | Inactive accounts with balances still appear |

### Gaps

**3.1 -- Balance sheet as of a mid-period date**
Every scenario uses an end-of-period date (e.g., 2026-03-31). The balance sheet is defined as "cumulative through a date," but no scenario tests an as-of date that falls mid-period, where some entries in the period are before the date and some after. This is the temporal cut-off behaviour that AccountBalance covers (AB-006), but the balance sheet's own cumulative aggregation should be tested independently.
**Severity: Significant**

**3.2 -- Balance sheet as of a date before any entries**
BS-006 has no entries at all. There is no scenario where entries exist but the as-of date is before all of them. Expected: all zeros. This tests the date filter, not the empty-ledger path.
**Severity: Minor**

**3.3 -- Retained earnings spanning closed periods**
BS-008 tests cross-period entries but all periods are open. There is no scenario verifying that retained earnings correctly accumulate revenue/expense from closed periods. Closing a period should not remove its data from the balance sheet.
**Severity: Minor**

**3.4 -- Balance sheet with opening balances**
Backlog item 010 describes opening balance entries. There is no scenario in the balance sheet spec where opening balances are posted and then verified on the balance sheet. Since opening balances are just journal entries this should work, but it is the primary verification path for story 010.
**Severity: Significant** -- This is the story-010-to-story-012 integration seam. If opening balances are wrong, the entire go-live balance sheet is wrong.

---

## 4. IncomeStatement.feature

### What IS tested (16 scenarios)

| Tag | Tests |
|-----|-------|
| IS-001 | Revenue and expense, correct net income |
| IS-002 | Revenue only, empty expenses |
| IS-003 | Expenses only, empty revenue |
| IS-004 | Voided entries excluded |
| IS-005 | Inactive-account filtering (no activity omitted) |
| IS-006 | Inactive accounts with activity appear |
| IS-007 | Empty period, zero net income |
| IS-008 | Positive net income |
| IS-009 | Negative net income (net loss) |
| IS-010 | Multiple revenue/expense accounts |
| IS-011 | Revenue balance = credits - debits |
| IS-012 | Expense balance = debits - credits |
| IS-013 | Lookup by key vs ID equivalence |
| IS-014 | Nonexistent period ID error |
| IS-015 | Nonexistent period key error |
| IS-016 | Closed period income statement works |

### Gaps

**4.1 -- Income statement excludes asset/liability/equity accounts**
The income statement is defined to include only revenue and expense accounts. No scenario posts entries involving asset, liability, and equity accounts and then verifies that only revenue/expense lines appear in the report. Every scenario uses assets as the other side of the entry, but the assertion is only on the revenue/expense sections. A scenario should explicitly assert that asset/liability/equity accounts are absent from the report.
**Severity: Minor** -- The existing scenarios imply this by only asserting revenue/expense sections, but an explicit assertion would make the filtering contract visible.

**4.2 -- Income statement is period-scoped, not cumulative**
The feature description says "period-scoped activity only, not cumulative," but no scenario creates entries in multiple periods and verifies that the income statement for one period excludes the other period's activity. The balance sheet has BS-008 for its cumulative nature. The income statement needs the mirror test.
**Severity: Significant** -- If the query accidentally uses cumulative logic instead of period-scoped, no test catches it.

---

## 5. PostObligationToLedger.feature

### What IS tested (15 scenarios)

| Tag | Tests |
|-----|-------|
| POL-001 | Receivable: correct JE + transition to posted |
| POL-002 | Payable: correct JE + transition to posted |
| POL-003 | JE description matches agreement/instance names |
| POL-004 | JE source and reference |
| POL-005 | JE entry_date = confirmed_date |
| POL-006 | Instance journal_entry_id set after posting |
| POL-007 | Non-confirmed instance rejected |
| POL-008 | No amount rejected |
| POL-009 | No confirmed_date rejected |
| POL-010 | No source_account rejected |
| POL-011 | No dest_account rejected |
| POL-012 | No fiscal period for date rejected |
| POL-013 | Closed fiscal period rejected |
| POL-014 | findByDate returns correct period |
| POL-015 | findByDate returns None outside range |

### Gaps

**5.1 -- Posting an already-posted instance**
There is no scenario that attempts to post an instance that is already in "posted" status. The status transition spec (ST-013) rejects posted->confirmed, but that is a different path. The post-to-ledger orchestration should reject an already-posted instance to prevent double journal entries.
**Severity: Critical** -- Double posting creates duplicate journal entries. Silent data corruption.

**5.2 -- Inactive source or dest account on agreement**
Backlog item 018 calls out: "Source or dest account is inactive -> rejected by posting engine." No scenario tests this. POL-010/011 test missing accounts, but not inactive ones. The posting engine (PJE-015) rejects inactive accounts, but the obligation-to-ledger orchestration is a different entry point and should be tested end-to-end.
**Severity: Significant**

**5.3 -- Posting when instance amount differs from agreement amount**
The status transition spec allows amount override at confirmation (ST-010). But the post-to-ledger spec always uses the instance amount without testing that it might differ from the agreement's original amount. A scenario should verify that the JE uses the instance-level amount, not the agreement-level amount.
**Severity: Minor** -- Likely works correctly, but the contract should be explicit.

**5.4 -- Atomicity: failed post leaves instance in confirmed status**
Backlog item 018 states: "If any validation fails, the post-to-ledger operation fails and the instance stays in confirmed status." No scenario verifies that a failed post (e.g., closed period) leaves the instance unchanged -- no partial JE, no status change.
**Severity: Significant** -- This is the transactional integrity contract for cross-domain operations.

---

## 6. ObligationAgreementCrud.feature

### What IS tested (28 scenarios)

| Tag | Tests |
|-----|-------|
| OA-001 to OA-002 | Create with all/required fields |
| OA-003 to OA-008 | Create pure validation (name, counterparty, amount, expectedDay, multi-error) |
| OA-009 to OA-011 | Create DB validation (nonexistent/inactive accounts) |
| OA-012 to OA-013 | Get by ID (found/not found) |
| OA-014 to OA-018 | List (default filter, by type, by cadence, no filter, empty) |
| OA-019 | Update all fields |
| OA-020 to OA-024 | Update errors (nonexistent, empty name, non-positive amount, bad expectedDay, inactive account) |
| OA-025 | Reactivate deactivated agreement |
| OA-026 to OA-028 | Deactivate (happy, nonexistent, blocked by active instances) |

### Gaps

**6.1 -- Create or update with source_account_id = dest_account_id**
Backlog item 014 explicitly says: "Agreement where source and dest are the same account -> reject. That's meaningless." No scenario tests this rejection. The domain model allows both fields to be set to the same value.
**Severity: Significant** -- The backlog explicitly calls this out as a required rejection.

**6.2 -- Create with inactive dest account**
OA-010 tests inactive source account rejection. OA-011 tests nonexistent dest account. There is no scenario testing create with an inactive dest account.
**Severity: Significant** -- Asymmetric validation coverage. If the dest account validation only checks existence but not active status, an inactive account could be wired into an agreement.

**6.3 -- Update with nonexistent source or dest account**
OA-024 tests update with inactive source account. There is no scenario for update with a nonexistent source account or nonexistent dest account.
**Severity: Minor**

**6.4 -- Deactivate an already-inactive agreement**
OA-026 deactivates an active one. OA-027 tests nonexistent. There is no idempotency test for deactivating an already-inactive agreement.
**Severity: Minor**

**6.5 -- Update with counterparty exceeding 100 characters**
OA-005 tests create with long counterparty. No corresponding update scenario.
**Severity: Minor**

---

## 7. OverdueDetection.feature

### What IS tested (11 scenarios)

| Tag | Tests |
|-----|-------|
| OD-001 | Single overdue instance transitioned |
| OD-002 | Multiple overdue instances |
| OD-003 | Instance on reference date NOT overdue |
| OD-004 | Instance after reference date NOT overdue |
| OD-005 | In-flight not flagged |
| OD-006 | Already overdue not re-transitioned |
| OD-007 | Confirmed not flagged |
| OD-008 | Inactive not flagged |
| OD-009 | Idempotency (run twice) |
| OD-010 | Mixed set, only eligible transitioned |
| OD-011 | No candidates, zero transitioned |

### Gaps

**7.1 -- In-flight instances past expected date**
Backlog item 017 says: "in_flight means it's been initiated but hasn't settled, which is a different kind of overdue." The spec correctly excludes in-flight from the expected->overdue auto-transition (OD-005). However, the backlog also describes output including "Current status (expected vs in_flight)" and says in-flight instances should "appear in the overdue report with a flag." The spec tests the transition but not the reporting/query output. If a separate query function is planned, it should have its own scenarios.
**Severity: Significant** -- The in-flight-but-past-due detection is explicitly called out in the backlog as a signal for the nagging agent.

**7.2 -- Posted instances are not flagged**
OD-005/006/007 test in_flight, overdue, and confirmed exclusion. There is no scenario for posted instances (which are terminal and should never be flagged). Skipped instances are also not tested.
**Severity: Minor** -- The query filters on status=expected, so other statuses are implicitly excluded, but completeness of the exclusion set matters.

**7.3 -- Error handling when a transition fails mid-batch**
The `OverdueDetectionResult` type has an `errors` field of `(int * string) list`. No scenario tests what happens when one transition in a batch fails (e.g., due to a DB error). The result type suggests partial success is possible, but no spec exercises it.
**Severity: Significant** -- The result type explicitly models partial failure, but no test verifies the behaviour.

---

## 8. SpawnObligationInstances.feature

### What IS tested (18 scenarios)

| Tag | Tests |
|-----|-------|
| SI-001 to SI-004 | Monthly date generation (full, Feb clamp non-leap, Feb clamp leap, no expectedDay default) |
| SI-005 to SI-006 | Quarterly date generation (full year, partial range) |
| SI-007 | Annual date generation (multi-year) |
| SI-008 | OneTime date generation |
| SI-009 | Start after end rejected |
| SI-010 | Name generation for all cadences |
| SI-011 | Spawn monthly happy path (3 months) |
| SI-012 | Variable amount leaves instance amount empty |
| SI-013 | OneTime spawn |
| SI-014 | All spawned instances isActive true |
| SI-015 | Overlapping range skips existing, creates new |
| SI-016 | OneTime already exists, skips |
| SI-017 | Inactive agreement rejected |
| SI-018 | Nonexistent agreement rejected |

### Gaps

**8.1 -- Quarterly date generation with expectedDay clamping**
SI-002/003 test monthly day-31 clamping for February. There is no test for quarterly clamping (e.g., expectedDay=31 in a quarter starting April, which has 30 days). The domain code uses `clampDay` for all cadences, but only monthly is tested.
**Severity: Minor**

**8.2 -- Annual cadence with expectedDay clamping (leap year Feb)**
If an annual agreement has expectedDay=29 and anchorMonth=February, leap vs non-leap year clamping applies. Not tested.
**Severity: Minor**

**8.3 -- Spawn for range that is entirely already covered**
Backlog item 015 says: "Spawning for a range that's entirely already covered -> no-op, success." SI-016 covers this for OneTime, but not for monthly/quarterly/annual where multiple instances all already exist.
**Severity: Minor**

**8.4 -- Spawned instances inherit agreement name**
The backlog says instance display combines agreement + instance name (e.g., "Jeffrey rent -- Apr 2026"). The spec tests instance names (SI-010, SI-011) but not whether the agreement name linkage is correct. The `ObligationInstance` type has its own `name` field; there is no test verifying `obligationAgreementId` is correctly set on spawned instances.
**Severity: Minor**

---

## 9. StatusTransitions.feature

### What IS tested (22 scenarios)

| Tag | Tests |
|-----|-------|
| ST-001 to ST-009 | All valid forward transitions (expected->in_flight, expected->confirmed, expected->confirmed with amount, in_flight->confirmed, expected->overdue, in_flight->overdue, overdue->confirmed, confirmed->posted, expected->skipped) |
| ST-010 | Amount override at confirmation |
| ST-011 | No amount anywhere -> confirmed fails |
| ST-012 to ST-015 | Invalid transitions (confirmed->expected, posted->confirmed, skipped->confirmed, overdue->in_flight) |
| ST-016 to ST-019 | Guard violations (posted without JE, posted with nonexistent JE, skipped without notes, confirmed without date) |
| ST-020 | Inactive instance rejected |
| ST-021 | Nonexistent instance rejected |
| ST-022 | Skipped succeeds when instance already has notes |

### Gaps

**9.1 -- Incomplete invalid transition coverage**
The domain model defines `allowedTransitions` with specific from->to pairs. The spec tests 4 invalid transitions but the full invalid set is much larger. Missing rejections that should be tested:

- posted -> expected (terminal state, no backward)
- posted -> in_flight
- posted -> overdue
- posted -> skipped
- skipped -> expected
- skipped -> in_flight
- skipped -> overdue
- skipped -> posted
- confirmed -> in_flight
- confirmed -> overdue
- confirmed -> skipped
- overdue -> expected
- overdue -> skipped
- overdue -> posted (must go through confirmed first)
- in_flight -> expected
- in_flight -> posted
- in_flight -> skipped
- expected -> posted (must go through confirmed first)

The spec proves 4 invalid transitions are rejected but leaves 14+ untested. Any bug in the transition map for these paths would go undetected.
**Severity: Critical** -- The state machine is the core integrity mechanism of the ops domain. Every invalid transition should be a tested rejection, not assumed from four samples.

**9.2 -- Skipped sets is_active = false**
Backlog item 016 says: "-> skipped: set is_active = false, update modified_at." ST-009 tests that status becomes "skipped" and notes are set, but does not assert that `isActive` becomes false.
**Severity: Significant** -- This is a documented field-update requirement that is not verified.

**9.3 -- Transition to confirmed with zero amount**
ST-003 provides an amount on the command. ST-011 tests no amount. There is no test where the command provides amount = 0 or a negative amount at confirmation time. The domain validates positive amounts for agreements but the transition path may not.
**Severity: Significant** -- If an amount of 0.00 slips through confirmation, the downstream post-to-ledger will create a journal entry with zero-amount lines, which the posting engine rejects (PJE-007). But the error would surface at posting time, not at confirmation time, making debugging harder.

**9.4 -- modified_at is updated on transition**
Backlog item 016 lists modified_at updates for every transition. No scenario asserts that modified_at changes after a transition.
**Severity: Minor**

**9.5 -- Expected -> confirmed skipping in_flight**
ST-002/003 test expected->confirmed directly, which is valid per the state machine. However, this bypasses in_flight. There is no test verifying the data integrity implications -- specifically that confirmedDate and amount are properly set even when in_flight is skipped.
**Severity: Minor** -- ST-002 and ST-003 do verify these fields. Noting for completeness that the skip-over path is tested.

---

## Cross-Cutting Gaps

These gaps span multiple spec files and concern systemic properties of the system.

**C.1 -- No opening balance spec exists**
Backlog item 010 (Opening Balances) is marked "Done" but there is no OpeningBalances.feature file anywhere in the Specs directory. The domain model defines `PostOpeningBalancesCommand` and `OpeningBalanceEntry` types. If opening balances were implemented as part of another feature's spec, I cannot find those scenarios. This is either an entirely missing spec file, or the coverage lives somewhere I was not directed to look.
**Severity: Critical** -- Opening balances are the go-live mechanism. If untested, the initial state of the entire ledger is unverified.

**C.2 -- No SubtreePL spec exists**
Backlog item 013 (P&L by Account Subtree) is marked "Done." The domain model defines `SubtreePLReport`. There are no scenarios anywhere testing subtree P&L.
**Severity: Significant** -- A completed backlog item with no corresponding spec.

**C.3 -- The A = L + E invariant is tested in one scenario, not as a structural invariant**
BS-002 tests the accounting equation with specific numbers. This is verification-by-example, not invariant enforcement. There is no property-based or parameterised test that asserts A = L + E holds for arbitrary sets of balanced entries. Given that this is the fundamental GAAP invariant, a single scenario with fixed numbers is fragile.
**Severity: Significant** -- One scenario with specific numbers is not the same as invariant enforcement.

**C.4 -- No end-to-end obligation lifecycle test**
The system has two parallel tracks (ledger and ops) that converge at story 018 (post obligation to ledger). There is no single scenario that exercises the full lifecycle: create agreement -> spawn instance -> transition through expected -> in_flight -> confirmed -> post to ledger -> verify on trial balance / income statement / balance sheet. Each piece is tested in isolation, but the integration across the full pipeline is not.
**Severity: Significant** -- Integration seams are where production bugs hide.

**C.5 -- Transfer and Invoice domains have zero specs**
Backlog items 019 (Transfers), 020 (Invoice Readiness), 021 (Generate Invoice) are "Not started." The domain model defines `Transfer` and `Invoice` types. I note this for completeness: these are not gaps in existing specs but areas of the domain model with no spec coverage at all. As these stories move to active, they will need full spec suites.
**Severity: N/A** -- Not started backlog items. Noted for awareness.

---

## Summary

| Severity | Count |
|----------|-------|
| Critical | 3 |
| Significant | 13 |
| Minor | 14 |

### Critical findings:

1. **5.1** -- No test prevents double-posting an already-posted obligation instance (silent data corruption risk)
2. **9.1** -- Only 4 of 18+ invalid status transitions are tested (state machine integrity)
3. **C.1** -- Opening balances (backlog item 010, marked Done) has no spec file at all

### Highest-impact significant findings:

- **4.2** -- Income statement period-scoping vs cumulative is never verified
- **5.4** -- Atomicity of failed obligation-to-ledger post is not tested
- **6.1** -- source = dest account on agreement not rejected despite explicit backlog requirement
- **7.3** -- Partial failure in overdue batch detection is modelled but untested
- **9.2** -- Skipped transition does not verify is_active = false

---

*Signed,*
**Omission Hunter**
*Adversarial completeness auditor*
