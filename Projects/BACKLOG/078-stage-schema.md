# 078 — Transaction Import Stage Schema

**Epic:** Infrastructure
**Depends On:** None
**Status:** Not started
**Priority:** High

---

## Problem Statement

LeoBloom needs a staging area for imported financial institution transactions.
The import pipeline is: parse FI exports → load into staging → categorize →
human review → post to ledger via CLI. The staging schema holds transactions
between parse and post, supporting deduplication, category assignment, and
review workflow.

This is a Hobson-side schema — not part of BD's migration chain. It lives in
the `leobloom_prod` database under a `stage` schema. BD builds the tables and
constraints; Hobson manages the data via Python import scripts.

## Design Decisions

- **Source-matched tables**: one table per FI, columns match source format.
  Preserves source detail for review. Adding a new FI = one new table.
- **Dedup via ref_id**: each row has a `ref_id` (native or composite). UNIQUE
  constraint on `(ref_id, batch_id)` prevents re-import of the same file.
- **Status workflow**: new → categorized → reviewed → posted (or duplicate/skipped).
- **Ledger link**: `journal_entry_id` FK populated after successful CLI post.
- **Transfer support**: `transfer_partner_id` links two rows that form a
  transfer pair. Both must reach "reviewed" before promotion.

## Tables

### stage.fidelity_visa

Source: Fidelity Rewards Visa CSV export.

| Column | Type | Notes |
|---|---|---|
| id | serial PK | |
| date | date | Transaction date |
| transaction_type | varchar(10) | DEBIT or CREDIT |
| name | varchar(200) | Merchant name |
| memo | varchar(500) | Raw memo field from CSV |
| amount | numeric(12,2) | Negative for debits, positive for credits |
| ref_id | varchar(100) | Extracted from memo (first token before semicolon) |
| proposed_account_code | varchar(10) | Hobson's category guess, nullable |
| status | varchar(20) | new, categorized, reviewed, posted, duplicate, skipped |
| journal_entry_id | int | FK to ledger.journal_entry, nullable |
| batch_id | varchar(200) | Source filename |
| created_at | timestamptz | DEFAULT now() |

UNIQUE constraint on `(ref_id, batch_id)`.

### stage.fidelity_history

Source: Fidelity Transaction History CSV (brokerage, CMA, IRA accounts).

| Column | Type | Notes |
|---|---|---|
| id | serial PK | |
| run_date | date | |
| account_name | varchar(100) | |
| account_number | varchar(20) | |
| action | varchar(100) | DIVIDEND RECEIVED, YOU SOLD, DIRECT DEPOSIT, etc. |
| symbol | varchar(10) | Ticker or blank |
| description | varchar(200) | |
| type | varchar(20) | Cash, Margin, etc. |
| price | numeric(12,2) | Nullable |
| quantity | numeric(16,6) | Nullable |
| commission | numeric(12,2) | Nullable |
| fees | numeric(12,2) | Nullable |
| accrued_interest | numeric(12,2) | Nullable |
| amount | numeric(12,2) | |
| settlement_date | date | Nullable |
| ref_id | varchar(100) | Composite: account_number\|date\|amount |
| proposed_account_code | varchar(10) | Nullable |
| status | varchar(20) | |
| transfer_partner_id | int | FK to stage.fidelity_history or cross-table ref, nullable |
| journal_entry_id | int | FK to ledger.journal_entry, nullable |
| batch_id | varchar(200) | |
| created_at | timestamptz | DEFAULT now() |

UNIQUE constraint on `(ref_id, batch_id)`.

### stage.secu

Source: SECU PDF statements (checking, money market, shares).

| Column | Type | Notes |
|---|---|---|
| id | serial PK | |
| posted_date | date | |
| effective_date | date | |
| direction | varchar(1) | + or - |
| amount | numeric(12,2) | Always positive; direction indicates sign |
| description | varchar(500) | Full description text |
| embedded_ref | varchar(50) | Parsed from description |
| account_number | varchar(20) | Which SECU sub-account |
| ref_id | varchar(100) | The embedded_ref |
| proposed_account_code | varchar(10) | Nullable |
| status | varchar(20) | |
| transfer_partner_id | int | Nullable |
| journal_entry_id | int | FK to ledger.journal_entry, nullable |
| batch_id | varchar(200) | |
| created_at | timestamptz | DEFAULT now() |

UNIQUE constraint on `(ref_id, batch_id)`.

### stage.ally

Source: Ally Bank PDF statements (spending, savings).

| Column | Type | Notes |
|---|---|---|
| id | serial PK | |
| date | date | |
| description | varchar(300) | |
| credits | numeric(12,2) | Nullable |
| debits | numeric(12,2) | Nullable |
| account_number | varchar(20) | |
| ref_id | varchar(100) | Composite: account_number\|date\|amount |
| proposed_account_code | varchar(10) | Nullable |
| status | varchar(20) | |
| transfer_partner_id | int | Nullable |
| journal_entry_id | int | FK to ledger.journal_entry, nullable |
| batch_id | varchar(200) | |
| created_at | timestamptz | DEFAULT now() |

UNIQUE constraint on `(ref_id, batch_id)`.

### stage.synchrony_amazon

Source: Synchrony Amazon Store Card PDF statements.

| Column | Type | Notes |
|---|---|---|
| id | serial PK | |
| date | date | |
| reference_num | varchar(30) | P934200... from statement |
| description | varchar(500) | Merchant + item descriptions |
| amount | numeric(12,2) | Positive for purchases, negative for payments/credits |
| ref_id | varchar(100) | The reference_num |
| proposed_account_code | varchar(10) | Nullable |
| status | varchar(20) | |
| journal_entry_id | int | FK to ledger.journal_entry, nullable |
| batch_id | varchar(200) | |
| created_at | timestamptz | DEFAULT now() |

UNIQUE constraint on `(ref_id, batch_id)`.

### stage.merchant_rules

Shared categorization rules across all FI sources.

| Column | Type | Notes |
|---|---|---|
| id | serial PK | |
| pattern | varchar(200) | SQL LIKE pattern for merchant name |
| account_code | varchar(10) | Target expense/revenue account code |
| source | varchar(30) | FI-specific rule (nullable = global) |
| notes | varchar(200) | Nullable |
| created_at | timestamptz | DEFAULT now() |

UNIQUE constraint on `(pattern, source)` where source is coalesced to empty string.

## Acceptance Criteria

1. `CREATE SCHEMA stage` in `leobloom_prod`.
2. All six tables created with constraints as specified.
3. `journal_entry_id` columns are FK references to `ledger.journal_entry(id)`.
4. UNIQUE constraints prevent duplicate loads from the same batch.
5. A `stage.import_summary` view (or query) that returns counts by status
   across all tables for quick dashboard.
6. Grant `claude` role full access to `stage` schema (SELECT, INSERT, UPDATE).
7. Tests: insert a row, attempt duplicate insert (expect conflict), verify
   status transitions work.
