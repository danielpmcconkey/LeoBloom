# Project 019 — Create and Confirm Transfers

## Goal

Add two operations on the `ops.transfer` table: initiate (create a transfer
record with status "initiated") and confirm (settle it by posting a journal
entry via the existing posting engine). This is the second cross-domain
orchestration in LeoBloom, following the obligation posting pattern from P018
but with its own validation rules -- both accounts must be active asset-type
accounts, and the journal entry debits the receiving account / credits the
sending account.

## Behavior

### Initiate

Creates a transfer record with status = `initiated`. No journal entry. Money
is conceptually in-flight.

Validation:
- `from_account_id` and `to_account_id` must both reference active ledger
  accounts of type "asset" (`account_type.name = 'asset'`)
- `from_account_id != to_account_id`
- `amount > 0`
- `expected_settlement` and `description` are optional

Returns the created `Transfer` record.

### Confirm

Settles an initiated transfer by posting a double-entry journal entry and
updating the transfer record.

Validation:
- Transfer must exist, be active, and have status = `initiated`
- A fiscal period must cover `confirmed_date`

Journal entry details:
- Debit `to_account_id` (money arriving increases the asset)
- Credit `from_account_id` (money leaving decreases the asset)
- `entry_date` = `confirmed_date`
- `source` = `Some "transfer"`
- `description` = `transfer.description` or auto-generate `"Transfer {id}"`
- `fiscal_period_id` = period covering `confirmed_date`
- Reference: `reference_type = "transfer"`, `reference_value = string transfer.id`

Sets `status = 'confirmed'`, `confirmed_date`, `journal_entry_id` on the
transfer. Returns the updated `Transfer` record.

## Design Decisions

### Non-atomic confirmation is acceptable

Same pattern as P018: `JournalEntryService.post` opens its own
connection + transaction. The confirm flow reads + validates in one
transaction, posts the JE (separate transaction), then updates the transfer
record (third transaction). If the update fails after JE succeeds, the JE
has a `transfer` reference for traceability, and the transfer stays
`initiated` so retry is safe.

### Account type validation is transfer-specific

Unlike obligations (which delegate account validation to the posting engine),
transfers validate account types at initiation time -- both accounts must be
asset accounts. This requires a DB query joining `ledger.account` to
`ledger.account_type`. A private helper in TransferService handles this.

### TransferRepository.updateConfirm does its own connection

The update needs to happen after `JournalEntryService.post` returns the new
JE id, so it opens its own connection + transaction (matching the pattern
where each write phase is independently atomic).

## Implementation

### Phase 1: Domain Types (Ops.fs)

Add at the bottom of `Ops.fs`:

- `TransferStatus` module with `toString` / `fromString` (mirrors
  `InstanceStatus`, `ObligationDirection`, etc.)
- `InitiateTransferCommand` record:
  `{ fromAccountId: int; toAccountId: int; amount: decimal; initiatedDate: DateOnly; expectedSettlement: DateOnly option; description: string option }`
- `ConfirmTransferCommand` record:
  `{ transferId: int; confirmedDate: DateOnly }`

**Verification:** Project compiles.

### Phase 2: TransferRepository (new file)

New file `Src/LeoBloom.Utilities/TransferRepository.fs`.

Functions:
- `insert (txn: NpgsqlTransaction) (cmd: InitiateTransferCommand) : Transfer`
  - INSERT into `ops.transfer` with status = 'initiated', RETURNING all columns
  - Maps DB row to `Transfer` domain type using `TransferStatus.fromString`
- `findById (txn: NpgsqlTransaction) (transferId: int) : Transfer option`
  - SELECT by id, map to domain type
- `updateConfirm (txn: NpgsqlTransaction) (transferId: int) (confirmedDate: DateOnly) (journalEntryId: int) : Transfer`
  - UPDATE status='confirmed', confirmed_date, journal_entry_id, modified_at = now()
  - RETURNING all columns

**Files:**
- Create: `Src/LeoBloom.Utilities/TransferRepository.fs`
- Modify: `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` -- add
  `<Compile Include="TransferRepository.fs" />` after `ObligationPostingService.fs`

**Verification:** Project compiles.

### Phase 3: TransferService (new file)

New file `Src/LeoBloom.Utilities/TransferService.fs`.

Private helper:
- `lookupAccountInfo (txn: NpgsqlTransaction) (accountId: int) : (bool * string) option`
  - SQL: `SELECT a.is_active, at.name FROM ledger.account a JOIN ledger.account_type at ON a.account_type_id = at.id WHERE a.id = @id`
  - Returns `Some (is_active, account_type_name)` or `None`

Public functions:

**`initiate (cmd: InitiateTransferCommand) : Result<Transfer, string list>`**
1. Pure validation: `amount > 0`, `fromAccountId != toAccountId`
2. Open connection + transaction
3. Look up both accounts via `lookupAccountInfo`
4. Validate: both exist, both active, both asset type
5. Call `TransferRepository.insert`
6. Commit, return `Ok transfer`

