# 070 — Missing Portfolio CLI Commands

**Epic:** Audit Remediation (2026-04-07)
**Depends On:** 059 (CLI portfolio commands)
**Status:** Not started
**Priority:** Medium
**Source:** Domain Invariant Auditor C-002/003/004/011

---

## Problem Statement

The P059 backlog specifies several CLI commands that have no Gherkin spec
coverage and may not be implemented:

1. **`portfolio account show <id>`** (AC-B3) — displays account detail with
   latest position summary. No scenarios exist.
2. **`portfolio account list --group <name>`** (AC-B2) — filters accounts
   by group. No scenarios exist.
3. **`portfolio account list --tax-bucket <name>`** (AC-B2) — filters
   accounts by tax bucket. No scenarios exist.
4. **`portfolio dimensions [--json]`** (AC-B12) — lists all 6 dimension
   tables and their values. No scenarios exist.

## What Ships

For each missing command:

1. Gherkin scenarios (happy path, error paths, `--json` variant)
2. Test implementations
3. CLI implementation if not already present
4. OutputFormatter support if not already present

## Acceptance Criteria

- AC-1: `portfolio account show <id>` works and has specs
- AC-2: `portfolio account list --group <name>` filters correctly
- AC-3: `portfolio account list --tax-bucket <name>` filters correctly
- AC-4: `portfolio dimensions` lists all 6 dimension tables with values
- AC-5: All four commands support `--json` output
- AC-6: Error cases (nonexistent ID, invalid filter) are tested
