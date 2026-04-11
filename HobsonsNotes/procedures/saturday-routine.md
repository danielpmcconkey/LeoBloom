# Saturday Routine

Weekly financial update. Dan provides export files, Hobson runs parsers
and reviews obligations.

## Part 0 — Jodi Review Items

Dan reports back on any parked transactions flagged for Jodi's input
(unrecognized Visa purchases, SECU transactions, etc.). For each item:

1. Dan identifies the expense or confirms it's a return/refund
2. Hobson classifies and updates the staging row
3. Re-run categorize/promote as needed

Check for parked items:
```sql
SELECT id, date, description, amount, status
FROM stage.fidelity_visa WHERE status = 'parked'
UNION ALL
SELECT id, posted_date, description, amount, status
FROM stage.secu WHERE status = 'parked'
ORDER BY date;
```

If no parked items, skip to Part 1.

## Part 1 — Portfolio Positions

Dan downloads these files and drops them in
`/mnt/media/BusinessRecords/LeoBloomImportData/`:

| Source | File | What Dan looks up |
|---|---|---|
| Fidelity | Positions CSV | — |
| T. Rowe Price | Transaction CSV | Current share price |
| HealthEquity | Cash .xls + Investment .xlsx | Current VIIIX price |
| Real estate | — | Zillow estimates (optional, as desired) |

Run each parser per its procedure:

### 1. Fidelity positions

Procedure: `procedures/portfolio-fidelity-csv.md`

```bash
cd ~/penthouse-pete/leobloom-ops/imports
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_positions.py \
  --csv /mnt/media/BusinessRecords/LeoBloomImportData/FidelityPositions_asof_<date>.csv \
  --post
```

Confirm 9 positions across 4 accounts.

### 2. T. Rowe Price 401(k)

Procedure: `procedures/portfolio-trowe.md`

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 trowe.py \
  --csv /mnt/media/BusinessRecords/LeoBloomImportData/<transactions>.csv \
  --price <current_price> \
  --statement-date YYYY-MM-DD \
  --statement-shares <shares> \
  --roth-basis <basis> \
  --avg-cost <cost> \
  --post
```

Statement anchor values only change quarterly — reuse until the next
statement arrives. Confirm total matches T. Rowe website.

### 3. HealthEquity HSA

Procedure: `procedures/portfolio-healthequity.md`

Requires `openpyxl` (installed in venv).

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 healthequity.py \
  --cash /mnt/media/BusinessRecords/LeoBloomImportData/<cash_file>.xls \
  --investment /mnt/media/BusinessRecords/LeoBloomImportData/<invest_file>.xlsx \
  --price <VIIIX_price> \
  --post
```

This also posts cash transaction JEs (contributions, sweeps, fees).
Idempotent — safe to re-run.

### 4. Real estate (as needed)

Procedure: `procedures/portfolio-real-estate.md`

Only when Dan provides updated valuations. No export file required.

### 5. Move files to processed

```bash
mv /mnt/media/BusinessRecords/LeoBloomImportData/FidelityPositions_*.csv \
   /mnt/media/BusinessRecords/LeoBloomImportData/processed/
```

T. Rowe transaction CSV: keep until next quarter (re-run each week
with updated price). HealthEquity files: move to processed.

## Part 2 — Obligation Review

Walk through each obligation agreement. For each one:

1. Check the latest instance status
2. Look for the corresponding ledger JE — verify it posted to the
   correct COA account
3. If the JE exists and is correct, transition the instance to `posted`
   and link the JE
4. If the JE is miscoded, void and repost before linking
5. Note any missing payments or upcoming due dates

### Agreement checklist

| # | Agreement | Cadence | What to check |
|---|---|---|---|
| 1 | Brian rent + utilities | Monthly | Latest invoice, payment in Ally, correct tenant revenue account (4110) |
| 2 | Alex rent + utilities | Monthly | Latest invoice, payment in Ally, correct tenant revenue account (4120) |
| 3 | Justin utilities | Monthly | Latest invoice, payment in Ally, correct account (4150) |
| 4 | Mortgage — Lockhart | Monthly | Autopay from CMA. Verify P&I split against amortization schedule |
| 5 | Water & Electric | Monthly | City of Concord bill pay. Verify Dr 5120 (not 5500) |
| 6 | Gas — Enbridge | Monthly | Dominion Energy autopull. Verify Dr 5130 (not 5500) |
| 7 | Homeowners Insurance | Annual | Allstate. Verify Dr 5150. Next bill ~Jan 2027 |
| 8 | HOA — Sheffield Manor | Irregular | Feb 1, May 1, Aug 1. $275 each. Spawn instances manually |
| 9 | Property Tax | Annual | Cabarrus County. Bill arrives ~Jul/Aug, due ~Oct 1 |

### Tenant payment verification

The Ally import classifier doesn't distinguish tenants — all property
deposits go to 4110. After import, verify each tenant payment hits the
correct revenue account per `procedures/import-ally.md` §Tenant
Payment Verification.

### Irregular obligations

Check agreement 8 (HOA). If the next due date is within 30 days and
no instance exists, spawn one manually.

## Part 3 — Import Pipeline (if applicable)

If Dan has new bank/brokerage statements to import, run the relevant
import procedures. This is separate from the Saturday routine but often
happens the same day.

## Report

After completing parts 1 and 2, report to Dan:

- Portfolio totals by account
- Any obligation instances still outstanding (expected or overdue)
- Any JEs voided/reposted this session
- Upcoming due dates in the next 30 days
