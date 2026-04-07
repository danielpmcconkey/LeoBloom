# Project 022 — Balance Projection: Plan

## Objective

Build a `BalanceProjectionService` that computes projected daily balances for
an account over a future date range. The projection combines current ledger
balance with expected obligation inflows/outflows and in-flight transfers to
produce a daily series (the "curve"). Pure computation — no persistence, no
new tables.

Expose via CLI as `leobloom report projection --account <code> --to <date>`.

## Architecture Decision

**Where does this live?** The Reporting project (`LeoBloom.Reporting`) only
references Domain, Utilities, and Ledger — it cannot see Ops. Balance
projection needs obligation instances and transfers (Ops data) plus current
balance (Ledger data).

**Decision:** Add the service to `LeoBloom.Ops`, which already references
Ledger. The repository joins Ops tables with Ledger account data in a single
query. This avoids adding cross-project dependencies and keeps the dependency
graph clean (Ops → Ledger, not Reporting → Ops).

The domain types go in `LeoBloom.Domain.Ops` alongside the existing
obligation/transfer types since the projection is fundamentally about
projecting obligation and transfer impacts.

## Phases

### Phase 1: Domain Types

**What:** Define the projection output types in `Domain/Ops.fs`.

**Types to add:**

```fsharp
type ProjectionLineItem =
    { date: DateOnly
      description: string
      amount: decimal option       // None = unknown amount
      direction: ProjectionDirection
      sourceType: ProjectionSourceType }

type ProjectionDirection = Inflow | Outflow

type ProjectionSourceType =
    | ObligationInflow
    | ObligationOutflow
    | TransferIn
    | TransferOut

type ProjectionDayDetail =
    { date: DateOnly
      openingBalance: decimal
      items: ProjectionLineItem list
      knownNetChange: decimal      // sum of items with known amounts
      closingBalance: decimal      // opening + knownNetChange
      hasUnknownAmounts: bool }    // true if any item.amount = None

type BalanceProjection =
    { accountId: int
      accountCode: string
      accountName: string
      asOfDate: DateOnly           // today — the anchor
      projectionEndDate: DateOnly
      currentBalance: decimal
      days: ProjectionDayDetail list }
```

**Files:** `Src/LeoBloom.Domain/Ops.fs` (append to end)

**Verification:** Project compiles. Types are accessible from Ops project.

---

### Phase 2: Repository — Projection Data Query

**What:** Create `BalanceProjectionRepository.fs` in `LeoBloom.Ops` with two
queries that fetch the raw data the service needs.

**Function 1 — `getProjectedObligationItems`:**
```sql
SELECT oi.expected_date, oi.amount, oa.obligation_type_id,
       oa.source_account_id, oa.dest_account_id, oa.name as agreement_name,
       oi.name as instance_name
FROM ops.obligation_instance oi
JOIN ops.obligation_agreement oa ON oa.id = oi.obligation_agreement_id
WHERE oi.is_active = true
  AND oi.status IN ('expected', 'in_flight')
  AND oi.expected_date >= @from_date
  AND oi.expected_date <= @to_date
  AND (
    (oa.obligation_type_id = (SELECT id FROM ops.obligation_type WHERE name = 'receivable')
     AND oa.dest_account_id = @account_id)
    OR
    (oa.obligation_type_id = (SELECT id FROM ops.obligation_type WHERE name = 'payable')
     AND oa.source_account_id = @account_id)
  )
ORDER BY oi.expected_date, oa.name
```

Returns: list of `ProjectionLineItem` (mapped from SQL rows, with direction
derived from obligation_type).

**Function 2 — `getProjectedTransferItems`:**
```sql
SELECT t.initiated_date, t.expected_settlement, t.amount,
       t.from_account_id, t.to_account_id, t.description
FROM ops.transfer t
WHERE t.is_active = true
  AND t.status = 'initiated'
  AND (t.from_account_id = @account_id OR t.to_account_id = @account_id)
```

Note: Transfers don't have an `expected_date` per se. Use
`expected_settlement` if available, otherwise `initiated_date` as the
projection date. Only include transfers whose projection date falls within
the range.

