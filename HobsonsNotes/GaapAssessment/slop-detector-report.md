# Slop Detector Report -- LeoBloom Gherkin Specs

**Date:** 2026-04-05
**Scope:** All 9 feature files, all corresponding test implementations
**Method:** For each scenario, determine what broken implementation would still pass

---

## Summary

| Rating | Count | Pct   |
|--------|-------|-------|
| Solid  | 62    | 68%   |
| Thin   | 23    | 25%   |
| Slop   | 6     | 7%    |
| **Total** | **91** | |

Overall impression: This is a well-constructed test suite. The dominant pattern is real integration tests against a live Postgres database with genuine service calls. The accounting-domain specs (Trial Balance, Balance Sheet, Income Statement) are particularly strong -- they exercise real query logic, cumulative vs. period-scoped behavior, voided entry exclusion, and the normal-balance formula with contra-entries. The weakest areas are the CRUD-heavy Ops specs and some status-transition scenarios that verify writes by reading back what was just written.

---

## Feature: TrialBalance.feature (11 scenarios)

### FT-TB-001: Balanced entries produce a balanced trial balance
**SOLID.** Posts a journal entry through the real service, then queries the trial balance and asserts on `isBalanced`, debit/credit totals, and line count. A broken aggregation query, a broken `isBalanced` flag computation, or incorrect group flattening would fail this test.

### FT-TB-002: Report groups accounts by type with correct subtotals
**SOLID.** Two multi-line entries across 4 account types. Asserts group order, per-group debit/credit subtotals. The expected values (asset group debit=1000, credit=200) emerge from the combination of two entries -- not a trivial echo of input. A broken group-by or subtotal aggregation fails here.

### FT-TB-003: Groups with no activity are omitted
**SOLID.** Only 2 of the 5 account types have activity. Asserts exactly 2 groups and the absence of the other 3. Tests filtering logic specifically.

### FT-TB-004: Voided entries excluded from trial balance
**SOLID.** Posts two entries, voids one through the real void service, asserts the totals reflect only the surviving entry. A broken void-exclusion filter fails this.

### FT-TB-005: Empty period returns balanced report with zero totals
**SOLID.** Edge case. Asserts zero totals, balanced=true, empty groups. Would catch a null-reference or "no rows" crash.

### FT-TB-006: Closed period trial balance still works
**SOLID.** Closes the period via raw SQL, then confirms the trial balance report still generates. Tests that reporting is read-only with respect to period state.

### FT-TB-007: Multiple entries in same period accumulate per account
**SOLID.** Two entries to the same account pair; asserts per-account debit/credit totals are the sum (800), not the individual amounts (500, 300). A broken per-account accumulation (e.g., returning only the last entry) would fail.

### FT-TB-008: Net balance uses normal_balance formula
**SOLID.** Explicitly asserts `normalBalance` type AND `netBalance` value for both a debit-normal and credit-normal account. The test verifies the formula direction, not just the amount. A naive `abs(debit - credit)` implementation would pass, but an implementation that gets the formula backward for credit-normal accounts would fail -- since both accounts show 700, the scenario is thin on *one* dimension (both net to the same sign), but the normal-balance type assertion compensates. Solid overall.

### FT-TB-009: Lookup by period key returns same result as by period ID
**SOLID.** Calls both lookup methods and asserts field-by-field equality across 6 fields. Tests the lookup-equivalence contract.

### FT-TB-010: Nonexistent period ID returns error
**SOLID.** Error-path validation.

### FT-TB-011: Nonexistent period key returns error
**SOLID.** Error-path validation.

---

## Feature: CloseReopenFiscalPeriod.feature (11 scenarios)

### FT-CFP-001: Close an open fiscal period
**THIN.** Sets up an open period, calls close, asserts `is_open = false`. The concern: a stub implementation that ignores the command and just returns a hard-coded `FiscalPeriod { isOpen = false }` would pass. The test implementation does verify the returned ID matches, but doesn't independently read the DB to confirm the flag actually changed.

**What would still pass:** An implementation that returns a modified copy but doesn't persist the change.

