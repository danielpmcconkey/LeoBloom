# Transaction Import Pipeline — Hobson's Plan

> Originally authored 2026-04-08. Revised 2026-04-09 after design
> session with Dan covering dedup rules, expense taxonomy, crosswalk,
> and Fidelity history handling.

> Note from Dan to Hobson: there are inaccuracies in this file. We are still referencing it while we build new importers. But, once those are done, the source of truth will be in the procedures doc. For now, we keep this document intact to use as reference and to confirm that we captured all our ideas from planning. 

## Architecture

```
FI exports (CSV/PDF)
    → Parser (per FI, Python)
        → stage.{fi} tables (one table per FI, in leobloom_prod)
            → Hobson categorises (merchant rules + Fidelity action rules)
                → Promote clean rows to ledger via CLI
                → Flag ambiguous rows for Dan's review
                    → Dan + Hobson review every remaining row
                        → Staging empty when done
```

## Import Cycle

Every import round follows this sequence. No shortcuts.

1. **Parse** source file into staging table. `batch_id` = source filename.
2. **Auto-categorise** using `stage.merchant_rules` (expenses) and
   action-based rules (Fidelity history).
3. **Dedup check** against ledger — query `journal_entry_reference` for
   each row's ref. Rows already in the ledger get status = `duplicate`.
4. **Promote clean rows** — native ref, no ledger match, not a transfer,
   category assigned. Post each via CLI.
5. **Flag composite-key matches** — these ALWAYS go to review. Never
   auto-skip. Dan decides.
6. **Match transfer pairs** — check both staging AND the ledger's
   `journal_entry_reference`. A partner may already be posted from a
   previous batch or a different FI source.
7. **Dan and Hobson review everything still in staging.** Every row, no
   exceptions. This includes orphaned transfer legs, uncategorised
   merchants, composite-key ambiguities, and anything else the
   automated logic couldn't resolve.
8. **Staging is empty when we're done.** If it's not, something is wrong
   and we figure out why before the next round.
9. **Move source file to `processed/`.** Once staging is clear for a
   batch, move the source file from the import data directory to
   `processed/`. This is how we know what's already been imported.
   Path: `/mnt/media/BusinessRecords/LeoBloomImportData/processed/`

## Deduplication — Three Layers

### Layer 1: Batch-level idempotency

`UNIQUE(ref_id, batch_id)` on every staging table. Re-running a parser
on the same file produces `INSERT ON CONFLICT DO NOTHING`. Mechanical.

### Layer 2: Ledger dedup

Before promoting any row from staging, check `journal_entry_reference`
via `findNonVoidedByReference()` for an existing entry with that ref.

- **Native ref match** (Visa, SECU, Synchrony): auto-mark as duplicate.
- **Composite key match** (Fidelity history, Ally): ALWAYS flag for
  Dan's review. Never auto-skip. Two genuinely distinct transactions
  can produce the same composite key.

### Layer 3: Transfer pairing

Two-sided transactions don't promote until both sides are accounted for.
"Accounted for" means either:
- Both sides are present in staging, OR
- One side is in staging and the partner is already in the ledger
  (check `journal_entry_reference` for the expected partner ref)

Posted as a single balanced JE. Reference: `{from_ref}|{to_ref}`.

If one side can't be found in staging or the ledger, it stays in staging
for the manual review sweep.

## Data Sources

| Source | Format | Ref ID | Dedup Key |
|---|---|---|---|
| Fidelity Rewards Visa | CSV | Memo field (long numeric) | Native ref |
| Fidelity Transaction History | CSV | None | account + date + amount |
| Ally Bank | PDF | None | account + date + amount |
| SECU | PDF | Embedded in description | Native ref |
| Synchrony Amazon | PDF | Reference # (P934200...) | Native ref |

Import data location: `/mnt/media/BusinessRecords/LeoBloomImportData/`

## Fidelity Transaction History — Handling Rules

The Fidelity history CSV contains multiple accounts and transaction
types. Not all are ledger events. Rules by action:

### Taxable Brokerage (Z08806967 → 1210)

| Action | Handling |
|---|---|
| DIVIDEND RECEIVED (non-SPAXX) | Dr 1210, Cr 4200. Revenue. |
| DIVIDEND RECEIVED (SPAXX) | Dr 1210, Cr 4200. Money market interest. |
| REINVESTMENT (SPAXX) | SKIP. Cash stays inside account. |
| YOU BOUGHT / YOU SOLD | SKIP. Portfolio module's domain. |
| TRANSFERRED TO/FROM | TRANSFER. Pair with other leg. |
| DIRECT DEPOSIT (payroll) | SKIP. Net deposit only, don't track. |
| DIRECT DEPOSIT (cash-back) | Dr 1210, Cr 4200. Revenue. |
| DIRECT DEBIT / BILL PAYMENT | Dr expense, Cr 1210. Categorise via merchant rules. |
| Commission / Fees (non-zero) | Dr 7220, Cr 1210. |

### Property CMA (Z52355485 → 1110)

