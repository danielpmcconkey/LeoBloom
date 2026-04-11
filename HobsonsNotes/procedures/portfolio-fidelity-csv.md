# Portfolio: Fidelity Positions

## When

Every Saturday. Dan updates all portfolio positions weekly.

## What you need from Dan

1. **Fidelity positions CSV** — downloaded from Fidelity, dropped in the
   import data directory. Filename pattern:
   `FidelityPositions_asof_YYYYMMDD.csv`

## Files

| What | Where |
|---|---|
| Script | `~/penthouse-pete/leobloom-ops/imports/fidelity_positions.py` |
| Import data | `/mnt/media/BusinessRecords/LeoBloomImportData/` |
| Python | `~/penthouse-pete/leobloom-ops/.venv/bin/python3` |

## What it does

Parses the Fidelity CSV and posts one portfolio position per fund per
account. The CSV provides everything — quantity, price, value, and cost
basis. No computation required.

### Skipped rows

- **SPAXX** — money market sweeps, tracked as cash in the ledger
- **Z52355485 (CMA)** — Property CMA, only holds SPAXX
- **TD 401(k) (UUID account)** — handled by `trowe.py`
- **Self-Driven (UUID account)** — phantom account Fidelity won't delete

### Account mapping

| CSV Account | Portfolio Account | ID |
|---|---|---|
| Z08806967 | JOINT Taxable Brokerage | 12 |
| 237564538 | Dan's TRADITIONAL IRA | 10 |
| 237566939 | Dan's ROTH IRA | 9 |
| 263151260 | Jodi's rollover IRA | 16 |

## Steps

### 1. Compute and post

```bash
cd ~/penthouse-pete/leobloom-ops/imports
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_positions.py \
  --csv /mnt/media/BusinessRecords/LeoBloomImportData/FidelityPositions_asof_<date>.csv \
  --post
```

The script prints a table before posting. Check:
- 9 positions (3 funds × 3 Dan accounts + 1 fund × Jodi)
- No unmapped account warnings
- Values look reasonable

### 2. Confirm

Report back to Dan:

| Account | Symbol | Shares | Price | Value |
|---|---|---|---|---|
| Brokerage (12) | FNILX, FXAIX, SCHD | from output | from output | from output |
| Traditional IRA (10) | FNILX, FXAIX, SCHD | | | |
| Roth IRA (9) | FXAIX, SCHD | | | |
| Jodi Rollover (16) | FXAIX | | | |
| **Total** | | | | |

### Dry run (optional)

Omit `--post` to preview without writing.

## Move to processed

After posting, move the CSV to processed:
```bash
mv /mnt/media/BusinessRecords/LeoBloomImportData/FidelityPositions_asof_*.csv \
   /mnt/media/BusinessRecords/LeoBloomImportData/processed/
```
