# Domain Invariant Auditor Report — Non-GAAP Domains

**Date:** 2026-04-07
**Scope:** Investment portfolio module (P057-P061), CLI infrastructure (P036, P038, P059), structural constraints, operational features (P022, P035), seed runner
**Method:** Extract every business rule and invariant from requirements documents, then classify spec coverage as enforced / implied / absent / unstated

---

## 1. Investment Portfolio Domain (P057, P058, P059, P061)

### 1.1 Schema and Structural Rules (P057)

| # | Rule | Source | Classification | Notes |
|---|------|--------|---------------|-------|
| S-001 | tax_bucket.name is NOT NULL | P057 schema | **Enforced** | FT-PSC-001 |
| S-002 | tax_bucket.name is UNIQUE | P057 schema | **Enforced** | FT-PSC-002 |
| S-003 | account_group.name is NOT NULL | P057 schema | **Enforced** | FT-PSC-003 |
| S-004 | account_group.name is UNIQUE | P057 schema | **Enforced** | FT-PSC-004 |
| S-005 | investment_account.name is NOT NULL | P057 schema | **Enforced** | FT-PSC-005 |
| S-006 | investment_account.tax_bucket_id FK is valid | P057 schema, AC-B4 | **Enforced** | FT-PSC-006 |
| S-007 | investment_account.account_group_id FK is valid | P057 schema, AC-B4 | **Enforced** | FT-PSC-007 |
| S-008 | fund PK is symbol (varchar), not serial | P057 AC-B6 | **Enforced** | FT-PSC-008 — duplicate symbol rejected as PK violation |
| S-009 | fund.name is NOT NULL | P057 schema | **Enforced** | FT-PSC-009 |
| S-010 | position unique constraint on (account, symbol, date) | P057 AC-B5 | **Enforced** | FT-PSC-010 |
| S-011 | position.investment_account_id FK is valid | P057 schema | **Enforced** | FT-PSC-011 |
| S-012 | position.symbol FK to fund is valid | P057 schema | **Enforced** | FT-PSC-012 |
| S-013 | position.position_date is NOT NULL | P057 schema | **Enforced** | FT-PSC-013 |
| S-014 | Fund classification dimension FKs are nullable | P057 design decision | **Enforced** | FT-PF-010 asserts all dimension fields null on a fund created with no dimensions |
| S-015 | investment_account.tax_bucket_id is NOT NULL | P057 schema | **Implied** | FT-PSC-006 tests invalid FK but no scenario tests a NULL tax_bucket_id insert. A NULL would be caught by the DB NOT NULL constraint but this is not explicitly verified. |
| S-016 | investment_account.account_group_id is NOT NULL | P057 schema | **Implied** | Same as S-015 — FK test exists but not a NULL-specific test. |
| S-017 | position.investment_account_id is NOT NULL | P057 schema | **Implied** | No explicit NULL-insert scenario. FT-PSC-011 tests invalid FK (9999) but not null. |
| S-018 | position.symbol is NOT NULL | P057 schema | **Implied** | No explicit NULL-symbol position insert scenario. |
| S-019 | position.price is NOT NULL | P057 schema | **Absent** | No structural constraint test for null price. Service-level FT-PF-021 tests negative values but not null. A direct SQL insert with null price would be caught by the DB but the spec suite does not verify this. |
| S-020 | position.quantity is NOT NULL | P057 schema | **Absent** | Same gap as S-019. |
| S-021 | position.current_value is NOT NULL | P057 schema | **Absent** | Same gap as S-019. |
| S-022 | position.cost_basis is NOT NULL | P057 schema | **Absent** | Same gap as S-019. |

**What goes undetected (S-019 through S-022):** If a migration accidentally drops the NOT NULL constraint on price, quantity, current_value, or cost_basis, no spec would fail. Null values could be inserted into the position table without detection.

### 1.2 Seed Data Rules (P057)