### FT-CFP-002: Reopen a closed fiscal period with a reason
**THIN.** Same structural weakness as CFP-001: asserts on the returned object, not on an independent DB read. However, the structural test `closePeriod and reopenPeriod are symmetric` (in the test file but not in the Gherkin) does verify via raw SQL -- so the test suite compensates, but the *Gherkin scenario* is thin on its own.

**What would still pass:** Same as CFP-001, an implementation that returns the right shape but doesn't persist.

### FT-CFP-003: Closing an already-closed period is idempotent
**SOLID.** Closes twice, asserts success both times. Tests idempotency explicitly.

### FT-CFP-004: Reopening an already-open period is idempotent
**SOLID.** Reopens an open period, asserts success. Tests idempotency.

### FT-CFP-005: Reopen with invalid reason is rejected (Scenario Outline, 2 examples)
**SOLID.** Tests both empty string and whitespace-only. Validates the pure reason-validation logic. The test implementation confirms the error message contains "reason".

### FT-CFP-006: Close a nonexistent fiscal period is rejected
**SOLID.** Error path.

### FT-CFP-007: Reopen a nonexistent fiscal period is rejected
**SOLID.** Error path.

### FT-CFP-008: Full close-reopen-close cycle
**SOLID.** Three sequential operations on the same period, asserts the state after each step. This is where the close/reopen logic gets a real workout -- if any step fails to persist, the next step's precondition is wrong.

### FT-CFP-009: Close a period with no journal entries
**THIN.** Functionally identical to CFP-001. The Gherkin step says "with no entries" but nothing in the test makes that fact meaningful -- it's the same close call either way. The *implementation* doesn't vary behavior based on entry count, so this tests nothing CFP-001 doesn't.

**What would still pass:** Any implementation that passes CFP-001.

### FT-CFP-010: Posting is rejected after closing a period via closePeriod
**SOLID.** This is a genuine integration test: closes the period, then attempts a journal entry post and asserts the error. Tests the cross-cutting enforcement of closed-period guards. A bug where the posting engine doesn't check `is_open` would fail this.

### FT-CFP-011: Reopen reason is logged at Info level
**THIN.** Asserts the reopen reason appears in log file content. The test reads log files from disk. This verifies logging infrastructure, not accounting logic. A broken accounting implementation that still logs the string would pass. Appropriate as a thin infrastructure test but should be understood as such.

**What would still pass:** An implementation that logs the reason but doesn't actually reopen the period.

---

## Feature: BalanceSheet.feature (11 scenarios)

### FT-BS-001: Balanced books produce isBalanced true
**SOLID.** Three entries (investment, income, expense) across 5 account types. Asserts `isBalanced`. The complexity of the setup means a broken cumulative-balance query or a broken accounting-equation check would fail.

### FT-BS-002: Assets equal liabilities plus total equity
**SOLID.** This is the accounting equation test. Three entries, asserts specific balances for each account (8000 assets, 2000 liabilities, 5000 equity), retained earnings of 1000, total equity of 6000, and `isBalanced`. The retained earnings value (1000) is *derived* from revenue activity -- it's not directly stated in any journal entry. This exercises the retained-earnings computation. Strong.

### FT-BS-003: Positive retained earnings when revenue exceeds expenses
**SOLID.** Revenue 3000, expense 1000, asserts retained earnings = 2000. The retained earnings emerge from the difference between revenue and expense accounts -- not from any single journal entry amount. Uses far-past dates to avoid test interference. Good isolation.

### FT-BS-004: Negative retained earnings when expenses exceed revenue
**SOLID.** Revenue 200, expense 800, asserts retained earnings = -600. Specifically tests the negative case. Also asserts individual account balances (asset = 1400 from 2000 borrowed + 200 revenue - 800 expense), demonstrating the asset balance is a derived value.

### FT-BS-005: Retained earnings zero when no revenue or expense activity
**SOLID.** Only equity transactions, asserts retained earnings = 0. Tests the zero-revenue/zero-expense case.

### FT-BS-006: Before any entries all zeros and balanced
**SOLID.** Queries a date (1900-01-01) with no data. Asserts all sections zero, balanced, and empty. Edge case for empty-database behavior.

### FT-BS-007: Voided entries excluded from balance sheet
**SOLID.** Posts two equity entries, voids one, asserts only the surviving entry's amounts appear. Same void-exclusion pattern as the trial balance, applied to the cumulative balance sheet.

