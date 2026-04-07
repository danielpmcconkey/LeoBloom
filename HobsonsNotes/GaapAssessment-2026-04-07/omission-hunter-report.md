# Omission Hunter Report — LeoBloom Spec Suite

**Date:** 2026-04-07
**Scope:** All 43 .feature files under `/Specs/`, domain model (Ledger.fs, Portfolio.fs, Ops.fs), and backlog requirements.
**Method:** For each domain area, catalogue what is tested, derive expected invariants and edge cases from first principles, then identify what is absent.

---

## Findings Summary

| Severity | Count |
|----------|-------|
| Critical | 8 |
| Significant | 18 |
| Minor | 11 |

---

## 1. Ledger Core (PostJournalEntry, AccountBalance, VoidJournalEntry)

### What is tested

**PostJournalEntry (21 scenarios):** Happy path (2-line, 3-line, references, null source, memos), pure validation (unbalanced, zero amount, negative amount, single-line, empty description, invalid entry_type, empty source, empty reference type/value), DB validation (closed period, date outside range, inactive account, nonexistent period), atomicity (failed post leaves no rows), edge cases (duplicate references, future date with valid period).

**AccountBalance (11 scenarios):** Normal-debit and normal-credit accounts, accumulation across entries, mixed debit/credit netting, voided entry exclusion, as-of date filtering, zero-balance account, inactive account, nonexistent account (by ID and code), code/ID lookup equivalence.

**VoidJournalEntry (8 scenarios):** Happy path void, entry remains in DB, lines/references intact, idempotency (void already-voided), empty/whitespace reason rejected, nonexistent entry rejected, void succeeds in closed period.

### What is missing

**GAP-001: Entry with all lines on the same account is not rejected**
Severity: **Significant**
A journal entry where every line debits the same account and credits the same account is semantically meaningless. The domain validators enforce balance, positive amounts, and minimum 2 lines, but nothing prevents posting `debit 1010:500, credit 1010:500`. In double-entry bookkeeping, a self-balancing entry on a single account is a no-op that inflates the ledger. The spec should verify the system rejects an entry where all lines reference the same account.

**GAP-002: Posting to the exact boundary dates of a fiscal period**
Severity: **Minor**
FT-PJE-014 tests a date *outside* the period. There are no scenarios testing posting on exactly the start date or exactly the end date of the fiscal period. Boundary conditions at period edges are classic off-by-one territory.

**GAP-003: Very large decimal amounts (precision boundary)**
Severity: **Minor**
No scenario tests amounts at the boundary of PostgreSQL numeric precision. For a financial system, verifying that amounts like 999999999.99 or amounts with more than 2 decimal places behave predictably matters. The domain doesn't enforce decimal-place limits on journal entry amounts the way invoice amounts are validated.

**GAP-004: Whitespace-only description is not tested**
Severity: **Minor**
FT-PJE-010 tests empty description. The `validateDescription` function rejects `IsNullOrWhiteSpace`, but no scenario exercises a whitespace-only description (e.g., `"   "`). This is low risk since the validator handles it, but the spec doesn't document the behavior.

**GAP-005: Void in a period that was later reopened**
Severity: **Minor**
FT-VJE-008 tests voiding in a closed period. No scenario covers the edge case of voiding an entry whose period was closed and then reopened. This verifies that reopen actually unlocks the period for all operations, not just posting.

---

## 2. Financial Reports (TrialBalance, IncomeStatement, BalanceSheet, SubtreePLReport)

### What is tested

**TrialBalance (11 scenarios):** Balanced report, grouping with subtotals, groups with no activity omitted, voided entries excluded, empty period, closed period, accumulation, net balance formula, lookup equivalence, nonexistent period ID/key.

**IncomeStatement (14 scenarios):** Revenue + expense net income, revenue-only, expense-only, voided exclusion, no-activity omission, inactive accounts with activity, empty period, net loss, multiple accounts, normal-balance formula, period scoping, lookup equivalence, nonexistent period, closed period.

**BalanceSheet (11 scenarios):** Balanced check, accounting equation, positive/negative/zero retained earnings, empty ledger, voided exclusion, cross-period accumulation, multiple accounts per section, zero-balance-with-activity visibility, inactive accounts.

