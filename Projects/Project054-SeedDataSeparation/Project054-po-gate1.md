# Project 054 -- Seed Data Separation: Gate 1 (Plan Approval)

**PO Verdict:** APPROVED
**Date:** 2026-04-06

---

## Checklist

- [x] **Objective is clear.** What: extract dev baseline data from migration chain into idempotent seed scripts. Why: stop the "works in dev, breaks prod" bug class. No ambiguity.
- [x] **Deliverables are concrete and enumerable.** Four files, all named with paths. No vague "refactor things."
- [x] **Acceptance criteria are testable.** Every B-criterion is binary pass/fail. Every S-criterion is verifiable by inspection or tooling.
- [x] **Behavioral vs. structural criteria are distinguished.** B1-B5 are Gherkin-eligible behaviors. S1-S9 are structural checks for QE/Governor. Clean split.
- [x] **Phases are ordered logically.** Directory and scripts first, then runner, then docs. Each phase has verification. Makes sense.
- [x] **Out of scope is explicit.** Matches my kickoff exactly and adds one good entry: "Adding a seed subcommand to the .NET migration binary." That preempts a likely scope creep vector.
- [x] **Dependencies are accurate.** P053 is done. No stale refs.
- [x] **No duplication of prior work.** This is new infrastructure.
- [x] **Consistent with kickoff.** See notes below.
- [x] **Key decisions from vision doc carried forward.** Upsert pattern chosen (over delete+insert), shell script chosen (over .NET subcommand), plain SQL, numeric prefix gaps of 10. All aligned with vision doc guidance.

---

## Notes

**Script ordering vs. vision doc.** The vision doc suggests `010-chart-of-accounts.sql, 020-fiscal-periods.sql`. The plan flips this to `010-fiscal-periods.sql, 020-chart-of-accounts.sql`. There's no functional dependency between the two -- neither table FKs into the other. This is a cosmetic difference and I don't care which order they land in. Not blocking.

**B5 is a good addition.** My kickoff had four behavioral criteria. The plan added B5 (runner stops on SQL error). That's a real behavior I'd want verified. Good catch by the Planner.

**S9 is a good addition.** Explicitly verifying that seed scripts are outside Migrondi's scan path. This was a risk in my kickoff (Risk #3) and the plan promoted it to a structural acceptance criterion. Correct call.

**S5 from kickoff ("no new migrations contain environment-specific INSERT/UPDATE") was dropped.** The plan's S7 covers "no existing migration files are modified or deleted" but that's different from my S5 which was about new migrations going forward. However, this is a process rule, not something you can test in a single project. It belongs in the README's migration hygiene section, not in acceptance criteria. I'm fine with this being covered by S6 (README documents the migration hygiene rule) rather than a standalone criterion.

**S3 from kickoff ("seed scripts include all columns as they exist in the current schema including account_subtype") was absorbed into S2.** The plan's S2 explicitly calls out account_subtype values. Covered.

**Research notes are solid.** The Planner verified that migrations 011-014 are dead (tables eliminated by 019), that obligation_agreement has no seed data, that Migrondi's scan path doesn't include Seeds/, and that migration 006 already has the consolidated state from P052. This is the kind of due diligence that prevents mid-build surprises.

**68 accounts claim in B3.** The Planner asserts "all 68 accounts" as the expected count. This will get verified at build time. If the count is wrong, it'll show up in the Gherkin scenario. Not blocking, but the Builder should double-count against the actual migration 006 data.

---

## Decision

The plan is tight, well-researched, and faithfully implements the vision doc and my kickoff. Acceptance criteria are complete and properly categorized. Risks are identified with mitigations. Out of scope is clear.

**APPROVED. Proceed to Gherkin writing and build.**
