# 043 — Idempotency Guards for Posting Services

**Epic:** K — Code Audit Remediation
**Depends On:** 018, 019
**Status:** Not started

---

Add idempotency checks to `ObligationPostingService.postToLedger` and
`TransferService.confirm`. Before posting a journal entry, each service
checks for an existing journal entry matching the reference_type and
reference_value. If a match is found, the service skips the post and
proceeds directly to the status transition.

This is the #1 finding from the 2026-04-05 code audit. Both services
execute three-phase workflows across separate database transactions. If
phase 2 (post journal entry) succeeds but phase 3 (update status) fails,
a retry without idempotency creates a duplicate journal entry — an orphaned
financial record in a system whose entire purpose is data integrity.

**Scope:**

1. In `ObligationPostingService.postToLedger`: before calling
   `JournalEntryService.post`, query for an existing journal entry with
   matching reference_type/reference_value. If found, use that entry's ID
   for the status transition instead of posting a new one.
2. Same pattern in `TransferService.confirm`.
3. ~10-15 lines per service. No architectural changes (no transaction
   refactoring, no FOR UPDATE locks).

**What this is NOT:**

- Not a single-transaction refactor (option (a) from the audit). That's a
  larger structural change. Idempotency is the minimum viable fix.
- Not a TOCTOU fix. The race window between validation and posting remains.
  At current scale (single CLI caller), this is acceptable.
- Not a retry framework. The guard makes retries safe, but automatic retry
  is out of scope.

**Files:** `ObligationPostingService.fs`, `TransferService.fs`

**Source:** Code audit SYNTHESIS.md, Tier 1 Finding #1