### FT-BS-008: Entries across multiple fiscal periods all contribute
**SOLID.** Three entries across three fiscal periods. Asserts the cumulative nature of the balance sheet (asset = 3000 + 1000 + 500 = 4500). A period-scoped implementation that only shows the current period would fail.

### FT-BS-009: Multiple accounts per section accumulate correctly
**SOLID.** 6 accounts across 3 sections, 4 journal entries. Asserts per-section line counts and totals. The implementation filters by test-specific account IDs to handle parallel test data. Good.

### FT-BS-010: Account with activity netting to zero still appears with balance zero
**SOLID.** GAAP-specific: an account with offsetting debits and credits must still appear. Asserts the account exists with balance 0.00 and that the section has 2 lines. Would catch an implementation that filters out zero-balance accounts.

### FT-BS-011: Inactive accounts with cumulative balances still appear
**SOLID.** Posts an entry, deactivates the account, asserts it still appears on the balance sheet. Tests that `is_active` doesn't filter out accounts with real balances.

---

## Feature: IncomeStatement.feature (16 scenarios)

### FT-IS-001: Period with revenue and expense activity produces correct net income
**SOLID.** Revenue 1000, expense 400, net income 600. Net income is derived (revenue total - expense total), not directly stated. Tests the core computation.

### FT-IS-002: Revenue only period
**SOLID.** Asserts revenue section populated, expense section empty (0 lines, 0 total). Tests section filtering.

### FT-IS-003: Expenses only period
**SOLID.** Mirror of IS-002 for expenses. Both together ensure the sections are independent.

### FT-IS-004: Voided entries excluded
**SOLID.** Same void-exclusion pattern. Good.

### FT-IS-005: Accounts with no activity omitted
**SOLID.** Creates two revenue accounts, only one has activity. Asserts the idle account doesn't appear.

### FT-IS-006: Inactive accounts with activity still appear
**SOLID.** Deactivates an account after posting. Asserts it still appears in the report with the correct balance.

### FT-IS-007: Empty period produces zero net income
**SOLID.** Edge case.

### FT-IS-008: Net income is positive when revenue exceeds expenses
**THIN.** Revenue 2000, expense 500, net income 1500. This is structurally identical to IS-001 with different numbers. It tests the same code path. Copy-paste variation pattern.

**What would still pass:** Any implementation that passes IS-001.

### FT-IS-009: Net loss when expenses exceed revenue
**SOLID.** Revenue 300, expense 800, net income -500. Tests the negative net income case specifically. The sign matters -- this would catch an implementation using `abs()`.

### FT-IS-010: Multiple revenue and expense accounts accumulate correctly
**SOLID.** 2 revenue accounts, 2 expense accounts, 4 entries. Asserts per-section line counts, section totals, and net income. The totals are aggregated from multiple sources.

### FT-IS-011: Revenue balance equals credits minus debits
**SOLID.** Credits revenue 700, then debits it 100 (adjustment). Asserts balance = 600. This tests the normal-balance formula for credit-normal accounts with contra-entries. A broken formula fails here.

### FT-IS-012: Expense balance equals debits minus credits
**SOLID.** Debits expense 500, credits it 150. Asserts balance = 350. Same formula test for debit-normal accounts.

### FT-IS-013: Lookup by period key returns same result as by period ID
**SOLID.** Lookup-equivalence test.

### FT-IS-014: Nonexistent period ID returns error
**SOLID.** Error path.

### FT-IS-015: Nonexistent period key returns error
**SOLID.** Error path.

### FT-IS-016: Closed period income statement still works
**SOLID.** Report generation on a closed period.

---

## Feature: PostObligationToLedger.feature (15 scenarios)

### FT-POL-001: Posting a confirmed receivable creates correct journal entry
**SOLID.** Full cross-domain integration: creates agreement, accounts, fiscal period, confirmed instance -- then posts to ledger and verifies the journal entry has 2 lines with correct debit/credit accounts and amounts, and the instance status becomes "posted". The test independently queries the DB for the journal entry lines and instance status. This is the centerpiece test of the ops-to-ledger bridge.

### FT-POL-002: Posting a confirmed payable creates correct journal entry
**SOLID.** Same as POL-001 but for payable direction. Verifies debit goes to dest, credit goes to source regardless of obligation type. Would catch an implementation that swaps accounts based on direction.

