# Slop Detector Report -- LeoBloom

**Date:** 2026-04-07
**Scope:** All 43 feature files under `/Specs/`
**Method:** For each scenario, ask: "What broken implementation would still pass this test?"

---

## Executive Summary

The spec suite is substantially sound. Out of roughly 400+ scenarios (including Scenario Outline expansions), I found **6 slop**, **28 thin**, and the rest solid. The codebase shows clear evidence of human oversight -- removed scenarios are annotated with REM-* codes, the balance sheet tests use far-past dates to avoid cross-test pollution, and the structural constraint suite is systematic without being mechanical.

The slop concentrations are in two areas: (1) logging infrastructure tests that verify plumbing rather than behavior, and (2) CLI tests that check exit codes and "contains some string" without verifying the actual data content. Neither is surprising -- these are classic AI generation artifacts. The domain-critical paths (double-entry posting, balance calculations, obligation state machine, balance projection) are well-tested.

---

## Findings by Feature File

### Behavioral/PostJournalEntry.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-PJE-001 | **Solid** | Posts entry, asserts id > 0, timestamps populated, line count = 2. Tests the full write path. |
| FT-PJE-002 | **Solid** | 3-line compound entry (two debits, one credit). Asserts line count. |
| FT-PJE-003 | **Solid** | Posts with references, asserts reference count = 2. |
| FT-PJE-004 | **Solid** | Null source accepted and verified via `source.IsNone`. |
| FT-PJE-005 | **Thin** | Asserts all lines have `memo.IsSome` but does not verify memo *values* match "Cash received"/"Rent income". A broken implementation that always sets memo to "X" would pass. |
| FT-PJE-006 | **Solid** | Unbalanced entry rejected; error message checked for "do not equal". |
| FT-PJE-007 | **Solid** | Zero amount rejected. |
| FT-PJE-008 | **Solid** | Negative amount rejected. |
| FT-PJE-009 | **Solid** | Single-line rejected. |
| FT-PJE-010 | **Solid** | Empty description rejected. |
| FT-PJE-011 | **Thin** | Test asserts `Assert.True(true)` on the Error branch. Doesn't verify the error *message*. Any function returning Error for any reason would pass. The spec says "entry_type parse result is Error" which is correct but the implementation doesn't verify the error content. |
| FT-PJE-012 | **Solid** | Empty source string rejected with "Source" in error. |
| FT-PJE-013 | **Solid** | Closed fiscal period rejected. |
| FT-PJE-014 | **Solid** | Entry date outside period range rejected. |
| FT-PJE-015 | **Solid** | Inactive account rejected. |
| FT-PJE-016 | **Solid** | Nonexistent fiscal period rejected. |
| FT-PJE-017 | **Solid** | Empty reference type rejected. |
| FT-PJE-018 | **Solid** | Empty reference value rejected. |
| FT-PJE-019 | **Solid** | Atomicity check -- queries DB to verify zero rows persisted after failed validation. This is excellent. |
| FT-PJE-020 | **Solid** | Duplicate references across entries allowed. |
| FT-PJE-021 | **Thin** | Future date entry succeeds. Only asserts id > 0. Identical assertion to FT-PJE-001 with a different date. Tests that the date check doesn't false-reject, but doesn't verify the entry date was actually persisted as 2026-06-15. |

### Behavioral/AccountBalance.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-AB-001 | **Solid** | Verifies balance is 1000.00 for a normal-debit account. Checks account code in result. |
| FT-AB-002 | **Solid** | Normal-credit account balance verified. |
| FT-AB-003 | **Solid** | Accumulation across two entries: 500 + 300 = 800. Tests real arithmetic emergence. |
| FT-AB-004 | **Solid** | Mixed debits and credits: 1000 debit - 400 credit = 600 net. Real netting logic exercised. |
| FT-AB-005 | **Solid** | Voided entry excluded. Sets up 500 + 200, voids 200, expects 500. |
| FT-AB-006 | **Solid** | As-of date filtering: entry after cutoff excluded. |
| FT-AB-007 | **Solid** | Zero balance for account with no entries. |
| FT-AB-008 | **Solid** | Inactive account balance still calculated. |
| FT-AB-009 | **Solid** | Nonexistent account ID returns error. |
| FT-AB-010 | **Solid** | Nonexistent account code returns error. |
| FT-AB-011 | **Solid** | Code lookup matches ID lookup -- good equivalence test. |

### Behavioral/VoidJournalEntry.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-VJE-001 | **Solid** | Void succeeds, checks voided_at set and void_reason matches. |
| FT-VJE-002 | **Solid** | Voided entry still exists in DB. Append-only design verified. |
| FT-VJE-003 | **Solid** | Lines and references intact after void. Checks counts and specific ref values. |
| FT-VJE-004 | **Solid** | Idempotent void -- second void keeps original voided_at/reason. Good design test. |
| FT-VJE-005 | **Solid** | Empty void reason rejected. |
| FT-VJE-006 | **Solid** | Whitespace-only void reason rejected. |
| FT-VJE-007 | **Solid** | Nonexistent entry ID rejected. |
| FT-VJE-008 | **Solid** | Void succeeds even in closed fiscal period. Important edge case for corrections. |

