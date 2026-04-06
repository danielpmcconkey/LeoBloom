-- Seed: chart of accounts for dev environment
-- Source: migration 006 (SeedChartOfAccounts, includes account_subtype from P052)
-- Pattern: INSERT ... ON CONFLICT DO UPDATE (idempotent upsert)
-- Ordering: parent-first to satisfy parent_code FK on fresh databases

BEGIN;

-- 1xxx Assets
INSERT INTO ledger.account (code, name, account_type_id, parent_code, account_subtype) VALUES
    ('1000', 'Assets',              1, NULL,   NULL),
    ('1100', 'Property Assets',     1, '1000', NULL),
    ('1110', 'Bank A — Operating',  1, '1100', 'Cash'),
    ('1120', 'Bank B — Deposits',   1, '1100', 'Cash'),
    ('1200', 'Personal Assets',     1, '1000', NULL),
    ('1210', 'Brokerage Account',   1, '1200', 'Investment'),
    ('1220', 'Bank C — Checking',   1, '1200', 'Cash')
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    parent_code     = EXCLUDED.parent_code,
    account_subtype = EXCLUDED.account_subtype;

-- 2xxx Liabilities
INSERT INTO ledger.account (code, name, account_type_id, parent_code, account_subtype) VALUES
    ('2000', 'Liabilities',                 2, NULL,   NULL),
    ('2100', 'Property Liabilities',        2, '2000', NULL),
    ('2110', 'Mortgage Payable — Rental',   2, '2100', 'LongTermLiability'),
    ('2200', 'Personal Liabilities',        2, '2000', NULL),
    ('2210', 'Mortgage Payable — Personal', 2, '2200', 'LongTermLiability'),
    ('2220', 'Credit Card',                 2, '2200', 'CurrentLiability')
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    parent_code     = EXCLUDED.parent_code,
    account_subtype = EXCLUDED.account_subtype;

-- 3xxx Equity
INSERT INTO ledger.account (code, name, account_type_id, parent_code, account_subtype) VALUES
    ('3000', 'Equity',              3, NULL,   NULL),
    ('3010', 'Owner''s Investment', 3, '3000', NULL),
    ('3020', 'Owner''s Draws',      3, '3000', NULL),
    ('3099', 'Retained Earnings',   3, '3000', NULL)
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    parent_code     = EXCLUDED.parent_code,
    account_subtype = EXCLUDED.account_subtype;

-- 4xxx Revenue
INSERT INTO ledger.account (code, name, account_type_id, parent_code, account_subtype) VALUES
    ('4000', 'Revenue',                          4, NULL,   NULL),
    ('4100', 'Property Revenue',                 4, '4000', NULL),
    ('4110', 'Rental Income — Tenant A',         4, '4100', 'OperatingRevenue'),
    ('4120', 'Rental Income — Tenant B',         4, '4100', 'OperatingRevenue'),
    ('4130', 'Utility Reimbursement — Tenant A', 4, '4100', 'OperatingRevenue'),
    ('4140', 'Utility Reimbursement — Tenant B', 4, '4100', 'OperatingRevenue'),
    ('4150', 'Utility Reimbursement — Tenant C', 4, '4100', 'OperatingRevenue'),
    ('4200', 'Personal Revenue',                 4, '4000', NULL),
    ('4210', 'Salary — Owner',                   4, '4200', 'OperatingRevenue'),
    ('4220', 'Salary — Spouse',                  4, '4200', 'OperatingRevenue')
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    parent_code     = EXCLUDED.parent_code,
    account_subtype = EXCLUDED.account_subtype;

