# 038 — CLI Obligation Commands

**Epic:** J — CLI Consumption Layer
**Depends On:** 036, 014, 015, 016, 017, 018
**Architecture:** [ADR-003 — CLI Architecture](../../Documentation/ADR/ADR-003-cli-architecture.md)
**Status:** Not started

---

Wrap existing obligation services for CLI access. Add one new query (`upcoming`),
one new filter (`--agreement` on instance list). Everything else wraps existing
service methods following the parse-call-format-exit pattern.

**Commands:**

```
leobloom obligation agreement list [--type receivable|payable] [--cadence CADENCE] [--inactive] [--json]
leobloom obligation agreement show <id> [--json]
leobloom obligation agreement create <args> [--json]
leobloom obligation agreement update <id> <args> [--json]
leobloom obligation agreement deactivate <id> [--json]

leobloom obligation instance list [--status STATUS] [--due-before DATE] [--due-after DATE] [--agreement ID] [--json]
leobloom obligation instance spawn <agreement-id> --from DATE --to DATE [--json]
leobloom obligation instance transition <instance-id> --to STATUS [--amount AMT] [--date DATE] [--notes TEXT] [--journal-entry-id ID] [--json]
leobloom obligation instance post <instance-id> [--json]

leobloom obligation overdue [--as-of DATE] [--json]
leobloom obligation upcoming [--days N] [--json]
```

## New backend work

### `upcoming` query

Returns instances with `expected_date` between today and today+N days where
status is **not terminal** — i.e., status NOT IN (Posted, Skipped). This means
Expected, InFlight, Overdue, and Confirmed (unposted) all appear.

Rationale: a controller checking "what's coming up" needs to see everything
that hasn't been resolved yet in the window, including items past their expected
date that haven't been posted or skipped. The overdue command covers long-tail
past-due discovery; upcoming covers the near-term operational view.

```sql
SELECT ... FROM ops.obligation_instance
WHERE is_active = true
  AND status NOT IN ('posted', 'skipped')
  AND expected_date >= @today
  AND expected_date <= @horizon
ORDER BY expected_date
```

Default N = 30 days.

### `instance list` — `--agreement` filter

Add optional `agreementId: int option` to `ListInstancesFilter`. When provided,
adds `AND obligation_agreement_id = @agreement_id` to the WHERE clause.

This is the right abstraction because accounts live on the agreement, not the
instance. If the user wants "instances for my mortgage," they filter by the
mortgage agreement ID, not the account.

## Acceptance criteria

### Agreement commands

- [ ] `agreement list` returns all active agreements (tabular or JSON)
- [ ] `agreement list --type receivable` filters by obligation direction
- [ ] `agreement list --cadence monthly` filters by cadence
- [ ] `agreement list --inactive` includes inactive agreements
- [ ] `agreement show <id>` returns agreement detail
- [ ] `agreement show` with nonexistent ID returns error with exit code 1
- [ ] `agreement create` with valid args creates agreement and returns it
- [ ] `agreement create` with invalid data returns validation errors (missing name, amount <= 0, source = dest account, nonexistent account, etc.)
- [ ] `agreement update <id>` updates agreement and returns it
- [ ] `agreement deactivate <id>` deactivates agreement
- [ ] `agreement deactivate` with active instances returns error listing the blocking instances

### Instance commands

- [ ] `instance list` returns instances (tabular or JSON)
- [ ] `instance list --status expected` filters by status
- [ ] `instance list --due-before DATE` filters by expected_date
- [ ] `instance list --due-after DATE` filters by expected_date
- [ ] `instance list --agreement ID` filters by agreement
- [ ] `instance list` filters compose (e.g., `--status expected --agreement 5 --due-before 2026-05-01`)
- [ ] `instance spawn <agreement-id> --from DATE --to DATE` spawns instances and returns spawn result (created + skipped counts)
- [ ] `instance transition <id> --to confirmed --amount 500` succeeds
- [ ] `instance transition` with invalid state transition returns error naming the current and target status
- [ ] `instance transition --to confirmed` without `--amount` returns error explaining amount is required for confirmation
- [ ] `instance transition --to skipped` without `--notes` returns error explaining notes are required for skipped
- [ ] `instance post <id>` posts confirmed instance to ledger and returns journal entry ID
- [ ] `instance post` on non-Confirmed instance returns error stating only Confirmed instances can be posted
- [ ] `instance post` when agreement is missing source or dest account returns error
- [ ] `instance post` when fiscal period doesn't exist or is closed returns error

### Top-level commands

- [ ] `overdue` runs overdue detection for today, returns transition count + any errors
- [ ] `overdue --as-of DATE` runs overdue detection for given date
- [ ] `upcoming` returns non-terminal instances due within 30 days
- [ ] `upcoming --days 7` returns non-terminal instances due within 7 days

### Cross-cutting

- [ ] All commands support `--json` flag for JSON output
- [ ] Exit codes follow `ExitCodes` convention (0 success, 1 business error, 2 system error)
- [ ] No business logic in CLI layer — all logic delegated to services
- [ ] Error messages from services are surfaced verbatim to the user (no swallowing)

## Consumers

- **COYS agents** (overdue, upcoming, spawn, transition, post)
- **Dan** (ad hoc status checks, agreement management)
