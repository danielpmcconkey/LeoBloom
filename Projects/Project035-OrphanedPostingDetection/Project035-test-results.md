# Project035 — Test Results

**Date:** 2026-04-12
**Commit:** 25b9e0e
**Result:** 13/13 verified

## Acceptance Criteria Verification

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| AC-B1 | No orphans returns clean result | Yes | Test `@FT-OPD-001` exists (line 112), asserts `Assert.Empty(result.orphans)` |
| AC-B2 | Detects dangling obligation status | Yes | Test `@FT-OPD-002` (line 127) sets up NULL `journal_entry_id` obligation + JE reference, asserts `DanglingStatus` condition |
| AC-B3 | Detects dangling transfer status | Yes | Test `@FT-OPD-003` (line 151) sets up NULL `journal_entry_id` transfer + JE reference, asserts `DanglingStatus` condition |
| AC-B4 | Detects missing obligation source | Yes | Test `@FT-OPD-004` (line 179) uses nonexistent ID `9999999`, asserts `MissingSource` condition |
| AC-B5 | Detects missing transfer source | Yes | Test `@FT-OPD-005` (line 200) uses nonexistent ID `9999999`, asserts `MissingSource` condition |
| AC-B6 | Detects posted obligation with voided JE | Yes | Test `@FT-OPD-006` (line 225) creates voided JE + posted obligation, asserts `VoidedBackingEntry` |
| AC-B7 | Detects confirmed transfer with voided JE | Yes | Test `@FT-OPD-007` (line 248) creates voided JE + confirmed transfer, asserts `VoidedBackingEntry` |
| AC-B8 | Normal postings not flagged | Yes | Test `@FT-OPD-008` (line 275) creates properly completed posting, asserts `Assert.Empty(mine)` |
| AC-B9 | JSON output mode | Yes | Test `@FT-OPD-009` (line 328) serializes result, parses JSON, verifies `orphans` array and `DanglingStatus` condition |
| AC-S1 | `Diagnostic` case in LeoBloomArgs | Yes | `Program.fs` line 28: `Diagnostic of ParseResults<DiagnosticArgs>`, routes to `DiagnosticCommands.dispatch` at line 75 |
| AC-S2 | Repository query is SELECT-only | Yes | `OrphanedPostingRepository.fs` contains only SELECT statements; grep for INSERT/UPDATE/DELETE returns zero matches |
| AC-S3 | No new migrations | Yes | No migration files reference OrphanedPosting; latest migration is P079's `AddIrregularCadence` |
| AC-S4 | Existing tests still pass | Yes | QE artifact: 1121 passed, 0 failed, 0 skipped at commit 25b9e0e |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-OPD-001 | Clean empty result | Yes (line 112) | Yes |
| @FT-OPD-002 | Dangling obligation status | Yes (line 127) | Yes |
| @FT-OPD-003 | Dangling transfer status | Yes (line 151) | Yes |
| @FT-OPD-004 | Missing obligation source | Yes (line 179) | Yes |
| @FT-OPD-005 | Missing transfer source | Yes (line 200) | Yes |
| @FT-OPD-006 | Posted obligation + voided JE | Yes (line 225) | Yes |
| @FT-OPD-007 | Confirmed transfer + voided JE | Yes (line 248) | Yes |
| @FT-OPD-008 | False positive guard | Yes (line 275) | Yes |
| @FT-OPD-009 | JSON output mode | Yes (line 328) | Yes |
| @FT-OPD-010 | InvalidReference (non-numeric ref) | Yes (line 302) | Yes |

## Fabrication Check

- All cited files exist and contain the claimed code
- QE test-results commit hash (25b9e0e) matches current HEAD
- QE reports 1121/1121 passing — no omitted failures
- No circular evidence: each criterion verified against actual file contents and grep results
- Trait tags in test file match Gherkin scenario IDs 1:1

## Verdict

**APPROVED** — all 13 acceptance criteria verified, all 10 Gherkin scenarios covered with passing tests, evidence chain is solid.
