# P076 — Account Update CLI Command — Plan

## Objective

Add `leobloom account update <id>` CLI command to modify mutable account fields
(name, subtype, external_ref) without touching SQL directly. Follows P077's
established CLI patterns (Argu DU, handler, dispatch in `AccountCommands.fs`).

## Phases

### Phase 1: Domain & Service/Repository Layer

**What:** Add `UpdateAccountCommand` domain type, repository `update` function,
and `AccountService.updateAccount` orchestrator.

**Files modified:**

- `Src/LeoBloom.Domain/Ledger.fs` — Add `UpdateAccountCommand` record type
- `Src/LeoBloom.Ledger/AccountRepository.fs` — Add `update` function
- `Src/LeoBloom.Ledger/AccountService.fs` — Add `updateAccount` function

**Details:**

1. **Domain type** — new command record:
   ```fsharp
   type UpdateAccountCommand =
       { accountId: int
         name: string option
         subType: AccountSubType option option   // None = don't change, Some None = clear, Some (Some x) = set
         externalRef: string option option }      // same semantics
   ```
   Wait — that's over-engineered. The backlog says these are *set* operations, not
   *clear* operations. Subtype and external_ref are assigned or changed, not cleared.
   Simpler approach: use `string option` for each field (None = don't touch, Some = set).
   The handler pre-parses subtype string → `AccountSubType` before calling service.

   ```fsharp
   type UpdateAccountCommand =
       { accountId: int
         name: string option
         subType: AccountSubType option
         externalRef: string option }
   ```
   Where `None` on each field means "don't change this field."

2. **Repository `update`** — dynamic SET clause builder (same pattern as `create`'s
   dynamic INSERT). Builds SET clauses only for provided fields. Always sets
   `modified_at = now()`. Uses RETURNING to get updated record.

3. **Service `updateAccount`** — validation logic:
   - Account must exist (findById)
   - If `name` is Some, must not be blank
   - If `subType` is Some, must validate against account's type via
     `findAccountTypeNameById` + `isValidSubType`. Use `Some subType` to call
     `isValidSubType typeName (Some subType)`.
   - externalRef: no validation needed (free-text)
   - At least one field must be Some (no no-op updates) — but this is better
     enforced at CLI layer since service callers might have their own UX
   - Returns `Result<Account * Account, string list>` — **before and after** records,
     so the CLI can show the diff

**Verification:** Unit tests in Phase 3 cover all paths.

### Phase 2: CLI Command — AccountUpdateArgs + Handler

**What:** Add `Update` subcommand to account CLI.

**Files modified:**
- `Src/LeoBloom.CLI/AccountCommands.fs` — Add `AccountUpdateArgs` DU,
  `handleUpdate` function, register in `AccountArgs`, add dispatch case
- `Src/LeoBloom.CLI/OutputFormatter.fs` — Add `formatAccountUpdate` for
  before/after display + `writeAccountUpdate` convenience function

**Details:**

1. **Argu DU:**
   ```fsharp
   type AccountUpdateArgs =
       | [<MainCommand; Mandatory>] Account_Id of int
       | Name of string
       | Subtype of string
       | External_Ref of string
       | Json
       interface IArgParserTemplate with
           member this.Usage =
               match this with
               | Account_Id _ -> "Account ID to update"
               | Name _ -> "New account name"
               | Subtype _ -> "New account subtype"
               | External_Ref _ -> "External reference"
               | Json -> "Output in JSON format"
   ```

2. **`handleUpdate` function:**
   - Extract accountId (mandatory), name/subtype/externalRef (all optional)
   - Validate at least one mutable flag provided → exit 1 if not
   - Parse subtype string via `AccountSubType.fromDbString` if present → error on invalid
   - Build `UpdateAccountCommand`
   - Open connection, begin transaction
   - Call `AccountService.updateAccount txn cmd`
   - On `Ok (before, after)` → output before/after via formatter, commit, exit 0
   - On `Error errs` → write errors to stderr, rollback, exit 1

3. **Register in `AccountArgs`:**
   ```fsharp
   | [<CliPrefix(CliPrefix.None)>] Update of ParseResults<AccountUpdateArgs>
   ```

4. **Dispatch case:**
   ```fsharp
   | Some (Update updateArgs) -> handleUpdate isJson updateArgs
   ```

5. **OutputFormatter additions:**
   - `formatAccountUpdate (before: Account) (after: Account)` — shows field-by-field
     diff, only listing changed fields with `before → after` format
   - `writeAccountUpdate isJson before after` — convenience that handles JSON
     (outputs `{before: ..., after: ...}`) and human format

**Verification:** `leobloom account update --help` shows correct flags. Manual
smoke test updates an account.

### Phase 3: Tests

**What:** Service-layer and CLI-level tests.

**Files created/modified:**
- `Src/LeoBloom.Tests/AccountUpdateTests.fs` — Service-layer tests
- `Src/LeoBloom.Tests/AccountCommandsTests.fs` — CLI-level tests for update command

**Test cases:**

| # | Test | Layer | Expected |
|---|------|-------|----------|
| 1 | Update name only | Service | Ok, name changed, other fields unchanged |
| 2 | Update subtype only (valid for type) | Service | Ok, subtype changed |
| 3 | Update external_ref only | Service | Ok, externalRef changed |
| 4 | Update all three fields at once | Service | Ok, all changed |
| 5 | Invalid subtype for account type | Service | Error mentioning "subtype" |
| 6 | Blank name rejected | Service | Error mentioning "blank" |
| 7 | Nonexistent account ID | Service | Error mentioning "does not exist" |
| 8 | CLI: update with --name → exit 0, shows before/after | CLI | Exit 0, stdout shows old and new name |
| 9 | CLI: update with --json → exit 0, valid JSON | CLI | Exit 0, parseable JSON |
| 10 | CLI: update with no flags → exit 1, error | CLI | Exit 1, stderr message |
| 11 | CLI: update with invalid subtype → exit 1, error | CLI | Exit 1, stderr message |
| 12 | CLI: update nonexistent account → exit 1, error | CLI | Exit 1, stderr message |

**Note:** Service tests use connection + transaction (rollback, no commit).
CLI tests use `CliRunner.run` and clean up created accounts in `finally` blocks.
Need to register new test file in `.fsproj`.

**Verification:** `dotnet test` passes all new and existing tests.

## Acceptance Criteria Traceability

| AC (from backlog) | Covered By |
|----|------------|
| 1. `leobloom account update <id> [--name] [--subtype] [--external-ref]` | Phase 2 (Argu DU) |
| 2. At least one flag required | Phase 2 (handleUpdate validation) |
| 3. Subtype validated against account type | Phase 1 (service), Phase 2 (CLI parse) |
| 4. Immutable fields not exposed | Phase 2 (no flags for code/type/parent) |
| 5. Before/after output for changed fields | Phase 2 (formatAccountUpdate) |
| 6. Tests: valid update, invalid subtype, no-flag, immutable non-exposure | Phase 3 |

## Risks

- **`UpdateAccountCommand` field semantics:** Using `option` for "don't change"
  means we can't distinguish "set to None" from "don't change" for subtype/externalRef.
  This is fine because the backlog only requires *setting* these fields, not *clearing* them.
  If clearing is needed later, switch to a `FieldUpdate<'T>` DU. Not building that now.

- **Service return type:** Returning `(Account * Account)` tuple (before, after)
  is slightly different from `createAccount` which returns just the created account.
  This is intentional — the update command's AC #5 requires before/after display.

- **Test file registration:** New `AccountUpdateTests.fs` must be added to the
  `.fsproj` `<Compile>` list in the correct order (after other Account test files).
  Miss this and the tests won't run.

## Out of Scope

- Clearing (unsetting) subtype or external_ref — only setting is supported
- Updating `is_active` — separate deactivation command already exists
- Batch updates
- Any changes to account list/show/balance/create commands
