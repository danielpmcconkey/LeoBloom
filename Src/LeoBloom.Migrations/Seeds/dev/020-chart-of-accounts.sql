-- Seed: chart of accounts for dev environment
-- Source: migration 006 (SeedChartOfAccounts, includes account_subtype from P052)
-- Pattern: INSERT ... ON CONFLICT DO UPDATE (idempotent upsert)
-- Parent-child links are set via parent_id (int FK) in a second pass.

BEGIN;

-- Pass 1: Upsert all accounts (parent_id set to NULL; corrected in pass 2)

-- 1xxx Assets
INSERT INTO ledger.account (code, name, account_type_id, account_subtype) VALUES
    ('1000', 'Assets',              1, NULL),
    ('1100', 'Property Assets',     1, NULL),
    ('1110', 'Bank A — Operating',  1, 'Cash'),
    ('1120', 'Bank B — Deposits',   1, 'Cash'),
    ('1200', 'Personal Assets',     1, NULL),
    ('1210', 'Brokerage Account',   1, 'Investment'),
    ('1220', 'Bank C — Checking',   1, 'Cash')
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    account_subtype = EXCLUDED.account_subtype;

-- 2xxx Liabilities
INSERT INTO ledger.account (code, name, account_type_id, account_subtype) VALUES
    ('2000', 'Liabilities',                 2, NULL),
    ('2100', 'Property Liabilities',        2, NULL),
    ('2110', 'Mortgage Payable — Rental',   2, 'LongTermLiability'),
    ('2200', 'Personal Liabilities',        2, NULL),
    ('2210', 'Mortgage Payable — Personal', 2, 'LongTermLiability'),
    ('2220', 'Credit Card',                 2, 'CurrentLiability')
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    account_subtype = EXCLUDED.account_subtype;

-- 3xxx Equity
INSERT INTO ledger.account (code, name, account_type_id, account_subtype) VALUES
    ('3000', 'Equity',              3, NULL),
    ('3010', 'Owner''s Investment', 3, NULL),
    ('3020', 'Owner''s Draws',      3, NULL),
    ('3099', 'Retained Earnings',   3, NULL)
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    account_subtype = EXCLUDED.account_subtype;

-- 4xxx Revenue
INSERT INTO ledger.account (code, name, account_type_id, account_subtype) VALUES
    ('4000', 'Revenue',                          4, NULL),
    ('4100', 'Property Revenue',                 4, NULL),
    ('4110', 'Rental Income — Tenant A',         4, 'OperatingRevenue'),
    ('4120', 'Rental Income — Tenant B',         4, 'OperatingRevenue'),
    ('4130', 'Utility Reimbursement — Tenant A', 4, 'OperatingRevenue'),
    ('4140', 'Utility Reimbursement — Tenant B', 4, 'OperatingRevenue'),
    ('4150', 'Utility Reimbursement — Tenant C', 4, 'OperatingRevenue'),
    ('4200', 'Personal Revenue',                 4, NULL),
    ('4210', 'Salary — Owner',                   4, 'OperatingRevenue'),
    ('4220', 'Salary — Spouse',                  4, 'OperatingRevenue')
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    account_subtype = EXCLUDED.account_subtype;

