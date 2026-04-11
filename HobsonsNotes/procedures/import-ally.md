# Import: Ally Bank

## When

Dan provides new Ally Bank statement PDFs in the import data directory.
Filename pattern: `AllyBankStatement2026-MM-DD.pdf`

## Source

- Import data: `/mnt/media/BusinessRecords/LeoBloomImportData/`
- Script: `~/penthouse-pete/leobloom-ops/imports/ally.py`
- Python: `~/penthouse-pete/leobloom-ops/.venv/bin/python3`
- Venv packages: `psycopg2`, `pdfplumber` (PDF statement parsing)

## PDF Format

Combined statement covering all Ally accounts. Each account has its own
section with an Activity table:

| Column | Notes |
|---|---|
| Date | MM/DD/YYYY |
| Description | Merchant/transaction type, may have continuation lines |
| Credits | Dollar amount (money in) |
| Debits | Dollar amount (money out) |
| Balance | Running balance (not stored) |

### Account Sections

| PDF Section Name | Account # | Code | Ledger ID |
|---|---|---|---|
| Spending Account | xxxxxx3700 (1070833700) | 1290 | 84 |
| Property Management | xxxxxx0412 (1149850412) | 1120 | 4 |
| Held for investments | xxxxxx7463 (2170477463) | 1300 | 85 |

### PDF Quirks

- "I nterest Paid" — OCR artifact (space in "Interest"). Parser normalizes.
- Beginning/Ending Balance lines are not transactions — parser skips them.
- Multi-line descriptions (e.g., Zelle payment details on continuation lines).
- `-$0.00` placeholder in the empty Credits/Debits column.

## Dedup

**ALL Ally refs are composite keys** (`account|YYYYMMDD|amount`). Matches
against the ledger are ALWAYS flagged for review, never auto-skipped.

## Steps

### 1. Parse

```bash
cd ~/penthouse-pete/leobloom-ops/imports
~/penthouse-pete/leobloom-ops/.venv/bin/python3 ally.py parse /mnt/media/BusinessRecords/LeoBloomImportData/<filename>.pdf
```

Loads rows into `stage.ally`. Idempotent.

### 2. Classify

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 ally.py classify
```

Rules:
| Pattern | Handling |
|---|---|
| Interest Paid | Revenue: Dr cash, Cr 4200 (Other Revenue) |
| Zelle/eCheck to x0412 (Property) | Revenue: Dr cash, Cr 4110 (Rental Revenue) |
| Zelle/NOW Deposit to x3700 (Personal) | Review — wrong account routing |
| Transfers | Review |
| Everything else | Expense — merchant rules |

### 3. Categorize

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 ally.py categorize
```

Matches remaining `new` rows against `stage.merchant_rules`.

### 4. Handle unmatched rows

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 ally.py show new
```

Manually assign codes and set status to `reviewed`.

### 5. Dedup

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 ally.py dedup
```

Composite keys only — any match goes to review.

### 6. Review

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 ally.py show review
```

Review flagged rows. For Zelle deposits routed to the wrong account:
post as rental revenue (4110) against whichever account the cash is
actually in.

### 7. Promote

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 ally.py promote
```

- `classified` and `categorized` rows: auto-posted per classify rules
- `reviewed` rows with a `proposed_account_code`: trusted directly
  - Credits: Dr cash, Cr target account
  - Debits: Dr target account, Cr cash

### 8. Seed new merchant rules

Any merchants categorized manually this session should be added to
`stage.merchant_rules` so they auto-categorize next time.

### 9. Verify and move

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 ally.py status
mv /mnt/media/BusinessRecords/LeoBloomImportData/<filename>.pdf \
   /mnt/media/BusinessRecords/LeoBloomImportData/processed/
```

## Tenant Payment Verification

The classifier lumps all property account deposits into 4110 (Rental
Revenue) without distinguishing tenants. After promote, manually verify
each tenant payment is credited to the correct revenue account:

| Tenant | Rent Revenue | Utility Reimb |
|---|---|---|
| Brian McConkey | 4110 (id 20) | 4130 (id 22) |
| Alex Chambers | 4120 (id 21) | — |
| Justin McConkey | — | 4150 (id 24) |

Check the payer name in the transaction description (Zelle includes
the sender name; check deposits do not — match by amount and timing
against the latest invoices).

If a payment is miscoded, void and repost with the correct credit
account before linking to the obligation instance.

## Accounts

| Role | Code | Ledger ID | Name |
|---|---|---|---|
| Cash (Spending) | 1290 | 84 | Ally Spending (Personal) |
| Cash (Property) | 1120 | 4 | Ally Checking (Property) |
| Cash (Savings) | 1300 | 85 | Ally Savings |
| Revenue (interest) | 4200 | 25 | Personal Revenue |
| Revenue (rent — Brian) | 4110 | 20 | Rental Income — Brian |
| Revenue (rent — Alex) | 4120 | 21 | Rental Income — Alex |
| Revenue (utility reimb — Brian) | 4130 | 22 | Utility Reimbursement — Brian |
| Revenue (utility reimb — Justin) | 4150 | 24 | Utility Reimbursement — Justin |

## Reference Format

`ally:{account_number}|{YYYYMMDD}|{amount}` — composite key. Duplicate
keys with sequence suffix: `|1`, `|2`, etc.

## Known Quirks

- Dan's Zelle is routing tenant payments to x3700 (personal) instead of
  x0412 (property). Until fixed, these are still rental revenue — just
  against the wrong cash account. Post as Dr 1290, Cr 4110.
- Account activity is minimal (mostly interest). Expect 1-3 transactions
  per statement until Zelle routing is fixed.
