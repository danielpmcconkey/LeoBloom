# Import: Fidelity Rewards Visa

## When

Dan provides a new Fidelity Visa CSV export in the import data directory.
Filename pattern: `FidelityRewardsVisaTransactionHistory_*.csv`

## Source

- Import data: `/mnt/media/BusinessRecords/LeoBloomImportData/`
- Script: `~/penthouse-pete/leobloom-ops/imports/fidelity_visa.py`
- Python: `~/penthouse-pete/leobloom-ops/.venv/bin/python3`
- Venv packages: `psycopg2` (no excel libs needed — CSV only)

## CSV Format

| Column | Notes |
|---|---|
| Date | YYYY-MM-DD |
| Transaction | DEBIT or CREDIT (credits are refunds) |
| Name | Merchant name |
| Memo | Reference — two formats (see below) |
| Amount | Negative = purchase, positive = refund/payment |

### Memo Format Split (2026-01-22)

- **After 2026-01-22:** semicolon-delimited, first token is a long native
  ref (e.g., `24445006022000789209313`). Reliable for dedup.
- **Before 2026-01-22:** short 4-digit codes that can collide across
  merchants. Composite ref: `date|memo|amount`. Always flags for review.

## Steps

### 1. Parse

```bash
cd ~/penthouse-pete/leobloom-ops/imports
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_visa.py parse /mnt/media/BusinessRecords/LeoBloomImportData/<filename>.csv
```

Loads rows into `stage.fidelity_visa`. Idempotent — re-running on the
same file is a no-op (`ON CONFLICT DO NOTHING` on `ref_id + batch_id`).

### 2. Categorize

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_visa.py categorize
```

Matches merchant names against `stage.merchant_rules`. Credits (refunds)
are flagged for review with `proposed_account_code = 'REFUND'`.

### 3. Check status

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_visa.py status
```

Expect: most rows `categorized`, some `new` (unmatched merchants), some
`review` (refunds).

### 4. Handle unmatched merchants

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_visa.py show new
```

For each unmatched row:
- Assign a category manually in the staging table, OR
- Add a merchant rule to `stage.merchant_rules` and re-run categorize

After manual categorization, update status to `reviewed`:
```sql
UPDATE stage.fidelity_visa SET status = 'reviewed' WHERE id = <id>;
```

### 5. Dedup

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_visa.py dedup
```

Checks `journal_entry_reference` in the ledger:
- Native ref match → auto-marked `duplicate`
- Composite ref match → flagged `review` (may be genuinely distinct)

### 6. Review

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_visa.py show review
```

Review every flagged row with Dan. Decide: promote, skip, or park.

### 7. Promote

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_visa.py promote
```

Posts each `categorized` or `reviewed` row to the ledger via CLI:
`Dr expense account, Cr 2220 (Visa liability)`.

### 8. Handle special rows

- **CC payments** (autopay from SECU): skip — SECU side handles the
  balanced JE. Mark `skipped`.
- **Refunds:** Set `proposed_account_code` to the original expense account
  code, set status to `reviewed`, then promote. The CLI posts:
  `Dr 2220, Cr expense` (reduces the expense).
- **Parked rows:** Rows Dan needs to investigate. Leave in staging until
  resolved.

### 9. Verify staging is clean

```bash
~/penthouse-pete/leobloom-ops/.venv/bin/python3 fidelity_visa.py status
```

All rows should be `posted`, `duplicate`, `skipped`, or intentionally
parked. If anything unexpected remains, investigate before proceeding.

### 10. Seed new merchant rules

Any merchants categorized manually this session should be added to
`stage.merchant_rules` so they auto-categorize next time.

### 11. Move source file to processed

```bash
mv /mnt/media/BusinessRecords/LeoBloomImportData/<filename>.csv \
   /mnt/media/BusinessRecords/LeoBloomImportData/processed/
```

## Accounts

| Role | Code | Ledger ID | Name |
|---|---|---|---|
| Credit (liability) | 2220 | 13 | Fidelity Rewards Visa |
| Debit (expense) | varies | varies | Per merchant rule |

## Reference Format

`fidelity-visa:{ref_id}` — stored as `journal_entry_reference` on the
posted JE. Native refs use the long numeric token; composite refs use
`date|memo|amount`.
