# 083 — Closed Period Enforcement, Reversing Entries & Adjustments

**Epic:** K — Fiscal Period Closure
**Depends On:** 081
**Status:** Not started

---

Complete the closed-period safety net: block voids on closed periods,
introduce reversing entries as the alternative, and add the adjustment
workflow for post-close corrections.

**Origin:** Hobson brief `fiscal-period-closure.md` §2, §3, §4.

## What exists today

- JE posting already rejects closed periods (P055)
- JE voiding does NOT check period status — **this is the gap**
- No reversing entry mechanism
- No adjustment-for-period tagging

## Part 1: Void enforcement on closed periods

### Change to `JournalEntryService.voidEntry`

Before voiding, look up the JE's `fiscal_period_id`, check `is_open`. If
the period is closed, reject with an actionable error:

```
Error: Cannot void JE 472 — it belongs to closed period 'March 2026' (closed 2026-04-15).
       Post a reversing entry in the current open period instead:
       leobloom ledger reverse --journal-entry-id 472
```

## Part 2: Reversing entries

### New command: `leobloom ledger reverse --journal-entry-id <id>`

Builds a new JE that mirrors the original with debits and credits swapped:

- `entry_date` = today (or `--date` override, must fall in an open period)
- `fiscal_period_id` = derived from entry_date (standard path)
- `description` = `"Reversal of JE {id}: {original description}"`
- `source` = `"reversal"`
- Lines: same accounts, same amounts, opposite entry types
- Reference: `referenceType = "reversal"`, `referenceValue = "{original_id}"`

### Validation

- Original JE must exist and not already be voided
- Original JE must not already have a reversal (check references for
  `referenceType = 'reversal'`, `referenceValue = '{id}'`)
- Target fiscal period must be open (standard posting guard handles this)
- The original JE does NOT need to be in a closed period — reversals are
  valid anytime. But the primary use case is closed-period corrections.

### Service

Add `JournalEntryService.reverseEntry` that:
1. Loads original JE + lines
2. Validates (above)
3. Builds `PostJournalEntryCommand` with swapped lines
4. Delegates to existing `postEntry` (reuses all posting validation)
5. Returns the new JE

## Part 3: Post-close adjustments

### Schema migration

**`ledger.journal_entry` — add column:**

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `adjustment_for_period_id` | `integer` | yes | FK to `fiscal_period(id)` ON DELETE RESTRICT |

Backfill: all existing JEs get `NULL`.

### Domain type change

Add `adjustmentForPeriodId: int option` to `JournalEntry` record.

### CLI changes

Add `--adjustment-for-period` flag to `leobloom ledger post`:

- Optional. When provided, sets `adjustment_for_period_id` on the new JE.
- The JE itself posts to its normal fiscal period (derived from entry_date).
- The adjustment target period can be closed — that's the whole point.
- Validation: the referenced period must exist.

### Explicit fiscal period override

Add `--fiscal-period-id` flag to `leobloom ledger post`:

- Optional. When provided, overrides the date-derived period assignment.
- Target period must be open (existing posting guard enforces this).
- This is the escape hatch for the rare case where `entry_date` doesn't
  map to the desired period.

## Acceptance criteria

### Void enforcement

- [ ] Voiding a JE in a closed period is rejected with error naming the period and suggesting `ledger reverse`
- [ ] Voiding a JE in an open period continues to work as before
- [ ] Error message includes the period name, close date, and the reversal command syntax

### Reversing entries

- [ ] `ledger reverse --journal-entry-id N` creates a new JE with swapped debits/credits
- [ ] Reversal description is auto-generated: `"Reversal of JE {id}: {original description}"`
- [ ] Reversal posts to the fiscal period derived from the entry date (standard path)
- [ ] Reversing an already-voided JE is rejected
- [ ] Reversing a JE that already has a reversal is rejected (idempotency guard)
- [ ] Reversal JE has reference `type=reversal, value={original_id}`
- [ ] `--date` override works and validates against open periods
- [ ] `--json` supported

### Post-close adjustments

- [ ] `adjustment_for_period_id` column exists with FK constraint
- [ ] `ledger post --adjustment-for-period N` tags the JE correctly
- [ ] Adjustment target period can be closed
- [ ] Adjustment target period must exist
- [ ] JE itself posts to its own (open) fiscal period normally
- [ ] `--json` output includes `adjustmentForPeriodId`

### Fiscal period override

- [ ] `ledger post --fiscal-period-id N` overrides date-derived period
- [ ] Override target period must be open
- [ ] Omitting the flag preserves existing date-derivation behavior

## Out of scope

- Report disclosure of adjustments (P084)
- Automatic detection of "this should have been an adjustment" scenarios
- `--as-originally-closed` report mode (P084)
