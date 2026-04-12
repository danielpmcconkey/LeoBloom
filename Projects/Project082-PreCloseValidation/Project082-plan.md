# Project 082 — Pre-Close Validation — Plan

## Objective

Add GAAP-informed validation checks that run before a fiscal period can be
closed. Four checks (trial balance equilibrium, balance sheet equation, data
hygiene, open obligations) must all pass — or the operator must `--force`
with a `--note` explaining why. A dry-run `fiscal-period validate` command
lets Hobson preview results without closing.

## Spec Deviation: Module Placement

The spec places `FiscalPeriodValidation.fs` in `LeoBloom.Ledger`. However,
check #4 (open obligations) requires querying `ObligationInstanceRepository`
which lives in `LeoBloom.Ops`. The dependency graph is:

    Ops → Ledger → Domain

Ledger cannot reference Ops. Two clean options:

1. **Place the module in `LeoBloom.Ops`** — it already sees both Ledger and
   Ops repositories. Simple, no injection plumbing.
2. **Place in Ledger with function injection** — checks 1–3 run natively,
   check 4 is injected as `unit -> ValidationCheck`. The CLI wires it up.

**Decision: Option 1.** The module lives in `LeoBloom.Ops` as
`FiscalPeriodValidation.fs`. It's pragmatic — the alternative adds
indirection for no real benefit. The module is still purely about fiscal
period concerns; its Ops residency is a dependency-graph artifact, not a
domain signal.

## Phases

### Phase 1: Domain Types

**What:** Add `ValidationCheck` and `PreCloseValidationResult` types to
`LeoBloom.Domain/Ledger.fs`. Extend `CloseFiscalPeriodCommand` with `force`
field.

**Files:**
- `Src/LeoBloom.Domain/Ledger.fs` — modified (add types, extend command)

**Types to add (in the fiscal period types section):**

```fsharp
type ValidationCheck =
    { name: string
      passed: bool
      detail: string }

type PreCloseValidationResult =
    { checks: ValidationCheck list
      allPassed: bool }
```

**Extend `CloseFiscalPeriodCommand`:**

```fsharp
type CloseFiscalPeriodCommand =
    { fiscalPeriodId: int
      actor: string
      note: string option
      force: bool }          // new field
```

**Verification:** Project compiles. Existing tests still pass (they'll need
the new `force = false` field added to their command construction).

### Phase 2: Validation Module

**What:** Create `FiscalPeriodValidation.fs` in `LeoBloom.Ops` with all four
checks and a `validate` orchestrator.

**Files:**
- `Src/LeoBloom.Ops/FiscalPeriodValidation.fs` — created
- `Src/LeoBloom.Ops/LeoBloom.Ops.fsproj` — modified (add Compile entry)

**Functions:**

```fsharp
module FiscalPeriodValidation =

    /// Check 1: SUM(debits) = SUM(credits) for non-voided JEs in period
    let checkTrialBalance (txn) (periodId) : ValidationCheck

    /// Check 2: A = L + E as of period end_date
    /// Reuses BalanceSheetService.getAsOfDate
    let checkBalanceSheetEquation (txn) (period: FiscalPeriod) : ValidationCheck

    /// Check 3a: No voided JEs with NULL void_reason
    /// Check 3b: No JEs with entry_date outside period range
    let checkDataHygiene (txn) (period: FiscalPeriod) : ValidationCheck list

    /// Check 4: No in_flight obligation instances in period date range
    let checkOpenObligations (txn) (period: FiscalPeriod) : ValidationCheck

    /// Run all checks, return composite result
    let validate (txn) (period: FiscalPeriod) : PreCloseValidationResult
```

**SQL for check 1 (trial balance):**
```sql
SELECT
    SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE 0 END) as total_debits,
    SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END) as total_credits
FROM ledger.journal_entry je
JOIN ledger.journal_entry_line jel ON jel.journal_entry_id = je.id
WHERE je.fiscal_period_id = @period_id
  AND je.voided_at IS NULL
```

**Check 2 (balance sheet):** Call `BalanceSheetService.getAsOfDate txn period.endDate`,
inspect `report.isBalanced`. On failure, report `assets.sectionTotal` vs
`liabilities.sectionTotal + totalEquity`.

