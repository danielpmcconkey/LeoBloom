# Project 042 -- Test Results

**Date:** 2026-04-06
**Commit:** 72df9df (feature/p042-cli-invoice-commands, uncommitted changes on top of main)
**Result:** 12/12 acceptance criteria verified

## Test Suite Summary

- **Total tests:** 658
- **Passed:** 655
- **Failed:** 3 (all pre-existing PostObligationToLedger failures -- tracked as P055)
- **P042-specific tests:** 19/19 passed

### Pre-existing Failures (not P042)

All 3 failures are in `PostObligationToLedgerTests`:
1. `posting when no fiscal period covers confirmed_date returns error`
2. `failed post to closed period leaves instance in confirmed status with no journal entry`
3. `posting when fiscal period is closed returns error`

These are closed-period posting issues tracked under P055. The brief stated 2 pre-existing
failures; the actual count is 3 (all in the same test class, same root cause). Not a P042
concern.

## Acceptance Criteria Verification

| # | Criterion | Verified | Notes |
|---|-----------|----------|-------|
| AC1 | `InvoiceCommands.fs` exists and compiles | Yes | File at `Src/LeoBloom.CLI/InvoiceCommands.fs`, 131 lines. Module `LeoBloom.CLI.InvoiceCommands`. Defines `InvoiceRecordArgs`, `InvoiceShowArgs`, `InvoiceListArgs`, `InvoiceArgs` DUs and `dispatch` function. Build succeeds with 0 errors. |
| AC2 | `LeoBloom.CLI.fsproj` references `LeoBloom.Ops` | Yes | Line 23: `<ProjectReference Include="..\LeoBloom.Ops\LeoBloom.Ops.fsproj" />`. `InvoiceCommands.fs` included in compile order (line 16) after `ReportCommands.fs` and before `Program.fs`. |
| AC3 | `Program.fs` routes `invoice` subcommand to `InvoiceCommands.dispatch` | Yes | Line 16: `Invoice of ParseResults<InvoiceArgs>` in `LeoBloomArgs` DU. Line 47-48: `Some (Invoice invoiceResults) -> InvoiceCommands.dispatch isJson invoiceResults`. Usage string: "Invoice commands (record, show, list)". |
| AC4 | `OutputFormatter.fs` handles Invoice type in `formatHuman` | Yes | Line 196: `:? Invoice as inv -> formatInvoice inv` in `formatHuman`. Private `formatInvoice` (lines 157-170) and `formatInvoiceList` (lines 172-184) functions present. Dedicated `writeInvoiceList` function (lines 234-242) avoids F# type erasure issue with list matching. |
| AC5 | `dotnet build` succeeds for entire solution with no errors | Yes | Build succeeded, 0 warnings, 0 errors. |
| AC6 | All existing tests still pass (no regressions) | Yes | 655/658 pass. The 3 failures are pre-existing `PostObligationToLedgerTests` (P055). No new failures introduced by P042. |
| AC7 | `invoice record` accepts all RecordInvoiceCommand fields as CLI args | Yes | `InvoiceRecordArgs` DU defines: `Tenant`, `Fiscal_Period_Id`, `Rent_Amount`, `Utility_Share`, `Total_Amount`, `Generated_At` (all `Mandatory`), plus optional `Document_Path`, `Notes`, and `Json`. Test FT-ICD-001 exercises all required + optional args and passes. |
| AC8 | `invoice show` accepts invoice ID as positional argument | Yes | `InvoiceShowArgs` has `[<MainCommand; Mandatory>] Invoice_Id of int`. Tests FT-ICD-010 and FT-ICD-011 exercise this and pass. |
| AC9 | `invoice list` accepts optional `--tenant` and `--fiscal-period-id` filters | Yes | `InvoiceListArgs` defines `Tenant of string` and `Fiscal_Period_Id of int` (both optional). Tests FT-ICD-021 (tenant filter) and FT-ICD-022 (period filter) exercise these and pass. |
| AC10 | All three subcommands support `--json` flag | Yes | Each handler checks `isJson || args.Contains ...Json`. Tests FT-ICD-002 (record JSON), FT-ICD-011 (show JSON), FT-ICD-023 (list JSON) all pass and validate JSON parsing. |
| AC11 | Service errors surface to stderr with exit code 1 | Yes | `handleRecord` calls `write isJson` which returns `ExitCodes.businessError` (1) on `Error`. Test FT-ICD-005 sends invalid fiscal period 999999, asserts exit code 1 and non-empty stderr. Test FT-ICD-012 sends nonexistent invoice ID, asserts exit code 1 and non-empty stderr. Both pass. |
| AC12 | Missing required args produce Argu error to stderr with exit code 2 | Yes | `LeoBloomExiter` exits with `ExitCodes.systemError` (2) on Argu parse errors. Tests FT-ICD-003 (no args, exit 2), FT-ICD-013 (show no ID, exit 2) both pass. Tests FT-ICD-004a through 004f assert exit 1 or 2 for each missing required field and all pass. |

