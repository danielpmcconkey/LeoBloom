# Project 021 -- PO Gate 1: Plan Approval

**Verdict: APPROVED**

**Date:** 2026-04-06
**Reviewed by:** PO Agent
**Artifact:** Project021-plan.md
**Cross-referenced:** Project021-po-brief.md, Project021-brainstorm.md, 021-generate-invoice.md (backlog item), 1712000018000_CreateInvoice.sql (schema)

---

## Checklist

- [x] Objective is clear -- what and why, no ambiguity
- [x] Deliverables are concrete and enumerable
- [x] Acceptance criteria are testable (binary yes/no)
- [x] Acceptance criteria distinguish behavioral (B1-B14) from structural (S1-S7)
- [x] Phases are ordered logically with verification at each step
- [x] Out of scope is explicit
- [x] Dependencies are accurate
- [x] No deliverable duplicates work already done in a prior project
- [x] Consistent with the backlog item that triggered it
- [x] Key brainstorm decisions are carried forward

---

## Assessment

Solid plan. The brainstorm raised three questions the PO brief flagged (closed-period recording, generated_at semantics, decimal precision handling), and all three have clear, correct resolutions that carried forward into the plan.

Specific things I verified:

1. **Schema alignment.** The `RecordInvoiceCommand` fields map cleanly to the `ops.invoice` DDL. The `generatedAt` field is required in the command (brainstorm decision: caller-provided, not DB default). The domain `Invoice` type already exists and matches the table. No gaps.

2. **Behavioral criteria coverage.** B1-B14 cover every behavior described in the PO brief's acceptance criteria table. The mapping is one-to-one. The Matthew case (zero rent, B4) and the companion zero-utility case (B5) are both present. Nonexistent fiscal period (B14) is explicitly called out, as is the decision NOT to check `is_open`.

3. **Pattern consistency.** The plan correctly identifies TransferService/TransferRepository as the pattern to follow and calls out specific deviations where appropriate (e.g., `ListInvoicesFilter` lives in the repository file, not Domain, matching `ListAgreementsFilter` in `ObligationAgreementRepository.fs`). I confirmed this pattern in the codebase.

4. **TestHelpers cleanup ordering.** The plan correctly identifies that invoice cleanup must precede fiscal_period cleanup due to FK constraints. The existing `tryDelete "ops.invoice" "fiscal_period_id"` line at TestHelpers.fs:101 handles cleanup for other tests that create fiscal periods; the new `tryDelete "ops.invoice" "id"` line is additive. Insertion point is correct.

5. **Risks are real and mitigated.** The decimal precision edge case (Risk 1), F# compile ordering (Risk 2), cleanup ordering (Risk 3), and race condition handling (Risk 4) are all legitimate. The mitigations are appropriate.

6. **Out of scope matches PO brief and backlog item.** No scope creep. No missing exclusions.

No issues found. Proceed to Gherkin writing and build.
