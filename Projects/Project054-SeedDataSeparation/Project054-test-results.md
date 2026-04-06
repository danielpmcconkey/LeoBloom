# Project 054 — Test Results

**Date:** 2026-04-06
**Commit:** 75da4da (HEAD of feature/p054-seed-data-separation; changes uncommitted in working tree)
**Result:** 16/16 verified (9 AC + 7 Gherkin)

## Acceptance Criteria Verification

### Behavioral Criteria

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| B1 | `run-seeds.sh dev` executes 010 then 020 in order | Yes | Test FT-SR-007 passes; runner output shows 010 before 020 |
| B2 | Running seeds twice produces identical data, no errors | Yes | Test FT-SR-004 passes; runs twice, counts stable at 36/69, no stderr |
| B3 | Migrations + seeds produce working dev baseline: 36 periods, all accounts with correct subtypes | Yes | Tests FT-SR-001/002/003 pass. **Note:** plan says "68 accounts" but migration 006 contains 69 rows. Seed script, Gherkin spec, and tests all correctly use 69. The plan has a minor count typo. |
| B4 | `run-seeds.sh prod` exits non-zero with directory-not-found message | Yes | Test FT-SR-006 passes; verifies non-zero exit and "not found" in stderr |
| B5 | SQL error stops runner, exits non-zero | Yes | Test FT-SR-005 passes; creates temp dir with bad SQL, confirms non-zero exit and second script not reached |

### Structural Criteria

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| S1 | `Seeds/dev/` exists with two seed scripts | Yes | Directory contains `010-fiscal-periods.sql` and `020-chart-of-accounts.sql` |
| S2 | Seeds cover all baseline data from migrations 005/006 including account_subtype | Yes | 36 fiscal periods match migration 005; 69 accounts match migration 006 (which already includes P052 subtypes) |
| S3 | Chart of accounts inserts in parent-first order | Yes | Verified programmatically: no child row precedes its parent in insertion order |
| S4 | Each seed uses `INSERT ... ON CONFLICT DO UPDATE` | Yes | Both scripts use `ON CONFLICT (period_key/code) DO UPDATE SET` |
| S5 | Each seed wrapped in `BEGIN`/`COMMIT` | Yes | Both scripts start with `BEGIN;` and end with `COMMIT;` |
| S6 | README documents usage, fresh-DB workflow, migration hygiene rule | Yes | Sections present: why seeds exist, usage, prerequisites, fresh DB workflow, migration hygiene decision tree, conventions, prod note |
| S7 | No existing migration files modified or deleted | Yes | `git diff -- Src/LeoBloom.Migrations/Migrations/` shows zero changes |
| S8 | Full test suite passes with zero failures, zero skips | Yes | 615 tests passed, 0 failed, 0 skipped |
| S9 | Seed scripts not in Migrondi scan path | Yes | `Program.fs` line 45: `Path.Combine(AppContext.BaseDirectory, "Migrations")` -- Seeds directory is a sibling, not under Migrations |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-SR-001 | Seeds populate fiscal periods on a fresh database | Yes | Yes |
| @FT-SR-002 | Seeds populate chart of accounts on a fresh database | Yes | Yes |
| @FT-SR-003 | Seeds apply account subtypes from the chart of accounts | Yes | Yes |
| @FT-SR-004 | Running seeds twice produces identical state | Yes | Yes |
| @FT-SR-005 | Runner stops on SQL error with non-zero exit | Yes | Yes |
| @FT-SR-006 | Runner exits non-zero for nonexistent environment directory | Yes | Yes |
| @FT-SR-007 | Seeds execute in numeric filename order | Yes | Yes |

## Notes

1. **Account count discrepancy in plan:** B3 says "68 accounts" but migration 006 contains 69 account rows. The seed script, Gherkin spec, and all tests use 69, which is correct. This is a harmless typo in the plan text only -- the implementation is right.

2. **Working tree state:** All P054 artifacts are in the working tree (untracked/modified), not yet committed. The branch `feature/p054-seed-data-separation` has zero commits beyond `main`. RTE will need to stage and commit everything.

3. **Test FT-SR-005 approach:** Uses a temp directory with a deliberately broken SQL file, copies the runner script there, and verifies error handling. Clean approach -- no mutation of the real seed directory.

4. **Test FT-SR-004 stderr check:** The idempotency test asserts `String.IsNullOrWhiteSpace(stderr2)` on the second run. This is a strong signal -- psql emits nothing to stderr when upserts succeed cleanly.

## Verdict

**APPROVED**

Every acceptance criterion is verified against the actual repo state. All 615 tests pass. All 7 Gherkin scenarios have corresponding tests that exercise the described behavior. No fabrication detected. The evidence chain is solid.
