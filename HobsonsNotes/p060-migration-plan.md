# P060 — Portfolio Data Migration (Hobson's Job)

**Status:** Waiting on P057 (schema) to be applied to prod
**Blocked by:** P057 migration must land in leobloom_prod first

## What

One-time SQL script to migrate investment data from
`householdbudget.personalfinance` → `leobloom_prod.portfolio`.

## Source → Target Mapping

| Source | Target | Rows | Transform |
|--------|--------|------|-----------|
| personalfinance.taxbucket | portfolio.tax_bucket | 5 | 1:1 |
| personalfinance.investmentaccountgroup | portfolio.account_group | 6 | 1:1 |
| personalfinance.investmentaccount | portfolio.investment_account | 17 | Remap FKs |
| personalfinance.fundtype (× 6 axes) | portfolio.dim_* (6 tables) | ~15-20 distinct per dim | Split shared lookup into 6 tables |
| personalfinance.fund | portfolio.fund | 46 | Remap 6 fundtype FKs → 6 dim FKs |
| personalfinance.position | portfolio.position | 2,184 | Remap account FK, keep symbol as-is |

## Key Transform: fundtype Splitting

PersonalFinance `fund` table has 6 FK columns all pointing to `fundtype`:
- investment_type → dim_investment_type
- size → dim_market_cap
- index_or_individual → dim_index_type
- sector → dim_sector
- region → dim_region
- objective → dim_objective

Script resolves each FK to the fundtype.name, then looks up (or inserts)
that name in the correct dimension table.

## Execution Steps

1. Run P057 migration against leobloom_prod (review first per standing orders)
2. Write the migration SQL script (cross-database via dblink or FDW, or
   simpler: two-phase with temp tables / CSV intermediate)
3. Run with a transaction wrapper
4. Verify counts
5. Spot-check known positions (FXAIX in Dan's Roth, Jan 2026)
6. Script goes in HobsonsNotes/ — not in src tree

## Irregular Fund Symbols (migrate as-is)

- "4107 Home" — Logan Circle home equity
- "Lockhart Pl" — Lockhart Place home equity
- "V????" — Vanguard Inst 500 Idx Tr C
- "VanXXX" — Vanguard Dvlpd Mrkts Ind Inst
- "SECU-3015584" — Jodi's SECU IRA

## Connection Details

- Source: `householdbudget` on localhost, user `dansdev`, password hex-decoded
  from $PGPASS env var
- Target: `leobloom_prod` on localhost, user TBD (leobloom_hobson or dansdev)
