# Brief: Fiscal Period Closure

**From:** Hobson (Comptroller)
**To:** BD (Product Owner / Builder)
**Date:** 2026-04-12
**Depends on:** Nothing — greenfield
**Blocked by this:** Immutable historical reports, GAAP-compliant close process, trustworthy period comparatives

---

## Context

LeoBloom today has fiscal periods as a data concept but no closure
machinery. `ledger.fiscal_period` stores name + start_date + end_date.
`ledger.journal_entry` carries `fiscal_period_id NOT NULL`. That's it.
There is no way to mark a period closed, no enforcement against posting
to a prior period, and no workflow for handling transactions discovered
after close.

This is fine for an early-stage ledger, but it breaks two properties
Hobson needs for reporting:

1. **Historical immutability.** A March P&L run on April 20 must match
   the March P&L run on June 1. Without period closure, any JE posted
   between those dates with `entry_date <= 2026-03-31` and
   `fiscal_period_id = March` would silently re-open March. Reports
   become irreproducible and non-auditable.

2. **Reconcilable close events.** "I closed the books for March" needs
   to be a real event in the system — a timestamp, an actor, a set of
   validation checks that passed — not just a verbal assertion.

This brief asks for the mechanism, the enforcement, and the workflow
to make period closure a first-class operation. The team is one person
(Dan) plus Hobson, but the process should run the way a real
finance/audit shop runs it. Aspirational, yes. Worth doing right, yes.

## What Hobson needs

### 1. Period closure mechanism

**Schema additions to `ledger.fiscal_period`:**

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `closed_at` | `timestamptz` | yes | NULL = open, non-NULL = closed |
| `closed_by` | `varchar(50)` | yes | Free text (e.g. "dan", "hobson", "auto") — audit trail, not auth |
| `reopened_count` | `integer` | no, default 0 | Incremented every reopen; signals a period that's been touched post-close |

**CLI commands:**

```
leobloom fiscal-period close --id <int> [--force] [--note <text>]
leobloom fiscal-period reopen --id <int> --reason <text>
leobloom fiscal-period status [--id <int>]
```

- `close` is idempotent: closing an already-closed period is a no-op that
  returns a clear message, not an error.
- `reopen` requires `--reason`. Increments `reopened_count`. Logs to audit
  table.
- `status` lists all periods with: id, name, start, end, open/closed,
  closed_at, reopened_count.

**Pre-close validation (blocks close unless passed or `--force`):**

1. **Trial balance equilibrium.** `SUM(debits) = SUM(credits)` for all
   non-voided JEs in the period. This should always be true — it's
   enforced at post time — but verify it here as a guardrail.
2. **Balance sheet equation.** Assets = Liabilities + Equity computed
   as of `end_date` using the same logic the net worth report uses.
3. **Data hygiene.** No voided JEs with NULL `void_reason`. No JEs with
   `fiscal_period_id` mismatched to `entry_date` unless
   `adjustment_for_period_id` is set (see §4).
4. **Open obligations cleared.** No obligation instances in the period
   are still `in_flight` or `parked`. (Hobson wants to know about these
   before close; `--force` allows closing with them outstanding if Dan
   decides they roll forward.)

`--force` bypasses validation, requires `--note` explaining why, logs
the bypass.

### 2. Posting enforcement

**Rule:** app-layer validation in the journal posting path (per the
"constraints in app layer" standing order) rejects:

- New JE creation where `fiscal_period_id` references a closed period
- JE line additions or edits to a JE in a closed period
- JE voids on a JE in a closed period

**Error messages must be specific and actionable:**

```
Error: Fiscal period 'March 2026' closed on 2026-04-15.
       To post an adjustment for this period, post to the current open
       period ('April 2026', id 4) with --adjustment-for-period-id 3
       to tag it as a March adjustment.
```

**Void handling is special.** Once a period is closed, you cannot void
a JE inside it — that would mutate closed history. Instead, you post a
**reversing entry** in the current open period. The CLI should reject
the void with guidance:

