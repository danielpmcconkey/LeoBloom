# Lockhart: Payment Received

## When

Dan confirms a tenant payment has been received (Zelle cleared, cheque
deposited, etc.).

## Steps

### 1. Find the instance

```
LEOBLOOM_ENV=Production .../LeoBloom.CLI \
  obligation instance list --agreement-id <AGREEMENT_ID>
```

Or query directly:
```sql
SELECT oi.id, oa.name, oi.name AS instance, oi.amount, oi.status
FROM ops.obligation_instance oi
JOIN ops.obligation_agreement oa ON oi.obligation_agreement_id = oa.id
WHERE oi.status IN ('expected', 'in_flight', 'overdue')
ORDER BY oi.expected_date;
```

### 2. Transition to confirmed

```
LEOBLOOM_ENV=Production .../LeoBloom.CLI \
  obligation instance transition <INSTANCE_ID> \
  --to confirmed \
  --amount <AMOUNT_PAID> \
  --date <DATE_RECEIVED> \
  --notes "<payment method and details>"
```

Example:
```
obligation instance transition 1 \
  --to confirmed \
  --amount 818.28 \
  --date 2026-04-07 \
  --notes "Cheque deposited to Ally, cleared Apr 7"
```

## Payment Methods by Tenant

| Tenant | Method | Details |
|---|---|---|
| Brian | Cheque | Remote-deposited to Ally business checking |
| Alex | Zelle | Sends to Dan's Ally (business account) |
| Justin | Zelle (utilities only) | Dan pays Justin's rent to himself |

## Status Transitions

```
expected → in_flight → confirmed
                    ↘ overdue → confirmed (late payment)
```

- `in_flight`: invoice sent, awaiting payment
- `confirmed`: payment received and verified
- `overdue`: past due date, not yet paid

## Partial Payments

If a tenant pays a different amount than invoiced, use `--amount` to record
the actual amount received and `--notes` to explain the discrepancy.
