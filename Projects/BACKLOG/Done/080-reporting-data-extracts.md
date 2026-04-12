# Brief: Reporting Data Extracts

**From:** Hobson (Comptroller)
**To:** BD (Product Owner / Builder)
**Date:** 2026-04-12
**Depends on:** Nothing — greenfield
**Blocked by this:** Hobson's report generation (net worth PDF, transaction detail PDF)

---

## Context

Hobson generates human-readable financial reports (PDF) for Dan and his
family. These reports combine ledger data and portfolio data into
formatted documents. The CLI needs to provide raw structured data
extracts (JSON) that Hobson's scripts consume. These extracts are
**not report-specific** — they are generic data feeds that serve multiple
reports.

The two immediate reports are:

1. **Estate instructions** — a net worth breakdown with asset/liability
   detail, portfolio positions by tax bucket, and supporting narrative.
   Audience: Dan's family. Output: PDF.

2. **Transaction detail by period** — every journal entry line in a
   fiscal period, grouped by COA hierarchy with per-account totals.
   Audience: Dan. Output: PDF.

Future reports will reuse the same extracts.

---

## What Hobson needs

Four data extracts, each outputting JSON to stdout. The CLI design
(subcommand names, flag conventions) is BD's call. What follows is
the data contract — the shape and content of the JSON Hobson will
consume.

### Extract 1: Account tree

All accounts in the system with their metadata and hierarchy.

**Parameters:** None (always returns the full tree).

**Output — array of objects:**

```json
[
  {
    "id": 2,
    "code": "1100",
    "name": "Property Assets",
    "parent_id": 1,
    "account_type": "Asset",
    "normal_balance": "debit",
    "subtype": null,
    "is_active": true
  },
  {
    "id": 3,
    "code": "1110",
    "name": "Fidelity CMA (Property)",
    "parent_id": 2,
    "account_type": "Asset",
    "normal_balance": "debit",
    "subtype": "Cash",
    "is_active": true
  }
]
```

**Required fields:**
- `id` — primary key
- `code` — account code string
- `name` — display name
- `parent_id` — nullable, references parent account id
- `account_type` — resolved name from `ledger.account_type` (Asset, Liability, Equity, Revenue, Expense)
- `normal_balance` — "debit" or "credit", from `ledger.account_type`
- `subtype` — nullable varchar from `ledger.account.account_subtype`
- `is_active` — boolean

**Notes:**
- Include ALL accounts (active and inactive). Hobson filters as needed.
- Flat array is fine. Hobson builds the tree from `parent_id`.


### Extract 2: Account balances

Balance per account as of a given date.

**Parameters:**
- `--as-of <YYYY-MM-DD>` — required. Only include journal entries with
  `entry_date <= as-of`.

**Output — array of objects:**

```json
[
  {
    "account_id": 3,
    "code": "1110",
    "name": "Fidelity CMA (Property)",
    "balance": 1059.53
  }
]
```

