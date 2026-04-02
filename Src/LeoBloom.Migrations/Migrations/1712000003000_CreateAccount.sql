-- MIGRONDI:NAME=1712000003000_CreateAccount.sql
-- MIGRONDI:TIMESTAMP=1712000003000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ledger.account (
    id              serial          PRIMARY KEY,
    code            varchar(10)     UNIQUE NOT NULL,
    name            varchar(100)    NOT NULL,
    account_type_id integer         NOT NULL REFERENCES ledger.account_type(id) ON DELETE RESTRICT,
    parent_code     varchar(10)     REFERENCES ledger.account(code) ON DELETE RESTRICT,
    is_active       boolean         NOT NULL DEFAULT true,
    created_at      timestamptz     NOT NULL DEFAULT now(),
    modified_at     timestamptz     NOT NULL DEFAULT now()
);

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ledger.account;
