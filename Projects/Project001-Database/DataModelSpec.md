# Data Model Spec — Draft

Cash basis, double-entry, append-only ledger. PostgreSQL. Schema defined by SQL
migration files; F# types mirror the schema but are not the source of truth.

**Constraint philosophy:** Schema enforces structural integrity only — PK, FK,
UNIQUE, NOT NULL, data types. All business rules and data quality validation
live in the application layer, covered by BDD-style tests.

**Column conventions in this spec:**
- `NOT NULL` is stated explicitly where required. Columns without it are nullable.
- `DEFAULT` values are stated where applicable.
- `FK →` denotes a foreign key reference. All FKs are structurally enforced.

---

## Databases

| Name | Purpose | Who uses it |
|------|---------|-------------|
| `leobloom_prod` | Real financial data | Dan / Hobson (host machine) |
| `leobloom_dev` | Development and test | BD (Docker sandbox) |

Separate Postgres roles per database. Prod credentials not available to BD's
environment. Same migrations run against both databases. Configuration
(connection strings) in `appsettings.{Environment}.json`; secrets (passwords)
injected via environment variables, never in the repo.

---

## Schema: `ledger`

All double-entry bookkeeping tables live here. This is the accounting engine.
Self-contained — no dependencies on any other schema.

### `account_type`

Lookup table for the five fundamental account types in double-entry bookkeeping.

| Column | Type | Notes |
|--------|------|-------|
| `id` | `serial PK` | |
| `name` | `varchar(20) UNIQUE NOT NULL` | `asset`, `liability`, `equity`, `revenue`, `expense` |
| `normal_balance` | `varchar(6) NOT NULL` | `debit` for asset/expense; `credit` for liability/revenue/equity |

Seeded once at DB creation. Five rows, never changes.

---

### `account`

The chart of accounts. See `ChartOfAccountsDraft.md` for the initial set.

| Column | Type | Notes |
|--------|------|-------|
| `id` | `serial PK` | Surrogate key for FK references |
| `code` | `varchar(10) UNIQUE NOT NULL` | Business identifier, e.g. "1010", "4010". Numbering convention (not GAAP-mandated): 1xxx assets, 2xxx liabilities, 3xxx equity, 4xxx revenue, 5xxx investment property expenses, 6xxx personal expenses |
| `name` | `varchar(100) NOT NULL` | e.g. "Fidelity CMA", "Rental Income — Jeffrey" |
| `account_type_id` | `integer NOT NULL FK → account_type.id` | |
| `parent_code` | `varchar(10) FK → account.code` | Hierarchical COA structure. Nullable (top-level accounts have no parent). The account hierarchy serves as the sole classification mechanism — no separate "category" concept |
| `is_active` | `boolean NOT NULL DEFAULT true` | Soft-disable without deleting |
| `created_at` | `timestamptz NOT NULL DEFAULT now()` | |
| `modified_at` | `timestamptz NOT NULL DEFAULT now()` | |

**Invariants (enforced in application layer):**
- `account_type_id` must reference a valid account type.
- The `code` numbering range should be consistent with the account type
  (convention, not hard rule).
- `parent_code`, if set, must reference an existing account.

**Design notes:**
- `normal_balance` lives on `account_type`, not here. It's a property of the
  type (assets increase with debits, revenue increases with credits), not of
  individual accounts. Query via join to `account_type`.
- No `category` column. The COA hierarchy (via `parent_code`) is the sole
  classification mechanism. "Investment property expenses" vs "personal
  expenses" is encoded by the account's position in the tree (5xxx vs 6xxx),
  not by a separate tag. This is how GAAP-informed systems work — the chart
  of accounts IS the taxonomy.
- Surrogate `id` exists because `journal_entry_line.account_id` and other FK
  references use it. If account codes are ever renumbered, FK references don't
  break.

---

### `fiscal_period`

Defines valid fiscal periods. Enables period-close functionality.

| Column | Type | Notes |
|--------|------|-------|
| `id` | `serial PK` | |
| `period_key` | `varchar(7) UNIQUE NOT NULL` | e.g. "2026-03" |
| `start_date` | `date NOT NULL` | First day of the period |
| `end_date` | `date NOT NULL` | Last day of the period |
| `is_open` | `boolean NOT NULL DEFAULT true` | When false, no new entries may be posted to this period (enforced in application layer) |
| `created_at` | `timestamptz NOT NULL DEFAULT now()` | |

**Design notes:**
- Seeded in advance (generate a few years of monthly periods).
- `is_open` enables period close without a separate migration later.

