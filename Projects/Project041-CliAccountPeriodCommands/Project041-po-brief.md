# P041 -- CLI Account + Period Commands

**Status:** In Progress
**Depends on:** P036 (CLI Framework -- Done), P007 (Account Balance -- Done), P009 (Fiscal Period Close/Reopen -- Done)
**Triggered by:** CLI sequencing plan (backlog items 036-042)
**Epic:** J -- CLI Consumption Layer

## Objective

Expose account queries (read-only) and fiscal period management as two new
CLI subcommand groups: `account` and `period`. This gives Dan a direct way
to look up the COA, check account balances, and manage month-end close from
the command line. It also gives automated agents a machine-readable path to
account lookups for validation.

This is a thin CLI wrapper plus a handful of new service/repository methods
that don't exist yet. The close/reopen and balance services already exist;
the list/show/create operations need new plumbing.

## What Ships

1. **AccountCommands.fs** -- Argu DU definitions + dispatch for:
   - `account list [--type TYPE] [--inactive]`
   - `account show <id-or-code>`
   - `account balance <id-or-code> [--as-of DATE]`

2. **PeriodCommands.fs** -- Argu DU definitions + dispatch for:
   - `period list`
   - `period close <id-or-key>`
   - `period reopen <id-or-key> --reason TEXT`
   - `period create --start DATE --end DATE --key TEXT`

3. **Program.fs update** -- add `Account` and `Period` cases to
   `LeoBloomArgs` DU and wire dispatch.

4. **OutputFormatter.fs update** -- human-readable formatting for Account,
   Account list, FiscalPeriod, and FiscalPeriod list types.

5. **New service/repository methods** (these do not exist today):
   - Account list query (with optional type/inactive filters)
   - Account show (by id or code, returning full Account record)
   - Fiscal period list query
   - Fiscal period create (insert with key/start/end, returns new record)
   - Fiscal period find-by-key (for `<id-or-key>` resolution on close/reopen)

6. **Gherkin specs** -- behavioral scenarios in
   `Specs/CLI/AccountCommands.feature` and
   `Specs/CLI/PeriodCommands.feature`.

7. **Test implementations** -- in `Src/LeoBloom.Tests/`.

## Existing Service Inventory

What already exists and just needs CLI wiring:
- `AccountBalanceService.getBalanceById` / `getBalanceByCode` -- backs `account balance`
- `FiscalPeriodService.closePeriod` -- backs `period close`
- `FiscalPeriodService.reopenPeriod` -- backs `period reopen`
- `FiscalPeriodRepository.findById` / `findByDate` -- period lookup by id/date
- `AccountBalanceRepository.resolveAccountId` -- code-to-id resolution

What does NOT exist and must be built:
- Account list query (filtered by type, with inactive toggle)
- Account show/detail query (full Account record by id or code)
- Fiscal period list query (return all periods)
- Fiscal period create (insert new period row)
- Fiscal period find-by-key (period_key string -> FiscalPeriod option)

This is more than a pure CLI wrapper project. The service gap is real but
bounded -- five new repository functions and thin service wrappers. No new
domain types are needed (Account and FiscalPeriod records already exist).

## CLI Interface Design

Follows the Argu pattern established in LedgerCommands.fs, TransferCommands.fs,
and InvoiceCommands.fs.

### id-or-key / id-or-code resolution

The backlog specifies `<id-or-code>` for accounts and `<id-or-key>` for periods.
The CLI should accept either an integer ID or a string code/key, and resolve
accordingly. This is the same pattern as AccountBalanceService which already
has `getBalanceById` vs `getBalanceByCode`.

## Acceptance Criteria

### Behavioral (Gherkin scenarios)

**Account commands:**

- B1: `account list` with no filters lists all active accounts, exit 0.
- B2: `account list --type asset` filters by account type, exit 0.
- B3: `account list --inactive` includes inactive accounts, exit 0.
- B4: `account list --json` outputs valid JSON, exit 0.
- B5: `account show <id>` for an existing account prints details, exit 0.
- B6: `account show <code>` for an existing account prints details, exit 0.
- B7: `account show` for nonexistent id/code prints error to stderr, exit 1.
- B8: `account show --json` outputs valid JSON, exit 0.
- B9: `account balance <id>` returns current balance, exit 0.
- B10: `account balance <code> --as-of 2026-01-31` returns historical balance, exit 0.
- B11: `account balance` for nonexistent account prints error to stderr, exit 1.
- B12: `account balance --json` outputs valid JSON, exit 0.

**Period commands:**

- B13: `period list` lists all fiscal periods, exit 0.
- B14: `period list --json` outputs valid JSON, exit 0.
- B15: `period close <id>` closes an open period, exit 0.
- B16: `period close <key>` closes an open period by key, exit 0.
- B17: `period close` for nonexistent period prints error to stderr, exit 1.
- B18: `period reopen <id> --reason "audit adjustment"` reopens a closed period, exit 0.
- B19: `period reopen` without --reason prints error to stderr, exit 1 or 2.
- B20: `period reopen` for nonexistent period prints error to stderr, exit 1.
- B21: `period create --start 2026-05-01 --end 2026-05-31 --key "2026-05"` creates a period, exit 0.
- B22: `period create --json` outputs valid JSON, exit 0.
- B23: `period create` with missing required args prints error to stderr, exit 1 or 2.

### Structural (Builder/QE verification, not Gherkin)

- S1: AccountCommands.fs and PeriodCommands.fs exist and compile.
- S2: Program.fs routes `account` and `period` subcommands to their dispatch functions.
- S3: OutputFormatter.fs handles Account, Account list, FiscalPeriod, and
  FiscalPeriod list types in formatHuman.
- S4: New repository functions exist for account list, account show, period list,
  period create, and period find-by-key.
- S5: All existing tests still pass (no regressions).

## Out of Scope

- Account creation/deactivation -- the backlog explicitly says this happens
  through migrations, not CLI.
- Modifying account properties (name, type, parent, sub-type).
- Fiscal period deletion -- not in the backlog, not in the domain.
- Any changes to the existing balance calculation logic (P007 territory).
- Any changes to the existing close/reopen validation logic (P009 territory).
- Batch operations.

## Flags for Planner

1. **Service gap is bigger than the prior CLI projects.** P039, P042, and P037
   were pure wrappers over existing service methods. P041 needs five new
   repository functions and their service wrappers. The planner should phase
   this so the repository/service layer is built and unit-tested before the CLI
   layer wires into it.

2. **id-or-key resolution pattern.** The planner needs to decide: does the CLI
   do the int-parse check and call different service methods (like
   AccountBalanceService), or does a single service method accept a string and
   resolve internally? Either way, precedent exists.

3. **Period create validation.** The backlog doesn't mention overlap validation
   (e.g., creating a period that overlaps an existing one). The planner should
   decide whether to add basic overlap detection or punt it. GAAP doesn't
   require it -- fiscal periods can theoretically overlap (e.g., a fiscal year
   period that contains monthly periods) -- but it's worth a conscious decision.

4. **Account type filter.** The `--type TYPE` flag on `account list` needs to
   resolve against account_type names in the database (asset, liability,
   equity, revenue, expense). The planner should confirm the set of valid
   type names.

## Assessment: Brainstorm Needed?

**No.** The commands are well-defined in the backlog. The service gap is
straightforward (list/show/create CRUD, no complex orchestration). The CLI
pattern is proven across three prior projects. Straight to planning.
