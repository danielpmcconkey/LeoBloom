# Project 037 — CLI Reporting Commands (Accounting)

**PO Brief**
**Date:** 2026-04-06
**Backlog Item:** Projects/BACKLOG/037-cli-reporting-commands.md
**Epic:** J — CLI Consumption Layer
**Status:** KICKOFF

---

## Objective

Add 5 accounting report commands to the existing `leobloom report`
subcommand group. These are thin CLI wrappers around existing Ledger
services. No new domain logic, no new report calculations. Same
mechanical pattern as the commands already in ReportCommands.fs
(Schedule E, General Ledger, Cash Receipts, Cash Disbursements) and
the patterns established by P039 (Transfers) and P042 (Invoices).

## What's In Scope

### 5 New Report Commands

| Command | Backing Service | Key Args |
|---|---|---|
| `report trial-balance` | `TrialBalanceService.getByPeriodId` / `getByPeriodKey` | `--period <id-or-key>` |
| `report balance-sheet` | `BalanceSheetService.getAsOfDate` | `--as-of DATE` |
| `report income-statement` | `IncomeStatementService.getByPeriodId` / `getByPeriodKey` | `--period <id-or-key>` |
| `report pnl-subtree` | `SubtreePLService.getByAccountCodeAndPeriodId` / `...PeriodKey` | `--account ACCT --period <id-or-key>` |
| `report account-balance` | `AccountBalanceService.getBalanceByCode` | `--account ACCT [--as-of DATE]` |

### Per-Command Deliverables

For each of the 5 commands:

1. **Argu DU definition** in ReportCommands.fs (argument parsing)
2. **Handler function** that calls the existing service and routes to output
3. **Dispatch case** wired into the existing `ReportArgs` DU and `dispatch` function
4. **Human-readable formatter** in OutputFormatter.fs for tabular output
5. **`--json` flag** support (the existing report commands use `writeHuman`
   which lacks `--json`; new commands should use the `write` function that
   supports both modes)

### Other In-Scope Work

- Wire the new Argu cases into the `ReportArgs` DU in ReportCommands.fs
- Add `formatHuman` match cases in OutputFormatter.fs for each new report type
- Date parsing reuse (the `parseDate` helper already exists in ReportCommands.fs)

## What's Explicitly Out of Scope

- **No new domain logic.** The services exist, the report types exist. If a
  service doesn't do what the CLI needs, that's a different project.
- **No new domain services.** No new repositories, no new SQL.
- **No changes to existing service behavior.** The services return what they
  return. The CLI formats it.
- **No modifications to the existing 4 report commands** (Schedule E, General
  Ledger, Cash Receipts, Cash Disbursements) unless needed for shared
  infrastructure (e.g., wiring the dispatch).
- **No interactive prompts.** Per ADR-003.
- **No REST API.** Per CLI direction decision.

## PO Flag: pnl-subtree Argument Mismatch

The backlog specifies:
```
leobloom report pnl-subtree --account ACCT --from DATE --to DATE
```

But `SubtreePLService` only supports period-based lookups (`getByAccountCodeAndPeriodId`
/ `getByAccountCodeAndPeriodKey`), not date ranges. There is no date-range
variant of this service.

**Decision required from the Planner:** The CLI should use `--period <id-or-key>`
instead of `--from DATE --to DATE`, consistent with how trial-balance and
income-statement work. The backlog description was aspirational; the service
API is the constraint. If a date-range P&L subtree is genuinely needed, that's
a new service method and belongs in a separate project.

## Acceptance Criteria

### Behavioral (Gherkin scenarios)

Per ADR-003, CLI Gherkin specs cover CLI behavior, not service logic. Each
command needs scenarios for:

1. **Happy path with human output** -- command runs, tabular output to stdout,
   exit 0
2. **Happy path with --json** -- command runs, JSON output to stdout, exit 0
3. **Missing required args** -- Argu prints usage to stderr, exit 2
4. **Invalid argument values** (bad dates, nonexistent periods/accounts) --
   error message to stderr, exit 1

That's roughly 4 scenarios per command, ~20 scenarios total. The Gherkin
writer may consolidate where the pattern is identical.

### Structural (Builder/QE verification, not Gherkin)

5. Each command's Argu DU is defined and wired into the dispatch
6. Each report type has a `formatHuman` match case in OutputFormatter.fs
7. `--json` serialization produces valid JSON for each report type
8. `account-balance --as-of` defaults to today when omitted
9. `trial-balance --period` and `income-statement --period` accept both
   integer IDs and string period keys (e.g., "2026-01" or "7")
10. All 5 commands appear in `leobloom report --help` output
11. Solution builds with zero warnings
12. Existing tests pass (no regressions)

## GAAP Considerations for Report Formatting

These are formatting rules the Builder and Planner need to respect when
implementing human-readable output. They don't change domain logic (the
services already compute correctly), but the CLI presentation must not
misrepresent the numbers.

### Trial Balance
- Debit and credit columns must be separate (not a single net column).
  A trial balance that nets debits and credits is not a trial balance.
- Grand totals must show total debits and total credits independently.
- Must display whether the trial balance is balanced (debits = credits).
  An out-of-balance trial balance is a red flag, not something to hide.

### Balance Sheet
- Must show the accounting equation structure: Assets = Liabilities + Equity.
- Retained earnings must appear as a separate line item within Equity, not
  lumped into the equity account balances (the service already computes it
  separately).
- Must display whether the balance sheet balances. Same rationale as TB.

### Income Statement
- Revenue and expenses must be in separate sections with section totals.
- Net income = Revenue - Expenses, displayed at the bottom.
- Amounts follow normal balance sign convention (revenue positive,
  expenses positive, net income can be negative for a loss).

### P&L Subtree
- Same formatting rules as income statement, scoped to the subtree root.
- Must display the root account code and name so the user knows what
  subtree they're looking at.

### Account Balance
- Must display the account's normal balance type (debit or credit) so the
  sign of the balance is interpretable.
- A negative balance on a debit-normal account means credit > debit (unusual
  but legitimate -- e.g., bank overdraft). Don't suppress or abs() it.

## Dependencies

- **P036** (CLI Foundation) -- already done, provides the CLI infrastructure
- **P008** (Trial Balance Service) -- already done
- **P011** (Balance Sheet Service) -- already done
- **P012** (Income Statement Service) -- already done
- **P013** (P&L Subtree Service) -- already done
- **P007** (Account Balance Service) -- already done
- **ADR-003** (CLI Architecture) -- governs the thin-wrapper pattern

All dependencies are met. This project is ready to build.

## Verdict

**APPROVED for planning.** This is a straightforward mechanical project.
No brainstorm needed -- the backlog item is unambiguous, the services exist,
and the CLI pattern is established. Skip straight to planning.

The one thing the Planner must address is the pnl-subtree argument mismatch
flagged above.
