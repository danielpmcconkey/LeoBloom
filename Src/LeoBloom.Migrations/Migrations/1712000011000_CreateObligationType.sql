-- MIGRONDI:NAME=1712000011000_CreateObligationType.sql
-- MIGRONDI:TIMESTAMP=1712000011000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ops.obligation_type (
    id   serial      PRIMARY KEY,
    name varchar(20) UNIQUE NOT NULL
);

INSERT INTO ops.obligation_type (name) VALUES
    ('receivable'),
    ('payable');

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ops.obligation_type;
