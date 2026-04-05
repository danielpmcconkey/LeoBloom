# 038 — CLI Obligation Commands

**Epic:** J — CLI Consumption Layer
**Depends On:** 036, 014, 015, 016, 017, 018
**Architecture:** [ADR-003 — CLI Architecture](../../Documentation/ADR/ADR-003-cli-architecture.md)
**Status:** Not started

---

Wrap existing obligation services for CLI access. Add one new query (`upcoming`).

**Commands:**

```
leobloom obligation agreement list [--type receivable|payable] [--cadence CADENCE] [--inactive]
leobloom obligation agreement show <id>
leobloom obligation agreement create <args>
leobloom obligation agreement update <id> <args>
leobloom obligation agreement deactivate <id>

leobloom obligation instance list [--status STATUS] [--due-before DATE] [--due-after DATE]
leobloom obligation instance spawn <agreement-id> --from DATE --to DATE
leobloom obligation instance transition <instance-id> --to STATUS [--amount AMT] [--date DATE] [--notes TEXT] [--journal-entry-id ID]
leobloom obligation instance post <instance-id>

leobloom obligation overdue [--as-of DATE]
leobloom obligation upcoming [--days N]
```

**New query — `upcoming`:** Returns instances in expected/in_flight status with
expected_date within the next N days. The nagging agent's primary query. This
requires a new service method (not just a CLI wrapper).

**Consumers:** COYS agents (overdue, upcoming, spawn, transition, post),
Dan (ad hoc status checks).