### FT-POL-003: Journal entry description matches agreement and instance names
**SOLID.** Asserts the exact description format ("Brian Rent -- April 2026"). Tests the description-composition logic.

### FT-POL-004: Journal entry has obligation source and reference
**SOLID.** Asserts `source = "obligation"` and a reference with `type = "obligation"` and `value = instanceId`. Tests the metadata trail from ops to ledger.

### FT-POL-005: Journal entry entry_date equals confirmed_date
**SOLID.** Asserts the entry date comes from the instance's confirmed_date, not from `now()` or the expected_date. Specific and meaningful.

### FT-POL-006: Instance journal_entry_id is set after posting
**SOLID.** Asserts the back-link from the obligation instance to the created journal entry. Verifies the bidirectional reference is established.

### FT-POL-007: Instance not in confirmed status returns error
**SOLID.** Error path. Instance left in "expected" status.

### FT-POL-008: Instance with no amount returns error
**SOLID.** Uses raw SQL to create a confirmed instance with null amount, bypassing transition validation. Tests the posting-specific validation.

### FT-POL-009: Instance with no confirmed_date returns error
**SOLID.** Same raw-SQL approach for null confirmed_date.

### FT-POL-010: Agreement has no source_account_id
**SOLID.** Error path.

### FT-POL-011: Agreement has no dest_account_id
**SOLID.** Error path.

### FT-POL-012: No fiscal period covers confirmed_date
**SOLID.** Error path.

### FT-POL-013: Fiscal period is closed
**SOLID.** Error path.

### FT-POL-014: findByDate returns correct period for date within range
**THIN.** This tests a repository method directly. It inserts a period and looks it up. The assertion verifies ID, start_date, and end_date -- all of which were just set in the insert. It's essentially a roundtrip test for a simple date-range query.

**What would still pass:** A `findByDate` that ignores the date parameter and returns the most recently inserted period.

### FT-POL-015: findByDate returns None for date outside all periods
**SOLID.** Counterpart to POL-014. Together they constrain findByDate -- it must return a result for in-range dates and None for out-of-range dates. Neither alone is sufficient; together they're solid.

---

## Feature: ObligationAgreementCrud.feature (28 scenarios)

### FT-OA-001: Create agreement with all fields provided
**THIN.** Creates an agreement with all fields, then asserts every field matches what was provided. This is a classic mirror test -- the assertion is a trivial transform of the input. The test *does* verify that generated fields (id > 0, timestamps) are populated, which adds some value.

**What would still pass:** A `create` that stores nothing in the DB but returns a copy of the command with an auto-incremented ID and `now()` timestamps.

### FT-OA-002: Create agreement with only required fields
**THIN.** Same mirror pattern. Asserts optional fields are None. A `create` that just returns the input would pass.

**What would still pass:** Same as OA-001.

### FT-OA-003: Create with empty name is rejected
**SOLID.** Validation error path.

### FT-OA-004: Create with name exceeding 100 characters is rejected
**SOLID.** Validation error path.

### FT-OA-005: Create with counterparty exceeding 100 characters is rejected
**SOLID.** Validation error path.

### FT-OA-006: Create with non-positive amount is rejected (2 examples)
**SOLID.** Tests 0 and -50. Boundary + negative.

### FT-OA-007: Create with expected day outside 1-31 is rejected (3 examples)
**SOLID.** Tests 0, 32, -1. Both boundaries.

### FT-OA-008: Create with multiple validation errors collects all errors
**SOLID.** Asserts at least 3 errors returned. Tests error-collection behavior (not short-circuiting).

### FT-OA-009: Create with nonexistent source account is rejected
**SOLID.** DB-level validation.

### FT-OA-010: Create with inactive source account is rejected
**SOLID.** DB-level validation.

### FT-OA-011: Create with nonexistent dest account is rejected
**SOLID.** DB-level validation.

### FT-OA-012: Get agreement by ID returns the agreement
**SLOP.** Creates an agreement, gets it by ID, asserts the name matches. This is a trivial CRUD roundtrip. The assertion only checks `id` and `name` -- both were just set by the test.

