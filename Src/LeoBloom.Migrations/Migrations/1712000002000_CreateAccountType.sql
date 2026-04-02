-- MIGRONDI:NAME=1712000002000_CreateAccountType.sql
-- MIGRONDI:TIMESTAMP=1712000002000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ledger.account_type (
    id          serial       PRIMARY KEY,
    name        varchar(20)  UNIQUE NOT NULL,
    normal_balance varchar(6) NOT NULL
);

INSERT INTO ledger.account_type (name, normal_balance) VALUES
    ('asset',     'debit'),
    ('liability', 'credit'),
    ('equity',    'credit'),
    ('revenue',   'credit'),
    ('expense',   'debit');

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ledger.account_type;