**SubtreePLReport (12 scenarios):** Revenue + expense subtree, revenue-only, expense-only, standalone account, non-rev/exp subtree, voided exclusion, outside-subtree exclusion, multi-level hierarchy, lookup equivalence, nonexistent account/period, empty period.

### What is missing

**GAP-006: Balance sheet as-of date before any entry date**
Severity: **Minor**
FT-BS-006 tests with no entries at all. No scenario tests a balance sheet as-of date that is *before* all entries exist (entries exist, but all after the as-of date). This is distinct from "no entries" and tests the cumulative date filter.

**GAP-007: Income statement / trial balance with an account that has contra-normal activity**
Severity: **Significant**
No scenario exercises an account with activity in the opposite direction of its normal balance resulting in a negative balance (e.g., an expense account with net credits). IS-011 and IS-012 test adjustments but the net balance is still positive. A truly negative balance on a section account (expense with credits > debits) would test the net-balance formula at its edge.

**GAP-008: Balance sheet accounting equation is never asserted as A = L + E as a first-class invariant**
Severity: **Critical**
FT-BS-001 checks `isBalanced`, and FT-BS-002 checks specific totals. But there is no scenario that asserts `assets section total == liabilities section total + total equity` as a direct equality check across a variety of conditions. The `isBalanced` flag is presumably derived from this equation, but the spec trusts the flag rather than verifying the arithmetic independently. If the `isBalanced` computation is wrong, every scenario that checks `the balance sheet is balanced` passes despite a bug.

**GAP-009: Subtree P&L for a root account that is itself a revenue or expense account with children of mixed types**
Severity: **Minor**
FT-SPL-004 tests a standalone revenue account. No scenario tests a revenue parent account with both revenue children and expense children, verifying that the parent's own activity is included in the correct section.

---

## 3. Fiscal Period Management (CloseReopenFiscalPeriod)

### What is tested (12 scenarios)

Close, reopen, idempotency both directions, empty/whitespace reason rejection, nonexistent period, full lifecycle, posting rejected after close, side effects (balances unchanged by close), reopen reason logged.

### What is missing

**GAP-010: Reopening a period and then posting to it**
Severity: **Significant**
FT-CFP-010 tests that posting is rejected after close. There is no scenario that completes the full cycle: close a period, reopen it, then post to it successfully. This is the positive-path proof that reopen actually restores the period to a writable state.

**GAP-011: Overlapping fiscal period dates**
Severity: **Critical**
The domain model and backlog mention `FiscalPeriodRepository.findByDate`. No spec anywhere tests what happens when two fiscal periods have overlapping date ranges. If someone creates periods 2026-03-01 to 2026-03-31 and 2026-03-15 to 2026-04-15, which period does `findByDate(2026-03-20)` return? The `PostObligationToLedger` feature relies on `findByDate` but the spec does not test the ambiguous case. The creation path (PRD-045 tests start after end, PRD-046 tests duplicate keys) does not test overlapping date ranges with different keys. This could silently post entries to the wrong period.

---

## 4. Opening Balances

### What is tested (14 scenarios)

Balanced entry creation, balancing line direction (debit/credit), single account entry, metadata verification, duplicate account rejection, empty entries rejection, zero balance rejection, balancing account in entries rejection, nonexistent account/balancing account, non-equity balancing account, default/custom description.

### What is missing

**GAP-012: Negative balance in opening balances**
Severity: **Significant**
The command takes `balance: decimal` entries. No scenario tests a negative balance entry (e.g., an asset with a negative balance, which is unusual but possible for overdrawn accounts). The spec should verify whether negative balances are rejected or accepted, and if accepted, that the debit/credit direction is computed correctly.

**GAP-013: Opening balances posted to a closed fiscal period**
Severity: **Significant**
Opening balances are posted as journal entries through the posting engine. No scenario tests posting opening balances against a closed period. The posting engine would reject it, but the spec doesn't document this expected behavior for the opening balances path specifically.

---

## 5. Obligation Lifecycle (Agreements, Spawn, Transitions, Overdue, PostToLedger)

### What is tested

**ObligationAgreementCrud (30 scenarios):** Full CRUD with all/required fields, pure validation (empty name, name too long, counterparty too long, non-positive amount, expected day outside 1-31, multiple errors), DB validation (nonexistent/inactive source/dest accounts, source=dest), getById, list (with filters), update, deactivate, deactivate blocked by active instances, reactivate.

