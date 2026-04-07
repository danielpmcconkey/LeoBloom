-- Seed: sample funds for dev environment
-- Dimension names resolved to IDs via subqueries for readability and idempotency.

BEGIN;

INSERT INTO portfolio.fund (symbol, name, investment_type_id, market_cap_id, index_type_id, sector_id, region_id, objective_id)
VALUES
    (
        'VTI',
        'Vanguard Total Stock Market ETF',
        (SELECT id FROM portfolio.dim_investment_type WHERE name = 'ETF'),
        (SELECT id FROM portfolio.dim_market_cap     WHERE name = 'Large Cap'),
        (SELECT id FROM portfolio.dim_index_type     WHERE name = 'Index'),
        (SELECT id FROM portfolio.dim_sector         WHERE name = 'Broad Market'),
        (SELECT id FROM portfolio.dim_region         WHERE name = 'US'),
        (SELECT id FROM portfolio.dim_objective      WHERE name = 'Growth')
    ),
    (
        'VXUS',
        'Vanguard Total International Stock ETF',
        (SELECT id FROM portfolio.dim_investment_type WHERE name = 'ETF'),
        (SELECT id FROM portfolio.dim_market_cap     WHERE name = 'Large Cap'),
        (SELECT id FROM portfolio.dim_index_type     WHERE name = 'Index'),
        (SELECT id FROM portfolio.dim_sector         WHERE name = 'Broad Market'),
        (SELECT id FROM portfolio.dim_region         WHERE name = 'International Developed'),
        (SELECT id FROM portfolio.dim_objective      WHERE name = 'Growth')
    ),
    (
        'BND',
        'Vanguard Total Bond Market ETF',
        (SELECT id FROM portfolio.dim_investment_type WHERE name = 'ETF'),
        (SELECT id FROM portfolio.dim_market_cap     WHERE name = 'N/A'),
        (SELECT id FROM portfolio.dim_index_type     WHERE name = 'Index'),
        (SELECT id FROM portfolio.dim_sector         WHERE name = 'N/A'),
        (SELECT id FROM portfolio.dim_region         WHERE name = 'US'),
        (SELECT id FROM portfolio.dim_objective      WHERE name = 'Income')
    ),
    (
        'VOO',
        'Vanguard S&P 500 ETF',
        (SELECT id FROM portfolio.dim_investment_type WHERE name = 'ETF'),
        (SELECT id FROM portfolio.dim_market_cap     WHERE name = 'Large Cap'),
        (SELECT id FROM portfolio.dim_index_type     WHERE name = 'Index'),
        (SELECT id FROM portfolio.dim_sector         WHERE name = 'Broad Market'),
        (SELECT id FROM portfolio.dim_region         WHERE name = 'US'),
        (SELECT id FROM portfolio.dim_objective      WHERE name = 'Growth')
    ),
    (
        'VBTLX',
        'Vanguard Total Bond Market Index Fund',
        (SELECT id FROM portfolio.dim_investment_type WHERE name = 'Mutual Fund'),
        (SELECT id FROM portfolio.dim_market_cap     WHERE name = 'N/A'),
        (SELECT id FROM portfolio.dim_index_type     WHERE name = 'Index'),
        (SELECT id FROM portfolio.dim_sector         WHERE name = 'N/A'),
        (SELECT id FROM portfolio.dim_region         WHERE name = 'US'),
        (SELECT id FROM portfolio.dim_objective      WHERE name = 'Income')
    ),
    (
        'VTSAX',
        'Vanguard Total Stock Market Index Fund',
        (SELECT id FROM portfolio.dim_investment_type WHERE name = 'Mutual Fund'),
        (SELECT id FROM portfolio.dim_market_cap     WHERE name = 'Large Cap'),
        (SELECT id FROM portfolio.dim_index_type     WHERE name = 'Index'),
        (SELECT id FROM portfolio.dim_sector         WHERE name = 'Broad Market'),
        (SELECT id FROM portfolio.dim_region         WHERE name = 'US'),
        (SELECT id FROM portfolio.dim_objective      WHERE name = 'Growth')
    )
ON CONFLICT (symbol) DO UPDATE SET
    name                = EXCLUDED.name,
    investment_type_id  = EXCLUDED.investment_type_id,
    market_cap_id       = EXCLUDED.market_cap_id,
    index_type_id       = EXCLUDED.index_type_id,
    sector_id           = EXCLUDED.sector_id,
    region_id           = EXCLUDED.region_id,
    objective_id        = EXCLUDED.objective_id;

COMMIT;
