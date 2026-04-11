# HealthEquity HSA — Cash Transactions + Investment Position

## When

Every Saturday. Dan updates all portfolio positions weekly.

## What you need from Dan

1. **Cash transactions report** (.xls, actually HTML) — downloaded from
   HealthEquity, covering at least since the last import. Drop in import
   data directory.

2. **Investment report** (.xlsx) — "All Investment Transactions" from
   HealthEquity, covering at least since the last position date. Drop in
   import data directory.

3. **Current VIIIX closing price** — Dan looks this up.

## Files

| What | Where |
|---|---|
| Script | `~/penthouse-pete/leobloom-ops/imports/healthequity.py` |
| Import data | `/mnt/media/BusinessRecords/LeoBloomImportData/` |
| Python | `~/penthouse-pete/leobloom-ops/.venv/bin/python3` |
| Venv packages | `psycopg2`, `openpyxl` (investment .xlsx parsing) |

## What it does

Two things in one run:

1. **Cash transactions → ledger JEs** on accounts 1340 (HSA Cash) and
   1350 (HSA Invested). Classifies and posts:
   - Employee/Employer contributions: Dr 1340, Cr 4210 (Salary)
   - Investment sweeps: Dr 1350, Cr 1340
   - Interest: Dr 1340, Cr 4200
   - Admin fees ($10/mo capped): Dr 7220, Cr 1340
   - Dedup via `healthequity-cash` references — safe to re-run.

2. **Investment position → portfolio.position** for account 11 (VIIIX).
   Reads shares from the xlsx's last row. Cost basis anchors from the
   latest DB position, then adds new buy/dividend amounts by matching
   on share count.

## Steps

### 1. Compute and post

```bash
cd ~/penthouse-pete/leobloom-ops/imports
~/penthouse-pete/leobloom-ops/.venv/bin/python3 healthequity.py \
  --cash /mnt/media/BusinessRecords/LeoBloomImportData/<cash_file>.xls \
  --investment /mnt/media/BusinessRecords/LeoBloomImportData/<invest_file>.xlsx \
  --price <VIIIX_price> \
  --post
```

Before posting, the script prints the full breakdown. Check:
- No REVIEW items (unknown transaction types)
- Cash transactions classified correctly
- Investment shares and value look right

### 2. Confirm

Report back to Dan:

**Cash (ledger account 1340):**
- Number of new JEs posted
- Current HSA cash balance (should be ~$990-$1,000)

**Investment (portfolio account 11):**

| | Shares | Price | Value |
|---|---|---|---|
| VIIIX | from output | current price | from output |

### Dry run (optional)

Omit `--post` to preview without writing.

## Accounts

### Ledger
| Code | ID | Name | Role |
|---|---|---|---|
| 1340 | 89 | HealthEquity HSA — Cash | Cash account (~$1,000 minimum) |
| 1350 | 90 | HealthEquity HSA — Invested | Investment at cost |
| 4210 | 26 | Salary — Dan | Revenue (contributions) |
| 4200 | 25 | Personal Revenue | Interest income |
| 7220 | 68 | Investment Fees | Admin fees |

### Portfolio
| ID | Name | Symbol | Tax Bucket |
|---|---|---|---|
| 11 | Health Equity | VIIIX | Tax free HSA |

## File format quirks

- **Cash .xls is actually HTML.** HealthEquity exports an HTML table
  with a .xls extension. The parser handles this — don't try to open
  it with openpyxl.
- **Investment .xlsx is real xlsx.** Standard openpyxl parsing.
- **Amounts use parentheses for negatives:** `($10.00)` not `-$10.00`.

## Cash account mechanics

HealthEquity requires a $1,000 minimum in the cash account. The pattern
each pay period is:
1. Employee contribution lands (~$298)
2. HealthEquity sweeps the excess to investment (balance returns to $1,000)
3. Monthly: interest ($0.08-0.09) and admin fee ($10.00)

The sweep amount may differ from the contribution because of
accumulated interest or pending fees. The parser uses actual amounts
from each transaction, not assumptions about matching.
