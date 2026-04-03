# LeoBloom Product Backlog

**Product Owner:** Hobson
**Tech Lead:** Basement Dweller
**Business Analyst:** CE (Compound Engineering)
**Stakeholder:** Dan

---

## How to read this file

Epics are capability areas. Stories are the unit of work — each maps to one
project folder (`ProjectNNN/`), one feature branch, one PR. Sized for one BD
session.

**Status:** Backlog → Ready → In Progress → Done
A story is **Ready** when all dependencies are Done and a BRD exists.

**Flow:** Hobson writes the story brief here → CE expands into BRD + BDD doc →
Hobson approves → BD writes feature files → CE plans → BD builds → PR → Dan
reviews.

The summary table in each epic is the at-a-glance view. The subsections below
it are the brief to CE. If something isn't in the brief, CE shouldn't invent
it — ask Hobson.

**Canonical reference:** `Projects/Project001-Database/Specs/DataModelSpec.md`
is the source of truth for schema, column types, invariants, and key queries.
Stories reference it by section. CE should read it before writing any BRD.

---

## Foundation (Projects 001–004, 029)

| Project | Description | Status |
|---------|-------------|--------|
| 001 | Database schema, migrations, seed data, structural constraint specs | Done |
| 002 | Test harness — TickSpec/xUnit, 88 Gherkin scenarios, shared config | Done |
| 003 | BDD infrastructure — FT tags, feature file reorg, DeleteTarget refactor, docs | Done |
| 004 | Domain types — F# types in Domain, business logic BDD in Domain.Tests | Done |
| 029 | Lookup table elimination — replace integer FK lookups with DU-backed strings | Done |

Project 004 delivers F# record/DU types mirroring every schema table, plus pure
validation functions for the fundamental invariants (balance rule, amount
positivity, entry_type values). No persistence. No status machine. Those come in
later stories that build on these types.

### 029 — Eliminate Lookup Tables, Replace with DU-Backed String Columns

**Executive decision (Dan):** F# discriminated unions in the Domain layer are
the source of truth for valid enumerated values. The database should store
human-readable strings, not integer foreign keys to lookup tables. This project
eliminates every pure lookup table and replaces the integer FK columns on
dependent tables with `varchar` columns containing the DU case name as a string.

**Why now:** Projects 005 (Post Journal Entry) and 006 (Void Journal Entry) are
about to build service and persistence layers on top of the current schema. If
we build write paths that resolve integer FKs against lookup tables, then rip
those tables out later, we're rewriting the Dal, the service layer, and every
test that touches them. Do it now while the only consumers are Domain types and
raw SQL migrations.

**What is a "pure lookup table"?** A table that exists solely to assign meaning
to an integer primary key. It has an `id` column, a `name` column, and nothing
else. No business data. No user-created rows. No additional attributes that
serve a normalization purpose. The valid values are fixed by the application and
already defined as F# DUs in the Domain layer.

**Tables to eliminate (4 total):**

| Lookup Table | Schema | Values | Corresponding DU | Dependent Table(s) | FK Column(s) |
|---|---|---|---|---|---|
| `obligation_type` | `ops` | receivable, payable | `Ops.ObligationDirection` | `ops.obligation_agreement` | `obligation_type_id` |
| `obligation_status` | `ops` | expected, in_flight, confirmed, posted, overdue, skipped | `Ops.InstanceStatus` | `ops.obligation_instance` | `status_id` |
| `cadence` | `ops` | monthly, quarterly, annual, one_time | `Ops.RecurrenceCadence` | `ops.obligation_agreement` | `cadence_id` |
| `payment_method` | `ops` | autopay_pull, ach, zelle, cheque, bill_pay, manual | `Ops.PaymentMethodType` | `ops.obligation_agreement` | `payment_method_id` |

**Why `account_type` stays:** `account_type` carries `normal_balance` — that's
a real attribute, not just a name. Without the table, every row in
`ledger.account` would need both `account_type` and `normal_balance` columns,
which is denormalization. The relationship between account type and normal
balance is a domain constant (assets are always normal-debit), but storing it
once in a normalized table is the right call. `account_type` is 3NF, not a
lookup. It stays, along with `account` and `fiscal_period` — those are real
entities with real data.

**Migration strategy (per lookup table):**

1. **Add new column(s):** `ALTER TABLE ... ADD COLUMN <name> varchar(N) NOT NULL DEFAULT '<placeholder>';`
2. **Populate from existing FK:** `UPDATE ... SET <name> = (SELECT name FROM <lookup> WHERE id = <fk_col>);`
3. **Drop the FK constraint:** `ALTER TABLE ... DROP CONSTRAINT ...;`
4. **Drop the old integer column:** `ALTER TABLE ... DROP COLUMN <fk_col>;`
5. **Drop the lookup table:** `DROP TABLE <lookup>;`

Steps 1-5 run in a single migration per lookup table (or one combined migration
if the team prefers).

**Rollback story:** Each migration has a DOWN section that reverses the process:
recreate the lookup table, re-seed it, add the integer column back, populate it
from the string column, re-add the FK constraint, drop the string column. This
is mechanical but must be tested. The BDD should include a rollback scenario or
at minimum verify the DOWN migration compiles.

**What changes in the Domain layer:**

- Remove the record types that mirror eliminated lookup tables:
  `Ops.ObligationType`, `Ops.ObligationStatus`, `Ops.Cadence`,
  `Ops.PaymentMethod` (the `{id: int; name: string}` records).
  `Ledger.AccountType` stays — it mirrors a real table, not a lookup.
- The DU types (`ObligationDirection`, `InstanceStatus`, `RecurrenceCadence`,
  `PaymentMethodType`, `EntryType`, `NormalBalance`) already exist and are
  correct. They become the sole representation.
- Add string conversion functions: `ObligationDirection.toString` /
  `ObligationDirection.fromString` (or a module-level function) for each DU.
  These are the serialization boundary between Domain and Dal.
