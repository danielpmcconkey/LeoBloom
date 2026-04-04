# 025 — Obligation Endpoints

**Epic:** J — API Layer
**Depends On:** 018
**Status:** Not started

---

- `POST /api/obligations/agreements` — create agreement.
- `GET /api/obligations/agreements` — list, filterable by type/active/counterparty.
- `PUT /api/obligations/agreements/{id}` — update.
- `POST /api/obligations/agreements/{id}/spawn` — spawn instances for a date range.
- `GET /api/obligations/instances` — list, filterable by agreement/status/date range.
- `PATCH /api/obligations/instances/{id}/transition` — status transition. Body:
  `{ "to": "confirmed", "amount": 1000.00, "confirmed_date": "2026-04-03" }`.
- `POST /api/obligations/instances/{id}/post` — post to ledger (Story 018).
- `GET /api/obligations/overdue` — Story 017 query.