### Behavioral/BalanceSheet.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-BS-001 | **Solid** | Balanced books produce isBalanced true. Multi-entry setup. |
| FT-BS-002 | **Solid** | Full accounting equation verified with specific amounts. Asset 8000 = Liability 2000 + Equity 5000 + RE 1000. The test implementation uses `List.find` on specific account IDs to filter from cumulative report. Excellent. |
| FT-BS-003 | **Solid** | Positive retained earnings: 3000 rev - 1000 exp = 2000 RE. Uses 1950 dates to isolate from parallel tests. |
| FT-BS-004 | **Solid** | Negative retained earnings: 200 rev - 800 exp = -600 RE. Also verifies isBalanced. |
| FT-BS-005 | **Solid** | Zero retained earnings with equity-only activity. |
| FT-BS-006 | **Solid** | Empty balance sheet -- all zeros, all empty sections. Comprehensive zeroes check. |
| FT-BS-007 | **Solid** | Voided entries excluded. Posts two, voids one, asserts 5000 not 7000. |
| FT-BS-008 | **Solid** | Cross-period cumulation verified with specific account-level assertions. |
| FT-BS-009 | **Solid** | Multiple accounts per section. Filters by specific account IDs, verifies counts and totals. |
| FT-BS-010 | **Solid** | Zero-balance account with activity still appears. Good GAAP requirement. |
| FT-BS-011 | **Solid** | Inactive account with balance still appears. |

### Behavioral/TrialBalance.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-TB-001 | **Solid** | Balanced trial balance: debits 500, credits 500, 2 account lines. |
| FT-TB-002 | **Solid** | Grouping by account type with specific subtotals per group. Multi-entry, multi-account. |
| FT-TB-003 | **Solid** | Empty groups omitted. Checks exactly 2 groups, no liability/equity/expense. |
| FT-TB-004 | **Solid** | Voided entries excluded from trial balance. |
| FT-TB-005 | **Solid** | Empty period: zero totals, zero groups. |
| FT-TB-006 | **Solid** | Closed period trial balance still works. |
| FT-TB-007 | **Solid** | Multiple entries accumulate per account: 500 + 300 = 800 debit on account 1010. |
| FT-TB-008 | **Solid** | Net balance formula: debit account -> positive, credit account -> positive. Both 700.00. |
| FT-TB-009 | **Solid** | Lookup by key = lookup by ID. Equivalence verified. |
| FT-TB-010 | **Solid** | Nonexistent period ID returns error. |
| FT-TB-011 | **Solid** | Nonexistent period key returns error. |

### Behavioral/IncomeStatement.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-IS-001 | **Solid** | Revenue 1000, expense 400, net income 600. Three separate assertions. |
| FT-IS-002 | **Solid** | Revenue only: expenses section 0.00 with 0 lines. |
| FT-IS-003 | **Solid** | Expenses only: revenue section 0.00 with 0 lines. |
| FT-IS-004 | **Solid** | Voided entries excluded. |
| FT-IS-005 | **Solid** | Inactive account with no activity omitted from sections. Verifies 1 line, not 2. |
| FT-IS-006 | **Solid** | Inactive account with activity still appears. |
| FT-IS-007 | **Solid** | Empty period: all zeros. |
| FT-IS-009 | **Solid** | Net loss: -500. Revenue 300, expense 800. |
| FT-IS-010 | **Solid** | Multiple revenue and expense accounts. Verifies section line counts and computed totals. |
| FT-IS-011 | **Solid** | Revenue balance = credits - debits. 700 credit - 100 debit adjustment = 600. Real normal-balance formula. |
| FT-IS-012 | **Solid** | Expense balance = debits - credits. 500 debit - 150 credit adjustment = 350. |
| FT-IS-013 | **Solid** | Lookup equivalence by key vs ID. |
| FT-IS-014 | **Solid** | Nonexistent period ID error. |
| FT-IS-015 | **Solid** | Nonexistent period key error. |
| FT-IS-016 | **Solid** | Closed period income statement still works. |
| FT-IS-017 | **Solid** | Period scoping: March report excludes April activity. |

