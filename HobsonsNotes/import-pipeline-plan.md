# Transaction Import Pipeline — Hobson's Plan

> Authored 2026-04-08. Covers the full import pipeline design from FI exports
> to posted journal entries. BD's side is in backlog cards P075-078. This doc
> covers Hobson's side: parsers, categorization, review workflow, and execution.

## Architecture

```
FI exports (CSV/PDF)
    → Parser (per FI, Python)
        → stage.{fi}_transactions (one table per FI, in LeoBloom DB)
            → Hobson categorizes (merchant rules table)
                → Dan reviews (unknowns, possible dupes, transfers)
                    → CLI posts to ledger (with journal_entry_reference for audit trail)
```

## Key Design Decisions

- **Staging schema**: `stage` schema in `leobloom_prod`, owned by Hobson-side
  scripts. Not in BD's migration chain unless/until we formalize it.
- **Source-matched staging**: one table per FI, columns match source format.
  Preserves source detail for review. Adding a new FI = one new table + parser.
- **Deduplication**: Sources with native ref IDs (Visa memo ref, SECU embedded
  ref, Synchrony ref#) use those. Sources without (Fidelity brokerage, Ally)
  use composite key: account_number + date + amount. Exact ref match =
  auto-skip. Composite match = flagged for Dan's review.
- **Transfers**: Two-sided. Both legs must be present in staging before
  promotion. Staged as separate rows, linked at promotion time. Ledger
  reference stored as `{from_ref}|{to_ref}`.
- **Posting**: Always through CLI (`leobloom ledger post`). No direct DB
  inserts for ledger tables.
- **Provenance**: Stored as `journal_entry_reference` entries (type + value)
  on posted JEs. Staging table gets `journal_entry_id` FK once promoted.
- **Category mapping**: Hobson proposes via merchant rules table, Dan reviews
  unknowns.
- **Review/promotion workflow**: MVP is conversational (Dan + Hobson).

## Data Sources

| Source | Format | Files | Ref ID | Dedup Key |
|---|---|---|---|---|
| Fidelity Rewards Visa | CSV | 1 | Memo field (long numeric ref) | Native ref |
| Fidelity CMA/Brokerage | CSV | 2 | None | account + date + amount |
| Ally Bank (personal) | PDF | 4 | None | account + date + amount |
| SECU | PDF | 3 | Embedded in description | Native ref |
| Synchrony Amazon | PDF | 3 | Reference # (P934200...) | Native ref |

Import data location: `/mnt/media/BusinessRecords/LeoBloomImportData/`

Synchrony Amazon account number: x9145 (from statement header).

Fidelity Rewards Visa note: the converted January statement data (from
HobsonConvertThese) uses 4-digit ref numbers in the memo field. These are
from a one-time manual conversion. All future imports use the standard CSV
export which has long numeric refs. The 4-digit refs are adequate for the
historical data — they won't recur.

## Deduplication Design

### Re-import protection (idempotency)
- UNIQUE constraint on `(ref_id, batch_id)` in each staging table.
- Re-running a parser on the same file → INSERT ON CONFLICT DO NOTHING.

### Cross-source overlap (transfers)
- Same real-world transaction appears in multiple FI exports (e.g., a credit
  card payment shows in both the Visa CSV and the SECU statement).
- Both sides stage independently. Promotion script checks: does the other
  side exist in staging? If yes, promote as a paired transfer. If no, hold.
- Once posted, `journal_entry_reference` on the ledger entry records both
  source refs. Future imports check references before staging.

### Collision risk for composite keys
- Fidelity brokerage and Ally lack native ref IDs. Composite key:
  `account_number|date|amount`.
- Two transactions on the same account, same day, same amount → ambiguous.
  These are flagged status = 'review' for manual resolution.
- Acceptable tradeoff: Dan doesn't need per-penny attribution between
  identical transactions (e.g., two $1,000 Zelle payments on the same day).

## COA Expansion (Step 1 — blocked on P077)

17 new accounts needed before importing. See backlog card P077 for the CLI
command. Full account list with codes, names, subtypes, and FI numbers is
in the plan file and in `HobsonsNotes/procedures/fi-account-numbers.md`.

**New assets (parent 1200):** 1230-1350 (13 accounts — Fidelity IRAs, SECU
money market/share, Ally personal, T. Rowe, HSA).

**New liabilities (parent 2200):** 2230-2260 (4 accounts — auto loan,
Synchrony Amazon, Clear Balance medical).

## Parsers (Step 3)

Python scripts in `~/penthouse-pete/leobloom-ops/imports/`.

**Phase 1 — CSV (immediate after schema):**
- `parse_fidelity_visa.py` — extract ref from memo (first token before `;`)
- `parse_fidelity_history.py` — construct composite ref, strip footer disclaimers

**Phase 2 — PDF (after CSV pipeline is proven):**
- `parse_secu.py` — pdfplumber, multi-account, extract embedded refs
- `parse_ally.py` — pdfplumber, multi-account, sparse activity
- `parse_synchrony.py` — pdfplumber, extract reference numbers + item descriptions

All parsers are idempotent. batch_id = source filename.

## Categorization (Step 4)

`categorize.py` applies `stage.merchant_rules` patterns to all 'new' rows.
Matched → status = 'categorized'. Unmatched → stay 'new' for Hobson/Dan review.

Merchant rules are global (not per-source). SQL LIKE patterns.

## Review & Promotion (Step 5)

**MVP workflow (conversational):**
1. Hobson reports: X new, Y categorized, Z possible dupes, W transfers pending
2. Dan reviews unknowns, Hobson proposes categories
3. Confirmed patterns → added to merchant_rules
4. Promotion script calls CLI per reviewed row
5. Staging row updated with journal_entry_id, status = 'posted'

**Transfer promotion:**
- Both legs must be status = 'reviewed'
- Single CLI call: debit destination, credit source
- Reference: `{from_ref}|{to_ref}`

**CLI call pattern:**
```
leobloom ledger post \
  --debit {expense_acct}:{amount} --credit 2220:{amount} \
  --date {date} --description "{merchant}" \
  --source "fidelity-visa" --fiscal-period-id {period} \
  --ref fidelity-visa:{ref_id}
```

## Verification (Step 6)

- Trial balance after each batch
- Spot check: source file → staging → journal entry
- Reconciliation: staging totals vs. FI statement ending balances
- Dedup audit: query for any ref_id posted more than once

## Execution Order

1. COA expansion (blocked on P077 — account create CLI)
2. Stage schema + tables (P078 or Hobson-side script)
3. Merchant rules seed
4. Fidelity Visa parser → categorize → review → post (full pipeline on one source)
5. Fidelity History parser
6. PDF parsers (SECU, Ally, Synchrony) one at a time
7. Cross-source transfer matching

## Dependencies on BD

| Need | Backlog | Status |
|---|---|---|
| Account create CLI (with subtype) | P077 | Not started |
| Stage schema DDL | P078 | Not started |
| External ref on ledger.account | P075 | Not started |
| Account update CLI | P076 | Not started |

P077 is the critical blocker. P078 can run in parallel. P075/076 are nice-to-have
for the initial import but become essential for reconciliation.
