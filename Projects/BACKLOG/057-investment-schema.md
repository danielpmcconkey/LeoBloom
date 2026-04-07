# 057 — Investment Schema

**Epic:** L — Investment Portfolio Module
**Depends On:** None (greenfield schema in new `portfolio` schema)
**Status:** Not started
**Priority:** High (first in sequence)

---

## Problem Statement

LeoBloom manages Dan's household accounting ledger but has no awareness of
investment holdings. Investment data currently lives in a separate
PersonalFinance app (`householdbudget.personalfinance` schema) with its own
position tracking, fund catalog, account groupings, and tax classification.

Dan wants LeoBloom to be both bookkeeper and financial adviser. This project
creates the foundational database schema for the investment portfolio module.

## What It Does

Creates a new `portfolio` Postgres schema with tables for:

1. **Tax buckets** — how an account is taxed (tax-deferred, Roth, HSA,
   taxable/capital gains, primary residence).
2. **Investment account groups** — logical groupings (e.g., "Dan's 401(k)",
   "Dan's IRAs", "Brokerage Account", "Home Equity").
3. **Investment accounts** — individual accounts (e.g., "Dan's ROTH IRA
   237566939"), each belonging to a group and a tax bucket.
4. **Fund classification dimensions** — six orthogonal axes for classifying
   securities: investment type, market cap size, index vs individual, sector,
   geographic region, and investment objective. Each dimension is its own
   lookup table rather than a shared multi-purpose table.
5. **Funds** — the securities/assets themselves (symbol, name), with FK
   references to each of the six classification dimensions.
6. **Positions** — point-in-time snapshots of holdings. Each row records one
   fund in one account on one date: price, quantity, current value, cost
   basis. This is valuation data, not ledger data.

## Schema Design

### `portfolio` schema (new)

```
portfolio.tax_bucket
  id serial PK
  name varchar(100) NOT NULL UNIQUE

portfolio.account_group
  id serial PK
  name varchar(200) NOT NULL UNIQUE

portfolio.investment_account
  id serial PK
  name varchar(200) NOT NULL
  tax_bucket_id int NOT NULL FK -> tax_bucket
  account_group_id int NOT NULL FK -> account_group

portfolio.dim_investment_type
  id serial PK
  name varchar(100) NOT NULL UNIQUE

portfolio.dim_market_cap
  id serial PK
  name varchar(100) NOT NULL UNIQUE

portfolio.dim_index_type
  id serial PK
  name varchar(100) NOT NULL UNIQUE

portfolio.dim_sector
  id serial PK
  name varchar(100) NOT NULL UNIQUE

portfolio.dim_region
  id serial PK
  name varchar(100) NOT NULL UNIQUE

portfolio.dim_objective
  id serial PK
  name varchar(100) NOT NULL UNIQUE

portfolio.fund
  symbol varchar(20) PK
  name varchar(200) NOT NULL
  investment_type_id int FK -> dim_investment_type
  market_cap_id int FK -> dim_market_cap
  index_type_id int FK -> dim_index_type
  sector_id int FK -> dim_sector
  region_id int FK -> dim_region
  objective_id int FK -> dim_objective

portfolio.position
  id serial PK
  investment_account_id int NOT NULL FK -> investment_account
  symbol varchar(20) NOT NULL FK -> fund
  position_date date NOT NULL
  price numeric(18,4) NOT NULL
  quantity numeric(18,4) NOT NULL
  current_value numeric(18,4) NOT NULL
  cost_basis numeric(18,4) NOT NULL
  UNIQUE (investment_account_id, symbol, position_date)
```

### Design decisions

- **Separate dimension tables** instead of PersonalFinance's shared `fundtype`
  table. Each classification axis gets its own table with its own values.
  Cleaner FKs, no ambiguity, easier to extend independently.
- **`portfolio` schema** — investment data is neither ledger nor ops. It's a
  parallel domain. Own schema keeps concerns cleanly separated.
- **Fund FKs are nullable** on classification dimensions — a new fund can be
  added before it's fully classified. Positions require a classified fund
  (enforced at app layer, not schema).
- **Position unique constraint** on (account, symbol, date) prevents duplicate
  snapshots.
- **No link to `ledger.account`** at this stage. The investment accounts exist
  in a parallel universe from the COA. A future project may bridge them
  (e.g., linking the Fidelity CMA investment account to its ledger account
  for reconciliation), but that's out of scope here.

## Acceptance Criteria

### Behavioral (Gherkin scenarios)

| ID | Criterion | Description |
|----|-----------|-------------|
| AC-B1 | Schema creation | Migration creates the `portfolio` schema and all 10 tables with correct columns, types, and constraints. |
| AC-B2 | Tax bucket seed | Dev seed populates the 5 tax buckets (Tax deferred, Tax free HSA, Tax free Roth, Tax on capital gains, Primary residence). |
| AC-B3 | Dimension seed | Dev seed populates all six dimension tables with values matching the PersonalFinance fund catalog. |
| AC-B4 | Foreign key integrity | Inserting an investment account with a nonexistent tax_bucket_id or account_group_id is rejected by the database. |
| AC-B5 | Position uniqueness | Inserting two positions for the same account + symbol + date is rejected by the unique constraint. |
| AC-B6 | Fund PK is symbol | Funds are identified by symbol (varchar PK), not a serial ID. |

### Structural

| ID | Criterion | Description |
|----|-----------|-------------|
| AC-S1 | Migration numbered 022 | Follows the existing migration numbering convention. |
| AC-S2 | Dev seed scripts created | `Seeds/dev/030-tax-buckets.sql`, `040-account-groups.sql`, `050-fund-dimensions.sql`, `060-funds.sql` (or similar logical split). All idempotent upserts. |
| AC-S3 | Existing tests still pass | All pre-existing tests pass without modification. |

## Scope Boundaries

### In scope

- Migration DDL for all 10 tables
- Dev seed scripts for reference/dimension data
- Domain types in a new `LeoBloom.Portfolio` F# project (or module within
  Domain) for TaxBucket, AccountGroup, InvestmentAccount, Fund,
  FundClassification dimensions, Position
- Basic repository functions: insert and query for each entity

### Explicitly out of scope

- **No CLI commands.** That's a separate project.
- **No data migration from PersonalFinance.** That's a separate project.
- **No reporting or analytics.** That's a separate project.
- **No link to ledger accounts.** Future work.
- **No Monte Carlo integration.** Future work.

## Source

- PersonalFinance schema: `householdbudget.personalfinance` (position,
  investmentaccount, investmentaccountgroup, fund, fundtype, taxbucket)
- Hobson's analysis of the PersonalFinance portfolio visualization pipeline
- Dan's directive to consolidate personal finance management into LeoBloom
