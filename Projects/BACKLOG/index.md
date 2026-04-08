# LeoBloom Product Backlog

> **Note:** Done and cancelled item files have been moved to `Done/` as of
> 2026-04-06. Only active (not started) items remain in this directory.

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
| **035** | **Orphaned posting detection** | **Not started** |
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
| **056** | **Replace parent_code with parent_id** | **Not started** |
| **057** | **Investment schema** | **Not started** |
| **058** | **Investment domain types and repository** | **Not started** |
| **059** | **CLI portfolio commands** | **Not started** |
| **060** | **Portfolio data migration from PersonalFinance** | **Not started (Hobson, not Nightshift)** |
| **061** | **Portfolio allocation reporting** | **Not started** |
| **062** | **Consolidate normal balance resolution logic** | **Not started** |
| **063** | **Portfolio schema delete restriction tests** | **Not started** |
| **064** | **Balance sheet A=L+E independent verification** | **Not started** |
| **065** | **Balance projection status filter negative tests** | **Not started** |
| **066** | **Transfer atomicity and closed-period tests** | **Not started** |
| **067** | **Portfolio validation gaps (cost_basis, future dates)** | **Not started** |
| **068** | **Fiscal period overlap prevention** | **Not started** |
| **069** | **Account CRUD behavioral specs** | **Not started** |
| **070** | **Missing portfolio CLI commands** | **Not started** |
| **071** | **Consolidate CLI parseDate + fix TransferCommands** | **Not started** |
| **072** | **Housekeeping batch (audit cleanup)** | **Not started** |
| 073 | Connection injection + test isolation | Done |
| 074 | Npgsql version alignment | Not started |
| 075 | External account reference on ledger.account | Not started |
| 076 | Account update CLI command | Not started |
| **077** | **Account create CLI command** | **Not started** |
| **078** | **Transaction import stage schema** | **Not started** |

---

## Active Items (files in this directory)

| # | File | Status |
|---|------|--------|
| 022 | `022-balance-projection.md` | Not started |
| 035 | `035-orphaned-posting-detection.md` | Not started |
| 038 | `038-cli-obligation-commands.md` | Not started |
| 056 | `056-parent-account-fk-to-id.md` | Not started |
| 057 | `057-investment-schema.md` | Not started |
| 058 | `058-investment-domain-and-repository.md` | Not started |
| 059 | `059-cli-portfolio-commands.md` | Not started |
| 060 | `060-portfolio-data-migration.md` | Not started |
| 061 | `061-portfolio-allocation-reporting.md` | Not started |
| 062 | `062-normal-balance-consolidation.md` | Not started |
| 063 | `063-portfolio-delete-restrictions.md` | Not started |
| 064 | `064-balance-sheet-equation-test.md` | Not started |
| 065 | `065-balance-projection-filter-tests.md` | Not started |
| 066 | `066-transfer-parity-tests.md` | Not started |
| 067 | `067-portfolio-validation-gaps.md` | Not started |
| 068 | `068-fiscal-period-overlap-guard.md` | Not started |
| 069 | `069-account-crud-behavioral-specs.md` | Not started |
| 070 | `070-missing-portfolio-cli-commands.md` | Not started |
| 071 | `071-cli-parsedate-consolidation.md` | Not started |
| 072 | `072-housekeeping-batch.md` | Not started |
| 074 | `074-npgsql-version-alignment.md` | Not started |
| 075 | `075-external-account-reference.md` | Not started |
| 076 | `076-account-update-cli.md` | Not started |
| 077 | `077-account-create-cli.md` | Not started |
| 078 | `078-stage-schema.md` | Not started |

P028 (write-level ledger validation) has no spec file — it exists only in
this index (status: Done, covered by 005/006).

Also in this directory: `remediation-stories.md` (GAAP remediation sub-backlog
from P034).

## Done/Cancelled Items

All spec files for done and cancelled items are in `Done/`. 30 files total.

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
  generation. Just the DB layer for recording invoices. Complete.

### Code Audit Remediation (043-051)

Source: 2026-04-05 code audit (SYNTHESIS.md). ADRs in BdsNotes/decisions/.
All items complete.

### Foundation Cleanup (053 + 054)

Complete. Both projects done.

1. **053 (fix pre-existing test failures)** — Done. 5 tests in
   `PostObligationToLedgerTests` fixed by moving fiscal period dates to 2099
   to avoid seed data collision. All 608 tests pass.

2. **054 (seed data separation)** — Done. Baseline data (COA, fiscal periods)
   extracted from migration chain into idempotent seed scripts under
   `Seeds/dev/`. Shell runner, documentation, full BDD coverage. 615 tests
   pass.

