# 009 — Close / Reopen Fiscal Period

**Epic:** C — Fiscal Period Management
**Depends On:** 005
**Status:** Not started

---

Toggle `fiscal_period.is_open` and enforce the consequences.

**Close:** Set `is_open = false`. From this point:
- The journal entry posting engine (Story 005) rejects any new entry targeting
  this period.
- Voiding entries in the closed period is still allowed (see Story 006 edge
  case discussion — voiding is metadata, not a new posting).
- Trial balance and all read operations still work.

**Reopen:** Set `is_open = true`. This should require an explicit reason (logged
somewhere — even just a note field or an event). Reopening is not casual; it
means the books were wrong and need correction.

**Edge cases CE should address in the BRD:**

- Close a period that has no entries → valid. An empty closed period is fine.
- Close a period that still has in-flight obligations (ops side) → **not our
  problem at the ledger layer.** The ledger doesn't know about ops. If the ops
  layer needs to warn about this, that's Epic E's concern.
- Reopen a period, post a correction, close again → valid workflow. No limit on
  transitions.
- What about the `fiscal_period` table itself — can Dan add new periods? Not in
  this story. Periods are seeded by migration (36 months). Adding more is a
  future migration. Keep it simple.

**DataModelSpec references:** `fiscal_period.is_open`.
