# Project 072 — Housekeeping Batch: Plan

## Objective

Batch-resolve 10 low-severity audit findings (tech debt + consistency) in a
single cleanup pass. Every item is a convention-alignment refactor with zero
behavioral changes. If any item would require modifying an existing test, it
gets pulled from this batch.

## Key Decisions

### D1: Npgsql version — pin to 9.0.3 (downgrade Migrations)
Six projects use 9.0.3; only Migrations uses 10.0.1. The Migrations project
uses raw `NpgsqlConnection` and `NpgsqlCommand` — the same API surface as
every other project. Npgsql 10.x is a major bump that could introduce subtle
behavioral changes in the driver. Pinning to the majority version (9.0.3) is
the conservative choice with zero risk.

### D2: Ghost directory — already deleted
`Src/LeoBloom.Api/` does not exist in the current tree. This item is a no-op.
Skip it.

### D3: Log path — use `./logs` relative to AppContext.BaseDirectory
The current default `/workspace/application_logs/leobloom` only works inside
the Docker sandbox. The fix: default to a `logs` subdirectory relative to the
application's base directory (`AppContext.BaseDirectory`). This works
everywhere — Docker, host, CI. The `Logging:FileBasePath` config key still
overrides it. Additionally, create the directory if it doesn't exist (fail-loud
is overkill for a log path — just ensure it exists before writing).

### D4: isJson threading in ReportCommands
Add `isJson: bool` as the first parameter to `ReportCommands.dispatch`,
matching every other command module's signature. Thread it to handlers that
already extract `isJson` locally from sub-args, replacing their local
extraction. Handlers without `--json` support ignore the parameter (same
pattern as DiagnosticCommands).

## Phases

### Phase 1: Infrastructure & Dependencies (Items 1, 3, 4)

**What:**
- Downgrade `Npgsql` in `LeoBloom.Migrations.fsproj` from 10.0.1 → 9.0.3
- Change `Log.fs` default path from `/workspace/application_logs/leobloom`
  to `Path.Combine(AppContext.BaseDirectory, "logs")` and add
  `Directory.CreateDirectory(baseDir) |> ignore` before constructing `logPath`
- Add `portfolio` to the search path in
  `Src/LeoBloom.CLI/appsettings.Development.json`:
  `Search Path=ledger,ops,portfolio,public`

**Files modified:**
- `Src/LeoBloom.Migrations/LeoBloom.Migrations.fsproj` — version change
- `Src/LeoBloom.Utilities/Log.fs` — default path + directory creation
- `Src/LeoBloom.CLI/appsettings.Development.json` — search path

**Verification:** `dotnet build` succeeds. Existing tests pass.

### Phase 2: CLI Consistency (Items 5, 6)

**What:**
- Add `(isJson: bool)` parameter to `ReportCommands.dispatch`. Update call
  site in `Program.fs` to pass `isJson`. Inside `dispatch`, thread `isJson`
  to handlers that already support it (handlers that currently extract it
  from sub-args). Remove the local extraction in those handlers — they
  receive it from dispatch now.
- Change `formatInvoiceList` empty case from `""` to `"(no invoices found)"`
- Change `formatTransferList` empty case from `""` to
  `"(no transfers found)"`

**Files modified:**
- `Src/LeoBloom.CLI/ReportCommands.fs` — dispatch signature + handler updates
- `Src/LeoBloom.CLI/Program.fs` — call site update (line 60)
- `Src/LeoBloom.CLI/OutputFormatter.fs` — two empty-list messages

**Verification:** `dotnet build` succeeds. Existing tests pass.

### Phase 3: Repository Convention Alignment (Items 7, 8, 9, 10)

**What:**
- Extract `readEntry` helper in `JournalEntryRepository.fs`: create a
  `let private readEntry (reader: DbDataReader) : JournalEntry` function
  matching the convention, then replace the three inlined reader mappings
  with calls to `readEntry reader`.
- Rename `mapReader` → `readAgreement` in `ObligationAgreementRepository.fs`.
  This is a private function, so no external call sites to update.
- Replace `"Query error:"` with `"Persistence error:"` in four locations in
  `AccountBalanceService.fs` (lines 34, 45, 55, 68).
- Replace local `addOpt` in `FundRepository.fs` with `DataHelpers.optParam`.
  The local version takes `int option` via closure; the shared version takes
  `obj option` and explicit `cmd`. Rewrite the six call sites to use
  `DataHelpers.optParam "@param" (value |> Option.map box) sql`.

**Files modified:**
- `Src/LeoBloom.Ledger/JournalEntryRepository.fs` — extract helper
- `Src/LeoBloom.Ops/ObligationAgreementRepository.fs` — rename function
- `Src/LeoBloom.Ledger/AccountBalanceService.fs` — error message text
- `Src/LeoBloom.Portfolio/FundRepository.fs` — replace local helper

**Verification:** `dotnet build` succeeds. Existing tests pass.

## Acceptance Criteria

- AC-1: All `.fsproj` files reference the same Npgsql major version (9.x)
- AC-2: `Src/LeoBloom.Api/` directory does not exist *(already true — no-op)*
- AC-3: All existing tests pass with zero modifications to test files
- AC-4: `Log.fs` default path is relative (no hardcoded Docker path); log
  directory is created automatically if absent
- AC-5: Connection string search path includes `portfolio` schema
- AC-6: `ReportCommands.dispatch` signature includes `isJson: bool`,
  matching all other command module dispatch functions
- AC-7: `formatInvoiceList` and `formatTransferList` return descriptive
  messages for empty lists, matching the dominant pattern
- AC-8: `JournalEntryRepository` has a shared `readEntry` helper; no
  inlined reader mappings remain
- AC-9: `ObligationAgreementRepository` reader helper is named
  `readAgreement`, matching the `read{Entity}` convention
- AC-10: `AccountBalanceService` uses `"Persistence error:"` consistently
  (no `"Query error:"` strings)
- AC-11: `FundRepository` uses `DataHelpers.optParam` — no local `addOpt`

## Risks

- **Npgsql downgrade (low):** Migrations project uses standard ADO.NET
  surface (`NpgsqlConnection`, `NpgsqlCommand`). No 10.x-specific APIs are
  used. If the downgrade somehow breaks the migration runner, the build will
  catch it immediately.
- **isJson threading (low):** Report handlers that already extract `isJson`
  from sub-args will switch to receiving it from dispatch. The behavior is
  identical — same boolean, different source. Handlers without `--json`
  support simply ignore the parameter.
- **DataHelpers.optParam type boxing (low):** The local `addOpt` takes
  `int option`; the shared helper takes `obj option`. The `int` values need
  boxing via `Option.map box`. This is a negligible cost and matches how
  every other repository does it.

## Out of Scope

- Adding `--json` support to report handlers that don't have it
- Updating the production appsettings (only Development is in the repo)
- Any changes to test files (AC-3 gate)
- TransferCommands date parser (C2 finding — separate project)
