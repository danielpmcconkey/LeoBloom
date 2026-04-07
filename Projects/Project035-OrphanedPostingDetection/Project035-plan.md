# Project 035 — Orphaned Posting Detection — Plan

## Objective

Add a read-only CLI diagnostic that detects three categories of orphaned
journal-entry postings: dangling status updates, missing source records, and
posted/confirmed records backed by voided journal entries. This is the first
command in a new `diagnostic` command group. No mutations, no migrations.

## Decision Log

**Two queries, not one.** The spec describes two distinct lookup directions.
A single query with unions and conditional joins would be harder to read and
harder to test. Two queries is cleaner:

- **Query A (reference → source):** Starts from `journal_entry_reference`
  where `reference_type IN ('obligation', 'transfer')`. LEFT JOINs to the
  source table. Detects dangling status (source exists but `journal_entry_id`
  IS NULL) and missing source (source row doesn't exist at all).

- **Query B (source → journal entry):** Starts from `obligation_instance`
  (status = 'posted') and `transfer` (status = 'confirmed') where
  `journal_entry_id IS NOT NULL`. JOINs to `journal_entry`. Detects
  `voided_at IS NOT NULL`.

**varchar casting:** PostgreSQL's `~ '^\d+$'` regex filter before
`CAST(reference_value AS integer)`. Rows with non-numeric reference_value
for obligation/transfer types get reported as a fourth anomaly category:
`InvalidReference`. This avoids silent data loss and costs nothing.

**Domain type placement:** New `OrphanedPosting` type goes in `Ops.fs`
since it spans ops and ledger concepts but lives in the ops diagnostic
workflow. Lightweight — just a record and a DU for the condition.

**Exit codes:** Match spec exactly. 0 = ran (even with orphans found),
1 = validation error (not applicable here since the command takes no
required args, but wired for consistency), 2 = system error (DB failure).

## Phases

### Phase 1: Domain Types

**What:** Add the diagnostic result types to `Ops.fs`.

**Files:**
- `Src/LeoBloom.Domain/Ops.fs` — append after the balance projection types

**New types:**
```fsharp
type OrphanCondition =
    | DanglingStatus      // JE reference exists, source journal_entry_id is NULL
    | MissingSource       // JE reference points to nonexistent source record
    | VoidedBackingEntry  // Source posted/confirmed but backing JE is voided
    | InvalidReference    // reference_value is non-numeric for obligation/transfer type

type OrphanedPosting =
    { sourceType: string        // "obligation" or "transfer"
      sourceRecordId: int option // None when missing source or invalid ref
      journalEntryId: int
      condition: OrphanCondition
      referenceValue: string }   // raw value from journal_entry_reference

type OrphanedPostingResult =
    { orphans: OrphanedPosting list }
```

**Verification:** Project compiles.

### Phase 2: Repository

**What:** Create `OrphanedPostingRepository.fs` with the two read-only
queries.

**Files:**
- `Src/LeoBloom.Ops/OrphanedPostingRepository.fs` — new file
- `Src/LeoBloom.Ops/LeoBloom.Ops.fsproj` — add compile include (before services, after existing repos)

**Query A — reference-to-source orphans:**
```sql
-- Step 1: Find obligation/transfer references with non-numeric values
SELECT jer.journal_entry_id, jer.reference_type, jer.reference_value
FROM ledger.journal_entry_reference jer
WHERE jer.reference_type IN ('obligation', 'transfer')
  AND jer.reference_value !~ '^\d+$'

-- Step 2: Numeric references — left join to source tables
SELECT jer.journal_entry_id,
       jer.reference_type,
       jer.reference_value,
       oi.id AS obligation_id,
       oi.journal_entry_id AS obligation_je_id,
       t.id AS transfer_id,
       t.journal_entry_id AS transfer_je_id
FROM ledger.journal_entry_reference jer
LEFT JOIN ops.obligation_instance oi
  ON jer.reference_type = 'obligation'
 AND oi.id = CAST(jer.reference_value AS integer)
LEFT JOIN ops.transfer t
  ON jer.reference_type = 'transfer'
 AND t.id = CAST(jer.reference_value AS integer)
WHERE jer.reference_type IN ('obligation', 'transfer')
  AND jer.reference_value ~ '^\d+$'
  AND (
    -- Missing source: neither table matched
    (jer.reference_type = 'obligation' AND oi.id IS NULL)
    OR (jer.reference_type = 'transfer' AND t.id IS NULL)
    -- Dangling status: source exists but journal_entry_id is NULL
    OR (jer.reference_type = 'obligation' AND oi.id IS NOT NULL AND oi.journal_entry_id IS NULL)
    OR (jer.reference_type = 'transfer' AND t.id IS NOT NULL AND t.journal_entry_id IS NULL)
  )
```

**Query B — voided backing entry:**
```sql
-- Obligations in 'posted' status with voided JE
SELECT oi.id AS source_id, oi.journal_entry_id, 'obligation' AS source_type
FROM ops.obligation_instance oi
JOIN ledger.journal_entry je ON je.id = oi.journal_entry_id
WHERE oi.status = 'posted'
  AND oi.journal_entry_id IS NOT NULL
  AND je.voided_at IS NOT NULL

UNION ALL

-- Transfers in 'confirmed' status with voided JE
SELECT t.id AS source_id, t.journal_entry_id, 'transfer' AS source_type
FROM ops.transfer t
JOIN ledger.journal_entry je ON je.id = t.journal_entry_id
WHERE t.status = 'confirmed'
  AND t.journal_entry_id IS NOT NULL
  AND je.voided_at IS NOT NULL
```

**Pattern:** Follows existing repo pattern — accepts `NpgsqlTransaction`,
returns typed list. Single public function `findOrphanedPostings` that runs
both queries and merges results.

**Verification:** Project compiles. (Tested via Phase 5.)

### Phase 3: Service

**What:** Create `OrphanedPostingService.fs` — thin wrapper that opens a
connection/transaction, calls the repository, and returns
`Result<OrphanedPostingResult, string list>`.

**Files:**
- `Src/LeoBloom.Ops/OrphanedPostingService.fs` — new file
- `Src/LeoBloom.Ops/LeoBloom.Ops.fsproj` — add compile include (after repo)

**Pattern:** Same as other services — `use conn`, `use txn`, try/with for
`Result.Error` on DB exceptions.

**Verification:** Project compiles.

### Phase 4: CLI Integration

**What:** Wire the diagnostic into the CLI.

**Files:**
- `Src/LeoBloom.CLI/DiagnosticCommands.fs` — new file
- `Src/LeoBloom.CLI/OutputFormatter.fs` — add formatting functions
- `Src/LeoBloom.CLI/Program.fs` — add `Diagnostic` case to DU and dispatch
- `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` — add `DiagnosticCommands.fs`

**DiagnosticCommands.fs structure:**
```fsharp
type OrphanedPostingsArgs =
    | Json
    interface IArgParserTemplate ...

type DiagnosticArgs =
    | [<CliPrefix(CliPrefix.None)>] Orphaned_Postings of ParseResults<OrphanedPostingsArgs>
    interface IArgParserTemplate ...

let dispatch (isJson: bool) (args: ParseResults<DiagnosticArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (Orphaned_Postings subArgs) -> handleOrphanedPostings (isJson || subArgs.Contains OrphanedPostingsArgs.Json)
    | None -> Console.Error.WriteLine(args.Parser.PrintUsage()); ExitCodes.systemError
```

Note: `--json` can come from either the global flag or the subcommand flag.
Follow the same pattern as other commands.

**OutputFormatter additions:**
- `formatOrphanedPostings` for human-readable table output
- `writeOrphanedPostings` for the isJson dispatch (JSON via System.Text.Json,
  human via the formatter)

**Human output format:**
```
No orphaned postings found.
```
or:
```
3 orphaned postings found.

  Source Type   Source ID   JE ID   Condition            Reference
  ----------   ---------   -----   -----------------    ---------
  obligation   42          17      Dangling status      42
  transfer     —           23      Missing source       999
  obligation   88          31      Voided backing JE    88
```

Source ID shows `—` when the source record doesn't exist (MissingSource) or
the reference is invalid (InvalidReference).

**Verification:** `dotnet build` succeeds. Manual `leobloom diagnostic
orphaned-postings` returns "No orphaned postings found" against a clean DB.

### Phase 5: Tests

**What:** BDD tests covering AC-B1 through AC-B9.

**Files:**
- `Specs/Ops/OrphanedPostingDetection.feature` — Gherkin scenarios
- `Src/LeoBloom.Tests/OrphanedPostingDetectionTests.fs` — test implementation
- `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` — add compile include

**Test approach:** Each scenario directly sets up the specific orphan
condition via SQL INSERTs (using the test helper pattern from existing tests),
then calls the service and asserts on the result. The tests do NOT shell out
to the CLI binary — they test the service layer, which is the same pattern
used by TransferTests and PostObligationToLedgerTests.

**Scenario mapping:**
| AC   | Scenario |
|------|----------|
| B1   | Clean DB → empty result |
| B2   | Insert JE + reference(obligation) + obligation_instance with NULL journal_entry_id → DanglingStatus |
| B3   | Same as B2 but for transfer → DanglingStatus |
| B4   | Insert JE + reference(obligation) pointing to nonexistent ID → MissingSource |
| B5   | Same as B4 but for transfer → MissingSource |
| B6   | Insert obligation_instance(posted) with journal_entry_id → void that JE → VoidedBackingEntry |
| B7   | Same as B6 but for transfer(confirmed) → VoidedBackingEntry |
| B8   | Insert properly completed posting → not in results |
| B9   | Call with isJson=true, verify output is valid JSON |

**Verification:** `dotnet test` passes all new and existing tests.

## Acceptance Criteria

- [ ] AC-B1: No orphans returns empty result and exit code 0
- [ ] AC-B2: Detects dangling obligation status
- [ ] AC-B3: Detects dangling transfer status
- [ ] AC-B4: Detects missing obligation source
- [ ] AC-B5: Detects missing transfer source
- [ ] AC-B6: Detects posted obligation with voided JE
- [ ] AC-B7: Detects confirmed transfer with voided JE
- [ ] AC-B8: Normal postings are not flagged
- [ ] AC-B9: JSON output mode produces valid JSON
- [ ] AC-S1: `Diagnostic` case in `LeoBloomArgs` DU, routing to `DiagnosticCommands.fs`
- [ ] AC-S2: All queries are SELECT-only
- [ ] AC-S3: No new migrations
- [ ] AC-S4: All pre-existing tests pass

## Risks

| Risk | Mitigation |
|------|------------|
| Non-numeric `reference_value` blows up CAST | Regex filter `~ '^\d+$'` before casting; non-numeric rows reported as `InvalidReference` |
| Existing tests fragile to new DU cases in Program.fs | New DU case is additive; Argu pattern match uses `TryGetSubCommand` which is forward-compatible |
| Test cleanup leaking orphan test data | Use existing `TestCleanup.Tracker` pattern — all INSERTs tracked and rolled back |

## Out of Scope

- Automatic remediation of detected orphans
- Scheduled/cron execution
- New database indexes or migrations
- Alerting or notifications
- The `InvalidReference` condition is a bonus diagnostic beyond the spec's three conditions — it falls out naturally from the varchar casting safety and costs zero additional complexity
