-- MIGRONDI:NAME=1712000020000_AddSecondaryIndexes.sql
-- MIGRONDI:TIMESTAMP=1712000020000
-- ---------- MIGRONDI:UP ----------

CREATE INDEX IF NOT EXISTS idx_jel_journal_entry_id
    ON ledger.journal_entry_line (journal_entry_id);

CREATE INDEX IF NOT EXISTS idx_jel_account_id
    ON ledger.journal_entry_line (account_id);

CREATE INDEX IF NOT EXISTS idx_je_fiscal_period_id
    ON ledger.journal_entry (fiscal_period_id);

CREATE INDEX IF NOT EXISTS idx_je_entry_date
    ON ledger.journal_entry (entry_date);

CREATE INDEX IF NOT EXISTS idx_je_voided_null
    ON ledger.journal_entry (id) WHERE voided_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_oi_agreement_id
    ON ops.obligation_instance (obligation_agreement_id);

-- ---------- MIGRONDI:DOWN ----------

DROP INDEX IF EXISTS ledger.idx_jel_journal_entry_id;
DROP INDEX IF EXISTS ledger.idx_jel_account_id;
DROP INDEX IF EXISTS ledger.idx_je_fiscal_period_id;
DROP INDEX IF EXISTS ledger.idx_je_entry_date;
DROP INDEX IF EXISTS ledger.idx_je_voided_null;
DROP INDEX IF EXISTS ops.idx_oi_agreement_id;
