# GAAP Assessment Synthesis — LeoBloom Full Sweep

**Date:** 2026-04-07
**Assessor:** Hobson
**Scope:** 43 feature files, ~400+ Gherkin scenarios, full source tree (9 F# projects), all migrations, all test code
**Auditors:** GAAP Mapper, Ledger Tracer, Omission Hunter, Slop Detector, Domain Invariant Auditor, Tech Debt Auditor, Test Harness Auditor, Consistency Auditor, DRY Auditor

---

## Executive Summary

The accounting engine is sound and the codebase is remarkably consistent for
one built by parallel autonomous agents. The math is clean (89 scenarios
traced, zero arithmetic errors), GAAP principles are well-enforced (23 of 26
principles with scenario-level enforcement), and the slop rate is ~1.5% —
concentrated in logging infrastructure, not accounting logic. The architecture
is disciplined: clean domain separation, one-way dependencies, idempotent
seeds, void-not-delete semantics, dual-mode CLI output.

That said, nine independent auditors converged on several real risks. The
findings below are cross-referenced — when three agents independently flag the
same issue from different angles, that's signal, not noise.

---

## Verdict by Auditor

| Auditor | Headline |
|---|---|
| **GAAP Mapper** | **23 enforced, 7 implied, 3 absent.** Core pillars solid. Cash-basis consistency is the biggest implied gap — the system can't prevent accrual-style entries. |
| **Ledger Tracer** | **Clean.** 89 scenarios, zero discrepancies. The math is right. |
| **Omission Hunter** | **8 critical, 18 significant, 11 minor.** Overlapping fiscal periods, balance projection double-counting, and balance sheet equation independence are the top criticals. |
| **Slop Detector** | **6 slop (1.5%), 28 thin, rest solid.** Slop is in logging infra and source-code greps. Accounting specs are strong. |
| **Domain Invariant Auditor** | **79 enforced, 15 implied, 11 absent, 6 unstated.** Portfolio module has missing CLI commands, validation gaps (negative cost_basis, future dates), and zero delete restriction coverage. |
| **Tech Debt Auditor** | **1 career risk, 1 regret, 5 friction, 5 acceptable debt, 5 solid.** Non-atomic multi-phase writes are the career risk. Silent error swallowing is the regret. |
| **Test Harness Auditor** | **3 structural, 3 fragile, 3 friction, 2 solid.** Global-scope reporting queries + default parallel execution = the active phantom failure mode. |
| **Consistency Auditor** | **2 confusing, 6 noisy, 2 harmless, 8 consistent.** Impressively uniform for multi-agent authorship. TransferCommands date parser and ReportCommands dispatch signature are the real issues. |
| **DRY Auditor** | **15 findings (4 semantic high-risk, 5 semantic medium, 6 structural).** Normal balance resolution in 7+ places is the most dangerous duplication in the codebase. |

---

## Cross-Agent Convergences

These findings were flagged independently by multiple auditors. The
convergence increases confidence that these are real, not false positives.

### 1. Normal Balance Resolution — 7+ Locations (3 agents)

**DRY Auditor** (Finding 15, High): The formula "debit-normal: debits - credits;
credit-normal: credits - debits" is implemented in TrialBalanceRepository,
IncomeStatementRepository, BalanceSheetRepository, SubtreePLRepository,
AccountBalanceRepository, ScheduleERepository, and GeneralLedgerReportService.

**Consistency Auditor** (N/A — SQL pattern variation noted).

**GAAP Mapper** (enforced via multiple specs, but enforcement is scattered).

**Risk:** A missed update to one location produces silently incorrect financial
figures. This is the single most dangerous duplication in the codebase.

**Fix:** Extract a pure `resolveBalance` function in the Domain module. All
repositories call it.

### 2. Non-Atomic Multi-Phase Writes (2 agents)

**Tech Debt Auditor** (F1, Career Risk): TransferService.confirm and
ObligationPostingService.postToLedger perform multi-phase writes across
separate connections. If the JE posts but the record update fails, the ledger
has an orphaned entry.

**Omission Hunter** (GAP-021, Significant): No atomicity test for failed
transfer confirmation — the transfer-side gap that PostObligationToLedger
already covers at FT-POL-017.

**Risk:** Inconsistent state between ledger and ops during failure windows.
The idempotency guard mitigates on retry, but there's no automated recovery
or alerting. The orphaned posting diagnostic catches some cases but not this
specific condition.

**Fix:** Pass a transaction through the call chain. Significant refactor.

### 3. Silent Error Swallowing in Ops List Operations (3 agents)

**Tech Debt Auditor** (F2, Regret): List methods return empty lists on
database errors. The caller can't distinguish "no data" from "database down."

**Consistency Auditor** (Confusing): 6 Ops service methods return raw types;
5 Ledger/Portfolio methods use `Result<T list, string list>`. Two conventions.

**DRY Auditor** (noted as part of the service boilerplate pattern).

**Risk:** A user runs `obligation list`, sees nothing, concludes they're
current, and misses a payment. Meanwhile the database was unreachable.

**Fix:** Trivial. Change return types to `Result` like everything else.

### 4. parseDate Duplication + TransferCommands Divergence (3 agents)

**DRY Auditor** (Finding 2): Identical function in 7 CLI modules.

**Consistency Auditor** (C2, Confusing): TransferCommands uses lenient
`TryParse` while all others use strict `TryParseExact("yyyy-MM-dd")`. The
error message claims strict format.

**Slop Detector** (not flagged — spec-level, not code-level).

**Fix:** Extract to shared module, fix TransferCommands to use strict parser.

### 5. Test Data Collision / Phantom Failures (2 agents)

**Test Harness Auditor** (Findings 1, 2, 7 — all Structural): Global-scope
reporting queries, cleanup residue accumulation, and default parallel xUnit
execution combine to produce the phantom failures BD is debugging right now.

**DRY Auditor** (Finding 2 in tests — postEntry helper duplicated in 6 files).

**Risk:** Already manifesting. Gets worse linearly with test count.

**Fix:** Scope reporting tests to their own data; extract shared helpers;
configure xUnit parallelism explicitly.

---

## Priority Tiers

### Tier 0 — Data Integrity (fix before trusting financial output)

| # | Finding | Source | Effort |
|---|---------|--------|--------|
| 1 | **Non-atomic multi-phase writes** — JE + record update across separate connections | Tech Debt F1 | Significant |
| 2 | **Overlapping fiscal period prevention** — no guard at any layer; entries could post to wrong period | Omission Hunter GAP-011/037 | Moderate |
| 3 | **Normal balance resolution consolidation** — 7+ copies of the core accounting formula | DRY Finding 15 | Moderate |
| 4 | **Balance projection status filter tests** — confirmed/posted items not verified as excluded; double-counting risk | Omission Hunter GAP-023/025, Domain Invariant BP-016/017 | Small (test-only) |

### Tier 1 — Correctness Gaps (fix before production reliance)

| # | Finding | Source | Effort |
|---|---------|--------|--------|
| 5 | **Silent error swallowing** — Ops list methods hide database failures | Tech Debt F2, Consistency Auditor | Trivial |
| 6 | **Balance sheet A=L+E independent verification** — tests trust the `isBalanced` flag | Omission Hunter GAP-008 | Small (test-only) |
| 7 | **Account CRUD behavioral specs** — zero service-level coverage for the write path | Omission Hunter GAP-038 | Moderate |
| 8 | **Portfolio delete restriction tests** — zero ON DELETE RESTRICT coverage for portfolio schema | Domain Invariant DR-002, Omission Hunter GAP-039 | Small (test-only) |
| 9 | **Missing portfolio CLI commands** — `account show`, `account list --group/--tax-bucket`, `dimensions` | Domain Invariant C-002/003/004/011 | Moderate |
| 10 | **Portfolio validation gaps** — negative cost_basis accepted, future-dated positions accepted | Domain Invariant D-014/D-015 | Small |
| 11 | **Transfer atomicity test** — no equivalent of FT-POL-017 for transfers | Omission Hunter GAP-021 | Small (test-only) |
| 12 | **Transfer closed-period test** — no equivalent of FT-POL-013 for transfers | Omission Hunter GAP-019 | Small (test-only) |

### Tier 2 — Test Infrastructure (fix for sustainable growth)

| # | Finding | Source | Effort |
|---|---------|--------|--------|
| 13 | **Reporting test isolation** — scope assertions to test-created data | Test Harness Finding 1 | Moderate |
| 14 | **Extract shared test helpers** — postEntry in 6 files, account type IDs in 10+ files | Test Harness Finding 5/6, DRY Finding 2 | Small |
| 15 | **xUnit parallelism configuration** — document and control the parallel behavior | Test Harness Finding 7 | Trivial |
| 16 | **PortfolioStructuralConstraintsTests** — migrate from manual cleanup to PortfolioTracker | Test Harness Finding 10 | Small |

### Tier 3 — Housekeeping

| # | Finding | Source | Effort |
|---|---------|--------|--------|
| 17 | **Npgsql version skew** (9.x vs 10.x) | Tech Debt F3 | Trivial |
| 18 | **Ghost LeoBloom.Api directory** | Tech Debt F6 | Trivial |
| 19 | **Log path defaults to Docker sandbox** | Tech Debt F11 | Trivial |
| 20 | **Portfolio schema in connection search path** | Tech Debt F4 | Trivial |
| 21 | **ReportCommands.dispatch missing isJson** | Consistency C1 | Small |
| 22 | **TransferCommands lenient date parser** | Consistency C2 | Trivial |
| 23 | **JournalEntry reader extraction** | DRY Finding 1, Consistency N3 | Small |
| 24 | **Empty-list messaging inconsistency** (invoices, transfers) | Consistency N2 | Trivial |
| 25 | **Database backup strategy** | Tech Debt F8 | Trivial |

---

## What's Solid

Worth stating plainly — this codebase earns its stripes in several areas:

- **The math is right.** 89 scenarios traced by the Ledger Tracer. Zero
  discrepancies. Every journal entry balances. Every report assertion matches
  the arithmetic.

- **GAAP fundamentals are enforced.** Double-entry, append-only audit trail,
  period integrity, accounting equation, financial statement articulation —
  all scenario-enforced.

- **The domain model is clean.** Discriminated unions, Result types, one-way
  dependencies, pure validation separated from DB validation. The Tech Debt
  Auditor called this out as "worth preserving — don't let anyone simplify
  this into a single module."

- **Void semantics are correct.** Void-not-delete, audit trail, idempotent
  re-void, partial index for non-voided queries.

- **Architectural consistency is impressive for multi-agent work.** 8 of 11
  pattern categories fully consistent. The Consistency Auditor found only 2
  confusing deviations across the entire codebase. Nightshift's output is
  coherent.

- **Slop rate is 1.5%.** The lowest it could reasonably be for AI-generated
  code. The accounting-domain specs show genuine understanding of what they're
  testing.

- **The GUID prefix isolation strategy works.** Simple, self-contained,
  scales to ~10,000 tests without collision. The Test Harness Auditor called
  it solid.

- **Seed data is idempotent.** Upsert patterns, runner stops on error, seeds
  tested for double-run safety.

---

## Comparison to Prior Assessment (2026-04-05)

The prior assessment covered 9 feature files and 91 scenarios (Projects
001–018). This assessment covers 43 feature files and ~400+ scenarios.

| Metric | 2026-04-05 | 2026-04-07 |
|---|---|---|
| Feature files | 9 | 43 |
| Scenarios | 91 | ~400+ |
| Ledger Tracer discrepancies | 0 | 0 |
| GAAP principles enforced | 12 | 23 |
| GAAP principles implied | 6 | 7 |
| GAAP principles absent | 4 | 3 |
| Slop rate | 7% | 1.5% |
| Omission Hunter criticals | 3 | 8 |
| Omission Hunter significant | 13 | 18 |

**Resolved from prior assessment:**
- Opening Balances spec — now exists (14 scenarios). Was critical omission.
- SubtreePL spec — now exists (12 scenarios). Was critical omission.
- Double-posting prevention — now tested (FT-POL-016). Was critical omission.
- State machine invalid transitions — now exhaustive (18-example outline). Was critical omission.

**New concerns:**
- Portfolio module gaps (absent from prior assessment, which predated P057–P061)
- Test harness scalability (not assessed previously)
- Codebase-level duplication and consistency (not assessed previously)
- Non-atomic writes and silent error swallowing (code-level, not spec-level)

The accounting engine is materially stronger than it was two days ago. The
new concerns are real but come from expanding the audit scope, not from
regression.

---

*This synthesis was compiled from nine independent audit reports. The
individual reports contain full per-scenario detail and are available in
this directory.*
