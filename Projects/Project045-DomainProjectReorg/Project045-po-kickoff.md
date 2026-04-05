# Project 045 — Domain-Based Project Reorganization

**PO Kickoff**
**Date:** 2026-04-05
**Status:** In Progress
**Epic:** K — Code Audit Remediation
**Depends On:** None
**Blocks:** P036 (CLI framework)

---

## Project Summary

LeoBloom.Utilities has become a god project — 22 .fs files spanning ledger
reporting, obligation management, transfer operations, and actual
infrastructure. Everything from BalanceSheetService to TransferRepository
lives in one flat namespace next to DataSource and Log. This makes it
impossible for the upcoming CLI layer (P036) to consume clean, bounded
domain modules.

This project splits the domain-specific files into two new projects that
already have placeholder directories under Src/:

- **LeoBloom.Ledger** — journal entries, accounts, fiscal periods, balances,
  and financial reporting (income statement, balance sheet, trial balance,
  subtree P&L, opening balances)
- **LeoBloom.Ops** — obligations (agreements, instances, posting), transfers

LeoBloom.Utilities retains only generic cross-cutting infrastructure:
DataSource, Log, and configuration/appsettings handling.

This is a structural-only change. No behavioral changes. No new features.
No GAAP implications. The 428 existing tests must continue to pass with
zero modifications to test logic (only namespace imports change).

---

## File Classification

### Stays in LeoBloom.Utilities (3 files)

| File | Rationale |
|------|-----------|
| DataSource.fs | Cross-cutting DB infrastructure |
| Log.fs | Cross-cutting logging infrastructure |
| (future DataHelpers.fs) | Shared DB utilities — does not exist yet, noted for P049 |

### Moves to LeoBloom.Ledger (16 files)

| File | Domain Concept |
|------|----------------|
| JournalEntryRepository.fs | Ledger core |
| JournalEntryService.fs | Ledger core |
| FiscalPeriodRepository.fs | Period management |
| FiscalPeriodService.fs | Period management |
| AccountBalanceRepository.fs | Account balances |
| AccountBalanceService.fs | Account balances |
| TrialBalanceRepository.fs | Reporting |
| TrialBalanceService.fs | Reporting |
| IncomeStatementRepository.fs | Reporting |
| IncomeStatementService.fs | Reporting |
| BalanceSheetRepository.fs | Reporting |
| BalanceSheetService.fs | Reporting |
| SubtreePLRepository.fs | Reporting |
| SubtreePLService.fs | Reporting |
| OpeningBalanceService.fs | Period transitions |

That is 15 files. One short of the 16 the backlog description implies by
calling it "22 files" minus 3 infrastructure minus 4 ops. Let me be
precise: the Utilities fsproj lists 21 Compile includes (no DataHelpers.fs
exists yet despite the backlog mentioning it). 21 - 3 infrastructure - 6
ops = 12... wait. Let me just count properly.

**Utilities fsproj current Compile entries (21):**
Log.fs, DataSource.fs, JournalEntryRepository.fs, FiscalPeriodRepository.fs,
FiscalPeriodService.fs, JournalEntryService.fs, AccountBalanceRepository.fs,
AccountBalanceService.fs, TrialBalanceRepository.fs, TrialBalanceService.fs,
IncomeStatementRepository.fs, IncomeStatementService.fs,
BalanceSheetRepository.fs, BalanceSheetService.fs, SubtreePLRepository.fs,
SubtreePLService.fs, ObligationAgreementRepository.fs,
ObligationAgreementService.fs, ObligationInstanceRepository.fs,
ObligationInstanceService.fs, ObligationPostingService.fs,
TransferRepository.fs, TransferService.fs, OpeningBalanceService.fs

That is actually 24 entries. Let me recount: that is 24 Compile includes.

**Stays in Utilities: 2 files**
- Log.fs
- DataSource.fs

**Moves to LeoBloom.Ledger: 16 files**
- JournalEntryRepository.fs
- JournalEntryService.fs
- FiscalPeriodRepository.fs
- FiscalPeriodService.fs
- AccountBalanceRepository.fs
- AccountBalanceService.fs
- TrialBalanceRepository.fs
- TrialBalanceService.fs
- IncomeStatementRepository.fs
- IncomeStatementService.fs
- BalanceSheetRepository.fs
- BalanceSheetService.fs
- SubtreePLRepository.fs
- SubtreePLService.fs
- OpeningBalanceService.fs

That is 15. The 16th is a judgment call — see below.

**Moves to LeoBloom.Ops: 6 files**
- ObligationAgreementRepository.fs
- ObligationAgreementService.fs
- ObligationInstanceRepository.fs
- ObligationInstanceService.fs
- ObligationPostingService.fs
- TransferRepository.fs
- TransferService.fs