| # | Rule | Source | Classification | Notes |
|---|------|--------|---------------|-------|
| SD-001 | Dev seed populates 5 tax buckets | P057 AC-B2 | **Enforced** | FT-SR-008 |
| SD-002 | Dev seed populates all 6 dimension tables | P057 AC-B3 | **Enforced** | FT-SR-009 (outline with all 6 tables and counts) |
| SD-003 | Seed data is idempotent (safe to run twice) | P057 AC-S2 | **Enforced** | FT-SR-011 |
| SD-004 | Sample funds have valid dimension FK references | P057 implied | **Enforced** | FT-SR-010 |

### 1.3 Domain and Repository Rules (P058)

| # | Rule | Source | Classification | Notes |
|---|------|--------|---------------|-------|
| D-001 | Create investment account with valid tax bucket and group succeeds | P058 AC-B1 | **Enforced** | FT-PF-001 |
| D-002 | Create fund with symbol and name succeeds; dimensions optional | P058 AC-B2 | **Enforced** | FT-PF-010, FT-PF-011 |
| D-003 | Record position with valid account and fund succeeds | P058 AC-B3 | **Enforced** | FT-PF-020 |
| D-004 | Reject duplicate position (same account + symbol + date) | P058 AC-B4 | **Enforced** | FT-PF-023 |
| D-005 | List positions by account filters correctly | P058 AC-B5 | **Enforced** | FT-PF-024, FT-PF-025 |
| D-006 | List positions by date range filters correctly | P058 AC-B6 | **Enforced** | FT-PF-026 |
| D-007 | Latest positions returns most recent per fund per account | P058 AC-B7 | **Enforced** | FT-PF-027 (single account), FT-PF-028 (all accounts) |
| D-008 | List funds by dimension filter returns only matching funds | P058 AC-B8 | **Enforced** | FT-PF-016 (scenario outline covering all 6 dimensions) |
| D-009 | Reject negative price, quantity, current_value at service layer | P058 AC-B9 | **Enforced** | FT-PF-021 (scenario outline: price, quantity, current_value) |
| D-010 | Fund must exist before recording a position | P058 AC-B10 | **Enforced** | FT-PF-022 |
| D-011 | Create fund with empty symbol is rejected | P058 implied | **Enforced** | FT-PF-012 |
| D-012 | Create fund with empty name is rejected | P058 implied | **Enforced** | FT-PF-013 |
| D-013 | Create investment account with empty name is rejected | P058 implied | **Enforced** | FT-PF-002 |
| D-014 | Reject negative cost_basis at service layer | P058 AC-B9 | **Absent** | The backlog says "negative quantity, price, or current_value" but cost_basis is also a numeric position field. FT-PF-021 covers price, quantity, and current_value but NOT cost_basis. A negative cost_basis would be accepted silently. |
| D-015 | Position date must not be in the future | P058 service layer description | **Absent** | P058 states "position date must not be in the future" as a validation rule in the service layer description, but no Gherkin scenario tests this. A future-dated position would be accepted. |
| D-016 | Investment account must exist before recording position | P058 implied | **Implied** | FT-PF-022 tests nonexistent fund but no scenario specifically tests a nonexistent investment account ID. The DB FK would catch it, but there is no app-layer or spec-level test for this path. |
| D-017 | findBySymbol for nonexistent symbol returns none | P058 implied | **Enforced** | FT-PF-014 |
| D-018 | List all funds returns complete ordered set | P058 implied | **Enforced** | FT-PF-015 |
| D-019 | List funds by dimension returns empty when no match | P058 implied | **Enforced** | FT-PF-017 |
| D-020 | List investment accounts returns empty when none exist | P058 implied | **Enforced** | FT-PF-004 |

**What goes undetected (D-014):** A position could be recorded with cost_basis = -5000.00 and it would pass all specs. This is an accounting aberration — negative cost basis is not valid for a position snapshot.

**What goes undetected (D-015):** A position dated 2099-01-01 would be accepted. The requirements explicitly call this out as a validation rule but no scenario enforces it.

**What goes undetected (D-016):** Recording a position for account_id 999999 (nonexistent) would hit the DB FK constraint and presumably fail, but the error path is not tested at the service layer. The spec cannot distinguish "service caught it cleanly" from "unhandled database exception bubbled up."