---

### `journal_entry`

A single accounting transaction. Groups one or more line items (debits and
credits) that must balance. Think of it as the header row of an entry — the
date, description, and metadata are shared across all lines.

| Column | Type | Notes |
|--------|------|-------|
| `id` | `serial PK` | |
| `entry_date` | `date NOT NULL` | When the money moved (cash basis). The financial date. |
| `description` | `varchar(500) NOT NULL` | Human-readable: "Jeffrey March rent", "Enbridge gas Feb 2026" |
| `source` | `varchar(50)` | How this entry was created: `manual`, `import`, `invoice`, `agent` |
| `fiscal_period_id` | `integer NOT NULL FK → fiscal_period.id` | |
| `voided_at` | `timestamptz` | Null = active entry. Not null = voided. Replaces a separate `is_void` flag. |
| `void_reason` | `varchar(500)` | Required when `voided_at` is set |
| `created_at` | `timestamptz NOT NULL DEFAULT now()` | When this entry was recorded in Leo Bloom (may differ from `entry_date` if entered retroactively) |
| `modified_at` | `timestamptz NOT NULL DEFAULT now()` | |

**Invariants (enforced in application layer):**
- A journal entry is never deleted. Append-only.
- Voiding sets `voided_at` and `void_reason`. The entry remains in the ledger.
  A correcting entry is posted separately if needed.
- `fiscal_period_id` must reference a period whose date range contains
  `entry_date`.
- `fiscal_period_id` must reference an open period (unless explicitly overridden).
- `void_reason` must be non-empty when `voided_at` is set.

**Design notes:**
- No `reference_id` column. External references (cheque numbers, Zelle
  confirmations, Ally transaction IDs, invoice numbers) are 1:many and live in
  `journal_entry_reference`.
- `voided_at IS NULL` = active entry. Avoids redundant `is_void` boolean.
- `created_at` vs `entry_date`: if Dan enters last Tuesday's transactions on
  Saturday, `entry_date` is Tuesday and `created_at` is Saturday.

---

### `journal_entry_reference`

External reference numbers associated with a journal entry. One entry can have
multiple references (Dan's invoice number, Jeffrey's cheque number, Ally's
transaction ID — all for the same real-world event).

| Column | Type | Notes |
|--------|------|-------|
| `id` | `serial PK` | |
| `journal_entry_id` | `integer NOT NULL FK → journal_entry.id` | |
| `reference_type` | `varchar(30) NOT NULL` | e.g. `invoice`, `cheque`, `zelle_confirmation`, `ally_txn`, `fidelity_txn` |
| `reference_value` | `varchar(200) NOT NULL` | The actual reference number/ID |
| `created_at` | `timestamptz NOT NULL DEFAULT now()` | |

---

### `journal_entry_line`

Individual debit or credit within a journal entry. The actual ledger postings.

A journal entry has one or more lines. Each line debits or credits a single
account for a specific amount. The lines within an entry must balance: total
debits = total credits.

Example — Jeffrey pays $1,000 rent into the CMA:

| Line | Account | Amount | Type |
|------|---------|--------|------|
| 1 | Fidelity CMA (asset) | 1,000.00 | debit |
| 2 | Rental Income — Jeffrey (revenue) | 1,000.00 | credit |

Debit to an asset = balance goes UP. Credit to revenue = balance goes UP.
Both sides increase, both sides balance. This is the "left side / right side"
convention of double-entry — debit means left, credit means right, and whether
that increases or decreases depends on the account type (see `account_type.normal_balance`).

Compound entries are supported. A mortgage payment might have three lines:

| Line | Account | Amount | Type |
|------|---------|--------|------|
| 1 | Mortgage Interest (expense) | 800.00 | debit |
| 2 | Mortgage Principal (liability) | 700.00 | debit |
| 3 | Fidelity CMA (asset) | 1,500.00 | credit |

Three lines, two debits and one credit, total debits = total credits = $1,500.

| Column | Type | Notes |
|--------|------|-------|
| `id` | `serial PK` | |
| `journal_entry_id` | `integer NOT NULL FK → journal_entry.id` | |
| `account_id` | `integer NOT NULL FK → account.id` | |
| `amount` | `numeric(12,2) NOT NULL` | Always positive. Direction determined by `entry_type`. |
| `entry_type` | `varchar(6) NOT NULL` | `debit` or `credit` |
| `memo` | `varchar(300)` | Optional line-level note |

