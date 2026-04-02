-- MIGRONDI:NAME=1712000014000_CreatePaymentMethod.sql
-- MIGRONDI:TIMESTAMP=1712000014000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ops.payment_method (
    id   serial      PRIMARY KEY,
    name varchar(30) UNIQUE NOT NULL
);

INSERT INTO ops.payment_method (name) VALUES
    ('autopay_pull'),
    ('ach'),
    ('zelle'),
    ('cheque'),
    ('bill_pay'),
    ('manual');

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ops.payment_method;