### Behavioral/SubtreePLReport.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-SPL-001 | **Solid** | Revenue 1000, expense 400, net 600 within subtree. |
| FT-SPL-002 | **Solid** | Revenue only subtree: expense section empty. |
| FT-SPL-003 | **Solid** | Expense only subtree: revenue section empty. |
| FT-SPL-004 | **Solid** | Standalone revenue account as root produces single-account P&L. |
| FT-SPL-005 | **Solid** | Non-revenue/expense root with no rev/exp children: empty report. |
| FT-SPL-006 | **Solid** | Voided entries excluded. |
| FT-SPL-007 | **Solid** | Accounts outside subtree excluded. 600 inside, 400 outside, only 600 shown. |
| FT-SPL-008 | **Solid** | Multi-level hierarchy: grandchild included. Tests tree traversal. |
| FT-SPL-009 | **Solid** | Lookup equivalence by key vs ID. |
| FT-SPL-010 | **Solid** | Nonexistent account code error. |
| FT-SPL-011 | **Solid** | Nonexistent period error. |
| FT-SPL-012 | **Solid** | Empty period: all zeros. |

### Behavioral/OpeningBalances.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-OB-001 | **Solid** | Asset + liability produce balanced entry. Verifies 3 lines and total debits = credits. |
| FT-OB-002 | **Solid** | Balancing line direction: asset opening -> equity credit of 5000. Computed, not echoed. |
| FT-OB-003 | **Solid** | Reversed direction: liability opening -> equity debit of 200000. |
| FT-OB-004 | **Solid** | Single account produces exactly 2-line entry. |
| FT-OB-005 | **Solid** | 3 accounts -> 4 lines. Verifies description and source metadata. |
| FT-OB-006 | **Solid** | Duplicate account rejected. |
| FT-OB-007 | **Solid** | Empty entries list rejected. |
| FT-OB-008 | **Solid** | Zero balance rejected. |
| FT-OB-009 | **Solid** | Balancing account in entries rejected. |
| FT-OB-010 | **Solid** | Nonexistent account error. |
| FT-OB-011 | **Solid** | Nonexistent balancing account error. |
| FT-OB-012 | **Solid** | Non-equity balancing account rejected. Domain-correct constraint. |
| FT-OB-013 | **Solid** | Default description "Opening balances". |
| FT-OB-014 | **Solid** | Custom description round-trips. |

### Behavioral/Transfers.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-TRF-001 | **Solid** | Initiate succeeds, status = "initiated". |
| FT-TRF-002 | **Solid** | Same-account transfer rejected. |
| FT-TRF-003 | **Solid** | Non-asset account rejected. |
| FT-TRF-004 | **Solid** | Inactive account rejected. |
| FT-TRF-005 | **Solid** | Zero amount rejected. |
| FT-TRF-006 | **Solid** | Negative amount rejected. |
| FT-TRF-007 | **Solid** | Confirm creates JE with correct debit/credit accounts and amounts. Verifies 2 lines with directional correctness. |
| FT-TRF-008 | **Solid** | Confirmed_date and journal_entry_id set on transfer record. |
| FT-TRF-009 | **Solid** | JE has transfer source and reference. |
| FT-TRF-010 | **Solid** | JE entry_date = confirmed_date. |
| FT-TRF-011 | **Solid** | Custom description round-trips to JE. |
| FT-TRF-012 | **Solid** | Auto-generated description starts with "Transfer". |
| FT-TRF-013 | **Solid** | Non-initiated transfer cannot be confirmed. |
| FT-TRF-014 | **Solid** | No fiscal period for date returns error. |
| FT-TRF-015 | **Solid** | Idempotency guard: retry skips duplicate JE. |
| FT-TRF-016 | **Solid** | Voided prior JE doesn't trigger idempotency guard. Subtle and important. |

### Behavioral/CloseReopenFiscalPeriod.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-CFP-001 | **Solid** | Close sets is_open = false. |
| FT-CFP-002 | **Solid** | Reopen with reason sets is_open = true. |
| FT-CFP-003 | **Solid** | Idempotent close. |
| FT-CFP-004 | **Solid** | Idempotent reopen. |
| FT-CFP-005 | **Solid** | Empty/whitespace reason rejected. Scenario Outline covers both. |
| FT-CFP-006 | **Solid** | Nonexistent period close rejected. |
| FT-CFP-007 | **Solid** | Nonexistent period reopen rejected. |
| FT-CFP-008 | **Solid** | Full lifecycle: close -> reopen -> close. All three succeed. |
| FT-CFP-010 | **Solid** | Integration: posting rejected after close. Crosses two domains. |
| FT-CFP-011 | **Thin** | Reopen reason logged. Checks log output contains the reason string. A log-only assertion -- if logging breaks, no user-visible behavior changes. |
| FT-CFP-012 | **Solid** | Closing doesn't modify balances. Compares trial balance before and after. Excellent side-effect test. |

