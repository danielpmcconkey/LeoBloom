# 044 — Database Indexes Migration

**Epic:** K — Code Audit Remediation
**Depends On:** 001
**Status:** Not started

---

Single migration file adding secondary indexes on frequently joined and
filtered columns. PostgreSQL does NOT automatically index foreign key
columns. Every report query (trial balance, income statement, balance
sheet, account balance, subtree P&L) joins on these columns with no index
support.

At current scale (~200 entries, ~600 lines) this is invisible. At 10x it's
sluggish. At 100x it's broken.

**Indexes to create:**

```sql
CREATE INDEX idx_jel_journal_entry_id ON ledger.journal_entry_line (journal_entry_id);
CREATE INDEX idx_jel_account_id ON ledger.journal_entry_line (account_id);
CREATE INDEX idx_je_fiscal_period_id ON ledger.journal_entry (fiscal_period_id);
CREATE INDEX idx_je_entry_date ON ledger.journal_entry (entry_date);
CREATE INDEX idx_je_voided_null ON ledger.journal_entry (id) WHERE voided_at IS NULL;
CREATE INDEX idx_oi_agreement_id ON ops.obligation_instance (obligation_agreement_id);
```

**Scope:**

1. One migration file with the above CREATE INDEX statements.
2. Run migration against leobloom_dev and verify indexes exist.
3. No application code changes.

**Source:** Code audit SYNTHESIS.md, Tier 1 Finding #2