**Required fields:**
- `account_id` — foreign key to account
- `code` — for convenience (avoids a join on Hobson's side)
- `name` — for convenience
- `balance` — numeric, computed as: **sum of debit amounts minus sum of
  credit amounts** across all non-voided journal entry lines where
  `entry_date <= as-of`

**Critical: void filtering.**
A journal entry is voided if `journal_entry.voided_at IS NOT NULL`.
Voided entries MUST be excluded from the balance calculation. This
must be an inner join or explicit WHERE filter — a LEFT JOIN with
`voided_at IS NULL` in the ON clause will leak voided lines through.

**Notes:**
- The `balance` field is a **raw debit-minus-credit** number. It is NOT
  adjusted for normal balance direction. Hobson applies normal balance
  logic on consumption. This keeps the extract generic.
- Include every account that has at least one non-voided posting.
  Accounts with zero balance may be omitted (Hobson will infer zero
  from absence).
- Parent/header accounts that have no direct postings may be omitted.


### Extract 3: Portfolio positions

Latest position per investment account + symbol as of a given date.

**Parameters:**
- `--as-of <YYYY-MM-DD>` — required. Only include positions where
  `position_date <= as-of`. For each (investment_account, symbol) pair,
  return only the row with the maximum `position_date`.

**Output — array of objects:**

```json
[
  {
    "investment_account_id": 5,
    "investment_account_name": "Dan's ROTH IRA 237566939",
    "tax_bucket": "Roth",
    "symbol": "FXAIX",
    "fund_name": "Fidelity 500 Index Fund",
    "position_date": "2026-04-11",
    "price": 186.42,
    "quantity": 370.081,
    "current_value": 68986.72,
    "cost_basis": 52341.00
  }
]
```

**Required fields:**
- `investment_account_id` — FK
- `investment_account_name` — from `portfolio.investment_account.name`
- `tax_bucket` — resolved name from `portfolio.tax_bucket`
- `symbol` — fund/position symbol (PK on `portfolio.fund`)
- `fund_name` — from `portfolio.fund.name`
- `position_date` — the date of this position snapshot
- `price` — per-unit price
- `quantity` — number of units held
- `current_value` — total market value
- `cost_basis` — total cost basis

**Notes:**
- Exclude positions where `current_value = 0`. These are historical
  positions that have been fully sold or rolled over. They add noise
  and Hobson doesn't need them for current reporting.
- If no `--as-of` is provided, default to today's date.


### Extract 4: Journal entry lines by fiscal period

All non-voided journal entry lines within a fiscal period, with
journal entry metadata.

**Parameters:**
- `--fiscal-period-id <int>` — required.

**Output — array of objects:**

```json
[
  {
    "journal_entry_id": 472,
    "entry_date": "2026-01-05",
    "description": "FOOD LION #1614 INDIAN TRAIL NC",
    "source": "fidelity-visa",
    "account_id": 85,
    "account_code": "5350",
    "account_name": "Food",
    "amount": 39.27,
    "entry_type": "debit",
    "memo": null
  }
]
```

**Required fields:**
- `journal_entry_id` — FK to journal entry
- `entry_date` — from `ledger.journal_entry.entry_date`
- `description` — from `ledger.journal_entry.description`
- `source` — nullable, from `ledger.journal_entry.source`
- `account_id` — FK to account
- `account_code` — for convenience
- `account_name` — for convenience
- `amount` — from `ledger.journal_entry_line.amount`
- `entry_type` — "debit" or "credit"
- `memo` — nullable, from `ledger.journal_entry_line.memo`

**Filtering:**
- Journal entry's `entry_date` must fall within the fiscal period's
  `start_date` and `end_date` (inclusive on both ends).
- Exclude voided journal entries (`voided_at IS NOT NULL`).

**Ordering:**
- `account_code ASC, entry_date ASC, journal_entry_id ASC`

**Notes:**
- Every line of every non-voided JE in the period is included, even
  the balancing sides (e.g. a food purchase will have both a debit to
  5350 and a credit to the payment account). Hobson groups and filters
  on consumption.
- Do not roll up or aggregate. This is line-level detail.

---

## What Hobson does NOT need from BD

- Formatted reports. Hobson handles all formatting and PDF generation.
- Net worth calculation logic. Hobson combines ledger balances and
  portfolio positions using account subtype as the discriminator
  (Cash subtype = use ledger balance; Investment subtype = use
  portfolio value).
- Account hierarchy traversal. Hobson builds the tree from the flat
  account-tree extract.
- Normal balance adjustment. Hobson applies this on consumption using
  the `normal_balance` field from the account tree.

---

## Acceptance criteria

- All four extracts produce valid JSON to stdout when invoked with
  `--json` (or however BD structures the flag).
- Void filtering is correct (inner join or WHERE, not LEFT JOIN leak).
- Portfolio positions exclude zero-value historical entries.
- Transaction extract respects fiscal period boundaries.
- Extracts work against both `leobloom_prod` and `leobloom_dev`
  (controlled by `LEOBLOOM_ENV` as with all CLI commands).
