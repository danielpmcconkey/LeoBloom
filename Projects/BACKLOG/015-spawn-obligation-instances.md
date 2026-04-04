# 015 — Spawn Obligation Instances

**Epic:** E — Obligation Lifecycle
**Depends On:** 014
**Status:** Not started

---

From an agreement + a date range, generate the individual instances that will
be tracked through the status lifecycle.

**Mechanics:**

- For a `monthly` agreement with `expected_day = 1` over range 2026-04 to
  2026-06, generate three instances:
  - "Apr 2026" — expected_date 2026-04-01
  - "May 2026" — expected_date 2026-05-01
  - "Jun 2026" — expected_date 2026-06-01
- For `quarterly`: one per quarter. For `annual`: one per year. For `one_time`:
  exactly one instance.
- Each instance starts with `status_id` = `expected`.
- Fixed-amount agreements (`amount IS NOT NULL`): pre-fill `instance.amount`
  from `agreement.amount`.
- Variable-amount agreements (`amount IS NULL`): leave `instance.amount` null.
  It gets set when the bill arrives (status transition to confirmed).
- Instance `name` = period label, e.g., "Apr 2026". Combined with the agreement
  name for display: "Jeffrey rent — Apr 2026".

**Unique constraint (application layer):** No two instances for the same
agreement with the same `expected_date`. If you try to spawn instances for a
range that already has some, skip the existing ones and only create the missing
ones. Don't fail the whole batch.

**Edge cases CE should address in the BRD:**

- Spawning for a range that's entirely already covered → no-op, success.
- Agreement with no `expected_day` → use the first of the period (day 1 for
  monthly, first day of quarter, Jan 1 for annual).
- `expected_day = 31` in a month with 30 days → use last day of month.
- Variable-amount instances: `amount` is null, `due_date` is null,
  `document_path` is null. All get set when the bill arrives. The BRD should
  describe the "bill arrival" update as a separate operation from status
  transition (set amount/due_date/document_path, then transition status).
- One-time agreements: generate exactly one instance regardless of date range.
  If it already exists, no-op.

**DataModelSpec references:** `obligation_instance` table, status lifecycle
diagram.
