# 026 — Transfer & Invoice Endpoints

**Epic:** J — API Layer
**Depends On:** 019, 021
**Status:** Not started

---

- `POST /api/transfers` — initiate.
- `PATCH /api/transfers/{id}/confirm` — confirm + create journal entry.
- `GET /api/transfers?status=initiated` — in-flight list.
- `GET /api/invoices/readiness?period={period_key}` — Story 020.
- `POST /api/invoices/generate` — generate for tenant + period.
- `GET /api/invoices?tenant={name}&period={period_key}` — list/filter.
