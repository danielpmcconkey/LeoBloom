# 036 — CLI Framework + Ledger Commands

**Epic:** J — CLI Consumption Layer
**Depends On:** 005, 006
**Architecture:** [ADR-003 — CLI Architecture](../../Documentation/ADR/ADR-003-cli-architecture.md)
**Status:** Done

---

Establish the CLI entry point, argument parsing, output formatting, and exit
code conventions. Implement the first command group (ledger) as proof of the
framework.

**Framework scope:**

- Entry point: `leobloom <group> <command> [args] [flags]`
- Human-readable output by default, `--json` flag for machine-parseable output
- Exit codes: 0 = success, 1 = validation/business error, 2 = system error
- Errors to stderr, data to stdout
- No subcommand abbreviation
- No interactive prompts (unattended cron bots call these)
- Idempotent where possible

**Commands:**

```
leobloom ledger post --debit <acct:amount> --credit <acct:amount> --date DATE --description TEXT
leobloom ledger void <entry-id> --reason TEXT
leobloom ledger show <entry-id>
```

**Consumers:** Dan (manual adjustments), COYS invoice agent (after confirmation).

**Why ledger first:** Every other command group depends on the framework this
project establishes. Ledger commands are thin wrappers around existing posting
and void services (P005, P006), making them the simplest proof of the framework.

**Design constraints (apply to all CLI projects):**

- No subcommand abbreviation
- Exit codes: 0 = success, 1 = validation/business error, 2 = system error
- Errors to stderr, data to stdout
- No interactive prompts
- Idempotent where possible
- Human-readable default, `--json` for machine-parseable
