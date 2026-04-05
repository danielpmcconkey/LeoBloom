# Project 043 -- Plan: Idempotency Guards for Posting Services

## Objective

Add read-before-write idempotency guards to `ObligationPostingService.postToLedger`
and `TransferService.confirm`. Before posting a journal entry (phase 2), each service
queries for an existing non-voided journal entry matching the expected reference. If
found, it skips the post and uses the existing entry's ID for the status transition
(phase 3). This eliminates the duplicate-journal-entry risk when phase 2 succeeds but
phase 3 fails and the caller retries.

Source: Code audit SYNTHESIS.md Tier 1 Finding #1 (5/6 reviewers). PO brief:
`Project043-po-brief.md`. This is option (b) from the audit -- idempotency guard,
not single-transaction refactor.

---

## Phase 1: Repository Layer -- Idempotency Lookup Query

### What

Add a new function `findNonVoidedByReference` to `JournalEntryRepository.fs` that
looks up an existing non-voided journal entry by reference_type + reference_value.

### File

`Src/LeoBloom.Utilities/JournalEntryRepository.fs` -- add after `insertReferences`
(after line 130).

### Function Signature

```fsharp
let findNonVoidedByReference
    (txn: NpgsqlTransaction)
    (referenceType: string)
    (referenceValue: string)
    : int option =
```

### SQL

```sql
SELECT je.id
FROM ledger.journal_entry_reference jer
JOIN ledger.journal_entry je ON je.id = jer.journal_entry_id
WHERE jer.reference_type = @rt
  AND jer.reference_value = @rv
  AND je.voided_at IS NULL
LIMIT 1
```

### Implementation

```fsharp
let findNonVoidedByReference
    (txn: NpgsqlTransaction)
    (referenceType: string)
    (referenceValue: string)
    : int option =
    use sql = new NpgsqlCommand(
        "SELECT je.id \
         FROM ledger.journal_entry_reference jer \
         JOIN ledger.journal_entry je ON je.id = jer.journal_entry_id \
         WHERE jer.reference_type = @rt \
           AND jer.reference_value = @rv \
           AND je.voided_at IS NULL \
         LIMIT 1",
        txn.Connection, txn)
    sql.Parameters.AddWithValue("@rt", referenceType) |> ignore
    sql.Parameters.AddWithValue("@rv", referenceValue) |> ignore
    use reader = sql.ExecuteReader()
    let result =
        if reader.Read() then Some (reader.GetInt32(0))
        else None
    reader.Close()
    result
```

### Why `int option`

The guard only needs the journal entry ID to pass to phase 3. Returning the full
`JournalEntry` record would waste a larger SELECT for data nobody uses. `Some id`
means "existing entry found, skip phase 2." `None` means "no prior entry, proceed
normally."

### Verification

- Function compiles and appears in the module.
- SQL joins through `journal_entry` to filter on `voided_at IS NULL`.
- Returns `int option`.

---

## Phase 2: ObligationPostingService Guard

### File

`Src/LeoBloom.Utilities/ObligationPostingService.fs`

### Insertion Point

Between line 76 (the `| Ok (instance, agreement, fiscalPeriod) ->` branch) and
line 102 (the `match JournalEntryService.post jeCmd with` call). Specifically,
the guard goes **after** the `PostJournalEntryCommand` is built (after line 99)
and **before** the `match JournalEntryService.post jeCmd with` on line 102.

### Logic

After building `jeCmd`, before calling `JournalEntryService.post`:

```fsharp
            // Idempotency guard: check for existing non-voided journal entry
            let existingJeId =
                use guardConn = DataSource.openConnection()
                use guardTxn = guardConn.BeginTransaction()
                try
                    let result =
                        JournalEntryRepository.findNonVoidedByReference
                            guardTxn "obligation" (string instance.id)
                    guardTxn.Commit()
                    result
                with ex ->
                    try guardTxn.Rollback() with _ -> ()
                    raise ex

            match existingJeId with
            | Some jeId ->
                Log.info
                    "Idempotency guard: found existing journal entry {JournalEntryId} for instance {InstanceId}, skipping post"
                    [| jeId :> obj; instance.id :> obj |]

                // Phase 3: Transition instance to posted (using existing JE)
                let transitionCmd : TransitionCommand =
                    { instanceId = instance.id
                      targetStatus = InstanceStatus.Posted
                      journalEntryId = Some jeId
                      amount = None
                      confirmedDate = None
                      notes = None }

                match ObligationInstanceService.transition transitionCmd with
                | Error errs ->
                    Log.warn
                        "Transition to posted failed for instance {InstanceId} using existing journal entry {JournalEntryId}"
                        [| instance.id :> obj; jeId :> obj |]
                    Error errs
                | Ok updated ->
                    Log.info "Transitioned instance {InstanceId} to posted (idempotent)"
                        [| updated.id :> obj |]
                    Ok { journalEntryId = jeId; instanceId = instance.id }

            | None ->
                // No existing entry -- proceed with normal phase 2 + phase 3
                // (existing code from line 102 onward, unchanged)
```

