# Lockhart: Invoice Sent

## When

Dan generates and sends monthly tenant invoices (typically 1st of the month
for the prior month's rent, paid in arrears).

## Tenant Obligations

| Tenant | Agreement ID | Rent | Notes |
|---|---|---|---|
| Brian McConkey | 1 | $1,000/mo | Full rent + 1/3 utilities |
| Alex Chambers | 2 | $700/mo | Full rent + 1/3 utilities |
| Justin McConkey | 3 | $0 rent | 1/3 utilities only (Dan pays Justin's rent) |

## Steps

### 1. Spawn the obligation instance (if not already spawned)

```
LEOBLOOM_ENV=Production .../LeoBloom.CLI \
  obligation instance spawn <AGREEMENT_ID> \
  --from <PERIOD_START> --to <PERIOD_END>
```

Example for April (covering March rent):
```
obligation instance spawn 1 --from 2026-03-01 --to 2026-03-31
```

Check existing instances first to avoid duplicates:
```
obligation instance list --agreement-id <ID>
```

### 2. Record the invoice

```
LEOBLOOM_ENV=Production .../LeoBloom.CLI \
  invoice record \
  --tenant "<TENANT_NAME>" \
  --fiscal-period-id <PERIOD_ID> \
  --rent-amount <RENT> \
  --utility-share <UTIL_SHARE> \
  --total-amount <TOTAL> \
  --generated-at <ISO_TIMESTAMP> \
  --document-path <PATH_TO_PDF>
```

### 3. Transition instance to in_flight

Once the invoice is sent to the tenant:
```
LEOBLOOM_ENV=Production .../LeoBloom.CLI \
  obligation instance transition <INSTANCE_ID> \
  --to in_flight \
  --date <DATE_SENT> \
  --notes "Invoice sent"
```

## Fiscal Period Lookup

Periods use `period_key` format `YYYY-MM`. To find the ID:
```sql
SELECT id, period_key FROM ledger.fiscal_period
WHERE period_key = '2026-04';
```

## Invoice PDF Paths

- Local: `/home/dan/penthouse-pete/property/invoices/`
- NAS archive: `/mnt/media/BusinessRecords/LockhartPl/Tenants/`
- Generator script: `/home/dan/penthouse-pete/property/generate_bills.py`

## Utility Split

Utilities (City of Concord water/electric + Enbridge gas) split equally
three ways. The total utility amount and each tenant's 1/3 share come from
the bills Dan receives. The generator script calculates the split.

## Outstanding Adjustments

Check obligation instances for shortfalls before generating. If a tenant
underpaid a prior month, add the difference to their next invoice total.

- **Justin: +$2.00** carry-forward from Feb 2026 (invoiced $68.28, paid $66.28)
