# Project 069 — Account CRUD Behavioral Specs — Plan

## Objective

Build service-layer CRUD for accounts (create, update, deactivate) and write
behavioral specs proving the write path works. Today there is no `AccountService`
or `AccountRepository` — account inserts are raw SQL scattered across test helpers
and seed migrations. This project fills that gap with a proper service/repo pair
and 9 behavioral scenarios exercising them.

## Design Decisions

**Deactivate with child accounts (Scenario 8):** Reject. Cascading deactivation
is dangerous for a chart of accounts — a single slip could hide an entire subtree.
The user must deactivate children first, bottom-up. This is the conservative,
accounting-safe choice.

**Deactivate with posted journal entries (Scenario 9):** Allow. An account that
has been used historically can still be deactivated — it just means no *new*
entries can target it. Historical entries remain intact. This matches the
behavior already enforced in `JournalEntryService.lookupAccountActivity` which
checks `is_active` at posting time.

## Phases

### Phase 1: AccountRepository + AccountService

**What:** New files `AccountRepository.fs` and `AccountService.fs` in
`Src/LeoBloom.Ledger/`.

**AccountRepository** (raw SQL, caller-provided `NpgsqlTransaction`):
- `findById : txn -> int -> Account option`
- `findByCode : txn -> string -> Account option`
- `create : txn -> code -> name -> accountTypeId -> parentId option -> Account`
- `updateName : txn -> int -> string -> Account`
- `deactivate : txn -> int -> Account`
- `hasChildren : txn -> int -> bool`
- `hasPostedEntries : txn -> int -> bool`

**AccountService** (validation + delegation to repo):
- `createAccount : txn -> CreateAccountCommand -> Result<Account, string list>`
  - Validates: code non-blank, name non-blank, account_type_id exists,
    parent_id (if given) exists and is active, delegates to repo.
  - Catches `23505` (unique violation on code) → friendly error.
- `updateAccountName : txn -> UpdateAccountNameCommand -> Result<Account, string list>`
  - Validates: account exists, new name non-blank.
- `deactivateAccount : txn -> DeactivateAccountCommand -> Result<Account, string list>`
  - Validates: account exists, is currently active.
  - Rejects if `hasChildren` (Scenario 8 decision).
  - Allows even if `hasPostedEntries` (Scenario 9 decision).

**Domain commands** (added to `Ledger.fs` or a new section):
```fsharp
type CreateAccountCommand =
    { code: string; name: string; accountTypeId: int; parentId: int option }

type UpdateAccountNameCommand =
    { accountId: int; name: string }

type DeactivateAccountCommand =
    { accountId: int }
```

**Files modified:**
- `Src/LeoBloom.Domain/Ledger.fs` — add command types
- `Src/LeoBloom.Ledger/LeoBloom.Ledger.fsproj` — add new `.fs` files to compile order

**Files created:**
- `Src/LeoBloom.Ledger/AccountRepository.fs`
- `Src/LeoBloom.Ledger/AccountService.fs`

**Verification:** `dotnet build Src/LeoBloom.Ledger/` succeeds. No tests yet.

### Phase 2: Behavioral Spec (Gherkin)

**What:** New file `Specs/Behavioral/AccountCrud.feature` with 9 scenarios
using tag prefix `@FT-AC-NNN`.

**Tag mapping:**
| Tag | Scenario |
|---|---|
| `@FT-AC-001` | Create account happy path |
| `@FT-AC-002` | Create with invalid account_type_id |
| `@FT-AC-003` | Create with duplicate code |
| `@FT-AC-004` | Create with invalid parent_id |
| `@FT-AC-005` | Create with inactive parent |
| `@FT-AC-006` | Update account name |
| `@FT-AC-007` | Deactivate account |
| `@FT-AC-008` | Deactivate account with child accounts — rejected |
| `@FT-AC-009` | Deactivate account with posted journal entries — allowed |

**Convention notes:**
- Background step: `Given the ledger schema exists for account crud`
- Test data prefix: `crud-test` (unique prefix per test via `TestData.uniquePrefix()`)

**Files created:**
- `Specs/Behavioral/AccountCrud.feature`

**Verification:** File exists, well-formed Gherkin, 9 scenarios, tags match spec.

### Phase 3: Test Implementation

**What:** New file `Src/LeoBloom.Tests/AccountCrudTests.fs` implementing all
9 scenarios against `AccountService`.

**Pattern:** Follow `FiscalPeriodManagementTests.fs` exactly:
- Each test opens a connection + transaction
- Uses `TestData.uniquePrefix()` for isolation
- Calls service methods, asserts on `Result<_, string list>`
- Uses `[<Trait("GherkinId", "FT-AC-NNN")>]` for traceability
- Transaction rolls back on dispose (no committed test data)

**Fiscal period year:** For Scenario 9 (posted entries), we need a fiscal period
and a journal entry. Assign a unique year per the project's year-reservation
convention (check DSWF or memory for the next available year).

**Files created:**
- `Src/LeoBloom.Tests/AccountCrudTests.fs`

**Files modified:**
- `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` — add `AccountCrudTests.fs`

**Verification:** `dotnet test --filter "GherkinId~FT-AC"` — all 9 pass.

## Acceptance Criteria

- [ ] AC-1: `AccountService.createAccount` with valid data returns `Ok account` with
      matching code, name, type, parent, and `isActive = true`
- [ ] AC-2: `AccountService.createAccount` rejects invalid type (error mentions type),
      duplicate code (error mentions code), invalid parent (error mentions parent),
      and inactive parent (error mentions inactive)
- [ ] AC-3a: `AccountService.deactivateAccount` sets `isActive = false`
- [ ] AC-3b: Deactivating an account with children returns `Error` (rejected)
- [ ] AC-3c: Deactivating an account with posted journal entries returns `Ok` (allowed)
- [ ] AC-4: All 9 test scenarios call `AccountService` methods, not raw SQL

## Risks

| Risk | Mitigation |
|---|---|
| F# compile order sensitivity | Place `AccountRepository.fs` before `AccountService.fs` in fsproj; both after existing repo/service files |
| `hasPostedEntries` query could be slow on large data | Index already exists: `journal_entry_line(account_id)` (migration 1712000020000) |
| Fiscal period year collision in Scenario 9 | Reserve a year in the year-assignment memory before writing tests |

## Out of Scope

- CLI commands for account create/update/deactivate (separate card per backlog)
- Account subtype validation on create (existing `AccountSubType.isValidSubType`
  can be wired in later — keep create simple for now)
- Cascade deactivation (explicitly rejected in design decision above)
- Account deletion (the schema uses `ON DELETE RESTRICT` — deletion is not a
  supported operation)
