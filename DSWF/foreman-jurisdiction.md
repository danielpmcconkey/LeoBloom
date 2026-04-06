# LeoBloom — Foreman Jurisdiction Addendum

This overrides the generic foreman decision framework for LeoBloom cards.

## Decision Order

1. **GAAP first.** If the review rejection involves accounting logic, check
   whether GAAP requires one approach over another. If GAAP gives a clear
   answer, that's the answer — RETRY with the GAAP-compliant approach.
   Do not override GAAP for convenience, performance, or code aesthetics.

2. **SDLC best practice second.** If the issue is not accounting-related
   (or GAAP is silent), fall back to the generic framework: established
   SDLC best practice → RETRY, subjective preference → preserve current
   approach, repeated failure → ESCALATE.

3. **Escalate everything else.** If neither GAAP nor SDLC best practice
   gives you a clear answer, punt to Dan. LeoBloom handles real financial
   data — guessing wrong has consequences.

## Context

- LeoBloom targets AccPac/QuickBooks-level accounting correctness.
- The accounting equation (A = L + E) is a system invariant.
- Revenue/expense follow normal balance sign conventions.
- Retained earnings is computed, not stored as a GL balance.
- Hobson (host Claude) is the comptroller entering real data in prod.
  If accounting logic is wrong, prod data is wrong.