**Invariants (enforced in application layer):**
- `amount` must be > 0. No negative amounts. Direction is `entry_type`.
- For every `journal_entry_id`: `SUM(amount) WHERE entry_type = 'debit'` must
  equal `SUM(amount) WHERE entry_type = 'credit'`. THE fundamental invariant
  of double-entry bookkeeping.
- A journal entry must have at least two lines.
- `entry_type` must be `debit` or `credit`.

---

## Key Mutations

### Post Journal Entry (Project 005)

The write path for creating journal entries. Inserts atomically into three tables
within a single transaction:

1. `journal_entry` — header row (date, description, source, fiscal_period_id)
2. `journal_entry_line` — one row per debit/credit line
3. `journal_entry_reference` — zero or more external reference rows

**Validation (application layer, before any SQL):**
- Pure: line count >= 2, amounts > 0, debits = credits, description non-empty,
  entry_type is debit/credit, source non-empty when provided, reference fields non-empty
- DB-dependent: fiscal period exists and is open, entry_date within period range,
  all accounts exist and are active

**Atomicity:** All-or-nothing. If any insert fails, the transaction rolls back.
No partial state.

**Implementation:** `LeoBloom.Dal.JournalEntryService.post` (production) and
`JournalEntryService.postInTransaction` (test-friendly variant).

---

## Schema: `ops`

Operational tracking — obligations, bills, transfers, invoices. The "nagging
layer." Separate from the ledger but references it.

The `ops` schema knows about the `ledger` schema. The `ledger` schema knows
nothing about `ops`. One-way dependency. The accounting engine is self-contained;
the operational layer is a consumer of it.

### `obligation_agreement`

The terms of a financial obligation — one side of a contractual relationship.
Defines what should happen, how often, and for how much. Instances are spawned
from this for each occurrence.

| Column | Type | Notes |
|--------|------|-------|
| `id` | `serial PK` | |
| `name` | `varchar(100) NOT NULL` | e.g. "Jeffrey rent", "Enbridge gas bill" |
| `obligation_type` | `varchar(20) NOT NULL` | `receivable` or `payable`. DU-backed: `ObligationDirection` in Domain layer. |
| `counterparty` | `varchar(100)` | The non-Dan side of the arrangement: "Jeffrey", "Enbridge", "Rocket Mortgage". Dan is always implicit. |
| `amount` | `numeric(12,2)` | The agreed-upon amount. Null when the agreement doesn't specify a fixed amount (metered utilities, variable-rate). Updated when terms change (escrow adjustment, new contract rate). |
| `cadence` | `varchar(20) NOT NULL` | `monthly`, `quarterly`, `annual`, `one_time`. DU-backed: `RecurrenceCadence` in Domain layer. |
| `expected_day` | `integer` | Day of month/quarter when expected. Nullable for irregular. |
| `payment_method` | `varchar(30)` | `autopay_pull`, `ach`, `zelle`, `cheque`, `bill_pay`, `manual`. Nullable — some agreements don't have a fixed method. DU-backed: `PaymentMethodType` in Domain layer. |
| `source_account_id` | `integer FK → ledger.account.id` | Where money comes from |
| `dest_account_id` | `integer FK → ledger.account.id` | Where money goes to |
| `is_active` | `boolean NOT NULL DEFAULT true` | Soft-disable without deleting |
| `notes` | `text` | |
| `created_at` | `timestamptz NOT NULL DEFAULT now()` | |
| `modified_at` | `timestamptz NOT NULL DEFAULT now()` | |

**Design notes:**
- No `category` column. The linked `source_account_id` / `dest_account_id`
  positions the agreement within the COA hierarchy, which IS the taxonomy.
  "Is this an investment property expense?" = "Does the destination account
  live under the 5xxx subtree?"
- `counterparty` is a free-form string for now. If counterparties multiply or
  need their own attributes (address, payment preferences), promote to a table.
- `amount` is the agreed-upon/contract amount. For fixed agreements (rent,
  mortgage P&I), instances are pre-filled from this. For variable agreements
  (utilities), this is null — the amount is only known when the bill arrives.
  When terms change (escrow adjustment, new lawn care contract), update this
  field. Future instances get the new amount; past instances are untouched.

---

### `obligation_instance`

A specific occurrence of an obligation agreement. "Jeffrey rent — Apr 2026."
This is the thing whose status gets tracked.