### What Changes in the Existing Code

The existing code from line 82 through line 127 gets restructured:

1. Lines 82-99 (build `jeCmd`) -- **unchanged**.
2. Lines 101-127 (phase 2 + phase 3) -- **indented one level** inside `| None ->`.
3. The new idempotency check + `| Some jeId ->` branch is inserted before `| None ->`.

### Return Type

Unchanged. Both the guard path and the normal path return
`Ok { journalEntryId = <id>; instanceId = instance.id }` -- satisfying AC-B6.

### Connection Lifecycle

The guard opens its own connection (`guardConn`), consistent with the existing
per-phase pattern. It does not reuse the phase 1 connection (already closed at
line 67).

### Verification

- Guard fires between phase 1 result and phase 2 call.
- When guard finds existing entry: skips `JournalEntryService.post`, proceeds
  directly to `ObligationInstanceService.transition` with the found JE ID.
- When guard finds nothing: existing behavior unchanged.
- Return shape is identical in both paths.

---

## Phase 3: TransferService Guard

### File

`Src/LeoBloom.Utilities/TransferService.fs`

### Insertion Point

Between line 141 (end of `jeCmd` construction, the `references` field ending with
`referenceValue = string transfer.id } ]`) and line 143 (the
`match JournalEntryService.post jeCmd with` call).

### Logic

Same pattern as ObligationPostingService:

```fsharp
            // Idempotency guard: check for existing non-voided journal entry
            let existingJeId =
                use guardConn = DataSource.openConnection()
                use guardTxn = guardConn.BeginTransaction()
                try
                    let result =
                        JournalEntryRepository.findNonVoidedByReference
                            guardTxn "transfer" (string transfer.id)
                    guardTxn.Commit()
                    result
                with ex ->
                    try guardTxn.Rollback() with _ -> ()
                    raise ex

            match existingJeId with
            | Some jeId ->
                Log.info
                    "Idempotency guard: found existing journal entry {JournalEntryId} for transfer {TransferId}, skipping post"
                    [| jeId :> obj; transfer.id :> obj |]

                // Phase 3: Update transfer record (using existing JE)
                try
                    use conn = DataSource.openConnection()
                    use txn = conn.BeginTransaction()
                    let updated = TransferRepository.updateConfirm txn transfer.id cmd.confirmedDate jeId
                    txn.Commit()
                    Log.info "Transfer {TransferId} confirmed successfully (idempotent)"
                        [| updated.id :> obj |]
                    Ok updated
                with ex ->
                    Log.errorExn ex
                        "Failed to update transfer {TransferId} after finding existing journal entry {JournalEntryId}"
                        [| transfer.id :> obj; jeId :> obj |]
                    Error [ sprintf "Failed to update transfer after journal entry was posted: %s" ex.Message ]

            | None ->
                // No existing entry -- proceed with normal phase 2 + phase 3
                // (existing code from line 143 onward, unchanged)
```

### What Changes in the Existing Code

1. Lines 120-141 (build `jeCmd`) -- **unchanged**.
2. Lines 143-165 (phase 2 + phase 3) -- **indented one level** inside `| None ->`.
3. The new idempotency check + `| Some jeId ->` branch is inserted before `| None ->`.

### Return Type

Unchanged. Both paths return `Ok <Transfer>` -- satisfying AC-B6. The guard path
calls `TransferRepository.updateConfirm` with the existing JE ID, which returns a
`Transfer` record just like the normal path.

### Verification

- Guard fires between phase 1 result and phase 2 call.
- When guard finds existing entry: skips `JournalEntryService.post`, proceeds
  directly to `TransferRepository.updateConfirm` with the found JE ID.
