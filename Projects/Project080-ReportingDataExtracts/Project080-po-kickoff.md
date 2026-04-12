# Project 080 — PO Kickoff

**Date:** 2026-04-12
**Backlog item:** P080 — Reporting Data Extracts
**Verdict:** APPROVED for planning

## Business Intent

Hobson needs structured JSON data feeds from the LeoBloom CLI to generate
PDF financial reports (estate instructions, transaction detail by period).
Four generic extracts — account tree, account balances as-of, portfolio
positions as-of, and JE lines by fiscal period — provide the raw data.
Hobson owns all formatting, hierarchy traversal, and normal balance
adjustment. BD owns the CLI commands and correct data extraction.

This is a **direct unblock** for Hobson's report generation pipeline.
Without these extracts, Hobson cannot produce the net worth or transaction
detail PDFs.

## Spec Assessment

The spec is unusually well-written. Hobson has provided:

- **Exact JSON schemas** for all four extracts with field-level definitions
- **Explicit filtering rules** — particularly the void filtering requirement
  (inner join / WHERE, not LEFT JOIN leak) and the zero-value portfolio
  position exclusion
- **Clear parameter contracts** — `--as-of` for temporal extracts,
  `--fiscal-period-id` for JE lines
- **An explicit "what Hobson does NOT need" section** — preventing scope creep
  into formatting, hierarchy traversal, or normal balance adjustment

### GAAP Considerations

- **Balance representation:** The spec explicitly states balances are raw
  debit-minus-credit, NOT adjusted for normal balance. This is correct for
  a generic extract — normal balance adjustment is a presentation concern
  that belongs in the report layer (Hobson's domain).
- **Void filtering:** GAAP requires voided entries to be excluded from
  financial statements. The spec's insistence on correct void filtering
  (not LEFT JOIN leak) is well-founded.
- No GAAP concerns flagged — the extracts are data feeds, not financial
  statements. GAAP compliance lives in how Hobson consumes them.

## Acceptance Criteria Review

The spec's acceptance criteria are sound:

1. **All four extracts produce valid JSON to stdout** — testable, binary
2. **Void filtering is correct** — testable via SQL inspection and behavioral test
3. **Portfolio positions exclude zero-value entries** — testable
4. **Transaction extract respects fiscal period boundaries** — testable
5. **Works against both prod and dev** — testable (env-controlled connection)

All five criteria directly serve the business intent (Hobson gets correct
data to generate reports). No criteria are missing — the field-level
contracts in the spec body provide the detailed verification targets.

## Guidance for Planning

- CLI subcommand design is BD's call. The spec doesn't prescribe names.
  Follow existing CLI conventions (check how `report` or `extract`
  subcommands are structured in the codebase).
- The `--json` flag pattern is established in the codebase. Use it.
- Four extracts = likely four commands or four subcommands under a parent.
  Planner should survey existing CLI structure before deciding.
- Behavioral Gherkin scenarios should cover: correct output shape, void
  filtering, zero-value exclusion, fiscal period boundary enforcement,
  and `--as-of` date filtering.
- Structural criteria (JSON serialization, command registration) are
  Builder/QE verification territory, not Gherkin.

## Rationale

Straightforward data extraction work with a clear, well-specified consumer.
The spec leaves no ambiguity about what data is needed or how it should be
shaped. The business value is immediate — Hobson is blocked without this.
Approved for planning with no conditions.
