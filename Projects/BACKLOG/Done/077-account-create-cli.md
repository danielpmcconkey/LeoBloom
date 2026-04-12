# 077 — Account Create CLI Command

**Epic:** CLI
**Depends On:** None
**Status:** Not started
**Priority:** High

---

## Problem Statement

There is no CLI command to create a new account. The `AccountService.createAccount`
method exists but is not wired to the CLI. All existing accounts were created via
SQL seed scripts (migration 006, dev seeds). With the upcoming COA expansion (~17
new accounts for personal assets, liabilities, and FI accounts), we need a proper
CLI command so account creation goes through domain validation.

The existing `CreateAccountCommand` is also missing the `account_subtype` field —
every new account needs a subtype (Cash, Investment, CurrentLiability, etc.) and
currently there's no way to set it at creation time without a direct SQL UPDATE.

## What Ships

A `leobloom account create` CLI command that creates a leaf or header account
with all required fields in a single call.

## Acceptance Criteria

1. `leobloom account create --code <string> --name <string> --type <int> --parent <int> [--subtype <string>]`
2. `--code` and `--name` are mandatory. `--type` (account_type_id) is mandatory.
3. `--parent` is optional (omit for top-level header accounts).
4. `--subtype` is optional. When provided, validated against account type using
   existing `AccountSubType.isValidSubType` logic.
5. `CreateAccountCommand` updated to include `subType: AccountSubType option`.
6. `AccountService.createAccount` updated to persist subtype.
7. `AccountRepository.create` updated to include `account_subtype` in INSERT.
8. Duplicate code returns a clear error (existing 23505 handling).
9. Output shows created account details (id, code, name, type, subtype, parent).
10. Tests cover: valid creation with subtype, creation without subtype, invalid
    subtype for type, duplicate code rejection, missing parent rejection.
