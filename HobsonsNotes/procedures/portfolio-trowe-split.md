# Portfolio Update: T. Rowe Price Share Split

## When

Dan provides T. Rowe Price 401(k) data. T. Rowe reports a single combined
position but splits the balance into Tax Deferred and Roth. We need to
derive per-account share counts.

## Accounts

| Account | Portfolio ID | Tax Bucket |
|---|---|---|
| T. Rowe Price 401(k) (traditional) | 14 | Tax deferred |
| T. Rowe Price 401(k) (Roth) | 13 | Tax free Roth |

## Fund

Symbol: **VI5TC** (VANGUARD INST 500 INDEX TRUST)

Note: T. Rowe may report the name as "VANGUARD INST 500 IDX TR B" — same
fund, different share class label. Use VI5TC.

## How to Split

Dan provides: total shares, price per share, average cost per share,
and the balance for each bucket (traditional and Roth).

1. Compute total value: `trad_balance + roth_balance`
2. Traditional shares: `total_shares * trad_balance / total_value`
3. Roth shares: `total_shares - trad_shares` (ensures exact sum)
4. Traditional cost basis: `trad_shares * avg_cost`
5. Roth cost basis: `roth_shares * avg_cost`

Round shares to 4 decimal places.

## Record

Two CLI calls, one per account, using the computed values:
- account-id 14: traditional shares, trad_balance as value
- account-id 13: roth shares, roth_balance as value

Date: use the date Dan provides the data (or the statement date if given).
