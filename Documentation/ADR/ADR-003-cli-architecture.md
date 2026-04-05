# ADR-003 — CLI Architecture

**Date:** 2026-04-05
**Status:** Accepted
**Scope:** P036–P042 (Epic J — CLI Consumption Layer)

## Context

LeoBloom's consumption layer is a CLI, not a REST API (P023–P027
cancelled). The CLI serves two consumers: Dan (manual adjustments) and
automated agents (COYS invoice agent, cron bots). This means no
interactive prompts, strict exit codes, and machine-parseable output
via `--json`.

## Decision

### The CLI layer is parse → call → format → exit code

CLI commands do not contain business logic. Each command:

1. Parses arguments into an existing command/query type
2. Calls the appropriate service function
3. Formats the `Result` for output (human-readable or `--json`)
4. Maps `Ok` → exit 0, `Error` → exit 1, system failure → exit 2

### Services already own orchestration

The service layer (JournalEntryService, FiscalPeriodService, etc.)
already manages connections, transactions, validation, and error
handling. Services return `Result<T, string list>` types that map
directly to CLI output. The CLI layer must not duplicate or re-implement
any of this.

### Cross-service orchestration belongs in services, not CLI

If a CLI command requires multiple service calls as a single logical
operation, that orchestration must be pushed down into a service (or a
new coordinating service). The CLI layer never opens connections or
manages transactions.

### Gherkin specs test CLI behavior, not service logic

Service logic already has 400+ tests. CLI Gherkin scenarios cover:

- Argument parsing (valid, invalid, missing required args)
- Output formatting (human-readable and `--json`)
- Error mapping (validation errors → stderr + exit 1)
- Exit code correctness
- Flag behavior (`--json`, command-specific flags)

CLI specs must NOT re-test business rules that are already covered by
service-level tests. If a service rejects an invalid fiscal period,
the CLI test verifies the error surfaces correctly — it does not
re-test the fiscal period validation logic.

## Consequences

- CLI projects (P037–P042) are thin. Most are a single file per command
  group.
- New business logic triggered by CLI requirements (e.g., cross-service
  orchestration for P040 tax reports) gets its own service with its own
  test coverage, independent of the CLI layer.
- The Gherkin writer and QE must be explicitly told to scope specs to
  CLI behavior. Without this guidance they will default to full
  behavioral coverage, which duplicates existing service tests.
