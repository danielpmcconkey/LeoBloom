# Project 1 — Database

## Objective

Stand up the Leo Bloom database layer: schema, migrations, seed data, and
configuration. When this project is done, both environments have a running
PostgreSQL database with all ledger and ops tables, structural constraints
verified by Gherkin acceptance specs, and seed data loaded.

No application code. No business logic. Structural integrity only.

---

## Environments

| Database | Environment | Owner | Machine |
|---|---|---|---|
| `leobloom_dev` | Development / Test | BD (Basement Dweller) | Docker sandbox |
| `leobloom_prod` | Production | Hobson / Dan | Host machine |

One codebase, two environments. Same migrations run against both databases.

---

## Configuration Strategy

- **`appsettings.Development.json`** — connection string for `leobloom_dev`
  (host, port, database name). Checked into repo.
- **`appsettings.Production.json`** — connection string for `leobloom_prod`
  (host, port, database name). Checked into repo.
- **Secrets (passwords, credentials)** — never in the repo. Injected at runtime
  via environment variables. Managed in `pass` store under `openclaw/leobloom/`
  or equivalent. Connection string references a placeholder that the runtime
  populates from the environment (e.g. `Password=%LEOBLOOM_DB_PASSWORD%` or
  equivalent for the chosen migration tool).

---

## Deliverables

### 1. Database Creation

Manual step (Dan / Phase 0):
- Create `leobloom_dev` and `leobloom_prod` databases on their respective
  PostgreSQL instances
- Create Postgres roles with appropriate permissions
- Store credentials in `pass`

### 2. Migration Infrastructure

- `LeoBloom.Migrations` — .NET console app, **F#** (F# all the way down)
- Migration tool: DbUp, Evolve, or other — **discuss with Dan before committing**
- Reads connection string from `appsettings.{Environment}.json`
- Reads password from environment variable
- Migrations are numbered SQL files, executed in order, idempotent tracking
  (migration journal table)
- `.sln` already includes the project in the repo structure

### 3. Migration Scripts

Executed in dependency order. Ledger first, ops second.

**Batch 1 — Ledger schema:**
1. Create `ledger` schema
2. `ledger.account_type`
3. `ledger.account`
4. `ledger.fiscal_period`
5. `ledger.journal_entry`
6. `ledger.journal_entry_reference`
7. `ledger.journal_entry_line`

**Batch 2 — Ops schema:**
1. Create `ops` schema
2. `ops.obligation_type`
3. `ops.obligation_status`
4. `ops.cadence`
5. `ops.payment_method`
6. `ops.obligation_agreement`
7. `ops.obligation_instance`
8. `ops.transfer`
9. `ops.invoice`

Each migration is a standalone `.sql` file. The SQL in these files is the source
of truth for the schema — F# types (Project 2) mirror it, not the other way
around.

### 4. Seed Data

Lookup tables seeded via migration scripts (run once, part of the migration
sequence):

| Table | Rows |
|---|---|
| `ledger.account_type` | 5 (asset, liability, equity, revenue, expense) |
| `ops.obligation_type` | 2 (receivable, payable) |
| `ops.obligation_status` | 6 (expected, in_flight, confirmed, posted, overdue, skipped) |
| `ops.cadence` | 4 (monthly, quarterly, annual, one_time) |
| `ops.payment_method` | 6 (autopay_pull, ach, zelle, cheque, bill_pay, manual) |
| `ledger.fiscal_period` | 36 months (2026-01 through 2028-12) |

**Chart of Accounts:**
- **Dev/test:** Anonymized sample COA from `Specs/SampleCOA.md`, checked into
  repo as a seed migration. Same structure and numbering as prod, generic names.
- **Prod:** Real COA from `~/penthouse-pete/property/ChartOfAccountsDraft.md`.
  SQL insert script lives outside the repo. Hobson writes it; Dan runs it
  manually.

### 5. Structural Acceptance Tests (Gherkin)

Written now as Project 1 acceptance criteria. Test harness to execute them comes
in Project 2.

Specs live in `Specs/Acceptance/Project1-StructuralConstraints.feature` (or
equivalent location agreed during implementation).

---

## Acceptance Tests — Structural Constraints

All scenarios below verify database-level enforcement. No application logic.