- When guard finds nothing: existing behavior unchanged.
- Return shape is identical in both paths.

---

## Phase 4: Gherkin Specifications

### Feature File Locations

**Obligation guard scenarios:** Append to existing file
`Specs/Behavioral/PostObligationToLedger.feature`, after the last scenario
(@FT-POL-017). Tags continue the @FT-POL sequence starting at @FT-POL-018.

**Transfer guard scenarios:** Append to existing file
`Specs/Behavioral/Transfers.feature`, after the last scenario (@FT-TRF-014).
Tags continue the @FT-TRF sequence starting at @FT-TRF-015.

### Scenario Mapping to Acceptance Criteria

#### PostObligationToLedger.feature additions

```gherkin
    # --- Idempotency Guard (P043) ---

    @FT-POL-018
    Scenario: Retry after partial failure skips duplicate journal entry (AC-B2)
        Given a receivable obligation agreement with source and destination accounts
        And an open fiscal period covering 2026-04-15
        And a confirmed instance with amount 1000.00 and confirmedDate 2026-04-15
        And a journal entry already exists with reference type "obligation" and value matching the instance ID
        When I post the instance to the ledger
        Then the post succeeds
        And no new journal entry was created
        And the instance status is "posted"
        And the instance journal_entry_id matches the pre-existing journal entry ID

    @FT-POL-019
    Scenario: Voided prior journal entry does not trigger the idempotency guard (AC-B5, obligation)
        Given a receivable obligation agreement with source and destination accounts
        And an open fiscal period covering 2026-04-15
        And a confirmed instance with amount 1000.00 and confirmedDate 2026-04-15
        And a voided journal entry exists with reference type "obligation" and value matching the instance ID
        When I post the instance to the ledger
        Then the post succeeds
        And a new journal entry was created (not the voided one)
        And the instance status is "posted"
```

AC-B1 (first-time obligation posting works unchanged) is already covered by
@FT-POL-001 through @FT-POL-006.

AC-B6 (return value identical) is verified structurally within @FT-POL-018 --
the test asserts `Ok` with journalEntryId and instanceId, same shape as normal.

#### Transfers.feature additions

```gherkin
    # --- Idempotency Guard (P043) ---

    @FT-TRF-015
    Scenario: Retry after partial failure skips duplicate journal entry (AC-B4)
        Given two active asset accounts "checking" and "savings"
        And an open fiscal period covering 2026-04-15
        And an initiated transfer of 1000.00 from "checking" to "savings"
        And a journal entry already exists with reference type "transfer" and value matching the transfer ID
        When I confirm the transfer on 2026-04-15
        Then the confirm succeeds
        And no new journal entry was created
        And the transfer status is "confirmed"
        And the transfer journal_entry_id matches the pre-existing journal entry ID

    @FT-TRF-016
    Scenario: Voided prior journal entry does not trigger the idempotency guard (AC-B5, transfer)
        Given two active asset accounts "checking" and "savings"
        And an open fiscal period covering 2026-04-15
        And an initiated transfer of 1000.00 from "checking" to "savings"
        And a voided journal entry exists with reference type "transfer" and value matching the transfer ID
        When I confirm the transfer on 2026-04-15
        Then the confirm succeeds
        And a new journal entry was created (not the voided one)
        And the transfer status is "confirmed"
```

AC-B3 (first-time transfer confirm works unchanged) is already covered by
@FT-TRF-007 through @FT-TRF-012.

### AC Coverage Summary

| AC ID | Covered By |
|-------|-----------|
| AC-B1 | @FT-POL-001 through @FT-POL-006 (existing, unchanged) |
| AC-B2 | @FT-POL-018 (new) |
| AC-B3 | @FT-TRF-007 through @FT-TRF-012 (existing, unchanged) |
| AC-B4 | @FT-TRF-015 (new) |
| AC-B5 | @FT-POL-019 + @FT-TRF-016 (new, one per service) |
| AC-B6 | Verified structurally within @FT-POL-018 and @FT-TRF-015 |

---

## Phase 5: Test Implementation

### Test File Locations

- **Obligation guard tests:** Add to existing `Src/LeoBloom.Tests/PostObligationToLedgerTests.fs`,
  after the last test (after the @FT-POL-017 test, around line 600).
