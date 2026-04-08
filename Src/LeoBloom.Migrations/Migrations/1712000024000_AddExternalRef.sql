-- MIGRONDI:NAME=1712000024000_AddExternalRef.sql
-- MIGRONDI:TIMESTAMP=1712000024000
-- ---------- MIGRONDI:UP ----------

ALTER TABLE ledger.account ADD COLUMN external_ref varchar(50);

-- ---------- MIGRONDI:DOWN ----------

ALTER TABLE ledger.account DROP COLUMN external_ref;