### Behavioral/PostObligationToLedger.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-POL-001 | **Solid** | Receivable posting: correct JE lines (debit dest, credit source), instance -> posted. |
| FT-POL-002 | **Solid** | Payable posting: same structure, different agreement type. |
| FT-POL-003 | **Solid** | JE description = "Brian Rent -- April 2026". Metadata composition verified. |
| FT-POL-004 | **Solid** | JE source = "obligation" and reference matches instance ID. |
| FT-POL-005 | **Solid** | JE entry_date = confirmed_date. |
| FT-POL-006 | **Solid** | Instance journal_entry_id set to created JE ID. |
| FT-POL-007 | **Solid** | Non-confirmed instance rejected. |
| FT-POL-008 | **Solid** | No-amount instance rejected. |
| FT-POL-009 | **Solid** | No confirmed_date rejected. |
| FT-POL-010 | **Solid** | No source account rejected. |
| FT-POL-011 | **Solid** | No dest account rejected. |
| FT-POL-012 | **Solid** | No fiscal period for date rejected. |
| FT-POL-013 | **Solid** | Closed fiscal period rejected. |
| FT-POL-014 | **Solid** | findByDate returns correct period. |
| FT-POL-015 | **Solid** | findByDate returns None for out-of-range date. |
| FT-POL-016 | **Solid** | Double-posting prevention: second post rejected, no duplicate JE, journal_entry_id unchanged. Three assertions. |
| FT-POL-017 | **Solid** | Failed post leaves instance in confirmed status, no JE created. Atomicity verified. |
| FT-POL-018 | **Solid** | Idempotency guard: pre-existing JE reused, no new JE created. |
| FT-POL-019 | **Solid** | Voided prior JE doesn't trigger idempotency guard. Creates new JE. |

### Behavioral/LoggingInfrastructure.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-LI-003 | **Thin** | Checks a log file exists. Verifies infrastructure plumbing, not behavior. Would pass if the file existed from a previous test run. |
| FT-LI-004 | **Thin** | Log filename pattern check. Same issue -- infrastructure, not logic. |
| FT-LI-007 | **Thin** | Posting emits Info log. Checks `logContent.Contains("Posting journal entry")`. Would pass if *any* prior test in the run posted an entry. The log is cumulative across the whole run. |
| FT-LI-008 | **Thin** | Voiding emits Info log. Same cumulative-log issue. |
| FT-LI-009 | **Thin** | Balance query by ID emits log. Same pattern. |
| FT-LI-010 | **Thin** | Balance query by code emits log. Same pattern. |
| FT-LI-011 | **Slop** | "DataSource initialization emits Info-level log entry" -- the test reads the *source file* and checks it contains `Log.info` and `DataSource initialized` as strings. This is a static string search of source code, not a test of runtime behavior. An implementation that has the string in a comment but never executes it would pass. |
| FT-LI-012 | **Thin** | Validation failure emits Warning log. Cumulative log issue. |

### Behavioral/IncomeStatement.feature -- already covered above.

### Ops/OverdueDetection.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-OD-001 | **Solid** | Single overdue instance transitioned. Checks count and 0 errors. |
| FT-OD-002 | **Solid** | Multiple overdue instances all transitioned. |
| FT-OD-003 | **Solid** | Instance on reference date NOT overdue. Boundary condition. |
| FT-OD-004 | **Solid** | Instance after reference date NOT overdue. |
| FT-OD-005 | **Solid** | In-flight not flagged. |
| FT-OD-006 | **Solid** | Already-overdue not re-transitioned. |
| FT-OD-007 | **Solid** | Confirmed not flagged. |
| FT-OD-008 | **Solid** | Inactive not flagged. |
| FT-OD-009 | **Solid** | Idempotent: second run transitions 0. |
| FT-OD-010 | **Solid** | Mixed scenario: only 2 of 6 are eligible. Table-driven input with diverse states. Excellent test. |
| FT-OD-011 | **Solid** | No candidates: 0 transitioned, 0 errors. |

### Ops/SpawnObligationInstances.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-SI-001 | **Solid** | Monthly cadence: 6 dates generated, all verified. Pure date logic. |
| FT-SI-002 | **Solid** | Day 31 clamped to Feb 28 in non-leap year. |
| FT-SI-003 | **Solid** | Day 31 clamped to Feb 29 in leap year. |
| FT-SI-004 | **Solid** | Default expectedDay to 1. |
| FT-SI-005 | **Solid** | Quarterly cadence: 4 dates. |
| FT-SI-006 | **Solid** | Partial quarterly range. |
| FT-SI-007 | **Solid** | Annual cadence across multiple years. |
| FT-SI-008 | **Solid** | OneTime: exactly one date. |
| FT-SI-009 | **Solid** | Invalid date range rejected. |
| FT-SI-010 | **Solid** | Instance name generation verified per cadence. Scenario Outline with 8 examples. |
| FT-SI-011 | **Solid** | Spawn integration: 3 instances, correct dates, names, amounts, status. |
| FT-SI-012 | **Solid** | Variable-amount agreement: instance amount is empty. |
| FT-SI-013 | **Solid** | OneTime spawn: 1 instance with correct name and amount. |
| FT-SI-015 | **Solid** | Overlap handling: 2 created, 2 skipped. |
| FT-SI-016 | **Solid** | OneTime idempotent: 0 created, 1 skipped. |
| FT-SI-017 | **Solid** | Inactive agreement rejected. |
| FT-SI-018 | **Solid** | Nonexistent agreement rejected. |

