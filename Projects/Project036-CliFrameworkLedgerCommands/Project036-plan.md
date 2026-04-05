# Project 036 --- Plan

## Objective

Stand up the CLI framework (entry point, Argu-based arg parsing, output
formatting, exit codes) and prove it with three ledger commands: `post`,
`void`, `show`. The framework must be extensible so P037--P042 add new
command groups without modifying existing framework code.

## Research Notes

**Strong local context, skipping external research.** The codebase is clean,
patterns are consistent, and the service signatures are all I need.

**Key finding:** There is no `getById` / `show` function in
`JournalEntryService` or `JournalEntryRepository`. The `ledger show` command
needs a read-path service function. This is new code, but it's trivial and
belongs in the Ledger domain project (per ADR-002). It does NOT belong in
the CLI layer (per ADR-003).

**Service signatures the CLI will call:**

- `JournalEntryService.post : PostJournalEntryCommand -> Result<PostedJournalEntry, string list>`
- `JournalEntryService.voidEntry : VoidJournalEntryCommand -> Result<JournalEntry, string list>`
- `JournalEntryService.getEntry : int -> Result<PostedJournalEntry, string list>` (new -- Phase 1)

All services open their own connections/transactions. The CLI never touches
Npgsql.

## Phases

### Phase 1: Service Gap -- `getEntry` Read Path

**What:** Add a `getEntry` function to `JournalEntryRepository` and
`JournalEntryService` that retrieves a journal entry with its lines and
references by ID. Returns `Result<PostedJournalEntry, string list>` --
`Error ["Journal entry with id N does not exist"]` when not found.

**Files:**

| File | Action |
|---|---|
| `Src/LeoBloom.Ledger/JournalEntryRepository.fs` | Add `getEntryById` function |
| `Src/LeoBloom.Ledger/JournalEntryService.fs` | Add `getEntry` function (connection-owning wrapper) |

**Details:**

- Repository: `getEntryById (txn: NpgsqlTransaction) (entryId: int) : PostedJournalEntry option`
  - SELECT from `ledger.journal_entry` + `ledger.journal_entry_line` + `ledger.journal_entry_reference`
  - Returns `None` when ID doesn't exist
- Service: `getEntry (entryId: int) : Result<PostedJournalEntry, string list>`
  - Opens connection + transaction (read-only pattern -- just use the existing `DataSource.openConnection()`)
  - Maps `None` to `Error`, `Some` to `Ok`
  - No write operations, but uses a transaction for consistency with the rest of the service layer

**Verification:** Existing tests pass. New function callable from the REPL or
test code. (Gherkin coverage comes later via CLI specs, not service specs --
per ADR-003.)

### Phase 2: CLI Project Skeleton

**What:** Create the `LeoBloom.CLI` project with Argu, wire it into the
solution, and implement the framework plumbing.

**Files:**

| File | Action |
|---|---|
| `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` | Create -- console app, references Domain + Ledger + Utilities, Argu package |
| `Src/LeoBloom.CLI/Program.fs` | Create -- entry point |
| `Src/LeoBloom.CLI/ExitCodes.fs` | Create -- exit code constants + mapping |
| `Src/LeoBloom.CLI/OutputFormatter.fs` | Create -- human-readable + JSON formatting |
| `Src/LeoBloom.CLI/ErrorHandler.fs` | Create -- Argu error handler (stderr, exit 2 for parse errors) |
| `LeoBloom.sln` | Modify -- add LeoBloom.CLI project |

**Project structure (fsproj compile order):**

```
ExitCodes.fs
OutputFormatter.fs
ErrorHandler.fs
Program.fs
```

**Details:**

- **fsproj:** `<OutputType>Exe</OutputType>`, target `net10.0`. References:
  - `LeoBloom.Domain`
  - `LeoBloom.Ledger`
  - `LeoBloom.Utilities` (for `DataSource`, `Log`)
  - NuGet: `Argu` (latest stable)

- **ExitCodes.fs:**
  ```fsharp
  module LeoBloom.CLI.ExitCodes
  let success = 0
  let businessError = 1
  let systemError = 2
  ```