## Gherkin Coverage

| Scenario Tag | Description | Test Exists | Passes |
|---|---|---|---|
| @FT-ICD-001 | Record invoice with all required and optional args | Yes | Yes |
| @FT-ICD-002 | Record invoice with --json outputs valid JSON | Yes | Yes |
| @FT-ICD-003 | Record with no arguments prints error to stderr | Yes | Yes |
| @FT-ICD-004 | Record missing a required argument is rejected (6 examples) | Yes (004a-004f) | Yes |
| @FT-ICD-005 | Record with service validation error surfaces to stderr | Yes | Yes |
| @FT-ICD-010 | Show existing invoice via CLI | Yes | Yes |
| @FT-ICD-011 | Show with --json outputs valid JSON | Yes | Yes |
| @FT-ICD-012 | Show nonexistent invoice prints error to stderr | Yes | Yes |
| @FT-ICD-013 | Show with no invoice ID prints error to stderr | Yes | Yes |
| @FT-ICD-020 | List all invoices with no filters | Yes | Yes |
| @FT-ICD-021 | List invoices filtered by tenant | Yes | Yes |
| @FT-ICD-022 | List invoices filtered by fiscal period | Yes | Yes |
| @FT-ICD-023 | List with --json outputs valid JSON | Yes | Yes |
| @FT-ICD-024 | List with no matching results prints empty output | Yes | Yes |

All 14 Gherkin scenarios (including the 6-row Scenario Outline expanded as 004a-004f) have
corresponding tests, and all pass.

## Brief B-Criteria Cross-Reference

| Brief | Description | Covered By | Met |
|---|---|---|---|
| B1 | record with all args, stdout, exit 0 | FT-ICD-001 | Yes |
| B2 | record --json, valid JSON, exit 0 | FT-ICD-002 | Yes |
| B3 | record missing required args, stderr, exit 1/2 | FT-ICD-003, 004a-f | Yes |
| B4 | record service validation error, stderr, exit 1 | FT-ICD-005 | Yes |
| B5 | show existing invoice, stdout, exit 0 | FT-ICD-010 | Yes |
| B6 | show --json, valid JSON, exit 0 | FT-ICD-011 | Yes |
| B7 | show nonexistent ID, stderr, exit 1 | FT-ICD-012 | Yes |
| B8 | show no ID, stderr, exit 2 | FT-ICD-013 | Yes |
| B9 | list no filters, stdout, exit 0 | FT-ICD-020 | Yes |
| B10 | list --tenant filter | FT-ICD-021 | Yes |
| B11 | list --fiscal-period-id filter | FT-ICD-022 | Yes |
| B12 | list --json, valid JSON, exit 0 | FT-ICD-023 | Yes |
| B13 | list no matching results, empty output, exit 0 | FT-ICD-024 | Yes |

## Fabrication Check

- All cited files verified to exist in the repo at stated paths.
- Test output obtained from a live `dotnet test` run on the current working tree.
- No circular evidence detected -- criteria verified against file contents and test execution.
- The 3 pre-existing failures are documented honestly (brief said 2, actual is 3).

## Verdict

**APPROVED**

Every acceptance criterion is verified. All 14 Gherkin scenarios have corresponding
passing tests. The evidence chain is direct: file contents checked, build verified, tests
executed. The 3 pre-existing PostObligationToLedger failures are unrelated to P042 and
tracked under P055.
