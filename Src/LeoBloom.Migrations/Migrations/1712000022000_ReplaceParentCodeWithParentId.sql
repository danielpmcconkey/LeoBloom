-- MIGRONDI:NAME=1712000022000_ReplaceParentCodeWithParentId.sql
-- MIGRONDI:TIMESTAMP=1712000022000
-- ---------- MIGRONDI:UP ----------

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

-- ---------- MIGRONDI:DOWN ----------

ALTER TABLE ledger.account
    ADD COLUMN parent_code varchar(10) REFERENCES ledger.account(code) ON DELETE RESTRICT;

UPDATE ledger.account child
SET parent_code = parent.code
FROM ledger.account parent
WHERE child.parent_id = parent.id;

ALTER TABLE ledger.account DROP COLUMN parent_id;
