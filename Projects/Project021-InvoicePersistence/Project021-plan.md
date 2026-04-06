# Project 021 -- Plan: Invoice Persistence

## Objective

Build the persistence layer for invoice records: a command type, pure
validation module, repository, and service. The COYS bot and Dan's ad-hoc
queries are blocked until the `ops.invoice` table is wired up through the
application. This project follows the TransferRepository/TransferService
pattern and delivers validate-then-insert with filtered query support.

## Research Decision

Strong local context, skipping external research. The TransferRepository,
TransferService, ObligationAgreementValidation, and ObligationAgreementRepository
(with its `list` + filter pattern) provide all the patterns needed.

---

## Phases

### Phase 1: Domain -- Command Type and Validation

**What:** Add `RecordInvoiceCommand`, `ListInvoicesFilter`, and
`InvoiceValidation` module to `Src/LeoBloom.Domain/Ops.fs`.

**Files modified:**
- `Src/LeoBloom.Domain/Ops.fs` -- append after `ConfirmTransferCommand`

**Details:**

1. `RecordInvoiceCommand` record type:
   ```
   tenant: string
   fiscalPeriodId: int
   rentAmount: decimal
   utilityShare: decimal
   totalAmount: decimal
   generatedAt: DateTimeOffset
   documentPath: string option
   notes: string option
   ```

2. `ListInvoicesFilter` record type:
   ```
   tenant: string option
   fiscalPeriodId: int option
   ```
   Defined here in Domain (not in the repository file) because the brainstorm
   says filter by tenant and fiscal_period_id, and keeping filter types near
   commands is cleaner. However, the existing pattern puts `ListAgreementsFilter`
   in `ObligationAgreementRepository.fs`. **Follow the existing pattern:**
   define `ListInvoicesFilter` at the top of `InvoiceRepository.fs` (Phase 2),
   not in Domain. This keeps consistency with how the codebase already works.

3. `InvoiceValidation` module (inside `Ops` module, after `ConfirmTransferCommand`):
   - `validateTenant` -- non-empty, max 50 chars (matches `varchar(50)`)
   - `validateAmount` -- >= 0, at most 2 decimal places. Shared by
     rentAmount, utilityShare, totalAmount. Takes a field name string for
     the error message.
   - `validateTotalEqualsComponents` -- `totalAmount = rentAmount + utilityShare`
     exactly. Reject if not.
   - `validateFiscalPeriodId` -- > 0
   - `validateCommand` -- composes all validators, returns
     `Result<unit, string list>` following the `ObligationAgreementValidation`
     pattern (collect all errors, not fail-fast).

   **Decimal precision check:** Use the pattern
   `amount * 100m <> System.Math.Truncate(amount * 100m)` or equivalent
   `amount <> System.Math.Round(amount, 2)` to detect more than 2 decimal
   places. The `Math.Round` comparison is cleaner. Reject with error like
   `"{fieldName} must have at most 2 decimal places"`.

**Verification:** `dotnet build` succeeds. The new types and validation
functions are resolvable from `LeoBloom.Domain.Ops`.

---

### Phase 2: Repository -- InvoiceRepository

**What:** Create `Src/LeoBloom.Ops/InvoiceRepository.fs` with raw SQL
persistence for invoice records.

**Files created:**
- `Src/LeoBloom.Ops/InvoiceRepository.fs`

**Files modified:**
- `Src/LeoBloom.Ops/LeoBloom.Ops.fsproj` -- add `<Compile Include="InvoiceRepository.fs" />`
  after `TransferService.fs`

**Details:**

Module: `LeoBloom.Ops.InvoiceRepository`

1. `ListInvoicesFilter` type at top of file (follows `ListAgreementsFilter`
   pattern in `ObligationAgreementRepository.fs`):
   ```
   tenant: string option
   fiscalPeriodId: int option
   ```

2. `mapReader` (private) -- maps `DbDataReader` to `Invoice` domain type.
   Column order matches `selectColumns`. Handle nullable columns
   (`document_path`, `notes`) with `IsDBNull` checks.

3. `selectColumns` (private) -- string listing all columns in SELECT order:
   `id, tenant, fiscal_period_id, rent_amount, utility_share, total_amount,
    generated_at, document_path, notes, is_active, created_at, modified_at`

4. `insert (txn: NpgsqlTransaction) (cmd: RecordInvoiceCommand) : Invoice` --
   INSERT with `RETURNING {selectColumns}`. Pass `generated_at` explicitly
   from the command (do not rely on the DB default). Use `DataHelpers.optParam`
   for `document_path` and `notes`.

