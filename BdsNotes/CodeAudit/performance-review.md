# LeoBloom Performance Audit

**Date:** 2026-04-05
**Auditor:** BD (Performance Oracle posture)
**Scope:** Full codebase -- Domain, Utilities (repositories + services), Migrations

---

## Summary Verdict

The code is **cleaner than expected** for an early-stage project. No catastrophic algorithmic bombs. The domain layer is pure, the validation logic is straightforward, and the repository layer uses parameterized queries throughout. That said, the database layer has **real problems that will bite at scale** -- specifically, a near-total absence of indexes on the columns that every query hits, and a few structural patterns in the service layer that will degrade under load. The algorithmic complexity of the F# code itself is fine -- nothing worse than O(n) in practice, because the data volumes per operation are small (journal entry lines, obligation instances per spawn, etc.). The danger here is not the F# -- it is the SQL.

**Overall grade: B- for current scale, D+ for 10x growth.**

---

## Critical Issues

### CRIT-1: No indexes on `journal_entry_line` join/filter columns

**Files:**
- `/workspace/LeoBloom/Src/LeoBloom.Migrations/Migrations/1712000009000_CreateJournalEntryLine.sql`
- Every repository that touches journal entry lines

**Problem:** The `journal_entry_line` table has **zero secondary indexes**. Only the PK (`id`) is indexed. Every single report query (trial balance, income statement, balance sheet, subtree P&L, account balance) joins `journal_entry_line` on `journal_entry_id` and `account_id`. Without indexes on these columns, PostgreSQL will sequential scan the entire table for every join.

**Queries affected:**
- `AccountBalanceRepository.getBalance` -- joins on `jel.journal_entry_id = je.id` and `jel.account_id = a.id`
- `TrialBalanceRepository.getActivityByPeriod` -- same join pattern
- `IncomeStatementRepository.getActivityByPeriod` -- same
- `BalanceSheetRepository.getCumulativeBalances` -- same
- `BalanceSheetRepository.getRetainedEarnings` -- same
- `SubtreePLRepository.getSubtreeActivityByPeriod` -- same

**Impact at scale:** With 10,000 journal entries averaging 3 lines each = 30,000 rows. Every report query does a full table scan on 30K rows and joins to `journal_entry`. At 100K entries (300K lines), this becomes a real performance problem. The `entry_date` and `voided_at` filters are on `journal_entry`, not on the line table, so PostgreSQL has to scan lines first, then filter via the join.

**Fix:** Add a migration:
```sql
CREATE INDEX idx_jel_journal_entry_id ON ledger.journal_entry_line (journal_entry_id);
CREATE INDEX idx_jel_account_id ON ledger.journal_entry_line (account_id);
```

Consider a composite index `(journal_entry_id, account_id, entry_type, amount)` to enable index-only scans for the aggregate queries.

**Severity: High. This is the single biggest performance risk in the codebase.**

---

### CRIT-2: No index on `journal_entry.fiscal_period_id`

**File:** `/workspace/LeoBloom/Src/LeoBloom.Migrations/Migrations/1712000007000_CreateJournalEntry.sql`

**Problem:** The `journal_entry` table has a FK to `fiscal_period` but no index on `fiscal_period_id`. Every period-scoped report query (trial balance, income statement, subtree P&L) filters on `je.fiscal_period_id = @fiscal_period_id`. Without an index, PostgreSQL scans the entire `journal_entry` table.

**Fix:**
```sql
CREATE INDEX idx_je_fiscal_period_id ON ledger.journal_entry (fiscal_period_id);
```

**Severity: High. Same growth trajectory as CRIT-1.**

---

### CRIT-3: No index on `journal_entry.entry_date` or `journal_entry.voided_at`

**File:** `/workspace/LeoBloom/Src/LeoBloom.Migrations/Migrations/1712000007000_CreateJournalEntry.sql`

**Problem:** The balance sheet and account balance queries filter on `je.entry_date <= @as_of_date AND je.voided_at IS NULL`. Both columns are unindexed.

**Fix:**
```sql
CREATE INDEX idx_je_entry_date ON ledger.journal_entry (entry_date);
-- Partial index for active (non-voided) entries:
CREATE INDEX idx_je_voided_at_null ON ledger.journal_entry (id) WHERE voided_at IS NULL;
```

The partial index is particularly valuable because the vast majority of entries will be non-voided, and every single query in the system filters on `voided_at IS NULL`.

**Severity: High for balance sheet queries as history grows.**

---

### CRIT-4: No index on `obligation_instance.obligation_agreement_id` or `obligation_instance.expected_date`

