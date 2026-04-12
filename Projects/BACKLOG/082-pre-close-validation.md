# 082 — Pre-Close Validation

**Epic:** K — Fiscal Period Closure
**Depends On:** 081
**Status:** Not started

---

Add GAAP-informed validation checks that run before a fiscal period can be
closed. The close command blocks unless all checks pass or `--force` is used.

**Origin:** Hobson brief `fiscal-period-closure.md` §1 (pre-close validation).

## Validation checks

### 1. Trial balance equilibrium

`SUM(debits) = SUM(credits)` for all non-voided JEs in the period.

This should always be true — it's enforced at post time by
`validateBalanced`. But verifying at close time catches any future bug that
might break the invariant. Belt and suspenders.

Query:
```sql
SELECT
    SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE 0 END) as total_debits,
    SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END) as total_credits
FROM ledger.journal_entry je
JOIN ledger.journal_entry_line jel ON jel.journal_entry_id = je.id
WHERE je.fiscal_period_id = @period_id
  AND je.voided_at IS NULL
```

### 2. Balance sheet equation

Assets = Liabilities + Equity as of the period's `end_date`. Use the same
computation path as the balance sheet report / P064 verification.

### 3. Data hygiene

- No voided JEs in the period with NULL `void_reason`
- No JEs with `fiscal_period_id = @period_id` where `entry_date` falls
  outside the period's `start_date..end_date` range (unless tagged as an
  adjustment — see P083)

### 4. Open obligations check

No obligation instances in the period (i.e., `expected_date` within
`start_date..end_date`) are still in `in_flight` status. Expected is fine
(they might just not be due yet at month-end). Overdue is fine (they've been
flagged). But `in_flight` means someone started processing it and didn't
finish — that's an incomplete transaction.

Note on irregular cadences: obligations with `cadence = irregular` may never
pass through `in_flight`. The check only blocks on instances that ARE
`in_flight`, not on instances that never entered that state.

## Force bypass

`--force` bypasses all validation. Requires `--note` explaining why. The
audit entry (written by P081's close mechanism) includes the force flag and
note. The validation results are still computed and logged even under force,
so the audit trail shows what was bypassed.

## Implementation

### New module: `FiscalPeriodValidation.fs` (in `LeoBloom.Ledger`)

```fsharp
type ValidationCheck =
    { name: string
      passed: bool
      detail: string }

type PreCloseValidationResult =
    { checks: ValidationCheck list
      allPassed: bool }

let validate (txn: NpgsqlTransaction) (periodId: int) : PreCloseValidationResult
```

### Service changes

`FiscalPeriodService.closePeriod` gains:
- `force: bool` parameter
- `note: string option` parameter (required when force = true)
- Calls `FiscalPeriodValidation.validate` before closing
- If any check fails and `force = false`, returns `Error` with check details
- If `force = true`, closes anyway, logs bypassed checks in audit note

### CLI changes

- `fiscal-period close --id N` gains `--force` and `--note` flags
- `--force` without `--note` is rejected
- Validation failure output lists each check with pass/fail and detail
- Add `fiscal-period validate --id N` command to run checks without closing
  (dry-run for pre-close review)

## Acceptance criteria

- [ ] `fiscal-period close --id N` runs all 4 validation checks before closing
- [ ] Trial balance disequilibrium blocks close with specific error showing debit/credit totals
- [ ] Balance sheet equation failure blocks close with specific error showing A, L+E values
- [ ] Voided JE with NULL void_reason blocks close, listing the offending JE IDs
- [ ] JE with entry_date outside period range blocks close, listing offending JE IDs
- [ ] In-flight obligation instances block close, listing instance IDs and agreement names
- [ ] Expected/Overdue/Confirmed/Posted obligation instances do NOT block close
- [ ] `--force --note "reason"` bypasses validation, logs bypass in audit entry
- [ ] `--force` without `--note` is rejected with clear error
- [ ] `fiscal-period validate --id N` runs checks and reports results without closing
- [ ] Validation results are logged even when force-closing
- [ ] All checks compose — multiple failures are all reported, not just the first

## Out of scope

- Automated remediation of failed checks
- Blocking close on unreconciled bank statements (no bank reconciliation module exists)
