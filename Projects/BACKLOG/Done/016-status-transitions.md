# 016 — Status Transitions

**Epic:** E — Obligation Lifecycle
**Depends On:** 015
**Status:** Not started

---

Enforce the obligation instance lifecycle state machine. This is the rulebook
for how obligations move through their life.

**The state machine (from DataModelSpec):**

```
expected → in_flight → confirmed → posted
                ↘ overdue
expected → overdue → confirmed → posted
expected → skipped
```

**Valid transitions and their requirements:**

| From | To | Required |
|------|-----|----------|
| expected | in_flight | — |
| expected | overdue | (set automatically by Story 017, or manually) |
| expected | skipped | `notes` should explain why (soft requirement — warn, don't reject) |
| in_flight | confirmed | `amount` must be set, `confirmed_date` must be set |
| in_flight | overdue | — |
| overdue | confirmed | `amount` must be set, `confirmed_date` must be set |
| confirmed | posted | `journal_entry_id` must be set (handled by Story 018) |

**Invalid transitions are rejected.** No going backwards. If something was
confirmed incorrectly, the corrective action is: void the journal entry (if
posted), create a new instance, and skip the bad one with a note.

**Field updates on transition:**

- `→ confirmed`: set `confirmed_date`, set `amount` (if variable), update
  `modified_at`.
- `→ posted`: set `journal_entry_id`, update `modified_at`.
- `→ skipped`: set `is_active = false`, update `modified_at`.

**Edge cases CE should address in the BRD:**

- Transition on an inactive instance → reject. Skipped/deactivated instances
  are terminal.
- Setting `amount` on a fixed-amount instance at confirmation → allow it. The
  actual amount might differ from the expected amount (partial payment, late
  fee). The agreement amount is the expectation; the instance amount is reality.
- Can `confirmed_date` differ from `expected_date`? Yes — rent expected on the
  1st might arrive on the 3rd. That's normal.
- Should transitions emit events? **Not yet.** DataModelSpec open question #4
  (audit log) is beyond the horizon. `modified_at` captures when. If we need
  "what changed," that's a future epic.

**DataModelSpec references:** `obligation_instance` table, status lifecycle
diagram, invariants section.