**File:** `/workspace/LeoBloom/Src/LeoBloom.Migrations/Migrations/1712000016000_CreateObligationInstance.sql`

**Problem:** Two key queries are affected:
1. `findExistingDates` filters on `obligation_agreement_id` and `expected_date`
2. `findOverdueCandidates` filters on `status`, `is_active`, and `expected_date`
3. `hasActiveInstances` filters on `obligation_agreement_id` and `is_active`

All unindexed.

**Fix:**
```sql
CREATE INDEX idx_oi_agreement_id ON ops.obligation_instance (obligation_agreement_id);
CREATE INDEX idx_oi_overdue_candidates ON ops.obligation_instance (expected_date)
    WHERE status = 'expected' AND is_active = true;
```

**Severity: Medium. Obligation instances grow linearly with time (12-24/year per agreement). Gets painful after a few years with many agreements.**

---

## Warnings

### WARN-1: `insertLines` issues one INSERT per line (N round-trips)

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/JournalEntryRepository.fs`, lines 43-71

**Problem:** `insertLines` maps over the line list and executes a separate `INSERT ... RETURNING` per line. A journal entry with 10 lines = 10 database round-trips within the transaction. Same pattern in `insertReferences` (lines 109-130).

**Current impact:** Low. Journal entries typically have 2-4 lines. But opening balance entries can have dozens of lines (one per account).

**Fix:** Batch insert with a single `INSERT ... VALUES (...), (...), ... RETURNING ...` or use `COPY` for bulk inserts. Alternatively, use `NpgsqlBinaryImporter` for high-throughput scenarios.

**Severity: Low-Medium. Acceptable for now, but worth batching when opening balance imports get larger.**

---

### WARN-2: `detectOverdue` opens a separate connection+transaction per transition

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/ObligationInstanceService.fs`, lines 117-164

**Problem:** `detectOverdue` first queries all overdue candidates (1 connection), then calls `transition` in a loop. Each `transition` call opens its own connection + transaction (via `DataSource.openConnection()`). If there are 50 overdue candidates, that is 51 database connections opened and closed.

**Current impact:** Low. Overdue detection runs infrequently and the pool handles it.

**Fix:** Either:
1. Accept a transaction parameter and do all transitions in one transaction, or
2. At minimum, batch the transitions into a single connection

**Severity: Medium. Connection pool exhaustion risk under burst scenarios.**

---

### WARN-3: `ObligationPostingService.postToLedger` uses three separate transactions

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/ObligationPostingService.fs`

**Problem:** This function uses three separate transactions:
1. Phase 1: Read + validate (opens connection, commits)
2. Phase 2: Post journal entry (JournalEntryService.post opens its own connection)
3. Phase 3: Transition instance to posted (ObligationInstanceService.transition opens its own connection)

If Phase 2 succeeds but Phase 3 fails, you have a journal entry with no corresponding posted status on the instance. The code logs a warning but doesn't compensate. This is a **correctness** issue more than performance, but it also means 3 connections where 1 would suffice.

Same pattern exists in `TransferService.confirm` (lines 80-165) -- three separate transactions.

**Severity: Medium (correctness concern; low performance impact).**

---

### WARN-4: `BalanceSheetService.getAsOfDate` executes two queries that scan the same data

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/BalanceSheetRepository.fs`

**Problem:** `getCumulativeBalances` and `getRetainedEarnings` both scan `journal_entry JOIN journal_entry_line JOIN account JOIN account_type` with the same `entry_date <= @as_of_date AND voided_at IS NULL` filter. The only difference is the account type filter (`asset/liability/equity` vs `revenue/expense`). This means the same large join is executed twice.

**Fix:** Combine into a single query that returns all account types, then partition in F# code.

**Severity: Low-Medium. Doubles the work for the heaviest query pattern.**

---

### WARN-5: List prepend + reverse pattern in repository readers

**Files:** Every repository with a `while reader.Read()` loop (TrialBalanceRepository, IncomeStatementRepository, BalanceSheetRepository, SubtreePLRepository, ObligationAgreementRepository, ObligationInstanceRepository)

**Problem:** The pattern `results <- item :: results` followed by `results |> List.rev` is idiomatic F# and O(n), so it is not a complexity problem. However, it allocates n cons cells during accumulation and then n more during reversal. For small result sets this is negligible. For large result sets, a `ResizeArray<T>` (System.Collections.Generic.List) would avoid the reversal allocation.

**Severity: Low. Idiomatic F#, acceptable trade-off. Only worth changing if profiling shows GC pressure from report queries returning hundreds of accounts.**

---

