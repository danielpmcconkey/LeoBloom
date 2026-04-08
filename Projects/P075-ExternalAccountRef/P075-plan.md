# P075 — External Account Reference: Plan

## Objective

Add a nullable `external_ref varchar(50)` column to `ledger.account` so Hobson
can store external financial-institution account numbers (e.g., Fidelity Z08806967,
Ally x0412) directly on the ledger account. This supports CSV import mapping,
reconciliation, and unambiguous identification when account names are similar.

## Phases

### Phase 1: Migration

**What:** Add the column via a Migrondi SQL migration.

**File created:**
- `Src/LeoBloom.Migrations/Migrations/1712000024000_AddExternalRef.sql`

**SQL (UP):**
```sql
ALTER TABLE ledger.account ADD COLUMN external_ref varchar(50);
```

**SQL (DOWN):**
```sql
ALTER TABLE ledger.account DROP COLUMN external_ref;
```

No seed data, no backfill, no index, no uniqueness constraint.

**Verification:** Run migrations. `\d ledger.account` shows `external_ref` as
nullable varchar(50).

---

### Phase 2: Domain Type + Repository

**What:** Thread `externalRef` through the domain type, repository read/write,
and CLI create command.

**Files modified:**

1. **`Src/LeoBloom.Domain/Ledger.fs`**
   - `Account` record: add `externalRef: string option` after `subType`.
   - `CreateAccountCommand` record: add `externalRef: string option`.

2. **`Src/LeoBloom.Ledger/AccountRepository.fs`**
   - `readAccount`: read `external_ref` at ordinal 9 (shift `isActive`,
     `createdAt`, `modifiedAt` to ordinals 7→8→9 becomes 8→9→10... wait —
     actually we insert the new column in the SELECT list).
   - `accountSelectSql`: add `external_ref` to SELECT between `account_subtype`
     and `is_active`.
   - `readAccount`: read new field at ordinal 6, shift `isActive` to 7,
     `createdAt` to 8, `modifiedAt` to 9.
   - `create`: add `external_ref` to the INSERT variations (expand the
     match matrix or refactor to parameterized nullable approach).
   - `updateName`, `deactivate`: RETURNING clauses get `external_ref` added
     (same column position as the SELECT).

3. **`Src/LeoBloom.Ledger/AccountService.fs`**
   - `createAccount`: pass `cmd.externalRef` to `AccountRepository.create`.
   - No validation needed — any account type can have it, it's optional, and
     there are no format constraints.

4. **`Src/LeoBloom.CLI/AccountCommands.fs`**
   - `AccountCreateArgs`: add `| External_Ref of string` with usage help.
   - `handleCreate`: parse the new arg, include in `CreateAccountCommand`.

**Verification:** `dotnet build` succeeds. Existing tests pass (field is optional
and defaults to `None` in all existing test helpers).

---

## Acceptance Criteria

- [ ] `ledger.account` has nullable `external_ref varchar(50)` column (migration).
- [ ] `Account` domain type has `externalRef: string option`.
- [ ] `CreateAccountCommand` has `externalRef: string option`.
- [ ] Repository reads/writes `external_ref` correctly.
- [ ] CLI `account create` accepts optional `--external-ref` flag.
- [ ] Existing tests pass without modification (field optional throughout).
- [ ] `dotnet build` clean, no warnings from this change.

## Risks

- **Repository ordinal shift:** Adding a column to `accountSelectSql` shifts
  all subsequent ordinals in `readAccount`. Must update all ordinal references
  in lockstep. Low risk — the mapping is explicit and compiler will catch
  type mismatches.
- **INSERT SQL matrix explosion:** `AccountRepository.create` already has a
  4-way match on `parentId × subType`. Adding `externalRef` would make it 8-way.
  Consider refactoring to build the SQL dynamically with a param list instead.
  This is builder's call — either approach works for a field this simple.

## Out of Scope

- No uniqueness constraint (by design — FIs reuse numbers).
- No format validation (varchar is sufficient; no regex on content).
- No seed data or backfill — Hobson populates prod directly.
- No new tests specifically for `external_ref` — existing test coverage confirms
  the field doesn't break anything. If the builder wants to add a quick
  round-trip test, that's fine but not required.
