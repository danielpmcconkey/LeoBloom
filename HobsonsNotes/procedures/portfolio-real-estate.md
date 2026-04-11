# Portfolio Update: Real Estate Values

## When

Dan provides updated property valuations (Zillow estimate, appraisal, etc.).

## Properties

| Property | Account ID | Fund Symbol | Cost Basis |
|---|---|---|---|
| Logan Circle (primary residence) | 8 | 4107 Home | 225,000 |
| Lockhart Place (investment property) | 17 | Lockhart Pl | 384,000 |

## How

Each property is tracked as quantity=1, price=value=current estimate.
Cost basis is the purchase price and doesn't change.

```
LEOBLOOM_ENV=Production Src/LeoBloom.CLI/bin/Release/net10.0/LeoBloom.CLI \
  portfolio position record \
  --account-id <ID> --symbol "<SYMBOL>" --date <YYYY-MM-DD> \
  --price <VALUE> --quantity 1 --value <VALUE> \
  --cost-basis <COST_BASIS>
```

Note: symbols contain spaces — quote them in the CLI call.