Same rules as taxable brokerage but posts against 1110.
SPAXX dividends are other revenue (4200), not rental income (4110).
Per GAAP: interest on an operating account is other revenue.

### IRA Accounts (237564538, 237566939 → 1240, 1250)

**SKIP all transactions.** Dan never withdraws from IRAs. Dividends,
reinvestments, trades — all handled by portfolio module. The ledger
tracks the account balance via periodic portfolio sync, not individual
transactions.

## Expense Taxonomy Decisions (2026-04-09)

Agreed with Dan. Full crosswalk in `HobsonsNotes/category-crosswalk.md`.

Key consolidation decisions:
- Food absorbs alcohol
- Automotive consolidated (no sub-accounts)
- Utilities consolidated
- Healthcare split: HSA-eligible (5400) vs non-HSA (5410)
- Insurance split: life/AD&D/disability (5550) vs other (5560)
- Taxes split: itemizable (5600) vs non-itemizable (5610)
- Kids' allowances consolidated into one account (6150)
- Jodi's business pulled to own subtree (6300) with 4 sub-accounts
- Personal home maintenance → General Household (6000)
- Entertainment and Online Services remain separate (5650, 5700)

## Special Transaction Rules

### Auto loan payment (Ford / 2230)
Post whole payment as transfer: Dr 2230, Cr cash. No principal/interest
split. Ford doesn't break it out on the statement. Could do a year-end
adjustment with the amortisation schedule if needed.

### Personal mortgage (SECU / 5311)
Lump payment includes principal, interest, escrowed tax and insurance.
Post whole amount to 5311 for now. Year-end adjusting entry to
reclassify principal as liability reduction on 2210 is optional.

### Merchandise refunds
Reduce the original expense account per GAAP. A grocery refund is Cr
5350, not revenue. Categoriser matches refund back to expense account
where possible. Flag for review when the original account is ambiguous.

### Payroll deposits
Skip. Dan tracks net direct deposits only, not paycheck line items.
These show up as DIRECT DEPOSIT in Fidelity history and as credits in
SECU. The cash arriving is visible in the account balance; no journal
entry needed for income tracking.

## Categorisation

### Merchant rules (`stage.merchant_rules`)
SQL LIKE patterns mapped to account codes. Seeded from Dan's 7,460
pre-categorised transactions in `householdbudget.personalfinance.tran`.
Crosswalk maps old taxonomy → new COA codes.

Rules are global (not per-source). Hobson proposes new rules, Dan
reviews. Once confirmed, the pattern goes into `merchant_rules` and
applies to all future imports.

### Action-based rules (Fidelity history only)
Handled by the parser, not merchant rules. The Fidelity CSV Action
field determines whether a row is a ledger event, a skip, or a transfer.
See handling rules table above.

## Posting Rules

- **All posting through CLI.** No direct inserts to ledger tables. The
  CLI validates business logic that isn't enforced at the DB level.
- **Provenance** stored as `journal_entry_reference` (type + value) on
  every posted JE. Staging row gets `journal_entry_id` after promotion.
- **Reference format:** `{source}:{ref_id}` — e.g.,
  `fidelity-visa:24445006022000789209313` or
  `secu:025344007589830` or
  `ally:1070833700|20260312|1000.00` (composite).

## COA Expansion

### Completed (2026-04-09)

Expense taxonomy accounts created/renamed via CLI on prod:
- Renamed: 5400, 5550, 5600, 6150
- Created: 5410, 5560, 5610, 6300, 6310, 6320, 6330, 6340

### Still needed

17 asset/liability accounts for FI account mapping. These were in the
original plan and still need creating:

**Assets (parent 1200):** 1230–1350 (Fidelity IRAs, SECU money
market/share, Ally personal, T. Rowe, HSA)

**Liabilities (parent 2200):** 2230–2260 (auto loan, Synchrony Amazon,
Clear Balance medical)

Full list with FI account numbers:
`HobsonsNotes/procedures/fi-account-numbers.md`

## Execution Order

1. ~~COA expansion — expense taxonomy~~ Done (2026-04-09)
2. COA expansion — asset/liability accounts (17 accounts)
3. Set `external_ref` on all accounts via `account update`
4. Build stage schema (P078 — Hobson, not BD)
5. Seed merchant rules from old transaction data
6. **Manual test: post one transaction per source via CLI**
7. Build Fidelity Visa parser → run on full file → confirm dedup catches
   the manual entry → review → promote
8. Build Fidelity History parser (CSV, action-based rules)
9. PDF parsers: SECU, Ally, Synchrony (one at a time)
10. Cross-source transfer matching
11. T. Rowe Price and HealthEquity — future, separate data pull needed

## Dependencies on BD

| Need | Backlog | Status |
|---|---|---|
| Account create CLI | P077 | Done |
| Account update CLI | P076 | Done |
| External ref on ledger.account | P075 | Done (migration applied) |
| Stage schema DDL | P078 | Hobson-only |

All BD dependencies are resolved. Pipeline is unblocked.
