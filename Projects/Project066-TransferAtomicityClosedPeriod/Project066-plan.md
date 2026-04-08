# Project 066 — Plan

## Objective

Add two Gherkin scenarios and corresponding xUnit tests to cover transfer
confirmation against closed fiscal periods — achieving parity with
FT-POL-013 (closed period rejection) and FT-POL-017 (atomicity of failed
posting) in the PostObligationToLedger domain.

## Phases

### Phase 1: Gherkin Scenarios

**What:** Append two scenarios to `Specs/Behavioral/Transfers.feature` after
FT-TRF-016, under a new section header `# --- Confirm: Closed Period ---`.

**Files modified:** `Specs/Behavioral/Transfers.feature`

**Scenario FT-TRF-017 — Atomicity of failed transfer confirmation:**

```gherkin
@FT-TRF-017
Scenario: Failed confirmation against a closed fiscal period leaves transfer in initiated status with no journal entry
    Given two active asset accounts "checking" and "savings"
    And an open fiscal period covering 2026-04-15
    And an initiated transfer of 1000.00 from "checking" to "savings"
    And the fiscal period covering 2026-04-15 is now closed
    When I confirm the transfer on 2026-04-15
    Then the confirm fails with error containing "not open"
    And the transfer status is "initiated"
    And the transfer journal_entry_id is null
    And no journal entry was created for this transfer
```

**Scenario FT-TRF-018 — Closed fiscal period rejection:**

```gherkin
@FT-TRF-018
Scenario: Confirming a transfer against a closed fiscal period is rejected
    Given two active asset accounts "checking" and "savings"
    And a closed fiscal period covering 2026-04-15
    And an initiated transfer of 500.00 from "checking" to "savings"
    When I confirm the transfer on 2026-04-15
    Then the confirm fails with error containing "not open"
```

**Design notes:**
- FT-TRF-017 mirrors FT-POL-017: create an open period, initiate the
  transfer, close the period, then attempt confirmation. Asserts atomicity —
  transfer stays `initiated`, no `journal_entry_id`, no journal entry created.
- FT-TRF-018 mirrors FT-POL-013: period is closed from the start. Asserts
  the rejection error message.
- Error string `"not open"` matches the existing posting engine message
  (`"Fiscal period '%d' is not open"`), consistent with FT-POL-013 and
  FT-CFP-011.

**Verification:** Feature file parses correctly; scenario count is 18.

### Phase 2: xUnit Tests

**What:** Add two test functions to `Src/LeoBloom.Tests/TransferTests.fs`
after the FT-TRF-016 test, with corresponding section headers.

**Files modified:** `Src/LeoBloom.Tests/TransferTests.fs`

**Test FT-TRF-017 implementation:**

1. Open connection + begin transaction (no commit — standard isolation).
2. `setupTwoAssetAccounts` with unique prefix.
3. `InsertHelpers.insertFiscalPeriod` — open period covering `2092-04-01` to
   `2092-04-30`, `is_open = true`. (Year 2092 per existing TransferTests
   reservation.)
4. `initiateTransfer` at `DateOnly(2092, 1, 1)` (uses default initiate date).
5. Close the period: raw SQL
   `UPDATE ledger.fiscal_period SET is_open = false WHERE id = @id`.
6. Call `TransferService.confirm` with `confirmedDate = DateOnly(2092, 4, 15)`.
7. Assert `Error` result containing `"not open"`.
8. `queryTransfer` — assert `status = "initiated"`, `journalEntryId = None`.
9. Count journal entries with reference `("transfer", transferId)` — assert 0.

**Test FT-TRF-018 implementation:**

1. Open connection + begin transaction.
2. `setupTwoAssetAccounts` with unique prefix.
3. `InsertHelpers.insertFiscalPeriod` — closed period covering `2092-04-01` to
   `2092-04-30`, **`is_open = false`**.
4. `initiateTransfer`.
5. Call `TransferService.confirm` with `confirmedDate = DateOnly(2092, 4, 15)`.
6. Assert `Error` result containing `"not open"`.

**Design notes:**
- FT-TRF-017 uses months not yet claimed by other tests in the file (all
  existing tests use months 1, 3–11 within year 2092). Month 4 is used by
  FT-TRF-008 but with a different prefix; since we use transactions (no
  commit), there's no conflict via `findByDate`. However, to be safe, use
  a fresh month range — **use `2092-02-01` to `2092-02-28`** for TRF-017
  (month 2 is unused) and **`2092-04-01` to `2092-04-30`** for TRF-018
  (month 4 already used by TRF-008, but FP IDs are distinct per transaction
  so no conflict). Actually — both tests are transactional, so month reuse
  within the same year is safe. We'll use months that read cleanly.
- No new helper functions needed — the existing `queryTransfer` helper
  already reads `journalEntryId`, and a simple inline SQL count suffices for
  journal entry count verification (same pattern used in FT-TRF-015).
- Both tests use the `[<Trait("GherkinId", ...)>]` convention.

**Verification:** `dotnet test --filter "GherkinId~FT-TRF-017|GherkinId~FT-TRF-018"` passes.

## Acceptance Criteria

- [ ] AC-1: FT-TRF-017 scenario exists in Transfers.feature and passes —
  failed confirmation leaves transfer in `initiated` status with no journal
  entry created
- [ ] AC-2: FT-TRF-018 scenario exists in Transfers.feature and passes —
  confirming against a closed fiscal period is rejected with error containing
  `"not open"`

## Risks

- **`findByDate` returning wrong period:** Mitigated by using year 2092
  (reserved for TransferTests) and running inside rolled-back transactions.
- **Error message drift:** If posting engine changes "not open" wording,
  both tests break. Low risk — phrase is stable and used across multiple
  test domains.

## Out of Scope

- Service-layer changes — the posting engine already rejects closed periods.
  These tests confirm existing behavior, not new behavior.
- CLI/command-layer tests for transfer confirmation against closed periods.
- Reopening a closed period and retrying confirmation.
