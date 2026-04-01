# Leo Bloom — Wakeup Notes

## What is this?
Custom full-stack financial management system. Double-entry bookkeeping (cash
basis, GAAP-informed), obligation tracking, nagging agent, invoice generation,
balance projection, UI. Named after Gene Wilder's accountant in The Producers.

Dan overruled the lean recommendation — this is a hobby project. Learning GAAP
by modelling his own finances. "FUN in the Dwarf Fortress sense."

## Tech Stack (agreed)
- **F# all the way down** — domain, API (Giraffe/Falco), data access (Donald/Dapper), frontend (Fable/Feliz/Elmish)
- **PostgreSQL** — three databases: `leobloom_prod`, `leobloom_dev`, `leobloom_test`
- **Schema-first migrations** — SQL migration files are source of truth, not F# types. DbUp or Evolve. Possibly a .NET console app project (`LeoBloom.Migrations`).
- **.sln at repo root** (not in Src/) because migrations and code are peers for CI/CD

## Data Model Status
- **Spec:** `Docs/DataModelSpec-v1.md` (needs to move to `Specs/` — see restructure below)
- **Ledger schema:** Reviewed and agreed. `account_type`, `account`, `fiscal_period`, `journal_entry`, `journal_entry_reference`, `journal_entry_line`
- **Ops schema:** Reviewed and agreed. `obligation_type`, `obligation_status`, `cadence`, `payment_method`, `obligation_agreement`, `obligation_instance`, `transfer`, `invoice`
- **Key design decisions baked in:**
  - No business logic in DB constraints — structural integrity only (PK, FK, UNIQUE, NOT NULL, types). See memory: `feedback_db_constraints.md`
  - No `category` column anywhere — COA hierarchy via `parent_code` is the sole taxonomy
  - `obligation_agreement` (the terms/template) + `obligation_instance` (specific occurrence)
  - `bill` table folded into `obligation_instance` (added `due_date`, `document_path`)
  - `fiscal_period_id` dropped from `obligation_instance` — `expected_date` identifies the occurrence
  - `voided_at` replaces `is_void` on journal entries
  - One amount per table: `obligation_agreement.amount` = agreed-upon, `obligation_instance.amount` = what actually happened
  - Ops depends on ledger. Ledger knows nothing about ops.

## COA
- **Real COA** lives OUTSIDE repo at `/home/dan/penthouse-pete/property/ChartOfAccountsDraft.md`
- Dan's finance data must never be in any git repo
- BD gets a fake/sample COA for dev/test. Hobson writes the real SQL inserts for prod.

## Repo Structure (done)
```
LeoBloom/
├── LeoBloom.sln
├── Specs/
│   └── DataModelSpec-v1.md
├── Projects/                       # Delivery stages (waterfall)
├── HobsonsNotes/
├── BdsNotes/
├── Src/
│   ├── LeoBloom.Ledger/            # classlib — accounting domain
│   ├── LeoBloom.Ledger.Tests/      # xunit
│   ├── LeoBloom.Ops/               # classlib — ops domain, refs Ledger
│   ├── LeoBloom.Ops.Tests/         # xunit
│   ├── LeoBloom.Data/              # classlib — data access, refs Ledger + Ops
│   ├── LeoBloom.Migrations/        # console app — DB migrations
│   └── LeoBloom.Api/               # webapi, refs Ledger + Ops + Data
└── .gitignore
```
Built on .NET 8.0. May want to upgrade to 9.0.

## Delivery Plan
- **Project 1:** Database — schema, migrations, seed data. Get the DB standing so Dan can start using it.
- **Project 2:** Application model layer — F# domain types, accounting engine, BDD tests (TickSpec)
- **Project 3:** UI — Fable/Feliz/Elmish frontend

## Next Steps
1. Do the repo restructure (move DataModelSpec, create Specs/, Projects/, BdsNotes/, Src/)
2. Draft Project 1 requirements (BRD for database stage)
3. Create fake sample COA for BD's dev/test databases
4. Write real COA SQL inserts (outside repo) for prod
5. Dan still has open questions in DataModelSpec (tenant table, audit log)

## Open Questions from DataModelSpec
- Tenant as first-class entity? Currently just a string. YAGNI for now.
- Audit log for ops status changes? `modified_at` captures when, not what changed.
- Multi-currency: No. Ever. USD only.
