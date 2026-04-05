# 050 — Use EntryType.toDbString

**Epic:** K — Code Audit Remediation
**Depends On:** None
**Status:** Not started

---

One-line fix in `JournalEntryRepository.insertLines` to use the existing
`EntryType.toDbString` domain function instead of an inline match
expression that duplicates the same logic.

**Scope:**

1. Replace the inline match on entry type in `insertLines` with a call to
   `EntryType.toDbString`.
2. Verify existing tests still pass.

**Files:** `JournalEntryRepository.fs`

**Source:** Code audit SYNTHESIS.md, Tier 3