- **OutputFormatter.fs:**
  - `formatHuman : obj -> string` -- type-dispatched pretty printing for domain types
  - `formatJson : obj -> string` -- System.Text.Json serialization
  - `write : bool -> Result<'a, string list> -> unit`
    - `bool` = isJson flag
    - `Ok` -> format to stdout
    - `Error` -> format error list to stderr
  - Human-readable output for `PostedJournalEntry`: show entry header, then lines as a table (account, debit, credit, memo), then references
  - Human-readable output for `JournalEntry` (void result): show entry header with void status

- **ErrorHandler.fs:**
  - Custom Argu `IExiter` implementation
  - Parse errors -> stderr + exit 2 (system error -- user gave us garbage)
  - This is where `--help` text goes too (Argu handles this via the exiter)

- **Program.fs -- entry point:**
  1. `Log.initialize()`
  2. Parse args via Argu into the top-level DU
  3. Route to the correct command handler
  4. Map `Result` to exit code
  5. `Log.closeAndFlush()`
  6. `exit code`

- **Top-level Argu DU (in Program.fs):**
  ```
  LeoBloomArgs
    | [<CliPrefix(CliPrefix.None)>] Ledger of ParseResults<LedgerArgs>

  LedgerArgs
    | [<CliPrefix(CliPrefix.None)>] Post of ParseResults<LedgerPostArgs>
    | [<CliPrefix(CliPrefix.None)>] Void of ParseResults<LedgerVoidArgs>
    | [<CliPrefix(CliPrefix.None)>] Show of ParseResults<LedgerShowArgs>
  ```
  This structure means P037--P042 add new top-level DU cases (e.g.,
  `| Account of ParseResults<AccountArgs>`) without modifying Ledger code.

- **`--json` flag:** Global flag on `LeoBloomArgs`, not per-subcommand. Keeps
  the interface consistent and avoids repeating it on every command group.

**Verification:** `dotnet build` succeeds. `dotnet run --project Src/LeoBloom.CLI -- --help` prints usage. `dotnet run --project Src/LeoBloom.CLI -- ledger --help` prints ledger subcommands.

### Phase 3: Ledger Command Handlers

**What:** Implement the three ledger commands as thin parse-call-format
functions.

**Files:**

| File | Action |
|---|---|
| `Src/LeoBloom.CLI/LedgerCommands.fs` | Create -- all three handlers + Argu DU definitions for ledger subcommands |
| `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` | Modify -- add LedgerCommands.fs before Program.fs |

**Compile order becomes:**

```
ExitCodes.fs
OutputFormatter.fs
ErrorHandler.fs
LedgerCommands.fs
Program.fs
```

**Command details:**

**`ledger post`:**
- Args: `--debit <acct:amount>` (repeatable), `--credit <acct:amount>` (repeatable), `--date DATE`, `--description TEXT`, `--source TEXT` (optional), `--fiscal-period-id INT`, `--ref <type:value>` (optional, repeatable)
- Parse `acct:amount` pairs into `PostLineCommand` list (debit lines from `--debit`, credit lines from `--credit`)
- Build `PostJournalEntryCommand`, call `JournalEntryService.post`
- Output: posted entry details

**`ledger void`:**
- Args: `<entry-id>` (positional via Argu `MainCommand`), `--reason TEXT`
- Build `VoidJournalEntryCommand`, call `JournalEntryService.voidEntry`
- Output: voided entry details

**`ledger show`:**
- Args: `<entry-id>` (positional)
- Call `JournalEntryService.getEntry`
- Output: entry with lines and references

**Parsing conventions:**
- `acct:amount` format: split on first `:`, left side is account ID (int), right side is amount (decimal). Parse errors become validation errors -> exit 1.
- Date format: `yyyy-MM-dd` parsed as `DateOnly`
- All parse failures produce clear error messages to stderr

**Error routing:**
- Argu parse failure -> stderr + exit 2 (handled by ErrorHandler)
- Our own parse failure (bad acct:amount format, bad date) -> stderr + exit 1
- Service returns `Error` -> stderr + exit 1
- Service returns `Ok` -> stdout + exit 0
- Unhandled exception -> stderr + exit 2

**Verification:** Manual smoke test against dev DB:
```bash
dotnet run --project Src/LeoBloom.CLI -- ledger show 1
dotnet run --project Src/LeoBloom.CLI -- ledger show 1 --json
dotnet run --project Src/LeoBloom.CLI -- ledger post --debit 1:100.00 --credit 2:100.00 --date 2026-01-15 --description "Test" --fiscal-period-id 1
dotnet run --project Src/LeoBloom.CLI -- ledger void 999 --reason "bad entry"  # should fail: not found
```