Returns: list of `ProjectionLineItem`.

**Files:**
- Create `Src/LeoBloom.Ops/BalanceProjectionRepository.fs`
- Add to `Src/LeoBloom.Ops/LeoBloom.Ops.fsproj` compile list

**Verification:** Project compiles. Repository functions accept
`(txn, accountId, fromDate, toDate)` and return `ProjectionLineItem list`.

---

### Phase 3: Service — BalanceProjectionService

**What:** Create `BalanceProjectionService.fs` in `LeoBloom.Ops`. This is the
orchestrator that computes the daily series.

**Public function:**
```fsharp
let project
    (accountCode: string)
    (projectionEndDate: DateOnly)
    : Result<BalanceProjection, string list>
```

**Logic:**
1. **Validate** — `projectionEndDate` must be strictly after today. If not,
   return `Error ["Projection end date must be in the future"]`.
2. **Resolve account** — Use `AccountBalanceService.showAccountByCode` to
   verify the account exists. Extract `accountId`.
3. **Get current balance** — Use `AccountBalanceService.getBalanceByCode`
   with `asOfDate = today`.
4. **Fetch projection items** — Call both repository functions to get
   obligation items and transfer items for `(today+1, projectionEndDate)`.
   (Today's obligations are already reflected in current balance if posted;
   if still expected, include today too. Actually: use `today` as fromDate
   since expected/in_flight items for today haven't hit the balance yet.)
5. **Build daily series** — For each day from today to projectionEndDate:
   - Filter items whose date matches this day.
   - Opening balance = previous day's closing balance (day 0 = current balance).
   - Known net change = sum of items with `Some amount` (inflows positive,
     outflows negative).
   - Closing balance = opening + known net change.
   - `hasUnknownAmounts` = any item has `amount = None`.
6. **Optimize** — Don't emit a `ProjectionDayDetail` for every single day.
   Only emit days where something changes (items exist) plus the first and
   last day. Days with no activity are implied by carrying forward the
   previous closing balance. Wait — spec says "daily series." Emit every day.
   If the range is huge (>365 days), that's fine. Keep it simple.

**Edge case handling:**
- **No future obligations/transfers** → every day has empty items, closing
  balance equals current balance. Flat line.
- **Null-amount obligations** → `ProjectionLineItem.amount = None`,
  `description` includes agreement/instance name. `hasUnknownAmounts = true`
  on that day. Closing balance only reflects known amounts.
- **Same-day obligations** → multiple items on one day, all listed in
  `items`. `knownNetChange` is the sum. Itemized by default.

**Files:**
- Create `Src/LeoBloom.Ops/BalanceProjectionService.fs`
- Add to `Src/LeoBloom.Ops/LeoBloom.Ops.fsproj` compile list

**Verification:** Project compiles. Service can be called from CLI. Manual
test with an account that has obligations yields a non-trivial projection.

---

### Phase 4: CLI Command

**What:** Add `projection` subcommand under the existing `report` command
group.

**Command syntax:**
```
leobloom report projection --account <code> --to <yyyy-MM-dd> [--json]
```

**Implementation:**

1. Add `ProjectionArgs` DU to `ReportCommands.fs`:
   ```fsharp
   type ProjectionArgs =
       | [<Mandatory>] Account of string
       | [<Mandatory>] To of string
       | Json
   ```

2. Add `Projection` case to `ReportArgs` DU.

3. Add `handleProjection` function:
   - Parse `--to` as DateOnly
   - Call `BalanceProjectionService.project accountCode toDate`
   - Format output

4. **Human-readable output format:**
   ```
   Balance Projection: Account 1110 (Operating Account)
   Current balance as of 2026-04-07: $12,500.00
   Projection through: 2026-05-07

   Date        Balance       Change    Items
   ─────────── ───────────── ───────── ─────────────────────────────
   2026-04-07  $12,500.00              (current)
   2026-04-15  $11,200.00   -$1,300.00 Mortgage Payment Apr 2026
   2026-04-20  $11,200.00   [unknown]  ⚠ Insurance (unknown amount)
   2026-05-01  $13,700.00   +$2,500.00 Rent Receivable May 2026
   ...
   ```

   For days with multiple items, show each on its own line indented under
   the date. The `[unknown]` flag marks null-amount obligations.

5. **JSON output:** Serialize the `BalanceProjection` record directly.

**Files:**
- Modify `Src/LeoBloom.CLI/ReportCommands.fs`
- Modify `Src/LeoBloom.CLI/OutputFormatter.fs` (add projection formatter)

**Verification:** `leobloom report projection --account 1110 --to 2026-05-07`
produces output. `--json` produces valid JSON. Past date is rejected.

---

### Phase 5: Tests

**What:** Unit/integration tests following the existing xUnit + TestCleanup
pattern.

**Test cases:**

1. **FT-BP-001: Basic projection with known obligations**
   - Setup: account, receivable agreement → dest_account, payable agreement →
     source_account, spawn instances for next 30 days.
   - Assert: projection contains correct daily balances reflecting inflows
     and outflows.

2. **FT-BP-002: Null-amount obligation surfaces as unknown**
   - Setup: agreement with `amount = None`, spawn instance.
   - Assert: projection day has `hasUnknownAmounts = true`, item amount is
     None, closing balance does NOT include the unknown amount.

3. **FT-BP-003: Same-day obligations are summed with itemized breakdown**
   - Setup: two obligations hitting the same day.
   - Assert: single day entry with two items, net change is sum.

4. **FT-BP-004: Past projection date is rejected**
   - Call with date = yesterday.
   - Assert: `Error` with appropriate message.

5. **FT-BP-005: No future obligations → flat line**
   - Setup: account with no obligations or transfers.
   - Assert: all days have same closing balance = current balance.

6. **FT-BP-006: In-flight transfers affect projection**
   - Setup: initiated transfer from account.
   - Assert: projection shows outflow on expected settlement date.

7. **FT-BP-007: In-flight transfer inbound shows as inflow**
   - Setup: initiated transfer to account.
   - Assert: projection shows inflow.

**Files:**
- Create `Src/LeoBloom.Tests/BalanceProjectionTests.fs`
- Add to `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` compile list

**Verification:** `dotnet test --filter "FullyQualifiedName~BalanceProjection"` — all green.

---

## Acceptance Criteria

- [ ] `leobloom report projection --account <code> --to <date>` returns a daily balance series
- [ ] Current balance anchors the projection (delegates to AccountBalanceService)
- [ ] Receivable obligation instances for the account show as inflows
- [ ] Payable obligation instances for the account show as outflows
- [ ] In-flight transfers (initiated, not confirmed) show as inflows/outflows
- [ ] Null-amount obligations appear as "unknown" with `hasUnknownAmounts` flag — not omitted, not guessed
- [ ] Multiple same-day obligations are summed with itemized breakdown
- [ ] Projection date in the past is rejected with a clear error
- [ ] Account with no future activity produces a flat line at current balance
- [ ] JSON output (`--json`) serializes the full `BalanceProjection` record
- [ ] No new database tables or stored state — pure computation
- [ ] All 7 test cases pass

## Risks

| Risk | Mitigation |
|------|-----------|
| `obligation_type` table uses int FK, not string — need to resolve "receivable"/"payable" to IDs in SQL | Use subquery `(SELECT id FROM ops.obligation_type WHERE name = 'receivable')` in the repo query, or resolve once at service level |
| Large date ranges (e.g., 5 years) produce huge daily series | Accept it for now. The data is small per-day. Optimization (weekly/monthly rollup) is out of scope. |
| Transfer projection date ambiguity — `expected_settlement` may be null | Fall back to `initiated_date`. Document this in the output. |
| Today's boundary — should today's expected obligations be included? | Yes. Expected/in_flight obligations for today haven't hit the ledger balance yet (only "posted" entries affect journal-based balance). Include today in the projection range. |

## Out of Scope

- Persisting projections or caching results
- Weekly/monthly rollup modes (daily only)
- Recurring obligation forecasting beyond spawned instances (only projects from existing `obligation_instance` rows)
- Historical balance queries (use `account balance` command for that)
- Multi-account aggregated projections
