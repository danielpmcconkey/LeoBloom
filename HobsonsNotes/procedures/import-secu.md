# Import: NC SECU Statements

## When

Dan provides SECU monthly statement PDFs in the import data directory.
One PDF per month, covering all three SECU accounts.

## Source

- Import data: `/mnt/media/BusinessRecords/LeoBloomImportData/`
- Script: `~/penthouse-pete/leobloom-ops/imports/secu.py`
- Staging table: `stage.secu`
- Python: `~/penthouse-pete/leobloom-ops/.venv/bin/python3`
- Venv packages: `psycopg2`, `pdfplumber` (PDF statement parsing)

## SECU Account Mapping

| SECU Account # | LeoBloom Code | Ledger ID | Name |
|---|---|---|---|
| 19631611 | 1220 | 7 | SECU Checking |
| 7114263 | 1270 | 82 | SECU Money Market |
| 60024190 | 1280 | 83 | SECU Share |

## PDF Format

SECU statements contain three account sections (Checking, Money Market,
Shares). The parser extracts transactions from all three. Each
transaction has:

- Posted date and effective date
- Description (may span multiple lines, joined with ` | `)
- Amount and direction (+ credit, - debit)
- Embedded reference (extracted from continuation lines when present)

Reference types:
- **Native refs** (most transactions): long numeric codes embedded in
  the description. Reliable for dedup.
- **Composite refs** (checks, dividends, some transfers):
  `account_number|YYYYMMDD|amount`. May collide — always flag for review.

## Steps

### 1. Parse

```bash
cd ~/penthouse-pete/leobloom-ops/imports
~/penthouse-pete/leobloom-ops/.venv/bin/python3 secu.py parse /mnt/media/BusinessRecords/LeoBloomImportData/<filename>.pdf
```

Extracts transactions into `stage.secu`. Idempotent — `ON CONFLICT DO
NOTHING` on `ref_id + batch_id`. Internal transfer dedup: both legs of
an intra-SECU transfer share the same `#` ref; only one staging row
survives per pair.

### 2. Classify

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 secu.py classify
```

Applies rule-based classification to `new` rows:

| Pattern | Treatment |
|---|---|
| TD BANK NA PAYROLL | Revenue: Dr cash, Cr 4210 (Salary) |
| TD BANK AMCB AP PAYMENT | Revenue: Dr cash, Cr 4200 (BYOD stipend) |
| DIVIDEND EARNED / Interest | Revenue: Dr cash, Cr 4200 |
| FORD MOTOR CR | Transfer: Dr 2230 or 2235, Cr cash |
| CARDMEMBER SERV | Transfer: Dr 2220 (Visa), Cr cash |
| AMZ_STORECRD_PMT | Transfer: Dr 2240 (Synchrony), Cr cash |
| Clear Balance $109 | Transfer: Dr 2250 (Dan medical), Cr cash |
| Clear Balance $273 | Transfer: Dr 2260 (Jodi medical), Cr cash |
| INTERNET TRANSFER to x8172 | Allowance: Dr 6150, Cr cash (Rachel) |
| INTERNET TRANSFER to x8323 | Allowance: Dr 6150, Cr cash (Justin) |
| INTERNET TRANSFER to x4204 | Education: Dr 5800, Cr cash (Rachel tuition) |
| INTERNET TRANSFER between Dan's accounts | Internal: Dr receiving, Cr sending |
| FID BKG SVC LLC MONEYLINE | Review: Fidelity transfer, destination unknown from SECU side |
| SECU BILLPAY TO SECUMORTGA | Expense: Dr 5311 (personal mortgage) |

Rows that classify get status `classified`. Unmatched rows stay `new`.

### 3. Categorize

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 secu.py categorize
```

Matches remaining `new` rows (expenses) against `stage.merchant_rules`.
Matched rows get status `categorized` with `proposed_account_code` set.

### 4. Check status

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 secu.py status
```

Expect:
- `categorized`: expenses matched by merchant rules
- `classified`: revenue, transfers, known patterns
- `new`: unmatched — need manual categorization or new merchant rules
- `review`: ambiguous rows flagged by classifier
- `duplicate`: already in ledger

### 5. Handle unmatched merchants

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 secu.py show new
```