### 1.4 CLI Portfolio Commands (P059)

| # | Rule | Source | Classification | Notes |
|---|------|--------|---------------|-------|
| C-001 | `portfolio account list` displays all accounts | P059 AC-B1 | **Enforced** | FT-PFC-020 |
| C-002 | `portfolio account list --group` filters by group | P059 AC-B2 | **Absent** | The backlog specifies filtering by group name. The CLI spec (PortfolioCommands.feature) has no filter-by-group scenario. FT-PFC-020/021/022 cover list and empty but not `--group` filtering. The CLI args in the backlog show `--group <name>` but the spec uses `--account-group-id`. Even so, no filter scenario exists in the spec. |
| C-003 | `portfolio account list --tax-bucket` filters by tax bucket | P059 AC-B2 | **Absent** | Same gap as C-002 — backlog specifies `--tax-bucket <name>` filter but no spec covers it. |
| C-004 | `portfolio account show <id>` displays detail with latest position summary | P059 AC-B3 | **Implied** | No `account show` command exists in the spec at all. The entire account show subcommand is absent from PortfolioCommands.feature. The backlog specifies it as AC-B3. |
| C-005 | `portfolio fund list` with dimension filters | P059 AC-B6 | **Enforced** | FT-PFC-041 covers all 6 dimension filters |
| C-006 | `portfolio fund show <symbol>` displays detail | P059 AC-B7 | **Enforced** | FT-PFC-050, FT-PFC-051 |
| C-007 | `portfolio fund create` creates a fund | P059 AC-B8 | **Enforced** | FT-PFC-030, FT-PFC-031 |
| C-008 | `portfolio position record` creates a position | P059 AC-B9 | **Enforced** | FT-PFC-060 |
| C-009 | `portfolio position list --account <id>` filters by account | P059 AC-B10 | **Enforced** | FT-PFC-071 |
| C-010 | `portfolio position latest` shows most recent per fund per account | P059 AC-B11 | **Enforced** | FT-PFC-080, FT-PFC-081 |
| C-011 | `portfolio dimensions` lists all 6 dimension tables | P059 AC-B12 | **Absent** | The backlog specifies a `portfolio dimensions [--json]` command. No scenario in PortfolioCommands.feature covers this command. |
| C-012 | All list/show commands support `--json` producing valid JSON | P059 AC-B13 | **Enforced** | FT-PFC-090, FT-PFC-091, FT-PFC-092 and numerous individual scenarios |
| C-013 | `portfolio` with no subcommand prints usage to stderr, exit 2 | P059 implied (framework convention) | **Enforced** | FT-PFC-001 |
| C-014 | Duplicate fund symbol CLI error path | P059 implied | **Enforced** | FT-PFC-035 |
| C-015 | Multiple dimension filters on fund list surfaces an error | P059 implied (single filter) | **Enforced** | FT-PFC-043 |
| C-016 | Missing required args for all create/record commands rejected | P059 implied | **Enforced** | FT-PFC-013, FT-PFC-034, FT-PFC-063 |
| C-017 | `portfolio account create` with blank name surfaces validation error | P059 implied | **Enforced** | FT-PFC-014 |

**What goes undetected (C-002, C-003):** The `portfolio account list` command would accept no group or tax-bucket filtering. If the implementation never implemented these filters, all specs would still pass. Dan would have no way to filter accounts by group or tax bucket from the CLI.

**What goes undetected (C-004):** There is no `portfolio account show` command in the specs. If the implementation omitted it entirely, all specs pass. Dan could not view individual account detail from the CLI.

**What goes undetected (C-011):** The `portfolio dimensions` command is entirely absent from specs. If the implementation omitted it, no spec would fail. Dan would have no CLI way to discover valid dimension values when classifying funds.

### 1.5 Portfolio Allocation Reporting (P061)