### WARN-6: `lookupAccountActivity` in JournalEntryService builds dynamic SQL with string interpolation

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/JournalEntryService.fs`, lines 34-51

**Problem:** The IN clause is built dynamically with `sprintf` and `String.concat`. This is correctly parameterized (each value gets its own `@a0`, `@a1`, etc.), so it is not a SQL injection risk. However, each unique parameter count generates a different query string, which means PostgreSQL cannot reuse prepared statement plans across different line counts.

Same pattern in `OpeningBalanceService.lookupAccounts`.

**Severity: Low. Plan cache pollution is negligible at this scale.**

---

## Observations (Not Issues)

### OBS-1: Domain validation is O(n) on line count -- totally fine

**File:** `/workspace/LeoBloom/Src/LeoBloom.Domain/Ledger.fs`

`validateBalanced`, `validateAmountsPositive`, `validateMinimumLineCount` all iterate the lines list once or twice. `validateCommand` runs all three and collects errors. Total: ~5 passes over the lines list. For journal entries with 2-20 lines, this is irrelevant. Even with 1000 lines, it would be microseconds.

### OBS-2: F# immutable records -- no allocation concern

The domain types are F# records (value-like semantics, allocated on the heap but small). No large object retention patterns. No mutable state leaking between requests.

### OBS-3: Connection pooling via NpgsqlDataSource is correctly configured

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/DataSource.fs`

The `NpgsqlDataSource` is a singleton (module-level binding), and `openConnection()` returns pooled connections. The 5-second command timeout is aggressive but appropriate for a financial engine -- you want queries to fail fast rather than hang.

### OBS-4: `generateExpectedDates` uses mutable accumulation with List.rev -- fine

**File:** `/workspace/LeoBloom/Src/LeoBloom.Domain/Ops.fs`, lines 249-290

The spawning logic generates dates using mutable variables and list prepend. The number of dates generated is bounded by the date range (max ~120 for 10 years of monthly obligations). Not a performance concern.

### OBS-5: The recursive CTE in SubtreePLRepository is well-structured

**File:** `/workspace/LeoBloom/Src/LeoBloom.Utilities/SubtreePLRepository.fs`, lines 20-46

The `WITH RECURSIVE subtree` CTE walks the account tree by `parent_code`. This is correct and efficient for PostgreSQL's CTE implementation. The recursion depth is bounded by the chart of accounts depth (typically 3-5 levels). The `IN (SELECT code FROM subtree)` subquery should be optimized by PostgreSQL into a semi-join.

### OBS-6: No caching layer exists

There is no memoization or caching of any kind. For a personal finance app, this is acceptable -- the query volume is low and data changes frequently enough that cache invalidation would add complexity without clear benefit. If this ever becomes multi-user or serves a dashboard, consider caching fiscal period lookups and account type mappings (they rarely change).

---

## Recommended Actions (Priority Order)

| Priority | Item | Effort | Impact |
|----------|------|--------|--------|
| 1 | Add indexes on `journal_entry_line` (CRIT-1) | Low (1 migration) | High |
| 2 | Add index on `journal_entry.fiscal_period_id` (CRIT-2) | Low (1 migration) | High |
| 3 | Add index on `journal_entry.entry_date` + partial index on `voided_at IS NULL` (CRIT-3) | Low (1 migration) | High |
| 4 | Add indexes on `obligation_instance` (CRIT-4) | Low (1 migration) | Medium |
| 5 | Combine balance sheet queries into one (WARN-4) | Medium | Medium |
| 6 | Batch `insertLines` into single INSERT (WARN-1) | Medium | Low-Medium |
| 7 | Single-transaction overdue detection (WARN-2) | Medium | Medium |
| 8 | Address multi-transaction atomicity in posting (WARN-3) | High | Medium (correctness) |

Items 1-4 are a single migration file and should be done immediately. They are the lowest effort, highest impact changes in this list.

---

## Scalability Projections

| Metric | Current (est.) | 10x | 100x | Bottleneck |
|--------|---------------|-----|------|------------|
| Journal entries | ~200 | 2,000 | 20,000 | Seq scan on `journal_entry` |
| Journal entry lines | ~600 | 6,000 | 60,000 | Seq scan on `journal_entry_line` (CRIT-1) |
| Report query time | <50ms | 200-500ms | 2-10s | Unindexed joins |
| Obligation instances | ~50 | 500 | 5,000 | Manageable |
| Connections per report | 1 | 1 | 1 | Fine |
| Connections per posting | 3 | 3 | 3 | Pool pressure if concurrent |

The critical path is report generation. With indexes, 100x scale should keep report queries under 200ms. Without indexes, expect multi-second queries at 100x.
