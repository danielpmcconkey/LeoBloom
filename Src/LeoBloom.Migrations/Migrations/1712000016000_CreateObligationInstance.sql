-- MIGRONDI:NAME=1712000016000_CreateObligationInstance.sql
-- MIGRONDI:TIMESTAMP=1712000016000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ops.obligation_instance (
    id                      serial          PRIMARY KEY,
    obligation_agreement_id integer         NOT NULL REFERENCES ops.obligation_agreement(id) ON DELETE RESTRICT,
    name                    varchar(100)    NOT NULL,
    status_id               integer         NOT NULL REFERENCES ops.obligation_status(id) ON DELETE RESTRICT,
    amount                  numeric(12,2),
    expected_date           date            NOT NULL,
    confirmed_date          date,
    due_date                date,
    document_path           varchar(500),
    journal_entry_id        integer         REFERENCES ledger.journal_entry(id) ON DELETE RESTRICT,
    notes                   text,
    is_active               boolean         NOT NULL DEFAULT true,
    created_at              timestamptz     NOT NULL DEFAULT now(),
    modified_at             timestamptz     NOT NULL DEFAULT now()
);

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ops.obligation_instance;
