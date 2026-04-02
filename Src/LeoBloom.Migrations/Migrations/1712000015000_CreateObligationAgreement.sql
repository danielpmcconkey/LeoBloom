-- MIGRONDI:NAME=1712000015000_CreateObligationAgreement.sql
-- MIGRONDI:TIMESTAMP=1712000015000
-- ---------- MIGRONDI:UP ----------

CREATE TABLE ops.obligation_agreement (
    id                  serial          PRIMARY KEY,
    name                varchar(100)    NOT NULL,
    obligation_type_id  integer         NOT NULL REFERENCES ops.obligation_type(id) ON DELETE RESTRICT,
    counterparty        varchar(100),
    amount              numeric(12,2),
    cadence_id          integer         NOT NULL REFERENCES ops.cadence(id) ON DELETE RESTRICT,
    expected_day        integer,
    payment_method_id   integer         REFERENCES ops.payment_method(id) ON DELETE RESTRICT,
    source_account_id   integer         REFERENCES ledger.account(id) ON DELETE RESTRICT,
    dest_account_id     integer         REFERENCES ledger.account(id) ON DELETE RESTRICT,
    is_active           boolean         NOT NULL DEFAULT true,
    notes               text,
    created_at          timestamptz     NOT NULL DEFAULT now(),
    modified_at         timestamptz     NOT NULL DEFAULT now()
);

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS ops.obligation_agreement;
