# Import: Synchrony Amazon Store Card

## When

Dan provides new Synchrony Amazon statement PDFs in the import data
directory. Filename pattern: `SynchronyCCAmazonStatement_2026-MM-DD.pdf`

## Source

- Import data: `/mnt/media/BusinessRecords/LeoBloomImportData/`
- Script: `~/penthouse-pete/leobloom-ops/imports/synchrony.py`
- Python: `~/penthouse-pete/leobloom-ops/.venv/bin/python3`
- Venv packages: `psycopg2`, `pdfplumber` (PDF statement parsing)

## PDF Format

Transaction Detail section with transactions grouped by type:
- **Payments** — credits, negative amounts (card payments from SECU)
- **Other Credits** — refunds, negative amounts
- **Purchases and Other Debits** — purchases, positive amounts

| Column | Notes |
|---|---|
| Date | MM/DD (year inferred from billing cycle) |
| Reference # | Native ref: `P934200...` (purchases/refunds), `F934200...` (payments) |
| Description | Merchant name, continuation lines have order IDs and product names |
| Amount | Signed: negative = credit/refund, positive = purchase |

### Billing Cycle

Pattern in PDF: `"NN Day Billing Cycle from MM/DD/YYYY to MM/DD/YYYY"`

Used to resolve year for MM/DD dates. December dates in a cycle ending
in January get the previous year.

### PDF Quirks

- Transactions span multiple pages with "Transaction Detail (Continued)" header.
- Each transaction has 1-2 continuation lines: order ID hash + product name(s).
- Product names are truncated (~30 chars).
- Page headers (PAGE N of N, account holder name) interrupt flow.

## Dedup

**Native refs** — P934200/F934200 format. Matches auto-mark as duplicate.

## Steps

### 1. Parse

```bash
cd ~/penthouse-pete/leobloom-ops/imports
~/penthouse-pete/leobloom-ops/.venv/bin/python3 synchrony.py parse /mnt/media/BusinessRecords/LeoBloomImportData/<filename>.pdf
```

Loads rows into `stage.synchrony_amazon`. Idempotent.

### 2. Classify

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 synchrony.py classify
```

Rules:
| Pattern | Handling |
|---|---|
| F-prefix ref (payments) | Skip — already posted from SECU side |
| Date before 2026-01-01 | Skip — pre-period |
| Negative amount (P-ref) | Classified as refund |
| Positive amount | Left as `new` for merchant rules |

### 3. Categorize

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 synchrony.py categorize
```

Matches `new` rows against `stage.merchant_rules`. Amazon product
descriptions in continuation lines help specific rules match.

### 4. Handle unmatched rows

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 synchrony.py show new
```

Most unmatched will be Amazon purchases. Categorize by product name:
- Household supplies, electronics, clothing → 6000 (General Household)
- Food/groceries → 5350
- Healthcare/vitamins → 5400
- Pet supplies → 5950
- Hobbies → 5850
- Entertainment/media → 5650

After assignment, update status to `categorized` (or `reviewed` if
manually decided).

### 5. Dedup

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 synchrony.py dedup
```

Native refs — auto-marks duplicates.

### 6. Handle refunds

Refunds need a `proposed_account_code` to know which expense to reduce.
Match back to original purchase if possible, otherwise default to 6000.

### 7. Promote

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 synchrony.py promote
```

- **Purchases:** Dr expense, Cr 2240 (card liability increases)
- **Refunds:** Dr 2240, Cr expense (liability and expense both decrease)
- **Reviewed rows:** trust the `proposed_account_code` directly

### 8. Seed new merchant rules

Any non-Amazon merchants categorized manually should be added to
`stage.merchant_rules`. Amazon RETAIL purchases generally need manual
categorization by product name each round — don't create a catch-all
rule for those.

### 9. Verify and move

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 synchrony.py status
mv /mnt/media/BusinessRecords/LeoBloomImportData/<filename>.pdf \
   /mnt/media/BusinessRecords/LeoBloomImportData/processed/
```

## Accounts

| Role | Code | Ledger ID | Name |
|---|---|---|---|
| Liability (card) | 2240 | 92 | Synchrony — Amazon Card |
| Expense (varies) | varies | varies | Per merchant rule / manual |

## Reference Format

`synchrony:{P934200...}` — native ref from the statement. Reliable for
dedup.

## Card Payment Flow

Payments to this card come from SECU checking. They show up:
1. On the SECU statement as "AMZ_STORECRD_PMT" → posted as Dr 2240, Cr 1220
2. On the Synchrony statement as a payment credit (F-prefix) → **skipped**

Only import expenses (purchases/refunds) from Synchrony. Payments are
handled on the SECU side.

## Merchant Rules

Most Amazon purchases match on specific product patterns seeded from
historical data. There is NO generic `%AMAZON RETAIL SEATTLE%` catch-all
rule — unmatched retail purchases need manual categorization by product
description each round.
