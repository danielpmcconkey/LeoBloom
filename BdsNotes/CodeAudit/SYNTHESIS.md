# LeoBloom Code Audit — Synthesis

**Date:** 2026-04-05
**Auditor:** BD (basement dweller), synthesizing findings from 6 independent review agents
**Scope:** Full codebase audit — architecture, simplicity, data integrity, patterns, performance, security

---

## Executive Summary

Six independent reviewers were told to assume the code stinks and prove otherwise. The consensus: **the code doesn't stink.** The domain layer is clean, the architecture is intentional, naming is consistent, SQL is parameterized, and the BDD test suite is real. Every reviewer graded the codebase B+ or better on production code quality.

That said, the reviewers converged hard on a short list of real problems — and when six skeptics independently flag the same issue, you listen.

**Overall grade: B+.** Solid bones, localized rot, nothing structural that can't be fixed in a few focused projects.

---

## Tier 1: Consensus Critical (Flagged by 4+ Reviewers)

### 1. Multi-Transaction Atomicity in Posting Services
**Flagged by:** Architecture, Data Integrity, Performance, Security, Pattern Recognition, Simplicity (indirectly)
**Convergence: 5/6 explicit, 1 indirect = unanimous concern**

`ObligationPostingService.postToLedger` and `TransferService.confirm` both execute three-phase workflows across three separate database transactions:
1. Read + validate (connection 1, committed)
2. Post journal entry (connection 2, committed)
3. Update source record status (connection 3, committed)

If phase 2 succeeds but phase 3 fails, you get an orphaned journal entry — money moves in the ledger with no corresponding status change on the obligation/transfer. The code logs a warning and claims "retry is safe," but there's no idempotency guard to prevent a retry from creating a *second* journal entry.

Data Integrity also flagged a TOCTOU race: between phase 1 committing and phase 2 starting, another caller could pass the same validation and double-post.

**This is the #1 finding of the audit.** In a system whose entire purpose is data integrity, a code path that can produce orphaned financial records is a correctness bug.

**Fix options (pick one):**
- (a) Single transaction: refactor services to accept/pass a transaction through all phases
- (b) Idempotency guard: check for existing journal entry by reference_type/reference_value before posting
- (c) SELECT ... FOR UPDATE in phase 1 + keep transaction open (addresses TOCTOU too)

**Files:** `ObligationPostingService.fs`, `TransferService.fs`, `JournalEntryService.fs`

---

### 2. Zero Secondary Indexes on Core Tables
**Flagged by:** Performance, Data Integrity

Every report query (trial balance, income statement, balance sheet, account balance, subtree P&L) joins `journal_entry_line` on `journal_entry_id` and `account_id`. Neither column is indexed. Same story for `journal_entry.fiscal_period_id`, `journal_entry.entry_date`, and the obligation instance query columns.

At current scale (~200 entries, ~600 lines) this is invisible. At 10x it's sluggish. At 100x it's broken.

**Fix:** One migration file:
```sql
CREATE INDEX idx_jel_journal_entry_id ON ledger.journal_entry_line (journal_entry_id);
CREATE INDEX idx_jel_account_id ON ledger.journal_entry_line (account_id);
CREATE INDEX idx_je_fiscal_period_id ON ledger.journal_entry (fiscal_period_id);
CREATE INDEX idx_je_entry_date ON ledger.journal_entry (entry_date);
CREATE INDEX idx_je_voided_null ON ledger.journal_entry (id) WHERE voided_at IS NULL;
CREATE INDEX idx_oi_agreement_id ON ops.obligation_instance (obligation_agreement_id);
```

**Effort: Low. Impact: High. Do this immediately.**

---

### 3. Missing CHECK Constraints on Enum Columns
**Flagged by:** Data Integrity, Security (indirectly via M-4)

Six varchar columns across the schema accept any string — validation exists only in the F# domain layer:
- `journal_entry_line.entry_type` (should be 'debit'|'credit')
- `account_type.normal_balance` (should be 'debit'|'credit')
- `obligation_agreement.obligation_type`, `.cadence`
- `obligation_instance.status`
- `transfer.status`

If anything bypasses the domain layer (manual SQL, a CLI command, a migration, a future import), garbage data enters the DB and causes `failwith` crashes on read.

Also missing: `CHECK (amount > 0)` on `journal_entry_line.amount` and `transfer.amount`.

**Fix:** One migration with CHECK constraints. Also consider a fiscal period exclusion constraint to prevent overlapping date ranges.

---

## Tier 2: Structural (Flagged by 2-3 Reviewers)

### 4. LeoBloom.Utilities is a God Project
**Flagged by:** Architecture, Simplicity

22 files spanning infrastructure (DataSource, Log), persistence (all repositories), and services (all orchestration). Everything is statically coupled — no seams for testing, no way to use services without dragging in Npgsql/Serilog/config.

The `.fsproj` file ordering is doing the work that project boundaries should do.

**Recommended split:**
- `LeoBloom.Infrastructure` (DataSource, Log, config)
- `LeoBloom.Persistence` (repositories)
- `LeoBloom.Services` (orchestration)

**This is the right thing to do but not urgent.** The current coupling hasn't caused bugs. It will matter when the CLI layer arrives (P036) and needs to consume services cleanly.

### 5. Error Type Schism
**Flagged by:** Architecture, Patterns

Write-path services return `Result<T, string list>`. Read-path services return `Result<T, string>`. Any consumer composing both must handle two error shapes. The CLI layer will feel this immediately.

**Fix:** Standardize on `Result<T, string list>` everywhere, or define a `ServiceError` DU in the domain.