5. `findById (txn: NpgsqlTransaction) (id: int) : Invoice option` --
   SELECT by PK, return `Some` or `None`.

6. `findByTenantAndPeriod (txn: NpgsqlTransaction) (tenant: string) (fiscalPeriodId: int) : Invoice option` --
   SELECT by the UNIQUE constraint columns. Used by the service for duplicate
   detection before insert.

7. `list (txn: NpgsqlTransaction) (filter: ListInvoicesFilter) : Invoice list` --
   Follow the `ObligationAgreementRepository.list` pattern: build WHERE
   clauses dynamically from filter options. Always include
   `is_active = true` (PO brief says "all matching active records").
   Order by `id` descending (most recent first).

**Verification:** `dotnet build` succeeds.

---

### Phase 3: Service -- InvoiceService

**What:** Create `Src/LeoBloom.Ops/InvoiceService.fs` with orchestration
logic for recording, showing, and listing invoices.

**Files created:**
- `Src/LeoBloom.Ops/InvoiceService.fs`

**Files modified:**
- `Src/LeoBloom.Ops/LeoBloom.Ops.fsproj` -- add `<Compile Include="InvoiceService.fs" />`
  after `InvoiceRepository.fs`

**Details:**

Module: `LeoBloom.Ops.InvoiceService`

Opens: `Npgsql`, `LeoBloom.Domain.Ops`, `LeoBloom.Utilities`, `LeoBloom.Ledger`

1. `recordInvoice (cmd: RecordInvoiceCommand) : Result<Invoice, string list>`

   Follow the `TransferService.initiate` three-phase pattern:

   **Phase 1 -- Pure validation:**
   Call `InvoiceValidation.validateCommand cmd`. If errors, return
   `Error errors` immediately.

   **Phase 2 -- DB validation + insert:**
   Open connection + transaction. Then:
   - Fiscal period existence: `FiscalPeriodRepository.findById txn cmd.fiscalPeriodId`.
     If `None`, rollback + return `Error ["Fiscal period with id N does not exist"]`.
     Do NOT check `is_open` (brainstorm decision: closed periods allowed).
   - Duplicate check: `InvoiceRepository.findByTenantAndPeriod txn cmd.tenant cmd.fiscalPeriodId`.
     If `Some _`, rollback + return `Error ["An invoice already exists for tenant 'X' in fiscal period N"]`.
   - Insert: `InvoiceRepository.insert txn cmd`. Commit. Return `Ok invoice`.

   **Race condition handling:**
   Wrap the insert in a try/catch. If `PostgresException` with `SqlState = "23505"`
   (unique violation), return the same duplicate error message. Any other
   exception: rollback + `Error ["Persistence error: ..."]` following the
   TransferService pattern.

   **Logging:** Structured logging via `Log.info` / `Log.warn` / `Log.errorExn`
   at the same granularity as TransferService.

2. `showInvoice (id: int) : Result<Invoice, string list>`

   Open connection + transaction. `InvoiceRepository.findById txn id`.
   If `None`, return `Error ["Invoice with id N does not exist"]`.
   If `Some`, return `Ok invoice`. Commit/rollback in try/with.

3. `listInvoices (filter: ListInvoicesFilter) : Invoice list`

   Open connection + transaction. `InvoiceRepository.list txn filter`.
   Return results. On exception, log + rollback + return empty list
   (follows `ObligationAgreementService.list` pattern).

**Verification:** `dotnet build` succeeds. The full solution compiles with
no warnings related to new code.

---

### Phase 4: Test Infrastructure Updates

**What:** Update `TestHelpers.fs` to support invoice test cleanup and
provide an insert helper for invoice test setup.

**Files modified:**
- `Src/LeoBloom.Tests/TestHelpers.fs`

**Details:**

1. **TestCleanup.Tracker** -- add `mutable InvoiceIds: int list` field.
   Initialize to `[]` in `create`.

2. **TestCleanup.trackInvoice** -- `let trackInvoice id tracker = tracker.InvoiceIds <- id :: tracker.InvoiceIds`

3. **TestCleanup.deleteAll** -- add invoice cleanup BEFORE fiscal_period
   cleanup (invoices FK to fiscal_period). Add:
   `tryDelete "ops.invoice" "id" tracker.InvoiceIds`
   Insert this line before the existing `tryDelete "ops.invoice" "fiscal_period_id" tracker.FiscalPeriodIds` line.
   The existing line cleans invoices by fiscal_period_id (for transfers/other
   tests that create fiscal periods). The new line cleans by direct invoice id.