For each unmatched row:
- Assign a category manually, OR
- Add a merchant rule and re-run categorize

### 6. Dedup

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 secu.py dedup
```

Checks `journal_entry_reference` for existing entries:
- Native ref match → auto-marked `duplicate`
- Composite ref match → flagged `review`

### 7. Review

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 secu.py show review
```

Review every flagged row. Common review categories:

**Internal SECU transfers:** Dr receiving account, Cr sending account.
Both are Dan's accounts — pure balance sheet movement.

**Fidelity MONEYLINE:** Can't determine destination Fidelity account
from SECU statement alone. Ask Dan or defer to cross-source matching.

**Checks (Withdrawal or Check NNNN):** No description of payee. Ask Dan.

**Large one-time items:** Refinance proceeds, property closings, etc.
Handle individually per GAAP.

**Pre-period rows (before fiscal period 1):** Skip if tracking from
Jan 1 onward. Mark as `skipped`.

### 8. Promote

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 secu.py promote
```

Posts `categorized` and `classified` rows to the ledger via CLI.

For expenses: `Dr expense account, Cr cash account`.
For revenue: `Dr cash account, Cr revenue account`.
For transfers: `Dr liability/asset, Cr cash account`.
For internals: `Dr receiving cash, Cr sending cash`.

**Note on Ford payments:** Two vehicles — Mach-E ($763.89, account
2230/id 91) and Explorer (~$1,223, account 2235/id 99, now paid off).
The classifier must distinguish by amount.

**Note on Clear Balance:** Distinguish Dan ($109, account 2250) from
Jodi ($273, account 2260) by amount, not by reference (refs change
monthly).

### 9. Handle remaining rows

- **Parked rows:** Items requiring Jodi's review or Dan's input. Leave
  in staging. Track in the wakeup.
- **Skipped rows:** Pre-period transactions. Mark `skipped`.
- **Duplicates:** Already in ledger. No action.

### 10. Verify staging is clean

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 secu.py status
```

All rows should be `promoted`, `duplicate`, `skipped`, or `parked`.
Nothing in `new`, `categorized`, `classified`, or `review`.

### 11. Seed new merchant rules

Any merchants categorized manually this session should be added to
`stage.merchant_rules` for next time.

### 12. Move source files to processed

```bash
mv /mnt/media/BusinessRecords/LeoBloomImportData/<statement>.pdf \
   /mnt/media/BusinessRecords/LeoBloomImportData/processed/
```

## Known Categorization Quirks

| Merchant | Correct Account | Notes |
|---|---|---|
| ALLSTATE INS CO INS PREM (~$424) | 5450 Automotive | Auto insurance, monthly |
| ALLSTATE (large, ~$2,887) | 5150 Homeowners Ins — Lockhart | Annual, typically on Visa |
| SECU FOUNDATION ($1/mo) | 7210 Bank Fees | CU membership fee, not charitable |
| DIGFORROCKS | 5850 Hobbies | Rock collecting, not general household |
| DEPT EDUCATION STUDENT LN | TBD | Student loan payment — needs P&I split or liability account |
| Churchill Mortgage ($1,866.58) | Split: Dr 2110 + Dr 5110 | Lockhart P&I. Interest = principal x 6.125%/12 |

## Mortgage Payment P&I Split

Lockhart monthly payment is $1,866.58 (P&I only, no escrow). To split:

```
Monthly interest = outstanding principal x 0.06125 / 12
Principal = $1,866.58 - interest
```

First payment interest (on $307,200): $1,568.00. Each subsequent month,
recalculate from the reduced principal — or look up the split in the
amortization schedule:

`/mnt/media/BusinessRecords/LockhartPl/Mortgage/calculated_amortization_schedule.ods`

Payment numbers are sequential from payment 1 (03/01/2026). The schedule
carries full precision internally; round principal and interest to the
penny when posting. Sub-penny differences vs Churchill's actual schedule
are expected and immaterial.

Post as compound JE: Dr 2110 (principal), Dr 5110 (interest), Cr 1220.

## Reference Format

`secu:{ref_id}` — native refs use the embedded numeric code; composite
refs use `account_number|YYYYMMDD|amount`.
