# Project 080 — Delivery Sign-off

**Date:** 2026-04-12
**Backlog item:** P080 — Reporting Data Extracts
**Commit:** 6b37845ae29a3c1f6431ba9b39af35ab4cceaee0
**Verdict:** APPROVED

## Business Outcome

Hobson needed four generic JSON data extracts to unblock his report generation
pipeline (estate instructions PDF, transaction detail PDF, and future reports).
All four extracts are delivered and working:

1. **Account tree** — full flat array with hierarchy pointers, metadata, and
   normal balance. Hobson builds the tree on his side.
2. **Account balances as-of** — raw debit-minus-credit with void exclusion.
   No normal balance adjustment (Hobson applies that).
3. **Portfolio positions as-of** — latest snapshot per (account, symbol) with
   zero-value exclusion. Hobson gets current holdings only.
4. **JE lines by fiscal period** — line-level detail, non-voided, ordered by
   account/date/id. Hobson groups and formats.

The business need — "give Hobson structured data feeds so he can generate
reports" — is fully met. The extracts are generic (not report-specific),
which means future reports can reuse them without code changes. The JSON
field contract uses snake_case as specified, so Hobson's scripts can parse
them directly.

## Evidence Summary

**Governor (independent verification):** 12/12 acceptance criteria verified
(7 behavioral, 5 structural). All 43 Gherkin scenarios mapped to passing
tests. Fabrication check clean — all cited files exist, code matches claims,
commit hash matches HEAD. Zero LEFT JOINs confirmed via grep.

**Reviewer:** APPROVED with no defects. Independently confirmed 1121/1121
tests pass. Verified spec fidelity across all four extracts including
snake_case field names, void filtering pattern, raw balance computation,
DISTINCT ON + subquery for positions, and ordering. Confirmed Builder's
getPositions SQL deviation is a correctness fix (plan's SQL was wrong —
would have filtered before latest-snapshot selection).

**QE:** 1121/1121 tests pass (0 failed, 0 skipped). 45 new tests added
across ExtractRepositoryTests.fs (behavioral) and ExtractCommandsTests.fs
(CLI). Full Gherkin coverage including the load-bearing FT-EXT-124 edge case
(latest snapshot with zero value correctly excludes even when earlier snapshot
had value).

**Builder's getPositions deviation:** The plan's SQL put `current_value <> 0`
alongside `DISTINCT ON` in a flat query, which would filter rows *before*
latest-snapshot selection. Builder correctly wrapped the `DISTINCT ON` in a
subquery and applied the zero-value filter on the outer query. This is what
the plan's own risk section described as the intended behavior. FT-EXT-124
validates it. Reviewer and Governor both independently confirmed this is
correct.

## Rationale

The backlog item's intent was clear and narrow: four JSON data extracts to
unblock Hobson's report pipeline. The delivered work matches that intent
exactly — four extracts, correct data contracts, correct filtering rules,
no scope creep. The critical business rules (void exclusion via INNER JOIN,
raw debit-minus-credit balance, zero-value position exclusion, fiscal period
scoping) are all implemented and tested. The evidence chain from Governor,
Reviewer, and QE is consistent and independently verified. No gaps between
what was promised and what was delivered.
