# P077 — Account Create CLI Command — Plan

## Objective

Wire `AccountService.createAccount` to the CLI as `leobloom account create` with
`--code`, `--name`, `--type`, `--parent`, and `--subtype` flags. The `CreateAccountCommand`
and downstream layers need the `subType` field threaded through so accounts can be
created with subtypes without touching SQL directly. This unblocks the COA expansion
(~17 new accounts Hobson needs for personal assets, liabilities, and FI accounts).

## Phases

### Phase 1: Domain & Service Layer — Thread SubType

**What:** Add `subType` to `CreateAccountCommand`, thread it through `AccountService.createAccount`
and `AccountRepository.create`.

**Files modified:**
- `Src/LeoBloom.Domain/Ledger.fs` — Add `subType: AccountSubType option` to `CreateAccountCommand`
- `Src/LeoBloom.Ledger/AccountService.fs` — Add subtype validation via `AccountSubType.isValidSubType`;
  pass subtype to repository. Requires looking up account type name from `accountTypeId` to validate.
- `Src/LeoBloom.Ledger/AccountRepository.fs` — Add `subType: AccountSubType option` parameter to
  `create`; include `account_subtype` in INSERT SQL when `Some`.

**Details:**

1. `CreateAccountCommand` gets a new field:
   ```fsharp
   type CreateAccountCommand =
       { code: string; name: string; accountTypeId: int; parentId: int option; subType: AccountSubType option }
   ```

2. `AccountService.createAccount` needs to:
   - Look up the account type name from `accountTypeId` (repo already has `accountTypeExists`;
     need to check if there's a `findAccountTypeById` or similar that returns the name).
   - Call `AccountSubType.isValidSubType accountTypeName cmd.subType` before proceeding.
   - Return error `"subtype '<X>' is not valid for account type '<Y>'"` on failure.
   - Pass `cmd.subType` to `AccountRepository.create`.

3. `AccountRepository.create` needs a new `subType` parameter. When `Some st`:
   - Add `account_subtype` to the INSERT column list
   - Add `@subType` parameter with `AccountSubType.toDbString st`
   - When `None`, omit (NULL default applies)

**Verification:** Existing tests still pass. New service-level tests confirm subtype threading
(covered in Phase 3).

### Phase 2: CLI Command — AccountCreateArgs + Handler

**What:** Add the `Create` subcommand to account CLI with all flags.

**Files modified:**
- `Src/LeoBloom.CLI/AccountCommands.fs` — Add `AccountCreateArgs` DU, `handleCreate` function,
  register `Create` case in `AccountArgs`, add dispatch case.

**Details:**

1. New Argu DU:
   ```fsharp
   type AccountCreateArgs =
       | [<Mandatory>] Code of string
       | [<Mandatory>] Name of string
       | [<Mandatory; CustomCommandLine("--type")>] Type of int
       | Parent of int
       | Subtype of string
       interface IArgParserTemplate with
           member this.Usage =
               match this with
               | Code _ -> "account code (e.g. 1010)"
               | Name _ -> "account name (e.g. 'Cash on Hand')"
               | Type _ -> "account type id (1=Asset, 2=Liability, etc.)"
               | Parent _ -> "parent account id (omit for top-level)"
               | Subtype _ -> "account subtype (Cash, Investment, etc.)"
   ```

2. `handleCreate` function:
   - Extract args: code, name, typeId, parentId (optional), subtype string (optional)
   - Parse subtype string via `AccountSubType.fromDbString` → error on invalid parse
   - Build `CreateAccountCommand`
   - Open connection, begin transaction
   - Call `AccountService.createAccount txn cmd`
   - On `Ok acct` → output via `OutputFormatter` (text or JSON), commit, exit 0
   - On `Error errs` → write errors to stderr, exit 1

3. Register in `AccountArgs`:
   ```fsharp
   | [<CliPrefix(CliPrefix.None)>] Create of ParseResults<AccountCreateArgs>
   ```

4. Add dispatch case:
   ```fsharp
   | Some (Create createArgs) -> handleCreate isJson createArgs
   ```

**Verification:** `leobloom account create --help` shows all flags. Manual smoke test with
valid args creates account.

### Phase 3: Tests

**What:** Add tests covering all AC #10 scenarios.

**Files modified/created:**
- `Src/LeoBloom.Tests/AccountCreateTests.fs` — Service-layer tests for subtype threading
- `Src/LeoBloom.Tests/AccountCommandsTests.fs` — CLI-level tests for the create command

**Test cases (maps to AC #10):**

| # | Test | Layer | Expected |
|---|------|-------|----------|
| 1 | Create with valid subtype (e.g. Cash for Asset) | Service | Ok, subType = Some Cash |
| 2 | Create without subtype | Service | Ok, subType = None |
| 3 | Invalid subtype for type (e.g. Cash for Expense) | Service | Error with validation message |
| 4 | Duplicate code rejection | Service | Error "already exists" |
| 5 | Missing parent rejection (parentId points to nonexistent) | Service | Error "does not exist" |
| 6 | CLI: create with all flags → exit 0, output shows details | CLI | Exit 0, stdout has id/code/name/type/subtype |
| 7 | CLI: create with invalid subtype → exit 1 | CLI | Exit 1, stderr has error |
| 8 | CLI: create missing mandatory flags → exit 2 | CLI | Exit 2 (Argu parse error) |

**Note:** Service-layer tests use transactions + rollback. CLI tests use `CliRunner.run`.
Existing `AccountCrudTests.fs` callers of `createAccount` need updating to include
`subType = None` in command construction.

**Verification:** `dotnet test` passes all new and existing tests.

## Acceptance Criteria Traceability

| AC | Covered By |
|----|------------|
| 1. CLI signature `--code --name --type --parent --subtype` | Phase 2 (Argu DU) |
| 2. `--code` and `--name` mandatory | Phase 2 (Argu `[<Mandatory>]`) |
| 3. `--parent` optional | Phase 2 (no `[<Mandatory>]` on Parent) |
| 4. `--subtype` validated against type | Phase 1 (service), Phase 2 (CLI parse) |
| 5. `CreateAccountCommand` updated with subType | Phase 1 |
| 6. `AccountService.createAccount` persists subtype | Phase 1 |
| 7. `AccountRepository.create` includes subtype in INSERT | Phase 1 |
| 8. Duplicate code → clear error | Already exists; tested in Phase 3 |
| 9. Output shows created account details | Phase 2 (OutputFormatter already handles it) |
| 10. Tests cover all scenarios | Phase 3 |

## Risks

- **Account type name lookup:** `isValidSubType` takes a type *name* string, but the CLI
  and command only have `accountTypeId`. Need to verify there's a repo method to look up
  the type name from the ID. If not, add a small helper (e.g. `findAccountTypeNameById`).
  Low risk — the table is tiny and the query is trivial.

- **Existing callers of `CreateAccountCommand`:** Adding `subType` field changes the record
  shape. All existing construction sites need `subType = None` added. Grep for
  `CreateAccountCommand` to find them (tests, seeds, etc.). Straightforward mechanical fix.

- **`--type` flag name collision with Argu:** The word "Type" is an F# keyword. May need
  `CustomCommandLine("--type")` attribute or a different DU case name like `AccountType`.
  Check Argu docs. If collision, use `TypeId` as DU case with `CustomCommandLine("--type")`.

## Out of Scope

- Account update command (that's P076)
- Batch account creation / COA expansion scripts (separate work after this ships)
- Any changes to account list/show/balance commands
- Migration changes (account_subtype column already exists)
