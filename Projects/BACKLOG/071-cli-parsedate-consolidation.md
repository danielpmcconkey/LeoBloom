# 071 — Consolidate CLI parseDate and Fix TransferCommands

**Epic:** Audit Remediation (2026-04-07)
**Depends On:** None
**Status:** Not started
**Priority:** Low
**Source:** DRY Auditor Finding 2, Consistency Auditor C2

---

## Problem Statement

The `parseDate` function is copy-pasted identically across 7 CLI modules.
Additionally, `TransferCommands.fs` uses `DateOnly.TryParse` (lenient)
while all others use `DateOnly.TryParseExact("yyyy-MM-dd")` (strict). The
error message in TransferCommands claims strict format, which is misleading.

## What Ships

1. Extract a shared `parseDate` function to a CLI helpers module (new file
   or added to an existing shared module in the CLI project)
2. Fix `TransferCommands.fs` to use the strict `TryParseExact` parser
3. Remove all 7 private `parseDate` / `parseDateOnly` definitions from
   individual command modules
4. Also extract `parsePeriodArg` (duplicated in PeriodCommands and
   ReportCommands) to the same shared module

## Acceptance Criteria

- AC-1: One `parseDate` definition, used by all CLI command modules
- AC-2: All date parsing uses `TryParseExact("yyyy-MM-dd")`
- AC-3: `parsePeriodArg` is defined once and shared
- AC-4: All existing tests pass with zero changes