- Update any record types that currently hold `int` FK fields to hold string
  or DU fields instead. E.g., `ObligationAgreement.obligationTypeId: int`
  becomes `ObligationAgreement.obligationType: ObligationDirection` (or
  `obligationType: string` at the Dal boundary).

**What changes in the Dal layer:**

- Queries that currently `JOIN` on lookup tables become simpler -- just read the
  string column directly.
- Insert/update statements write the string value instead of looking up an
  integer ID.
- Any existing Dal code (currently just `ConnectionString.fs`) is unaffected,
  but future Dal work (Projects 005+) will be built against the new schema.

**What Hobson (the Comptroller) needs to know:**

- `ledger.account` is unchanged — `account_type_id` FK stays, `account_type`
  table stays. Hobson's existing ledger queries keep working.
- `SELECT * FROM ops.obligation_agreement` will show `obligation_type = 'receivable'`,
  `cadence = 'monthly'`, `payment_method = 'ach'` instead of integer IDs.
- `SELECT * FROM ops.obligation_instance` will show `status = 'expected'`
  instead of `status_id = 1`.
- The lookup tables (`obligation_type`, `obligation_status`, `cadence`,
  `payment_method`) will no longer exist. If Hobson has any saved queries that
  join on them, those queries need updating.

**What this does NOT cover:**

- `entry_type` on `journal_entry_line` -- already a `varchar(6)` column
  storing `'debit'`/`'credit'` as strings. No lookup table exists. No change
  needed.
- `transfer.status` -- already a string column (`'initiated'`/`'confirmed'`).
  No lookup table. DataModelSpec explicitly chose not to create one.
- `journal_entry.source` -- already a free-form string. No change needed.
- Creating new DU types that don't already exist -- the DUs are already in
  `Ops.fs` and `Ledger.fs` from Project 004.
- Updating the DataModelSpec -- that is a separate concern. The spec should be
  updated to reflect the new schema, but whether that's part of this project or
  a follow-up is a BRD decision.

**Dependency note for the BA:** Projects 014 (Obligation Agreements), 015
(Spawn Obligation Instances), and 016 (Status Transitions) all reference the
lookup tables being eliminated here. Their backlog descriptions currently
mention `obligation_type_id`, `cadence_id`, `payment_method_id`, and
`status_id`. Those descriptions will need to be read as referring to the
replacement string columns after this project lands. The BA should flag this
when writing BRDs for those stories, but we are NOT updating those backlog
descriptions now -- they were written against the original schema and will be
reinterpreted in context.

**DataModelSpec references:** `obligation_type`, `obligation_status`, `cadence`,
`payment_method` table definitions. `obligation_agreement`, `obligation_instance`
FK columns.

---

## Epic A: Journal Entry Engine

The core write path. Post and void entries. Everything downstream — balances,
reports, projections, obligation posting — depends on this working correctly.
This is the heartbeat of the accounting engine.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 005 | **Post journal entry** | 004, 029 | Done |
| 006 | **Void journal entry** | 005, 029 | Backlog |
| 028 | **Write-level ledger validation** | 005, 006 | Backlog |

### 005 — Post Journal Entry

Create a journal entry with its lines and references, validated and persisted
to `leobloom_dev`.

**The cardinal rule:** For every journal entry, `SUM(amount) WHERE entry_type =
'debit'` must equal `SUM(amount) WHERE entry_type = 'credit'`. This is THE
invariant of double-entry bookkeeping. If this fails, reject the entire entry.
No partial saves.

**Validation (all enforced in application layer, per DataModelSpec):**

- Entry must have at least two lines.
- Every line's `amount` must be > 0. Direction is `entry_type`, not sign.
- `entry_type` must be `'debit'` or `'credit'`.
- `fiscal_period_id` must reference an open period (`is_open = true`).
- `fiscal_period_id` must reference a period whose date range contains
  `entry_date`. (e.g., an entry dated 2026-03-15 must reference the 2026-03
  period, not 2026-04.)
- `account_id` on each line must reference an active account (`is_active = true`).
- `source` should be one of: `'manual'`, `'import'`, `'invoice'`, `'agent'`,
  `'obligation'`, `'transfer'`. Extensible — don't hardcode as an enum in the
  domain, validate as non-empty string.
- `description` is required and non-empty.

**References:** A journal entry can have zero or more `journal_entry_reference`
rows. Each has a `reference_type` (e.g., `'invoice'`, `'cheque'`,
`'zelle_confirmation'`) and a `reference_value`. These are attached at creation
time, not added later (for now — mutability is a future concern).

**Compound entries:** Fully supported from day one. A mortgage payment has three
lines (interest debit, principal debit, cash credit). The invariant is simply
debits = credits regardless of line count. See DataModelSpec example.

**Persistence:** Append-only. A journal entry is never updated or deleted once
persisted. `created_at` is set by the database. `modified_at` is set by the
database and only changes if we ever allow metadata edits (not in scope here).

**Edge cases CE should address in the BRD:**

- What happens if `entry_date` falls in a valid period but that period is closed?
  Reject. The posting engine checks `is_open`.
- What if the same reference_type + reference_value combo already exists on
  another entry? Allow it — the same cheque number might appear on a void and
  its replacement.
- What about accounts that exist but are inactive? Reject. You can't post to a
  deactivated account.
- Can `entry_date` be in the future? Yes — Dan might pre-date an entry for an
  expected payment. The fiscal period just has to exist and be open.
- What if no fiscal period exists for the entry_date? Reject with a clear error.
  Don't auto-create periods.

**DataModelSpec references:** `journal_entry`, `journal_entry_line`,
`journal_entry_reference` tables. Key query #1 (trial balance) and #2 (account
balance) both depend on entries being correctly posted.

---

### 006 — Void Journal Entry

Mark an existing journal entry as void. The entry remains in the ledger
(append-only) but is excluded from all balance and report calculations.

**Mechanics:**

