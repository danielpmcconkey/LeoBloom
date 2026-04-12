# Project 083 — Closed Period Enforcement, Reversing Entries & Adjustments

## Objective

Close the void-on-closed-period gap, introduce reversing entries as the GAAP-correct
alternative, add post-close adjustment tagging, and provide a fiscal period override
on `ledger post`. All corrections happen in open periods with backward references —
no posting into closed periods.

**Traces to:** Spec `083-closed-period-enforcement.md`, Epic K §2–§4.

---

## Phases

### Phase 1: Schema Migration — `adjustment_for_period_id`

**What:** Add `adjustment_for_period_id` nullable FK column to `ledger.journal_entry`.

**Files:**
- **Create:** `Src/LeoBloom.Migrations/Migrations/1712000027000_AddAdjustmentForPeriodId.sql`

**Details:**
```sql
ALTER TABLE ledger.journal_entry
    ADD COLUMN adjustment_for_period_id integer NULL
        REFERENCES ledger.fiscal_period(id) ON DELETE RESTRICT;
```

Backfill: all existing rows get NULL (default). No data migration needed.

**Verification:** Migration runs cleanly. Column exists with FK constraint.

---

### Phase 2: Domain & Repository — Wire `adjustmentForPeriodId`

**What:** Thread the new column through the domain type, repository reads/writes,
and the `PostJournalEntryCommand` DTO.

**Files modified:**
- `Src/LeoBloom.Domain/Ledger.fs`
  - Add `adjustmentForPeriodId: int option` to `JournalEntry` record
  - Add `adjustmentForPeriodId: int option` to `PostJournalEntryCommand` record
- `Src/LeoBloom.Ledger/JournalEntryRepository.fs`
  - `readEntry`: read column index 9 (nullable int)
  - `insertEntry`: include `adjustment_for_period_id` in INSERT + RETURNING
  - `getEntryById`: update SELECT to include the new column

**Verification:** Existing tests still compile and pass. New field round-trips through
insert → read.

---

### Phase 3: Void Enforcement on Closed Periods

**What:** Block `voidEntry` when the JE's fiscal period is closed. Return an
actionable error suggesting `ledger reverse`.

**Files modified:**
- `Src/LeoBloom.Ledger/JournalEntryService.fs`
  - In `voidEntry`: after loading the entry (before the UPDATE), look up the
    fiscal period via `lookupFiscalPeriod`. If `not isOpen`, return error:
    ```
    Cannot void JE {id} — it belongs to closed period '{periodKey}' (closed {closedAt}).
    Post a reversing entry in the current open period instead:
    leobloom ledger reverse --journal-entry-id {id}
    ```
  - This requires the repository's `voidEntry` to first return the entry header
    (or we do a pre-check). **Approach:** Add a pre-flight fetch in the service
    layer before calling `JournalEntryRepository.voidEntry`. Load the entry via
    `getEntryById`, check voided status (reject if already voided — currently
    silent), check fiscal period status.

**Also modified:**
- `Src/LeoBloom.Ledger/FiscalPeriodRepository.fs` — may need a helper that returns
  `periodKey` and `closedAt` alongside `isOpen` (the existing `lookupFiscalPeriod`
  in the service only returns `id, startDate, endDate, isOpen`). **Approach:** Extend
  `JournalEntryService.FiscalPeriodCheck` to include `periodKey: string` and
  `closedAt: DateTimeOffset option`, update the SELECT in `lookupFiscalPeriod`.

**Verification:** Voiding JE in closed period → error with period name + close date +
reversal syntax. Voiding JE in open period → works as before.

---

### Phase 4: Reversing Entries — Service Layer

**What:** Implement `JournalEntryService.reverseEntry`.

