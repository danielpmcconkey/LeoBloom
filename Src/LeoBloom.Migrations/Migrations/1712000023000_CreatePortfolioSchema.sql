-- MIGRONDI:NAME=1712000023000_CreatePortfolioSchema.sql
-- MIGRONDI:TIMESTAMP=1712000023000
-- ---------- MIGRONDI:UP ----------

CREATE SCHEMA portfolio;

-- Reference / dimension tables (no dependencies)

CREATE TABLE portfolio.tax_bucket (
    id              serial          PRIMARY KEY,
    name            varchar(100)    NOT NULL UNIQUE
);

CREATE TABLE portfolio.account_group (
    id              serial          PRIMARY KEY,
    name            varchar(200)    NOT NULL UNIQUE
);

CREATE TABLE portfolio.dim_investment_type (
    id              serial          PRIMARY KEY,
    name            varchar(100)    NOT NULL UNIQUE
);

CREATE TABLE portfolio.dim_market_cap (
    id              serial          PRIMARY KEY,
    name            varchar(100)    NOT NULL UNIQUE
);

CREATE TABLE portfolio.dim_index_type (
    id              serial          PRIMARY KEY,
    name            varchar(100)    NOT NULL UNIQUE
);

CREATE TABLE portfolio.dim_sector (
    id              serial          PRIMARY KEY,
    name            varchar(100)    NOT NULL UNIQUE
);

CREATE TABLE portfolio.dim_region (
    id              serial          PRIMARY KEY,
    name            varchar(100)    NOT NULL UNIQUE
);

CREATE TABLE portfolio.dim_objective (
    id              serial          PRIMARY KEY,
    name            varchar(100)    NOT NULL UNIQUE
);

-- Depends on tax_bucket and account_group

CREATE TABLE portfolio.investment_account (
    id                  serial          PRIMARY KEY,
    name                varchar(200)    NOT NULL,
    tax_bucket_id       integer         NOT NULL REFERENCES portfolio.tax_bucket(id) ON DELETE RESTRICT,
    account_group_id    integer         NOT NULL REFERENCES portfolio.account_group(id) ON DELETE RESTRICT
);

-- Depends on dim tables (all nullable FKs)

CREATE TABLE portfolio.fund (
    symbol                  varchar(20)     PRIMARY KEY,
    name                    varchar(200)    NOT NULL,
    investment_type_id      integer         REFERENCES portfolio.dim_investment_type(id) ON DELETE RESTRICT,
    market_cap_id           integer         REFERENCES portfolio.dim_market_cap(id) ON DELETE RESTRICT,
    index_type_id           integer         REFERENCES portfolio.dim_index_type(id) ON DELETE RESTRICT,
    sector_id               integer         REFERENCES portfolio.dim_sector(id) ON DELETE RESTRICT,
    region_id               integer         REFERENCES portfolio.dim_region(id) ON DELETE RESTRICT,
    objective_id            integer         REFERENCES portfolio.dim_objective(id) ON DELETE RESTRICT
);

-- Depends on investment_account and fund

CREATE TABLE portfolio.position (
    id                      serial          PRIMARY KEY,
    investment_account_id   integer         NOT NULL REFERENCES portfolio.investment_account(id) ON DELETE RESTRICT,
    symbol                  varchar(20)     NOT NULL REFERENCES portfolio.fund(symbol) ON DELETE RESTRICT,
    position_date           date            NOT NULL,
    price                   numeric(18,4)   NOT NULL,
    quantity                numeric(18,4)   NOT NULL,
    current_value           numeric(18,4)   NOT NULL,
    cost_basis              numeric(18,4)   NOT NULL,
    UNIQUE (investment_account_id, symbol, position_date)
);

-- ---------- MIGRONDI:DOWN ----------

DROP TABLE IF EXISTS portfolio.position;
DROP TABLE IF EXISTS portfolio.fund;
DROP TABLE IF EXISTS portfolio.investment_account;
DROP TABLE IF EXISTS portfolio.dim_objective;
DROP TABLE IF EXISTS portfolio.dim_region;
DROP TABLE IF EXISTS portfolio.dim_sector;
DROP TABLE IF EXISTS portfolio.dim_index_type;
DROP TABLE IF EXISTS portfolio.dim_market_cap;
DROP TABLE IF EXISTS portfolio.dim_investment_type;
DROP TABLE IF EXISTS portfolio.account_group;
DROP TABLE IF EXISTS portfolio.tax_bucket;
DROP SCHEMA IF EXISTS portfolio;