### Ops/StatusTransitions.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-ST-001 through FT-ST-009 | **Solid** | Forward transitions all verified with post-transition state assertions. Amount, confirmedDate, journalEntryId, notes, isActive all checked where relevant. |
| FT-ST-010 | **Solid** | Amount override on confirmed transition: 500 -> 525. |
| FT-ST-011 | **Solid** | No amount on instance or command -> rejected. |
| FT-ST-012 through FT-ST-015 | **Solid** | Invalid backward transitions rejected. |
| FT-ST-016 | **Solid** | Posted without JE ID rejected. |
| FT-ST-017 | **Solid** | Posted with nonexistent JE rejected. |
| FT-ST-018 | **Solid** | Skipped without notes rejected. |
| FT-ST-019 | **Solid** | Confirmed without confirmedDate rejected. |
| FT-ST-020 | **Solid** | Inactive instance transition rejected. |
| FT-ST-021 | **Solid** | Nonexistent instance rejected. |
| FT-ST-022 | **Solid** | Skipped with pre-existing notes: no new notes required. |
| FT-ST-023 | **Solid** | Full invalid transition matrix: 18 examples covering every illegal edge. Exhaustive. |

### Ops/ObligationAgreementCrud.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-OA-001 | **Solid** | Create with all fields. Verified by subsequent getById. Round-trip test. |
| FT-OA-002 | **Solid** | Create with only required fields. Optional fields verified null. |
| FT-OA-003 through FT-OA-008 | **Solid** | Validation: empty name, long name, long counterparty, non-positive amount, invalid expectedDay, multiple errors collected. |
| FT-OA-009 through FT-OA-011 | **Solid** | DB validation: nonexistent/inactive source and dest accounts. |
| FT-OA-012 through FT-OA-018 | **Solid** | Get/list with filtering, active/inactive, empty result. |
| FT-OA-019 | **Solid** | Update: name and amount changed, modified_at > created_at, round-trip verified. |
| FT-OA-020 through FT-OA-024 | **Solid** | Update validation errors. |
| FT-OA-025 | **Solid** | Reactivation via update. |
| FT-OA-026 through FT-OA-028 | **Solid** | Deactivation: success, nonexistent, blocked by active instances. |
| FT-OA-029 | **Solid** | Source = dest rejected. |
| FT-OA-030 | **Solid** | Inactive dest account rejected. |

### Ops/BalanceProjection.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-BP-001 | **Solid** | Flat line: no obligations/transfers -> constant balance across all days. |
| FT-BP-002 | **Solid** | Receivable inflow: balance increases on expected_date. Verifies day before and day of. |
| FT-BP-003 | **Solid** | Payable outflow: balance decreases on expected_date. |
| FT-BP-004 | **Solid** | Transfer out decreases balance on settlement date. |
| FT-BP-005 | **Solid** | Transfer in increases balance on settlement date. |
| FT-BP-006 | **Solid** | No expected_settlement -> falls back to initiated_date. |
| FT-BP-007 | **Solid** | Combined formula: 5000 + 1000 - 400 - 200 + 100 = 5500. All four components exercised in one scenario. Verified against specific derived value. |
| FT-BP-008 | **Solid** | Series contains every day in range. |
| FT-BP-009 | **Solid** | Same-day multiple obligations: 3000 + 500 - 200 - 150 = 3150. Itemized breakdown with 3 items, net +150. |
| FT-BP-010 | **Solid** | Null-amount payable: surfaces as unknown outflow, not omitted, balance not reduced by guess. |
| FT-BP-011 | **Solid** | Null-amount receivable: surfaces as unknown inflow, balance not increased by guess. |
| FT-BP-012 | **Solid** | Past date rejected. |
| FT-BP-013 | **Solid** | Today-as-projection-date rejected. |
| FT-BP-014 | **Solid** | Nonexistent account rejected. |

### Ops/OrphanedPostingDetection.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-OPD-001 | **Solid** | Clean result: 0 orphans. |
| FT-OPD-002 | **Solid** | DanglingStatus for obligation and transfer. Scenario Outline with 2 examples. |
| FT-OPD-003 | **Solid** | MissingSource for obligation and transfer. |
| FT-OPD-004 | **Solid** | VoidedBackingEntry for obligation (posted) and transfer (confirmed). |
| FT-OPD-005 | **Solid** | Properly completed posting NOT flagged. Important negative test. |
| FT-OPD-006 | **Solid** | InvalidReference (non-numeric reference_value) detected. |
| FT-OPD-007 | **Solid** | JSON output mode returns valid JSON with correct content. |

