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
| 014 | Obligation agreements | Not started |
| 015 | Spawn obligation instances | Not started |
| 016 | Status transitions | Not started |
| 017 | Overdue detection | Not started |
| 018 | Post obligation to ledger | Not started |
| 019 | Transfers | Not started |
| 020 | Invoice readiness | Not started |
| 021 | Generate invoice | Not started |
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

---

## Sequencing Notes

- **Critical path:** 004 → 029 → 005 → 007 → 008 → 011/012 → 013. This is
  the ledger engine from types to financial statements. Project 029 (lookup
  table elimination) slots in before 005 because the write path must be built
  against the cleaned-up schema.
- **Parallel track:** 004 → 014 → 015 → 016 → 017. Obligation lifecycle can
  be built in parallel with the ledger reporting epics once Project 004 is done.
- **Convergence point:** Story 018 (obligation→ledger posting) requires both
  the posting engine (005) and the status machine (016). This is where the two
  tracks meet.
- **Late dependencies:** Transfers (019), invoices (020–021), and projection
  (022) are relatively independent once their prerequisites are met.
- **API projects (023–027) cancelled.** Consumption layer TBD — may not be
  a traditional REST API.

---

## Beyond the Horizon

Not scoped, not sized. Noted so we don't forget they exist.

- **Nagging agent** — Discord bot (OpenClaw pattern) for overdue alerts,
  upcoming obligation reminders, in-flight settlement monitoring.
- **UI** — dashboard, data entry, report views (Fable/Feliz/Elmish).
- **Invoice document generation** — PDF output from invoice records.
- **Import pipeline** — bulk journal entry creation from bank exports
  (Ally CSV, Fidelity CSV). Reuse Thatcher's parser patterns.
- **Audit event log** — ops status change history (DataModelSpec open
  question #4). Track what changed, from what, by whom.
