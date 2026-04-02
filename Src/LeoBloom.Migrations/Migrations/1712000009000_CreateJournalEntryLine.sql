-- MIGRONDI:NAME=1712000009000_CreateJournalEntryLine.sql
-- MIGRONDI:TIMESTAMP=1712000009000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ledger.journal_entry_line (
    id               serial         PRIMARY KEY,
    journal_entry_id integer        NOT NULL REFERENCES ledger.journal_entry(id) ON DELETE RESTRICT,
    account_id       integer        NOT NULL REFERENCES ledger.account(id) ON DELETE RESTRICT,
    amount           numeric(12,2)  NOT NULL,
    entry_type       varchar(6)     NOT NULL,
    memo             varchar(300)
);

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ledger.journal_entry_line;
