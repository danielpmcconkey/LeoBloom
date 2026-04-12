# Project 084 — Plan

## Objective

Add period provenance headers and adjustment disclosure footers to the four
period-based reports (income statement, balance sheet, trial balance, P&L
subtree) and the je-lines extract. Introduce `--as-originally-closed` flag
for historical reproducibility and `--include-adjustments` flag on extracts.
This makes P081–P083's fiscal closure machinery visible to report consumers
and satisfies GAAP transparency requirements for period disclosure.

## Architecture Decision

**Shared `PeriodDisclosure` type and query module.** Rather than duplicating
period metadata lookups across five report paths, introduce a single
`PeriodDisclosureRepository` module that fetches all disclosure data (period
metadata + adjustment summary + adjustment detail) in one call. Each report
service enriches its result with this disclosure. The formatters then render
a shared header/footer from the disclosure type.

**Balance sheet adaptation.** The balance sheet uses `--as-of` date, not
`--period`. To support period disclosure on balance sheets, the report needs
an optional `--period` argument. When supplied, the balance sheet gets
disclosure headers. When omitted (pure as-of-date mode), no period
disclosure is shown. This matches the spec's "period-based reports" scope
without breaking the existing date-based usage.

---

## Phases

### Phase 1: Domain Types & Disclosure Repository

**What:** Define the shared `PeriodDisclosure` type and a repository module
to fetch all disclosure data from the database.

**Files:**

- **Modify** `Src/LeoBloom.Domain/Ledger.fs` — Add types:
  - `AdjustmentDetail` — `{ journalEntryId: int; entryDate: DateOnly; description: string; netAmount: decimal }`
  - `PeriodDisclosure` — `{ fiscalPeriodId: int; periodKey: string; startDate: DateOnly; endDate: DateOnly; isOpen: bool; closedAt: DateTimeOffset option; closedBy: string option; reopenedCount: int; adjustmentCount: int; adjustmentNetImpact: decimal; adjustments: AdjustmentDetail list; asOriginallyClosed: bool }`

- **Create** `Src/LeoBloom.Ledger/PeriodDisclosureRepository.fs` — Module with:
  - `getDisclosure (txn) (fiscalPeriodId) : PeriodDisclosure` — Queries `fiscal_period` for metadata, then queries `journal_entry` where `adjustment_for_period_id = @period_id AND voided_at IS NULL` for adjustment summary (count, net impact) and detail list.
  - `getDisclosureByKey (txn) (periodKey) : PeriodDisclosure option` — Resolves key, delegates to above.

- **Modify** `Src/LeoBloom.Ledger/LeoBloom.Ledger.fsproj` — Add `PeriodDisclosureRepository.fs` to compile order (after `FiscalPeriodRepository.fs`, before service modules).

**Verification:** Unit test that creates a closed period with adjustment JEs and confirms `getDisclosure` returns correct metadata, counts, and detail rows.

### Phase 2: Enrich Report Types with Disclosure

**What:** Add an optional `PeriodDisclosure` field to each period-based
report type so disclosure data flows through existing return types.

**Files:**

- **Modify** `Src/LeoBloom.Domain/Ledger.fs` — Add `disclosure: PeriodDisclosure option` field to:
  - `TrialBalanceReport`
  - `IncomeStatementReport`
  - `SubtreePLReport`
  - `BalanceSheetReport`

