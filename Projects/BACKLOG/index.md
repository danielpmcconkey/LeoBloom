# LeoBloom Product Backlog

> **Note:** Done and cancelled item files live in `Done/`. Only active (not
> started) items remain in this directory. Last reconciled: 2026-04-12.

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
| 021 | Invoice record persistence | Done |
| **022** | **Balance projection** | **Not started** |
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
| 035 | Orphaned posting detection | Done |
| 036 | CLI framework + ledger commands | Done |
| 037 | CLI reporting commands (accounting) | Done |
| **038** | **CLI obligation commands** | **Not started** |
| 039 | CLI transfer commands | Done |
| 040 | CLI tax reports | Done |
| 041 | CLI account + period commands | Done |
| 042 | CLI invoice commands | Done |
| 043 | Idempotency guards | Done |
| 044 | Database indexes migration | Done |
| 045 | Domain-based project reorg | Done |
| 046 | Delete LeoBloom.Api | Done |
| 047 | Delete ghost directories | Done |
| 048 | Test cleanup | Done |
| 049 | Consolidate helpers | Done |
| 050 | Use EntryType.toDbString | Done |
| 051 | Move IncludeErrorDetail to appsettings | Done |
| 052 | Account sub-type classification | Done |
| 053 | Fix pre-existing test failures | Done |
| 054 | Seed data separation | Done |
| 055 | Closed fiscal period posting guard | Done |
| 056 | Replace parent_code with parent_id | Done |
| 057 | Investment schema | Done |
| 058 | Investment domain types and repository | Done |
| 059 | CLI portfolio commands | Done |
| 060 | Portfolio data migration from PersonalFinance | Done (Hobson) |
| 061 | Portfolio allocation reporting | Done |
| 062 | Consolidate normal balance resolution logic | Done |
| 063 | Portfolio schema delete restriction tests | Done |
| 064 | Balance sheet A=L+E independent verification | Done |
| 065 | Balance projection status filter negative tests | Done |
| 066 | Transfer atomicity and closed-period tests | Done |
| 067 | Portfolio validation gaps (cost_basis, future dates) | Done |
| 068 | Fiscal period overlap prevention | Done |
| 069 | Account CRUD behavioral specs | Done |
| 070 | Missing portfolio CLI commands | Done |
| 071 | Consolidate CLI parseDate + fix TransferCommands | Done |
| 072 | Housekeeping batch (audit cleanup) | Done |
| 073 | Connection injection + test isolation | Done |
| 074 | Npgsql version alignment | Done |
| 075 | External account reference on ledger.account | Done |
| 076 | Account update CLI command | Done |
| 077 | Account create CLI command | Done |
| 078 | Transaction import stage schema | Done (Hobson) |
| 079 | Add `irregular` recurrence cadence | Done |
| 080 | Reporting data extracts (JSON CLI) | Done |

---

## Active Items (files in this directory)

| # | File | Status | Notes |
|---|------|--------|-------|
| 022 | `022-balance-projection.md` | Not started | Lowest priority, needs BA pass |
| 038 | `038-cli-obligation-commands.md` | Not started | Held for Dan's input on open questions |

P028 (write-level ledger validation) has no spec file — it exists only in
this index (status: Done, covered by 005/006).

## Done/Cancelled Items

All spec files for done and cancelled items are in `Done/`. 56 files total.

---

## Sequencing Notes

### Completed Epics

All historical sequencing notes for completed work have been trimmed.
Completed epics: core ledger (001–019), API cancelled/CLI replacement
(023–027 → 036–042), code audit remediation (043–051), foundation cleanup
(053–055), investment portfolio module (056–061), GAAP audit remediation
(062–072), connection injection (073), import pipeline CLI support (074–077).

### Remaining Work

**Nightshift candidates (BD pipeline):**
- **038 (CLI obligation commands)** — Gap in the CLI layer. Held for Dan's
  input on open questions.
- **022 (balance projection)** — Lowest priority. Needs BA pass.

**Hobson-only (complete):**
- **060** — Portfolio data migration. Done.
- **078** — Stage schema. Done.

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
