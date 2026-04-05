# LeoBloom — Dev Workflow

## Pipeline

```
PO → Brainstorm → Plan/Deepen → Gherkin Writer → Builder → [reread] → QE → Reviewer → Governor → PO signoff → RTE → Stop (await Dan)
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
9. **BD re-reads this file.** Context checkpoint — Builder output is the
   heaviest payload in the pipeline and compresses everything before it.
   Re-reading refreshes the agent spawning protocol for the back half.
10. **QE** writes test implementations against the Gherkin specs and Builder's code
11. **Reviewer** adversarial code review
12. Dan / BD take reviewer findings case-by-case
13. **Governor** writes test results: `Projects/ProjectNNN-Name/ProjectNNN-test-results.md`
14. **PO** signs off, marks backlog item complete
15. **RTE** commits, pushes, creates PR, merges to main
16. **Stop.** Report results to Dan and wait for his instruction before
    starting the next project. Dan evaluates context health between projects.

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

## Agent Spawning Protocol

Every pipeline agent is spawned via the Agent tool with two layers:

1. **Generic blueprint** — the `subagent_type` parameter. This is the agent's
   core identity and toolset. Never omit it.
2. **DSWF addendum** — if `DSWF/{role}.md` exists, the agent's prompt MUST
   include an instruction to read it before starting work. This is where
   LeoBloom-specific conventions live (test patterns, file paths, domain
   knowledge).

### Subagent types for each pipeline role

| Pipeline Step | `subagent_type` | DSWF file (if exists) |
|---|---|---|
| Product Owner | `po` | `DSWF/po.md` |
| Brainstorm Analyst | `ba` | `DSWF/ba.md` |
| Planner | `planner` | `DSWF/planner.md` |
| Gherkin Writer | `gherkin-writer` | `DSWF/gherkin-writer.md` |
| Builder | `builder` | `DSWF/builder.md` |
| Quality Engineer | `qe` | `DSWF/qe.md` |
| Reviewer | `reviewer` | `DSWF/reviewer.md` |
| Governor | `governor` | `DSWF/governor.md` |
| Release Train Engineer | `rte` | `DSWF/rte.md` |

### Prompt template

When spawning any pipeline agent, the prompt must include:

```
Read BdsNotes/workflow.md for the full pipeline context.
If DSWF/{role}.md exists, read it before starting — it contains
LeoBloom-specific conventions for your role.
```

Not every role has a DSWF file yet. That's fine — the agent reads the
generic blueprint and workflow.md and does its job. When a DSWF file
is added later, the prompt template already tells the agent to look for it.

## Artifacts

All non-code artifacts live in `Projects/ProjectNNN-Name/`:
- `ProjectNNN-brainstorm.md` (if brainstorm was needed)
- `ProjectNNN-plan.md`
- `ProjectNNN-test-results.md`

Gherkin specs live in `Specs/{Category}/`.
Test code lives in `Src/LeoBloom.Tests/`.