```
Error: Cannot void JE 472 — it belongs to closed period 'March 2026'.
       Post a reversing entry in the current open period instead. See:
       leobloom ledger post --reverse-of 472
```

This implies a new command or flag: `--reverse-of <je-id>` that builds
a JE swapping debits and credits, with a description
"Reversal of JE {id}: {original description}".

### 3. Explicit fiscal period assignment

The journal posting command currently (presumably) derives
`fiscal_period_id` from `entry_date`. That's right 99.9999% of the
time. We need an escape hatch.

**New optional flag on `leobloom ledger post`:**

```
--fiscal-period-id <int>
```

**Behavior:**

- If omitted: derive from `entry_date` (current behavior, unchanged).
- If provided: validate that the target period is **open** (reject if
  closed, unless `--adjustment-for-period-id` is ALSO set and the
  target period is the current open period — see §4).
- Must be explicit. No auto-routing to "the current open period" — if
  Dan wants that, he passes the flag.

This is the escape hatch for the 0.0001% case. Most posting continues
exactly as it does today.

### 4. Post-close adjustment workflow

**Recommendation: reversing / catch-up entries, not backdated posting.**

**Scenario:** March closed on April 15. On April 20, Dan discovers a
$50 March utility bill he missed.

**Workflow:**

1. Post a new JE with `entry_date = 2026-04-20`, `fiscal_period_id = April`
   (the current open period). This is the **catch-up entry**.
