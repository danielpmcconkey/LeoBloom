# 076 — Account Update CLI Command

**Epic:** CLI
**Depends On:** 075 (external_ref field, if built first — but not strictly required)
**Status:** Not started
**Priority:** Medium

---

## Problem Statement

There is no CLI command to update an existing account. Name changes,
subtype corrections, and (once P075 lands) external reference updates
currently require direct SQL against prod. This breaks the standing order
that all data entry goes through the CLI so the app layer can validate.

## What Should Be Mutable

| Field | Mutable? | Reason |
|---|---|---|
| `name` | Yes | Accounts get renamed (e.g., "Fidelity CMA" → "Fidelity CMA (Property)") |
| `account_subtype` | Yes | May need correction or initial assignment |
| `external_ref` | Yes | Added after account creation (depends on P075) |
| `is_active` | Yes | Deactivation is already handled by a separate command, but could be unified |
| `code` | **No** | COA code is structural. Changing it breaks journal entry history. |
| `account_type_id` | **No** | Changes normal balance. Would invalidate all existing entries. |
| `parent_id` | **No** | Changes COA hierarchy. Affects all roll-up reporting. |

## Acceptance Criteria

1. `leobloom account update <id> [--name <string>] [--subtype <string>] [--external-ref <string>]`
2. At least one flag required (no no-op updates).
3. Subtype validated against account type (existing `isValidSubType` logic).
4. Immutable fields (`code`, `account_type_id`, `parent_id`) cannot be changed
   through this command.
5. Confirmation output shows before/after values for changed fields.
6. Tests cover: valid update, invalid subtype for type, no-flag rejection,
   immutable field rejection (if flags are even exposed — prefer not exposing).
