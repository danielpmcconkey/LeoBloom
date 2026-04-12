# 066 — Transfer Atomicity and Closed-Period Tests

**Epic:** Audit Remediation (2026-04-07)
**Depends On:** None
**Status:** Not started
**Priority:** Medium
**Source:** Omission Hunter GAP-019/021

---

## Problem Statement

PostObligationToLedger has two test scenarios that the transfer domain lacks
parity on:

1. **FT-POL-017 (atomicity):** If posting fails (e.g., closed period), the
   instance stays in confirmed status with no journal entry created.
2. **FT-POL-013 (closed period):** Posting to a closed fiscal period is
   rejected.

Transfers have the equivalent FT-TRF-014 (no fiscal period) but NOT the
closed-period or atomicity variants.

## What Ships

Two new scenarios in `Specs/Behavioral/Transfers.feature`:

1. **Atomicity of failed transfer confirmation:** Initiate a transfer. Close
   the fiscal period. Attempt confirmation. Assert failure. Assert the
   transfer remains in `initiated` status with no `journal_entry_id`.
2. **Closed fiscal period rejection:** Initiate a transfer with a date
   covered by a closed period. Attempt confirmation. Assert rejection with
   appropriate error message.

## Acceptance Criteria

- AC-1: A scenario confirms that a failed transfer confirmation leaves the
  transfer in `initiated` status with no journal entry created
- AC-2: A scenario confirms that confirming a transfer against a closed
  fiscal period is rejected
