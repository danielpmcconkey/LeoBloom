# Project 042 -- Plan

## Objective

Expose InvoiceService's three methods (recordInvoice, showInvoice, listInvoices)
as CLI subcommands under a new `invoice` command group. This is a thin wrapper --
all domain logic, validation, and persistence already exist in LeoBloom.Ops. The
job is Argu argument parsing, service dispatch, output formatting, and exit codes.

## Research Summary

Strong local context, skipped external research. The existing LedgerCommands.fs
and ReportCommands.fs provide a clear, consistent pattern. No ambiguity.

Key findings from repo research:

- **LedgerCommands.fs** is the primary template: Argu DUs with `IArgParserTemplate`,
  `Mandatory`/`MainCommand` attributes, `--json` flag at subcommand level, handlers
  that call service methods, `write isJson` for output.
- **OutputFormatter.fs** uses `formatHuman` with type-matching dispatch. Invoice and
  Invoice list need new format branches. The `write` function handles `Result<obj, string list>`.
  The `listInvoices` service returns `Invoice list` (not Result), so the list handler
  needs to wrap in `Ok` before calling `write`.
- **Program.fs** has a top-level `LeoBloomArgs` DU with `CliPrefix.None` subcommands.
  Invoice follows the Ledger pattern (passes `isJson` through).
- **CLI fsproj** needs `LeoBloom.Ops` project reference (currently has Domain, Ledger,
  Reporting, Utilities). InvoiceCommands.fs goes in compile order before Program.fs.
- **RecordInvoiceCommand** fields: tenant (string), fiscalPeriodId (int), rentAmount
  (decimal), utilityShare (decimal), totalAmount (decimal), generatedAt (DateTimeOffset),
  documentPath (string option), notes (string option).
- **ListInvoicesFilter** fields: tenant (string option), fiscalPeriodId (int option).
  Lives in InvoiceRepository.fs namespace LeoBloom.Ops.
- **Invoice** type lives in LeoBloom.Domain.Ops.

## Phases

### Phase 1: InvoiceCommands.fs

**What:** New file with Argu DU definitions and command handlers for record, show,
and list subcommands.

**File:** `Src/LeoBloom.CLI/InvoiceCommands.fs` (created)

**Details:**

Module: `LeoBloom.CLI.InvoiceCommands`

Opens: `System`, `Argu`, `LeoBloom.Domain.Ops`, `LeoBloom.Ops`,
`LeoBloom.CLI.OutputFormatter`

**Argu DUs:**

1. `InvoiceRecordArgs` -- all RecordInvoiceCommand fields as CLI args:
   - `[<Mandatory>] Tenant of string`
   - `[<Mandatory>] Fiscal_Period_Id of int`
   - `[<Mandatory>] Rent_Amount of decimal`
   - `[<Mandatory>] Utility_Share of decimal`
   - `[<Mandatory>] Total_Amount of decimal`
   - `[<Mandatory>] Generated_At of string` (parsed to DateTimeOffset in handler)
   - `Document_Path of string` (optional)
   - `Notes of string` (optional)
   - `Json`

2. `InvoiceShowArgs`:
   - `[<MainCommand; Mandatory>] Invoice_Id of int`
   - `Json`

3. `InvoiceListArgs`:
   - `Tenant of string` (optional filter)
   - `Fiscal_Period_Id of int` (optional filter)
   - `Json`

4. `InvoiceArgs` (parent DU):
   - `[<CliPrefix(CliPrefix.None)>] Record of ParseResults<InvoiceRecordArgs>`
   - `[<CliPrefix(CliPrefix.None)>] Show of ParseResults<InvoiceShowArgs>`
   - `[<CliPrefix(CliPrefix.None)>] List of ParseResults<InvoiceListArgs>`

**Handlers:**

