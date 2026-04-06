# 051 — Move IncludeErrorDetail to appsettings

**Epic:** K — Code Audit Remediation
**Depends On:** None
**Status:** Not started

---

Remove the hardcoded `builder.ConnectionStringBuilder.IncludeErrorDetail <- true`
from DataSource.fs. Move the setting into the connection string in
appsettings.json files instead, where it can differ per environment.

**Scope:**

1. Remove `IncludeErrorDetail <- true` from DataSource.fs.
2. Add `Include Error Detail=true` to the connection string in
   appsettings.Development.json (and test appsettings if separate).
3. Do NOT add it to any production appsettings — detailed errors should not
   leak in production.
4. Verify existing tests still pass.

**Files:** `DataSource.fs`, `appsettings*.json`

**Source:** Code audit SYNTHESIS.md, Tier 3
