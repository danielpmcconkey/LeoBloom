# Project 014 -- Obligation Agreements (CRUD) -- Plan

## Objective

Deliver repository and service layers for `ops.obligation_agreement`, the
master record defining recurring financial obligations. This is the first
ops-track project; the table and domain type already exist. We need command
types, pure validation, a SQL repository, and an orchestrating service --
following the exact patterns established by the ledger track (projects 005-013).

## Architecture Decisions

- **Command types and pure validation live in `Ops.fs`** (domain layer), matching
  how `Ledger.fs` houses `PostJournalEntryCommand` and `validateCommand`.
- **Repository is transaction-scoped** -- all functions take `NpgsqlTransaction`,
  matching `JournalEntryRepository` and `FiscalPeriodRepository`.
- **Service owns the connection lifecycle** -- opens connection, begins transaction,
  calls repo, commits/rolls back. Returns `Result<T, string list>`.
- **Deactivation guard queries `ops.obligation_instance`** for active rows. The
  table already exists (migration 1712000016000) with `obligation_agreement_id FK`
  and `is_active` column.

## Phases

### Phase 1: Domain -- Command Types and Pure Validation

**What:** Add command records and a validation module to `Ops.fs`.

**Files modified:**
- `Src/LeoBloom.Domain/Ops.fs`

**Details:**

New types (appended after the existing record types):

```fsharp
type CreateObligationAgreementCommand =
    { name: string
      obligationType: ObligationDirection
      counterparty: string option
      amount: decimal option
      cadence: RecurrenceCadence
      expectedDay: int option
      paymentMethod: PaymentMethodType option
      sourceAccountId: int option
      destAccountId: int option
      notes: string option }

type UpdateObligationAgreementCommand =
    { id: int
      name: string
      obligationType: ObligationDirection
      counterparty: string option
      amount: decimal option
      cadence: RecurrenceCadence
      expectedDay: int option
      paymentMethod: PaymentMethodType option
      sourceAccountId: int option
      destAccountId: int option
      isActive: bool
      notes: string option }
```

New validation module `ObligationAgreementValidation` with:
- `validateName` -- non-empty, max 100 chars
- `validateCounterparty` -- max 100 chars if Some
- `validateAmount` -- must be > 0m if Some
- `validateExpectedDay` -- 1..31 if Some
- `validateCreateCommand` -- collects all errors, returns `Result<unit, string list>`
- `validateUpdateCommand` -- same + `id > 0`

Pattern: match `Ledger.validateCommand` -- collect errors from sub-validators,
return combined list.

**Verification:** `dotnet build Src/LeoBloom.Domain/LeoBloom.Domain.fsproj` succeeds.

### Phase 2: Repository -- SQL CRUD

**What:** Create `ObligationAgreementRepository.fs` with raw SQL operations.

**Files created:**
- `Src/LeoBloom.Utilities/ObligationAgreementRepository.fs`

**Files modified:**
- `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` (add to compile order)

**Repository functions:**

1. **`mapReader`** (private) -- maps `NpgsqlDataReader` to `ObligationAgreement`.
   Handles nullable columns via `reader.IsDBNull` checks. Uses
   `ObligationDirection.fromString`, `RecurrenceCadence.fromString`,
   `PaymentMethodType.fromString` for DU columns (these return `Result`, so
   we need to handle the error case -- fail loud since DB data should always be
   valid).

2. **`insert`** -- `INSERT INTO ops.obligation_agreement (...) VALUES (...)
   RETURNING *`. Uses `optParam` helper (local, same pattern as
   `JournalEntryRepository`). For DU fields, call the corresponding
   `toString` before parameterizing. Returns `ObligationAgreement`.

3. **`findById`** -- `SELECT * FROM ops.obligation_agreement WHERE id = @id`.
   Returns `ObligationAgreement option`.

4. **`list`** -- `SELECT * FROM ops.obligation_agreement WHERE 1=1` with
   optional filter predicates appended dynamically:
   - `isActive: bool option` (default: filter to `true`)
   - `obligationType: ObligationDirection option`
   - `cadence: RecurrenceCadence option`
   - `ORDER BY name`
   - Returns `ObligationAgreement list`

   Filter type:
   ```fsharp
   type ListAgreementsFilter =
       { isActive: bool option         // None = no filter; Some true = active only
         obligationType: ObligationDirection option
         cadence: RecurrenceCadence option }
   ```

5. **`update`** -- `UPDATE ops.obligation_agreement SET ... WHERE id = @id
   RETURNING *`. Sets all mutable columns + `modified_at = now()`. Does NOT
   touch `id` or `created_at`. Returns `ObligationAgreement option` (None if
   id not found).

6. **`deactivate`** -- `UPDATE ops.obligation_agreement SET is_active = false,
   modified_at = now() WHERE id = @id RETURNING *`. Returns
   `ObligationAgreement option`.

7. **`hasActiveInstances`** -- `SELECT EXISTS (SELECT 1 FROM
   ops.obligation_instance WHERE obligation_agreement_id = @id AND
   is_active = true)`. Returns `bool`. Used by the service layer's
   deactivation guard.

**Compile order in fsproj:** Insert after `SubtreePLService.fs`, before
`OpeningBalanceService.fs`. The repo depends on `DataSource` (already above it)
and `LeoBloom.Domain` (project reference). The service file (Phase 3) goes
right after this file.

**Verification:** `dotnet build Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` succeeds.

### Phase 3: Service -- Orchestration and DB Validation

**What:** Create `ObligationAgreementService.fs` with validated CRUD operations.

**Files created:**
- `Src/LeoBloom.Utilities/ObligationAgreementService.fs`

**Files modified:**
- `Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` (add to compile order, after repo)