- **Transfer guard tests:** Add to existing `Src/LeoBloom.Tests/TransferTests.fs`,
  after the last test.

### Test Setup Pattern: Simulating Partial Failure State

The tests simulate the state left behind by a partial failure (phase 2 succeeded,
phase 3 failed) by directly inserting a journal entry + reference before calling the
service. This is NOT a real retry -- it is constructing the database state that a
partial failure would leave.

#### Obligation retry test setup (@FT-POL-018)

```fsharp
// 1. Create prerequisite records (agreement, accounts, fiscal period)
//    Same as existing happy-path tests (setupReceivableAccounts, insertAgreementWithAccounts, etc.)

// 2. Create a confirmed instance
let instanceId = createConfirmedInstance conn tracker agreementId instanceName amount confirmedDate

// 3. Directly insert a journal entry + reference to simulate partial failure
let preExistingJeId =
    InsertHelpers.insertJournalEntry conn tracker confirmedDate "simulated partial failure" fiscalPeriodId
// Insert journal entry lines (required for FK / balance, 2 lines to be balanced)
use lineCmd = new NpgsqlCommand(
    "INSERT INTO ledger.journal_entry_line (journal_entry_id, account_id, amount, entry_type) \
     VALUES (@je, @acct, @amt, @et)", conn)
// ... insert debit + credit lines matching the obligation amounts ...
// Insert the reference that the guard will find
use refCmd = new NpgsqlCommand(
    "INSERT INTO ledger.journal_entry_reference (journal_entry_id, reference_type, reference_value) \
     VALUES (@je, 'obligation', @rv)", conn)
refCmd.Parameters.AddWithValue("@je", preExistingJeId) |> ignore
refCmd.Parameters.AddWithValue("@rv", string instanceId) |> ignore
refCmd.ExecuteNonQuery() |> ignore

// 4. Call the service
let result = ObligationPostingService.postToLedger { instanceId = instanceId }

// 5. Assertions
//    - result is Ok
//    - result.journalEntryId = preExistingJeId (used existing, not new)
//    - Instance status is now "posted" (query DB to verify)
//    - No second journal entry: count references with type="obligation" and value=instanceId,
//      should be exactly 1
```

#### Voided entry test setup (@FT-POL-019)

Same as above, but after inserting the journal entry, void it:

```fsharp
// After inserting the journal entry:
use voidCmd = new NpgsqlCommand(
    "UPDATE ledger.journal_entry SET voided_at = now(), void_reason = 'test void' \
     WHERE id = @id", conn)
voidCmd.Parameters.AddWithValue("@id", preExistingJeId) |> ignore
voidCmd.ExecuteNonQuery() |> ignore

// Then call the service -- it should create a NEW journal entry, not skip
```

#### Transfer retry test setup (@FT-TRF-015)

Same pattern, using `reference_type = 'transfer'` and `reference_value = string transferId`.
The transfer must be in `initiated` status (the service validates this in phase 1).

#### Transfer voided entry test (@FT-TRF-016)

Same pattern as @FT-POL-019 but for transfers.

### Key Assertions for Each Test

**Retry tests (@FT-POL-018, @FT-TRF-015):**
1. Service returns `Ok`.
2. The journal entry ID in the result matches `preExistingJeId`.
3. Only 1 journal entry reference exists for this reference_type/reference_value
   (proving no duplicate was created).
4. The source record (instance/transfer) has been transitioned to posted/confirmed.

**Voided entry tests (@FT-POL-019, @FT-TRF-016):**
1. Service returns `Ok`.
2. The journal entry ID in the result does NOT equal `preExistingJeId`.
3. A new, non-voided journal entry exists.
4. The source record has been transitioned.

### Counting Journal Entries by Reference (verification query)

```sql
SELECT COUNT(*)
FROM ledger.journal_entry_reference jer
JOIN ledger.journal_entry je ON je.id = jer.journal_entry_id
WHERE jer.reference_type = @rt
  AND jer.reference_value = @rv
  AND je.voided_at IS NULL
```

Use this in assertions to verify "no new journal entry was created" (count = 1
means the pre-existing one is the only one) and "new journal entry was created"
(count = 1 for a non-voided entry different from the voided one).

### Cleanup Notes

- Track ALL inserted journal entry IDs (both pre-existing simulated ones and any
  created by the service) via `TestCleanup.trackJournalEntry`.
