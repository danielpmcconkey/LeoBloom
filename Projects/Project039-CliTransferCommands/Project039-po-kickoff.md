# Project 039 — CLI Transfer Commands: PO Kickoff

**Backlog Item:** 039-cli-transfer-commands.md
**Epic:** J — CLI Consumption Layer
**Dependencies:** P036 (CLI framework — done), P019 (transfer domain — done)
**Architecture:** ADR-003 — CLI Architecture (parse, call, format, exit code)
**Date:** 2026-04-06

---

## Objective

Expose transfer operations through the CLI so Dan can initiate and confirm
inter-account transfers, and query transfer status, without touching the
database directly.

## Scope

### In Scope

**CLI layer (thin wrappers):**

1. `leobloom transfer initiate` — parse args into `InitiateTransferCommand`,
   call `TransferService.initiate`, format result
2. `leobloom transfer confirm <id>` — parse args into `ConfirmTransferCommand`,
   call `TransferService.confirm`, format result
3. `leobloom transfer list [--status STATUS] [--from DATE] [--to DATE]` —
   parse filter args, call a new `TransferService.list` function, format result
4. `leobloom transfer show <id>` — parse id, call a new
   `TransferService.show` function, format result
5. `--json` flag support on all four subcommands
6. Wire `Transfer` subcommand into `Program.fs` top-level dispatch

**Service + repository additions (required by list/show):**

7. `TransferRepository.list` — filtered query by status, date range (active
   records only). Follows the `InvoiceRepository.list` pattern.
8. `TransferService.show` — wraps `TransferRepository.findById` with
   connection management and `Result` return. Follows `InvoiceService.showInvoice`.
9. `TransferService.list` — wraps `TransferRepository.list` with connection
   management. Follows `InvoiceService.listInvoices`.
10. `ListTransfersFilter` type — status, fromDate, toDate. Lives in
    `TransferRepository.fs` or domain types, following the
    `ListInvoicesFilter` pattern.

**Output formatting:**

11. `formatTransfer` (human-readable single transfer) in `OutputFormatter.fs`
12. `formatTransferList` (human-readable table) in `OutputFormatter.fs`
13. `writeTransferList` (dedicated write function to avoid type erasure,
    same pattern as `writeInvoiceList`)
14. Wire `Transfer` into `formatHuman` dispatch

### Out of Scope

- Transfer business logic changes (initiate/confirm already work)
- Voiding or cancelling transfers (not in backlog item)
- Obligation CLI commands (P037/P038)
- Any new validation rules

## Acceptance Criteria

### Behavioral (Gherkin scenarios)

| # | Criterion |
|---|-----------|
| B1 | `transfer initiate` with valid args returns exit 0 and prints the new transfer |
| B2 | `transfer initiate` with missing required args returns exit 2 (Argu parse error) |
| B3 | `transfer confirm <id> --date DATE` with valid args returns exit 0 and prints the confirmed transfer |
| B4 | `transfer confirm` with invalid/missing args returns appropriate error + nonzero exit |
| B5 | `transfer list` with no filters returns all active transfers, exit 0 |
| B6 | `transfer list --status initiated` filters correctly |
| B7 | `transfer list --from DATE --to DATE` filters by initiated date range |
| B8 | `transfer show <id>` for existing transfer returns exit 0 and prints detail |
| B9 | `transfer show <id>` for nonexistent transfer returns exit 1 with error |
| B10 | All four subcommands support `--json` flag producing valid JSON output |
| B11 | `transfer` with no subcommand prints usage to stderr and returns exit 2 |

### Structural (verified by QE/Governor, not Gherkin)

| # | Criterion |
|---|-----------|
| S1 | `TransferCommands.fs` exists in `Src/LeoBloom.CLI/` and is listed in the fsproj |
| S2 | `TransferCommands.dispatch` is wired into `Program.fs` top-level match |
| S3 | `TransferRepository.list` function exists and filters by status + date range |
| S4 | `TransferService.show` and `TransferService.list` functions exist |
| S5 | `ListTransfersFilter` type exists |
| S6 | `OutputFormatter.fs` has `formatTransfer`, `formatTransferList`, `writeTransferList` |
| S7 | `formatHuman` dispatches on `Transfer` type |
| S8 | All existing tests pass (no regressions) |
| S9 | Project builds with no warnings |

## Notes for Planner

- **Pattern reference:** P042 (InvoiceCommands) is the exact template. Follow
  it for Argu DU structure, dispatch pattern, output formatting, and the
  service/repo list+show additions.
- **The repo/service gap is real.** TransferRepository only has `insert`,
  `findById`, and `updateConfirm`. The list query and the service-level
  show/list wrappers need to be added. This is not just a CLI wrapper project.
- **Date filter semantics:** The `--from` and `--to` flags should filter on
  `initiated_date` (the transfer's creation date), not `confirmed_date`. This
  matches the user mental model of "when did I start this transfer."
- **ADR-003 applies:** No business logic in the CLI layer. The CLI parses,
  calls services, formats output, and maps exit codes. Period.
- **Gherkin scope:** Per ADR-003, CLI specs test argument parsing, output
  formatting, error surfacing, and exit codes. They do NOT re-test transfer
  business rules (validation, journal entry posting, etc.) which are already
  covered by TransferTests.fs.

## Verdict

No brainstorm needed. This is mechanical work with a clear pattern to follow.
Ready for planner.
