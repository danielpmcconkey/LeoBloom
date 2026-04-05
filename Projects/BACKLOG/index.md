# LeoBloom Product Backlog

| # | Project | Status |
|---|---------|--------|
| 001 | Database schema, migrations, seed data | Done |
| 002 | Test harness | Done |
| 003 | BDD infrastructure | Done |
| 004 | Domain types | Done |
| 005 | Post journal entry | Done |
| 006 | Void journal entry | Done |
| 007 | Account balance | Done |
| 008 | Trial balance | Done |
| 009 | Close / reopen fiscal period | Done |
| 010 | Opening balances | Done |
| 011 | Income statement | Done |
| 012 | Balance sheet | Done |
| 013 | P&L by account subtree | Done |
| 014 | Obligation agreements | Done |
| 015 | Spawn obligation instances | Done |
| 016 | Status transitions | Done |
| 017 | Overdue detection | Done |
| 018 | Post obligation to ledger | Done |
| 019 | Transfers | Done |
| 020 | Invoice readiness | Cancelled |
| 021 | Invoice record persistence | Not started |
| 022 | Balance projection | Not started |
| 023 | Journal entry endpoints | Cancelled |
| 024 | Reporting endpoints | Cancelled |
| 025 | Obligation endpoints | Cancelled |
| 026 | Transfer & invoice endpoints | Cancelled |
| 027 | Projection endpoint | Cancelled |
| 028 | Write-level ledger validation | Done (covered by 005/006) |
| 029 | Lookup table elimination | Done |
| 030 | Unfuck our test harness | Done |
| 031 | Foundational logging infrastructure | Done |
| 032 | Test Author Agent Blueprint | Done |
| 033 | Seal DataSource internals | Done |
| 034 | GAAP remediation | Done |
| 035 | Orphaned posting detection | Not started |
| 036 | CLI framework + ledger commands | Done |
| 037 | CLI reporting commands (accounting) | Not started |
| 038 | CLI obligation commands | Not started |
| 039 | CLI transfer commands | Not started |
| 040 | CLI tax reports | Not started |
| 041 | CLI account + period commands | Not started |
| 042 | CLI invoice commands | Not started |
| 043 | Idempotency guards | Done |
| 044 | Database indexes migration | Done |
| 045 | Domain-based project reorg | Done |
| 046 | Delete LeoBloom.Api | Done |
| 047 | Delete ghost directories | Done |
| 048 | Test cleanup | Done |
| 049 | Consolidate helpers | Done |
| 050 | Use EntryType.toDbString | Done |
| 051 | Move IncludeErrorDetail to appsettings | Done |

---

## Sequencing Notes

- **Critical path (done):** 004 -> 029 -> 005 -> 007 -> 008 -> 011/012 -> 013.
  Ledger engine from types to financial statements. Complete.
- **Parallel track (done):** 004 -> 014 -> 015 -> 016 -> 017. Obligation
  lifecycle. Complete.
- **Convergence point (done):** 018 (obligation -> ledger posting) required
  both the posting engine (005) and status machine (016). Complete.
- **Remediation (034):** GAAP patches to projects 001-018. Complete.
- **API projects (023-027) cancelled.** Replaced by CLI consumption layer
  (036-042).
- **P020 (Invoice Readiness) cancelled.** Readiness is the COYS bot's
  responsibility.
- **P021 rewritten** as invoice record persistence. No calculation, no PDF
  generation. Just the DB layer for recording invoices.

### Code Audit Remediation (043-051)

Source: 2026-04-05 code audit (SYNTHESIS.md). ADRs in BdsNotes/decisions/.

**Before any new feature work:**
1. **043 (idempotency guards)** — correctness fix, #1 audit finding.
2. **044 (database indexes)** — performance, one migration.

**Before P036 (CLI framework):**
3. **045 (domain-based project reorg)** — structural prerequisite so the CLI
   consumes clean domain projects, not a god project.
4. **046 (delete LeoBloom.Api)** — dead code removal, no dependencies.
5. **047 (delete ghost directories)** — dead scaffolding, no dependencies.
6. **048 (test cleanup)** — dead test removal, no dependencies.

**Any order, low effort:**
7. **049 (consolidate helpers)** — sequence after 045 if both in flight.
8. **050 (use EntryType.toDbString)** — trivial one-liner.
9. **051 (IncludeErrorDetail to appsettings)** — trivial config change.

Items 046-048 and 050-051 can run in parallel. Item 049 should follow 045
if the domain reorg moves files that contain the duplicated helpers.

### CLI Sequencing (036-042)

1. **036 (CLI framework + ledger commands)** — establishes the entry point,
   argument parsing, output conventions. Everything else depends on this.
2. **037, 038, 039, 041** in any order — thin wrappers around existing
   services. No new domain logic needed.
3. **021 (invoice record persistence), then 042 (CLI invoice commands)** —
   lean persistence layer followed by its CLI wrapper.
4. **040 (CLI tax reports)** — new report logic. Target completion well before
   2027 tax season.
5. **035 (orphaned posting detection)** — standalone diagnostic, no CLI
   dependency. Slot wherever.
6. **022 (balance projection)** — lowest priority, slot wherever.

---

## Beyond the Horizon

Not scoped, not sized. Noted so we don't forget they exist.

- **Nagging agent** — Discord bot (OpenClaw pattern) for overdue alerts,
  upcoming obligation reminders, in-flight settlement monitoring.
- **UI** — dashboard, data entry, report views (Fable/Feliz/Elmish).
- **Invoice document generation** — PDF output from invoice records. COYS bot
  responsibility, not LeoBloom's.
- **Import pipeline** — bulk journal entry creation from bank exports
  (Ally CSV, Fidelity CSV). Reuse Thatcher's parser patterns.
- **Audit event log** — ops status change history (DataModelSpec open
  question #4). Track what changed, from what, by whom.
- **Fixed asset module** — full depreciation tracking. Currently handled via
  config in P040. Revisit if Dan acquires more properties.
