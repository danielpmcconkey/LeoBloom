# 039 — CLI Transfer Commands

**Epic:** J — CLI Consumption Layer
**Depends On:** 036, 019
**Architecture:** [ADR-003 — CLI Architecture](../../Documentation/ADR/ADR-003-cli-architecture.md)
**Status:** Not started

---

Wrap existing transfer services for CLI access.

**Commands:**

```
leobloom transfer initiate --from-account ACCT --to-account ACCT --amount AMT --date DATE --description TEXT
leobloom transfer confirm <id> --date DATE
leobloom transfer list [--status STATUS] [--from DATE] [--to DATE]
leobloom transfer show <id>
```

**Consumers:** Dan (recording inter-account transfers).
