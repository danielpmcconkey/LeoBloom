# P076 — Account Update CLI Command — PO Kickoff

**Card ID:** 24
**Date:** 2026-04-08
**Status:** Kicked off — ready for planning

## Scope

CLI command `leobloom account update <id>` to modify mutable account fields:
- `--name <string>` — rename an account
- `--subtype <string>` — change or assign account subtype
- `--external-ref <string>` — set external reference (P075 column, already landed)

Immutable fields (`code`, `account_type_id`, `parent_id`) are **not exposed** as flags.
At least one mutable flag must be provided (no no-op updates).

## Key Acceptance Criteria (from backlog card)

1. CLI signature: `leobloom account update <id> [--name] [--subtype] [--external-ref]`
2. At least one flag required — reject no-op invocations
3. Subtype validated against account type via existing `isValidSubType`
4. Immutable fields not exposed (preferred) — no flags for code, type, parent
5. Confirmation output shows before/after for changed fields
6. Tests: valid update, invalid subtype, no-flag rejection, immutable-field non-exposure

## Dependencies

- **P075 (external_ref column):** Landed — commit `faaedb3` on main
- **P077 (account create CLI):** Landed — commit `2f3e448` on main. Establishes
  CLI patterns (Argu DU, handler structure, OutputFormatter usage) that P076 should follow

## Notes for Planner

- Follow P077's CLI pattern: Argu DU for args, handler function, dispatch case in AccountArgs
- Service layer needs an `updateAccount` function (doesn't exist yet)
- Repository needs an `update` function for account (doesn't exist yet)
- The `AccountSubType.isValidSubType` call requires account type *name*, not ID —
  same lookup issue P077 solved; reuse that approach
- Before/after output means the handler must fetch the account before applying changes
- Consider whether `is_active` should be included (card says "could be unified" but
  separate deactivation command may already exist — check before planning)
