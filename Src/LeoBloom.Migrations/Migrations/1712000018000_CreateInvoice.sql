-- MIGRONDI:NAME=1712000018000_CreateInvoice.sql
-- MIGRONDI:TIMESTAMP=1712000018000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ops.invoice (
    id              serial          PRIMARY KEY,
    tenant          varchar(50)     NOT NULL,
    fiscal_period_id integer        NOT NULL REFERENCES ledger.fiscal_period(id) ON DELETE RESTRICT,
    rent_amount     numeric(12,2)   NOT NULL,
    utility_share   numeric(12,2)   NOT NULL,
    total_amount    numeric(12,2)   NOT NULL,
    generated_at    timestamptz     NOT NULL DEFAULT now(),
    document_path   varchar(500),
    notes           text,
    is_active       boolean         NOT NULL DEFAULT true,
    created_at      timestamptz     NOT NULL DEFAULT now(),
    modified_at     timestamptz     NOT NULL DEFAULT now(),
    UNIQUE (tenant, fiscal_period_id)
);

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ops.invoice;
