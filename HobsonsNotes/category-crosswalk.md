# Category Crosswalk — Old Taxonomy to LeoBloom COA

> Source: `householdbudget.personalfinance.tran.category_id` (143 categories)
> Target: `leobloom_prod.ledger.account.code`
> Purpose: Seed `stage.merchant_rules` for transaction categorisation.
> Status: DRAFT — awaiting Dan's review.

## Mapping Rules

- **SKIP** = don't import as a ledger transaction (payroll line items,
  employer benefits, investment trades handled by portfolio module)
- **TRANSFER** = balance sheet movement via 9010, goes through transfer
  pairing logic
- **REVIEW** = ambiguous, needs Dan's call per transaction
- Categories not in this crosswalk are unmapped — Hobson flags for review

---

## Food & Drink (5350)

| Old Category | Code | Notes |
|---|---|---|
| Food\|Groceries | 5350 | |
| Food\|Restaurants | 5350 | |
| Food\|Food delivery | 5350 | |
| Alcohol\|Bar | 5350 | Absorbed into food per Dan |
| Alcohol\|Beer and wine | 5350 | |
| Alcohol\|Liquor store | 5350 | |

## Healthcare — HSA Eligible (5400)

| Old Category | Code | Notes |
|---|---|---|
| Healthcare/Medical\|Doctor / hospital | 5400 | |
| Healthcare/Medical\|Pharmacy and OTC meds | 5400 | |
| Healthcare/Medical\|Wellness purchases | 5400 | Most qualify for HSA |

## Healthcare — Non-HSA (5410)

| Old Category | Code | Notes |
|---|---|---|
| Healthcare/Medical\|Health insurance | 5410 | Premiums not HSA-eligible |
| Healthcare/Medical\|Dental insurance | 5410 | Premium, not procedure |
| Healthcare/Medical\|Vision insurance | 5410 | Premium |
| Healthcare/Medical\|Medical debt | 5410 | Debt payment, not eligible expense |

## Automotive (5450)

| Old Category | Code | Notes |
|---|---|---|
| Automotive\|AAA | 5450 | |
| Automotive\|Auto insurance | 5450 | |
| Automotive\|Auto maintenance and fees | 5450 | |
| Automotive\|Gasoline/Fuel | 5450 | |
| Automotive\|Tolls | 5450 | |
| Automotive\|Auto payment | TRANSFER | Dr 2230 (Auto Loan), Cr cash. Principal reduces liability; interest is expense. See open question below. |

## Utilities (5500)

| Old Category | Code | Notes |
|---|---|---|
| Utilities\|Electricity | 5500 | |
| Utilities\|Gas | 5500 | |
| Utilities\|Internet | 5500 | |
| Utilities\|Phone | 5500 | |
| Utilities\|Water / sewer | 5500 | |

## Insurance — Life & Disability (5550)

| Old Category | Code | Notes |
|---|---|---|
| Insurance\|Life insurance | 5550 | |
| Insurance\|AD&D insurance | 5550 | |

## Insurance — Other Personal (5560)

| Old Category | Code | Notes |
|---|---|---|
| Insurance\|Tuition insurance | 5560 | |

## Taxes — Itemizable (5600)

| Old Category | Code | Notes |
|---|---|---|
| Taxes\|NC state income tax | 5600 | SALT deduction |
| Taxes\|Property tax | 5312 | Already has its own account under Housing |

## Taxes — Non-Itemizable (5610)

| Old Category | Code | Notes |
|---|---|---|
| Taxes\|Federal income tax | 5610 | |
| Taxes\|Social Security (OASDI) | 5610 | |
| Taxes\|Medicare | 5610 | |

Note: Dan is no longer tracking paycheck line items. These categories
will only appear if importing historical data from the old system. Going
forward, only net deposits are tracked.

## Entertainment (5650)

| Old Category | Code | Notes |
|---|---|---|
| Entertainment\|Apple | 5650 | |
| Entertainment\|Audible | 5650 | |
| Entertainment\|Curiosity Stream / Nebula | 5650 | |
| Entertainment\|Disney | 5650 | |
| Entertainment\|Netflix | 5650 | |
| Entertainment\|Paramount Plus | 5650 | |
| Entertainment\|Peacock Tv | 5650 | |
| Entertainment\|Spotify | 5650 | |
| Entertainment\|Tumblr | 5650 | |
| Entertainment\|Other entertainment | 5650 | |
| Entertainment\|Google | 5650 | |

## Online Services (5700)

