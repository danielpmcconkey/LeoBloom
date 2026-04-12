-- MIGRONDI:NAME=1712000027000_AddAdjustmentForPeriodId.sql
-- MIGRONDI:TIMESTAMP=1712000027000
-- ---------- MIGRONDI:UP ----------

-- P083: Add adjustment_for_period_id column to ledger.journal_entry.
-- Nullable FK to ledger.fiscal_period(id). Existing rows get NULL (default).
-- ON DELETE RESTRICT prevents deleting a period that has adjustment-tagged JEs.

ALTER TABLE ledger.journal_entry
    ADD COLUMN adjustment_for_period_id integer NULL
        REFERENCES ledger.fiscal_period(id) ON DELETE RESTRICT;

-- ---------- MIGRONDI:DOWN ----------

ALTER TABLE ledger.journal_entry
    DROP COLUMN IF EXISTS adjustment_for_period_id;