That is 7 files.

**Totals:** 2 (Utilities) + 15 (Ledger) + 7 (Ops) = 24. Checks out.

### OpeningBalanceService — Classification Note

OpeningBalanceService goes to Ledger. Opening balances are a ledger
concept (closing/opening fiscal periods with journal entries). The backlog
item agrees — it lists "fiscal periods" under Ledger scope.

### ObligationPostingService — Cross-Domain Dependency

ObligationPostingService opens both `LeoBloom.Domain.Ops` and
`LeoBloom.Domain.Ledger`. It belongs in Ops (it is the bridge from
obligations to ledger posting). This confirms the backlog's note that
Ops must reference Ledger.

---

## Acceptance Criteria

### Structural Criteria (verified by build + QE, not Gherkin)

| # | Criterion | Verification |
|---|-----------|--------------|
| S1 | LeoBloom.Utilities contains only Log.fs and DataSource.fs as Compile includes | Inspect fsproj |
| S2 | LeoBloom.Ledger.fsproj exists with all 15 ledger files as Compile includes | Inspect fsproj |
| S3 | LeoBloom.Ops.fsproj exists with all 7 ops files as Compile includes | Inspect fsproj |
| S4 | Namespace declarations in moved files updated to LeoBloom.Ledger / LeoBloom.Ops respectively | Grep verification |
| S5 | LeoBloom.Ledger references LeoBloom.Utilities and LeoBloom.Domain | Inspect fsproj ProjectReference |
| S6 | LeoBloom.Ops references LeoBloom.Utilities, LeoBloom.Domain, and LeoBloom.Ledger | Inspect fsproj ProjectReference |
| S7 | LeoBloom.Tests references LeoBloom.Ledger and LeoBloom.Ops (in addition to existing refs) | Inspect fsproj ProjectReference |
| S8 | Test file `open` declarations updated to new namespaces | Grep verification |
| S9 | Solution (.sln) includes LeoBloom.Ledger and LeoBloom.Ops projects | dotnet sln list |
| S10 | No .fs file in Utilities references domain-specific types directly (no obligation, transfer, balance sheet, etc. service/repo code remains) | Grep verification |

### Build and Test Criteria (verified by Governor)

| # | Criterion | Verification |
|---|-----------|--------------|
| B1 | `dotnet build` succeeds with zero warnings related to this change | Build output |
| B2 | `dotnet test` — all 428 tests pass, zero failures, zero skips | Test output |
| B3 | No behavioral changes — test assertions are identical pre and post (only `open` statements change) | Diff review |

### Behavioral Criteria

**None.** This is a purely structural refactoring. No new behaviors are
introduced. No existing behaviors change. There are no Gherkin scenarios
to write for this project. All verification is structural and build-based.

---

## Project Reference Dependency Graph (Post-Reorg)

```
LeoBloom.Domain
    ^       ^       ^
    |       |       |
Utilities  Ledger  Ops
    ^       ^      ^ |
    |       |      | |
    +---+---+      | |
        |     +----+ |
        |     |      |
      Tests <-+------+
```

- Ledger -> Utilities, Domain
- Ops -> Utilities, Domain, Ledger
- Tests -> Domain, Utilities, Ledger, Ops

---

## Risks and Assumptions

### Risks

1. **F# compile order sensitivity.** F# fsproj files are order-dependent.
   The Compile Include order in Ledger and Ops must respect any
   intra-project dependencies (e.g., repositories before services that
   consume them). The current Utilities fsproj order is the guide.

2. **Npgsql and config package references.** Moved files use Npgsql and
   potentially configuration packages. Ledger and Ops fsproj files need
   the same PackageReference entries as Utilities currently has. Don't
   forget Serilog if any moved files reference Log.

3. **Solution file registration.** The Ledger and Ops fsproj files don't
   exist yet — they need to be created AND added to the .sln. Missing
   this step means `dotnet build` at solution level won't compile them.

4. **Transitive reference assumptions.** Tests currently only reference
   Utilities. After the split, tests need explicit references to Ledger
   and Ops. Don't assume transitive ProjectReference resolution.

### Assumptions

1. The Ledger and Ops directories already exist under Src/ (confirmed —
   they have bin/obj from solution restore but no fsproj or .fs files).

2. No DataHelpers.fs exists yet. The backlog mentions it as staying in
   Utilities, but it is a P049 concern, not P045.

3. No other projects besides Tests reference Utilities. LeoBloom.Api
   exists but is dead code (slated for deletion in P046).

4. The 428 test count is current as of project start.

---

## Backlog Status Update

P045 status changed from "Not started" to **In Progress** as of 2026-04-05.
