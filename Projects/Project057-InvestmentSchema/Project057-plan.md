# Project 057 — Investment Schema: Plan

## Objective

Create the `portfolio` Postgres schema and all 10 tables for the investment
portfolio module. Provide dev seed scripts for reference/dimension data so the
schema is immediately useful for development. This is DDL + seed data only —
domain types and repository code are P058's problem.

## Phases

### Phase 1: Migration — Create Portfolio Schema and Tables

**What:** A single migration file `1712000023000_CreatePortfolioSchema.sql` that:

1. Creates the `portfolio` schema
2. Creates all 10 tables in dependency order:
   - `portfolio.tax_bucket` (id serial PK, name varchar(100) NOT NULL UNIQUE)
   - `portfolio.account_group` (id serial PK, name varchar(200) NOT NULL UNIQUE)
   - `portfolio.investment_account` (id serial PK, name varchar(200) NOT NULL, tax_bucket_id int NOT NULL FK, account_group_id int NOT NULL FK)
   - `portfolio.dim_investment_type` (id serial PK, name varchar(100) NOT NULL UNIQUE)
   - `portfolio.dim_market_cap` (id serial PK, name varchar(100) NOT NULL UNIQUE)
   - `portfolio.dim_index_type` (id serial PK, name varchar(100) NOT NULL UNIQUE)
   - `portfolio.dim_sector` (id serial PK, name varchar(100) NOT NULL UNIQUE)
   - `portfolio.dim_region` (id serial PK, name varchar(100) NOT NULL UNIQUE)
   - `portfolio.dim_objective` (id serial PK, name varchar(100) NOT NULL UNIQUE)
   - `portfolio.fund` (symbol varchar(20) PK, name varchar(200) NOT NULL, six nullable FK columns to dim tables)
   - `portfolio.position` (id serial PK, investment_account_id int NOT NULL FK, symbol varchar(20) NOT NULL FK, position_date date NOT NULL, price/quantity/current_value/cost_basis numeric(18,4) NOT NULL, UNIQUE constraint on (investment_account_id, symbol, position_date))

**Files:**
- CREATE: `Src/LeoBloom.Migrations/Migrations/1712000023000_CreatePortfolioSchema.sql`

**Conventions to follow:**
- Migrondi header format: `-- MIGRONDI:NAME=...`, `-- MIGRONDI:TIMESTAMP=...`, `-- ---------- MIGRONDI:UP ----------` / `-- ---------- MIGRONDI:DOWN ----------`
- FK constraints use `ON DELETE RESTRICT` (matching existing pattern from `CreateAccount.sql`)
- DOWN section drops tables in reverse dependency order, then drops schema

**Verification:**
- Migration applies cleanly against a fresh database (after existing 022 migrations)
- All 10 tables exist in the `portfolio` schema with correct columns, types, and constraints
- FK integrity is enforced (inserting bad tax_bucket_id is rejected)
- Position unique constraint fires on duplicate (account, symbol, date)
- Fund PK is `symbol` (varchar), not a serial

### Phase 2: Dev Seed Scripts

**What:** Four seed scripts in `Seeds/dev/` following the established idempotent upsert pattern (INSERT ... ON CONFLICT DO UPDATE, wrapped in BEGIN/COMMIT):

1. **`030-tax-buckets.sql`** — The 5 tax buckets:
   - Tax deferred
   - Tax free HSA
   - Tax free Roth
   - Tax on capital gains
   - Primary residence

2. **`040-account-groups.sql`** — Sample investment account groups (anonymized for dev):
   - Retirement 401(k)
   - Roth IRAs
   - HSA Accounts
   - Brokerage
   - Home Equity

3. **`050-fund-dimensions.sql`** — All six dimension tables seeded with representative values:
   - dim_investment_type: Stock, Bond, ETF, Mutual Fund, Money Market, Real Estate, Target Date, Stable Value
   - dim_market_cap: Large Cap, Mid Cap, Small Cap, N/A
   - dim_index_type: Index, Individual, Blend
   - dim_sector: Technology, Healthcare, Financials, Energy, Consumer Discretionary, Consumer Staples, Industrials, Utilities, Real Estate, Materials, Communication Services, Broad Market, N/A
   - dim_region: US, International Developed, Emerging Markets, Global, N/A
   - dim_objective: Growth, Income, Growth & Income, Capital Preservation, Aggressive Growth, Balanced

4. **`060-funds.sql`** — A handful of well-known index funds/ETFs as sample data with dimension FK references (e.g., VTI, VXUS, BND, VOO). Uses subqueries to resolve dimension names to IDs for readability and idempotency.

**Files:**
- CREATE: `Src/LeoBloom.Migrations/Seeds/dev/030-tax-buckets.sql`
- CREATE: `Src/LeoBloom.Migrations/Seeds/dev/040-account-groups.sql`
- CREATE: `Src/LeoBloom.Migrations/Seeds/dev/050-fund-dimensions.sql`
- CREATE: `Src/LeoBloom.Migrations/Seeds/dev/060-funds.sql`

**Conventions to follow:**
- Comment header: `-- Seed: <description> for dev environment`
- Idempotent upserts: `INSERT ... ON CONFLICT DO UPDATE`
- Wrapped in `BEGIN; ... COMMIT;`
- Numbered to sort after existing seeds (020 is last)

**Verification:**
- `run-seeds.sh dev` executes all seed files in order without errors
- Running seeds twice produces identical results (idempotent)
- Tax bucket count = 5, dimension tables populated, sample funds have correct FK linkages

## Acceptance Criteria

- [ ] AC-B1: Migration creates `portfolio` schema and all 10 tables with correct columns, types, and constraints
- [ ] AC-B2: Dev seed populates the 5 tax buckets
- [ ] AC-B3: Dev seed populates all six dimension tables
- [ ] AC-B4: Inserting investment_account with nonexistent tax_bucket_id or account_group_id is rejected
- [ ] AC-B5: Inserting duplicate position (same account + symbol + date) is rejected
- [ ] AC-B6: Fund PK is symbol (varchar), not serial
- [ ] AC-S1: Migration numbered 023 (1712000023000)
- [ ] AC-S2: Dev seed scripts created at 030/040/050/060
- [ ] AC-S3: Existing tests still pass without modification

## Risks

- **Low: Dimension value completeness.** The seed dimension values are representative, not exhaustive. Future funds may need new dimension values. This is fine — seeds are for dev, and new values can be added to seed scripts or directly in prod.
- **Low: Fund symbol as PK.** Symbols can change (rare) or be reused after delisting. The spec explicitly calls for this design. If it becomes a problem, a future migration can add a surrogate key.

## Out of Scope

- F# domain types (`LeoBloom.Portfolio` project) — that's P058
- Repository functions — that's P058
- CLI commands for portfolio management — future project
- Data migration from PersonalFinance app — future project
- Investment account to ledger account linking — future project
- Position seed data (no sample positions in dev seeds — positions are transactional, not reference data)