| Column | Type | Notes |
|--------|------|-------|
| `id` | `serial PK` | |
| `obligation_agreement_id` | `integer NOT NULL FK → obligation_agreement.id` | |
| `name` | `varchar(100) NOT NULL` | Human-readable label, e.g. "Apr 2026". Combined with the parent agreement's name for display: "Jeffrey rent — Apr 2026" |
| `status` | `varchar(20) NOT NULL` | `expected`, `in_flight`, `confirmed`, `posted`, `overdue`, `skipped`. DU-backed: `InstanceStatus` in Domain layer. |
| `amount` | `numeric(12,2)` | The amount for this specific occurrence. Pre-filled from `obligation_agreement.amount` for fixed agreements. Set when the bill arrives for variable agreements. |
| `expected_date` | `date NOT NULL` | When we expect this to happen |
| `confirmed_date` | `date` | When it actually happened |
| `due_date` | `date` | For payable agreements — when the bill is due. Nullable. |
| `document_path` | `varchar(500)` | Path to bill PDF/scan if applicable. Nullable. |
| `journal_entry_id` | `integer FK → ledger.journal_entry.id` | Links to the ledger entry when posted |
| `notes` | `text` | |
| `is_active` | `boolean NOT NULL DEFAULT true` | Soft-delete |
| `created_at` | `timestamptz NOT NULL DEFAULT now()` | |
| `modified_at` | `timestamptz NOT NULL DEFAULT now()` | |

**Status lifecycle:**
```
expected → in_flight → confirmed → posted
                ↘ overdue
expected → overdue → confirmed → posted
expected → skipped
```

- `expected` — we know it should happen, hasn't yet
- `in_flight` — initiated but not settled (cheque deposited, sweep started, ACH
  pending)
- `confirmed` — money has arrived/departed, amount is known
- `posted` — a journal entry has been created in the ledger
- `overdue` — past the expected date, not yet confirmed
- `skipped` — explicitly waived (with a note explaining why)

**Invariants (enforced in application layer):**
- Unique on `(obligation_id, expected_date)`. One instance per obligation per
  expected date.
- `journal_entry_id` may only be set when status is `posted`.
- `amount` required when status is `confirmed` or `posted`.
- Status transitions must follow the lifecycle diagram above.

**Design notes:**
- No `fiscal_period_id`. The instance is identified by its parent obligation +
  `expected_date`. The linked `journal_entry` carries its own `fiscal_period_id`
  based on when money actually moved (cash basis). These may differ — an
  obligation expected in June could be paid in July, and the journal entry
  belongs to July.
- `due_date` and `document_path` absorb what was previously the separate `bill`
  table. A bill arriving is just an update to the obligation_instance: set
  `amount`, `due_date`, `document_path`, and advance status.
- Partial payments are not modelled. If Jeffrey owes $1,000 and pays $500, handle
  it manually. Revisit if this becomes a real problem.

---

### `transfer`

