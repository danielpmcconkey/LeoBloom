-- MIGRONDI:NAME=1712000008000_CreateJournalEntryReference.sql
-- MIGRONDI:TIMESTAMP=1712000008000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ledger.journal_entry_reference (
    id               serial         PRIMARY KEY,
    journal_entry_id integer        NOT NULL REFERENCES ledger.journal_entry(id) ON DELETE RESTRICT,
    reference_type   varchar(30)    NOT NULL,
    reference_value  varchar(200)   NOT NULL,
    created_at       timestamptz    NOT NULL DEFAULT now()
);

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ledger.journal_entry_reference;
