# Cross-Source Transfer Matching

## When

After all FI sources for an import round have been parsed, categorized,
and promoted. This is the final verification step before declaring the
round complete.

## Purpose

Each FI procedure handles its own side of inter-FI transfers (e.g., SECU
posts the Visa payment as Dr 2220, Cr 1220; the Visa procedure skips the
payment credit). This step verifies that every transfer was handled
correctly across sources — no orphaned legs, no misclassifications, no
double-posts.

## What to check

### 1. Orphaned transfer legs in staging

Query each staging table for rows still in `new`, `classified`, or
`review` status that look like transfers:

```sql
-- Fidelity Visa: payment credits (should have been skipped)
SELECT * FROM stage.fidelity_visa
WHERE status NOT IN ('posted', 'duplicate', 'skipped', 'parked')
AND (proposed_account_code IS NULL OR proposed_account_code = 'TRANSFER');

-- Fidelity History: TRANSFERRED TO/FROM rows
SELECT * FROM stage.fidelity_history
WHERE status NOT IN ('posted', 'duplicate', 'skipped', 'parked')
AND action LIKE '%TRANSFER%';

-- SECU: FID BKG SVC LLC MONEYLINE or other cross-FI patterns
SELECT * FROM stage.secu
WHERE status = 'review'
AND description LIKE '%FID BKG%';

-- Ally: inter-account transfers
SELECT * FROM stage.ally
WHERE status NOT IN ('posted', 'duplicate', 'skipped', 'parked');

-- Synchrony: payment credits (F-prefix, should have been skipped)
SELECT * FROM stage.synchrony_amazon
WHERE status NOT IN ('posted', 'duplicate', 'skipped', 'parked')
AND ref_id LIKE 'F%';
```

Any results need investigation. Either the per-FI procedure missed
something, or the row needs manual resolution.

### 2. Verify skipped transfer legs match posted counterparts

For each known transfer pair, confirm one side is posted and the other
is correctly skipped:

| Transfer | Posted from | Skipped from |
|---|---|---|
| Visa autopay (SECU → Visa) | SECU: Dr 2220, Cr 1220 | Visa: payment credit, status `skipped` |
| Synchrony payment (SECU → Amazon card) | SECU: Dr 2240, Cr 1220 | Synchrony: F-prefix ref, status `skipped` |
| Ford auto loan (SECU → Ford) | SECU: Dr 2230/2235, Cr 1220 | No Synchrony/Visa leg exists |
| Fidelity MONEYLINE (SECU → Fidelity) | SECU: Dr 1210/1110, Cr 1220 | History: usually `skipped` or owner equity |
| Brokerage ↔ CMA transfers | History: owner equity (3010/3020) | Only CMA leg posts; brokerage leg skipped |

### 3. Spot-check posted transfers in the ledger

Pull recently posted transfer JEs and verify the account coding:

```bash
LEOBLOOM_ENV=Production Src/LeoBloom.CLI/bin/Release/net10.0/LeoBloom.CLI \
  journal-entry list --from YYYY-MM-DD --to YYYY-MM-DD
```

Look for:
- **Internal transfers coded as revenue or expense.** A brokerage→CMA
  move is Dr 1110, Cr 1210 (or owner equity 3010/3020) — NOT revenue.
- **Owner's Investment (3010) used for internal transfers.** 3010 is for
  money coming INTO the business from outside. Moving cash between Dan's
  own accounts is an internal transfer (Dr receiving, Cr sending).
- **Liability payments with wrong liability account.** Verify Ford goes
  to 2230 (Mach-E) or 2235 (Explorer), not the other.

### 4. Reconcile transfer totals (optional)

For high-value transfer types, sum both sides and confirm they balance:

```sql
-- Example: total Visa payments posted from SECU
SELECT SUM(amount) FROM stage.secu
WHERE status = 'promoted' AND description LIKE '%CARDMEMBER%';

-- vs. total Visa payment credits skipped on the Visa side
SELECT COUNT(*) FROM stage.fidelity_visa
WHERE status = 'skipped' AND proposed_account_code IS NULL;
```

Counts and totals should correspond. Mismatches mean a payment was
missed on one side or double-counted.

## Common errors caught by this step

| Error | How it presents | Fix |
|---|---|---|
| Internal transfer coded as Owner's Investment | Dr 1110, Cr 3010 instead of Cr 1210 | Void and repost with correct accounts |
| Payment posted from both sides | Same dollar amount posted as expense AND as liability reduction | Void the duplicate |
| Orphaned MONEYLINE on SECU side | Status `review`, no matching Fidelity History row | Ask Dan which Fidelity account received the funds |
| Skipped row that should have posted | Transfer leg marked `skipped` but no counterpart in ledger | Post the missing leg |

## When you're done

All staging tables should have zero rows in `new`, `classified`,
`categorized`, or `review` status (except intentionally parked items).
Every transfer pair should have exactly one posted side and one skipped
side (or a single balanced JE for internal SECU-to-SECU moves).

Report any corrections made (voids, reposts) in the wakeup document.
