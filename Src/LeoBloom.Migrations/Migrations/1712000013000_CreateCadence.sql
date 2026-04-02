-- MIGRONDI:NAME=1712000013000_CreateCadence.sql
-- MIGRONDI:TIMESTAMP=1712000013000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ops.cadence (
    id   serial      PRIMARY KEY,
    name varchar(20) UNIQUE NOT NULL
);

INSERT INTO ops.cadence (name) VALUES
    ('monthly'),
    ('quarterly'),
    ('annual'),
    ('one_time');

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ops.cadence;
