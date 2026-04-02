-- MIGRONDI:NAME=1712000017000_CreateTransfer.sql
-- MIGRONDI:TIMESTAMP=1712000017000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ops.transfer (
    id                  serial          PRIMARY KEY,
    from_account_id     integer         NOT NULL REFERENCES ledger.account(id) ON DELETE RESTRICT,
    to_account_id       integer         NOT NULL REFERENCES ledger.account(id) ON DELETE RESTRICT,
    amount              numeric(12,2)   NOT NULL,
    status              varchar(20)     NOT NULL DEFAULT 'initiated',
    initiated_date      date            NOT NULL,
    expected_settlement date,
    confirmed_date      date,
    journal_entry_id    integer         REFERENCES ledger.journal_entry(id) ON DELETE RESTRICT,
    description         varchar(300),
    is_active           boolean         NOT NULL DEFAULT true,
    created_at          timestamptz     NOT NULL DEFAULT now(),
    modified_at         timestamptz     NOT NULL DEFAULT now()
);

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ops.transfer;
