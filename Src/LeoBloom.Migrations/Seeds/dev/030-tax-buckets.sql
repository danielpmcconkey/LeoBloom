-- Seed: tax buckets for dev environment

BEGIN;

INSERT INTO portfolio.tax_bucket (name) VALUES
    ('Tax deferred'),
    ('Tax free HSA'),
    ('Tax free Roth'),
    ('Tax on capital gains'),
    ('Primary residence')
ON CONFLICT (name) DO UPDATE SET
    name = EXCLUDED.name;

COMMIT;