**Service functions:**

1. **`lookupAccount`** (private) -- queries `ledger.account` by ID, returns
   `(int * bool) option` (id, is_active). Used to validate FK references.

2. **`validateAccountReferences`** (private) -- for each of
   `sourceAccountId` / `destAccountId` that is `Some`:
   - Account must exist
   - Account must be active
   - Collects errors into `string list`

3. **`create`** --
   - Log intent
   - Phase 1: `ObligationAgreementValidation.validateCreateCommand`
   - Phase 2: open conn + txn, `validateAccountReferences`, then
     `ObligationAgreementRepository.insert`
   - Commit or rollback
   - Returns `Result<ObligationAgreement, string list>`

4. **`getById`** --
   - Open conn + txn (read-only is fine but we use txn for consistency)
   - `ObligationAgreementRepository.findById`
   - Returns `ObligationAgreement option`

5. **`list`** --
   - Open conn + txn
   - `ObligationAgreementRepository.list`
   - Returns `ObligationAgreement list`

6. **`update`** --
   - Log intent
   - Phase 1: `ObligationAgreementValidation.validateUpdateCommand`
   - Phase 2: open conn + txn, verify agreement exists, validate account
     references, then `ObligationAgreementRepository.update`
   - Commit or rollback
   - Returns `Result<ObligationAgreement, string list>`

7. **`deactivate`** --
   - Log intent
   - Open conn + txn
   - Verify agreement exists (error if not)
   - Check `hasActiveInstances` (error if true: "Cannot deactivate agreement
     with active obligation instances")
   - `ObligationAgreementRepository.deactivate`
   - Commit or rollback
   - Returns `Result<ObligationAgreement, string list>`

**Pattern:** Every public function follows the same try/with + rollback pattern
from `JournalEntryService.post` and `FiscalPeriodService.closePeriod`.

**Verification:** `dotnet build Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj` succeeds.

### Phase 4: Tests

**What:** BDD-driven integration tests. The Gherkin Writer will produce the
scenarios; the Builder will implement them. This phase is included for
completeness -- the test file structure and patterns follow `Project030`'s
test harness conventions.

**Files created:**
- Test file(s) TBD by Gherkin Writer

**Files modified:**
- `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` (add test file to compile order)
- Possibly `Src/LeoBloom.Tests/TestHelpers.fs` (extend
  `insertObligationAgreement` helper if needed for richer test data)

**Test categories expected:**
- Create: happy path, validation failures (name, amount, expected_day),
  invalid account references, inactive account references
- Get by ID: found, not found
- List: default active filter, explicit filters, empty results
- Update: happy path, not found, validation failures, stale account refs
- Deactivate: happy path, not found, blocked by active instances

**Verification:** `dotnet test` passes.

## Compile Order (final state of fsproj)

```xml
<Compile Include="SubtreePLService.fs" />
<Compile Include="ObligationAgreementRepository.fs" />
<Compile Include="ObligationAgreementService.fs" />
<Compile Include="OpeningBalanceService.fs" />
```

`ObligationAgreementRepository.fs` and `ObligationAgreementService.fs` slot in
before `OpeningBalanceService.fs`. The repo must precede the service (F# file
ordering).

## Acceptance Criteria

- [ ] `CreateObligationAgreementCommand` and `UpdateObligationAgreementCommand` exist in `Ops.fs`
- [ ] Pure validation rejects: empty name, name > 100 chars, counterparty > 100 chars, amount <= 0, expected_day outside 1-31, id <= 0 on update
- [ ] Pure validation collects multiple errors (not short-circuit)
- [ ] `ObligationAgreementRepository` compiles and provides: insert, findById, list, update, deactivate, hasActiveInstances
- [ ] `ObligationAgreementService` compiles and provides: create, getById, list, update, deactivate
- [ ] Service create validates account FKs exist and are active before insert
- [ ] Service update validates agreement exists and account FKs are valid before update
- [ ] Service deactivate checks for active obligation instances and refuses if any exist
- [ ] All service functions return `Result<T, string list>` (except getById which returns option, and list which returns list)
- [ ] All service functions log via `Log` module on entry and on error
- [ ] `dotnet build` succeeds for the full solution
- [ ] Integration tests cover all CRUD paths and validation edge cases
- [ ] No changes to existing ledger-track files (one-way dependency: ops depends on ledger, not reverse)

## Risks

- **DU fromString errors in reader mapping.** The DB should always contain valid
  enum strings, but if it doesn't, `mapReader` will throw. Mitigation: fail
  loud with `failwithf` -- corrupt data should not be silently swallowed.
  This matches the philosophy of the ledger track where `EntryType` mapping
  uses a catch-all `| _ -> Credit` (which is arguably worse -- we should be
  stricter).

- **Filter SQL injection via dynamic WHERE clauses.** Mitigation: all filter
  values are parameterized. The dynamic part is only the clause structure
  (e.g., appending `AND is_active = @isActive`), never raw user strings.

- **`hasActiveInstances` false positive if obligation_instance rows exist but
  the is_active semantics differ from what we expect.** Mitigation: the query
  is explicit (`is_active = true`), and the obligation_instance schema is
  already defined. Low risk.

## Out of Scope

- **CLI commands** -- the consumption layer is CLI (per project direction), but
  the CLI surface for obligation agreements is a separate project.
- **Obligation instances (Project 015)** -- we only query them for the
  deactivation guard; no CRUD for instances in this project.
- **Batch operations** -- no bulk create/update. Single-record CRUD only.
- **Audit trail / change history** -- `modified_at` is updated but we don't
  track what changed. Not in scope.
- **Duplicate name detection** -- names are not unique-constrained in the
  schema. If Dan wants uniqueness, that's a schema change + new project.
