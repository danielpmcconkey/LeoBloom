# Project 021 -- Brainstorm: Invoice Persistence

## What We're Building

A persistence layer for invoice records: validation, repository, and service.
The `ops.invoice` table and the `Invoice` domain type already exist. Nothing
can currently write to or read from the table through the application. This
project wires it up so the COYS bot can record invoices and Dan can query them.

This is strictly a source-document-recording layer. No calculation, no PDF
generation, no ledger posting, no voiding workflow.

## Why This Approach

Follow the TransferRepository/TransferService pattern exactly. It's proven,
it's what the codebase uses, and there's no reason to invent a new pattern
for a simpler use case. Invoice persistence is actually less complex than
transfers -- there's no multi-phase confirm workflow, no journal entry posting,
no idempotency guard. It's validate-then-insert with query support.

### Alternatives Considered

1. **Generic CRUD abstraction** -- Rejected. YAGNI. The codebase uses
   explicit per-entity repository/service modules. One invoice entity doesn't
   justify an abstraction layer.

2. **Putting validation in the service instead of Domain** -- Rejected. The
   existing pattern (ObligationAgreementValidation in Domain) keeps pure
   validation in the domain layer. DB-dependent validation (fiscal period
   existence) lives in the service. Follow the pattern.

## Key Decisions

### 1. Closed period recording is allowed

An invoice is a source document, not a journal entry. You CAN record an
invoice against a closed fiscal period. The fiscal period gate belongs on the
posting side (journal entries), not on recording the existence of a document.
This is consistent with GAAP treatment of source documents vs. ledger postings.

The service validates that the fiscal period **exists** (FK check before
hitting the DB constraint), but does NOT check `is_open`.

### 2. `generated_at` comes from the caller

The schema has `generated_at timestamptz NOT NULL DEFAULT now()`. The PO brief
flagged the ambiguity between `generated_at` and `created_at`. Resolution:

- **`generated_at`** = when the COYS bot generated the PDF. Caller-provided.
  The `RecordInvoiceCommand` accepts this as a required field.
- **`created_at`** = when the row was inserted into the database. DB-managed
  via `DEFAULT now()`.

The `DEFAULT now()` on `generated_at` in the migration is a safety net, not
the intended semantic. The service should always pass the caller's value
explicitly. The command type makes it required so nobody accidentally relies
on the default.

### 3. `RecordInvoiceCommand` shape

```
RecordInvoiceCommand:
  tenant: string              -- required
  fiscalPeriodId: int         -- required
  rentAmount: decimal         -- required, >= 0
  utilityShare: decimal       -- required, >= 0
  totalAmount: decimal        -- required, must = rent + utility
  generatedAt: DateTimeOffset -- required, caller-provided
  documentPath: string option -- optional
  notes: string option        -- optional
```

This mirrors the Invoice domain type minus the DB-managed fields (id,
isActive, createdAt, modifiedAt).

### 4. Validation strategy: reject, don't round

The schema uses `numeric(12,2)`. If the caller passes `100.999`, reject it
with a validation error. Silent rounding in an accounting system is a bug
waiting to happen. The PO brief already called this out. The validation module
should check that amounts have at most 2 decimal places.

### 5. Duplicate detection: service check + DB constraint

The service checks for an existing (tenant, fiscal_period_id) record before
inserting. If a race condition slips past (two bots recording simultaneously),
the UNIQUE constraint catches it. The service should catch the
`PostgresException` with the unique violation code (23505) and return a
domain error, not a raw exception.

### 6. Fiscal period validation: service-level with clear errors

Follow the TransferService.initiate pattern:
1. Pure validation first (amounts, tenant, arithmetic identity)
2. DB validation (fiscal period exists via FiscalPeriodRepository.findById)
3. Duplicate check (query by tenant + fiscal_period_id)
4. Insert

If the fiscal period doesn't exist, return a domain error. Don't let the FK
constraint raise a raw Postgres exception.

### 7. Service pattern: own connection, own transaction

Each public method in InvoiceService opens its own connection and transaction,
same as TransferService. No connection/transaction sharing across service
boundaries.

## Resolved Questions

- **Q: Can you record an invoice for a closed fiscal period?**
  A: Yes. Source document recording, not a ledger posting. Period gate belongs
  on posting, not recording.

- **Q: What's `generated_at` vs `created_at`?**
  A: `generated_at` = PDF generation time (caller-provided).
  `created_at` = DB insertion time (DB-managed).

- **Q: Should `RecordInvoiceCommand` accept `generatedAt` or default to now()?**
  A: Accept it as required. The COYS bot knows when it generated the PDF.

- **Q: Reject or round on precision violations?**
  A: Reject. Explicit errors beat silent data mutation in accounting.

## Open Questions

None. All PO-raised questions have been resolved.

## Out of Scope

- Invoice voiding / soft-delete toggle (the `is_active` column exists but
  toggling it is future scope)
- CLI commands (that's P042)
- Utility share calculation
- PDF generation or delivery
- Ledger posting from invoice data
- Invoice numbering scheme
- Any modification to existing migration files

## Implementation Surface (for the Planner)

**New files needed:**
- `Src/LeoBloom.Domain/Ops.fs` -- add `RecordInvoiceCommand` type and
  `InvoiceValidation` module (append to existing file)
- `Src/LeoBloom.Ops/InvoiceRepository.fs` -- new file, insert/findById/list
- `Src/LeoBloom.Ops/InvoiceService.fs` -- new file, recordInvoice/showInvoice/listInvoices
- `Src/LeoBloom.Ops/LeoBloom.Ops.fsproj` -- add Compile includes for new files

**Existing patterns to follow:**
- TransferRepository for repository shape (mapReader, selectColumns, insert
  with RETURNING, findById, parameterized queries via NpgsqlCommand)
- TransferService.initiate for service shape (pure validation -> DB validation
  -> insert, own connection/transaction, structured logging)
- ObligationAgreementValidation for domain validation shape (individual
  validator functions composed into a validateCommand function, Result<unit,
  string list> return type)
- DataHelpers.optParam for optional parameter handling
- FiscalPeriodRepository.findById for the fiscal period existence check

**Dependencies:**
- FiscalPeriodRepository.findById (already exists in LeoBloom.Ledger)
- DataSource.openConnection (already exists in LeoBloom.Utilities)
- Log module (already exists in LeoBloom.Utilities)