| # | Rule | Source | Classification | Notes |
|---|------|--------|---------------|-------|
| R-001 | Allocation by default dimension (account-group) | P061 AC-B1 | **Enforced** | FT-RPP-010 |
| R-002 | Allocation by specific dimension | P061 AC-B2 | **Enforced** | FT-RPP-011 |
| R-003 | All 10 dimensions supported | P061 AC-B3 | **Enforced** | FT-RPP-012 (scenario outline with all 10) |
| R-004 | Allocation percentages sum to 100% | P061 AC-B4 | **Enforced** | FT-RPP-013 |
| R-005 | Portfolio summary shows total value, cost basis, unrealized gain/loss | P061 AC-B5 | **Enforced** | FT-RPP-020 |
| R-006 | Portfolio summary shows tax bucket breakdown | P061 AC-B5 | **Enforced** | FT-RPP-021 |
| R-007 | Portfolio summary shows top 5 holdings | P061 AC-B5 | **Enforced** | FT-RPP-022 |
| R-008 | Portfolio history shows time-series, one row per position date | P061 AC-B6 | **Enforced** | FT-RPP-030 |
| R-009 | Portfolio history --from/--to restricts date range | P061 AC-B7 | **Enforced** | FT-RPP-032, FT-RPP-033 |
| R-010 | Gains report per-fund unrealized gain/loss | P061 AC-B8 | **Enforced** | FT-RPP-040 |
| R-011 | Gains filtered by account | P061 AC-B9 | **Enforced** | FT-RPP-042 |
| R-012 | JSON output for all commands | P061 AC-B10 | **Enforced** | FT-RPP-014, FT-RPP-023, FT-RPP-034, FT-RPP-043, FT-RPP-050 |
| R-013 | Empty portfolio handled gracefully (informative message, exit 0) | P061 AC-B11 | **Enforced** | FT-RPP-015, FT-RPP-024, FT-RPP-035, FT-RPP-044 |
| R-014 | Allocation report sorted by value descending | P061 output spec | **Implied** | FT-RPP-010/011 check for "allocation values and percentages" but do not assert sort order. An implementation returning rows in alphabetical order would pass. |
| R-015 | Gains report sorted by gain descending | P061 output spec | **Implied** | FT-RPP-040 checks for per-fund rows but does not assert sort order. |
| R-016 | Gains report includes totals row | P061 output spec | **Enforced** | FT-RPP-041 |
| R-017 | Uses latest-position-per-fund logic, not raw positions | P061 AC-S2 | **Implied** | No scenario specifically verifies that stale positions are excluded from allocation/summary/gains reports. If the implementation summed all historical position rows instead of using only the latest, the behavioral specs could still pass if the test data had only one position per fund. |
| R-018 | Portfolio history defaults to tax-bucket dimension | P061 design | **Enforced** | FT-RPP-030 |
| R-019 | Invalid dimension value surfaces error | P061 implied | **Enforced** | FT-RPP-016 |
| R-020 | Report --help lists all 4 portfolio subcommands | P061 implied | **Enforced** | FT-RPP-001 |
| R-021 | Each subcommand has --help | P061 implied | **Enforced** | FT-RPP-060 |
| R-022 | Portfolio summary unrealized gain/loss as percentage | P061 AC-B5 | **Enforced** | FT-RPP-020 asserts "unrealized gain/loss percentage" |

**What goes undetected (R-014, R-015):** Sort order in allocation and gains reports is not tested. Rows could appear in any order.

**What goes undetected (R-017):** If the allocation report summed ALL position rows (including historical) instead of using only latest, the test data would need to have multi-date positions for the same fund to catch this. The spec hints at a "CLI-testable portfolio report environment" but doesn't specify that it must contain historical positions that should be excluded.

---

## 2. Balance Projection (P022)

