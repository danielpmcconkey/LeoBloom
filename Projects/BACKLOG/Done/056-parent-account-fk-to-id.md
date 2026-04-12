# 056 — Replace parent_code with parent_id in ledger.account

**Epic:** Technical Debt
**Depends On:** None
**Status:** Not started
**Priority:** Low

---

The `ledger.account` table uses `parent_code` (varchar, references the
account code) instead of `parent_id` (integer, references the account id)
for the parent account relationship. This is a foreign key on a business
value instead of a surrogate key.

It works because account codes are stable and never renamed, but it's
a design smell — every other relationship in the schema uses integer IDs.

**What ships:**

1. Migration: add `parent_id` column (nullable int, FK to `ledger.account.id`)
2. Migration: backfill `parent_id` from `parent_code` via a JOIN
3. Migration: drop `parent_code` column
4. Update `Account` domain type: `parentCode: string option` becomes `parentId: int option`
5. Update all repository queries that read/write `parent_code`
6. Update OutputFormatter to resolve parent ID to code/name for display
7. Update seed data / migrations that set `parent_code`

**Why it's low priority:** Nothing is broken. Account codes don't change.
No cascading update risk today. This is purely about schema consistency.
