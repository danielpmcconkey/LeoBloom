# P056 — Replace parent_code with parent_id — Plan

## Objective

Replace `parent_code` (varchar FK on business key) with `parent_id` (int FK on surrogate key) in `ledger.account`, aligning the parent-child relationship with every other FK in the schema. This is a schema consistency cleanup — nothing is functionally broken.

## Phases

### Phase 1: Migration — Add, Backfill, Drop

**What:** A single new migration that adds `parent_id`, backfills it from `parent_code`, and drops `parent_code`. Not a zero-downtime system, so no multi-step deploy needed.

**Files created:**
- `Src/LeoBloom.Migrations/Migrations/1712000022000_ReplaceParentCodeWithParentId.sql`

**Migration SQL (UP):**
```sql
-- 1. Add nullable parent_id column with FK to account.id
ALTER TABLE ledger.account
    ADD COLUMN parent_id integer REFERENCES ledger.account(id) ON DELETE RESTRICT;

-- 2. Backfill parent_id from parent_code
UPDATE ledger.account child
SET parent_id = parent.id
FROM ledger.account parent
WHERE child.parent_code = parent.code;

-- 3. Drop old column (and its implicit FK constraint)
ALTER TABLE ledger.account DROP COLUMN parent_code;
```

**Migration SQL (DOWN):**
```sql
ALTER TABLE ledger.account
    ADD COLUMN parent_code varchar(10) REFERENCES ledger.account(code) ON DELETE RESTRICT;

UPDATE ledger.account child
SET parent_code = parent.code
FROM ledger.account parent
WHERE child.parent_id = parent.id;

ALTER TABLE ledger.account DROP COLUMN parent_id;
```

**Verification:** Run migration up/down locally. Confirm `parent_id` values match expected parent IDs for seeded data.

---

### Phase 2: Seed Data

**What:** Update the production seed migration and dev seed file to use `parent_id` via subselects instead of `parent_code` string literals.

**Files modified:**
- `Src/LeoBloom.Migrations/Migrations/1712000006000_SeedChartOfAccounts.sql`
- `Src/LeoBloom.Migrations/Seeds/dev/020-chart-of-accounts.sql`

**Approach:** Replace `parent_code` column references with `parent_id`, and replace string literal values (e.g., `'1000'`) with subselects: `(SELECT id FROM ledger.account WHERE code = '1000')`.

**Important:** The seed migration's DOWN section (DELETE) doesn't reference `parent_code`, so only the UP INSERTs need updating. The dev seed's `ON CONFLICT DO UPDATE` clauses need `parent_id` instead of `parent_code`.

**Verification:** Run seeds against a fresh DB. Confirm all accounts have correct `parent_id` values.

---

### Phase 3: Domain Type + Repository Layer

**What:** Update the F# domain type and all repository queries.

**Files modified:**
- `Src/LeoBloom.Domain/Ledger.fs` — `Account` type: `parentCode: string option` → `parentId: int option`
- `Src/LeoBloom.Ledger/AccountBalanceRepository.fs`:
  - `readAccount` helper: column index 4 reads `parent_id` as `int option` instead of `string option`
  - `listAccounts` SQL: `a.parent_code` → `a.parent_id`
  - `findAccountById` SQL: same
  - `findAccountByCode` SQL: same
- `Src/LeoBloom.Ledger/SubtreePLRepository.fs`:
  - Recursive CTE: `a.parent_code = s.code` → `a.parent_id = s.id`
  - CTE anchor: `SELECT code FROM ledger.account WHERE code = @root_code` → `SELECT id, code FROM ledger.account WHERE code = @root_code`
  - CTE recursive: `SELECT a.code FROM ledger.account a JOIN subtree s ON a.parent_id = s.id`

**Key detail — readAccount column mapping:**
- Current: index 4 = `parent_code` (string nullable) → reads with `GetString`
- New: index 4 = `parent_id` (int nullable) → reads with `GetInt32`

**Verification:** Project compiles. Existing tests pass (after Phase 4 updates).

---

### Phase 4: OutputFormatter

**What:** Update the account detail display. Currently shows `Parent Code: 1000`. After change, needs to resolve `parent_id` to something useful.

**File modified:**
- `Src/LeoBloom.CLI/OutputFormatter.fs` (line ~270)

**Approach:** Change label from "Parent Code" to "Parent ID" and display the integer (or "(none)"). The formatter already has access to the `Account` record — it just displays the field. Displaying the raw ID is consistent with how `Type ID` is already shown on line 268.

