-- Seed: account groups for dev environment

BEGIN;

INSERT INTO portfolio.account_group (name) VALUES
    ('Retirement 401(k)'),
    ('Roth IRAs'),
    ('HSA Accounts'),
    ('Brokerage'),
    ('Home Equity')
ON CONFLICT (name) DO UPDATE SET
    name = EXCLUDED.name;

COMMIT;