### Closed Period Posting Guard (055)

**Done.** The guard already existed in `JournalEntryService.post` (line 62-63)
— it checks `isOpen` and rejects with "Fiscal period 'N' is not open". This
is the correct placement: the JE service is the domain chokepoint for all
ledger writes (obligations, transfers, direct posts), so the invariant is
enforced once, universally.

The two "failing" tests (POL-013, POL-017) were actually passing through
`JournalEntryService.post` and getting rejected correctly — but a fiscal
period date collision across test files caused `findByDate` to return a
different test's *open* period instead of the closed one the test created.
Fix: gave each test file that exercises `findByDate` its own year
(2091=PostObligationToLedgerTests, 2092=TransferTests). QE DSWF updated
with the isolation rule. 658/658 tests pass.

Design questions from the brief resolved via GAAP:
- Hard reject, no override flag. Reopen-then-post is the correct workflow.
- Guard covers all ledger writes (already does, at JE service level).
- Existing Gherkin specs were correct as written.

### CLI Sequencing (036-042)

1. **036 (CLI framework + ledger commands)** — Done.
2. **037, 038, 039, 041** — 037, 039, 041 done. **038 not started.**
3. **021 (invoice record persistence), then 042 (CLI invoice commands)** — Complete.
4. **040 (CLI tax reports)** — Done.
5. **035 (orphaned posting detection)** — standalone diagnostic, no CLI
   dependency. Slot wherever.
6. **022 (balance projection)** — lowest priority, slot wherever.

### Investment Portfolio Module (057-061)

Epic L. Brings Dan's investment tracking from PersonalFinance into LeoBloom.

1. **057 (investment schema)** — Greenfield. Creates `portfolio` schema, all
   10 tables, dev seeds. No dependencies on existing Leo code.
2. **058 (domain + repository)** — New `LeoBloom.Portfolio` F# project.
   Domain types, repos, services. Depends on 057.
3. **059 (CLI portfolio commands)** — Account, fund, position, dimension
   management. Depends on 058.
4. **060 (data migration)** — **Hobson's job, not Nightshift.** One-time
   script run on the host against prod. No BDD, no CLI command, no tests.
   Depends on 057 schema being applied to prod.
5. **061 (allocation reporting)** — Portfolio analysis reports (allocation by
   dimension, summary, history, gains). Depends on 058. Independent of 059/060
   but most useful after 060 populates real data.

**Nightshift sequencing:** 057 → 058 → {059, 060, 061} (last three are
independent, can run in any order).

### Audit Remediation (062-072)

Source: Nine-agent GAAP assessment, 2026-04-07. Reports in
`HobsonsNotes/GaapAssessment-2026-04-07/`.

**No dependencies on each other** unless noted. BD can enqueue in any order.

**High priority (data integrity):**
- **062** — Normal balance consolidation. Pure refactor, no deps.
- **063** — Portfolio delete restriction tests. Depends on 057.
- **064** — Balance sheet A=L+E test. No deps.
- **065** — Balance projection filter tests. No deps.
- **068** — Fiscal period overlap guard. No deps. Includes code + test.

**Medium priority (correctness gaps):**
- **066** — Transfer parity tests. No deps.
- **067** — Portfolio validation gaps. Depends on 058.
- **069** — Account CRUD behavioral specs. No deps.
- **070** — Missing portfolio CLI commands. Depends on 059.

**Low priority (cleanup):**
- **071** — CLI parseDate consolidation. No deps.
- **072** — Housekeeping batch. No deps.

**Note:** Test harness structural issues (connection/transaction architecture)
are being handled separately by BD as an architectural change. Cards 062-072
are independent of that work and can proceed in parallel.

### Transaction Import Pipeline (075-078)

Hobson's import pipeline needs CLI and schema support from BD.

**Sequencing:**
- **077 (account create CLI)** — No dependencies. Needed first so Hobson can
  expand the COA (~17 new accounts) before importing transactions.
- **075 (external account reference)** — No dependencies. Adds `external_ref`
  to `ledger.account` for FI account number mapping.
- **076 (account update CLI)** — Soft dependency on 075. Hobson needs this to
  populate `external_ref` and correct subtypes/names via CLI.
- **078 (stage schema)** — No dependencies on 075-077. Can be built in parallel.
  Creates the `stage` schema with per-FI staging tables and merchant rules.

**Tonight's priority:** 077 first (unblocks COA expansion), then 078, then
075 → 076. All four are independent enough to build in a single nightshift.

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