Money moving between Dan's own accounts. Not income, not expense — asset to
asset. Tracked separately because of the in-flight problem (money that's left
one account but hasn't arrived in another).

| Column | Type | Notes |
|--------|------|-------|
| `id` | `serial PK` | |
| `from_account_id` | `integer NOT NULL FK → ledger.account.id` | |
| `to_account_id` | `integer NOT NULL FK → ledger.account.id` | |
| `amount` | `numeric(12,2) NOT NULL` | |
| `status` | `varchar(20) NOT NULL DEFAULT 'initiated'` | `initiated` or `confirmed` |
| `initiated_date` | `date NOT NULL` | |
| `expected_settlement` | `date` | Typically initiated + 3 business days for ACH |
| `confirmed_date` | `date` | |
| `journal_entry_id` | `integer FK → ledger.journal_entry.id` | Created on confirmation |
| `description` | `varchar(300)` | "Ally → CMA sweep", "Brokerage liquidation" |
| `is_active` | `boolean NOT NULL DEFAULT true` | Soft-delete |
| `created_at` | `timestamptz NOT NULL DEFAULT now()` | |
| `modified_at` | `timestamptz NOT NULL DEFAULT now()` | |

**Invariants (enforced in application layer):**
- `from_account_id` != `to_account_id`.
- Both accounts must be `asset` type.
- Journal entry (debit destination, credit source) created when confirmed, not
  when initiated. In-flight transfers affect no account balances.

**Design notes:**
- Only two statuses (initiated, confirmed), so no lookup table — the overhead
  isn't worth it. If more statuses emerge, promote to a lookup.

---

### `invoice`

Generated tenant invoice for a period. A composite output document — combines
a tenant's fixed rent with their 1/3 share of that period's utility bills.
This is the one ops table that retains a fiscal_period reference, because an
invoice IS a monthly financial document ("here's what you owe for March").

| Column | Type | Notes |
|--------|------|-------|
| `id` | `serial PK` | |
| `tenant` | `varchar(50) NOT NULL` | "Jeffrey", "Alice", "Matthew" |
| `fiscal_period_id` | `integer NOT NULL FK → ledger.fiscal_period.id` | The period this invoice covers |
| `rent_amount` | `numeric(12,2) NOT NULL` | Fixed per tenant (Jeffrey=1000, Alice=700, Matthew=0) |
| `utility_share` | `numeric(12,2) NOT NULL` | 1/3 of total utilities for the period |
| `total_amount` | `numeric(12,2) NOT NULL` | rent + utility_share |
| `generated_at` | `timestamptz NOT NULL DEFAULT now()` | |
| `document_path` | `varchar(500)` | Path to generated invoice PDF/file |
| `notes` | `text` | |
| `is_active` | `boolean NOT NULL DEFAULT true` | Soft-delete |
| `created_at` | `timestamptz NOT NULL DEFAULT now()` | |
| `modified_at` | `timestamptz NOT NULL DEFAULT now()` | |

**Invariants (enforced in application layer):**
- Unique on `(tenant, fiscal_period_id)`.
- `total_amount` = `rent_amount` + `utility_share`.
- An invoice can only be generated when all utility bills for the target month
  have been received. Application checks: all active payable
  `obligation_agreement` rows with variable amounts (null
  `obligation_agreement.amount`) that have destination accounts under the
  investment property expense subtree must have an `obligation_instance`
  with `amount` set and `expected_date` within the target month.

---

## Cross-schema relationships

```
ops.obligation_agreement.source_account_id   →  ledger.account.id
ops.obligation_agreement.dest_account_id     →  ledger.account.id
ops.obligation_instance.journal_entry_id  →  ledger.journal_entry.id
ops.transfer.from_account_id             →  ledger.account.id
ops.transfer.to_account_id               →  ledger.account.id
ops.transfer.journal_entry_id            →  ledger.journal_entry.id
ops.invoice.fiscal_period_id             →  ledger.fiscal_period.id
```

The `ops` schema depends on `ledger`. The `ledger` schema knows nothing about
`ops`. One-way dependency. The accounting engine is self-contained; the
operational layer is a consumer of it.

---

## Key queries the system must support

These inform indexing and should become test cases:

1. **Trial balance**: For a given fiscal period, sum all debit lines and all
   credit lines across all non-void entries. They must be equal.

2. **Account balance**: For a given account, calculate the balance using
   `normal_balance` from `account_type`: sum debits minus credits (for
   normal-debit accounts) or credits minus debits (for normal-credit accounts),
   across all non-void entries up to a given date.

3. **Overdue obligations**: All active `obligation_instance` rows where status =
   `expected` and `expected_date` < today.

4. **Invoice readiness**: For a target month, check that all variable-amount
   investment property payable obligation_instances with `expected_date` in that
   month have `amount` set.

5. **In-flight transfers**: All active `transfer` rows where status =
   `initiated`. Include expected settlement date.

6. **Balance projection**: Current account balance + expected inflows − expected
   outflows − in-flight transfers, rolled forward N days. Computed view, not
   stored.

7. **P&L by account subtree**: Revenue minus expenses for a given period,
   filtered by account hierarchy (walk the `parent_code` tree). Replaces the
   old "P&L by category" query — the COA hierarchy IS the category.

---

## Open questions

1. ~~**Compound journal entries**~~ **Resolved: yes.** The invariant is simply
   debits = credits, regardless of line count. Mortgage splits, multi-account
   transactions, all supported from day one.

2. **Tenant as a first-class entity?** Currently "Jeffrey" is just a string on
   `invoice` and `obligation_agreement.counterparty`. If tenants come and go,
   or if there's ever a fourth, a `tenant` table with lease dates and rent
   amounts might be cleaner. But for three people who aren't going anywhere
   soon, YAGNI may apply.

3. ~~**Fiscal period close**~~ **Resolved:** `fiscal_period.is_open` handles
   this. Application layer enforces that entries can't be posted to closed
   periods.

4. **Audit log**: The journal is inherently an audit log (append-only). But
   the ops tables have mutable status fields
   (`obligation_instance.status`, `transfer.status`). Should we log
   status changes in an `event_log` table? The `modified_at` timestamps
   capture *when* but not *what changed* or *from what*.

5. **Multi-currency**: No. Not now, not ever for this use case. Everything is
   USD. Noted explicitly so nobody future-proofs for it.