**SQL for check 3a (void reason):**
```sql
SELECT id FROM ledger.journal_entry
WHERE fiscal_period_id = @period_id
  AND voided_at IS NOT NULL
  AND (void_reason IS NULL OR void_reason = '')
```

**SQL for check 3b (entry date outside range):**
```sql
SELECT id FROM ledger.journal_entry
WHERE fiscal_period_id = @period_id
  AND voided_at IS NULL
  AND (entry_date < @start_date OR entry_date > @end_date)
```

**SQL for check 4 (open obligations):**
```sql
SELECT oi.id, oa.name as agreement_name
FROM ops.obligation_instance oi
JOIN ops.obligation_agreement oa ON oa.id = oi.obligation_agreement_id
WHERE oi.expected_date >= @start_date
  AND oi.expected_date <= @end_date
  AND oi.status = 'in_flight'
  AND oi.is_active = true
```

**File ordering in .fsproj:** Insert `FiscalPeriodValidation.fs` after
`ObligationPostingService.fs` (needs access to repos above it). Place it
before `TransferRepository.fs` — or at the end of the obligation-related
block.

**Verification:** Module compiles. Unit test calling `validate` on a clean
period returns `allPassed = true`.

### Phase 3: Service Integration

**What:** Wire validation into `FiscalPeriodService.closePeriod`. Add force
bypass logic and audit trail enrichment.

**Files:**
- `Src/LeoBloom.Ledger/FiscalPeriodService.fs` — modified

Wait — `FiscalPeriodService` is in `LeoBloom.Ledger` which can't see
`LeoBloom.Ops`. The service orchestration must move.

**Revised approach:** Create a new `FiscalPeriodCloseService.fs` in
`LeoBloom.Ops` that wraps the validation + close flow. The existing
`FiscalPeriodService.closePeriod` stays untouched (it's still the raw close
operation). The new service is the "validated close" entry point.

**Files:**
- `Src/LeoBloom.Ops/FiscalPeriodCloseService.fs` — created
- `Src/LeoBloom.Ops/LeoBloom.Ops.fsproj` — modified (add Compile entry)

```fsharp
module FiscalPeriodCloseService =

    /// Close with pre-validation. Returns validation result on failure
    /// (unless force=true, in which case closes and logs bypass).
    let closeWithValidation
        (txn: NpgsqlTransaction)
        (cmd: CloseFiscalPeriodCommand)
        : Result<FiscalPeriod * PreCloseValidationResult, PreCloseValidationResult>
```

**Logic:**
1. Look up the fiscal period (error if not found or already closed)
2. Run `FiscalPeriodValidation.validate txn period`
3. If `allPassed` or `cmd.force`:
   - If `cmd.force && not allPassed`: require `cmd.note` is `Some _`
     (return error if missing)
   - Build audit note: include validation summary + force reason
   - Call `FiscalPeriodService.closePeriod txn { cmd with note = enrichedNote }`
   - Return `Ok (period, validationResult)`
4. If not passed and not force:
   - Return `Error validationResult`

**Verification:** Service correctly blocks close on validation failure,
allows force bypass, enriches audit note.

### Phase 4: CLI Changes

**What:** Add `--force` flag to `PeriodCloseArgs`. Add `Validate` subcommand.
Update `handleClose` to call `FiscalPeriodCloseService`. Add `handleValidate`.

**Files:**
- `Src/LeoBloom.CLI/PeriodCommands.fs` — modified

**Changes:**

1. Add `Force` case to `PeriodCloseArgs`:
   ```fsharp
   type PeriodCloseArgs =
       | [<MainCommand; Mandatory>] Period of string
       | Actor of string
       | Note of string
       | Force
       | Json
   ```

2. Add `PeriodValidateArgs` DU:
   ```fsharp
   type PeriodValidateArgs =
       | [<MainCommand; Mandatory>] Period of string
       | Json
   ```

3. Add `Validate` case to `PeriodArgs`:
   ```fsharp
   | [<CliPrefix(CliPrefix.None)>] Validate of ParseResults<PeriodValidateArgs>
   ```

4. Update `handleClose`:
   - Parse `--force` flag
   - If `force && note.IsNone`, print error and return
   - Call `FiscalPeriodCloseService.closeWithValidation` instead of
     `FiscalPeriodService.closePeriod`
   - On `Error validationResult`, format check list with pass/fail markers

