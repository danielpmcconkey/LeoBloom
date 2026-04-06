# 021 — Invoice Record Persistence

**Epic:** H — Invoice Lifecycle
**Depends On:** 001
**Status:** Not started

---

Create the persistence layer for invoice records in the ops schema. LeoBloom
does not generate invoices — COYS bots handle calculation, PDF creation,
utility splitting, and delivery. LeoBloom records the result.

**Scope:**

1. **Schema:** `invoice` table in the ops schema. Columns: tenant, fiscal
   period, rent amount, utility share, total amount, document path (nullable),
   notes (nullable), created timestamp, is_active flag. Unique constraint on
   (tenant, fiscal_period_id).
2. **Domain types:** Invoice record type with validation (amounts non-negative,
   tenant non-empty, total = rent + utility share).
3. **Service layer:**
   - `RecordInvoice` — persist a new invoice record. Reject duplicates
     (same tenant + period).
   - `ListInvoices` — query with optional filters (tenant, period).
   - `ShowInvoice` — retrieve a single invoice by ID.

**What this is NOT:**

- No readiness check (cancelled P020 — that's the bot's job).
- No calculation of utility shares or rent amounts (bot does this).
- No PDF generation (bot does this).
- No utility splitting logic.
- No voiding/regeneration workflow (future scope if needed).

**Edge cases CE should address in the BRD:**

- Duplicate (tenant, period) -> reject with clear error.
- Zero rent amount (e.g., Matthew) with non-zero utility share -> valid.
- Null document_path at creation -> valid. Bot may update later.

**Replaces:** Original P021 (Generate Invoice), which assumed LeoBloom owned
the calculation and generation logic. That responsibility moved to COYS bots.
