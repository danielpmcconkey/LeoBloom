# 024 — Reporting Endpoints

**Epic:** J — API Layer
**Depends On:** 011, 012, 013
**Status:** Not started

---

- `GET /api/accounts/{code}/balance?as_of={date}` — Story 007.
- `GET /api/reports/trial-balance?period={period_key}` — Story 008.
- `GET /api/reports/income-statement?period={period_key}` — Story 011.
- `GET /api/reports/balance-sheet?as_of={date}` — Story 012.
- `GET /api/reports/pnl?root={account_code}&period={period_key}` — Story 013.

All read-only. No auth concerns beyond preventing accidental writes.