**SpawnObligationInstances (18 scenarios):** Pure date generation (monthly, quarterly, annual, one-time, clamping, defaults), name generation, spawn integration (happy path, variable amount, one-time), overlap/idempotency, error cases (inactive/nonexistent agreement).

**StatusTransitions (23 scenarios + outline with 18 invalid pairs):** All valid forward transitions (expected->in_flight, expected->confirmed, expected->overdue, expected->skipped, in_flight->confirmed, in_flight->overdue, overdue->confirmed, confirmed->posted), amount handling, all invalid transitions (18 pairs in outline), guard violations (posted without JE, posted with nonexistent JE, skipped without notes, confirmed without date), edge cases (inactive instance, nonexistent instance, notes preservation).

**OverdueDetection (11 scenarios):** Single/multiple overdue, boundary (on/after reference date), filtering by status (in_flight, already overdue, confirmed, inactive), idempotency, mixed set, no candidates.

**PostObligationToLedger (19 scenarios):** Happy path receivable/payable, metadata (description, source, reference, entry_date, journal_entry_id), validation (non-confirmed status, no amount, no confirmed_date, missing source/dest accounts, no fiscal period, closed period), double-posting prevention, atomicity of failure, idempotency guard, voided prior JE.

### What is missing

**GAP-014: Overdue detection for in_flight instances past their expected date**
Severity: **Significant**
The overdue detection only transitions `expected` instances to `overdue`. FT-OD-005 confirms `in_flight` instances are NOT flagged. But the domain model allows `in_flight -> overdue` (it's in the `allowedTransitions` map, ST-006 tests it). The *batch* detection skips them, but there's no documented rationale in the spec for why in_flight instances that are past due aren't auto-transitioned by the batch. This is a design decision that should be explicitly tested as intentional, not just observed as a side effect of the query filter. If the batch query changes to include in_flight, the spec wouldn't catch the regression.

**GAP-015: Spawning instances when agreement expectedDay is set but cadence is one_time**
Severity: **Minor**
The spawn date generation for `OneTime` ignores `expectedDay` and uses `startDate`. No scenario verifies that a one_time agreement with `expectedDay = 15` still produces the start date, not the 15th. This is a boundary between cadence types.

**GAP-016: Posting an obligation instance where the agreement's accounts have become inactive since the agreement was created**
Severity: **Significant**
FT-POL-010/011 test missing accounts. But no scenario tests what happens when the agreement references accounts that *existed and were active at creation time* but have since been deactivated. The posting path reads the agreement's account IDs and passes them to the journal entry engine, which validates account activity. This should be explicitly tested.

**GAP-017: Confirmed transition with zero amount**
Severity: **Significant**
ST-010 tests overriding amount. ST-011 tests missing amount. No scenario tests transitioning to confirmed with amount = 0.00. The domain validators don't appear to check for positive amounts on instances the way they do on agreements. A zero-amount confirmed instance would produce a journal entry with zero amounts, which would fail the posting engine's `validateAmountsPositive`. This cross-domain failure path isn't tested.

**GAP-018: Deactivate agreement with instances in non-terminal statuses other than expected**
Severity: **Minor**
FT-OA-028 tests deactivation blocked by "active obligation instances." The test uses a generic "active instance" but doesn't verify the blocking behavior across different instance statuses (in_flight, overdue, confirmed). If the blocking query checks `isActive` but the spec only tests one status, a query change could miss non-terminal states.

---

## 6. Transfers

### What is tested (16 scenarios)

Initiate happy path, validation (same account, non-asset, inactive, zero/negative amount), confirm happy path, metadata (source, reference, entry_date, description auto-gen), validation (non-initiated status, no fiscal period), idempotency guard, voided prior JE.

### What is missing

**GAP-019: Confirming a transfer when the fiscal period is closed**
Severity: **Significant**
FT-TRF-014 tests missing fiscal period. There is no scenario for confirming a transfer on a date covered by a *closed* fiscal period. The posting engine rejects it, but the transfer-specific spec doesn't document this. By contrast, PostObligationToLedger has explicit FT-POL-013 for this case.

**GAP-020: Transfer show/list at the service level**
Severity: **Minor**
The CLI specs (TransferCommands.feature) test show and list via CLI. But there are no service-level behavioral specs for listing or filtering transfers. If the CLI tests are all that cover list/show behavior, a service-layer regression wouldn't be caught by the behavioral specs.

**GAP-021: Atomicity of failed transfer confirmation**
Severity: **Significant**
PostObligationToLedger has FT-POL-017 (failed post leaves instance in confirmed status with no JE). There is no equivalent for transfers. If confirmation fails (e.g., closed period), the spec doesn't verify that the transfer remains in `initiated` status with no journal_entry_id set.

---

## 7. Invoice Persistence

### What is tested (20 scenarios)

Record happy path (full fields, null optionals, closed period, zero rent, zero utility), validation (empty tenant, tenant too long, negative amounts, decimal precision, total != components, multiple errors), DB validation (nonexistent period, duplicate tenant+period), show by ID, nonexistent show, list (no filter, by tenant, by period, by both, empty).

### What is missing

**GAP-022: Invoice with totalAmount = 0 (rent and utility both zero)**
Severity: **Minor**
FT-INV-004/005 test zero rent or zero utility individually. No scenario tests both zero, resulting in totalAmount = 0. The `validateAmount` function allows zero for individual fields (only negatives are rejected) but the total summing to zero is a degenerate case.

---

## 8. Balance Projection

### What is tested (14 scenarios)

Flat line, inflows (receivable), outflows (payable), transfers in/out, no-settlement fallback, combined formula, series coverage (every day), same-day sum with itemized breakdown, null-amount obligations (unknown inflow/outflow markers), validation (past date, today, nonexistent account).

### What is missing

**GAP-023: Confirmed/posted obligation instances excluded from projection**
Severity: **Critical**
The backlog spec (022) states that projection inflows/outflows come from instances in `expected` or `in_flight` status. But no scenario verifies that a `confirmed` or `posted` instance is *excluded* from the projection. If the query doesn't filter by status, confirmed instances (already accounted for in the ledger) would be double-counted in the projection. This is a silent data corruption risk for cash planning.

**GAP-024: Projection with overdue instances**
Severity: **Significant**
Overdue is a valid instance status. The backlog says `expected` or `in_flight`, but the spec doesn't test whether overdue instances appear in the projection. An overdue instance that hasn't been confirmed represents a real expected flow, but whether it should be included is a business decision that the spec should make explicit.

**GAP-025: Projection where confirmed transfers are excluded**
Severity: **Critical**
The backlog says `status = 'initiated'` transfers. No scenario verifies that a `confirmed` transfer (which has already been posted to the ledger) is excluded from the projection. Same double-counting risk as GAP-023.

**GAP-026: Projection starting balance uses as-of-today, not as-of-projection-end**
Severity: **Significant**
The spec uses `current balance` as the anchor. No scenario verifies that the opening balance is calculated as of today (not some future date). If the balance query uses the projection end date instead of today, the projection would include future entries in the baseline.

---

## 9. Orphaned Posting Detection

### What is tested (7 scenarios)

Clean result, dangling status (obligation + transfer), missing source (obligation + transfer), voided backing entry (obligation posted + transfer confirmed), normal postings not flagged, invalid reference, JSON output.

### What is missing

**GAP-027: Multiple orphan conditions in a single diagnostic run**
Severity: **Minor**
Each scenario tests one condition in isolation. No scenario verifies that when multiple orphan conditions exist simultaneously (e.g., one dangling + one missing source + one voided), all are reported. This tests the aggregation, not just detection.

**GAP-028: Voided backing entry for non-terminal source states**
Severity: **Minor**
FT-OPD-004 tests obligation in `posted` and transfer in `confirmed`. But what about an obligation in `confirmed` status with a voided JE? The spec only checks terminal posted/confirmed states. A confirmed obligation with a voided JE is unlikely but the diagnostic should handle it gracefully.

---

## 10. Portfolio Module (InvestmentAccount, Fund, Position)

### What is tested

**InvestmentAccount (4 scenarios):** Create happy path, empty name rejection, list all, list empty.

**Fund (8 scenarios):** Create with required fields, create with dimensions, empty symbol/name rejection, findBySymbol nonexistent, list all, list by dimension filter (6 dimensions), empty dimension results.

**Position (9 scenarios):** Record happy path, negative field validation (price, quantity, current_value), nonexistent fund, duplicate rejection, list by account, list empty, list by date range, latest per fund, latest across accounts.

### What is missing

**GAP-029: Investment account — create with nonexistent tax_bucket_id or account_group_id**
Severity: **Significant**
The structural constraints (FT-PSC-006, PSC-007) test FK violations at the DB level. But the service-level specs have no scenario for creating an investment account referencing a nonexistent tax bucket or account group. The service should surface a meaningful error, not a raw FK violation.

**GAP-030: Investment account — create with duplicate name**
Severity: **Minor**
No uniqueness constraint on investment account name is tested at either the structural or behavioral level. If two accounts can have the same name, that's fine but should be documented. If they shouldn't, it's untested.

**GAP-031: Fund — create with nonexistent dimension IDs**
Severity: **Significant**
FT-PF-011 creates a fund with valid dimension IDs. No scenario tests creating a fund with a nonexistent dimension ID (e.g., investmentTypeId 999). The structural constraints cover this at the DB level, but the service should return a domain-appropriate error.

**GAP-032: Position — record with zero price, zero quantity, or zero current_value**
Severity: **Significant**
FT-PF-021 tests negative values. No scenario tests zero values. The validators reject `< 0` (or `< -0.01`), but zero price/quantity may be valid (e.g., a position being closed out) or invalid depending on business rules. The spec is silent.

**GAP-033: Position — record with negative cost_basis**
Severity: **Significant**
FT-PF-021 tests negative price, quantity, and current_value. It does NOT test negative cost_basis. The domain type `costBasis: decimal` has no apparent validation. A negative cost basis would produce incorrect gain/loss calculations in the gains report.

**GAP-034: Position — record for nonexistent investment account**
Severity: **Significant**
FT-PF-022 tests nonexistent fund symbol. No behavioral scenario tests recording a position for a nonexistent investment account ID. The structural constraint (PSC-011) covers the FK, but the service error path is untested.

---

## 11. Portfolio Reporting (CLI PortfolioReportCommands)

### What is tested (22 scenarios)

Allocation (default dimension, specific dimension, all 10 dimensions, percentages sum to 100, JSON, empty portfolio, invalid dimension), portfolio-summary (total/cost/gain, tax bucket breakdown, top 5, JSON, empty), portfolio-history (default, by group, date filtering, JSON, empty, invalid date), gains (per-fund, totals, account filter, JSON, empty, invalid account), JSON consistency, help.

### What is missing

**GAP-035: Allocation report where a fund has no dimension assignment for the selected dimension**
Severity: **Minor**
Funds can have null dimension fields. No scenario tests what category label is used when a fund's selected dimension is null (e.g., `report allocation --by sector` when some funds have no sector). Is it "Uncategorized"? Is it omitted? The spec doesn't say.

**GAP-036: Gains report with zero cost basis**
Severity: **Significant**
If cost_basis is zero, the gain/loss percentage is undefined (division by zero). No scenario tests this boundary. The `totalGainLossPct` computation would need to handle it gracefully.

---

## 12. Structural and Cross-Cutting

### What is tested

**LedgerStructuralConstraints (29 scenarios):** NOT NULL on all required columns across account_type, account, fiscal_period, journal_entry, journal_entry_reference, journal_entry_line. UNIQUE on account_type name, account code, fiscal_period key. FK on all foreign keys. Nullable verification for parent_id, voided_at.

**OpsStructuralConstraints (36 scenarios):** NOT NULL/FK/UNIQUE for obligation_agreement, obligation_instance, transfer, invoice. Varchar NOT NULL for obligation_type, cadence, status.

**PortfolioStructuralConstraints (13 scenarios):** NOT NULL/UNIQUE/FK for tax_bucket, account_group, investment_account, fund, position. Position composite unique.

**DeleteRestrictionConstraints (14 scenarios):** ON DELETE RESTRICT for all parent-child FK relationships across both schemas.

**Other structural (ConsolidatedHelpers, DataSourceEncapsulation, LogModuleStructure, SeedRunner):** Helper consolidation round-trip, DataSource API surface, no printfn in source, seed data population and idempotency.

### What is missing

**GAP-037: Fiscal period date overlap prevention at the DB or service level**
Severity: **Critical**
This is the structural manifestation of GAP-011. No structural constraint prevents overlapping fiscal period date ranges. The `period_key` is unique, but `(2026-03, 2026-03-01, 2026-03-31)` and `(2026-03-alt, 2026-03-15, 2026-04-15)` could both be inserted. There is no CHECK constraint, exclusion constraint, or service-layer validation preventing this. This is a data integrity gap that could cause `findByDate` to return ambiguous results.

**GAP-038: Account creation/update service-level behavioral specs**
Severity: **Critical**
The structural specs test DB constraints on accounts. The CLI specs test `account list`, `account show`, `account balance`. But there is no service-level behavioral spec for *creating* or *updating* an account. The chart of accounts is populated by seeds, but there's no spec governing what happens when an account is created with an invalid account_type_id, a duplicate code, or an invalid parent_id through the service layer. Account sub-type validation (AST feature) exists but only for the subtype dimension. The rest of account CRUD has no behavioral spec.

**GAP-039: Delete restriction for portfolio schema parent-child relationships**
Severity: **Minor**
DeleteRestrictionConstraints.feature covers all ledger and ops FK relationships. It does NOT cover portfolio schema relationships (e.g., deleting a tax_bucket with dependent investment_accounts, deleting a fund with dependent positions, deleting an investment_account with dependent positions). The FK constraints exist (tested by PSC structural specs), but the ON DELETE behavior is not verified.

---

## 13. Cross-Domain Integration Gaps

**GAP-040: End-to-end obligation lifecycle from spawn through ledger posting**
Severity: **Significant**
Each stage of the obligation lifecycle is tested in isolation (spawn, transition, post). There is no end-to-end scenario that spans: create agreement -> spawn instances -> transition expected->in_flight->confirmed -> post to ledger -> verify account balance reflects the posting. This full lifecycle test would catch integration failures between the separately tested components.

**GAP-041: Transfer lifecycle end-to-end with balance verification**
Severity: **Significant**
Similar to GAP-040. No scenario initiates a transfer, confirms it, and then verifies that both the source and destination account balances reflect the transfer amount. Each piece is tested, but the chain is not.

**GAP-042: Voided journal entry impact on all downstream reports**
Severity: **Minor**
Individual report specs test voided entry exclusion (balance sheet, trial balance, income statement, account balance, subtree P&L). But there is no scenario that voids an entry and then verifies all affected reports simultaneously, confirming consistency across the entire reporting surface.

---

## Severity Distribution by Domain Area

| Domain | Critical | Significant | Minor |
|--------|----------|-------------|-------|
| Ledger Core | 0 | 1 | 3 |
| Financial Reports | 1 | 1 | 2 |
| Fiscal Period Mgmt | 1 | 1 | 0 |
| Opening Balances | 0 | 2 | 0 |
| Obligation Lifecycle | 0 | 3 | 2 |
| Transfers | 0 | 2 | 1 |
| Invoice | 0 | 0 | 1 |
| Balance Projection | 2 | 2 | 0 |
| Orphaned Posting | 0 | 0 | 2 |
| Portfolio Module | 0 | 5 | 1 |
| Portfolio Reporting | 0 | 1 | 1 |
| Structural / Cross-Cutting | 2 | 0 | 1 |
| Cross-Domain Integration | 0 | 2 | 1 |

---

## Top Priority Findings

The following gaps represent the highest risk to data integrity and should be addressed first:

1. **GAP-008 (Critical):** Balance sheet A=L+E is never verified independently of the `isBalanced` flag. If the flag computation is wrong, all balance sheet tests pass silently.

2. **GAP-011 / GAP-037 (Critical):** No protection against overlapping fiscal period date ranges. `findByDate` returns ambiguous results. Entries could post to the wrong period.

3. **GAP-023 / GAP-025 (Critical):** Balance projection does not verify exclusion of confirmed/posted obligations or confirmed transfers. Double-counting risk in cash projections.

4. **GAP-038 (Critical):** No service-level behavioral specs for account creation or update. The chart of accounts is a load-bearing data structure with no behavioral test coverage for its write path.

---

*Omission Hunter*
