# 037 — CLI Reporting Commands (Accounting)

**Epic:** J — CLI Consumption Layer
**Depends On:** 036, 008, 011, 012, 013, 007
**Architecture:** [ADR-003 — CLI Architecture](../../Documentation/ADR/ADR-003-cli-architecture.md)
**Status:** Done

---

Thin wrappers around existing accounting report services. No new report logic.

**Commands:**

```
leobloom report trial-balance --period <id-or-key>
leobloom report balance-sheet --as-of DATE
leobloom report income-statement --period <id-or-key>
leobloom report pnl-subtree --account ACCT --period <id-or-key>
leobloom report account-balance --account ACCT [--as-of DATE]
```

**Consumers:** Dan (month-end review), nagging agent (balance checks).

**Notes:**

- Each command maps directly to an existing service method.
- Human-readable tabular output by default, `--json` for agents.
- Report formatting (column widths, alignment, totals) lives in the CLI
  layer, not in the domain services.
- `pnl-subtree` uses `--period` (not `--from/--to`) — matches the service API.

**Completed:** 2026-04-06 | Commit: 6dbfeb5