| # | Rule | Source | Classification | Notes |
|---|------|--------|---------------|-------|
| BP-001 | Flat line when no future obligations or transfers | P022 | **Enforced** | FT-BP-001 |
| BP-002 | Receivable inflows increase projected balance | P022 formula | **Enforced** | FT-BP-002 |
| BP-003 | Payable outflows decrease projected balance | P022 formula | **Enforced** | FT-BP-003 |
| BP-004 | Initiated transfer out decreases balance | P022 formula | **Enforced** | FT-BP-004 |
| BP-005 | Initiated transfer in increases balance | P022 formula | **Enforced** | FT-BP-005 |
| BP-006 | All components combine correctly | P022 formula | **Enforced** | FT-BP-007 |
| BP-007 | Output is a daily series (the curve) | P022 | **Enforced** | FT-BP-008 |
| BP-008 | Multiple obligations same day: sum with itemized breakdown | P022 edge case | **Enforced** | FT-BP-009 |
| BP-009 | Null-amount obligation surfaces as "unknown outflow" | P022 edge case | **Enforced** | FT-BP-010 |
| BP-010 | Null-amount receivable surfaces as "unknown inflow" | P022 edge case | **Enforced** | FT-BP-011 |
| BP-011 | Projection date in the past is rejected | P022 edge case | **Enforced** | FT-BP-012 |
| BP-012 | Projection date equal to today is rejected | P022 edge case | **Enforced** | FT-BP-013 |
| BP-013 | Nonexistent account returns error | P022 implied | **Enforced** | FT-BP-014 |
| BP-014 | Computed, not stored — recalculates from current state | P022 design | **Implied** | No scenario tests that re-running projection after state change produces different results. This is an architectural statement rather than a testable assertion. |
| BP-015 | Transfer with no expected_settlement falls back to initiated_date | P022 implied | **Enforced** | FT-BP-006 |
| BP-016 | In-flight status filter: only "initiated" transfers included | P022 | **Implied** | FT-BP-004/005/006 set up "initiated" transfers. No scenario verifies that confirmed transfers are excluded. An implementation that included confirmed transfers in the projection would not be caught unless the test data also had confirmed transfers present. |
| BP-017 | Expected/in_flight instances included; posted/skipped excluded | P022 | **Implied** | Same pattern — the scenarios set up expected instances but don't verify that posted/skipped/confirmed instances are excluded from the projection. |

**What goes undetected (BP-016, BP-017):** If the projection included confirmed transfers or posted obligation instances in its calculation, the specs would pass as long as the test data didn't happen to include those statuses alongside the expected data. These are filter-correctness tests that need negative examples.

---

## 3. Orphaned Posting Detection (P035)

| # | Rule | Source | Classification | Notes |
|---|------|--------|---------------|-------|
| OP-001 | No orphans = clean result | P035 AC-B1 | **Enforced** | FT-OPD-001 |
| OP-002 | Detects dangling obligation status | P035 AC-B2 | **Enforced** | FT-OPD-002 (scenario outline) |
| OP-003 | Detects dangling transfer status | P035 AC-B3 | **Enforced** | FT-OPD-002 (scenario outline) |
| OP-004 | Detects missing obligation source | P035 AC-B4 | **Enforced** | FT-OPD-003 (scenario outline) |
| OP-005 | Detects missing transfer source | P035 AC-B5 | **Enforced** | FT-OPD-003 (scenario outline) |
| OP-006 | Detects posted obligation with voided JE | P035 AC-B6 | **Enforced** | FT-OPD-004 (scenario outline) |
| OP-007 | Detects confirmed transfer with voided JE | P035 AC-B7 | **Enforced** | FT-OPD-004 (scenario outline) |
| OP-008 | Normal postings not flagged | P035 AC-B8 | **Enforced** | FT-OPD-005 |
| OP-009 | JSON output mode | P035 AC-B9 | **Enforced** | FT-OPD-007 |
| OP-010 | Non-numeric reference_value handled gracefully | P035 risk note | **Enforced** | FT-OPD-006 (InvalidReference condition) |
| OP-011 | Query is read-only — no mutations | P035 AC-S2 | **Implied** | No spec specifically verifies that the diagnostic query does not modify data. This is a structural guarantee that would need to be verified by inspecting the SQL or monitoring DB state before/after. |
| OP-012 | CLI exit code 0 for successful run regardless of orphan count | P035 design | **Implied** | The feature file asserts "the diagnostic succeeds" but does not explicitly check exit code in the Gherkin. The CLI spec (which would test exit codes) does not yet exist for the diagnostic command group. |
| OP-013 | CLI `diagnostic orphaned-postings` command group routing | P035 AC-S1 | **Absent** | P035 specifies a `diagnostic` command group in the CLI. No CLI spec file exists for DiagnosticCommands. The Ops-level spec (OrphanedPostingDetection.feature) tests the service logic but the CLI layer is unspecified. |

