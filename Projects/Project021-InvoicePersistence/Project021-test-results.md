# Project 021 -- Test Results

**Date:** 2026-04-06
**Commit:** ad67ab55a1abbac410afec4ef8a64a253f3c6f70
**Branch:** feature/p021-invoice-persistence
**Result:** 21/21 acceptance criteria verified

## Test Suite Summary

Full suite: 639 total, 637 passed, 2 failed.

The 2 failures are pre-existing `PostObligationToLedgerTests` closed-period posting tests
(present on `main` as well). They are unrelated to P021:

- `posting when fiscal period is closed returns error`
- `failed post to closed period leaves instance in confirmed status with no journal entry`

All 24 invoice tests (covering 20 Gherkin scenarios) pass: 24/24.

## Acceptance Criteria Verification

### Behavioral

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| B1 | Recording a valid invoice persists it and returns a complete record with assigned ID | Yes | @FT-INV-001 passes; asserts id > 0, timestamps, tenant, totalAmount, generatedAt |
| B2 | Recording a duplicate (same tenant + fiscal_period_id) is rejected with a clear error | Yes | @FT-INV-013 passes; error contains "already exists" |
| B3 | Recording an invoice where total != rent + utility is rejected with validation error | Yes | @FT-INV-010 passes; error contains "total" |
| B4 | Recording an invoice with zero rent amount and non-zero utility share succeeds | Yes | @FT-INV-004 passes; rentAmount=0, utilityShare=85.50 |
| B5 | Recording an invoice with zero utility share and non-zero rent succeeds | Yes | @FT-INV-005 passes; utilityShare=0, rentAmount=1200 |
| B6 | Recording an invoice with empty tenant is rejected | Yes | @FT-INV-006 passes; error contains "tenant" |
| B7 | Recording an invoice with negative amounts is rejected | Yes | @FT-INV-008 (3 tests for rent/utility/total) all pass |
| B8 | Recording an invoice with null document_path succeeds | Yes | @FT-INV-002 passes; asserts documentPath.IsNone and notes.IsNone |
| B9 | Showing an invoice by ID returns the full record | Yes | @FT-INV-014 passes; round-trips tenant, id, totalAmount |
| B10 | Showing a nonexistent invoice returns an error | Yes | @FT-INV-015 passes; error contains "does not exist" |
| B11 | Listing invoices with no filters returns all active invoices | Yes | @FT-INV-016 passes; finds both tenants in unfiltered list |
| B12 | Listing invoices filtered by tenant returns only that tenant's invoices | Yes | @FT-INV-017 passes; Jeffrey present, Adam absent |
| B13 | Listing invoices filtered by fiscal period returns only that period's invoices | Yes | @FT-INV-018 passes; exactly 1 match for fp1 |
| B14 | Recording an invoice for a nonexistent fiscal period is rejected | Yes | @FT-INV-012 passes; error contains "fiscal period" |

### Structural

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| S1 | InvoiceRepository.fs exists in Src/LeoBloom.Ops/ | Yes | File present, 4979 bytes |
| S2 | InvoiceService.fs exists in Src/LeoBloom.Ops/ | Yes | File present, 4129 bytes |
| S3 | RecordInvoiceCommand and InvoiceValidation exist in Domain Ops.fs | Yes | Found at lines 378, 388, 418 in Ops.fs |
| S4 | New files are registered in LeoBloom.Ops.fsproj Compile includes | Yes | Lines 16-17; InvoiceRepository before InvoiceService (correct F# order) |
| S5 | No modifications to existing migration files | Yes | git diff main shows zero changes in Migrations/ |
| S6 | All existing tests continue to pass (dotnet test) | Yes | 637/639 pass; 2 failures are pre-existing on main (see above) |
| S7 | Service follows the TransferService pattern (own connection, own txn per public method) | Yes | All 3 public methods open own conn + txn; race condition handled with PostgresException 23505 catch |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-INV-001 | Recording a valid invoice persists it and returns a complete record | Yes | Yes |
| @FT-INV-002 | Recording an invoice with null optional fields succeeds | Yes | Yes |
| @FT-INV-003 | Recording an invoice for a closed fiscal period succeeds | Yes | Yes |
| @FT-INV-004 | Recording an invoice with zero rent and non-zero utility succeeds | Yes | Yes |
| @FT-INV-005 | Recording an invoice with zero utility and non-zero rent succeeds | Yes | Yes |
| @FT-INV-006 | Recording with empty tenant is rejected | Yes | Yes |
| @FT-INV-007 | Recording with tenant exceeding 50 characters is rejected | Yes | Yes |
| @FT-INV-008 | Recording with negative amount is rejected (3 examples) | Yes (3 tests) | Yes |
| @FT-INV-009 | Recording with more than 2 decimal places is rejected (3 examples) | Yes (3 tests) | Yes |
| @FT-INV-010 | Recording with total not equal to rent plus utility is rejected | Yes | Yes |
| @FT-INV-011 | Recording with multiple validation errors collects all errors | Yes | Yes |
| @FT-INV-012 | Recording with nonexistent fiscal period is rejected | Yes | Yes |
| @FT-INV-013 | Recording a duplicate tenant + fiscal period is rejected | Yes | Yes |
| @FT-INV-014 | Showing an invoice by ID returns the full record | Yes | Yes |
| @FT-INV-015 | Showing a nonexistent invoice returns an error | Yes | Yes |
| @FT-INV-016 | Listing invoices with no filter returns all active invoices | Yes | Yes |
| @FT-INV-017 | Listing invoices filtered by tenant returns only that tenant | Yes | Yes |
| @FT-INV-018 | Listing invoices filtered by fiscal period returns only that period | Yes | Yes |
| @FT-INV-019 | Listing invoices filtered by both tenant and fiscal period | Yes | Yes |
| @FT-INV-020 | Listing invoices returns empty when none match | Yes | Yes |

## Verdict

**APPROVED**

Every acceptance criterion is verified against actual repo state and live test execution.
All 20 Gherkin scenarios have corresponding tests that pass. The evidence chain is clean --
no fabrication, no circular reasoning, no stale artifacts. The 2 pre-existing failures are
documented and unrelated to this project's scope.