### Phase 4: appsettings for CLI

**What:** The CLI exe needs `appsettings.Development.json` in its output
directory so `DataSource` can find the connection string.

**Files:**

| File | Action |
|---|---|
| `Src/LeoBloom.CLI/appsettings.Development.json` | Create -- copy from existing project pattern |
| `Src/LeoBloom.CLI/LeoBloom.CLI.fsproj` | Modify -- add `<Content Include="appsettings.*.json" CopyToOutputDirectory="PreserveNewest" />` |

**Verification:** `dotnet run --project Src/LeoBloom.CLI -- ledger show 1`
connects to leobloom_dev without config errors.

> **Note:** Phase 4 is listed separately for clarity, but the Builder should
> do it during Phase 2 since the project won't run without it. It's a
> reminder, not a sequential dependency.

## Acceptance Criteria

- [ ] `dotnet build` succeeds for the entire solution with zero warnings from LeoBloom.CLI
- [ ] `leobloom --help` prints top-level usage showing available command groups
- [ ] `leobloom ledger --help` prints ledger subcommands (post, void, show)
- [ ] `leobloom ledger post` with valid args posts a journal entry, prints result to stdout, exits 0
- [ ] `leobloom ledger post --json` with valid args prints JSON to stdout, exits 0
- [ ] `leobloom ledger post` with invalid args (missing required) prints error to stderr, exits 1 or 2 as appropriate
- [ ] `leobloom ledger void <id> --reason TEXT` voids an entry, prints result to stdout, exits 0
- [ ] `leobloom ledger void <nonexistent-id> --reason TEXT` prints error to stderr, exits 1
- [ ] `leobloom ledger show <id>` prints entry with lines and references to stdout, exits 0
- [ ] `leobloom ledger show <nonexistent-id>` prints error to stderr, exits 1
- [ ] `leobloom ledger show <id> --json` prints JSON to stdout, exits 0
- [ ] `leobloom garbage` prints error to stderr, exits 2
- [ ] No business logic exists in any CLI file -- all commands are parse-call-format
- [ ] No Npgsql references in LeoBloom.CLI project (no connection/transaction management)
- [ ] Adding a new command group (e.g., `account`) requires only: (1) new DU case in top-level args, (2) new command file, (3) new route in Program.fs -- no changes to existing command files
- [ ] All existing tests pass (`dotnet test`)

## Risks

**Argu version compatibility with .NET 10:** Argu targets .NET Standard 2.0+
so it should work fine, but the Builder should verify the latest stable
version installs cleanly. If it doesn't, fall back to the newest version
that does.

**`getEntry` read path is new service code:** This is the one place where
P036 touches business logic outside the CLI layer. It's straightforward
(SELECT by ID, return None/Some), but it must follow the existing patterns
in `JournalEntryRepository` exactly. The Reviewer should verify it's not
doing anything clever.

**`--debit`/`--credit` parsing:** The `acct:amount` format is custom parsing
on top of Argu. Argu gives us the raw string; we split and parse. Edge
cases: negative amounts (reject -- amounts are always positive per domain
rules), non-numeric account IDs, missing colon, empty strings. The Gherkin
specs need to cover these parse edge cases.

**`--json` flag placement:** Putting it at the top level means `leobloom
--json ledger show 1`, not `leobloom ledger show 1 --json`. Argu may or may
not propagate top-level flags to subcommands -- the Builder needs to verify
this. If Argu doesn't support it naturally, the flag should go on each
command group DU instead (more repetition but guaranteed to work).

## Out of Scope

- **No account, fiscal-period, report, or ops commands** -- those are P037--P042
- **No Gherkin specs for service logic** -- `getEntry` is a trivial read; service-level behavioral coverage is unnecessary per ADR-003
- **No integration test harness for CLI** -- the Gherkin specs (written by Gherkin Writer, implemented by QE) cover CLI behavior. The Builder does manual smoke testing only.
- **No `--verbose` or `--quiet` flags** -- not in the backlog item, can be added later if needed
- **No shell completion scripts** -- nice to have, not in scope
