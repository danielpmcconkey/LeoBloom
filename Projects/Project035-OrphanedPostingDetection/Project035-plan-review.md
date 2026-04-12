# Project 035 — Plan Review

**Date:** 2026-04-12
**Backlog item:** P035 — Orphaned Posting Detection
**Verdict:** APPROVED

## Business Intent

Pre-P043 posting failures could leave the ledger and ops tables out of sync
— journal entries posted but source records never updated, or source records
pointing to voided journal entries. This is a data integrity forensic tool:
find the damage, report it to Dan, let him decide what to do. No mutations,
no automation, just visibility into a known historical risk.

## Acceptance Criteria Assessment

**Behavioral criteria (AC-B1 through AC-B9):** All nine map directly to the
three detection conditions described in the spec, plus a clean-state baseline
(B1), a false-positive guard (B8), and output format verification (B9). Each
criterion, if green, proves one facet of the diagnostic's correctness. No
gaps — if all nine pass, the diagnostic catches what it should catch and
ignores what it should ignore.

**Structural criteria (AC-S1 through AC-S4):**
- S1 (new command group) ensures the CLI wiring exists.
- S2 (read-only) is the safety invariant — this thing touches nothing.
- S3 (no migrations) confirms we're leveraging existing schema.
- S4 (existing tests pass) is the regression guard.

All structural criteria serve the business intent. Nothing missing.

**InvalidReference bonus:** The plan adds a fourth detection condition
(`InvalidReference`) for non-numeric `reference_value` entries. This isn't
in the spec, but it falls out naturally from the varchar-to-integer casting
safety requirement the spec itself calls out in Risk Notes. It costs zero
additional complexity and surfaces a real anomaly. Approved as-is — it's
defensive, not scope creep.

## Rationale

The plan faithfully translates the spec's three detection conditions into two
queries (reference-to-source and source-to-journal-entry), which matches the
spec's own design notes about the two lookup directions. The five-phase
breakdown (types, repo, service, CLI, tests) follows existing project patterns.
The varchar casting risk is handled correctly with regex filtering. Exit codes
match spec. Output formats match spec. Scope boundaries are respected — no
remediation, no scheduling, no migrations.

The plan solves exactly the problem the backlog item describes: give Dan
visibility into orphaned postings so he can decide what to do about them.
