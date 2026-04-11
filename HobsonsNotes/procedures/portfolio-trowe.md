# Portfolio: T. Rowe Price 401(k) Positions

## When

Every Saturday. Dan updates all portfolio positions weekly.

Also after a new quarterly statement arrives (to refresh the statement
anchor values).

## What you need from Dan

1. **Quarterly statement PDF** — most recent, in the import directory.
   Read these four values from it:
   - Statement end date (e.g., 2025-12-31)
   - Total ending shares (Transaction Detail → last Ending Balance row)
   - Average cost per share (Investment Activity section header)
   - Roth basis amount (Contributions section, explicit callout)

2. **Transaction CSV** — downloaded from T. Rowe Price, covering at least
   from the statement date through today. Must include the most recent
   fund exchange (share class change). One year of history is sufficient.

3. **Current share price** — Dan looks this up on the T. Rowe website.

## Files

| What | Where |
|---|---|
| Script | `~/penthouse-pete/leobloom-ops/imports/trowe.py` |
| Import data | `/mnt/media/BusinessRecords/LeoBloomImportData/` |

## Steps

### 1. Read statement data

From the quarterly statement PDF, note:

| Field | Where to find it | Example |
|---|---|---|
| Statement end date | Page 1, period header | 2025-12-31 |
| Total shares | Page 3, Transaction Detail, last "Ending Balance" | 1511.1699 |
| Avg cost/share | Page 3, Investment Activity, in parentheses after fund name | 137.83 |
| Roth basis | Page 2, Contributions section, explicit sentence | 84188.35 |

These values only change quarterly. Reuse them until the next statement.

### 2. Compute and post

```bash
cd ~/penthouse-pete/leobloom-ops/imports
python3 trowe.py \
  --csv /mnt/media/BusinessRecords/LeoBloomImportData/<transactions>.csv \
  --price <current_price> \
  --statement-date YYYY-MM-DD \
  --statement-shares <shares> \
  --roth-basis <basis> \
  --avg-cost <cost> \
  --post
```

Before posting, the script prints the full breakdown. Check:
- "Verified" message (CSV share total matches statement)
- No unknown sources in the breakdown
- Total value matches what T. Rowe's website shows

The script inserts new rows or updates existing ones for the position
date. Two rows: one Roth, one Traditional.

### 3. Confirm

Report the posted positions back to Dan:

| | Shares | Price | Value |
|---|---|---|---|
| Roth (account 13) | from output | current price | from output |
| Traditional (account 14) | from output | current price | from output |
| **Total** | | | should match T. Rowe |

If the total doesn't match T. Rowe's website balance, investigate
before moving on.

### Dry run (optional)

Omit `--post` to compute without writing to the DB. Useful if you want
to sanity-check before posting, or if Dan just wants to see the numbers.

## How it works

The script reconstructs per-source share counts from the transaction CSV:
- **Contributions** add shares to a source
- **Exchange In** adds shares (fund share class changes)
- **Fees** deduct shares (plan administrative expenses)
- **Market Fluctuations** have 0 shares — ignored

Sources map to sub-accounts:
- `ROTH CONTRIBUTIONS` → account 13 (Roth, tax-free)
- `EMPLOYEE DEFERRAL`, `SAFE HARBOR MATCH`, `CORE CONTRIBUTION`,
  `MISCELLANEOUS CREDIT` → account 14 (Traditional, tax-deferred)

**Cost basis** uses the quarterly statement as anchor. Exchange In
amounts are market value (not cost basis), so the statement's Roth basis
and average cost per share are required to split cost correctly. Post-
statement contributions roll forward at cost (contribution $ = cost $).

## Fund class changes

T. Rowe Price has changed the Vanguard fund share class twice:
- 06/04/2025: VANGUARD INST 500 INDEX TRUST → IDX TR C
- 11/05/2025: IDX TR C → IDX TR B (current)

The script identifies the current fund by finding the most recent
Exchange In. The CSV must cover that exchange date. If it doesn't,
download a longer date range.

DB symbol is `VI5TC` regardless of share class — same underlying
S&P 500 index fund.

## Accounts

| Role | ID | Name | Tax Bucket |
|---|---|---|---|
| Roth | 13 | T. Rowe Price 401(k) (Roth) | Tax free Roth |
| Traditional | 14 | T. Rowe Price 401(k) (traditional) | Tax deferred |

## Cost basis caveat

The traditional cost basis is approximate. Exchange In amounts carry
market value, not original cost. The Roth cost is anchored precisely
from the statement's explicit Roth basis figure. For a tax-deferred
401(k) this doesn't matter — there's no taxable event until withdrawal.
The Roth basis matters for tracking contributions vs earnings (relevant
for early withdrawal rules), and that figure is exact.