### Ops/InvoicePersistence.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-INV-001 | **Solid** | Full record with all fields. Verifies tenant, totalAmount, generatedAt round-trip. |
| FT-INV-002 | **Solid** | Null optional fields verified. |
| FT-INV-003 | **Solid** | Closed fiscal period allowed for invoices (unlike JE posting). Domain-correct. |
| FT-INV-004 | **Solid** | Zero rent + non-zero utility allowed. |
| FT-INV-005 | **Solid** | Non-zero rent + zero utility allowed. |
| FT-INV-006 through FT-INV-009 | **Solid** | Validation: empty tenant, long tenant, negative amounts, decimal precision. |
| FT-INV-010 | **Solid** | Total != rent + utility rejected. Cross-field validation. |
| FT-INV-011 | **Solid** | Multiple errors collected. |
| FT-INV-012 | **Solid** | Nonexistent fiscal period rejected. |
| FT-INV-013 | **Solid** | Duplicate tenant + period rejected. |
| FT-INV-014 through FT-INV-020 | **Solid** | Show/list with filtering. |

### Ops/SeedRunner.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-SR-001 | **Solid** | 36 fiscal periods (2026-01 through 2028-12). Specific count verified. |
| FT-SR-002 | **Solid** | 69 accounts. Every parent_id references existing account. |
| FT-SR-003 | **Thin** | "Leaf-level detail accounts have non-null subtype" -- depends on definition of "leaf-level detail". Could be ambiguous in implementation. |
| FT-SR-004 | **Solid** | Idempotent: second run produces same row counts. |
| FT-SR-005 | **Solid** | SQL error stops execution. Verifies exit code and subsequent scripts not run. |
| FT-SR-006 | **Solid** | Nonexistent environment directory produces non-zero exit. |
| FT-SR-007 | **Solid** | Execution order verified: 010 before 020. |
| FT-SR-008 through FT-SR-011 | **Solid** | Portfolio reference data: specific row counts per dimension table. |

### Portfolio/InvestmentAccount.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-PF-001 | **Solid** | Create + findById round-trip. |
| FT-PF-002 | **Solid** | Empty name rejected. |
| FT-PF-003 | **Solid** | List returns both created accounts. |
| FT-PF-004 | **Solid** | Empty list when none exist. |

### Portfolio/Fund.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-PF-010 | **Solid** | Create fund, all dimension fields null, findBySymbol round-trip. |
| FT-PF-011 | **Solid** | Optional dimension fields persist. |
| FT-PF-012 | **Solid** | Empty symbol rejected. |
| FT-PF-013 | **Solid** | Empty name rejected. |
| FT-PF-014 | **Solid** | Nonexistent symbol returns none. |
| FT-PF-015 | **Solid** | List all funds returns all. |
| FT-PF-016 | **Solid** | List by dimension filter: 6 dimension types tested via Scenario Outline. |
| FT-PF-017 | **Solid** | Empty dimension filter returns empty list. |

### Portfolio/Position.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-PF-020 | **Solid** | Record position with all fields. Round-trip of price, quantity, current_value, cost_basis. |
| FT-PF-021 | **Solid** | Negative numeric fields rejected per Scenario Outline. |
| FT-PF-022 | **Solid** | Nonexistent fund symbol rejected. |
| FT-PF-023 | **Solid** | Duplicate position (account + symbol + date) rejected. |
| FT-PF-024 | **Solid** | List filtered by account. |
| FT-PF-025 | **Solid** | Empty list for account with no positions. |
| FT-PF-026 | **Solid** | Date range filter: only Feb-28 position returned when range is Feb. |
| FT-PF-027 | **Solid** | Latest per fund: only 2026-03-31 returned, not 2026-02-28. |
| FT-PF-028 | **Solid** | Latest across all accounts: 2 positions (one per account-symbol pair). |

### Ledger/AccountSubTypes.feature

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-AST-001 | **Solid** | 9 valid subtype/type combinations via Scenario Outline. |
| FT-AST-002 | **Solid** | 8 invalid combinations rejected. |
| FT-AST-003 | **Solid** | All 9 subtypes rejected for Equity. |
| FT-AST-004 | **Solid** | Null subtype valid for all 5 types. |
| FT-AST-005 | **Solid** | toDbString/fromDbString round-trip for all 9 subtypes. |
| FT-AST-006 | **Solid** | Unrecognized string rejected. |
| FT-AST-007 through FT-AST-009 | **Solid** | Persistence round-trip for subtypes and null subtype. |
| FT-AST-010 through FT-AST-012 | **Thin** | Domain validation tests that duplicate FT-AST-002 and FT-AST-003 with different specific inputs. Same code path, same behavior, just different example values. Not slop, but redundant. |
| FT-AST-013 | **Solid** | Update subtype Cash -> Investment succeeds. |
| FT-AST-014 | **Solid** | Update subtype to null succeeds. |
| FT-AST-015 | **Solid** | 14 seed accounts have expected subtypes. Data verification against known seed. |
| FT-AST-016 | **Solid** | 15 header accounts have null subtype. |
| FT-AST-017 | **Solid** | 3 equity leaf accounts have null subtype. |

