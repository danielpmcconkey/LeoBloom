# LeoBloom Project Conventions — All Agents

Every DSWF addendum in this directory imports these conventions. If your
role-specific addendum doesn't exist yet, read this file instead.

## Naming — Non-Negotiable

**Project directories:** `Projects/ProjectNNN-ShortName/`
- Three-digit zero-padded number
- PascalCase short name
- Examples: `Project054-SeedDataSeparation`, `Project077-AccountCreateCli`

**Artifact files:** `ProjectNNN-{artifact}.md`
- The prefix matches the directory number
- Examples: `Project054-plan.md`, `Project054-test-results.md`
- Never: `P054-plan.md`, `p054_plan.md`, or any other variation

**Feature branches:** `feat/pNNN-short-description` (lowercase kebab-case)
- Example: `feat/p054-seed-data-separation`

**Gherkin specs:** `Specs/{Category}/{FeatureName}.feature`

**Test files:** `Src/LeoBloom.Tests/{FeatureName}Tests.fs`

## Artifact Inventory

Each completed project directory should contain:

| Artifact | Produced by | Required? |
|----------|-------------|-----------|
| `ProjectNNN-plan.md` | Planner | Yes |
| `ProjectNNN-brainstorm.md` | BA | Only if brainstorm was needed |
| `ProjectNNN-plan-review.md` | PO (Gate 1) | Yes |
| `ProjectNNN-test-results.md` | Governor | Yes |
| `ProjectNNN-signoff.md` | PO (Gate 2) | Yes |

The Reviewer and QE do not produce project-directory artifacts. The Reviewer
reports findings to BD. The QE produces test code and a test-results artifact
that the Governor consumes.

## Stack

- F# / .NET 10
- PostgreSQL (NpgsqlDataSource)
- xUnit test framework
- No mocking — all tests hit the real database
- CLI consumption layer (no REST API)

## GAAP Context

LeoBloom targets AccPac/QuickBooks-level accounting correctness. Design
decisions are evaluated against GAAP first, convenience second. The accounting
equation (A = L + E) is a system invariant. Hobson (Dan's host-side Claude)
is the comptroller entering real financial data against production.

## Backlog

- Index: `Projects/BACKLOG/index.md`
- Active specs: `Projects/BACKLOG/`
- Completed/cancelled specs: `Projects/BACKLOG/Done/`
