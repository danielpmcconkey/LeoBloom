-- MIGRONDI:NAME=1712000021000_AddAccountSubType.sql
-- MIGRONDI:TIMESTAMP=1712000021000
-- ---------- MIGRONDI:UP ----------

ALTER TABLE ledger.account ADD COLUMN account_subtype varchar(25);

-- Set subtypes on existing seed accounts (leaf accounts only; headers stay null)

-- Assets: Cash
UPDATE ledger.account SET account_subtype = 'Cash' WHERE code IN ('1110', '1120', '1220');

-- Assets: Investment
UPDATE ledger.account SET account_subtype = 'Investment' WHERE code = '1210';

-- Liabilities: LongTermLiability
UPDATE ledger.account SET account_subtype = 'LongTermLiability' WHERE code IN ('2110', '2210');

-- Liabilities: CurrentLiability
UPDATE ledger.account SET account_subtype = 'CurrentLiability' WHERE code = '2220';

-- Revenue: OperatingRevenue
UPDATE ledger.account SET account_subtype = 'OperatingRevenue'
WHERE code IN ('4110', '4120', '4130', '4140', '4150', '4210', '4220');

-- Revenue: OtherRevenue
UPDATE ledger.account SET account_subtype = 'OtherRevenue' WHERE code = '7110';

-- Expenses: OperatingExpense
UPDATE ledger.account SET account_subtype = 'OperatingExpense'
WHERE code IN (
    '5110', '5120', '5130', '5140', '5150', '5160', '5170', '5180', '5190', '5200', '5210',
    '5311', '5312', '5313', '5350', '5400', '5450', '5500', '5550', '5600', '5650',
    '5700', '5750', '5800', '5850', '5900', '5950',
    '6000', '6050', '6100', '6150', '6200'
);

-- Expenses: OtherExpense
UPDATE ledger.account SET account_subtype = 'OtherExpense' WHERE code IN ('7210', '7220');

-- ---------- MIGRONDI:DOWN ----------

ALTER TABLE ledger.account DROP COLUMN account_subtype;