-- 5xxx Expenses
INSERT INTO ledger.account (code, name, account_type_id, parent_code, account_subtype) VALUES
    ('5000', 'Expenses',                       5, NULL,   NULL),
    ('5100', 'Property Expenses',              5, '5000', NULL),
    ('5110', 'Mortgage Interest — Rental',     5, '5100', 'OperatingExpense'),
    ('5120', 'Water & Electric',               5, '5100', 'OperatingExpense'),
    ('5130', 'Gas',                            5, '5100', 'OperatingExpense'),
    ('5140', 'Property Tax — Rental',          5, '5100', 'OperatingExpense'),
    ('5150', 'Homeowners Insurance — Rental',  5, '5100', 'OperatingExpense'),
    ('5160', 'HOA Dues',                       5, '5100', 'OperatingExpense'),
    ('5170', 'Repairs & Maintenance',          5, '5100', 'OperatingExpense'),
    ('5180', 'Supplies',                       5, '5100', 'OperatingExpense'),
    ('5190', 'Depreciation',                   5, '5100', 'OperatingExpense'),
    ('5200', 'Lawn Care',                      5, '5100', 'OperatingExpense'),
    ('5210', 'Pest Control',                   5, '5100', 'OperatingExpense'),
    ('5300', 'Personal Expenses',              5, '5000', NULL),
    ('5310', 'Housing',                        5, '5300', NULL),
    ('5311', 'Mortgage Payment — Personal',    5, '5310', 'OperatingExpense'),
    ('5312', 'Property Tax — Personal',        5, '5310', 'OperatingExpense'),
    ('5313', 'Homeowners Insurance — Personal',5, '5310', 'OperatingExpense'),
    ('5350', 'Food',                           5, '5300', 'OperatingExpense'),
    ('5400', 'Healthcare',                     5, '5300', 'OperatingExpense'),
    ('5450', 'Automotive',                     5, '5300', 'OperatingExpense'),
    ('5500', 'Utilities — Personal',           5, '5300', 'OperatingExpense'),
    ('5550', 'Insurance',                      5, '5300', 'OperatingExpense'),
    ('5600', 'Taxes',                          5, '5300', 'OperatingExpense'),
    ('5650', 'Entertainment',                  5, '5300', 'OperatingExpense'),
    ('5700', 'Online Services',                5, '5300', 'OperatingExpense'),
    ('5750', 'Travel',                         5, '5300', 'OperatingExpense'),
    ('5800', 'Education',                      5, '5300', 'OperatingExpense'),
    ('5850', 'Hobbies',                        5, '5300', 'OperatingExpense'),
    ('5900', 'Personal Care',                  5, '5300', 'OperatingExpense'),
    ('5950', 'Pets',                           5, '5300', 'OperatingExpense'),
    ('6000', 'General Household',              5, '5300', 'OperatingExpense'),
    ('6050', 'Charitable Contributions',       5, '5300', 'OperatingExpense'),
    ('6100', 'Business Expenses — Owner',      5, '5300', 'OperatingExpense'),
    ('6150', 'Business Expenses — Spouse',     5, '5300', 'OperatingExpense'),
    ('6200', 'Miscellaneous',                  5, '5300', 'OperatingExpense')
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    parent_code     = EXCLUDED.parent_code,
    account_subtype = EXCLUDED.account_subtype;

-- 7xxx Other Income / Expense
INSERT INTO ledger.account (code, name, account_type_id, parent_code, account_subtype) VALUES
    ('7100', 'Other Income',    4, NULL,   NULL),
    ('7110', 'Interest Income', 4, '7100', 'OtherRevenue'),
    ('7200', 'Other Expense',   5, NULL,   NULL),
    ('7210', 'Bank Fees',       5, '7200', 'OtherExpense'),
    ('7220', 'Investment Fees', 5, '7200', 'OtherExpense')
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    parent_code     = EXCLUDED.parent_code,
    account_subtype = EXCLUDED.account_subtype;

-- 9xxx Memo / Non-Posting
INSERT INTO ledger.account (code, name, account_type_id, parent_code, account_subtype) VALUES
    ('9010', 'Inter-Account Transfer', 5, NULL, NULL)
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    parent_code     = EXCLUDED.parent_code,
    account_subtype = EXCLUDED.account_subtype;

COMMIT;
