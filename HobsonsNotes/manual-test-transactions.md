# Manual Test Transactions — Dedup Verification

> Posted 2026-04-10 by Hobson. One transaction per FI source, posted to
> the ledger via CLI with the proper reference. When the parser runs on
> the full import files, these should be detected as duplicates during
> promotion (Layer 2 dedup).

## 1. Fidelity Visa

- **Source row:** CSV line 10 — `2026-01-05, DEBIT, FOOD LION #1614 INDIAN TRAIL NC, memo 5433, -39.27`
- **Ref:** `fidelity-visa:2026-01-05|5433|-39.27`
- **JE ID:** 7 (original JE 2 voided — ref format changed to composite)
- **Entry:** Dr 5350 (Food) 39.27, Cr 2220 (Fidelity Rewards Visa) 39.27
- **Period:** 1 (2026-01)
- **Why this one:** Common grocery purchase, clean amount, unambiguous category.
- **Note:** Short 4-digit memos (pre-2026-01-22) use composite ref `date|memo|amount`
  because the 4-digit codes are NOT reliably unique (memo `2322` appeared twice
  for different transactions). Long numeric refs (post-2026-01-22) are native.

## 2. Fidelity Transaction History

- **Source row:** CSV line 19 — `03/03/2026, JOINT Taxable Brokerage, Z08806967, DIRECT DEPOSIT ELAN CARDSVCRedemption (Cash), 169.3`
- **Ref:** `fidelity-history:Z08806967|20260303|169.30`
- **Entry:** Dr 1210 (Fidelity Brokerage) 169.30, Cr 4200 (Personal Revenue) 169.30
- **Period:** 3 (2026-03)
- **Why this one:** Cash-back reward (revenue), composite key, tests the non-merchant-rule path. The Fidelity history parser classifies by Action field, not merchant name.
- **Note:** CSV shows amount as `169.3` — composite key uses 2-decimal normalisation: `169.30`.

## 3. SECU

- **Source row:** Jan statement p2 — `01-12-26, 01-12-26, -, 423.83, ALLSTATE INS CO INS PREM, 026009009807398`
- **Ref:** `secu:026009009807398`
- **Entry:** Dr 5450 (Automotive) 423.83, Cr 1220 (SECU Checking) 423.83
- **Period:** 1 (2026-01)
- **Why this one:** Native ref (embedded in description), auto insurance, clean amount, no transfer pairing needed.

## 4. Ally

- **Source row:** Jan statement p3 (Held for investments, x7463) — `01/05/2026, Interest Paid, credit $0.49`
- **Ref:** `ally:2170477463|20260105|0.49`
- **Entry:** Dr 1300 (Ally Savings) 0.49, Cr 4200 (Personal Revenue) 0.49
- **Period:** 1 (2026-01)
- **Why this one:** Only real transaction in the early Ally statements. Composite key. Small amount but sufficient for dedup verification. Personal Ally spending account had zero activity until March.

## 5. Synchrony Amazon

- **Source row:** Jan statement p3 — `01/11, P934200QWEHMB0S1G, AMAZON MARKETPLACE SEATTLE WA, $72.66`
- **Ref:** `synchrony:P934200QWEHMB0S1G`
- **Entry:** Dr 6000 (General Household) 72.66, Cr 2240 (Synchrony — Amazon Card) 72.66
- **Period:** 1 (2026-01)
- **Why this one:** Native ref (P934200... format), common Amazon purchase, clear category.

---

## Verification Plan

After posting these five entries:
1. Confirm each JE exists via `ledger show`
2. Confirm each has a `journal_entry_reference` with the correct type and value
3. When each parser runs, the staging row with the matching ref should be detected as already existing in the ledger
4. If any manually-posted transaction is NOT flagged as a duplicate, the promotion logic is broken — fix before proceeding