2. Tag it with a new field: `adjustment_for_period_id = 3` (March's id).
3. Description convention: `"[Mar 2026 catch-up] Duke Energy — March
   utility"`.

**Why this over backdating / period override:**

- The ledger's `entry_date` remains truthful: the posting happened on
  April 20. You don't falsify history.
- March's closed books remain closed and unchanged. Re-running the
  March report tomorrow gives the same result as today.
- The adjustment is visible as what it is: an April event about a
  March item, not a stealthy March mutation.
- GAAP-conformant: this is exactly how a real close handles subsequent
  discovery of prior-period items.

**Schema addition to `ledger.journal_entry`:**

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `adjustment_for_period_id` | `integer` | yes | FK to `fiscal_period(id)` ON DELETE RESTRICT. NULL for normal JEs. |

**Materiality:** Hobson does not need the system to enforce a materiality
threshold. Dan judges per adjustment. The system just needs to store
the flag so reports can surface it.

**Edge case: adjustment for an already-reopened period.** If Dan reopens
March, fixes the missed utility directly (no adjustment flag), and
re-closes March — the `reopened_count` signals to future report
readers that March has been touched. The adjustment workflow is the
preferred path; reopen is the escape hatch for truly egregious cases
(audit findings, tax corrections).

### 5. Report disclosure convention

Every historical report Hobson generates must display period
provenance. Convention:

**Header:**

```
Period: March 2026 (2026-03-01 → 2026-03-31)
Status: CLOSED 2026-04-15 14:23:07 UTC by dan | Reopened: 0 times
Report generated: 2026-04-20 09:15:42 UTC
Adjustments from later periods: 2 JEs ($52.17 net impact)
View: As currently stands | [--as-originally-closed for original]
```

For an open period:

```
Period: April 2026 (2026-04-01 → 2026-04-30)
Status: OPEN as of report generation
Report generated: 2026-04-20 09:15:42 UTC
⚠ This period is open and subject to change.
```

**Footer (when adjustments > 0):** the report lists each adjustment JE:
posting date, JE id, description, net $ impact.

**Report modes (new CLI flag on report commands):**

- `--include-adjustments` (default true): the report shows figures as
  they currently stand, including later-posted adjustments tagged for
  this period.
- `--as-originally-closed`: the report shows figures as they were at
  close time. Implementation: filter JEs by
  `created_at <= fiscal_period.closed_at` AND
  `fiscal_period_id = target OR adjustment_for_period_id = target`.
  **This requires that `journal_entry` has a reliable `created_at`
  (or `posted_at`) timestamp** — see open questions.

Both modes are useful. Dan running "show me March as-adjusted" is the
common case. Dan running "show me March as-originally-closed" is for
audit and reconciliation with prior reports.

### 6. Audit trail

New table: `ledger.fiscal_period_audit`

| Column | Type | Notes |
|---|---|---|
| `id` | `serial` | PK |
| `fiscal_period_id` | `integer` | FK to `fiscal_period(id)` |
| `action` | `varchar(20)` | `'closed'` or `'reopened'` |
| `actor` | `varchar(50)` | Free text ("dan", "hobson") |
| `occurred_at` | `timestamptz` | Default `now()` |
| `note` | `text` | Free text — for close, this is `--note`; for reopen, this is `--reason` (required) |

Every close and reopen event writes a row. Read-only from Dan's
perspective; use `leobloom fiscal-period audit --id <int>` to view.

### 7. Migration plan

1. Add `closed_at`, `closed_by`, `reopened_count` columns to
   `ledger.fiscal_period`. Backfill all existing periods:
   `closed_at = NULL`, `closed_by = NULL`, `reopened_count = 0`.
2. Add `adjustment_for_period_id` column to `ledger.journal_entry`.
   Backfill all existing rows to NULL.
3. Create `ledger.fiscal_period_audit` table.
4. Add FK constraint on `adjustment_for_period_id`.

Existing JEs remain valid. No data transformation needed. Dan then
manually closes historical periods (Jan, Feb, Mar 2026) via the CLI
after reviewing each — Hobson runs a trial-balance check on each
before Dan closes.

### 8. Standing orders updated

After this project ships, Hobson's procedures need updates:

- **Saturday routine** gains a "close last month" step at the end of
  each month's first Saturday, after reconciliation.
- **Post-close adjustment procedure** — new procedure doc explaining
  the catch-up entry pattern and when to use it vs reopen.

Hobson handles the procedure doc updates after BD ships the mechanism.

---

## What Hobson does NOT need from BD

- **UI.** CLI only, per `project_leobloom_no_api.md`.
- **Automatic close scheduling.** Dan closes manually as part of the
  Saturday routine. No cron, no background job.
- **Materiality auto-calculation.** Dan judges. System just stores the
  `adjustment_for_period_id` flag.
- **Multi-user approval workflow.** Team of one. No maker-checker.
- **Retained earnings auto-roll.** LeoBloom doesn't have a retained
  earnings automation story today. When it does, this mechanism will
  integrate — but that's a separate project. For now, period close is
  a metadata event, not a P&L rollover.
- **Auth / authorization on the `closed_by` field.** Free text, honor
  system, single-user. Don't build a user table for this.

---

## Acceptance criteria

**Closure mechanics:**

- [ ] `fiscal-period close --id N` sets `closed_at`, `closed_by`, logs to audit table
- [ ] Closing an already-closed period is idempotent and returns a clear message
- [ ] `fiscal-period reopen --id N --reason "..."` requires `--reason`, increments `reopened_count`, logs to audit table
- [ ] `fiscal-period status` lists all periods with open/closed status, close time, reopen count
- [ ] `fiscal-period audit --id N` shows the full close/reopen history for a period

**Pre-close validation:**

- [ ] Close is blocked if trial balance is not in equilibrium
- [ ] Close is blocked if balance sheet equation does not hold at period end
- [ ] Close is blocked if voided JEs exist with NULL `void_reason`
- [ ] Close is blocked if in-flight/parked obligations exist in the period
- [ ] `--force` bypasses all above, requires `--note`, logs the bypass in the audit entry

**Posting enforcement:**

- [ ] New JEs cannot be posted to a closed period (rejected with actionable error)
- [ ] JE line additions / edits are blocked for JEs in a closed period
- [ ] JE voids are blocked for JEs in a closed period (error suggests `--reverse-of`)
- [ ] `--fiscal-period-id` override flag validates target period is open
- [ ] Adjustments (with `--adjustment-for-period-id`) can be posted to the current open period even when the target adjustment period is closed

**Reversing entries:**

- [ ] `leobloom ledger post --reverse-of <je-id>` generates a JE with debits and credits swapped from the original
- [ ] Reversal JE description is auto-populated as "Reversal of JE {id}: {original description}"
- [ ] Reversal JEs post to the current open period

**Post-close adjustments:**

- [ ] `adjustment_for_period_id` column exists and is nullable with FK to fiscal_period
- [ ] Adjustment JEs are tagged correctly and queryable by target period
- [ ] Transaction detail extract (from P080) is extended to optionally include adjustments: `SELECT ... WHERE fiscal_period_id = @fp OR (@include_adjustments AND adjustment_for_period_id = @fp)`

**Report disclosure:**

- [ ] Report header shows period name, date range, status, close timestamp, reopen count, adjustment count
- [ ] Report footer lists adjustment JEs when adjustments > 0
- [ ] `--as-originally-closed` flag on reports filters to pre-close postings only (requires `journal_entry.created_at` or equivalent — see open questions)

**Data integrity:**

- [ ] All existing JEs survive the migration with NULL `adjustment_for_period_id`
- [ ] All existing fiscal periods survive the migration in the OPEN state
- [ ] No existing tests break

---

## Open questions (for BD PO)

1. **Timestamp for "as originally closed" filter.** Does
   `ledger.journal_entry` have a `created_at` or `posted_at` column
   today? If not, we need one, and it needs to be set at post time
   (not at `entry_date`). The migration from 2026-07 shows
   `voided_at timestamptz` and `created_at timestamptz DEFAULT now()`
   — that should be sufficient. Confirm.

2. **Reversing entries vs void.** Today's void workflow presumably
   has a purpose (correcting mistakes before close). Should void
   continue to exist for open periods, and reversals only apply to
   closed periods? My assumption: yes. Open periods use void for
   mistakes, closed periods use reversal for corrections. Confirm
   this split is acceptable.

3. **Obligation instances during close.** If March has in-flight
   obligations when Dan tries to close, the current recommendation
   is to block close. But for irregular cadences (e.g. HOA Lockhart
   — now `irregular`), the obligation may never have an in-flight
   state at all; Hobson spawns and pays it manually. Does the
   validation need to understand cadence type, or does Dan just
   `--force` past it every time and accept the log entry?

4. **Retroactive audit trail.** The audit table will have no rows
   for the periods Dan closes *after* this project ships (Jan, Feb,
   Mar 2026). Is that acceptable (they appear as "closed at migration
   time, no audit history"), or do we need a backfill convention?
   Hobson's recommendation: accept the gap. These are pre-system
   periods.

5. **Interaction with project 080 extracts.** The JE lines extract
   should grow an `--include-adjustments` flag, or become two
   extracts? Hobson's recommendation: one extract with a flag,
   default true. The balance extract should grow a `--closed-only`
   flag for the "as originally closed" view, or does this compose
   differently? Needs design discussion.

---

## Suggested delivery split (BD PO's call)

If BD wants to split this across projects, Hobson's suggested phasing:

1. **Phase 1 — Schema + close/reopen/status CLI.** No posting enforcement
   yet. Hobson can manually close periods for reporting purposes and
   relies on himself not to post to them. Unblocks the "as of" report
   use case.
2. **Phase 2 — Posting enforcement + `--fiscal-period-id` override.**
   System now refuses to post to closed periods. This is the real
   safety net.
3. **Phase 3 — Adjustment workflow + reversing entries + report disclosure.**
   The full GAAP-compliant post-close workflow. Report headers and
   footers updated.
4. **Phase 4 — Audit table + `as-originally-closed` mode.** The "run
   the same March report twice and get the same answer" guarantee.

All four phases are necessary for the full intent. Phase 1 alone gives
Hobson most of the immediate value.
