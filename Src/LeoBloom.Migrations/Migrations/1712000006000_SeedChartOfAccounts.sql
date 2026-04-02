-- MIGRONDI:NAME=1712000006000_SeedChartOfAccounts.sql
-- MIGRONDI:TIMESTAMP=1712000006000
-- ---------- MIGRONDI:UP ----------

-- Inserted in parent-first order to satisfy parent_code FK.
-- Account type IDs: 1=asset, 2=liability, 3=equity, 4=revenue, 5=expense

-- 1xxx Assets
INSERT INTO ledger.account (code, name, account_type_id, parent_code) VALUES
    ('1000', 'Assets',              1, NULL),
    ('1100', 'Property Assets',     1, '1000'),
    ('1110', 'Bank A — Operating',  1, '1100'),
    ('1120', 'Bank B — Deposits',   1, '1100'),
    ('1200', 'Personal Assets',     1, '1000'),
    ('1210', 'Brokerage Account',   1, '1200'),
    ('1220', 'Bank C — Checking',   1, '1200');

-- 2xxx Liabilities
INSERT INTO ledger.account (code, name, account_type_id, parent_code) VALUES
    ('2000', 'Liabilities',                 2, NULL),
    ('2100', 'Property Liabilities',        2, '2000'),
    ('2110', 'Mortgage Payable — Rental',   2, '2100'),
    ('2200', 'Personal Liabilities',        2, '2000'),
    ('2210', 'Mortgage Payable — Personal', 2, '2200'),
    ('2220', 'Credit Card',                 2, '2200');

-- 3xxx Equity
INSERT INTO ledger.account (code, name, account_type_id, parent_code) VALUES
    ('3000', 'Equity',              3, NULL),
    ('3010', 'Owner''s Investment', 3, '3000'),
    ('3020', 'Owner''s Draws',      3, '3000'),
    ('3099', 'Retained Earnings',   3, '3000');

-- 4xxx Revenue
INSERT INTO ledger.account (code, name, account_type_id, parent_code) VALUES
    ('4000', 'Revenue',                          4, NULL),
    ('4100', 'Property Revenue',                 4, '4000'),
    ('4110', 'Rental Income — Tenant A',         4, '4100'),
    ('4120', 'Rental Income — Tenant B',         4, '4100'),
    ('4130', 'Utility Reimbursement — Tenant A', 4, '4100'),
    ('4140', 'Utility Reimbursement — Tenant B', 4, '4100'),
    ('4150', 'Utility Reimbursement — Tenant C', 4, '4100'),
    ('4200', 'Personal Revenue',                 4, '4000'),
    ('4210', 'Salary — Owner',                   4, '4200'),
    ('4220', 'Salary — Spouse',                  4, '4200');

-- 5xxx Expenses
INSERT INTO ledger.account (code, name, account_type_id, parent_code) VALUES
    ('5000', 'Expenses',                       5, NULL),
    ('5100', 'Property Expenses',              5, '5000'),
    ('5110', 'Mortgage Interest — Rental',     5, '5100'),
    ('5120', 'Water & Electric',               5, '5100'),
    ('5130', 'Gas',                            5, '5100'),
    ('5140', 'Property Tax — Rental',          5, '5100'),
    ('5150', 'Homeowners Insurance — Rental',  5, '5100'),
    ('5160', 'HOA Dues',                       5, '5100'),
    ('5170', 'Repairs & Maintenance',          5, '5100'),
    ('5180', 'Supplies',                       5, '5100'),
    ('5190', 'Depreciation',                   5, '5100'),
    ('5200', 'Lawn Care',                      5, '5100'),
    ('5210', 'Pest Control',                   5, '5100'),
    ('5300', 'Personal Expenses',              5, '5000'),
    ('5310', 'Housing',                        5, '5300'),
    ('5311', 'Mortgage Payment — Personal',    5, '5310'),
    ('5312', 'Property Tax — Personal',        5, '5310'),
    ('5313', 'Homeowners Insurance — Personal',5, '5310'),
    ('5350', 'Food',                           5, '5300'),
    ('5400', 'Healthcare',                     5, '5300'),
    ('5450', 'Automotive',                     5, '5300'),
    ('5500', 'Utilities — Personal',           5, '5300'),
    ('5550', 'Insurance',                      5, '5300'),
    ('5600', 'Taxes',                          5, '5300'),
    ('5650', 'Entertainment',                  5, '5300'),
    ('5700', 'Online Services',                5, '5300'),
    ('5750', 'Travel',                         5, '5300'),
    ('5800', 'Education',                      5, '5300'),
    ('5850', 'Hobbies',                        5, '5300'),
    ('5900', 'Personal Care',                  5, '5300'),
    ('5950', 'Pets',                           5, '5300'),
    ('6000', 'General Household',              5, '5300'),
    ('6050', 'Charitable Contributions',       5, '5300'),
    ('6100', 'Business Expenses — Owner',      5, '5300'),
    ('6150', 'Business Expenses — Spouse',     5, '5300'),
    ('6200', 'Miscellaneous',                  5, '5300');

-- 7xxx Other Income / Expense
INSERT INTO ledger.account (code, name, account_type_id, parent_code) VALUES
    ('7100', 'Other Income',    4, NULL),
    ('7110', 'Interest Income', 4, '7100'),
    ('7200', 'Other Expense',   5, NULL),
    ('7210', 'Bank Fees',       5, '7200'),
    ('7220', 'Investment Fees', 5, '7200');

-- 9xxx Memo / Non-Posting
INSERT INTO ledger.account (code, name, account_type_id, parent_code) VALUES
    ('9010', 'Inter-Account Transfer', 5, NULL);

-- ---------- MIGRONDI:DOWN ----------

DELETE FROM ledger.account WHERE code IN (
    '1000','1100','1110','1120','1200','1210','1220',
    '2000','2100','2110','2200','2210','2220',
    '3000','3010','3020','3099',
    '4000','4100','4110','4120','4130','4140','4150','4200','4210','4220',
    '5000','5100','5110','5120','5130','5140','5150','5160','5170','5180','5190','5200','5210',
    '5300','5310','5311','5312','5313','5350','5400','5450','5500','5550','5600','5650','5700','5750','5800','5850','5900','5950',
    '6000','6050','6100','6150','6200',
    '7100','7110','7200','7210','7220',
    '9010'
);