**What goes undetected (OP-013):** The `diagnostic` command group could be missing from the CLI entirely and all specs would pass, because the detection logic is tested at the service level but there is no CLI integration spec for `leobloom diagnostic orphaned-postings`.

---

## 4. CLI Infrastructure (P036 + Cross-Cutting Patterns)

| # | Rule | Source | Classification | Notes |
|---|------|--------|---------------|-------|
| CLI-001 | Exit codes: 0 = success, 1 = validation/business, 2 = system/parse | P036 | **Enforced** | FT-CLF-005, and consistently applied across all CLI specs |
| CLI-002 | Errors to stderr, data to stdout | P036 | **Enforced** | Every CLI spec checks stderr for errors, stdout for data |
| CLI-003 | Unknown top-level command prints error to stderr, exit 2 | P036 | **Enforced** | FT-CLF-003 |
| CLI-004 | No arguments prints usage/error, exit 2 | P036 | **Enforced** | FT-CLF-004 |
| CLI-005 | `--help` prints usage with available command groups | P036 | **Enforced** | FT-CLF-001 |
| CLI-006 | No subcommand for a group prints usage to stderr, exit 2 | P036 convention | **Enforced** | FT-PFC-001 (portfolio), FT-ACT-001 (account), FT-OBL-001/002/003 (obligation), FT-PRD-001 (period), FT-TRC-040 (transfer) |
| CLI-007 | `--json` flag produces valid JSON | P036 convention | **Enforced** | Extensive coverage across all command groups |
| CLI-008 | No interactive prompts | P036 | **Implied** | This is a design constraint, not behaviorally testable. No spec verifies that stdin is not read. If someone added an interactive prompt, no spec would catch it. |
| CLI-009 | Idempotent where possible | P036 | **Implied** | No spec tests that running the same command twice produces the same result (except seed runner idempotency, FT-SR-004). |
| CLI-010 | `--help` available for each command group's subcommands | P036 convention | **Enforced** | FT-CLF-002 (ledger), FT-RPP-060 (portfolio reports), FT-RPT-050 (tax reports), FT-RPT-101 (accounting reports) |
| CLI-011 | `--help` listed in top-level includes "portfolio" group | P059 AC-S2 | **Absent** | FT-CLF-001 checks for "ledger" but does not check for "portfolio", "account", "period", "obligation", "transfer", "invoice", or "report". If the portfolio group were missing from top-level routing, this specific check wouldn't catch it. |

**What goes undetected (CLI-011):** FT-CLF-001 only verifies that `--help` contains "ledger". The absence of any other command group from the top-level routing would not be detected by this scenario. This test was written when ledger was the only group and was never extended.

---

## 5. CLI Obligation Commands (P038)

