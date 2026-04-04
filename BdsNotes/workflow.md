# LeoBloom — Dev Workflow

## Pipeline

```
PO → Brainstorm → Plan/Deepen → Gherkin Writer → Builder → QE → Reviewer → Governor → PO signoff → RTE
```

## Step-by-Step

1. **PO** picks the next backlog item from `Projects/BACKLOG.md`
2. **PO** creates the project dir: `Projects/ProjectNNN-Name/`
3. **RTE** creates the feature branch
4. **BA (Brainstorm)** explores requirements if they're ambiguous. Writes
   `Projects/ProjectNNN-Name/ProjectNNN-brainstorm.md`. Skip if the backlog
   item is unambiguous.
5. **Planner** creates the implementation plan:
   `Projects/ProjectNNN-Name/ProjectNNN-plan.md`. Deepen if warranted.
6. **PO** approves the plan
7. **Gherkin Writer** writes behavioral specs to `Specs/{Category}/`
8. **Builder** implements the plan. Runs existing tests to verify nothing breaks.
9. **QE** writes test implementations against the Gherkin specs and Builder's code
10. **Reviewer** adversarial code review
11. Dan / BD take reviewer findings case-by-case
12. **Governor** writes test results: `Projects/ProjectNNN-Name/ProjectNNN-test-results.md`
13. **PO** signs off, marks backlog item complete
14. **RTE** commits, pushes, creates PR, merges to main

## GAAP Compliance

LeoBloom targets AccPac/QuickBooks-level accounting correctness. Every
design decision — data model, report logic, edge case handling — should
be evaluated against GAAP first, convenience second. If BD isn't sure
what GAAP says, raise it with Dan before making assumptions.

Examples of where this matters:
- Zero-balance accounts with activity must display (they represent real
  assets/liabilities)
- Retained earnings is cumulative net income, not a GL account balance
- Revenue/expense sign conventions follow normal balance rules
- The accounting equation must always hold as a system invariant
- Closing entries vs. computed retained earnings (we chose computed, but
  GAAP informed the decision)

This isn't an academic exercise. Hobson is the comptroller entering real
financial data upstairs. If we get the accounting wrong, the prod data
is wrong.

## Migrations

If migrations are needed:
- Builder runs them in dev during the build phase
- Migration Prod Executor (Hobson) confirms safety and runs in prod

## DSWF

All agents read `DSWF/{role}.md` before starting work on this project.
That's where LeoBloom-specific conventions live (test patterns, file paths,
domain knowledge).

## Artifacts

All non-code artifacts live in `Projects/ProjectNNN-Name/`:
- `ProjectNNN-brainstorm.md` (if brainstorm was needed)
- `ProjectNNN-plan.md`
- `ProjectNNN-test-results.md`

Gherkin specs live in `Specs/{Category}/`.
Test code lives in `Src/LeoBloom.Tests/`.