**`confirm (cmd: ConfirmTransferCommand) : Result<Transfer, string list>`**
1. Open connection + transaction (read phase)
2. `TransferRepository.findById` -- validate exists, active, status = Initiated
3. `FiscalPeriodRepository.findByDate` -- validate period covers confirmedDate
4. Commit read transaction
5. Build `PostJournalEntryCommand`:
   - `entryDate` = confirmedDate
   - `description` = transfer.description or `sprintf "Transfer %d" transfer.id`
   - `source` = `Some "transfer"`
   - `fiscalPeriodId` = resolved period id
   - Lines: debit toAccountId, credit fromAccountId, both for transfer.amount
   - Reference: `referenceType = "transfer"`, `referenceValue = string transfer.id`
6. Call `JournalEntryService.post` -- if Error, return Error
7. Open new connection + transaction (write phase)
8. Call `TransferRepository.updateConfirm` with journalEntryId from posted result
9. Commit, return `Ok updatedTransfer`

**Files:**
- Create: `Src/LeoBloom.Utilities/TransferService.fs`
- Modify: `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` -- add
  `<Compile Include="TransferService.fs" />` after `TransferRepository.fs`

**Verification:** Project compiles.

### Phase 4: Gherkin Spec

New file `Specs/Ops/Transfers.feature` with tag prefix `@FT-TRF`.

Scenarios covering all acceptance criteria (initiate happy path, initiate
validation errors, confirm happy path, confirm validation errors, journal
entry correctness).

**Verification:** Spec file exists, scenarios map 1:1 to acceptance criteria.

### Phase 5: Tests

New file `Src/LeoBloom.Tests/TransferTests.fs`.

All tests hit real DB. Each test:
- Uses `TestData.uniquePrefix()` for isolation
- Creates its own asset-type accounts via `InsertHelpers.insertAccountType`
  + `InsertHelpers.insertAccount`
- Creates fiscal periods via `InsertHelpers.insertFiscalPeriod` (for confirm tests)
- Cleans up via `TestCleanup.track` + `TestCleanup.deleteAll`
- Tags behavioral tests with `[<Trait("GherkinId", "@FT-TRF-NNN")>]`

Test helper addition: `InsertHelpers.insertTransfer` for tests that need a
pre-existing initiated transfer (confirm tests). Inserts directly into
`ops.transfer` with status='initiated'.

**Files:**
- Create: `Src/LeoBloom.Tests/TransferTests.fs`
- Modify: `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` -- add
  `<Compile Include="TransferTests.fs" />` after `OverdueDetectionTests.fs`
- Modify: `Src/LeoBloom.Tests/TestHelpers.fs` -- add `insertTransfer` helper

**Verification:** `dotnet test` passes. All acceptance criteria covered.

## Artifacts

- Domain types: additions to `Src/LeoBloom.Domain/Ops.fs`
- Repository: new file `Src/LeoBloom.Utilities/TransferRepository.fs`
- Service: new file `Src/LeoBloom.Utilities/TransferService.fs`
- Project file: `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` (compile order)
- Gherkin: `Specs/Ops/Transfers.feature` (tag prefix `@FT-TRF`)
- Tests: `Src/LeoBloom.Tests/TransferTests.fs`
- Test helpers: additions to `Src/LeoBloom.Tests/TestHelpers.fs`
- Test project file: `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj`

## Acceptance Criteria

- [ ] Initiating a transfer between two active asset accounts succeeds and creates a record with status 'initiated'
- [ ] Initiating with from_account = to_account returns error
- [ ] Initiating with non-asset account type returns error
- [ ] Initiating with inactive account returns error
- [ ] Initiating with amount <= 0 returns error
- [ ] Confirming an initiated transfer creates correct journal entry (debit to, credit from) and sets status 'confirmed'
- [ ] Confirming sets confirmed_date and journal_entry_id on the transfer
- [ ] Journal entry has source = "transfer" and reference type "transfer" with transfer id
- [ ] Journal entry entry_date = confirmed_date
- [ ] Journal entry uses transfer description when present
- [ ] Journal entry auto-generates description when transfer has no description
- [ ] Confirming a transfer not in 'initiated' status returns error
- [ ] Confirming when no fiscal period covers confirmed_date returns error
- [ ] FiscalPeriodRepository.findByDate already tested in P018 -- no need to re-test

## Risks

- **Non-atomic confirm flow**: If `TransferRepository.updateConfirm` fails
  after `JournalEntryService.post` succeeds, the JE exists without a
  confirmed transfer. Mitigation: the JE has a `transfer` reference for
  traceability, and the transfer stays `initiated` so retry is safe. Log a
  warning if this happens.
- **Fiscal period gaps**: If no period covers confirmed_date, confirm fails.
  Correct behavior -- create the period first.
- **Account type check at initiation only**: We validate asset type when
  initiating but not again when confirming. If someone changes account type
  between initiate and confirm (unlikely in practice), the JE posting engine
  will still validate the accounts exist and are active, but won't re-check
  account type. Acceptable for now.

## Out of Scope

- Cancelling / voiding a transfer
- Batch transfers
- Transfer between non-asset accounts (intentionally restricted)
- CLI integration (consumption layer concern)
- Refactoring JournalEntryService to accept external transactions
