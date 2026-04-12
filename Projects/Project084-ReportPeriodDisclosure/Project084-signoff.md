# Project 084 — Delivery Sign-off

**Date:** 2026-04-12
**Backlog item:** P084 — Report Period Disclosure & As-Originally-Closed Mode
**Commit:** uncommitted on feat/p084-report-period-disclosure (base: e7e153c)
**Verdict:** APPROVED

## Business Outcome

The business need is met. P081-P083 built the fiscal period closure machinery
(close metadata, pre-close validation, enforcement, adjustments). P084 makes
all of that visible to anyone consuming a report. A report consumer can now
see when a period was closed, by whom, how many times it was reopened, what
adjustments were applied after close, and can reproduce the period as it
stood at close time. This directly satisfies the GAAP transparency requirement
for period disclosure that motivated Epic K.

Specifically:
- All four period-based reports (income statement, trial balance, P&L subtree,
  balance sheet with `--period`) now show provenance headers with period
  metadata and adjustment summaries.
- Adjustment footers list individual post-close JEs with ID, date,
  description, and net amount.
- `--as-originally-closed` reproduces the period as of its close timestamp,
  supporting audit reproducibility.
- `je-lines` extracts include adjustment JEs by default and wrap period
  targeting in a metadata envelope.
- Open periods show "OPEN" status. Closed periods show close timestamp,
  actor, and reopen count.

No business requirement from the spec was dropped or weakened.

## Evidence Summary

**Governor (test-results):** 17/17 acceptance criteria verified — 10
behavioral, 7 structural. All 21 Gherkin scenarios (FT-RPD-001 through
FT-RPD-021) have corresponding tests and pass. Governor performed independent
file-level verification of all Builder claims. No fabrication detected.

**Reviewer:** APPROVED after one conditional fix. The conditional was a real
bug — `--exclude-adjustments` was over-excluding JEs that belonged to the
target period but happened to adjust other periods. Builder applied the fix
(removed `AND je.adjustment_for_period_id IS NULL` from the exclude branch).
Reviewer re-verified and approved. Non-blocking observations: balanced JEs
produce zero net impact (expected), PeriodMetadataEnvelope is a breaking
change for existing period-targeting consumers (acknowledged in plan risks).

**QE:** 45 new tests in `ReportPeriodDisclosureTests.fs`. Full suite
1224/1224 pass, 0 failures, 0 skips. Fiscal years 2103-2104 reserved for
test isolation.

**Gherkin coverage:** All 10 behavioral AC mapped to scenarios. Scenario
Outlines used for the 4-report pattern (FT-RPD-001, -003, -011, -014, -019),
avoiding 20 redundant individual scenarios while still exercising each report
command. Edge cases beyond the 10 AC also covered (no-adjustment footer,
balance sheet without --period, reopened period headers, etc.).

## Rationale

The spec asked for period provenance headers, adjustment disclosure footers,
`--as-originally-closed` historical reproducibility, and extract enhancements.
All four are delivered and independently verified. The reviewer caught one
real bug in the extract exclude logic — it was fixed and re-verified before
reaching this gate. The GAAP transparency requirement that motivated this
work is satisfied: closed periods disclose their adjustments, and auditors
can reproduce the period as it stood at close time.

This completes Epic K (Fiscal Period Closure: P081-P084).
