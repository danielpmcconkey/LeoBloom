# 084 — Report Period Disclosure & As-Originally-Closed Mode

**Epic:** K — Fiscal Period Closure
**Depends On:** 083 (adjustment_for_period_id must exist)
**Status:** Not started

---

Add period provenance headers and adjustment disclosure to all period-based
reports. Add `--as-originally-closed` mode for historical reproducibility.

**Origin:** Hobson brief `fiscal-period-closure.md` §5.

## Report header additions

Every period-based report (income statement, balance sheet, P&L by subtree,
trial balance, reporting data extracts) gains header metadata:

```
Period: March 2026 (2026-03-01 → 2026-03-31)
Status: CLOSED 2026-04-15 14:23:07 UTC by dan | Reopened: 0 times
Report generated: 2026-04-20 09:15:42 UTC
Adjustments from later periods: 2 JEs ($52.17 net impact)
```

For open periods:
```
Period: April 2026 (2026-04-01 → 2026-04-30)
Status: OPEN as of report generation
Report generated: 2026-04-20 09:15:42 UTC
```

## Adjustment footer

When a closed period has adjustment JEs tagged with
`adjustment_for_period_id = period.id`, the report footer lists each:

```
Post-close adjustments applied to this period:
  JE 523  2026-04-20  [Mar 2026 catch-up] Duke Energy — March utility  -$47.82
  JE 531  2026-04-22  [Mar 2026 catch-up] Late vendor credit            +$4.35
  Net adjustment impact: -$43.47
```

## `--as-originally-closed` mode

New CLI flag on period-based report commands. Filters JEs to show the period
as it stood at close time:

**Include:** JEs where:
- `fiscal_period_id = @target_period` AND `created_at <= fiscal_period.closed_at`
- OR `adjustment_for_period_id = @target_period` AND `created_at <= fiscal_period.closed_at`

**Exclude:** JEs posted after the period was closed (whether direct or adjustment).

This relies on `journal_entry.created_at` (already exists) and
`fiscal_period.closed_at` (added by P081).

**Edge case:** If the period has been reopened and re-closed, `closed_at`
reflects the most recent close. The `--as-originally-closed` flag uses the
*current* `closed_at` value. For "as of the first close" semantics, the user
would need to check the audit trail (P081) for the original close timestamp
and use a hypothetical `--as-of-timestamp` flag — but that's out of scope.
Document this limitation.

## Interaction with P080 extracts

The JE lines extract (`leobloom extract je-lines`) gains:
- `--include-adjustments` flag (default true) — when targeting a specific
  period, also includes JEs where `adjustment_for_period_id = @period`
- Period metadata in the extract header/JSON envelope

## Acceptance criteria

- [ ] Income statement, balance sheet, P&L by subtree, trial balance all show period provenance header
- [ ] Header includes period name, date range, open/closed status, close timestamp, reopened count
- [ ] Header includes adjustment count and net impact when adjustments exist
- [ ] Open period header shows warning that period is subject to change
- [ ] Footer lists individual adjustment JEs with date, description, and amount
- [ ] `--as-originally-closed` flag filters to pre-close JEs only
- [ ] `--as-originally-closed` on an open period is rejected with clear error
- [ ] Extract commands include adjustment JEs when `--include-adjustments` is set
- [ ] JSON output includes period metadata envelope
- [ ] Human-readable output formats headers/footers cleanly

## Out of scope

- `--as-of-timestamp` for arbitrary historical snapshots
- Automatic comparison between as-adjusted and as-originally-closed views
- PDF/export formatting of disclosure headers
