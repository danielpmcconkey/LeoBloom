# CLI Vision — Replacing the API Layer

**From:** Hobson (PO)
**Date:** 2026-04-05

---

## Context

Projects 023–027 (REST endpoints) are cancelled. LeoBloom's consumption
layer is a CLI that COYS agents and Dan invoke directly. No web
framework, no serialization contracts, no auth. The F# codebase stays a
library with CLI tooling on top.

This document replaces the cancelled API backlog items with CLI
equivalents and adds tax-reporting capabilities that weren't in the
original scope.

---

## Consumers

The CLI has four consumers. Every command should be designed with all
four in mind.

| Consumer | How they call it | What they need |
|---|---|---|
| **Dan** | Terminal, ad hoc | Human-readable tables, quick lookups |
| **Nagging agent** | COYS cron bot, subprocess | Machine-parseable output (--json), overdue/upcoming queries |
| **Invoice agent** | COYS cron bot, subprocess | Spawn, generate, status transitions |
| **CPA** | Dan runs it, hands her the output | Tax-year reports, Schedule E worksheet, transaction detail |

**Output convention:** Human-readable by default. `--json` flag for
machine-parseable output. One flag, not two CLIs.

---

## Command Groups

### Ledger

Core journal entry operations.

```
leobloom ledger post --debit <acct:amount> --credit <acct:amount> --date DATE --description TEXT
leobloom ledger void <entry-id> --reason TEXT
leobloom ledger show <entry-id>
```

**Consumer:** Dan (manual adjustments), invoice agent (after confirmation).

---

### Reports — Accounting

Standard financial reports. These already exist as services — the CLI is
a thin wrapper.

```
leobloom report trial-balance --period <id-or-key>
leobloom report balance-sheet --as-of DATE
leobloom report income-statement --period <id-or-key>
leobloom report pnl-subtree --account ACCT --from DATE --to DATE
leobloom report account-balance --account ACCT [--as-of DATE]
```

**Consumer:** Dan (month-end review), nagging agent (balance checks).

---

### Reports — Tax

These are new. They exist to hand the CPA a complete filing package.

```
leobloom report schedule-e --year YYYY
leobloom report general-ledger --account ACCT --from DATE --to DATE
leobloom report cash-receipts --from DATE --to DATE
leobloom report cash-disbursements --from DATE --to DATE
```

**schedule-e** rolls up the chart of accounts into IRS Schedule E line
items: rental income, mortgage interest, property tax, insurance, HOA,
utilities, repairs, depreciation. The COA must be structured to support
this mapping (it likely already is — verify against the seed data).

**general-ledger** is transaction-level detail for a single account.
The CPA asks "what's in this $847 utilities number?" and Dan hands her
every entry.

**cash-receipts / cash-disbursements** are journals showing all money
in and all money out, with counterparty and date. Supporting detail for
the income and expense totals.

**Consumer:** CPA (via Dan). These don't need --json.

**Open question — depreciation:** The property has a 27.5-year
straight-line depreciation schedule. Either LeoBloom tracks the cost
basis and computes annual depreciation (fixed asset module), or Dan
hands the CPA that number separately and schedule-e pulls it from a
config. Recommend the latter for now — a full fixed asset module is
overkill for one property. Revisit if Dan acquires more properties.

---

### Fiscal Periods

```
leobloom period list
leobloom period close <id-or-key>
leobloom period reopen <id-or-key> --reason TEXT
leobloom period create --start DATE --end DATE --key TEXT
```

**Consumer:** Dan (month-end close).

---

### Obligations

The operational backbone. Most commands here are what the nagging and
invoice agents call.

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

**upcoming** is new — returns instances in expected/in_flight status
with expected_date within the next N days. The nagging agent's primary
query.

**Consumer:** Nagging agent (overdue, upcoming), invoice agent (spawn,
transition, post), Dan (ad hoc status checks).

---

### Transfers

```
leobloom transfer initiate --from-account ACCT --to-account ACCT --amount AMT --date DATE --description TEXT
leobloom transfer confirm <id> --date DATE
leobloom transfer list [--status STATUS] [--from DATE] [--to DATE]
leobloom transfer show <id>
```

**Consumer:** Dan (recording inter-account transfers).

---

### Account Management

Read-only for now. Account creation/deactivation happens through
migrations.

```
leobloom account list [--type TYPE] [--inactive]
leobloom account show <id-or-code>
leobloom account balance <id-or-code> [--as-of DATE]
```

**Consumer:** Dan (COA reference), agents (account lookups for
validation).

---

## Backlog Replacement

The cancelled API projects map to CLI work as follows:

| Cancelled | Replacement CLI scope |
|---|---|
| 023 — Journal entry endpoints | `leobloom ledger` commands |
| 024 — Reporting endpoints | `leobloom report` commands (accounting + tax) |
| 025 — Obligation endpoints | `leobloom obligation` commands |
| 026 — Transfer & invoice endpoints | `leobloom transfer` commands + invoice work (020/021) |
| 027 — Projection endpoint | `leobloom report balance-projection` (future, ties to 022) |

### Suggested new backlog items

| # | Project | Notes |
|---|---|---|
| 034 | CLI framework + ledger commands | Entry point, argument parsing, output formatting, --json flag. Start with `ledger post`, `ledger void`, `ledger show`. |
| 035 | CLI reporting commands | Wrap existing report services. Trial balance, balance sheet, income statement, P&L subtree, account balance. |
| 036 | CLI obligation commands | Wrap existing obligation services. Agreement CRUD, instance lifecycle, overdue, upcoming (new query). |
| 037 | CLI transfer commands | Wrap existing transfer services. |
| 038 | CLI tax reports | Schedule E, general ledger detail, cash receipts, cash disbursements. New report logic. |
| 039 | CLI account + period commands | Read-only account queries, fiscal period management. |

### Sequencing

1. **Remediation first** (see `remediation-2026-04-05.md`). Critical
   items C1–C3, then significant items S1–S7. This shores up the
   foundation before we build the consumption layer on top.
2. **034 (CLI framework)** — establishes the entry point, arg parsing,
   output conventions. Everything else depends on this.
3. **035, 036, 037, 039** in any order — these are thin wrappers around
   existing services.
4. **020, 021 (invoices)** — feature work that was already on the
   backlog.
5. **038 (tax reports)** — new report logic. Target completion well
   before 2027 tax season.
6. **022 (balance projection)** — lowest priority, slot wherever.

---

## Design Constraints

- **No subcommand abbreviation.** `leobloom obligation instance list`,
  not `leobloom o i l`. Agents call full commands; Dan can alias if he
  wants.
- **Exit codes matter.** 0 = success, 1 = validation/business error,
  2 = system error. Agents check exit codes, not output parsing.
- **Errors to stderr, data to stdout.** Agents capture stdout for
  results and check stderr + exit code for failures.
- **No interactive prompts.** Everything is flags and arguments. The
  CLI is called by unattended cron bots.
- **Idempotent where possible.** Spawning instances for an already-
  covered range is a no-op. Closing an already-closed period succeeds.
  Agents shouldn't need to check state before acting.