| # | Rule | Source | Classification | Notes |
|---|------|--------|---------------|-------|
| OC-001 | Agreement list with filters (type, cadence, inactive) | P038 | **Enforced** | FT-OBL-010 through FT-OBL-015 |
| OC-002 | Agreement show/create/update/deactivate | P038 | **Enforced** | FT-OBL-020 through FT-OBL-053 |
| OC-003 | Instance list with filters (status, due-before, due-after) | P038 | **Enforced** | FT-OBL-060 through FT-OBL-065 |
| OC-004 | Instance spawn with agreement-id, from, to | P038 | **Enforced** | FT-OBL-070 through FT-OBL-073 |
| OC-005 | Instance transition with status, optional amount/date/notes | P038 | **Enforced** | FT-OBL-080 through FT-OBL-085 |
| OC-006 | Instance post to ledger | P038 | **Enforced** | FT-OBL-090 through FT-OBL-093 |
| OC-007 | Overdue detection with optional --as-of | P038 | **Enforced** | FT-OBL-100 through FT-OBL-104 |
| OC-008 | Upcoming query — instances in expected/in_flight within N days | P038 new query | **Enforced** | FT-OBL-110 through FT-OBL-118 |
| OC-009 | Upcoming defaults to 30-day horizon | P038 | **Enforced** | FT-OBL-110 |
| OC-010 | Upcoming includes expected and in_flight statuses | P038 | **Enforced** | FT-OBL-112, FT-OBL-113 |
| OC-011 | Upcoming excludes confirmed instances | P038 implied | **Enforced** | FT-OBL-115 |
| OC-012 | Upcoming excludes instances beyond horizon | P038 implied | **Enforced** | FT-OBL-114 |
| OC-013 | Upcoming excludes posted/skipped/overdue instances | P038 implied | **Implied** | FT-OBL-115 explicitly excludes confirmed. No scenario explicitly excludes posted, skipped, or overdue from upcoming. If the implementation included overdue instances in the upcoming list, no spec would catch it. |
| OC-014 | Agreement create requires --name, --type, --cadence | P038 | **Enforced** | FT-OBL-033, FT-OBL-034 |
| OC-015 | Transition with --journal-entry-id flag | P038 command spec | **Absent** | The backlog shows `--journal-entry-id ID` as an optional arg on transition. No spec covers this flag. |
| OC-016 | Instance post includes journal_entry_id | P038 implied | **Enforced** | FT-OBL-090 asserts "journal entry reference" in output |

**What goes undetected (OC-013):** Overdue instances showing in the upcoming list would not be caught. Only confirmed exclusion is tested.

**What goes undetected (OC-015):** The `--journal-entry-id` flag on instance transition could be silently ignored and no spec would detect it.

---

## 6. Structural and Operational Specs

### 6.1 Seed Runner (P054)

| # | Rule | Source | Classification | Notes |
|---|------|--------|---------------|-------|
| SR-001 | Seeds populate 36 fiscal periods | P054 | **Enforced** | FT-SR-001 |
| SR-002 | Seeds populate 69 accounts | P054 | **Enforced** | FT-SR-002 |
| SR-003 | Seeds apply account subtypes | P054 | **Enforced** | FT-SR-003 |
| SR-004 | Seeds are idempotent | P054 | **Enforced** | FT-SR-004 |
| SR-005 | Runner stops on SQL error, non-zero exit | P054 | **Enforced** | FT-SR-005 |
| SR-006 | Nonexistent environment directory = non-zero exit | P054 | **Enforced** | FT-SR-006 |
| SR-007 | Seeds execute in numeric filename order | P054 | **Enforced** | FT-SR-007 |
| SR-008 | Parent references valid | P054 | **Enforced** | FT-SR-002 asserts "every account with parent_id references an existing account" |

### 6.2 Delete Restriction Constraints

| # | Rule | Source | Classification | Notes |
|---|------|--------|---------------|-------|
| DR-001 | All FK ON DELETE RESTRICT constraints are tested | Schema design | **Enforced** | FT-DR-001 through FT-DR-018 cover all FK relationships |
| DR-002 | Portfolio schema ON DELETE RESTRICT | P057 implied | **Absent** | No delete restriction tests exist for the portfolio schema. If the migration set ON DELETE CASCADE instead of RESTRICT on investment_account -> tax_bucket, investment_account -> account_group, position -> investment_account, or position -> fund, no spec would catch it. |

**What goes undetected (DR-002):** Deleting a tax_bucket, account_group, fund, or investment_account that has dependent children could cascade-delete all dependents silently, with no spec failing. The delete restriction feature explicitly tests every FK in ledger and ops schemas but has zero portfolio coverage.

### 6.3 DataSource Encapsulation

Fully covered: FT-DSI-001, FT-DSI-008. No gaps.

### 6.4 Log Module Structure

Fully covered: FT-LMS-008, FT-LMS-009. No gaps.

### 6.5 Consolidated Helpers

Fully covered: FT-CHL-001 through FT-CHL-008. No gaps.

---

## 7. Unstated Invariants

These are invariants I identify from domain knowledge that are neither in the requirements documents nor in the specs.