```gherkin
Feature: Ledger schema structural constraints

  # --- account_type ---

  Scenario: account_type requires a name
    Given the ledger schema exists
    When I insert into account_type with a null name
    Then the insert is rejected with a NOT NULL violation

  Scenario: account_type name must be unique
    Given an account_type "asset" exists
    When I insert another account_type with name "asset"
    Then the insert is rejected with a UNIQUE violation

  Scenario: account_type requires a normal_balance
    Given the ledger schema exists
    When I insert into account_type with a null normal_balance
    Then the insert is rejected with a NOT NULL violation

  # --- account ---

  Scenario: account requires a code
    Given the ledger schema exists
    When I insert into account with a null code
    Then the insert is rejected with a NOT NULL violation

  Scenario: account code must be unique
    Given an account with code "1010" exists
    When I insert another account with code "1010"
    Then the insert is rejected with a UNIQUE violation

  Scenario: account requires a name
    Given the ledger schema exists
    When I insert into account with a null name
    Then the insert is rejected with a NOT NULL violation

  Scenario: account requires an account_type_id
    Given the ledger schema exists
    When I insert into account with a null account_type_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: account account_type_id must reference a valid account_type
    Given the ledger schema exists
    When I insert into account with account_type_id 9999
    Then the insert is rejected with a FK violation

  Scenario: account parent_code must reference a valid account code
    Given the ledger schema exists
    When I insert into account with parent_code "XXXX" that does not exist
    Then the insert is rejected with a FK violation

  Scenario: account parent_code is nullable
    Given the ledger schema exists
    When I insert a valid account with a null parent_code
    Then the insert succeeds

  # --- fiscal_period ---

  Scenario: fiscal_period requires a period_key
    Given the ledger schema exists
    When I insert into fiscal_period with a null period_key
    Then the insert is rejected with a NOT NULL violation

  Scenario: fiscal_period period_key must be unique
    Given a fiscal_period "2026-03" exists
    When I insert another fiscal_period with period_key "2026-03"
    Then the insert is rejected with a UNIQUE violation

  Scenario: fiscal_period requires start_date and end_date
    Given the ledger schema exists
    When I insert into fiscal_period with a null start_date
    Then the insert is rejected with a NOT NULL violation

  # --- journal_entry ---

  Scenario: journal_entry requires an entry_date
    Given the ledger schema exists
    When I insert into journal_entry with a null entry_date
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry requires a description
    Given the ledger schema exists
    When I insert into journal_entry with a null description
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry requires a fiscal_period_id
    Given the ledger schema exists
    When I insert into journal_entry with a null fiscal_period_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry fiscal_period_id must reference a valid fiscal_period
    Given the ledger schema exists
    When I insert into journal_entry with fiscal_period_id 9999
    Then the insert is rejected with a FK violation

  Scenario: journal_entry voided_at is nullable
    Given the ledger schema exists
    When I insert a valid journal_entry with null voided_at
    Then the insert succeeds

  # --- journal_entry_reference ---

  Scenario: journal_entry_reference requires a journal_entry_id
    Given the ledger schema exists
    When I insert into journal_entry_reference with a null journal_entry_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry_reference journal_entry_id must reference a valid journal_entry
    Given the ledger schema exists
    When I insert into journal_entry_reference with journal_entry_id 9999
    Then the insert is rejected with a FK violation

  Scenario: journal_entry_reference requires reference_type
    Given the ledger schema exists
    When I insert into journal_entry_reference with a null reference_type
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry_reference requires reference_value
    Given the ledger schema exists
    When I insert into journal_entry_reference with a null reference_value
    Then the insert is rejected with a NOT NULL violation

  # --- journal_entry_line ---

  Scenario: journal_entry_line requires a journal_entry_id
    Given the ledger schema exists
    When I insert into journal_entry_line with a null journal_entry_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry_line journal_entry_id must reference a valid journal_entry
    Given the ledger schema exists
    When I insert into journal_entry_line with journal_entry_id 9999
    Then the insert is rejected with a FK violation

  Scenario: journal_entry_line requires an account_id
    Given the ledger schema exists
    When I insert into journal_entry_line with a null account_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry_line account_id must reference a valid account
    Given the ledger schema exists
    When I insert into journal_entry_line with account_id 9999
    Then the insert is rejected with a FK violation

  Scenario: journal_entry_line requires an amount
    Given the ledger schema exists
    When I insert into journal_entry_line with a null amount
    Then the insert is rejected with a NOT NULL violation

  Scenario: journal_entry_line requires an entry_type
    Given the ledger schema exists
    When I insert into journal_entry_line with a null entry_type
    Then the insert is rejected with a NOT NULL violation


Feature: Ops schema structural constraints

  # --- obligation_type ---

  Scenario: obligation_type name must be unique
    Given an obligation_type "receivable" exists
    When I insert another obligation_type with name "receivable"
    Then the insert is rejected with a UNIQUE violation

  Scenario: obligation_type requires a name
    Given the ops schema exists
    When I insert into obligation_type with a null name
    Then the insert is rejected with a NOT NULL violation

  # --- obligation_status ---

  Scenario: obligation_status name must be unique
    Given an obligation_status "expected" exists
    When I insert another obligation_status with name "expected"
    Then the insert is rejected with a UNIQUE violation

  Scenario: obligation_status requires a name
    Given the ops schema exists
    When I insert into obligation_status with a null name
    Then the insert is rejected with a NOT NULL violation

  # --- cadence ---

  Scenario: cadence name must be unique
    Given a cadence "monthly" exists
    When I insert another cadence with name "monthly"
    Then the insert is rejected with a UNIQUE violation

  Scenario: cadence requires a name
    Given the ops schema exists
    When I insert into cadence with a null name
    Then the insert is rejected with a NOT NULL violation

  # --- payment_method ---

  Scenario: payment_method name must be unique
    Given a payment_method "zelle" exists
    When I insert another payment_method with name "zelle"
    Then the insert is rejected with a UNIQUE violation

  Scenario: payment_method requires a name
    Given the ops schema exists
    When I insert into payment_method with a null name
    Then the insert is rejected with a NOT NULL violation

  # --- obligation_agreement ---

  Scenario: obligation_agreement requires a name
    Given the ops schema exists
    When I insert into obligation_agreement with a null name
    Then the insert is rejected with a NOT NULL violation

  Scenario: obligation_agreement requires an obligation_type_id
    Given the ops schema exists
    When I insert into obligation_agreement with a null obligation_type_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: obligation_agreement obligation_type_id must reference a valid obligation_type
    Given the ops schema exists
    When I insert into obligation_agreement with obligation_type_id 9999
    Then the insert is rejected with a FK violation

  Scenario: obligation_agreement requires a cadence_id
    Given the ops schema exists
    When I insert into obligation_agreement with a null cadence_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: obligation_agreement cadence_id must reference a valid cadence
    Given the ops schema exists
    When I insert into obligation_agreement with cadence_id 9999
    Then the insert is rejected with a FK violation

  Scenario: obligation_agreement payment_method_id must reference a valid payment_method
    Given the ops schema exists
    When I insert into obligation_agreement with payment_method_id 9999
    Then the insert is rejected with a FK violation

  Scenario: obligation_agreement source_account_id must reference a valid ledger account
    Given the ops schema exists
    When I insert into obligation_agreement with source_account_id 9999
    Then the insert is rejected with a FK violation

  Scenario: obligation_agreement dest_account_id must reference a valid ledger account
    Given the ops schema exists
    When I insert into obligation_agreement with dest_account_id 9999
    Then the insert is rejected with a FK violation

  Scenario: obligation_agreement amount is nullable
    Given the ops schema exists
    When I insert a valid obligation_agreement with a null amount
    Then the insert succeeds

  # --- obligation_instance ---

  Scenario: obligation_instance requires an obligation_agreement_id
    Given the ops schema exists
    When I insert into obligation_instance with a null obligation_agreement_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: obligation_instance obligation_agreement_id must reference a valid agreement
    Given the ops schema exists
    When I insert into obligation_instance with obligation_agreement_id 9999
    Then the insert is rejected with a FK violation

  Scenario: obligation_instance requires a name
    Given the ops schema exists
    When I insert into obligation_instance with a null name
    Then the insert is rejected with a NOT NULL violation

  Scenario: obligation_instance requires a status_id
    Given the ops schema exists
    When I insert into obligation_instance with a null status_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: obligation_instance status_id must reference a valid obligation_status
    Given the ops schema exists
    When I insert into obligation_instance with status_id 9999
    Then the insert is rejected with a FK violation

  Scenario: obligation_instance requires an expected_date
    Given the ops schema exists
    When I insert into obligation_instance with a null expected_date
    Then the insert is rejected with a NOT NULL violation

  Scenario: obligation_instance journal_entry_id must reference a valid journal_entry
    Given the ops schema exists
    When I insert into obligation_instance with journal_entry_id 9999
    Then the insert is rejected with a FK violation

  Scenario: obligation_instance journal_entry_id is nullable
    Given the ops schema exists
    When I insert a valid obligation_instance with null journal_entry_id
    Then the insert succeeds

  # --- transfer ---

  Scenario: transfer requires a from_account_id
    Given the ops schema exists
    When I insert into transfer with a null from_account_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: transfer from_account_id must reference a valid ledger account
    Given the ops schema exists
    When I insert into transfer with from_account_id 9999
    Then the insert is rejected with a FK violation

  Scenario: transfer requires a to_account_id
    Given the ops schema exists
    When I insert into transfer with a null to_account_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: transfer to_account_id must reference a valid ledger account
    Given the ops schema exists
    When I insert into transfer with to_account_id 9999
    Then the insert is rejected with a FK violation

  Scenario: transfer requires an amount
    Given the ops schema exists
    When I insert into transfer with a null amount
    Then the insert is rejected with a NOT NULL violation

  Scenario: transfer requires a status
    Given the ops schema exists
    When I insert into transfer with a null status
    Then the insert is rejected with a NOT NULL violation

  Scenario: transfer requires an initiated_date
    Given the ops schema exists
    When I insert into transfer with a null initiated_date
    Then the insert is rejected with a NOT NULL violation

  Scenario: transfer journal_entry_id must reference a valid journal_entry
    Given the ops schema exists
    When I insert into transfer with journal_entry_id 9999
    Then the insert is rejected with a FK violation

  # --- invoice ---

  Scenario: invoice requires a tenant
    Given the ops schema exists
    When I insert into invoice with a null tenant
    Then the insert is rejected with a NOT NULL violation

  Scenario: invoice requires a fiscal_period_id
    Given the ops schema exists
    When I insert into invoice with a null fiscal_period_id
    Then the insert is rejected with a NOT NULL violation

  Scenario: invoice fiscal_period_id must reference a valid fiscal_period
    Given the ops schema exists
    When I insert into invoice with fiscal_period_id 9999
    Then the insert is rejected with a FK violation

  Scenario: invoice requires rent_amount
    Given the ops schema exists
    When I insert into invoice with a null rent_amount
    Then the insert is rejected with a NOT NULL violation

  Scenario: invoice requires utility_share
    Given the ops schema exists
    When I insert into invoice with a null utility_share
    Then the insert is rejected with a NOT NULL violation

  Scenario: invoice requires total_amount
    Given the ops schema exists
    When I insert into invoice with a null total_amount
    Then the insert is rejected with a NOT NULL violation

  Scenario: invoice tenant and fiscal_period_id must be unique together
    Given an invoice for tenant "Brian" and fiscal_period "2026-03" exists
    When I insert another invoice for tenant "Brian" and fiscal_period "2026-03"
    Then the insert is rejected with a UNIQUE violation
```

---

## Out of Scope

- Application domain types (F# — Project 2)
- Business logic validation (balanced entries, status transitions — Project 2)
- BDD test harness / runner (Project 2)
- API layer (Project 3)
- UI (Project 3)

---

## Open Questions (carried from DataModelSpec)

These do not block Project 1 but should be resolved before Project 2:

1. **Tenant as first-class entity?** Currently a string. YAGNI for now — revisit
   if tenant turnover becomes real.
2. **Audit log for ops status changes?** `modified_at` captures when, not what.
   If needed, an `event_log` table is a future migration — doesn't affect the
   initial schema.

---

## Tech Stack Decisions

- **Language:** F# all the way down, including the Migrations console app
- **Migration tool:** BD to discuss and debate options with Dan before committing
- **.NET 10:** Required. BD to install in the sandbox if not present
- **Other tech decisions:** BD to raise with Dan, not decide unilaterally

---

## Dependencies

- Dan: Create `leobloom_dev` database and Postgres role on sandbox instance (Phase 0)
- Dan: Resolve open questions before Project 2
- Hobson: Write prod COA SQL inserts (outside repo)
- BD: Implement migrations, sample COA, run against `leobloom_dev`
