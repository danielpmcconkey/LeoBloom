-- MIGRONDI:NAME=1712000026000_FiscalPeriodCloseMetadata.sql
-- MIGRONDI:TIMESTAMP=1712000026000
-- ---------- MIGRONDI:UP ----------

ALTER TABLE ledger.fiscal_period
    ADD COLUMN closed_at timestamptz NULL DEFAULT NULL,
    ADD COLUMN closed_by varchar(50) NULL DEFAULT NULL,
    ADD COLUMN reopened_count integer NOT NULL DEFAULT 0;

UPDATE ledger.fiscal_period
SET closed_at = now(), closed_by = 'migration'
WHERE is_open = false;

CREATE TABLE ledger.fiscal_period_audit (
    id serial PRIMARY KEY,
    fiscal_period_id integer NOT NULL REFERENCES ledger.fiscal_period(id) ON DELETE RESTRICT,
    action varchar(20) NOT NULL,
    actor varchar(50) NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    note text
);

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE ledger.fiscal_period_audit;

ALTER TABLE ledger.fiscal_period
    DROP COLUMN IF EXISTS closed_at,
    DROP COLUMN IF EXISTS closed_by,
    DROP COLUMN IF EXISTS reopened_count;
