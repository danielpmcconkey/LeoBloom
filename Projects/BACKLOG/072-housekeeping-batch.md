# 072 — Housekeeping Batch (Audit Cleanup)

**Epic:** Audit Remediation (2026-04-07)
**Depends On:** None
**Status:** Not started
**Priority:** Low
**Source:** Tech Debt F3/F4/F6/F11, Consistency C1/N2/N3/N4/N5/N6

---

## Problem Statement

Several low-severity audit findings can be batched into a single cleanup
pass. None of these are dangerous, but they create friction and visual
noise for the next developer (or agent).

## What Ships

1. **Npgsql version alignment** — Pin all projects to the same Npgsql major
   version (either 9.x or 10.x, not both). Currently Migrations uses 10.0.1,
   everything else uses 9.0.3.

2. **Delete ghost `Src/LeoBloom.Api/` directory** — Dead project, not in
   solution, contains stale build artifacts.

3. **Fix default log path** — `Log.fs` defaults to
   `/workspace/application_logs/leobloom` (Docker sandbox path). Change to a
   sensible host default or fail loud if the directory doesn't exist.

4. **Add `portfolio` to connection string search path** — Currently
   `Search Path=ledger,ops,public`. Add `portfolio`.

5. **Add `isJson` to `ReportCommands.dispatch`** — The only dispatch
   function that omits the `isJson` parameter. Thread it from Program.fs
   like every other command module.

6. **Empty-list messaging** — `formatInvoiceList` and `formatTransferList`
   return empty string instead of `"(no invoices found)"` /
   `"(no transfers found)"`. Match the dominant pattern.

7. **Extract `readEntry` helper in JournalEntryRepository** — The reader
   mapping is inlined 3 times. Every other repository has a shared reader
   helper.

8. **Rename `mapReader` to `readAgreement`** in
   ObligationAgreementRepository — match the `read{Entity}` convention.

9. **Replace `"Query error:"` with `"Persistence error:"`** in
   AccountBalanceService — match the dominant error message convention.

10. **Replace local `addOpt` in FundRepository** with
    `DataHelpers.optParam` — match the existing convention.

## Acceptance Criteria

- AC-1: All projects reference the same Npgsql major version
- AC-2: `Src/LeoBloom.Api/` directory is deleted
- AC-3: All existing tests pass with zero behavioral changes