- **Modify** each service module to populate `disclosure`:
  - `Src/LeoBloom.Ledger/TrialBalanceService.fs`
  - `Src/LeoBloom.Ledger/IncomeStatementService.fs`
  - `Src/LeoBloom.Ledger/BalanceSheetService.fs`
  - `Src/LeoBloom.Ledger/SubtreePLService.fs`

  Each service's `getByPeriodId` / `getByPeriodKey` calls
  `PeriodDisclosureRepository.getDisclosure` and sets the field. For balance
  sheet (which doesn't have a period ID today), the disclosure is `None`
  unless a period is explicitly provided (Phase 4 adds that argument).

- **Modify** `Src/LeoBloom.Reporting/ExtractTypes.fs` — Add `PeriodMetadataEnvelope` type for JSON extract wrapping:
  - `{ periodKey: string; startDate: DateOnly; endDate: DateOnly; status: string; closedAt: DateTimeOffset option; closedBy: string option; reopenedCount: int; adjustmentCount: int; adjustmentNetImpact: decimal; lines: JournalEntryLineRow list }`

**Verification:** Existing tests still compile and pass. Services return disclosure data when periods have close metadata.

### Phase 3: Format Headers & Footers

**What:** Update `OutputFormatter.fs` to render period provenance headers and
adjustment footers from the disclosure data attached to reports.

**Files:**

- **Modify** `Src/LeoBloom.CLI/OutputFormatter.fs`:
  - Add `formatDisclosureHeader (d: PeriodDisclosure) : string list` — Renders the header block:
    - Period name and date range
    - Open/closed status with timestamp and actor
    - Reopened count
    - Adjustment count and net impact (if any)
    - Report generation timestamp
    - If `asOriginallyClosed`, note: "Showing period as of close (pre-adjustment view)"
  - Add `formatDisclosureFooter (d: PeriodDisclosure) : string list` — Renders adjustment detail list when adjustments exist
  - Integrate header into `formatTrialBalance`, `formatIncomeStatement`, `formatSubtreePL`, `formatBalanceSheet` — prepend disclosure header lines before report body, append footer after
  - For JSON output: the disclosure data serializes naturally as part of the report type (no special handling needed — `System.Text.Json` will include the `disclosure` field)

**Verification:** Run each report command against a period with close metadata and adjustments. Confirm header/footer appear in human-readable output. Confirm JSON output includes disclosure object.

### Phase 4: CLI Flags — `--as-originally-closed`

**What:** Add the `--as-originally-closed` flag to the four period-based
report commands. Wire it through to filter JEs by `created_at <= closed_at`.

**Files:**

- **Modify** `Src/LeoBloom.CLI/ReportCommands.fs`:
  - Add `As_Originally_Closed` case to `TrialBalanceArgs`, `IncomeStatementArgs`, `PnlSubtreeArgs`
  - Add `As_Originally_Closed` and `Period` cases to `BalanceSheetArgs` (period is optional; required when `--as-originally-closed` is used)
  - Update handlers to pass flag through to services

- **Modify** each service module to accept `asOriginallyClosed: bool` parameter:
  - `Src/LeoBloom.Ledger/TrialBalanceService.fs`
  - `Src/LeoBloom.Ledger/IncomeStatementService.fs`
  - `Src/LeoBloom.Ledger/BalanceSheetService.fs`
  - `Src/LeoBloom.Ledger/SubtreePLService.fs`

- **Modify** each repository module to add `closed_at` filter variant:
  - `Src/LeoBloom.Ledger/TrialBalanceRepository.fs` — Add `getActivityByPeriodAsOfClose` that adds `AND je.created_at <= @closed_at` and also includes JEs where `adjustment_for_period_id = @period_id AND created_at <= @closed_at`
  - `Src/LeoBloom.Ledger/IncomeStatementRepository.fs` — Same pattern
  - `Src/LeoBloom.Ledger/BalanceSheetRepository.fs` — Same pattern (using period + created_at filter)
  - `Src/LeoBloom.Ledger/SubtreePLRepository.fs` — Same pattern

- **Validation logic in services:**
  - If `asOriginallyClosed = true` and period is open → return `Error "Cannot use --as-originally-closed on an open period"`
  - If `asOriginallyClosed = true` and `closedAt` is `None` → return `Error "Period has no close timestamp"`
  - Set `disclosure.asOriginallyClosed = true` so the formatter shows the indicator

**Verification:** Test that `--as-originally-closed` on a closed period excludes post-close JEs. Test that it errors on an open period.

### Phase 5: Extract Enhancements — `--include-adjustments` & Metadata

**What:** Add `--include-adjustments` flag and period metadata envelope to
the je-lines extract command.

**Files:**

- **Modify** `Src/LeoBloom.CLI/ExtractCommands.fs`:
  - Add `Include_Adjustments` case to `ExtractJeLinesArgs` (default true, so add `No_Adjustments` as the flag — or use `Include_Adjustments` with default true semantics; the flag presence disables when using `--no-include-adjustments`)
  - Actually, Argu doesn't support `--no-` prefixes natively. Better approach: add `Exclude_Adjustments` flag. Default behavior (flag absent) includes adjustments. When `--exclude-adjustments` is passed, omit them. This matches the spec's "default true" intent.
  - Add `As_Originally_Closed` case to `ExtractJeLinesArgs`
  - Update `handleJeLines` to pass flags through

- **Modify** `Src/LeoBloom.Reporting/ExtractRepository.fs`:
  - Update `getJournalEntryLines` to accept `includeAdjustments: bool` and `closedAtCutoff: DateTimeOffset option`
  - When `includeAdjustments` is true: add `OR je.adjustment_for_period_id = @fiscal_period_id` to the WHERE clause
  - When `closedAtCutoff` is set: add `AND je.created_at <= @closed_at`
  - Return `PeriodMetadataEnvelope` (or handle envelope wrapping in the handler)

- **Modify** `Src/LeoBloom.CLI/ExtractCommands.fs`:
  - Update `handleJeLines` to fetch disclosure, wrap result in metadata envelope for JSON output

**Verification:** Extract with adjustments returns both direct and adjustment JEs. Extract with `--exclude-adjustments` returns only direct JEs. JSON output includes period metadata envelope.

---

## Acceptance Criteria

### Behavioral (→ Gherkin scenarios)

- [ ] Income statement, balance sheet (with `--period`), P&L subtree, and trial balance show period provenance header in human-readable output
- [ ] Header shows period name, date range, open/closed status, close timestamp + actor, and reopened count
- [ ] Header shows adjustment count and net impact when adjustment JEs exist for the period
- [ ] Open period header shows "OPEN as of report generation" status
- [ ] Footer lists individual adjustment JEs with ID, date, description, and net amount
- [ ] `--as-originally-closed` filters to JEs created at or before `closed_at`
- [ ] `--as-originally-closed` on an open period returns a clear error
- [ ] Extract `je-lines` includes adjustment JEs by default when targeting a period
- [ ] Extract `je-lines` with `--exclude-adjustments` omits adjustment JEs
- [ ] JSON output includes period metadata in the response envelope

### Structural (→ Builder/QE verify)

- [ ] `PeriodDisclosure` type exists in `Ledger.fs`
- [ ] `PeriodDisclosureRepository.fs` exists and compiles
- [ ] `PeriodDisclosureRepository.fs` is in `.fsproj` compile order
- [ ] All four report types have `disclosure: PeriodDisclosure option` field
- [ ] `PeriodMetadataEnvelope` type exists in `ExtractTypes.fs`
- [ ] `--as-originally-closed` flag registered on all period-based report commands
- [ ] `--exclude-adjustments` flag registered on `je-lines` extract command

---

## Risks

1. **Report type field addition is a breaking change for JSON consumers.**
   Adding `disclosure` to the report types adds a new field to JSON output.
   Since it's additive (new optional field), this is backward-compatible for
   well-behaved JSON consumers. However, consumers doing strict schema
   validation would break. Mitigation: this is acceptable — we don't promise
   stable JSON schemas yet, and the disclosure data is required by GAAP.

2. **Balance sheet period integration.** The balance sheet currently uses
   `--as-of` date, not `--period`. Adding optional `--period` for disclosure
   is new surface area. Mitigation: make `--period` optional; when absent,
   balance sheet works exactly as before with no disclosure header.

3. **`created_at` vs `date` for as-originally-closed.** The spec explicitly
   uses `created_at` for the filter, not `entry_date`. This is correct —
   `created_at` is the DB insertion timestamp, which represents when the
   entry was actually recorded. An adjustment JE might have an `entry_date`
   within the closed period but a `created_at` after the close.

4. **GAAP: Period disclosure is a transparency requirement.** The adjustment
   footer and provenance header directly support audit trail requirements.
   The `--as-originally-closed` flag supports audit reproducibility. No GAAP
   concerns with the approach — it's implementing a GAAP requirement.

5. **Reopened period + as-originally-closed uses latest `closed_at`.**
   Documented limitation per spec. Users needing "first close" view must
   consult the audit trail (P081) for the original timestamp.

---

## Out of Scope

- `--as-of-timestamp` for arbitrary historical snapshots
- Automatic comparison between as-adjusted and as-originally-closed views
- PDF/export formatting of disclosure headers
- Disclosure on non-period-based reports (general ledger, cash receipts/disbursements, schedule E)
- Period argument on balance sheet beyond what's needed for disclosure