**Alternative considered:** Resolve parent ID to code+name via a DB lookup. Rejected — the formatter is a pure function that takes an `Account` record, not a DB connection. Changing its signature would be scope creep. The parent can be looked up separately if needed.

**Verification:** `dotnet build` passes. Manual smoke test of `account show` command.

---

### Phase 5: Tests

**What:** Update all test code that references `parent_code` / `parentCode`.

**Files modified:**
- `Src/LeoBloom.Tests/TestHelpers.fs`:
  - `insertAccountWithParent`: change SQL from `parent_code` to `parent_id`, parameter from `parentCode: string` to `parentId: int`, param type from string to int
  - `insertAccountWithParentAndSubType`: same changes
- `Src/LeoBloom.Tests/LedgerConstraintTests.fs`:
  - `account parent_code must reference a valid account code` → rename + update SQL to use `parent_id` with invalid int ID
  - `account parent_code is nullable` → rename + confirm still works (parent_id is also nullable)
- `Src/LeoBloom.Tests/DeleteRestrictionTests.fs`:
  - `cannot delete account with dependent child account via parent_code` → update SQL to use `parent_id` instead of `parent_code`
- `Src/LeoBloom.Tests/SeedRunnerTests.fs`:
  - Referential integrity check (lines 128-135): rewrite SQL to check `parent_id` references `id` instead of `parent_code` references `code`
  - Comment on line 149: update "parent_code" reference in comment
- `Src/LeoBloom.Tests/SubtreePLTests.fs`:
  - All `insertAccountWithParent` calls currently pass `parentCode` (a string like `"XXPA"`). These must change to pass the `parentId` (the int returned by `insertAccount`).
  - **This is the biggest test change** — every test in SubtreePLTests creates a parent account and then passes its code to child inserts. The parent's `id` is already captured in `_parentAcct`, so the change is: use the returned `int` instead of the code string.

**Verification:** `dotnet test` — all tests pass.

---

## Execution Order

Phases 1-2 (migration + seeds) can be written first since they're pure SQL.
Phase 3 (domain + repo) must come next — it breaks compilation until Phase 4-5 are done.
Phases 3, 4, 5 should be committed together or in rapid sequence — the codebase won't compile between them.

**Recommended commit sequence:**
1. Migration file (Phase 1) — standalone, doesn't break anything
2. Seed updates (Phase 2) — standalone SQL
3. Domain + Repo + Formatter + Tests (Phases 3-5) — single commit, all F# changes together

---

## Acceptance Criteria

- [ ] `ledger.account` has `parent_id integer REFERENCES ledger.account(id) ON DELETE RESTRICT` — no `parent_code` column
- [ ] `Account` domain type has `parentId: int option`, no `parentCode`
- [ ] All repository SQL queries use `parent_id` instead of `parent_code`
- [ ] Recursive CTE in SubtreePLRepository joins on `parent_id = id` instead of `parent_code = code`
- [ ] Seed migration inserts use subselects for `parent_id`
- [ ] Dev seed file uses `parent_id` with subselects
- [ ] OutputFormatter displays parent ID (not parent code)
- [ ] All existing tests updated and passing
- [ ] Migration has a working DOWN path
- [ ] `dotnet build` succeeds with zero warnings related to this change
- [ ] `dotnet test` passes

## Risks

- **Seed ordering dependency:** The production seed migration inserts parent-first to satisfy FK. With `parent_id` via subselects, the parent row must exist at INSERT time. Same constraint, different syntax — the existing parent-first ordering already handles this.
- **SubtreePL CTE correctness:** The recursive CTE is the trickiest query. Changing the join condition from `parent_code = code` to `parent_id = id` is logically equivalent, but the CTE's SELECT list needs `id` in addition to (or instead of) `code` for the recursive join. Must verify the anchor and recursive terms both project the right columns.
- **Test helper signature change cascade:** `insertAccountWithParent` changes from `parentCode: string` to `parentId: int`. Every call site must be updated. This is mechanical but touching ~15+ call sites means easy to miss one. Compiler will catch any misses.

## Out of Scope

- Resolving parent ID to code/name in OutputFormatter (would require DB access in a pure function)
- Adding an index on `parent_id` (low-volume table, not needed)
- Changing `SubtreePLReport.rootAccountCode` to `rootAccountId` (the subtree is still looked up by code, which is fine)
- Any changes to the orphaned posting detection diagnostic (P035) — verified it doesn't reference `parent_code`
