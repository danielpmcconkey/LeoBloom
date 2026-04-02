# Sample Chart of Accounts — Dev / Test

Anonymized version of the production COA for BD's use. Structure, numbering,
and account types are identical to prod. Names are generic.

---

## 1xxx — Assets

| Code | Parent | Name | Account Type |
|------|--------|------|--------------|
| 1000 | — | Assets | asset |
| 1100 | 1000 | Property Assets | asset |
| 1110 | 1100 | Bank A — Operating | asset |
| 1120 | 1100 | Bank B — Deposits | asset |
| 1200 | 1000 | Personal Assets | asset |
| 1210 | 1200 | Brokerage Account | asset |
| 1220 | 1200 | Bank C — Checking | asset |

## 2xxx — Liabilities

| Code | Parent | Name | Account Type |
|------|--------|------|--------------|
| 2000 | — | Liabilities | liability |
| 2100 | 2000 | Property Liabilities | liability |
| 2110 | 2100 | Mortgage Payable — Rental | liability |
| 2200 | 2000 | Personal Liabilities | liability |
| 2210 | 2200 | Mortgage Payable — Personal | liability |
| 2220 | 2200 | Credit Card | liability |

## 3xxx — Equity

| Code | Parent | Name | Account Type |
|------|--------|------|--------------|
| 3000 | — | Equity | equity |
| 3010 | 3000 | Owner's Investment | equity |
| 3020 | 3000 | Owner's Draws | equity |
| 3099 | 3000 | Retained Earnings | equity |

## 4xxx — Revenue

| Code | Parent | Name | Account Type |
|------|--------|------|--------------|
| 4000 | — | Revenue | revenue |
| 4100 | 4000 | Property Revenue | revenue |
| 4110 | 4100 | Rental Income — Tenant A | revenue |
| 4120 | 4100 | Rental Income — Tenant B | revenue |
| 4130 | 4100 | Utility Reimbursement — Tenant A | revenue |
| 4140 | 4100 | Utility Reimbursement — Tenant B | revenue |
| 4150 | 4100 | Utility Reimbursement — Tenant C | revenue |
| 4200 | 4000 | Personal Revenue | revenue |
| 4210 | 4200 | Salary — Owner | revenue |
| 4220 | 4200 | Salary — Spouse | revenue |

## 5xxx — Expenses

| Code | Parent | Name | Account Type |
|------|--------|------|--------------|
| 5000 | — | Expenses | expense |
| 5100 | 5000 | Property Expenses | expense |
| 5110 | 5100 | Mortgage Interest — Rental | expense |
| 5120 | 5100 | Water & Electric | expense |
| 5130 | 5100 | Gas | expense |
| 5140 | 5100 | Property Tax — Rental | expense |
| 5150 | 5100 | Homeowners Insurance — Rental | expense |
| 5160 | 5100 | HOA Dues | expense |
| 5170 | 5100 | Repairs & Maintenance | expense |
| 5180 | 5100 | Supplies | expense |
| 5190 | 5100 | Depreciation | expense |
| 5200 | 5100 | Lawn Care | expense |
| 5210 | 5100 | Pest Control | expense |
| 5300 | 5000 | Personal Expenses | expense |
| 5310 | 5300 | Housing | expense |
| 5311 | 5310 | Mortgage Payment — Personal | expense |
| 5312 | 5310 | Property Tax — Personal | expense |
| 5313 | 5310 | Homeowners Insurance — Personal | expense |
| 5350 | 5300 | Food | expense |
| 5400 | 5300 | Healthcare | expense |
| 5450 | 5300 | Automotive | expense |
| 5500 | 5300 | Utilities — Personal | expense |
| 5550 | 5300 | Insurance | expense |
| 5600 | 5300 | Taxes | expense |
| 5650 | 5300 | Entertainment | expense |
| 5700 | 5300 | Online Services | expense |
| 5750 | 5300 | Travel | expense |
| 5800 | 5300 | Education | expense |
| 5850 | 5300 | Hobbies | expense |
| 5900 | 5300 | Personal Care | expense |
| 5950 | 5300 | Pets | expense |
| 6000 | 5300 | General Household | expense |
| 6050 | 5300 | Charitable Contributions | expense |
| 6100 | 5300 | Business Expenses — Owner | expense |
| 6150 | 5300 | Business Expenses — Spouse | expense |
| 6200 | 5300 | Miscellaneous | expense |

## 7xxx — Other Income / Expense

| Code | Parent | Name | Account Type |
|------|--------|------|--------------|
| 7000 | — | Other Income / Expense | expense |
| 7010 | 7000 | Bank Fees | expense |
| 7020 | 7000 | Interest Income | revenue |
| 7030 | 7000 | Investment Fees | expense |

## 9xxx — Memo / Non-Posting

| Code | Parent | Name | Account Type |
|------|--------|------|--------------|
| 9010 | — | Inter-Account Transfer | expense |
