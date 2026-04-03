-- MIGRONDI:NAME=1712000019000_EliminateLookupTables.sql
-- MIGRONDI:TIMESTAMP=1712000019000
-- ---------- MIGRONDI:UP ----------

-- === obligation_agreement: replace obligation_type_id with obligation_type varchar ===
ALTER TABLE ops.obligation_agreement ADD COLUMN obligation_type varchar(20);
UPDATE ops.obligation_agreement SET obligation_type = (SELECT name FROM ops.obligation_type WHERE id = obligation_type_id);
ALTER TABLE ops.obligation_agreement ALTER COLUMN obligation_type SET NOT NULL;
ALTER TABLE ops.obligation_agreement DROP CONSTRAINT obligation_agreement_obligation_type_id_fkey;
ALTER TABLE ops.obligation_agreement DROP COLUMN obligation_type_id;

-- === obligation_agreement: replace cadence_id with cadence varchar ===
ALTER TABLE ops.obligation_agreement ADD COLUMN cadence varchar(20);
UPDATE ops.obligation_agreement SET cadence = (SELECT name FROM ops.cadence WHERE id = cadence_id);
ALTER TABLE ops.obligation_agreement ALTER COLUMN cadence SET NOT NULL;
ALTER TABLE ops.obligation_agreement DROP CONSTRAINT obligation_agreement_cadence_id_fkey;
ALTER TABLE ops.obligation_agreement DROP COLUMN cadence_id;

-- === obligation_agreement: replace payment_method_id with payment_method varchar ===
ALTER TABLE ops.obligation_agreement ADD COLUMN payment_method varchar(30);
UPDATE ops.obligation_agreement SET payment_method = (SELECT name FROM ops.payment_method WHERE id = payment_method_id) WHERE payment_method_id IS NOT NULL;
ALTER TABLE ops.obligation_agreement DROP CONSTRAINT obligation_agreement_payment_method_id_fkey;
ALTER TABLE ops.obligation_agreement DROP COLUMN payment_method_id;

-- === obligation_instance: replace status_id with status varchar ===
ALTER TABLE ops.obligation_instance ADD COLUMN status varchar(20);
UPDATE ops.obligation_instance SET status = (SELECT name FROM ops.obligation_status WHERE id = status_id);
ALTER TABLE ops.obligation_instance ALTER COLUMN status SET NOT NULL;
ALTER TABLE ops.obligation_instance DROP CONSTRAINT obligation_instance_status_id_fkey;
ALTER TABLE ops.obligation_instance DROP COLUMN status_id;

-- === Drop the lookup tables (order: dependents removed first) ===
DROP TABLE ops.obligation_type;
DROP TABLE ops.obligation_status;
DROP TABLE ops.cadence;
DROP TABLE ops.payment_method;

-- ---------- MIGRONDI:DOWN ----------

-- === Recreate lookup tables with seed data ===
CREATE TABLE ops.obligation_type (
    id   serial      PRIMARY KEY,
    name varchar(20) UNIQUE NOT NULL
);
INSERT INTO ops.obligation_type (name) VALUES ('receivable'), ('payable');

CREATE TABLE ops.obligation_status (
    id   serial      PRIMARY KEY,
    name varchar(20) UNIQUE NOT NULL
);
INSERT INTO ops.obligation_status (name) VALUES ('expected'), ('in_flight'), ('confirmed'), ('posted'), ('overdue'), ('skipped');

CREATE TABLE ops.cadence (
    id   serial      PRIMARY KEY,
    name varchar(20) UNIQUE NOT NULL
);
INSERT INTO ops.cadence (name) VALUES ('monthly'), ('quarterly'), ('annual'), ('one_time');

CREATE TABLE ops.payment_method (
    id   serial      PRIMARY KEY,
    name varchar(30) UNIQUE NOT NULL
);
INSERT INTO ops.payment_method (name) VALUES ('autopay_pull'), ('ach'), ('zelle'), ('cheque'), ('bill_pay'), ('manual');

-- === obligation_instance: restore status_id ===
ALTER TABLE ops.obligation_instance ADD COLUMN status_id integer;
UPDATE ops.obligation_instance SET status_id = (SELECT id FROM ops.obligation_status WHERE name = status);
ALTER TABLE ops.obligation_instance ALTER COLUMN status_id SET NOT NULL;
ALTER TABLE ops.obligation_instance ADD CONSTRAINT obligation_instance_status_id_fkey FOREIGN KEY (status_id) REFERENCES ops.obligation_status(id) ON DELETE RESTRICT;
ALTER TABLE ops.obligation_instance DROP COLUMN status;

-- === obligation_agreement: restore payment_method_id ===
ALTER TABLE ops.obligation_agreement ADD COLUMN payment_method_id integer;
UPDATE ops.obligation_agreement SET payment_method_id = (SELECT id FROM ops.payment_method WHERE name = payment_method) WHERE payment_method IS NOT NULL;
ALTER TABLE ops.obligation_agreement ADD CONSTRAINT obligation_agreement_payment_method_id_fkey FOREIGN KEY (payment_method_id) REFERENCES ops.payment_method(id) ON DELETE RESTRICT;
ALTER TABLE ops.obligation_agreement DROP COLUMN payment_method;

-- === obligation_agreement: restore cadence_id ===
ALTER TABLE ops.obligation_agreement ADD COLUMN cadence_id integer;
UPDATE ops.obligation_agreement SET cadence_id = (SELECT id FROM ops.cadence WHERE name = cadence);
ALTER TABLE ops.obligation_agreement ALTER COLUMN cadence_id SET NOT NULL;
ALTER TABLE ops.obligation_agreement ADD CONSTRAINT obligation_agreement_cadence_id_fkey FOREIGN KEY (cadence_id) REFERENCES ops.cadence(id) ON DELETE RESTRICT;
ALTER TABLE ops.obligation_agreement DROP COLUMN cadence;

-- === obligation_agreement: restore obligation_type_id ===
ALTER TABLE ops.obligation_agreement ADD COLUMN obligation_type_id integer;
UPDATE ops.obligation_agreement SET obligation_type_id = (SELECT id FROM ops.obligation_type WHERE name = obligation_type);
ALTER TABLE ops.obligation_agreement ALTER COLUMN obligation_type_id SET NOT NULL;
ALTER TABLE ops.obligation_agreement ADD CONSTRAINT obligation_agreement_obligation_type_id_fkey FOREIGN KEY (obligation_type_id) REFERENCES ops.obligation_type(id) ON DELETE RESTRICT;
ALTER TABLE ops.obligation_agreement DROP COLUMN obligation_type;
