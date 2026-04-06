# 042 — CLI Invoice Commands

**Epic:** J — CLI Consumption Layer
**Depends On:** 036, 021
**Architecture:** [ADR-003 — CLI Architecture](../../Documentation/ADR/ADR-003-cli-architecture.md)
**Status:** Done

---

Thin wrapper around P021's invoice record persistence service.

**Commands:**

```
leobloom invoice record --tenant TEXT --period <id-or-key> --rent-amount AMT --utility-share AMT --total-amount AMT [--document-path PATH] [--notes TEXT]
leobloom invoice list [--tenant TEXT] [--period <id-or-key>]
leobloom invoice show <id>
```

**Consumers:** COYS bot (records the invoice after it does the calculation and
PDF generation), Dan (ad hoc queries).

**Notes:**

- All calculation, PDF generation, and delivery logic lives in the COYS bot.
- LeoBloom just persists the record and makes it queryable.
- `--json` output for the bot, human-readable for Dan.
