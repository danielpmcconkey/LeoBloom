# 040 — CLI Tax Reports

**Epic:** J — CLI Consumption Layer
**Depends On:** 036, 007
**Status:** Not started

---

New report logic for tax filing. These are not thin wrappers — they require
new service implementations.

**Commands:**

```
leobloom report schedule-e --year YYYY
leobloom report general-ledger --account ACCT --from DATE --to DATE
leobloom report cash-receipts --from DATE --to DATE
leobloom report cash-disbursements --from DATE --to DATE
```

**schedule-e:** Rolls up the chart of accounts into IRS Schedule E line items:
rental income, mortgage interest, property tax, insurance, HOA, utilities,
repairs, depreciation. COA must support this mapping (verify against seed data).

**general-ledger:** Transaction-level detail for a single account. The CPA asks
"what's in this $847 utilities number?" and Dan hands her every entry.

**cash-receipts / cash-disbursements:** Journals showing all money in and all
money out, with counterparty and date. Supporting detail for income and expense
totals.

**Depreciation:** Via config, not a fixed asset module. The property has a
27.5-year straight-line depreciation schedule. Dan provides cost basis and
in-service date in config; schedule-e pulls the annual amount. Revisit if
Dan acquires more properties.

**Consumers:** CPA (via Dan). Human-readable output only — `--json` not
required for tax reports.