- The service-created journal entries are returned in the `Ok` result. Track those too.
- Track accounts, account types, fiscal periods, agreements as usual.

---

## Phase 6: Build Verification

After all changes:

1. `dotnet build` from solution root -- must compile cleanly.
2. `dotnet test` -- all 424 existing tests pass. The guard is invisible to normal
   (non-retry) code paths because `findNonVoidedByReference` returns `None` when
   no prior entry exists, and the `| None ->` branch executes the original code.
3. The 4 new tests pass.

---

## Acceptance Criteria Checklist

- [ ] **AC-B1:** First-time obligation posting works unchanged (verified by existing @FT-POL-001 through @FT-POL-006 passing without modification)
- [ ] **AC-B2:** Retry after partial failure skips duplicate (obligation) -- @FT-POL-018
- [ ] **AC-B3:** First-time transfer confirm works unchanged (verified by existing @FT-TRF-007 through @FT-TRF-012 passing without modification)
- [ ] **AC-B4:** Retry after partial failure skips duplicate (transfer) -- @FT-TRF-015
- [ ] **AC-B5:** Voided prior entry does NOT trigger the guard -- @FT-POL-019 + @FT-TRF-016
- [ ] **AC-B6:** Return value is identical whether guard fires or not -- verified structurally in @FT-POL-018 and @FT-TRF-015
- [ ] **AC-S1:** New repository query exists in `JournalEntryRepository`
- [ ] **AC-S2:** Guard is present in both services
- [ ] **AC-S3:** No new database migration required (uses existing schema)
- [ ] **AC-S4:** All pre-existing BDD tests pass without modification

---

## Risks

| Risk | Mitigation |
|------|-----------|
| Guard query returns voided entry as match (wrong join) | SQL explicitly joins through `journal_entry` and filters `voided_at IS NULL`. Tested by @FT-POL-019 and @FT-TRF-016. |
| Guard connection left open on error | Guard follows existing pattern: `use` binding ensures disposal. Explicit try/rollback/raise on exception. |
| Phase 3 failure after guard fires (transition rejects already-posted instance) | This is the retry scenario itself. The transition service must accept the call. For obligations, the instance is still in `confirmed` status (phase 3 failed previously), so the transition to `posted` is valid. For transfers, the transfer is still in `initiated` status, so `updateConfirm` succeeds. |
| TOCTOU race between guard check and JournalEntryService.post | Acknowledged and out of scope per PO brief. Single CLI caller makes this a theoretical, not practical, risk. |

---

## Out of Scope

- Single-transaction refactor (audit option (a)) -- larger structural change, separate project.
- TOCTOU fix (SELECT ... FOR UPDATE) -- acceptable at current scale.
- Automatic retry framework -- guard makes retries safe, retry automation is separate.
- Unique constraint on `journal_entry_reference` -- would break legitimate multi-reference use cases.
- Changes to `JournalEntryService.post` -- guard lives in calling services.
- Logging beyond guard info message -- PO brief says info-level "found existing entry" is all.

---

## Execution Order

1. **Builder:** Phase 1 (repository function), then Phase 2 (ObligationPostingService), then Phase 3 (TransferService). Compile after each.
2. **Gherkin Writer:** Phase 4 (append scenarios to existing feature files).
3. **QE:** Phase 5 (implement tests in existing test files), then Phase 6 (full build + test run).
4. **Governor:** Verify all ACs, structural review.

---

## PO Gate 1 Review

**Verdict: APPROVED**

Every acceptance criterion (AC-B1 through AC-B6, AC-S1 through AC-S4) has a clear implementation path and test coverage. Scope boundaries from the brief are respected exactly -- no transaction refactor, no TOCTOU fix, no retry framework, no unique constraints, no changes to JournalEntryService.post. All six risk notes are addressed. The plan is tight and surgical.

**One correction for the Builder/Gherkin Writer:** Phase 4 says to append obligation scenarios "after the last scenario (@FT-POL-017)," but @FT-POL-017 is not the last scenario in `PostObligationToLedger.feature`. The @FT-POL-014 and @FT-POL-015 scenarios (FiscalPeriodRepository.findByDate tests) appear after it at lines 178-188. Append the new P043 scenarios after @FT-POL-015, which is the actual end of the file.

**Reviewed:** 2026-04-05 | **PO Agent**