- Set `voided_at` to the current timestamp.
- Set `void_reason` to a non-empty string explaining why.
- `void_reason` is required — a void without a reason is rejected.
- Voiding is idempotent: voiding an already-voided entry is a no-op (return
  success, don't update timestamps or reason).
- No reversing entry is auto-created. If the books need correcting, a new
  journal entry is posted separately. Voiding just removes the original from
  calculations.

**What voiding does NOT do:**

- It does not delete the entry or its lines.
- It does not unlink references.
- It does not cascade to ops. If an `obligation_instance` or `transfer` has
  `journal_entry_id` pointing to this entry, that's the ops layer's problem
  (Epic F / Epic G). The ledger is self-contained.

**Edge cases CE should address in the BRD:**

- Can you void an entry in a closed fiscal period? **Decision needed.** My lean:
  yes. Voiding is a metadata update, not a new posting. The `is_open` gate
  prevents new entries from being posted to the period, but marking an existing
  entry as void doesn't change the period's financial activity — it removes
  activity. If CE disagrees, flag it for Hobson.
- What if someone voids an entry that's the only entry in a period? Fine. The
  period now has zero activity. Trial balance still shows zero = zero.
- Should void_reason have a minimum length? No. Non-empty is sufficient. "Error"
  is a valid reason. Don't nanny.

**DataModelSpec references:** `journal_entry.voided_at`, `journal_entry.void_reason`.
All balance queries (key queries #1, #2) filter on `voided_at IS NULL`.

---

### 028 — Write-Level Ledger Validation

Full validation suite at the service/persistence layer for every write
operation to ledger tables. Domain types (Project 004) define the rules;
this story proves those rules are enforced at the boundary where data
actually gets persisted — not just in isolated unit tests.

**Scope:** Every add or edit that touches `journal_entry`,
`journal_entry_line`, `journal_entry_reference`, or the void fields on
`journal_entry` must run domain validation before persisting. This is the
service layer, not the domain layer — we're testing the integration between
the domain validation functions and the persistence code that calls them.

**What this covers:**

- All validations from Story 005 (post journal entry) exercised through the
  service/persistence write path: balance rule, minimum two lines, amount
  positivity, entry_type values, fiscal period open/date-range checks,
  account active checks, non-empty description, non-empty source.
- All validations from Story 006 (void journal entry) exercised through the
  service/persistence write path: non-null/non-whitespace void_reason,
  idempotent void behavior.
- **Specifically:** `IsNullOrWhiteSpace` validation on `void_reason` must be
  tested at the write level. An isolated domain unit test proves the
  validation function works. This story proves the service layer actually
  calls it before persisting. If someone bypasses or forgets to wire the
  check, this catches it.
- Reference validation: `journal_entry_reference` rows attached during
  creation are validated (non-empty type and value).

**What this does NOT cover:**

- Read operations (balance queries, reports) — those are Epic B/D.
- New domain validation rules — Project 004 owns the rules. This story owns
  the proof that the write path calls them.
- Database-level constraints (CHECK, NOT NULL, FK) — those are Project 001's
  territory. This story is about the application layer rejecting bad data
  before it even hits the database.
- Ops tables (obligations, transfers) — those are Epic E/F.

**Edge cases CE should address in the BRD:**

- What happens when a valid domain object is constructed but the service
  layer skips validation before persisting? This story should make that
  scenario impossible to miss.
- Whitespace-only void_reason (`"   "`, `"\t"`, `"\n"`) must be rejected
  identically to null/empty. The write path must call the same
  `IsNullOrWhiteSpace` check the domain exposes.
- Validation error messages: the service layer should surface the domain
  validation errors clearly, not swallow them into a generic persistence
  failure.

**DataModelSpec references:** `journal_entry`, `journal_entry_line`,
`journal_entry_reference` tables. `journal_entry.voided_at`,
`journal_entry.void_reason`.

---

## Epic B: Balance Calculation

The core read path. Every report, projection, and integration depends on being
able to answer "what is the balance of account X at date Y?" If this is wrong,
everything downstream is wrong.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 007 | **Account balance** | 005 | Backlog |
| 008 | **Trial balance** | 007 | Backlog |

### 007 — Account Balance

Calculate the balance of a single account as of a given date.

**The formula (from DataModelSpec key query #2):**

- Look up the account's `account_type` → `normal_balance` (`'debit'` or `'credit'`).
- Sum all `journal_entry_line` rows for this account where:
  - The parent `journal_entry.voided_at IS NULL` (exclude voided).
  - The parent `journal_entry.entry_date <= target_date`.
- For **normal-debit** accounts (asset, expense):
  `balance = SUM(debit amounts) - SUM(credit amounts)`
- For **normal-credit** accounts (liability, revenue, equity):
  `balance = SUM(credit amounts) - SUM(debit amounts)`
- Result is a signed decimal. Positive = balance in the normal direction.
  Negative = balance is inverted (e.g., an asset account with more credits than
  debits — an overdraft).

**This is a single-account calculation.** Parent accounts do NOT automatically
include children. If account 5000 (Investment Property Expenses) is a parent of
5100 (Mortgage), calling balance on 5000 returns only lines posted directly to
5000. Subtree aggregation is Epic D (P&L by subtree). Keep them separate.

**Inputs:** `account_id` (or `account_code`) + `as_of_date`.
**Output:** Decimal balance + the account's `normal_balance` direction for context.

**Edge cases CE should address in the BRD:**

- Account with no entries → balance is 0.00.
- Account that is inactive → still calculate. `is_active` controls whether you
  can *post* to it, not whether it has a balance.
- `as_of_date` in the future → valid. Returns balance of all entries up to that
  date (there probably won't be future-dated entries, but don't reject).
- Account that doesn't exist → error, not zero.

**DataModelSpec references:** Key query #2, `account_type.normal_balance`.

---

### 008 — Trial Balance

For a fiscal period, produce a trial balance report and verify system integrity.

**The trial balance (from DataModelSpec key query #1):**

- For a given `fiscal_period_id`, find all `journal_entry` rows where
  `fiscal_period_id` matches and `voided_at IS NULL`.
- Sum all their `journal_entry_line` rows:
  - Total debits = `SUM(amount) WHERE entry_type = 'debit'`
  - Total credits = `SUM(amount) WHERE entry_type = 'credit'`
- **Total debits must equal total credits.** If they don't, the system has a bug.
  This is not a user error — it means the posting engine (Story 005) has a defect.

**The report:** Beyond the integrity check, produce a useful output:

- Each account that has activity in the period, with:
  - Account code, name, type
  - Debit total for the period
  - Credit total for the period
  - Net balance (using the normal_balance formula from Story 007)
- Grouped by account type (assets, liabilities, equity, revenue, expenses)
- Subtotals per group
- Grand total debits and credits at the bottom

**Inputs:** `fiscal_period_id` (or `period_key` like `'2026-03'`).
**Output:** The report structure above + a boolean `is_balanced`.

**Edge cases CE should address in the BRD:**

- Period with no entries → balanced (0 = 0), empty report.
- Period that is closed → still runs. Trial balance is a read operation.
- Should this include a "balance as of period end" (cumulative) or just "activity
  in this period"? **Both are useful.** Activity-in-period is the trial balance.
  Cumulative balance is the balance sheet (Story 012). The BRD should be explicit
  about which this is: **activity in the period only.**

**DataModelSpec references:** Key query #1.

---

## Epic C: Fiscal Period Management

Period-close discipline. Once the books for March are reconciled, close March so
nothing new can be posted to it. Reopening is allowed but deliberate.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 009 | **Close / reopen fiscal period** | 005 | Backlog |
| 010 | **Opening balances** | 005, 007 | Backlog |

### 009 — Close / Reopen Fiscal Period

Toggle `fiscal_period.is_open` and enforce the consequences.

**Close:** Set `is_open = false`. From this point:
- The journal entry posting engine (Story 005) rejects any new entry targeting
  this period.
- Voiding entries in the closed period is still allowed (see Story 006 edge
  case discussion — voiding is metadata, not a new posting).
- Trial balance and all read operations still work.

**Reopen:** Set `is_open = true`. This should require an explicit reason (logged
somewhere — even just a note field or an event). Reopening is not casual; it
means the books were wrong and need correction.

**Edge cases CE should address in the BRD:**

- Close a period that has no entries → valid. An empty closed period is fine.
- Close a period that still has in-flight obligations (ops side) → **not our
  problem at the ledger layer.** The ledger doesn't know about ops. If the ops
  layer needs to warn about this, that's Epic E's concern.
- Reopen a period, post a correction, close again → valid workflow. No limit on
  transitions.
- What about the `fiscal_period` table itself — can Dan add new periods? Not in
  this story. Periods are seeded by migration (36 months). Adding more is a
  future migration. Keep it simple.

**DataModelSpec references:** `fiscal_period.is_open`.

---

### 010 — Opening Balances

Bootstrap the system with initial account balances. This is the go-live moment
for the ledger.

**How it works:** Opening balances are just a journal entry. No special
mechanism — the double-entry system handles it natively.

For each account with a non-zero starting balance:
- If the account is normal-debit (asset, expense): debit the account.
- If the account is normal-credit (liability, revenue, equity): credit the account.
- The other side of every line goes to an "Opening Balance Equity" account
  (a 3xxx equity account — must exist in the COA).
- The entry balances because: all the account-side lines sum to X, and the
  Opening Balance Equity line is X on the opposite side.

**This is a one-time operation.** Run it once at go-live. If there's a mistake,
void the opening balance entry and create a new one.

**Inputs:** A list of `(account_id, balance_amount)` pairs + the `entry_date`
(probably the first day of the first fiscal period, e.g., 2026-04-01).

**Validation:**
- All accounts must be active.
- The fiscal period for entry_date must exist and be open.
- The resulting journal entry must balance (the Opening Balance Equity line is
  computed, not provided — it's the plug that makes it balance).
- The Opening Balance Equity account must exist in the COA.

**Edge cases CE should address in the BRD:**

- What if some accounts don't have opening balances? Leave them out. Zero-balance
  accounts don't need lines.
- What if Opening Balance Equity ends up with a non-zero balance after this?
  That's expected — it represents the net worth at go-live. Over time, as real
  entries accumulate, it becomes less significant.
- Can this be run more than once? Technically yes (it's just a journal entry),
  but the BRD should note it's designed as a one-time operation. Running it
  twice would double the balances. Void the first one before creating a second.

**Dependency on Story 007:** We need account balance calculation to *verify* the
opening balances are correct after posting. Post the entry (005), then check
each account's balance (007) matches the intended starting state.

---

## Epic D: Financial Statements

The reports Dan actually wants to look at. "How much did the property make this
month?" "What's my net worth in the property accounts?" These are the payoff
for all the engine work.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 011 | **Income statement** | 008 | Backlog |
| 012 | **Balance sheet** | 008 | Backlog |
| 013 | **P&L by account subtree** | 011 | Backlog |

### 011 — Income Statement

Revenue minus expenses for a fiscal period. The "did we make money?" report.

**Structure:**

```
Revenue (4xxx accounts)
  Rental Income — Brian          1,000.00
  Rental Income — Alex             700.00
  ...
  ─────────────────────────────
  Total Revenue                  X,XXX.XX

Expenses (5xxx + 6xxx accounts)
  Mortgage Interest                800.00
  Property Insurance               125.00
  ...
  ─────────────────────────────
  Total Expenses                 X,XXX.XX

Net Income                       X,XXX.XX
```

**Mechanics:**
- Filter to revenue accounts (type = `'revenue'`) and expense accounts
  (type = `'expense'`).
- For each account, calculate the balance for the period (sum of activity in
  that period only, not cumulative — unlike account balance which is as-of-date).
- Revenue balances are positive when credits > debits (normal for revenue).
- Expense balances are positive when debits > credits (normal for expenses).
- Net Income = Total Revenue - Total Expenses.

**Important distinction:** This is period activity, not cumulative balance.
"Revenue in March" means credits to revenue accounts from entries in the March
fiscal period. Story 007 gives cumulative balance; this story needs
period-specific activity.

**Edge cases CE should address in the BRD:**

- Period with no revenue or no expenses → show the section with zero total.
- Accounts with zero activity in the period → omit from the report (don't clutter
  with zero rows).
- Should inactive accounts with activity appear? Yes — they had activity when
  they were active. Show them.

**DataModelSpec references:** Account types, key query #7 (P&L by subtree is
the filtered version of this).

---

### 012 — Balance Sheet

Assets, liabilities, and equity at a point in time. The "what do we own and
owe?" report.

**Structure:**

```
Assets (1xxx accounts)
  Fidelity CMA                  XX,XXX.XX
  Ally Checking                   X,XXX.XX
  ...
  ─────────────────────────────
  Total Assets                  XX,XXX.XX

Liabilities (2xxx accounts)
  Mortgage Principal            XXX,XXX.XX
  ...
  ─────────────────────────────
  Total Liabilities             XX,XXX.XX

Equity (3xxx accounts)
  Opening Balance Equity         XX,XXX.XX
  Retained Earnings              XX,XXX.XX  ← computed
  ─────────────────────────────
  Total Equity                  XX,XXX.XX

Total Liabilities + Equity      XX,XXX.XX
```

**The accounting equation:** Assets = Liabilities + Equity. If this doesn't
hold, the system has a bug.

**Retained Earnings:** This is the tricky part. In a formal system, revenue and
expense accounts are "closed" to retained earnings at period-end via closing
entries. We are NOT doing closing entries — that's unnecessary ceremony for this
use case. Instead, **compute retained earnings dynamically:**

`Retained Earnings = All-time revenue account balances - All-time expense account balances`

This is the cumulative net income since inception. It appears as a line in the
equity section. The balance sheet then balances because retained earnings
absorbs the revenue/expense activity.

**Inputs:** `as_of_date`. This is a point-in-time snapshot, not a period report.
**Output:** The report structure above + a boolean `is_balanced` (Assets = L + E).

**Edge cases CE should address in the BRD:**

- Balance sheet as of a date before any entries → all zeros, balanced.
- Retained earnings is negative (expenses > revenue) → show as negative. This is
  normal for a property that's still in early months with startup costs.
- Include inactive accounts with balances? Yes — they still represent real
  assets/liabilities.

---

### 013 — P&L by Account Subtree

The filtered income statement. "How much did the investment property cost me?"
vs "How much did I spend personally?" This is DataModelSpec key query #7.

**Mechanics:**

Walk the `parent_code` hierarchy starting from a given root account code.
Collect all descendant accounts. Produce an income statement (Story 011) filtered
to only those accounts.

**Example:** P&L rooted at 5000 (Investment Property Expenses) shows only
accounts under the 5xxx subtree. P&L rooted at 6000 (Personal Expenses) shows
only 6xxx. P&L rooted at 4000 (Revenue) shows all rental income.

**The subtree walk:** Account 5000 has children 5100, 5200, etc. Account 5100
might have children 5110, 5120. The walk collects all descendants recursively
via `parent_code`. This is a tree traversal, not a prefix match — even though
the numbering convention happens to align, the walk uses the FK relationship,
not string matching on codes.

**Inputs:** `root_account_code` + `fiscal_period_id`.
**Output:** Income statement structure filtered to the subtree.

**Edge cases CE should address in the BRD:**

- Root account that has no children → report contains only that account's activity.
- Root account that is a revenue account → works fine, produces a revenue-only P&L.
- Can you root at a non-revenue, non-expense account (e.g., an asset)? Technically
  yes, but the output wouldn't be a meaningful P&L. The BRD should document this
  as "supported but not the intended use case."
- Depth limit? No. The COA is shallow (2-3 levels). Don't over-engineer.

---

## Epic E: Obligation Lifecycle

The ops engine. Agreements define the contractual terms ("Brian owes $1,000 rent
monthly"). Instances track each occurrence ("Brian rent — Apr 2026, expected").
The status machine tracks reality ("received? posted to ledger?").

The ops schema depends on the ledger schema. The ledger knows nothing about ops.
One-way dependency.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 014 | **Obligation agreements** | 004 | Backlog |
| 015 | **Spawn obligation instances** | 014 | Backlog |
| 016 | **Status transitions** | 015 | Backlog |
| 017 | **Overdue detection** | 016 | Backlog |

### 014 — Obligation Agreements

CRUD for obligation agreements. These are the standing arrangements: "Brian pays
$1,000 rent monthly," "Enbridge sends a gas bill monthly (variable amount),"
"Property insurance is $1,500 annually."

**Create:** Persist an `obligation_agreement` with all fields from the
DataModelSpec. Validate:

- `obligation_type_id` must reference a valid obligation_type (receivable or
  payable).
- `cadence_id` must reference a valid cadence.
- `payment_method_id` (if set) must reference a valid payment_method.
- `source_account_id` (if set) must reference an active ledger account.
- `dest_account_id` (if set) must reference an active ledger account.
- `amount` is nullable — null means variable (metered utilities, etc.).
- `counterparty` is a free-form string. No validation beyond non-empty for
  readability.

**Update:** All fields are mutable. When `amount` changes (e.g., escrow
adjustment, new contract rate), future instances get the new amount. Past
instances are untouched — they represent what actually happened.

**Deactivate:** Set `is_active = false`. Never delete. Existing instances remain.

**Edge cases CE should address in the BRD:**

- Agreement with no source or dest account → valid. Some agreements are tracked
  for nagging purposes before the accounting is wired up.
- Agreement where source and dest are the same account → reject? Or allow?
  **Decision:** reject. That's meaningless.
- Changing obligation_type (receivable ↔ payable) on an existing agreement with
  instances → allowed, but the BRD should note that existing instances retain
  their semantics. This is a "you know what you're doing" operation.

**DataModelSpec references:** `obligation_agreement` table, all columns.

---

### 015 — Spawn Obligation Instances

From an agreement + a date range, generate the individual instances that will
be tracked through the status lifecycle.

**Mechanics:**

- For a `monthly` agreement with `expected_day = 1` over range 2026-04 to
  2026-06, generate three instances:
  - "Apr 2026" — expected_date 2026-04-01
  - "May 2026" — expected_date 2026-05-01
  - "Jun 2026" — expected_date 2026-06-01
- For `quarterly`: one per quarter. For `annual`: one per year. For `one_time`:
  exactly one instance.
- Each instance starts with `status_id` = `expected`.
- Fixed-amount agreements (`amount IS NOT NULL`): pre-fill `instance.amount`
  from `agreement.amount`.
- Variable-amount agreements (`amount IS NULL`): leave `instance.amount` null.
  It gets set when the bill arrives (status transition to confirmed).
- Instance `name` = period label, e.g., "Apr 2026". Combined with the agreement
  name for display: "Brian rent — Apr 2026".

**Unique constraint (application layer):** No two instances for the same
agreement with the same `expected_date`. If you try to spawn instances for a
range that already has some, skip the existing ones and only create the missing
ones. Don't fail the whole batch.

**Edge cases CE should address in the BRD:**

- Spawning for a range that's entirely already covered → no-op, success.
- Agreement with no `expected_day` → use the first of the period (day 1 for
  monthly, first day of quarter, Jan 1 for annual).
- `expected_day = 31` in a month with 30 days → use last day of month.
- Variable-amount instances: `amount` is null, `due_date` is null,
  `document_path` is null. All get set when the bill arrives. The BRD should
  describe the "bill arrival" update as a separate operation from status
  transition (set amount/due_date/document_path, then transition status).
- One-time agreements: generate exactly one instance regardless of date range.
  If it already exists, no-op.

**DataModelSpec references:** `obligation_instance` table, status lifecycle
diagram.

---

### 016 — Status Transitions

Enforce the obligation instance lifecycle state machine. This is the rulebook
for how obligations move through their life.

**The state machine (from DataModelSpec):**

```
expected → in_flight → confirmed → posted
                ↘ overdue
expected → overdue → confirmed → posted
expected → skipped
```

**Valid transitions and their requirements:**

| From | To | Required |
|------|-----|----------|
| expected | in_flight | — |
| expected | overdue | (set automatically by Story 017, or manually) |
| expected | skipped | `notes` should explain why (soft requirement — warn, don't reject) |
| in_flight | confirmed | `amount` must be set, `confirmed_date` must be set |
| in_flight | overdue | — |
| overdue | confirmed | `amount` must be set, `confirmed_date` must be set |
| confirmed | posted | `journal_entry_id` must be set (handled by Story 018) |

**Invalid transitions are rejected.** No going backwards. If something was
confirmed incorrectly, the corrective action is: void the journal entry (if
posted), create a new instance, and skip the bad one with a note.

**Field updates on transition:**

- `→ confirmed`: set `confirmed_date`, set `amount` (if variable), update
  `modified_at`.
- `→ posted`: set `journal_entry_id`, update `modified_at`.
- `→ skipped`: set `is_active = false`, update `modified_at`.

**Edge cases CE should address in the BRD:**

- Transition on an inactive instance → reject. Skipped/deactivated instances
  are terminal.
- Setting `amount` on a fixed-amount instance at confirmation → allow it. The
  actual amount might differ from the expected amount (partial payment, late
  fee). The agreement amount is the expectation; the instance amount is reality.
- Can `confirmed_date` differ from `expected_date`? Yes — rent expected on the
  1st might arrive on the 3rd. That's normal.
- Should transitions emit events? **Not yet.** DataModelSpec open question #4
  (audit log) is beyond the horizon. `modified_at` captures when. If we need
  "what changed," that's a future epic.

**DataModelSpec references:** `obligation_instance` table, status lifecycle
diagram, invariants section.

---

### 017 — Overdue Detection

Identify obligations that are past due. This is the nagging agent's primary
input signal.

**The query (from DataModelSpec key query #3):**

All active `obligation_instance` rows where:
- `status_id` references `expected` or `in_flight`
- `expected_date < today`
- `is_active = true`

**Output per overdue instance:**

- Agreement name + instance name (e.g., "Brian rent — Apr 2026")
- Counterparty
- Expected amount (from agreement or instance)
- Expected date
- Days overdue (`today - expected_date`)
- Current status (expected vs in_flight — in_flight means it's been initiated
  but hasn't settled, which is a different kind of overdue)

**This story also includes the automatic `expected → overdue` transition.** When
overdue detection runs, any instance that qualifies should have its status
updated to `overdue` (unless it's `in_flight` — those stay `in_flight` but
appear in the overdue report with a flag).

**Edge cases CE should address in the BRD:**

- Instance with expected_date = today → NOT overdue. Overdue means past due.
  Strictly `expected_date < today`.
- Variable-amount instance that's overdue but has no amount → still overdue.
  The amount being unknown doesn't exempt it.
- Should this be a one-shot query or a scheduled process? **Both.** The domain
  function is a query. The nagging agent (beyond the horizon) will call it on a
  schedule. This story builds the query and the auto-transition, not the
  scheduling.

**DataModelSpec references:** Key query #3.

---

## Epic F: Obligation → Ledger Posting

The bridge between ops and the accounting engine. When an obligation is
confirmed and ready to post, this creates the journal entry that records it in
the books.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 018 | **Post obligation to ledger** | 005, 016 | Backlog |

### 018 — Post Obligation to Ledger

When an obligation instance transitions to `posted`, create a journal entry from
the agreement's terms and link it back.

**Mechanics:**

1. Instance must be in `confirmed` status with `amount` set and `confirmed_date`
   set.
2. Look up the parent `obligation_agreement` for `source_account_id` and
   `dest_account_id`.
3. Determine debit/credit sides:
   - **Receivable** (money coming in): debit `dest_account_id` (the asset
     receiving cash), credit `source_account_id` (the revenue account).
   - **Payable** (money going out): debit `dest_account_id` (the expense
     account), credit `source_account_id` (the asset paying cash).
4. Create a journal entry via Story 005's posting engine:
   - `entry_date` = `instance.confirmed_date` (cash basis — when the money moved)
   - `description` = `"{agreement.name} — {instance.name}"`
   - `source` = `'obligation'`
   - `fiscal_period_id` = the period containing `confirmed_date`
   - Two lines: one debit, one credit, both for `instance.amount`
5. Set `instance.journal_entry_id` to the new entry's ID.
6. Transition instance status to `posted`.

**This reuses the posting engine.** All of Story 005's validation applies: the
fiscal period must be open, accounts must be active, the entry must balance.
If any validation fails, the post-to-ledger operation fails and the instance
stays in `confirmed` status.

**Edge cases CE should address in the BRD:**

- Agreement with no source or dest account → cannot post. Reject with clear
  error ("agreement missing source/dest account, cannot create journal entry").
- Source or dest account is inactive → rejected by posting engine. The agreement
  needs updating before this instance can be posted.
- Fiscal period for confirmed_date is closed → rejected by posting engine. Reopen
  the period first.
- Should this auto-create references? E.g., if the instance has a
  `document_path` (bill scan), should the journal entry get a reference? **Yes —
  create a reference with `reference_type = 'obligation'` and `reference_value =
  instance.id`** so there's a traceable link from the ledger back to ops.

**DataModelSpec references:** `obligation_instance.journal_entry_id`, cross-schema
relationships section. The ops→ledger one-way dependency.

---

## Epic G: Transfer Management

Asset-to-asset moves. When Dan sweeps money from Ally to the CMA, or liquidates
a brokerage position, the money is in transit for a few days. This epic tracks
that in-flight state and creates the journal entry when it settles.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 019 | **Transfers** | 005, 007 | Backlog |

### 019 — Create and Confirm Transfers

**Initiate a transfer:**

- Create a `transfer` record with `status = 'initiated'`.
- Both `from_account_id` and `to_account_id` must reference active ledger
  accounts of type `asset`. No transferring from a revenue account.
- `from_account_id` != `to_account_id`.
- `initiated_date` = when the transfer was started.
- `expected_settlement` = `initiated_date` + 3 business days (default for ACH).
  Nullable — some transfers settle instantly.
- **No journal entry is created.** In-flight money hasn't arrived yet. It doesn't
  affect balances.

**Confirm a transfer:**

- Set `status = 'confirmed'`, `confirmed_date` = when the money arrived.
- Create a journal entry via Story 005's posting engine:
  - Debit `to_account_id` (money arriving increases the asset).
  - Credit `from_account_id` (money leaving decreases the asset).
  - `entry_date` = `confirmed_date`.
  - `source` = `'transfer'`.
  - `description` = `transfer.description` or auto-generate.
- Set `transfer.journal_entry_id` to the new entry's ID.

**In-flight tracking (DataModelSpec key query #5):**

All active `transfer` rows where `status = 'initiated'`. This feeds into
balance projection (Epic I) — in-flight transfers reduce available cash even
though they haven't posted to the ledger yet.

**Edge cases CE should address in the BRD:**

- Confirm a transfer that was never initiated → reject. Must be `initiated`.
- Cancel a transfer? **Use `is_active = false`.** Set it inactive with a note.
  No status rollback.
- Transfer where from_account has insufficient balance → allow it. The ledger
  is a record of what happened, not a constraint engine. If the CMA goes
  negative, that's a real problem Dan deals with — Leo Bloom just records it.
- Should a reference be created linking the journal entry back to the transfer?
  **Yes — `reference_type = 'transfer'`, `reference_value = transfer.id`.**

**DataModelSpec references:** `transfer` table, key query #5.

---

## Epic H: Invoice Generation

The tenant-facing output. Each month, Dan needs to tell each tenant what they
owe: fixed rent + their 1/3 share of that month's utility bills.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 020 | **Invoice readiness** | 015, 017 | Backlog |
| 021 | **Generate invoice** | 020 | Backlog |

### 020 — Invoice Readiness Check

Before generating invoices for a month, verify that all the inputs are ready.

**The check (from DataModelSpec key query #4):**

For a target month (fiscal_period):
1. Find all active `obligation_agreement` rows where:
   - `obligation_type` = payable
   - `amount IS NULL` (variable — these are the utility bills)
   - `dest_account_id` is under the investment property expense subtree (5xxx).
     Determined by walking `parent_code` up to a 5xxx root.
2. For each, find the corresponding `obligation_instance` with `expected_date`
   in the target month.
3. Each instance must have `amount` set (the bill has arrived and the amount is
   known).

**Output:**
- `ready: true/false`
- If not ready: list of unready obligations (agreement name, counterparty,
  missing data — usually "amount not set, bill not received")

**Edge cases CE should address in the BRD:**

- No variable-amount obligations exist → always ready (rent-only invoices).
- Some instances don't exist yet (not spawned) → not ready. The instance must
  exist AND have an amount.
- An obligation is overdue (bill never came) → not ready. The invoice can't be
  generated until the amount is known, even if it's late.
- What about fixed-amount payable obligations (e.g., insurance)? They don't
  block readiness — their amount is known from the agreement.

**DataModelSpec references:** Key query #4, invoice invariants.

---

### 021 — Generate Invoice

Create the invoice record for a tenant + fiscal period.

**Mechanics:**

1. Verify readiness (Story 020). If not ready, reject.
2. For the target `fiscal_period_id`, calculate total utilities:
   - Sum the `amount` of all confirmed/posted `obligation_instance` rows for
     variable-amount payable agreements with dest accounts under 5xxx, with
     `expected_date` in the target month.
3. `utility_share` = total_utilities / 3 (three tenants, equal split).
4. `rent_amount` = from the tenant's receivable agreement (the agreement where
   `counterparty` = tenant name, `obligation_type` = receivable, `cadence` =
   monthly). This is the fixed amount from the agreement.
5. `total_amount` = `rent_amount` + `utility_share`.
6. Persist the `invoice` record.

**Unique constraint:** One invoice per `(tenant, fiscal_period_id)`. Reject
duplicates.

**Current tenants (from DataModelSpec):** Brian, Alex, Justin. Justin's rent is
$0 (living arrangement — the invoice still exists for the utility share).

**Edge cases CE should address in the BRD:**

- Utility share results in fractional cents (e.g., $150.01 / 3) → round to
  nearest cent. Rounding differences (total of rounded shares != original total)
  are a real thing. **Decision:** round each share normally. Accept the
  penny discrepancy. Don't over-engineer.
- Justin's rent is $0 but utility share is non-zero → valid invoice. `total_amount`
  = `utility_share`.
- What if a tenant has no receivable agreement? → error. Every tenant must have
  an agreement for invoicing to work.
- Regenerating an invoice (same tenant + period) → reject. Void the existing
  invoice (set `is_active = false`) and create a new one if needed.
- `document_path` is null at creation time. PDF generation is beyond the horizon.

**DataModelSpec references:** `invoice` table, all columns and invariants.

---

## Epic I: Balance Projection

"Will I have enough in the CMA to cover next month's mortgage?" This is the
forward-looking view — current reality plus expected future activity.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 022 | **Balance projection** | 007, 015, 019 | Backlog |

### 022 — Balance Projection

Compute projected balance for an account over a future date range.

**The formula (from DataModelSpec key query #6):**

```
projected_balance(account, date) =
    current_balance(account, today)                           # Story 007
  + expected_inflows(account, today → date)                   # receivable instances
  − expected_outflows(account, today → date)                  # payable instances
  − in_flight_transfers_out(account, today → date)            # initiated, from_account = this
  + in_flight_transfers_in(account, today → date)             # initiated, to_account = this
```

**Expected inflows:** Active `obligation_instance` rows where:
- Parent agreement is `receivable`
- Parent agreement's `dest_account_id` = the target account
- Instance `status` is `expected` or `in_flight`
- Instance `expected_date` is between today and the projection date

**Expected outflows:** Same logic for `payable` agreements where
`source_account_id` = the target account.

**In-flight transfers:** Active `transfer` rows where `status = 'initiated'`
and either `from_account_id` or `to_account_id` = the target account.

**Inputs:** `account_id` (or code) + `projection_date` (how far forward).
**Output:** A daily or periodic series showing the projected balance at each
point. Not just the final number — Dan wants to see the curve.

**This is computed, not stored.** Every call recalculates from current state.

**Edge cases CE should address in the BRD:**

- Variable-amount obligations with null amount → flag as uncertainty. Show them
  in the output as "unknown outflow on [date]" rather than omitting them.
  Don't guess the amount.
- Account with no future obligations or transfers → flat line at current balance.
- Multiple obligations hitting on the same day → sum them. Show the itemized
  breakdown and the net effect.
- Projection date in the past → reject or return current balance? **Reject.** Use
  account balance (Story 007) for historical queries.

**DataModelSpec references:** Key query #6.

---

## Epic J: API Layer

REST endpoints via Giraffe or Falco (F# web frameworks). Thin pass-through to
the domain layer — all validation and business logic lives in Domain, not in
controllers.

No authentication for now. Dan is the only user. Add auth when there's a UI
or external integration that warrants it.

| Project | Story | Depends On | Status |
|---------|-------|------------|--------|
| 023 | **Journal entry endpoints** | 005, 006 | Backlog |
| 024 | **Reporting endpoints** | 011, 012, 013 | Backlog |
| 025 | **Obligation endpoints** | 018 | Backlog |
| 026 | **Transfer & invoice endpoints** | 019, 021 | Backlog |
| 027 | **Projection endpoint** | 022 | Backlog |

### 023 — Journal Entry Endpoints

- `POST /api/journal-entries` — create with lines + references. Request body
  mirrors Story 005 inputs. Returns the created entry with ID.
- `GET /api/journal-entries/{id}` — full entry with lines and references.
- `GET /api/journal-entries?period={period_key}&account={code}` — list entries,
  filterable by fiscal period and/or account. Paginated.
- `PATCH /api/journal-entries/{id}/void` — void an entry. Body: `{ "reason": "..." }`.

**Error responses:** Domain validation errors (unbalanced entry, closed period,
inactive account) return 422 with a structured error body describing what failed.
Not found returns 404. Malformed requests return 400.

### 024 — Reporting Endpoints

- `GET /api/accounts/{code}/balance?as_of={date}` — Story 007.
- `GET /api/reports/trial-balance?period={period_key}` — Story 008.
- `GET /api/reports/income-statement?period={period_key}` — Story 011.
- `GET /api/reports/balance-sheet?as_of={date}` — Story 012.
- `GET /api/reports/pnl?root={account_code}&period={period_key}` — Story 013.

All read-only. No auth concerns beyond preventing accidental writes.

### 025 — Obligation Endpoints

- `POST /api/obligations/agreements` — create agreement.
- `GET /api/obligations/agreements` — list, filterable by type/active/counterparty.
- `PUT /api/obligations/agreements/{id}` — update.
- `POST /api/obligations/agreements/{id}/spawn` — spawn instances for a date range.
- `GET /api/obligations/instances` — list, filterable by agreement/status/date range.
- `PATCH /api/obligations/instances/{id}/transition` — status transition. Body:
  `{ "to": "confirmed", "amount": 1000.00, "confirmed_date": "2026-04-03" }`.
- `POST /api/obligations/instances/{id}/post` — post to ledger (Story 018).
- `GET /api/obligations/overdue` — Story 017 query.

### 026 — Transfer & Invoice Endpoints

- `POST /api/transfers` — initiate.
- `PATCH /api/transfers/{id}/confirm` — confirm + create journal entry.
- `GET /api/transfers?status=initiated` — in-flight list.
- `GET /api/invoices/readiness?period={period_key}` — Story 020.
- `POST /api/invoices/generate` — generate for tenant + period.
- `GET /api/invoices?tenant={name}&period={period_key}` — list/filter.

### 027 — Projection Endpoint

- `GET /api/accounts/{code}/projection?through={date}` — Story 022.

Returns a time series with daily/periodic data points and flagged uncertainties
(variable-amount obligations with unknown amounts).

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
- **API is last.** The domain layer gets proven via BDD tests before we expose
  it over HTTP. The API is a thin pass-through — building it early just means
  rewriting it when invariants change.
