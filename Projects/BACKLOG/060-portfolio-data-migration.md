# 060 — Portfolio Data Migration from PersonalFinance

**Epic:** L — Investment Portfolio Module
**Depends On:** 057 (investment schema)
**Status:** Not started
**Priority:** High
**Executor:** Hobson (not Nightshift/BD)

---

## Problem Statement

Dan's historical investment data lives in `householdbudget.personalfinance`
(position, investmentaccount, investmentaccountgroup, fund, fundtype,
taxbucket). This data needs to land in LeoBloom's new `portfolio` schema.

## Why Hobson, Not BD

This is a one-time data migration, not a product feature. It doesn't belong
in the BDD pipeline, shouldn't produce Gherkin specs, and shouldn't leave a
permanent CLI command in the codebase. Hobson writes a script, runs it against
prod, verifies the counts, and it's done.

## What It Does

A standalone migration script (SQL or F#) that reads from
`householdbudget.personalfinance` and writes to `leobloom_prod.portfolio`.

### Migration steps (in order)

1. **Tax buckets** — Read `personalfinance.taxbucket`, insert into
   `portfolio.tax_bucket`. Direct 1:1 mapping.

2. **Account groups** — Read `personalfinance.investmentaccountgroup`, insert
   into `portfolio.account_group`. Direct 1:1 mapping.

3. **Investment accounts** — Read `personalfinance.investmentaccount`, insert
   into `portfolio.investment_account`. Map old FK IDs to new IDs for
   tax_bucket and account_group.

4. **Fund classification dimensions** — Read `personalfinance.fundtype`. For
   each fund in `personalfinance.fund`, resolve the six FK columns
   (investment_type, size, index_or_individual, sector, region, objective)
   to fundtype names, then insert those names into the appropriate dimension
   table (`dim_investment_type`, `dim_market_cap`, `dim_index_type`,
   `dim_sector`, `dim_region`, `dim_objective`). Deduplicate — many funds
   share the same dimension values.

5. **Funds** — Read `personalfinance.fund`, insert into `portfolio.fund`.
   Map old fundtype FK IDs to new dimension table IDs.

6. **Positions** — Read `personalfinance.position`, insert into
   `portfolio.position`. Map old investmentaccount IDs to new
   investment_account IDs. All 2,184 rows (2020-01 through 2026-01).

### Key transformation

PersonalFinance uses a single `fundtype` table as a shared lookup for six
different classification dimensions. LeoBloom's schema (P057) splits these
into six separate dimension tables. The migration resolves each fund's six
FK columns through `fundtype` to get the name, then inserts that name into
the correct dimension table.

### Idempotency

The script is idempotent — safe to run twice:
- Dimension values, tax buckets, account groups: INSERT ON CONFLICT DO NOTHING
- Funds: INSERT ON CONFLICT DO NOTHING
- Investment accounts: INSERT ON CONFLICT DO NOTHING
- Positions: INSERT ON CONFLICT (account, symbol, date) DO NOTHING

## Source Data Reference

| Source Table | Row Count | Notes |
|---|---|---|
| `personalfinance.taxbucket` | 5 | Tax deferred, HSA, Roth, Capital gains, Primary residence |
| `personalfinance.investmentaccountgroup` | 6 | Dan's 401(k), Dan's IRAs, Jodi's IRAs, Brokerage, Home Equity, Health Equity |
| `personalfinance.investmentaccount` | 17 | Fidelity, SECU, Ally, T. Rowe Price, HealthEquity, home equity |
| `personalfinance.fundtype` | 35 | Shared lookup: investment types, cap sizes, sectors, regions, objectives |
| `personalfinance.fund` | 46 | Index funds, individual stocks, home equity entries |
| `personalfinance.position` | 2,184 | Monthly snapshots, full history |

## Verification

After running, Hobson verifies:
- Row counts match source (5 + 6 + 17 + 46 + 2,184 = expected)
- Dimension tables contain only the distinct values actually referenced by funds
- A spot-check of a known fund (e.g., FXAIX) has correct dimension values
- A spot-check of a known position (e.g., FXAIX in Dan's Roth, Jan 2026) has
  correct price, quantity, value, and cost basis

## Notes

- `personalfinance.fund.symbol` has irregular values ("4107 Home",
  "Lockhart Pl", "V????", "VanXXX", "SECU-3015584"). These are valid
  non-tradeable assets. Migrate as-is.
- Script lives in `HobsonsNotes/` or a scratch location — not in the
  LeoBloom source tree.