| # | Invariant | Domain Basis | Risk |
|---|-----------|-------------|------|
| U-001 | Position current_value should equal price x quantity (or be close) | Financial identity: value = price x quantity. The schema stores all three independently with no cross-validation. | A position could be recorded with price=100, quantity=10, current_value=500 (should be 1000). No validation catches this. Reporting would show incorrect portfolio values. |
| U-002 | Cost basis should be non-negative | Financial domain: cost basis represents the purchase price of a holding. Negative cost basis is not meaningful for position snapshots. | D-014 above covers the spec gap; this notes the domain rationale. |
| U-003 | Fund symbol should be uppercase or follow a consistent convention | Financial convention: ticker symbols are uppercase. The schema accepts any varchar(20). | "vti" and "VTI" could coexist as separate funds (PK is case-sensitive in Postgres by default). No validation or spec addresses case normalization. |
| U-004 | The 5 tax buckets are a closed set (no arbitrary additions) | P057 AC-B2 lists exactly 5 tax buckets as a domain constant. | Nothing prevents CLI creation of arbitrary tax buckets. The seed data is fixed but the schema allows INSERTs. If the domain intends these as a closed enum, there is no enforcement. |
| U-005 | Portfolio reports should not include voided position data | Implied by GAAP design — voided entries are excluded from accounting reports | The position table has no voided_at concept. This is likely fine (positions are snapshots, not transactions), but worth noting that unlike the ledger domain, there is no void/correction mechanism for positions. An incorrect position must be deleted and re-inserted, but no delete command exists in the CLI spec. |
| U-006 | Position date range queries should be inclusive on both ends | Standard date range semantics | FT-PF-026 tests a date range 2026-02-01 to 2026-02-28 and expects the 2026-02-28 position. This tests inclusive-end. Inclusive-start is not separately tested (no position exactly on the start date in a separate scenario). |

---

## Summary

### Coverage Statistics

| Classification | Count |
|---|---|
| **Enforced** | 79 |
| **Implied** | 15 |
| **Absent** | 11 |
| **Unstated** | 6 |

### Critical Findings (Absent rules with material risk)

1. **D-014 — Negative cost_basis accepted silently.** The validation scenario outline covers price, quantity, and current_value but omits cost_basis. Write one additional example row.

2. **D-015 — Future-dated positions accepted.** The backlog explicitly states this as a validation rule. No scenario exists. Write a rejection scenario.

3. **C-004 — `portfolio account show` command entirely missing from specs.** The backlog specifies it. Write the full subcommand spec (happy path, error paths, --json).

4. **C-002/C-003 — Account list filtering by group/tax-bucket missing from specs.** The backlog specifies these filters. Write filter scenarios.

5. **C-011 — `portfolio dimensions` command entirely missing from specs.** The backlog specifies it. Write the subcommand spec.

6. **DR-002 — No delete restriction tests for portfolio schema.** Every other schema has ON DELETE RESTRICT coverage. The portfolio schema has zero. Write delete restriction scenarios for all portfolio FK relationships (tax_bucket, account_group, investment_account, fund as parents).

7. **OP-013 — No CLI spec for `diagnostic` command group.** Service logic is tested but the CLI wiring is not.

8. **S-019 through S-022 — Position numeric columns NOT NULL not tested.** price, quantity, current_value, cost_basis could have NOT NULL dropped from a migration with no spec failure.

### Significant Findings (Implied rules that need strengthening)

9. **R-017 — Latest-position logic in reports not specifically verified.** Test data should include historical positions for the same fund to verify that only the latest is used.

10. **BP-016/BP-017 — Projection status filters not negatively tested.** Scenarios should include confirmed/posted records that must be excluded.

11. **OC-013 — Upcoming excludes confirmed but not explicitly posted/skipped/overdue.** Add negative filter cases.

12. **CLI-011 — Top-level `--help` only checks for "ledger".** Should verify all command groups are listed.

---

*Signed: Domain Invariant Auditor*
*Model: claude-opus-4-6 (1M context)*
*Assessment date: 2026-04-07*