**Files modified:**
- `Src/LeoBloom.Ledger/JournalEntryService.fs`
  - New function `reverseEntry (txn: NpgsqlTransaction) (entryId: int) (dateOverride: DateOnly option) : Result<PostedJournalEntry, string list>`
  - Logic:
    1. Load original JE + lines via `getEntryById`
    2. Validate original exists
    3. Validate original is not voided (`voidedAt = None`)
    4. Validate no existing reversal: call `JournalEntryRepository.findNonVoidedByReference txn "reversal" (string entryId)` — if Some, reject
    5. Determine entry date: `dateOverride |> Option.defaultValue (DateOnly.FromDateTime(DateTime.Today))`
    6. Derive fiscal period from date (need to look up period containing the date — **new repo query**)
    7. Build `PostJournalEntryCommand`:
       - `entryDate` = resolved date
       - `description` = `sprintf "Reversal of JE %d: %s" entryId original.entry.description`
       - `source` = `Some "reversal"`
       - `fiscalPeriodId` = derived period id
       - `lines` = original lines with swapped `entryType` (Debit↔Credit)
       - `references` = `[{ referenceType = "reversal"; referenceValue = string entryId }]`
    8. Delegate to existing `post txn cmd`
    9. Return the posted reversal JE

- `Src/LeoBloom.Ledger/FiscalPeriodRepository.fs`
  - **New function:** `findOpenPeriodForDate (txn: NpgsqlTransaction) (date: DateOnly) : FiscalPeriod option`
    — `SELECT ... FROM ledger.fiscal_period WHERE start_date <= @d AND end_date >= @d AND is_open = true LIMIT 1`
  - This is needed by `reverseEntry` to derive the fiscal period from the entry date.

**Verification:** Reversal creates a balanced JE with swapped lines, correct description,
source="reversal", and reference type="reversal". Rejections work for voided/already-reversed.

---

### Phase 5: CLI — `ledger reverse` Command

**What:** Add the `ledger reverse` subcommand to the CLI.

**Files modified:**
- `Src/LeoBloom.CLI/LedgerCommands.fs`
  - New Argu DU: `LedgerReverseArgs` with:
    - `[<Mandatory>] Journal_Entry_Id of int`
    - `Date of string` (optional override)
    - `Json`
  - Add `| [<CliPrefix(CliPrefix.None)>] Reverse of ParseResults<LedgerReverseArgs>` to `LedgerArgs`
  - New handler `handleReverse`: parse args, open txn, call `JournalEntryService.reverseEntry`,
    commit/rollback, write output
  - Add `Reverse` case to dispatch

**Verification:** `leobloom ledger reverse --journal-entry-id 5` works end-to-end.
`--date` override works. `--json` works.

---

### Phase 6: CLI — `ledger post` Enhancements

**What:** Add `--adjustment-for-period` and `--fiscal-period-id` override flags.

**Files modified:**
- `Src/LeoBloom.CLI/LedgerCommands.fs`
  - `LedgerPostArgs`: Change `Fiscal_Period_Id` from `[<Mandatory>]` to optional.
    Add `Adjustment_For_Period of int`.
  - `handlePost`:
    - If `--fiscal-period-id` provided: use it directly (existing validation handles open check)
    - If not provided: derive from date via `FiscalPeriodRepository.findOpenPeriodForDate`
    - If `--adjustment-for-period` provided: validate the target period exists (can be closed),
      set on command
    - Wire `adjustmentForPeriodId` through to `PostJournalEntryCommand`

- `Src/LeoBloom.Ledger/JournalEntryService.fs`
  - `validateDbDependencies`: add validation for `adjustmentForPeriodId` — if Some, check
    that the referenced period exists (via `lookupFiscalPeriod`). No open check — closed is fine.

**Design note on `--fiscal-period-id` becoming optional:**
Currently `Fiscal_Period_Id` is `[<Mandatory>]`. Making it optional means the CLI must
derive it from the entry date when not provided. This is a UX improvement but changes
existing behavior (callers who omit it will get auto-derivation instead of an Argu error).
This is the intended direction per the spec. The `findOpenPeriodForDate` query from Phase 4
handles this.

**Verification:** `--adjustment-for-period` tags the JE. Target can be closed. `--fiscal-period-id`
override works. Omitting `--fiscal-period-id` derives from date.

---

### Phase 7: JSON Output Updates

