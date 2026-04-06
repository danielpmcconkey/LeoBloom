# P042 -- CLI Invoice Commands

**Status:** In Progress
**Depends on:** P021 (Invoice Persistence -- Done), P036 (CLI Framework -- Done)
**Triggered by:** CLI sequencing plan (backlog items 036-042)

## Objective

Expose InvoiceService's three methods -- recordInvoice, showInvoice,
listInvoices -- as CLI subcommands under a new `invoice` command group.

This is a thin CLI wrapper. All domain logic, validation, and persistence
already exist in LeoBloom.Ops.InvoiceService (shipped in P021 / PR #32).
The job is argument parsing, service dispatch, output formatting, and exit
codes.

## What Ships

1. **InvoiceCommands.fs** -- Argu DU definitions + dispatch for:
   - `invoice record` -- record a new invoice
   - `invoice show <id>` -- show an invoice by ID
   - `invoice list` -- list invoices with optional filters

2. **Program.fs update** -- add `Invoice` case to the top-level `LeoBloomArgs`
   DU and wire dispatch.

3. **OutputFormatter.fs update** -- add human-readable formatting for
   `Invoice` and `Invoice list` types.

4. **LeoBloom.CLI.fsproj update** -- add `LeoBloom.Ops` project reference
   and include InvoiceCommands.fs in the compile order.

5. **Gherkin specs** -- behavioral scenarios in `Specs/CLI/InvoiceCommands.feature`.

6. **Test implementations** -- in `Src/LeoBloom.Tests/`.

## CLI Interface Design

Follows the exact pattern established by LedgerCommands.fs and
ReportCommands.fs:

### invoice record

```
leobloom invoice record
    --tenant "Jeffrey"
    --fiscal-period-id 12
    --rent-amount 1200.00
    --utility-share 85.50
    --total-amount 1285.50
    --generated-at "2026-04-01T12:00Z"
    [--document-path "/docs/inv-001.pdf"]
    [--notes "April charges"]
    [--json]
```

### invoice show

```
leobloom invoice show <invoice-id> [--json]
```

### invoice list

```
leobloom invoice list [--tenant "Jeffrey"] [--fiscal-period-id 12] [--json]
```

## Acceptance Criteria

### Behavioral (Gherkin scenarios)

- B1: `invoice record` with all required args + optional args records an
  invoice and prints the result to stdout, exit 0.
- B2: `invoice record` with --json flag outputs valid JSON to stdout, exit 0.
- B3: `invoice record` missing required args prints error to stderr,
  exit 1 or 2.
- B4: `invoice record` that triggers service validation error surfaces the
  error to stderr, exit 1.
- B5: `invoice show <id>` for an existing invoice prints details to stdout,
  exit 0.
- B6: `invoice show <id>` with --json flag outputs valid JSON, exit 0.
- B7: `invoice show` for nonexistent ID prints error to stderr, exit 1.
- B8: `invoice show` with no ID prints error to stderr, exit 2.
- B9: `invoice list` with no filters returns all invoices to stdout, exit 0.
- B10: `invoice list --tenant "X"` filters by tenant, exit 0.
- B11: `invoice list --fiscal-period-id N` filters by period, exit 0.
- B12: `invoice list --json` outputs valid JSON, exit 0.
- B13: `invoice list` with no matching results prints empty output (not an
  error), exit 0.

### Structural (Builder/QE verification, not Gherkin)

- S1: InvoiceCommands.fs exists and compiles.
- S2: LeoBloom.CLI.fsproj references LeoBloom.Ops.
- S3: Program.fs routes `invoice` subcommand to InvoiceCommands.dispatch.
- S4: OutputFormatter.fs handles Invoice type in formatHuman.
- S5: All existing tests still pass (no regressions).

## Out of Scope

- Invoice business logic changes (that's P021 territory, already shipped).
- PDF generation or document management.
- Batch operations.
- Any new service methods -- this wraps what exists.

## Assessment: Brainstorm Needed?

**No.** This is unambiguous. The service API is defined (3 methods, clear
signatures), the CLI pattern is established (LedgerCommands.fs is the
template), and the domain types are frozen. Straight to planning.
