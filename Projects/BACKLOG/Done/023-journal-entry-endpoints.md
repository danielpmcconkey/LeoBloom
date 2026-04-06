# 023 — Journal Entry Endpoints

**Epic:** J — API Layer
**Depends On:** 005, 006
**Status:** Not started

---

- `POST /api/journal-entries` — create with lines + references. Request body
  mirrors Story 005 inputs. Returns the created entry with ID.
- `GET /api/journal-entries/{id}` — full entry with lines and references.
- `GET /api/journal-entries?period={period_key}&account={code}` — list entries,
  filterable by fiscal period and/or account. Paginated.
- `PATCH /api/journal-entries/{id}/void` — void an entry. Body: `{ "reason": "..." }`.

**Error responses:** Domain validation errors (unbalanced entry, closed period,
inactive account) return 422 with a structured error body describing what failed.
Not found returns 404. Malformed requests return 400.
