# Import: Fidelity Transaction History

## When

Dan provides a Fidelity Transaction History CSV export in the import data
directory. Filename pattern: `FidelityTransactionHistory_*_through_*.csv`

## Source

- Import data: `/mnt/media/BusinessRecords/LeoBloomImportData/`
- Script: `~/penthouse-pete/leobloom-ops/imports/fidelity_history.py`
- Python: `~/penthouse-pete/leobloom-ops/.venv/bin/python3`
- Venv packages: `psycopg2` (no excel libs needed — CSV only)

## CSV Format

| Column | Notes |
|---|---|
| Run Date | MM/DD/YYYY |
| Account | Human-readable name |
| Account Number | Z52355485, Z08806967, 237564538, 237566939 |
| Action | Classification-driving field (see below) |
| Symbol | Ticker or blank |
| Description | Fund name or "No Description" |
| Amount ($) | Positive = inflow, negative = outflow |
| Settlement Date | Often blank for cash transactions |

### CSV Quirks

- Two blank lines at top of file
- Legal disclaimer footer — parser stops at first non-date row
- Multiple accounts in one file (4 accounts)
- Date range can overlap between files — batch_id distinguishes

## Accounts in the CSV

| Account Name | Account # | LeoBloom Code | Ledger ID |
|---|---|---|---|
| Cash Management (Property) | Z52355485 | 1110 | 3 |
| JOINT Taxable Brokerage | Z08806967 | 1210 | 6 |
| Dan's TRADITIONAL IRA | 237564538 | 1240 | 79 |
| Dan's ROTH IRA | 237566939 | 1250 | 80 |

## Action-Based Classification

The `classify` step handles this. No merchant rules for most rows.

| Action Prefix | IRA | Brokerage/CMA |
|---|---|---|
| REINVESTMENT (SPAXX) | Skip | Skip (non-event) |
| DIVIDEND RECEIVED | Skip | Revenue: Dr cash, Cr 4200 |
| YOU BOUGHT / YOU SOLD | Skip | Skip (portfolio domain) |
| DIRECT DEPOSIT (payroll) | N/A | Revenue: Dr cash, Cr 4210 (Salary) |
| DIRECT DEPOSIT (ELAN CARDSVCRedemption) | N/A | Revenue: Dr cash, Cr 4200 |
| TRANSFERRED TO/FROM | Skip | Owner equity (see below) |
| DIRECT DEBIT | N/A | Expense via merchant rules |
| BILL PAYMENT | N/A | Expense via merchant rules |
| BILL PAYMENT STATE EMPLOYEES CREDIT | N/A | Transfer: Dr 2210 (Mortgage — Personal), Cr cash |
| DIRECT DEBIT ROCKET MORTGAG... | N/A | Transfer: Dr 2110 (Mortgage — Lockhart), Cr 1110 |
| Commission / Fees (non-zero) | Skip | Dr 7220 (Investment Fees, ledger ID 68), Cr cash |

### CMA Dividends — GAAP Note

SPAXX dividends in the Property CMA (Z52355485) are **4200 (Other
Revenue), not 4110 (Rental Revenue)**. Per GAAP, interest earned on an
operating account is other revenue regardless of the account's purpose.

### Owner Equity Transfers (Brokerage ↔ CMA)

- Money into CMA: Dr 1110 (CMA), Cr 3010 (Owner's Investment)
- Money out of CMA: Dr 3020 (Owner's Draws), Cr 1110
- Only the CMA leg posts — brokerage leg is skipped to avoid double-posting

## Composite Key

No native reference IDs in this CSV. Composite key:
`{account_number}|{YYYYMMDD}|{amount}` — no dashes in date.

Duplicate base keys (e.g., two $500 payroll deposits same day) get a
sequence suffix: `|1`, `|2`, etc.

All composite key matches during dedup flag for review — never auto-skip.

## Steps

### 1. Parse

```bash
cd ~/penthouse-pete/leobloom-ops/imports
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_history.py parse /mnt/media/BusinessRecords/LeoBloomImportData/<filename>.csv
```

Run once per file. Idempotent.

### 2. Classify

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_history.py classify
```

Applies action-based rules. Most rows get classified or skipped here.
Expense rows (DIRECT DEBIT, BILL PAYMENT) stay as `new` for merchant
rule matching.

### 3. Categorize

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_history.py categorize
```

Matches remaining `new` rows against `stage.merchant_rules`. Matches
against the full Action field (which includes merchant name).

### 4. Check status

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_history.py status
```

### 5. Handle unmatched merchants

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_history.py show new
```

Add merchant rules and re-run categorize, or manually categorize and
set status to `reviewed`.

### 6. Dedup

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_history.py dedup
```

Composite keys only — all matches flag for review.

### 7. Review

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_history.py show review
```

Review flagged rows. Mark confirmed duplicates:
```sql
UPDATE stage.fidelity_history SET status = 'duplicate' WHERE id = <id>;
```

### 8. Promote

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_history.py promote
```

### 9. Verify staging is clean

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_history.py status
```

All rows should be posted, skipped, or duplicate.

### 10. Seed new merchant rules

Any expense rows (DIRECT DEBIT, BILL PAYMENT) that were manually
categorized should be added to `stage.merchant_rules` so they
auto-categorize next time.

### 11. Move source files to processed

```bash
mv /mnt/media/BusinessRecords/LeoBloomImportData/<filename>.csv \
   /mnt/media/BusinessRecords/LeoBloomImportData/processed/
```

## Mortgage Payment P&I Split

Rocket Mortgage payments from the CMA Property account (1110) are
Lockhart P&I. The parser posts the full $1,866.58 as Dr 2110, Cr 1110.
This needs a compound JE instead: Dr 2110 (principal), Dr 5110
(interest), Cr 1110.

To find the split, look up the payment number in the amortization
schedule:

`/mnt/media/BusinessRecords/LockhartPl/Mortgage/calculated_amortization_schedule.ods`

Payment numbers are sequential from payment 1 (03/01/2026). The schedule
carries full precision internally; round principal and interest to the
penny when posting. Sub-penny differences vs Churchill's actual schedule
are expected and immaterial.

Formula if calculating manually:
```
Monthly interest = outstanding principal x 0.06125 / 12
Principal = $1,866.58 - interest
```

## Reference Format

`fidelity-history:{ref_id}` — composite key with no-dash date format.