5. Add `handleValidate`:
   - Resolve period, look it up
   - Call `FiscalPeriodValidation.validate`
   - Format and print results (same format as close failure output)
   - Always returns success exit code (it's informational)

6. Wire `Validate` into dispatch.

**Verification:** `fiscal-period close 1` runs validation first.
`fiscal-period validate 1` prints check results. `--force` without `--note`
is rejected.

### Phase 5: Test Updates

**What:** Update existing `FiscalPeriodCloseMetadataTests.fs` to include the
new `force` field. Add new test file for validation.

**Files:**
- `Src/LeoBloom.Tests/FiscalPeriodCloseMetadataTests.fs` — modified
  (add `force = false` to all `CloseFiscalPeriodCommand` constructions)
- `Src/LeoBloom.Tests/PreCloseValidationTests.fs` — created
- `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` — modified

**Test scenarios (new file):**
- Clean period passes all checks
- Trial balance disequilibrium detected (requires direct SQL to break invariant)
- Voided JE with NULL void_reason detected
- JE with entry_date outside period range detected
- In-flight obligation instance blocks close
- Expected/Overdue/Confirmed/Posted obligations do NOT block
- Force bypass closes despite failures, audit note includes check details
- Force without note is rejected
- Multiple failures reported together (not fail-fast)
- Validate command (dry-run) returns results without closing

**Verification:** All tests pass. `dotnet test` green.

## Acceptance Criteria

### Behavioral (→ Gherkin)
- [ ] `fiscal-period close --id N` runs all 4 validation checks before closing
- [ ] Trial balance disequilibrium blocks close with error showing debit/credit totals
- [ ] Balance sheet equation failure blocks close with error showing A, L+E values
- [ ] Voided JE with NULL void_reason blocks close, listing offending JE IDs
- [ ] JE with entry_date outside period range blocks close, listing offending JE IDs
- [ ] In-flight obligation instances block close, listing instance IDs and agreement names
- [ ] Expected/Overdue/Confirmed/Posted obligation instances do NOT block close
- [ ] `--force --note "reason"` bypasses validation, logs bypass in audit entry
- [ ] `--force` without `--note` is rejected with clear error
- [ ] `fiscal-period validate --id N` runs checks and reports without closing
- [ ] Validation results are logged even when force-closing
- [ ] All checks compose — multiple failures are all reported, not just the first

### Structural (→ Builder/QE verify)
- [ ] `FiscalPeriodValidation.fs` exists in `LeoBloom.Ops`
- [ ] `FiscalPeriodCloseService.fs` exists in `LeoBloom.Ops`
- [ ] `CloseFiscalPeriodCommand` has `force: bool` field
- [ ] `ValidationCheck` and `PreCloseValidationResult` types exist in Domain
- [ ] `PeriodCloseArgs` includes `Force` case
- [ ] `PeriodArgs` includes `Validate` subcommand
- [ ] All existing tests updated with `force = false` and still pass

## Risks

1. **Dependency graph forces module placement in Ops, not Ledger.** The spec
   says Ledger, but the obligation check requires Ops visibility. Flagged
   above — PO should confirm this is acceptable. The alternative (function
   injection) adds complexity for no functional benefit.

2. **Balance sheet equation check depends on BalanceSheetService.** If that
   service has a bug, the validation check inherits it. This is intentional —
   the pre-close check should use the same computation as the report, not a
   parallel one.

3. **GAAP: Trial balance should always pass.** It's enforced at post time.
   If this check ever fails, it means there's a bug in the posting pipeline.
   The check is belt-and-suspenders, which is exactly what pre-close
   validation is for.

4. **P083 adjustment exception not implemented.** Per PO direction, check 3b
   (entry_date outside range) has no adjustment exception yet. P083 will add
   it. The current implementation is stricter, which is correct for now.

5. **FiscalPeriodCloseService wraps FiscalPeriodService.closePeriod.** This
   creates a two-layer service pattern (validated close → raw close). The CLI
   calls the validated version; the raw version remains for internal use and
   backward compatibility. This is deliberate, not accidental complexity.

## Out of Scope

- Automated remediation of failed checks
- Blocking close on unreconciled bank statements
- P083 adjustment tagging exception
- Configurable check severity (all checks are blocking or nothing)
