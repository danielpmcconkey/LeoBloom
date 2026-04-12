# PO Addendum — LeoBloom

## Naming Conventions

**Project directories:** `Projects/ProjectNNN-ShortName/` — three-digit
zero-padded number, PascalCase short name. Examples:
- `Projects/Project054-SeedDataSeparation/`
- `Projects/Project073-ConnectionInjection/`

**NOT:** `P054-SeedDataSeparation`, `project054`, `Project54`, or any other
variation. The `ProjectNNN-` prefix is non-negotiable.

**Artifact files inside the project directory:**
- Plan review: `ProjectNNN-plan-review.md`
- Delivery sign-off: `ProjectNNN-signoff.md`
- Plan: `ProjectNNN-plan.md`
- Brainstorm: `ProjectNNN-brainstorm.md`
- Test results (Governor writes this): `ProjectNNN-test-results.md`

All artifact filenames use the `ProjectNNN-` prefix matching the directory.

## Backlog

- Backlog index: `Projects/BACKLOG/index.md`
- Active spec files: `Projects/BACKLOG/`
- Completed/cancelled spec files: `Projects/BACKLOG/Done/`
- When marking a backlog item Done, update the index AND move the spec file
  to `Done/` if it hasn't been moved already.

## GAAP Context

LeoBloom is a double-entry bookkeeping system targeting AccPac/QuickBooks-level
accounting correctness. When evaluating business intent at either gate, consider
whether the plan or delivery aligns with GAAP principles. If you're unsure
whether a business requirement has GAAP implications, flag it for escalation
rather than approving blindly.

Examples of GAAP-relevant business intent:
- Zero-balance accounts with activity must still appear in reports
- Revenue/expense sign conventions follow normal balance rules
- The accounting equation (A = L + E) is a system invariant
- Retained earnings is computed, not stored as a GL balance

## Escalation

In addition to the generic PO escalation triggers, escalate when:
- A plan touches the accounting equation or double-entry invariants
- A backlog item's intent is ambiguous about GAAP treatment
- The plan involves data migration or schema changes to production