-- 5xxx Expenses
INSERT INTO ledger.account (code, name, account_type_id, account_subtype) VALUES
    ('5000', 'Expenses',                       5, NULL),
    ('5100', 'Property Expenses',              5, NULL),
    ('5110', 'Mortgage Interest — Rental',     5, 'OperatingExpense'),
    ('5120', 'Water & Electric',               5, 'OperatingExpense'),
    ('5130', 'Gas',                            5, 'OperatingExpense'),
    ('5140', 'Property Tax — Rental',          5, 'OperatingExpense'),
    ('5150', 'Homeowners Insurance — Rental',  5, 'OperatingExpense'),
    ('5160', 'HOA Dues',                       5, 'OperatingExpense'),
    ('5170', 'Repairs & Maintenance',          5, 'OperatingExpense'),
    ('5180', 'Supplies',                       5, 'OperatingExpense'),
    ('5190', 'Depreciation',                   5, 'OperatingExpense'),
    ('5200', 'Lawn Care',                      5, 'OperatingExpense'),
    ('5210', 'Pest Control',                   5, 'OperatingExpense'),
    ('5300', 'Personal Expenses',              5, NULL),
    ('5310', 'Housing',                        5, NULL),
    ('5311', 'Mortgage Payment — Personal',    5, 'OperatingExpense'),
    ('5312', 'Property Tax — Personal',        5, 'OperatingExpense'),
    ('5313', 'Homeowners Insurance — Personal',5, 'OperatingExpense'),
    ('5350', 'Food',                           5, 'OperatingExpense'),
    ('5400', 'Healthcare',                     5, 'OperatingExpense'),
    ('5450', 'Automotive',                     5, 'OperatingExpense'),
    ('5500', 'Utilities — Personal',           5, 'OperatingExpense'),
    ('5550', 'Insurance',                      5, 'OperatingExpense'),
    ('5600', 'Taxes',                          5, 'OperatingExpense'),
    ('5650', 'Entertainment',                  5, 'OperatingExpense'),
    ('5700', 'Online Services',                5, 'OperatingExpense'),
    ('5750', 'Travel',                         5, 'OperatingExpense'),
    ('5800', 'Education',                      5, 'OperatingExpense'),
    ('5850', 'Hobbies',                        5, 'OperatingExpense'),
    ('5900', 'Personal Care',                  5, 'OperatingExpense'),
    ('5950', 'Pets',                           5, 'OperatingExpense'),
    ('6000', 'General Household',              5, 'OperatingExpense'),
    ('6050', 'Charitable Contributions',       5, 'OperatingExpense'),
    ('6100', 'Business Expenses — Owner',      5, 'OperatingExpense'),
    ('6150', 'Business Expenses — Spouse',     5, 'OperatingExpense'),
    ('6200', 'Miscellaneous',                  5, 'OperatingExpense')
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    account_subtype = EXCLUDED.account_subtype;

-- 7xxx Other Income / Expense
INSERT INTO ledger.account (code, name, account_type_id, account_subtype) VALUES
    ('7100', 'Other Income',    4, NULL),
    ('7110', 'Interest Income', 4, 'OtherRevenue'),
    ('7200', 'Other Expense',   5, NULL),
    ('7210', 'Bank Fees',       5, 'OtherExpense'),
    ('7220', 'Investment Fees', 5, 'OtherExpense')
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    account_subtype = EXCLUDED.account_subtype;

-- 9xxx Memo / Non-Posting
INSERT INTO ledger.account (code, name, account_type_id, account_subtype) VALUES
    ('9010', 'Inter-Account Transfer', 5, NULL)
ON CONFLICT (code) DO UPDATE SET
    name            = EXCLUDED.name,
    account_type_id = EXCLUDED.account_type_id,
    account_subtype = EXCLUDED.account_subtype;

-- Pass 2: Set parent_id for all child accounts via a single join-based UPDATE
UPDATE ledger.account child
SET parent_id = parent.id
FROM ledger.account parent
WHERE (child.code, parent.code) IN (
    ('1100', '1000'), ('1200', '1000'),
    ('1110', '1100'), ('1120', '1100'),
    ('1210', '1200'), ('1220', '1200'),
    ('2100', '2000'), ('2200', '2000'),
    ('2110', '2100'),
    ('2210', '2200'), ('2220', '2200'),
    ('3010', '3000'), ('3020', '3000'), ('3099', '3000'),
    ('4100', '4000'), ('4200', '4000'),
    ('4110', '4100'), ('4120', '4100'), ('4130', '4100'), ('4140', '4100'), ('4150', '4100'),
    ('4210', '4200'), ('4220', '4200'),
    ('5100', '5000'), ('5300', '5000'),
    ('5110', '5100'), ('5120', '5100'), ('5130', '5100'), ('5140', '5100'), ('5150', '5100'),
    ('5160', '5100'), ('5170', '5100'), ('5180', '5100'), ('5190', '5100'), ('5200', '5100'), ('5210', '5100'),
    ('5310', '5300'), ('5350', '5300'), ('5400', '5300'), ('5450', '5300'), ('5500', '5300'),
    ('5550', '5300'), ('5600', '5300'), ('5650', '5300'), ('5700', '5300'), ('5750', '5300'),
    ('5800', '5300'), ('5850', '5300'), ('5900', '5300'), ('5950', '5300'),
    ('6000', '5300'), ('6050', '5300'), ('6100', '5300'), ('6150', '5300'), ('6200', '5300'),
    ('5311', '5310'), ('5312', '5310'), ('5313', '5310'),
    ('7110', '7100'),
    ('7210', '7200'), ('7220', '7200')
);

COMMIT;