### CLI Feature Files (8 files)

The CLI test files are the largest slop risk area. Let me assess the broad patterns:

**General pattern across all CLI specs:**
- Happy path: "run command -> stdout contains X -> exit code 0"
- Error path: "run command -> stderr contains an error message -> exit code 1 or 2"
- Missing args: Scenario Outlines covering each omitted required flag

**Verdict breakdown:**

| Category | Count | Verdict | Reasoning |
|----------|-------|---------|-----------|
| Happy paths with "stdout contains X" | ~25 | **Thin** | These verify the CLI *says something about* the expected content but don't check the output contains correct *computed values*. For example, FT-LCD-001 checks "stdout contains the posted entry details" -- in the test, this means the output is non-empty. A CLI that prints "done" for every command would pass many of these. However, these are explicitly labeled as CLI-layer tests, with service logic covered elsewhere. Calling them thin, not slop, because the CLI is thin by design. |
| "--json outputs valid JSON" | ~20 | **Solid** | These actually parse the stdout as JSON and fail if it's malformed. A broken serializer would fail. |
| Missing arg detection | ~30 | **Solid** | These verify specific error paths are handled. |
| Error surfacing | ~15 | **Solid** | These verify service errors bubble to stderr. |
| "--help" and exit code checks | ~15 | **Thin** | Purely structural. Help output existing doesn't prove the command works. |

**Specific CLI slop findings:**

| Tag | Verdict | Notes |
|-----|---------|-------|
| FT-RPT-110 | **Thin** | "stdout contains separate debit and credit columns" -- vague. What does "contains" mean in practice? |
| FT-RPT-120 | **Thin** | "stdout contains 'Assets'" -- a CLI that always prints "Assets Liabilities Equity Retained Earnings" as a header without any data would pass. |
| FT-TRC-031 | **Thin** | "stdout contains only transfers with status 'initiated'" -- without verifying the count or that non-matching items are excluded, this could pass with empty output or unfiltered output that happens to include initiated ones. |
| FT-OBL-111 | **Thin** | "stdout contains only instances due within 7 days" -- verifying "within 7 days" from the stdout output is ambiguous. |

### Structural Feature Files (6 files)

| Feature | Verdict | Notes |
|---------|---------|-------|
| LedgerStructuralConstraints (29 scenarios) | **Solid** | Each scenario inserts a row violating one specific DB constraint and asserts the correct violation type (NOT NULL, UNIQUE, FK). Pattern is systematic and correct. |
| OpsStructuralConstraints (18 scenarios) | **Solid** | Same pattern as ledger. |
| PortfolioStructuralConstraints (13 scenarios) | **Solid** | Same pattern. |
| DeleteRestrictionConstraints (13 scenarios) | **Solid** | ON DELETE RESTRICT verified for every FK. Each scenario: insert parent + child, delete parent, assert FK violation. |
| LogModuleStructure (2 scenarios) | **Slop** | FT-LMS-008 and FT-LMS-009 are `grep`-based source code scans. FT-LMS-008 checks that `eprintfn` doesn't appear in TestHelpers.fs. FT-LMS-009 checks no `printfn` or `eprintfn` in Src/ (excluding Migrations). These are code hygiene rules, not behavioral tests. A codebase that redirected all output through a different function (e.g., `Console.WriteLine`) would pass. |
| DataSourceEncapsulation | **Mixed** | FT-DSI-001 is **Thin** -- inspects module bindings structurally. FT-DSI-008 is **Solid** -- actually runs the migration tool and verifies exit code 0 and migrondi schema exists. |
| ConsolidatedHelpers (8 scenarios) | **Solid** | FT-CHL-001 through FT-CHL-004 verify the consolidated optParam helper round-trips null/non-null values through the actual posting engine. These are regression tests for a specific refactoring. FT-CHL-005/006 verify RepoPath resolution. FT-CHL-007/008 verify constraint assert helpers detect each violation type. |

---

## Slop Findings Summary

### SLOP (6 scenarios) -- False confidence; broken implementation would pass

1. **FT-LI-011** -- DataSource init log: reads source file, checks string exists. Static grep, not runtime behavior. A dead-code `Log.info "DataSource initialized"` in a comment would pass.

2. **FT-LMS-008** -- TestHelpers `eprintfn` check: source file grep. Any non-`eprintfn` console output method would pass.

3. **FT-LMS-009** -- No `printfn`/`eprintfn` in Src: source file grep. Same issue. `Console.Write`, `stderr.Write`, or any other output mechanism would pass.