- `handleRecord`: Parse `Generated_At` string to DateTimeOffset (with error
  handling like LedgerCommands' date parsing). Build `RecordInvoiceCommand`,
  call `InvoiceService.recordInvoice`, pass result through `write isJson`
  (mapping Ok value to `obj`).

- `handleShow`: Get invoice ID from args, call `InvoiceService.showInvoice`,
  pass result through `write isJson`.

- `handleList`: Build `ListInvoicesFilter` from optional args, call
  `InvoiceService.listInvoices`. This returns `Invoice list` (not Result),
  so wrap as `Ok (invoices :> obj)` and pass through `write isJson`. The
  formatter will handle list display.

**Dispatch:**

```fsharp
let dispatch (isJson: bool) (args: ParseResults<InvoiceArgs>) : int
```

Follows LedgerCommands.dispatch pattern exactly -- match on
`args.TryGetSubCommand()`, route to handlers, print usage on `None`.

**Verification:** File compiles. Argu DUs parse expected argument shapes.

### Phase 2: OutputFormatter.fs Update

**What:** Add human-readable formatting for Invoice (single) and Invoice list
to the `formatHuman` dispatch function.

**File:** `Src/LeoBloom.CLI/OutputFormatter.fs` (modified)

**Details:**

- Add `open LeoBloom.Domain.Ops` to imports.
- Add `formatInvoice` private function that renders an Invoice in a style
  consistent with `formatEntryHeader` -- labeled fields, aligned columns:
  ```
  Invoice #42
    Tenant:         Jeffrey
    Fiscal Period:  12
    Rent Amount:    1200.00
    Utility Share:  85.50
    Total Amount:   1285.50
    Generated At:   2026-04-01 12:00:00
    Document Path:  /docs/inv-001.pdf
    Notes:          April charges
    Created:        2026-04-01 12:00:00
    Modified:       2026-04-01 12:00:00
  ```
- Add `formatInvoiceList` private function that renders a tabular list:
  ```
  ID     Tenant      Period  Rent       Utility    Total
  ----   ---------   ------  ---------  ---------  ---------
  42     Jeffrey     12      1200.00    85.50      1285.50
  ```
  Empty list produces no output (empty string) -- consistent with "empty
  output, not an error" per B13.
- Add match cases to `formatHuman`:
  - `:? Invoice as inv -> formatInvoice inv`
  - `:? (Invoice list) as invs -> formatInvoiceList invs`

  Note: The `Invoice list` match must come before the catch-all `_` case.
  F# list type matching: use a boxed list check or cast the obj. Builder
  should verify the actual runtime type that arrives when `invoices :> obj`
  is passed -- if it's `obj list` vs `Invoice list`, the match pattern
  needs to account for that. Safest approach: match on the list type at
  the call site rather than in formatHuman, or use a dedicated
  `writeInvoiceList` function in the handler. Builder decides the cleanest
  implementation.

**Verification:** Invoice and Invoice list render correctly in human mode.
JSON mode works via existing `formatJson` (no changes needed).

### Phase 3: Program.fs Update

**What:** Add `Invoice` case to `LeoBloomArgs` DU and wire dispatch.

**File:** `Src/LeoBloom.CLI/Program.fs` (modified)

**Details:**

- Add `open LeoBloom.CLI.InvoiceCommands`
- Add to `LeoBloomArgs` DU:
  ```fsharp
  | [<CliPrefix(CliPrefix.None)>] Invoice of ParseResults<InvoiceArgs>
  ```
- Add usage string: `"Invoice commands (record, show, list)"`
- Add match case in dispatch:
  ```fsharp
  | Some (Invoice invoiceResults) ->
      InvoiceCommands.dispatch isJson invoiceResults
  ```

**Verification:** `leobloom invoice --help` shows record/show/list subcommands.

### Phase 4: fsproj Update

**What:** Add LeoBloom.Ops project reference and InvoiceCommands.fs to compile order.

**File:** `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` (modified)

**Details:**

- Add `<Compile Include="InvoiceCommands.fs" />` after ReportCommands.fs and
  before Program.fs in the ItemGroup.
- Add `<ProjectReference Include="..\LeoBloom.Ops\LeoBloom.Ops.fsproj" />`
  to the ProjectReference ItemGroup.

**Verification:** `dotnet build` succeeds for CLI project.

## Execution Order

Phases 1-4 are interdependent but the Builder should implement in the listed
order (1 through 4). Phase 4 is technically needed for compilation, so the
Builder will likely do 4 first or alongside 1 -- that's fine. The phase
numbering is logical grouping, not strict sequencing.

## Acceptance Criteria

- [ ] AC1: `Src/LeoBloom.CLI/InvoiceCommands.fs` exists and compiles (S1)
- [ ] AC2: `LeoBloom.CLI.fsproj` references `LeoBloom.Ops` (S2)
- [ ] AC3: `Program.fs` routes `invoice` subcommand to `InvoiceCommands.dispatch` (S3)
- [ ] AC4: `OutputFormatter.fs` handles Invoice type in `formatHuman` (S4)
- [ ] AC5: `dotnet build` succeeds for the entire solution with no errors
- [ ] AC6: All existing tests still pass (S5)
- [ ] AC7: `invoice record` accepts all RecordInvoiceCommand fields as CLI args
- [ ] AC8: `invoice show` accepts an invoice ID as a positional argument
- [ ] AC9: `invoice list` accepts optional `--tenant` and `--fiscal-period-id` filters
- [ ] AC10: All three subcommands support `--json` flag for JSON output
- [ ] AC11: Service errors surface to stderr with exit code 1
- [ ] AC12: Missing required args produce Argu error to stderr with exit code 2

## Risks

- **Invoice list type matching in formatHuman**: F# type erasure means
  `obj :? Invoice list` may not work at runtime. The Builder needs to verify
  this and may need an alternative approach (e.g., wrapping the list in a
  discriminated union or using a dedicated write function for lists). This is
  the only non-trivial implementation decision in the project.

- **DateTimeOffset parsing**: The `--generated-at` arg comes in as a string.
  The handler needs to parse it to `DateTimeOffset`. If the format is wrong,
  it should produce a user-friendly error (not a stack trace). Follow the
  LedgerCommands `parseDate` pattern -- try-parse, return `Result`.

## Out of Scope

- Invoice business logic changes (P021 territory)
- PDF generation or document management
- Batch operations
- New service methods -- this wraps what exists
- Tests (QE writes those after Builder)
- Gherkin specs (Gherkin Writer's job)
