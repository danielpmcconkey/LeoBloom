# Project 040 — CLI Tax Reports

**Status:** Done
**PO:** Approved (Gate 2 sign-off 2026-04-05)
**Date:** 2026-04-05

---

## Objective

Add a `report` command group to the LeoBloom CLI with four tax report
commands (`schedule-e`, `general-ledger`, `cash-receipts`,
`cash-disbursements`), backed by a new `LeoBloom.Reporting` F# class
library.

## Why

Dan's CPA needs a complete filing package for the 2026 tax year. These
reports don't exist yet — the domain logic is new, not a thin wrapper
around existing services.

## Scope

### In Scope

1. **New project: `LeoBloom.Reporting`** — F# class library. Depends on
   Domain, Utilities, Ledger, and Ops. Houses cross-domain reporting logic
   where the domain boundary is "things the CPA asks for."

2. **Four tax report services:**
   - `schedule-e` — rolls up COA into IRS Schedule E line items (rental
     income, mortgage interest, property tax, insurance, HOA, utilities,
     repairs, depreciation). Depreciation from config, not computed.
     One property, 27.5-year straight-line.
   - `general-ledger` — transaction-level detail for a single account
     over a date range
   - `cash-receipts` — all money in, with counterparty and date, over a
     date range
   - `cash-disbursements` — all money out, with counterparty and date,
     over a date range

3. **New CLI `report` command group** — does not yet exist. P040 creates it
   with the four tax report subcommands. Future P037 adds accounting reports
   to the same group.

4. **COA-to-Schedule E line mapping** — must be verified against seed data.

5. **Depreciation from config** — not a fixed asset module. One property,
   27.5-year straight-line.

### Out of Scope

- Accounting reports (trial balance, balance sheet, income statement, P&L
  subtree, account balance) — that's P037
- `--json` output for tax reports (consumer is CPA via Dan, not agents)
- Fixed asset module / depreciation computation
- Any modifications to the Ledger or Ops domain projects
- Invoice commands (P042)

## Key Architectural Decisions

- **ADR-003** governs CLI architecture: parse -> call -> format -> exit code.
  No business logic in CLI.
- Cross-service orchestration belongs in services (Reporting project), not CLI.
- CLI Gherkin specs test CLI behavior only, not report logic.
- All four reports live in `LeoBloom.Reporting`, even the thin ledger reads.
  The domain boundary is "things the CPA asks for."

## Dependencies

- P036 (CLI framework + ledger commands) — Done. Provides the CLI entry
  point, argument parsing, and output formatting conventions.
- Domain, Utilities, Ledger, Ops projects — all exist and are stable.

## Source Documents

- CLI vision: `BdsNotes/cli-vision-2026-04-05.md`
- Backlog: `Projects/BACKLOG/index.md`