4-6. **BalanceSheet structural tests** (unnamed, in BalanceSheetTests.fs but not in feature files): `BalanceSheetLine type has required fields`, `BalanceSheetSection type has required fields`, `BalanceSheetReport type has required fields`. These construct a record and assert the values they just set. Pure mirror tests. They test that F# records store values, which is language-guaranteed. A type with completely wrong field semantics would pass as long as it compiles. **Note:** These are in the test file but NOT in the feature file -- they appear to be QE-added structural tests, not Gherkin-mapped scenarios. Flagging them because they exist in the test suite and provide false confidence.

### THIN (28 scenarios) -- Tests something real but insufficiently

1. **FT-PJE-005** -- Memo presence verified but not content.
2. **FT-PJE-011** -- Error result verified but not error message.
3. **FT-PJE-021** -- Future date succeeds but persisted date not verified.
4. **FT-LI-003** -- Log file exists (could be from prior run).
5. **FT-LI-004** -- Log filename format (same).
6. **FT-LI-007** -- Post log entry (cumulative log, not isolated).
7. **FT-LI-008** -- Void log entry (same).
8. **FT-LI-009** -- Balance-by-ID log entry (same).
9. **FT-LI-010** -- Balance-by-code log entry (same).
10. **FT-LI-012** -- Validation failure log entry (same).
11. **FT-CFP-011** -- Reopen reason logged (log assertion, not behavioral).
12. **FT-SR-003** -- "Leaf-level detail" definition ambiguous.
13. **FT-AST-010** -- Redundant with FT-AST-002.
14. **FT-AST-011** -- Redundant with FT-AST-003.
15. **FT-AST-012** -- Redundant with FT-AST-002.
16. **FT-DSI-001** -- Module API surface inspection (structural, not behavioral).
17-28. **~12 CLI happy-path scenarios** -- "stdout contains X" without verifying computed values. These are thin by design (CLI-layer only), but still thin.

---

## Slop Patterns Detected

### 1. Mirror Tests (3 instances)
The BalanceSheet structural tests in the test file construct records and assert the values they just assigned. This is the textbook AI pattern: "I need a test, so I'll set a value and check I get it back." F# records are guaranteed by the compiler to store their fields correctly.

### 2. Source-Code Grep as "Test" (3 instances)
FT-LI-011, FT-LMS-008, FT-LMS-009 all search source files for string patterns. This is code linting, not testing. These belong in a CI lint step, not a behavioral test suite.

### 3. Cumulative Log Assertions (6 instances)
The logging tests read *all* log files and check for substrings. Since logs accumulate across the entire test run, any prior test that triggers the same log message will satisfy the assertion. The test for "Posting journal entry" in FT-LI-007 would pass even if FT-LI-007's specific post operation didn't log, as long as *any* other test posted a journal entry first.

### 4. CLI "Contains" Assertions (~12 instances)
Many CLI happy-path tests check `stdout.Contains("some keyword")` without verifying the actual output structure or computed values. This is the expected pattern for CLI-layer tests (service logic tested elsewhere), but it means a CLI that prints a static banner would pass several of these.

---

## What Wasn't Found

The areas I expected to find slop but didn't:

- **Balance Sheet retained earnings**: The test correctly derives 3000 rev - 1000 exp = 2000 RE. Not echoing setup.
- **Trial balance net balance formula**: Both normal-debit and normal-credit formulas tested with entries that have bidirectional activity (credit adjustments on revenue, debit adjustments on expenses). Not just "put in 700, get back 700."
- **Obligation state machine**: 18-example exhaustive invalid transition matrix. This is thorough -- every illegal edge in the graph is tested.
- **Balance projection combined formula**: 5000 + 1000 - 400 - 200 + 100 = 5500. All four components in one scenario. The expected value requires all four components to work correctly.
- **Opening balance balancing line**: Direction and amount computed, not echoed from input. Asset opening -> equity credit; liability opening -> equity debit.

---

## Recommendation

The suite is in good shape for autonomous-agent-generated code. The slop concentration is in non-critical areas (logging, source hygiene, type structure). The accounting-critical paths -- double-entry posting, balance calculations, the accounting equation, period scoping, voided entry exclusion, the obligation state machine, balance projection, and transfer confirmation -- are all solidly tested.

Priority fixes:
1. Move the three source-grep "tests" (FT-LI-011, FT-LMS-008, FT-LMS-009) to a linter or pre-commit hook. They don't belong in the behavioral test suite.
2. Delete the three BalanceSheet mirror tests. They test the F# compiler, not the application.
3. Consider isolating logging assertions by using a unique marker string per test and searching for that specific marker, rather than searching the cumulative log for generic strings.

---

*Signed: Slop Detector*
*Calibration: "Would this test catch a real bug, or would it pass even with a broken implementation?"*