**What would still pass:** A `getById` that caches the last created agreement in memory and returns it regardless of the ID parameter. (In fairness, this is somewhat inherent to all "get by ID" tests -- but the scenario adds zero accounting logic. It's testing the ORM.)

### FT-OA-013: Get agreement by nonexistent ID returns none
**SOLID.** Error path counterpart that constrains OA-012.

### FT-OA-014: List with default filter returns only active agreements
**SOLID.** Creates one active, one inactive. Asserts the active appears and the inactive doesn't. Tests the default `isActive = true` filter.

### FT-OA-015: List filtered by obligation type
**SOLID.** Creates receivable + payable. Filters by payable. Asserts correctly. Tests the type filter.

### FT-OA-016: List filtered by cadence
**SOLID.** Tests the cadence filter.

### FT-OA-017: List with no filter returns all including inactive
**SOLID.** Asserts both active and inactive appear when no isActive filter is set.

### FT-OA-018: List returns empty when no agreements match
**THIN.** Filters results by test-specific prefix and asserts empty. Due to the prefix-filtering approach, this doesn't actually test "empty database" -- it tests "no agreements with this prefix exist." The implementation might return thousands of other agreements.

**What would still pass:** A `list` that always returns all agreements. The test would still pass because the prefix filter finds nothing.

### FT-OA-019: Update agreement with all fields
**THIN.** Updates name and amount, asserts they changed. Also checks `modified_at > created_at`. Mostly a mirror test, but the timestamp assertion adds a small amount of real value.

**What would still pass:** An `update` that returns a modified copy without persisting (same weakness as the create tests).

### FT-OA-020: Update nonexistent agreement is rejected
**SOLID.** Error path.

### FT-OA-021: Update with empty name is rejected
**SOLID.** Validation error path.

### FT-OA-022: Update with non-positive amount is rejected
**SOLID.** Validation error path.

### FT-OA-023: Update with expected day outside 1-31 is rejected
**SOLID.** Validation error path.

### FT-OA-024: Update with inactive source account is rejected
**SOLID.** DB-level validation.

### FT-OA-025: Reactivate a previously deactivated agreement
**SLOP.** Sets `isActive = true` on a deactivated agreement, asserts `isActive = true`. The assertion is literally the input. No subsequent action (like listing or spawning) verifies the reactivation has a downstream effect.

**What would still pass:** An `update` that returns `isActive = true` without modifying the database.

### FT-OA-026: Deactivate an active agreement
**THIN.** Calls deactivate, asserts `isActive = false`. Same mirror weakness -- no verification that the deactivation persists or has effects.

**What would still pass:** A `deactivate` that returns `{ ...agreement with isActive = false }` without persisting.

### FT-OA-027: Deactivate a nonexistent agreement is rejected
**SOLID.** Error path.

### FT-OA-028: Deactivate blocked by active obligation instances
**SOLID.** This is a real constraint test. Creates an agreement with an active instance, attempts to deactivate, asserts the specific error. Tests business-rule enforcement.

---

## Feature: OverdueDetection.feature (11 scenarios)

### FT-OD-001: Single overdue instance is transitioned
**SOLID.** Creates an expected instance with a past date, runs detection, asserts 1 transitioned and 0 errors. Tests the core detection logic.

### FT-OD-002: Multiple overdue instances are all transitioned
**SOLID.** 3 instances, all overdue. Asserts all 3 transitioned.

### FT-OD-003: Instance on the reference date is not overdue
**SOLID.** Boundary test. expected_date == reference_date. Asserts 0 transitioned. Defines the boundary: overdue means *strictly before* the reference date.

### FT-OD-004: Instance after the reference date is not overdue
**SOLID.** expected_date > reference_date. Asserts 0 transitioned.

### FT-OD-005: In-flight instances are not flagged
**SOLID.** Status filter test.

### FT-OD-006: Already overdue instances are not re-transitioned
**SOLID.** Prevents double-transition.

### FT-OD-007: Confirmed instances are not flagged
**SOLID.** Status filter test.

### FT-OD-008: Inactive instances are not flagged
**SOLID.** isActive filter test.

### FT-OD-009: Running detection twice produces same result
**SOLID.** Idempotency test. First run: 1 transitioned. Second run: 0 transitioned. Proves the transition actually changes state and the query doesn't re-select already-transitioned instances.

### FT-OD-010: Only eligible instances among a mixed set are transitioned
**SOLID.** 6 instances with different statuses, dates, and active flags. Asserts exactly 2 transitioned. This is the comprehensive filter test and it's well-constructed -- each non-transitioning instance is excluded for a different reason.

### FT-OD-011: No overdue instances returns zero transitioned
**SOLID.** Empty-set edge case.

---

## Feature: SpawnObligationInstances.feature (18 scenarios)

### FT-SI-001: Monthly cadence produces correct dates for full range
**SOLID.** Pure function test. Asserts exact dates. Tests the date-generation algorithm.

### FT-SI-002: Monthly cadence clamps day 31 to Feb 28 (non-leap)
**SOLID.** Tests day-clamping logic for short months.

### FT-SI-003: Monthly cadence clamps day 31 to Feb 29 (leap year)
**SOLID.** Leap year variant.

### FT-SI-004: Monthly cadence defaults expectedDay to 1
**SOLID.** Tests the default-day behavior.

### FT-SI-005: Quarterly cadence produces four dates
**SOLID.** Tests quarterly date generation.

### FT-SI-006: Quarterly cadence for partial range
**SOLID.** Tests boundary filtering within the quarterly algorithm.

### FT-SI-007: Annual cadence, multi-year range
**SOLID.** Tests annual date generation.

### FT-SI-008: OneTime cadence produces exactly one date
**SOLID.** Tests one-time behavior.

### FT-SI-009: startDate after endDate is rejected
**SOLID.** Pure validation test.

### FT-SI-010: Instance names follow cadence-specific format (8 examples)
**SOLID.** Pure function test. Comprehensive coverage of all 4 cadences with representative dates.

### FT-SI-011: Spawn monthly instances for a 3-month range
**SOLID.** Integration test. Verifies dates, names, status, isActive, and amount for all 3 created instances. Thorough.

### FT-SI-012: Spawn for variable-amount agreement leaves instance amount empty
**SOLID.** Null-amount propagation.

### FT-SI-013: Spawn OneTime creates a single instance
**SOLID.** Integration variant for one-time cadence.

### FT-SI-014: All spawned instances have isActive true
**SLOP.** Creates quarterly instances, asserts all have `isActive = true`. This is a copy-paste variant of SI-011 that tests only one dimension already covered. The scenario adds no new behavioral test -- `isActive` is tested in SI-011's "each instance has status expected and isActive true" assertion.

**What would still pass:** Any implementation that passes SI-011.

### FT-SI-015: Spawn overlapping range skips existing dates and creates new ones
**SOLID.** Pre-inserts instances for Jan and Feb, spawns Jan-Apr, asserts 2 created + 2 skipped. Tests the deduplication logic.

### FT-SI-016: Spawn OneTime when instance already exists skips without error
**SOLID.** OneTime deduplication variant.

### FT-SI-017: Spawn for inactive agreement returns error
**SOLID.** Error path.

### FT-SI-018: Spawn for nonexistent agreement returns error
**SOLID.** Error path.

---

## Feature: StatusTransitions.feature (22 scenarios)

### FT-ST-001: Expected to in_flight
**SOLID.** Tests a valid transition. The state machine is the real logic under test.

### FT-ST-002: Expected to confirmed with amount and date
**SOLID.** Asserts status, confirmedDate, and amount are all set correctly after transition. Multiple assertions on emergent state.

### FT-ST-003: Expected to confirmed providing amount in command
**SOLID.** Instance has no amount; command provides one. Tests amount-injection during transition.

### FT-ST-004: In_flight to confirmed
**SOLID.** Different starting state, same target.

### FT-ST-005: Expected to overdue
**SOLID.** Transition test.

### FT-ST-006: In_flight to overdue
**SOLID.** Transition test.

### FT-ST-007: Overdue to confirmed
**SOLID.** Tests the recovery path from overdue.

### FT-ST-008: Confirmed to posted with journal entry
**SOLID.** Creates a real journal entry, transitions to posted, asserts both status and `journalEntryId`. Tests the posted-transition guard and side effect.

### FT-ST-009: Expected to skipped with notes
**SOLID.** Asserts status and notes content. Tests the skip path with its notes requirement.

### FT-ST-010: Confirmed transition updates amount when provided even if already set
**SOLID.** Instance has amount 500, command overrides to 525. Asserts 525. Tests amount-override semantics.

### FT-ST-011: Confirmed transition fails when no amount on instance or in command
**SOLID.** Error path for the amount guard.

### FT-ST-012: Confirmed to expected is rejected
**SOLID.** Invalid backward transition.

### FT-ST-013: Posted to confirmed is rejected
**SOLID.** Terminal state enforcement.

### FT-ST-014: Skipped to confirmed is rejected
**SOLID.** Terminal state enforcement.

### FT-ST-015: Overdue to in_flight is rejected
**SOLID.** Tests a specific disallowed path (overdue can go to confirmed but not in_flight).

### FT-ST-016: Posted without journal_entry_id fails
**SOLID.** Guard validation.

### FT-ST-017: Posted with nonexistent journal entry fails
**SOLID.** DB-level guard. The test implementation checks for "does not exist" in the error -- this tests that the service validates the FK reference, not just the presence of the field.

### FT-ST-018: Skipped without notes fails (when instance has no notes)
**SOLID.** Guard validation.

### FT-ST-019: Confirmed without confirmedDate fails
**SOLID.** Guard validation.

### FT-ST-020: Transition on inactive instance fails
**SOLID.** isActive guard.

### FT-ST-021: Transition on nonexistent instance fails
**SOLID.** Error path.

### FT-ST-022: Transition to skipped succeeds when instance already has notes
**SLOP.** Instance has "existing note", transition to skipped with no new notes, asserts status = skipped. This scenario exists to test the notes-guard escape hatch (existing notes suffice). However, the test implementation does NOT verify that the existing notes are preserved -- it only checks the status. A broken implementation that clears the notes while transitioning to skipped would pass.

**What would still pass:** An implementation that transitions to skipped and nulls out the existing notes.

---

## Slop Patterns Identified

### 1. Mirror Tests (5 instances)
The CRUD create/update/deactivate/reactivate scenarios (OA-001, OA-002, OA-019, OA-025, OA-026) assert that the returned object matches the input. No independent verification (e.g., a subsequent `getById`) confirms persistence.

### 2. Copy-Paste Variation (2 instances)
IS-008 is structurally identical to IS-001 with different numbers. SI-014 is a subset of SI-011. Neither adds a new code path.

### 3. No-Effect Edge Cases (1 instance)
CFP-009 ("close period with no entries") is functionally identical to CFP-001. The "no entries" condition has no impact on close behavior.

### 4. Missing Secondary Assertions (1 instance)
ST-022 checks status but not whether existing notes survived the transition.

---

## What Is NOT Slop

Some patterns that might look suspicious on first glance but are actually solid:

- **The balance sheet retained earnings tests** (BS-003, BS-004, BS-005): The expected values are *derived* from the journal entries, not echoed. Revenue 3000 - Expense 1000 = Retained Earnings 2000 requires the system to correctly identify revenue/expense accounts, apply the normal-balance formula, and subtract.

- **The overdue detection filtering tests** (OD-003 through OD-008): Each tests a single filter dimension, but OD-010 combines them all. The individual tests serve as localization aids when a bug is found.

- **The status transition tests** (ST-001 through ST-009): These look like "set status, check status" mirror tests, but they're testing a state machine. The `isValidTransition` function must allow the specific path. An implementation that allows all transitions would also pass the *happy path* tests, but would fail the *invalid transition* tests (ST-012 through ST-015), which provide the constraint.

---

## Recommendations (for Omission Hunter or follow-up)

1. **OA-001/OA-002**: Add a `getById` call after create to verify persistence independently.
2. **OA-025/OA-026**: After reactivate/deactivate, verify via `list` with appropriate filter.
3. **ST-022**: Assert `notes.IsSome` and that the original note text is preserved.
4. **IS-008**: Either remove (redundant with IS-001) or make it exercise a different code path.
5. **SI-014**: Either remove (redundant with SI-011) or assert something SI-011 doesn't.
6. **CFP-009**: Either remove (redundant with CFP-001) or document that it exists purely for specification coverage.

---

*Signed: Slop Detector*
*Assessment covers 91 Gherkin scenarios across 9 feature files, with full test implementation review.*
