# 017 — Overdue Detection

**Epic:** E — Obligation Lifecycle
**Depends On:** 016
**Status:** Not started

---

Identify obligations that are past due. This is the nagging agent's primary
input signal.

**The query (from DataModelSpec key query #3):**

All active `obligation_instance` rows where:
- `status_id` references `expected` or `in_flight`
- `expected_date < today`
- `is_active = true`

**Output per overdue instance:**

- Agreement name + instance name (e.g., "Jeffrey rent — Apr 2026")
- Counterparty
- Expected amount (from agreement or instance)
- Expected date
- Days overdue (`today - expected_date`)
- Current status (expected vs in_flight — in_flight means it's been initiated
  but hasn't settled, which is a different kind of overdue)

**This story also includes the automatic `expected → overdue` transition.** When
overdue detection runs, any instance that qualifies should have its status
updated to `overdue` (unless it's `in_flight` — those stay `in_flight` but
appear in the overdue report with a flag).

**Edge cases CE should address in the BRD:**

- Instance with expected_date = today → NOT overdue. Overdue means past due.
  Strictly `expected_date < today`.
- Variable-amount instance that's overdue but has no amount → still overdue.
  The amount being unknown doesn't exempt it.
- Should this be a one-shot query or a scheduled process? **Both.** The domain
  function is a query. The nagging agent (beyond the horizon) will call it on a
  schedule. This story builds the query and the auto-transition, not the
  scheduling.

**DataModelSpec references:** Key query #3.