### 6. Dead Scaffolding & Ghost Projects
**Flagged by:** Architecture, Simplicity, Patterns

- 5 empty project directories (LeoBloom.Data, .Ledger, .Ops, .Ledger.Tests, .Ops.Tests)
- LeoBloom.Api with the default WeatherForecast template (cancelled direction)
- Orphaned `Invoice` type in Ops.fs with no consumer
- ~587 LOC of BDD structural tests that assert `true` or guard against a rename that already happened

**Fix:** Delete. All of it. ~700 LOC of dead weight plus 6 empty directories.

### 7. Helper Duplication
**Flagged by:** Simplicity, Patterns

- `optParam`: copied 4x across repositories (3 identical, 1 slightly different signature)
- `buildSection`: copied 3x across reporting services (2 identical)
- `repoRoot` walkUp: copied 4x across test files (all identical)
- `lookupAccount` variants: 4 services each roll their own account-lookup SQL
- Constraint test helpers: duplicated between LedgerConstraintTests and OpsConstraintTests

**Fix:** Extract shared helpers. Low effort, prevents future drift.

---

## Tier 3: Hygiene (Flagged by 1-2 Reviewers, Lower Severity)

| Finding | Reviewer | Severity |
|---------|----------|----------|
| `EntryType.toDbString` defined but not used in JournalEntryRepository (inline match instead) | Patterns | Low — one-line fix |
| Mutable state in domain logic (`generateExpectedDates` uses `let mutable` + while loops) | Architecture, Patterns | Low — `List.unfold` would be more idiomatic |
| Read-only services wrap SELECTs in transactions (unnecessary ceremony) | Simplicity | Low — works, just noise |
| Silent empty password fallback on `LEOBLOOM_DB_PASSWORD` | Security | Medium for prod, Low for dev |
| Exception messages leak DB details to callers (`ex.Message` in error results) | Security | Low for CLI, Medium if API ever returns |
| `IncludeErrorDetail = true` unconditionally in DataSource | Security | Low — gate behind DEBUG |
| Npgsql version mismatch (9.0.3 vs 10.0.1 across projects) | Security | Low — maintenance burden |
| Void doesn't check fiscal period open status | Data Integrity | Medium — business rule question |
| No audit trail for fiscal period reopen (logged but not persisted) | Security | Low |
| `FiscalPeriodCheck` type is redundant with `FiscalPeriod` | Simplicity | Low — 18 LOC |
| `IncomeStatementLine` and `BalanceSheetLine` are structurally identical types | Patterns | Low |
| Explicit `reader.Close()` calls despite `use` binding (50 occurrences) | Simplicity, Patterns | Informational — consistent, not harmful |
| Domain types are ID-coupled not entity-coupled (persistence leaking into domain) | Architecture | Low — defensible trade-off at current scale |
| `Log.initialize()` is not thread-safe | Architecture, Security | Informational for CLI |

---

## What the Reviewers Agreed Is Good

Every reviewer independently noted these as strengths:

- **Domain purity.** Types are immutable F# records, validators are pure functions, Ledger/Ops separation is clean with enforced one-way dependency.
- **SQL parameterization.** Every query uses `@param` placeholders. Zero injection vectors.
- **Transaction discipline on writes.** Core write paths (JournalEntryService.post) correctly wrap all DB operations in a single transaction. The multi-transaction problem is isolated to two services.
- **Void-not-delete pattern.** Journal entries are voided with timestamp + reason, never deleted. All queries filter `voided_at IS NULL`.
- **Architectural constraint tests.** Tests that verify no printfn usage, DataSource encapsulation, and namespace boundaries via reflection/file scanning.
- **Credential externalization.** No hardcoded passwords, env var config, `.gitignore` excludes sensitive files.
- **Consistent naming.** camelCase fields, PascalCase types/modules, snake_case SQL — no deviations found.
- **BDD test discipline.** Gherkin traceability on every test, consistent setup/teardown pattern, real DB assertions.

---

## Recommended Remediation Order

| Priority | Finding | Effort | Impact | Tier |
|----------|---------|--------|--------|------|
| 1 | Multi-transaction atomicity (single txn or idempotency) | Medium | Critical — correctness | 1 |
| 2 | Add database indexes | Low (1 migration) | High — performance | 1 |
| 3 | Add CHECK constraints on enum columns | Low (1 migration) | High — data integrity | 1 |
| 4 | Delete dead scaffolding & ghost projects | Low | Medium — hygiene | 2 |
| 5 | Unify error types to `Result<T, string list>` | Low-Medium | Medium — CLI readiness | 2 |
| 6 | Use `EntryType.toDbString` in repository | Trivial | Low — consistency | 3 |
| 7 | Extract shared helpers (optParam, buildSection, repoRoot) | Low | Low — maintenance | 2 |
| 8 | Split LeoBloom.Utilities into 3 projects | High | Medium — architecture | 2 |
| 9 | Fail hard on missing DB password in Production | Trivial | Medium — security posture | 3 |
| 10 | Gate `IncludeErrorDetail` behind DEBUG | Trivial | Low — defense in depth | 3 |

Items 1-3 should happen before any new feature work. Items 4-7 are natural cleanup before P036 (CLI framework). Item 8 is the big structural move — do it when the CLI project forces the issue, not before.

---

## Individual Reports

- [Architecture Review](architecture-review.md)
- [Simplicity Review](simplicity-review.md)
- [Data Integrity Review](data-integrity-review.md)
- [Pattern Review](pattern-review.md)
- [Performance Review](performance-review.md)
- [Security Review](security-review.md)
