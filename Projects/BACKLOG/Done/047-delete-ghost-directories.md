# 047 — Delete Ghost Directories

**Epic:** K — Code Audit Remediation
**Depends On:** None
**Status:** Not started

---

Remove empty project directories from Src/ that contain only bin/obj
artifacts and no source code:

- LeoBloom.Data
- LeoBloom.Ledger.Tests
- LeoBloom.Ops.Tests

All tests live in LeoBloom.Tests. The separate test project directories
were scaffolded but never used.

**Scope:**

1. Remove solution file entries for these projects (if any).
2. Delete the three directories.
3. Verify solution builds cleanly.

**Note:** LeoBloom.Ledger and LeoBloom.Ops are NOT ghost directories —
they are targets for the domain reorg (P045). Only delete the three listed
above.

**Source:** Code audit SYNTHESIS.md, Tier 2 Finding #6
