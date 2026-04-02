-- MIGRONDI:NAME=1712000007000_CreateJournalEntry.sql
-- MIGRONDI:TIMESTAMP=1712000007000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ledger.journal_entry (
    id              serial          PRIMARY KEY,
    entry_date      date            NOT NULL,
    description     varchar(500)    NOT NULL,
    source          varchar(50),
    fiscal_period_id integer        NOT NULL REFERENCES ledger.fiscal_period(id) ON DELETE RESTRICT,
    voided_at       timestamptz,
    void_reason     varchar(500),
    created_at      timestamptz     NOT NULL DEFAULT now(),
    modified_at     timestamptz     NOT NULL DEFAULT now()
);

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ledger.journal_entry;
