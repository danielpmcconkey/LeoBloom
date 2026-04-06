# Project 021 -- PO Brief: Invoice Record Persistence

**Product Owner:** PO Agent
**Date:** 2026-04-06
**Backlog item:** 021-generate-invoice.md (rewritten as invoice record persistence)
**Epic:** H -- Invoice Lifecycle

---

## Problem Statement

LeoBloom has an `ops.invoice` table (migration 1712000018000) and an
`Invoice` domain type in `LeoBloom.Domain.Ops`, but no service layer to
create, query, or validate invoice records. The table is structurally
present but operationally dead -- nothing can write to it or read from it
through the application.

The COYS bot needs to record invoices after it calculates utility shares and
generates PDFs. Dan needs to query invoices ad hoc. Both consumers are
blocked until this persistence layer exists.

P042 (CLI Invoice Commands) depends on this project. It cannot be built
until there is a service to wrap.

---

## What an Invoice Is (and Isn't) in This System

An invoice in LeoBloom is a **source document record** -- it captures the
fact that a tenant invoice was generated for a specific period with specific
amounts. It is NOT:

- A calculation engine (the COYS bot computes rent + utility shares)
- A document generator (the COYS bot creates PDFs)
- A delivery mechanism (the COYS bot sends invoices)
- A receivable tracker (that's the obligation lifecycle, P014-P018)

The invoice record is the accounting system's acknowledgment that a
financial document exists. In GAAP terms, this is recording a source
document -- the same way you'd file a copy of an invoice you sent. The
amounts on the invoice should be consistent with what gets posted to the
ledger, but the invoice record itself is not a ledger posting.

**Key invariant:** `total_amount = rent_amount + utility_share`. This is
an arithmetic identity, not a business rule that could have exceptions.
If it doesn't add up, the data is wrong.

---

## Scope

### What Gets Built

1. **Validation module** (`InvoiceValidation` or equivalent) in Domain:
   - tenant non-empty, within varchar(50)
   - rent_amount >= 0 (Matthew pays $0 rent -- this is valid)
   - utility_share >= 0
   - total_amount = rent_amount + utility_share (reject if not)
   - fiscal_period_id > 0

2. **Repository** (`InvoiceRepository` in LeoBloom.Ops):
   - `insert` -- persist a validated invoice record. Returns the created
     Invoice with its assigned ID.
   - `getById` -- retrieve by PK.
   - `list` -- query with optional filters (tenant, fiscal_period_id).
     Returns all matching active records.

3. **Service** (`InvoiceService` in LeoBloom.Ops):
   - `recordInvoice` -- validate + check for duplicate (tenant,
     fiscal_period_id) + insert. Reject duplicates with clear error.
   - `showInvoice` -- retrieve by ID. Error if not found.
   - `listInvoices` -- filtered query.

4. **Command type** (`RecordInvoiceCommand` in Domain):
   - tenant, fiscalPeriodId, rentAmount, utilityShare, totalAmount,
     documentPath (option), notes (option)

### What Does NOT Get Built

- No readiness check (cancelled P020)
- No calculation of utility shares or rent amounts
- No PDF generation
- No voiding/soft-delete workflow (future scope -- the `is_active` column
  exists in the schema but toggling it is not in this project's scope)
- No CLI commands (that's P042)
- No ledger posting from invoice data
- No invoice numbering scheme

---

## Acceptance Criteria

### Behavioral (Gherkin scenarios)

| ID | Criterion |
|----|-----------|
| B1 | Recording a valid invoice persists it and returns a complete record with assigned ID |
| B2 | Recording a duplicate (same tenant + fiscal_period_id) is rejected with a clear error |
| B3 | Recording an invoice where total != rent + utility is rejected with validation error |
| B4 | Recording an invoice with zero rent amount and non-zero utility share succeeds (Matthew case) |
| B5 | Recording an invoice with zero utility share and non-zero rent succeeds |
| B6 | Recording an invoice with empty tenant is rejected |
| B7 | Recording an invoice with negative amounts is rejected |
| B8 | Recording an invoice with null document_path succeeds |
| B9 | Showing an invoice by ID returns the full record |
| B10 | Showing a nonexistent invoice returns an error |
| B11 | Listing invoices with no filters returns all active invoices |
| B12 | Listing invoices filtered by tenant returns only that tenant's invoices |
| B13 | Listing invoices filtered by fiscal period returns only that period's invoices |
| B14 | Recording an invoice for a nonexistent fiscal period is rejected |

### Structural (verified by QE/Governor, not Gherkin)

| ID | Criterion | Verification |
|----|-----------|--------------|
| S1 | `InvoiceRepository.fs` exists in LeoBloom.Ops | File inspection |
| S2 | `InvoiceService.fs` exists in LeoBloom.Ops | File inspection |
| S3 | `RecordInvoiceCommand` and validation exist in Domain | File inspection |
| S4 | New files are registered in LeoBloom.Ops.fsproj Compile includes | fsproj inspection |
| S5 | No modifications to existing migration files | Diff review |
| S6 | All existing tests continue to pass | `dotnet test` output |
| S7 | Service follows the TransferService pattern (own connection, own transaction) | Code inspection |

---

## Edge Cases the Brainstorm/Plan Should Address

1. **Fiscal period validation depth:** Should `recordInvoice` check that
   the fiscal period exists? Yes -- the FK constraint will catch it at the
   DB level, but the service should validate first and return a clear
   domain error rather than leaking a Postgres FK violation. Follow the
   same pattern as `TransferService.initiate` (pure validation, then DB
   validation, then insert).

2. **Fiscal period open/closed:** Should you be able to record an invoice
   against a closed period? The backlog item doesn't say. My instinct:
   yes, allow it. The invoice is a source document record, not a ledger
   posting. You should be able to record that an invoice exists for a past
   period. But this is a GAAP-adjacent question worth the Brainstorm's
   attention.

3. **Amount precision:** The schema uses `numeric(12,2)`. The validation
   should reject amounts with more than 2 decimal places, or round.
   Probably reject -- explicit is better than silent rounding in an
   accounting system.

4. **Concurrent duplicate detection:** Two bots recording the same
   (tenant, period) simultaneously. The UNIQUE constraint handles this
   at the DB level, but the service should catch the constraint violation
   and return a domain error, not a raw exception.

---

## Dependencies

- **Depends on:** P001 (Database -- schema exists), existing LeoBloom.Ops
  project structure, existing Domain types
- **Blocked by:** Nothing -- all prerequisites are complete
- **Blocks:** P042 (CLI Invoice Commands)

---

## Brainstorm Assessment

**Recommendation: Brainstorm first, then plan.**

The backlog item is well-specified for scope, but there are real questions
worth thinking through:

1. The fiscal period open/closed question above has GAAP implications.
   Source documents vs. ledger postings have different rules about period
   sensitivity, and I want someone to think through this deliberately
   rather than the Planner just guessing.

2. The `generated_at` column exists in the schema alongside `created_at`.
   What's the semantic difference? Is `generated_at` when the COYS bot
   created the PDF, and `created_at` when LeoBloom recorded it? The
   Brainstorm should clarify this so the command type and service handle
   it correctly.

3. The existing `Invoice` domain type has `generatedAt: DateTimeOffset`.
   Should the `RecordInvoiceCommand` accept a `generatedAt` from the
   caller (the COYS bot knows when it generated the PDF), or should it
   default to `now()`? This affects the command shape.

These aren't blockers, but they're the kind of thing that turns into a
mid-build "wait, what should this do?" if nobody thinks about them first.

---

## Backlog Status Update

P021 status changed from "Not started" to **In Progress** as of 2026-04-06.
