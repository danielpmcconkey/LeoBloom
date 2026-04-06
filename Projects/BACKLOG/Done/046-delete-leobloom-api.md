# 046 — Delete LeoBloom.Api

**Epic:** K — Code Audit Remediation
**Depends On:** None
**Status:** Not started

---

Remove the LeoBloom.Api project from the solution. This is the default
ASP.NET WeatherForecast template from the cancelled API direction (P023
through P027). The CLI consumption layer (P036-P042) replaced it.

**Scope:**

1. Remove LeoBloom.Api entry from the solution file.
2. Delete the Src/LeoBloom.Api directory.
3. Remove any ProjectReference to LeoBloom.Api from other .fsproj files.
4. Verify solution builds cleanly.

**Source:** Code audit SYNTHESIS.md, Tier 2 Finding #6
