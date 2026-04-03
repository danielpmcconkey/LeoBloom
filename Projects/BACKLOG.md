# LeoBloom Product Backlog

**Product Owner:** Hobson
**Tech Lead:** Basement Dweller
**Stakeholder:** Dan

---

## How to read this file

Epics are capability areas. Stories are the unit of work — each maps to one
project folder (`ProjectNNN/`), one feature branch, one PR. Sized for one BD
session.

**Status:** Backlog → Ready → In Progress → Done
A story is **Ready** when all dependencies are Done and a BRD exists.

---

## Foundation (Projects 001–004)

| Project | Description | Status |
|---------|-------------|--------|
| 001 | Database schema, migrations, seed data, structural constraint specs | Done |
| 002 | Test harness — TickSpec/xUnit, 88 Gherkin scenarios, shared config | In Progress |
| 003 | BDD infrastructure — FT tags, feature file reorg, DeleteTarget refactor, docs | Ready |
| 004 | Domain types — F# types in Domain, business logic BDD in Domain.Tests | Backlog |

---

## Epic A: Journal Entry Engine

The core write path. Post and void entries. Everything downstream depends on
this working correctly.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 005 | **Post journal entry** — validate balance (debits = credits), enforce open fiscal period, persist entry + lines + references, source tagging | 004 | Backlog |
| 006 | **Void journal entry** — set voided_at + void_reason, validate reason non-empty, idempotent, voided entries excluded from all balance queries | 005 | Backlog |

---

## Epic B: Balance Calculation

The core read path. Every report and projection depends on being able to
answer "what is the balance of account X?"

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 007 | **Account balance** — for an account up to a date, apply normal_balance direction from account_type, exclude voided entries | 005 | Backlog |
| 008 | **Trial balance** — for a fiscal period, sum all account balances, verify system-wide debit = credit | 007 | Backlog |

---

## Epic C: Fiscal Period Management

Period-close discipline. Lock the books so retroactive edits can't happen.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 009 | **Close / reopen fiscal period** — toggle is_open, enforce no-posting-to-closed in journal entry engine, reopen requires explicit override | 005 | Backlog |
| 010 | **Opening balances** — bootstrap initial account balances via journal entry against equity, one-time go-live operation | 005, 007 | Backlog |

---

## Epic D: Financial Statements

The reports Dan actually wants to look at.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 011 | **Income statement** — revenue minus expenses for a period | 008 | Backlog |
| 012 | **Balance sheet** — assets, liabilities, equity at a point in time | 008 | Backlog |
| 013 | **P&L by account subtree** — walk parent_code hierarchy, filter by subtree (investment property 5xxx vs personal 6xxx) | 011 | Backlog |

---

## Epic E: Obligation Lifecycle

The ops engine. Agreements define what should happen; instances track whether
it did.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 014 | **Obligation agreements** — create, update, deactivate. Validate FK refs to ledger accounts, cadence, payment method | 004 | Backlog |
| 015 | **Spawn obligation instances** — from agreement + date range, generate instances. Fixed amounts pre-filled, variable amounts null | 014 | Backlog |
| 016 | **Status transitions** — enforce lifecycle state machine (expected → in_flight → confirmed → posted; expected → overdue; expected → skipped). Validate required fields per state | 015 | Backlog |
| 017 | **Overdue detection** — identify active instances past expected_date still in expected or in_flight status | 016 | Backlog |

---

## Epic F: Obligation → Ledger Posting

The bridge. When money is confirmed, the accounting engine records it.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 018 | **Post obligation to ledger** — on "posted" status, create journal entry from agreement's source/dest accounts + instance amount, link via journal_entry_id | 005, 016 | Backlog |

---

## Epic G: Transfer Management

Asset-to-asset moves. Solves the in-flight problem (money left one account
but hasn't arrived in another).

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 019 | **Transfers** — initiate (no journal entry), confirm (create journal entry: debit dest, credit source), track in-flight. Both accounts must be asset type | 005, 007 | Backlog |

---

## Epic H: Invoice Generation

The tenant-facing output. Rent + 1/3 utility share = what you owe this month.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 020 | **Invoice readiness** — for a target month, verify all variable-amount utility obligations have amounts set | 015, 017 | Backlog |
| 021 | **Generate invoice** — compute 1/3 utility share, create invoice record, link to fiscal period | 020 | Backlog |

---

## Epic I: Balance Projection

"Will I have enough in the CMA next month?" Computed, not stored.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 022 | **Balance projection** — current balance + expected inflows − expected outflows − in-flight transfers, rolled forward N days | 007, 015, 019 | Backlog |

---

## Epic J: API Layer

REST endpoints. The backbone for any UI or external integration.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 023 | **Journal entry endpoints** — POST (create with lines + refs), GET (by id, by period, by account), PATCH (void) | 005, 006 | Backlog |
| 024 | **Reporting endpoints** — GET account balance, trial balance, income statement, balance sheet, P&L by subtree | 011, 012, 013 | Backlog |
| 025 | **Obligation endpoints** — CRUD agreements, list/filter instances, transition status, post to ledger | 018 | Backlog |
| 026 | **Transfer & invoice endpoints** — create/confirm transfers, check invoice readiness, generate invoice | 019, 021 | Backlog |
| 027 | **Projection endpoint** — GET balance projection for account + date range | 022 | Backlog |

---

## Beyond the Horizon

Not scoped, not sized. Noted so we don't forget they exist.

- **Nagging agent** — Discord bot (OpenClaw pattern) for overdue alerts,
  upcoming obligation reminders, in-flight settlement monitoring
- **UI** — dashboard, data entry, report views (Fable/Feliz/Elmish)
- **Invoice document generation** — PDF output from invoice records
- **Import pipeline** — bulk entry from bank exports (Ally, Fidelity CSVs)
- **Audit event log** — ops status change history (DataModelSpec open question #4)

---

## Notes

- Stories 005–013 (Epics A–D) are pure ledger. No ops dependency. This is
  the accounting engine and it must be solid before ops builds on top of it.
- Epics E–F can start in parallel with Epics C–D if BD has capacity, since
  Epic E only depends on Project 004.
- The API epic (J) is last because the domain layer needs to be proven via
  BDD tests before we expose it over HTTP. The API is a thin pass-through.
- Each story gets a BRD when it moves to Ready. Acceptance criteria live in
  the BDD doc, not here.