4. **InsertHelpers.insertInvoice** -- convenience for test setup:
   ```
   let insertInvoice (conn) (tracker) (tenant) (fiscalPeriodId) (rent) (utility) (total) : int
   ```
   Direct SQL INSERT into `ops.invoice`, returns id, tracks with `trackInvoice`.
   Sets `generated_at` to `DateTimeOffset.UtcNow`.

**Verification:** `dotnet build` for test project succeeds.

---

## File Change Summary

| File | Action | Phase |
|------|--------|-------|
| `Src/LeoBloom.Domain/Ops.fs` | Modify (append) | 1 |
| `Src/LeoBloom.Ops/InvoiceRepository.fs` | Create | 2 |
| `Src/LeoBloom.Ops/InvoiceService.fs` | Create | 3 |
| `Src/LeoBloom.Ops/LeoBloom.Ops.fsproj` | Modify (add Compile includes) | 2, 3 |
| `Src/LeoBloom.Tests/TestHelpers.fs` | Modify (tracker + helper) | 4 |

No migration files are created or modified.

---

## Acceptance Criteria

### Behavioral (traced from PO brief)

- [ ] B1: Recording a valid invoice persists it and returns a complete record with assigned ID
- [ ] B2: Recording a duplicate (same tenant + fiscal_period_id) is rejected with a clear error
- [ ] B3: Recording an invoice where total != rent + utility is rejected with validation error
- [ ] B4: Recording an invoice with zero rent amount and non-zero utility share succeeds
- [ ] B5: Recording an invoice with zero utility share and non-zero rent succeeds
- [ ] B6: Recording an invoice with empty tenant is rejected
- [ ] B7: Recording an invoice with negative amounts is rejected
- [ ] B8: Recording an invoice with null document_path succeeds
- [ ] B9: Showing an invoice by ID returns the full record
- [ ] B10: Showing a nonexistent invoice returns an error
- [ ] B11: Listing invoices with no filters returns all active invoices
- [ ] B12: Listing invoices filtered by tenant returns only that tenant's invoices
- [ ] B13: Listing invoices filtered by fiscal period returns only that period's invoices
- [ ] B14: Recording an invoice for a nonexistent fiscal period is rejected

### Structural

- [ ] S1: `InvoiceRepository.fs` exists in `Src/LeoBloom.Ops/`
- [ ] S2: `InvoiceService.fs` exists in `Src/LeoBloom.Ops/`
- [ ] S3: `RecordInvoiceCommand` and `InvoiceValidation` exist in Domain `Ops.fs`
- [ ] S4: New files are registered in `LeoBloom.Ops.fsproj` Compile includes
- [ ] S5: No modifications to existing migration files
- [ ] S6: All existing tests continue to pass (`dotnet test`)
- [ ] S7: Service follows the TransferService pattern (own connection, own transaction per public method)

---

## Risks

1. **Decimal precision validation edge case:** `System.Math.Round(amount, 2)` uses
   banker's rounding by default. For the precision *check* (not rounding), this
   doesn't matter -- we're comparing equality, and any value with >2 decimal
   places will not round-trip to itself. But the Builder should use
   `MidpointRounding.AwayFromZero` or the truncation approach to be explicit.
   *Mitigation:* Call out in the plan; Gherkin Writer should include a test
   with `100.005` (banker's rounding edge).

2. **F# compile order in fsproj:** `InvoiceRepository.fs` must come before
   `InvoiceService.fs` because the service depends on the repository.
   *Mitigation:* Explicit ordering in Phase 2/3 details.

3. **TestCleanup ordering:** Invoice rows FK to `fiscal_period`, so invoice
   cleanup must run before fiscal_period cleanup. The existing cleanup already
   deletes `ops.invoice` by `fiscal_period_id`; adding by `id` is additive.
   *Mitigation:* Phase 4 specifies exact insertion point.

4. **Race condition duplicate detection:** The `catch PostgresException 23505`
   path needs to distinguish the invoice unique constraint from any other
   unique constraint that might fire. In practice, the only UNIQUE on
   `ops.invoice` is `(tenant, fiscal_period_id)`, so a blanket 23505 catch
   is safe here. If additional unique constraints are added later, this would
   need revisiting. *Mitigation:* Acceptable for now; note for future.

---

## Out of Scope

- Invoice voiding / soft-delete toggle
- CLI commands (P042)
- Utility share calculation or PDF generation
- Ledger posting from invoice data
- Invoice numbering scheme
- Any modification to existing migration files
- Any `is_open` check on fiscal period (explicitly decided: closed periods allowed)