| Old Category | Code | Notes |
|---|---|---|
| Online Services\|Amazon Prime | 5700 | |
| Online Services\|Ancestry.com | 5700 | |
| Online Services\|Garmin | 5700 | |
| Online Services\|Google | 5700 | |
| Online Services\|Ground News | 5700 | |
| Online Services\|Ifit | 5700 | |
| Online Services\|Keeper Security | 5700 | |
| Online Services\|Norton Antivirus / Lifelock | 5700 | |
| Online Services\|Other online services | 5700 | |

## Travel (5750)

| Old Category | Code | Notes |
|---|---|---|
| Travel\|Activities while traveling | 5750 | |
| Travel\|Lodging | 5750 | |
| Travel\|Luggage and travel supplies | 5750 | |
| Travel\|Meals while travelling | 5750 | |
| Travel\|Transportation | 5750 | |

## Education (5800)

| Old Category | Code | Notes |
|---|---|---|
| Education | 5800 | |
| Education\|Books and tuition | 5800 | |
| Education\|Education supplies | 5800 | |

## Hobbies (5850)

| Old Category | Code | Notes |
|---|---|---|
| Hobbies\|Brewing | 5850 | |
| Hobbies\|Cooking | 5850 | |
| Hobbies\|Gardening | 5850 | |
| Hobbies\|Hiking | 5850 | |
| Hobbies\|Language learning | 5850 | |
| Hobbies\|Other | 5850 | |
| Hobbies\|Rock collecting | 5850 | |

## Personal Care (5900)

| Old Category | Code | Notes |
|---|---|---|
| Personal\|Accessories | 5900 | |
| Personal\|Beauty/hair/nails | 5900 | |
| Personal\|Jewelry | 5900 | |
| Personal\|Make-up | 5900 | |
| Personal\|Skin care products | 5900 | |

## Pets (5950)

| Old Category | Code | Notes |
|---|---|---|
| Pets\|Kenneling | 5950 | |
| Pets\|Pet supplies | 5950 | |
| Pets\|Veterinary services | 5950 | |

## General Household (6000)

| Old Category | Code | Notes |
|---|---|---|
| General household\|Random crap to keep the house running | 6000 | |
| General Merchandise\| | 6000 | |
| Clothing/Shoes\|Clothing | 6000 | |
| Clothing/Shoes\|Shoes | 6000 | |
| Electronics\| | 6000 | |
| Home ownership\|Home Improvement | 6000 | Personal home, not Lockhart |
| Home ownership\|Home repair | 6000 | |
| Home ownership\|HVAC maintenance | 6000 | |
| Home ownership\|Lawn | 6000 | |

## Charitable Contributions (6050)

| Old Category | Code | Notes |
|---|---|---|
| Charitable contributions\| | 6050 | |

## Business Expenses — Dan (6100)

| Old Category | Code | Notes |
|---|---|---|
| Business Expenses: Dan\|Advertising and marketing costs | 6100 | |
| Business Expenses: Dan\|Legal and professional fees | 6100 | |
| Business Expenses: Dan\|Subscriptions | 6100 | |
| Business Expenses: Dan\|Supplies and materials | 6100 | |
| Reimbursable expenses for work | 6100 | |

## Kids' Allowances (6150)

| Old Category | Code | Notes |
|---|---|---|
| Justin allowance\| | 6150 | |
| Rachel allowance\| | 6150 | |

## Miscellaneous (6200)

| Old Category | Code | Notes |
|---|---|---|
| Cash withdrawal\| | 6200 | |

## Jodi's Business Expenses (6300+)

| Old Category | Code | Notes |
|---|---|---|
| Business Expenses: Jodi\|Advertising and marketing costs | 6310 | |
| Business Expenses: Jodi\|Legal and professional fees | 6320 | |
| Business Expenses: Jodi\|Subscriptions | 6330 | |
| Business Expenses: Jodi\|Supplies and materials | 6340 | |

## Bank & Investment Fees (7200+)

| Old Category | Code | Notes |
|---|---|---|
| Fees\|Bank fees | 7210 | |
| Fees\|Investment fees | 7220 | |
| Fees\|Other fees | 7210 | Default to bank fees unless clearly investment-related |

## Housing (existing accounts)

| Old Category | Code | Notes |
|---|---|---|
| Home ownership\|Mortgage P&I | 5311 | See open question on principal vs interest |
| Home ownership\|Home insurance | 5313 | |

## Revenue (4xxx)

| Old Category | Code | Notes |
|---|---|---|
| Dividend\| | 4200 | Other Revenue |
| Interest\| | 4200 | |
| Cash-back reward\| | 4200 | |
| Tax refund\| | 4200 | |
| Miscellaneous income\| | 4200 | |
| Reimbursement for work expense | 4200 | |
| Merchandise refund or rebate\| | REVIEW | Should reduce the original expense account, not post as revenue |

