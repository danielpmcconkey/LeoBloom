# 045 — Domain-Based Project Reorganization

**Epic:** K — Code Audit Remediation
**Depends On:** None (but should precede 036)
**Status:** Not started

---

Move domain-specific services and repositories out of LeoBloom.Utilities
into their domain projects. The audit flagged Utilities as a god project
(22 files spanning infrastructure, persistence, and services). Dan's
decision: organize by business domain, not by architectural layer.

**Target structure:**

- **LeoBloom.Utilities** — generic cross-cutting infrastructure only:
  DataSource, Log, config, DataHelpers
- **LeoBloom.Ledger** — ledger services and repositories (journal entries,
  accounts, fiscal periods, reporting)
- **LeoBloom.Ops** — ops services and repositories (obligations, transfers,
  posting)

The empty project directories for Ledger and Ops already exist under Src/.

**Scope:**

1. Move ledger-domain .fs files from Utilities to LeoBloom.Ledger.
2. Move ops-domain .fs files from Utilities to LeoBloom.Ops.
3. Update .fsproj Compile includes in all three projects.
4. Update namespace declarations in moved files.
5. Update ProjectReference entries (Ledger and Ops reference Utilities;
   Ops references Ledger for posting).
6. Update test file imports/opens in LeoBloom.Tests.
7. Verify full solution builds and all tests pass.

**What stays in Utilities:**

- DataSource.fs
- Log.fs
- Configuration / appsettings handling
- DataHelpers.fs (shared DB utilities like optParam)

**Design decision:** ADR-002 documents the domain-based split rationale.

**Source:** Code audit SYNTHESIS.md, Tier 2 Finding #4
