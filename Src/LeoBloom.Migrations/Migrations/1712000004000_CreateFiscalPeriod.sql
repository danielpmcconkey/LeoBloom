-- MIGRONDI:NAME=1712000004000_CreateFiscalPeriod.sql
-- MIGRONDI:TIMESTAMP=1712000004000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ledger.fiscal_period (
    id          serial      PRIMARY KEY,
    period_key  varchar(7)  UNIQUE NOT NULL,
    start_date  date        NOT NULL,
    end_date    date        NOT NULL,
    is_open     boolean     NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now()
);

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ledger.fiscal_period;