## Transfers (9010 / pairing logic)

| Old Category | Code | Notes |
|---|---|---|
| Transfers\|Between SECU and Fidelity Brokerage | TRANSFER | |
| Transfers\|Between SECU and Fidelity Card | TRANSFER | |
| Transfers\|Between Fidelity Brokerage and Fidelity Card | TRANSFER | |
| Transfers\|Between SECU accounts | TRANSFER | |
| Transfers\|Between SECU and Amazon Card | TRANSFER | |
| Transfers\|Between SECU and Rocket | TRANSFER | |
| Transfers\|Between SECU checking and Rachel | TRANSFER | |
| Transfers\|Between TD and 401(k) | TRANSFER | |
| Transfers\|Between TD and Fidelity Brokerage | TRANSFER | |
| Transfers\|Between TD and HSA | TRANSFER | |
| Transfers\|Between TD and SECU | TRANSFER | |
| Credit card debt payment\| | TRANSFER | Dr liability, Cr cash |

## SKIP — Not Imported

| Old Category | Reason |
|---|---|
| Total wages\|Pay for hours worked | Net deposits only going forward |
| Total wages\|Bonus | |
| Total wages\|Flex PTO pay | |
| Total wages\|Holiday pay | |
| Total wages\|Vacation pay | |
| Total wages\|RSU cash-out | |
| Employer paid benefit\|401(k) match | |
| Employer paid benefit\|BYOD stipend | |
| Employer paid benefit\|Dental insurance | |
| Employer paid benefit\|HSA contribution | |
| Employer paid benefit\|Life insurance | |
| Employer paid benefit\|Long term disability insurance | |
| Employer paid benefit\|Medical Insurance | |
| Healthcare/Medical\|Dental insurance paid by employer | |
| Healthcare/Medical\|Health insurance paid by employer | |
| Insurance/Disability insurance paid by employer | |
| Insurance/Life insurance paid by employer | |
| Investments\|Invest in 401(k) | Portfolio module |
| Investments\|Invest in HSA | Portfolio module |
| Investments\|Invest in IRA | Portfolio module |
| Investments\|Invest in taxable brokerage | Portfolio module |
| Investments\|Moving funds within an IRA | Portfolio module |
| Investment sales\| | Portfolio module |

---

## Fidelity Transaction History — Handling Rules

These apply to the Fidelity CSV parser, not to merchant rules.

| Action Pattern | Taxable Brokerage | IRA Accounts |
|---|---|---|
| DIVIDEND RECEIVED (non-SPAXX) | Dr 1210, Cr 4200 | SKIP |
| DIVIDEND RECEIVED (SPAXX) | Dr 1210, Cr 4200 | SKIP |
| REINVESTMENT (SPAXX) | SKIP — internal rebalance | SKIP |
| YOU BOUGHT / YOU SOLD | SKIP — portfolio module | SKIP |
| TRANSFERRED TO/FROM | TRANSFER — pair with other leg | SKIP |
| DIRECT DEPOSIT (payroll) | SKIP — net deposit only | N/A |
| DIRECT DEPOSIT (cash-back) | Dr 1210, Cr 4200 | N/A |
| DIRECT DEBIT / BILL PAYMENT | Dr expense, Cr 1210 | N/A |
| Commission / Fees (if non-zero) | 7220 Investment Fees | SKIP |

CMA (Property) transactions follow the same rules as taxable brokerage
but post against 1110 instead of 1210. Property CMA dividends are
property revenue (4110 or 4200 — Dan's call).

---

## Open Questions for Dan

1. **Auto payment (Automotive\|Auto payment):** The Ford payment is part
   principal (reduces 2230 liability) and part interest (expense). Do we
   have the principal/interest split, or should we just post the whole
   payment as a transfer to 2230 and sort out interest later?

2. **Mortgage P&I (Home ownership\|Mortgage P&I):** Same question for the
   personal mortgage — 5311 is "Mortgage Payment" but GAAP says only the
   interest is an expense. Principal reduces 2210. Do we split, or post
   whole payment to 5311 for now?

3. **Merchandise refunds:** Should these reduce the original expense
   account (Cr 5350 if it was a grocery refund), or post as revenue to
   4200? Reducing the expense is more correct but requires knowing which
   expense it was.

4. **Property CMA dividends:** SPAXX dividends in the property CMA
   (Z52355485) — post as rental revenue (4110) or other revenue (4200)?

5. **Home maintenance vs General Household:** I mapped personal home
   improvement, repair, HVAC, and lawn to 6000 (General Household).
   Should these have their own account under Housing (5310) instead?
