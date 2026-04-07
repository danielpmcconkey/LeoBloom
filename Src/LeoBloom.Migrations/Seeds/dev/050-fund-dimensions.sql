-- Seed: fund classification dimensions for dev environment

BEGIN;

INSERT INTO portfolio.dim_investment_type (name) VALUES
    ('Stock'),
    ('Bond'),
    ('ETF'),
    ('Mutual Fund'),
    ('Money Market'),
    ('Real Estate'),
    ('Target Date'),
    ('Stable Value')
ON CONFLICT (name) DO UPDATE SET
    name = EXCLUDED.name;

INSERT INTO portfolio.dim_market_cap (name) VALUES
    ('Large Cap'),
    ('Mid Cap'),
    ('Small Cap'),
    ('N/A')
ON CONFLICT (name) DO UPDATE SET
    name = EXCLUDED.name;

INSERT INTO portfolio.dim_index_type (name) VALUES
    ('Index'),
    ('Individual'),
    ('Blend')
ON CONFLICT (name) DO UPDATE SET
    name = EXCLUDED.name;

INSERT INTO portfolio.dim_sector (name) VALUES
    ('Technology'),
    ('Healthcare'),
    ('Financials'),
    ('Energy'),
    ('Consumer Discretionary'),
    ('Consumer Staples'),
    ('Industrials'),
    ('Utilities'),
    ('Real Estate'),
    ('Materials'),
    ('Communication Services'),
    ('Broad Market'),
    ('N/A')
ON CONFLICT (name) DO UPDATE SET
    name = EXCLUDED.name;

INSERT INTO portfolio.dim_region (name) VALUES
    ('US'),
    ('International Developed'),
    ('Emerging Markets'),
    ('Global'),
    ('N/A')
ON CONFLICT (name) DO UPDATE SET
    name = EXCLUDED.name;

INSERT INTO portfolio.dim_objective (name) VALUES
    ('Growth'),
    ('Income'),
    ('Growth & Income'),
    ('Capital Preservation'),
    ('Aggressive Growth'),
    ('Balanced')
ON CONFLICT (name) DO UPDATE SET
    name = EXCLUDED.name;

COMMIT;
