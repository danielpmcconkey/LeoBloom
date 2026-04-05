# 049 — Consolidate Helper Duplication

**Epic:** K — Code Audit Remediation
**Depends On:** None (but sequence after 045 if both are in flight)
**Status:** Not started

---

Extract duplicated helper functions into shared locations. The audit found
multiple copies of the same functions across the codebase, diverging slowly.

**Scope:**

1. **optParam** — Move to `DataHelpers.fs` in LeoBloom.Utilities. Remove
   the 4 copies scattered across repository files. All repositories already
   reference Utilities.
2. **repoRoot** — Consolidate the 4 identical copies into `TestHelpers.fs`.
   Rewrite using `[<CallerFilePath>]` attribute instead of the fragile
   walkUp-from-Assembly.Location approach.
3. **Constraint test helpers** — Consolidate duplicated helpers between
   LedgerConstraintTests and OpsConstraintTests into `TestHelpers.fs`. Use
   the LedgerConstraintTests version (it's the better implementation).

**What this is NOT:**

- Not consolidating `buildSection` (3 copies in reporting services). That's
  a reporting-internal concern and can wait for a reporting refactor.
- Not consolidating `lookupAccount` variants (4 services each roll their
  own). Those have subtly different query needs; premature extraction would
  add coupling.

**Source:** Code audit SYNTHESIS.md, Tier 2 Finding #7
