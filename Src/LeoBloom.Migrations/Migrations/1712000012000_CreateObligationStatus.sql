-- MIGRONDI:NAME=1712000012000_CreateObligationStatus.sql
-- MIGRONDI:TIMESTAMP=1712000012000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ops.obligation_status (
    id   serial      PRIMARY KEY,
    name varchar(20) UNIQUE NOT NULL
);

INSERT INTO ops.obligation_status (name) VALUES
    ('expected'),
    ('in_flight'),
    ('confirmed'),
    ('posted'),
    ('overdue'),
    ('skipped');

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ops.obligation_status;