**What:** Ensure `adjustmentForPeriodId` appears in all JSON output for JEs.

**Files modified:**
- Wherever JE serialization happens — likely handled automatically by the F# record
  serializer since we added the field to the domain type. Verify in `OutputFormatter.fs`
  or wherever `System.Text.Json` serialization is configured.

**Verification:** `--json` output on `ledger post`, `ledger show`, and `ledger reverse`
includes `adjustmentForPeriodId` (null when not set, integer when set).

---

## Acceptance Criteria

### Void Enforcement

- [x] **[Behavioral]** Voiding a JE in a closed period is rejected with error naming the period and suggesting `ledger reverse`
- [x] **[Behavioral]** Voiding a JE in an open period continues to work as before
- [x] **[Behavioral]** Error message includes the period name, close date, and the reversal command syntax

### Reversing Entries

- [x] **[Behavioral]** `ledger reverse --journal-entry-id N` creates a new JE with swapped debits/credits
- [x] **[Behavioral]** Reversal description is auto-generated: `"Reversal of JE {id}: {original description}"`
- [x] **[Behavioral]** Reversal posts to the fiscal period derived from the entry date (standard path)
- [x] **[Behavioral]** Reversing an already-voided JE is rejected
- [x] **[Behavioral]** Reversing a JE that already has a reversal is rejected (idempotency guard)
- [x] **[Behavioral]** Reversal JE has reference `type=reversal, value={original_id}`
- [x] **[Behavioral]** `--date` override works and validates against open periods
- [x] **[Behavioral]** `--json` supported on `ledger reverse`

### Post-Close Adjustments

- [x] **[Structural]** `adjustment_for_period_id` column exists with FK constraint
- [x] **[Behavioral]** `ledger post --adjustment-for-period N` tags the JE correctly
- [x] **[Behavioral]** Adjustment target period can be closed
- [x] **[Behavioral]** Adjustment target period must exist
- [x] **[Behavioral]** JE itself posts to its own (open) fiscal period normally
- [x] **[Behavioral]** `--json` output includes `adjustmentForPeriodId`

### Fiscal Period Override

- [x] **[Behavioral]** `ledger post --fiscal-period-id N` overrides date-derived period
- [x] **[Behavioral]** Override target period must be open
- [x] **[Behavioral]** Omitting the flag preserves existing date-derivation behavior

---

## Risks

### GAAP Compliance

The reversing entry mechanism is standard GAAP treatment for correcting entries in closed
periods. The `adjustment_for_period_id` tag enables P084's report disclosure ("as originally
closed" vs "as adjusted"). No GAAP red flags here — this is the textbook approach.

**One nuance:** The spec says reversals are valid anytime (not just for closed-period JEs).
This is correct — reversals are a general-purpose correction tool. But the *primary* use
case is closed-period corrections, and the void-enforcement error message drives users
toward it.

### `Fiscal_Period_Id` Becoming Optional

Making `--fiscal-period-id` optional on `ledger post` is a behavioral change for existing
callers (Hobson, scripts). Previously Argu would reject the command without it. After this
change, omitting it triggers auto-derivation from the entry date.

**Mitigation:** This is explicitly specced behavior. Hobson already passes `--fiscal-period-id`
in all calls, so existing usage continues to work. The auto-derivation is additive.

### `findOpenPeriodForDate` Edge Cases

If no open period covers the given date, the reversal (or post without explicit period)
fails. The error should be clear: "No open fiscal period covers date {date}".

If multiple periods cover the date (shouldn't happen given overlap validation in P055/P081),
we take the first match. The overlap guard makes this a non-issue in practice.

### Migration Safety

The migration is purely additive (nullable column + FK). No backfill logic beyond NULL
default. Zero risk of data loss. Rollback is `ALTER TABLE ... DROP COLUMN`.

---

## Out of Scope

- Report disclosure of adjustments (P084)
- Automatic detection of "this should have been an adjustment" scenarios
- `--as-originally-closed` report mode (P084)
- Bulk reversal tooling
- Reversal of reversals (a reversal is just a normal JE — you can reverse it like any other)
