# 041 — CLI Account + Period Commands

**Epic:** J — CLI Consumption Layer
**Depends On:** 036, 007, 009
**Architecture:** [ADR-003 — CLI Architecture](../../Documentation/ADR/ADR-003-cli-architecture.md)
**Status:** Done

---

Read-only account queries and fiscal period management.

**Account commands (read-only):**

```
leobloom account list [--type TYPE] [--inactive]
leobloom account show <id-or-code>
leobloom account balance <id-or-code> [--as-of DATE]
```

Account creation/deactivation happens through migrations, not CLI.

**Period commands:**

```
leobloom period list
leobloom period close <id-or-key>
leobloom period reopen <id-or-key> --reason TEXT
leobloom period create --start DATE --end DATE --key TEXT
```

**Consumers:** Dan (COA reference, month-end close), agents (account lookups
for validation).
