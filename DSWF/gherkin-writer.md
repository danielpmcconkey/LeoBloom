# Gherkin Writer Addendum — LeoBloom

Read `DSWF/conventions.md` first — it defines naming conventions and artifact
inventory for all agents.

## Spec Location

`Specs/Behavioral/{FeatureName}.feature`

## Tag Convention

`@FT-{CATEGORY}-{NNN}` — check existing files in `Specs/Behavioral/` for
established category prefixes before creating a new one.

## Existing Categories

Scan the `Specs/` directory to discover current categories. Common ones:
- `@FT-PJE` — Post Journal Entry
- `@FT-VJE` — Void Journal Entry
- `@FT-AB` — Account Balance

## Domain Guidance

When writing scenarios for financial behaviors:
- Use account names and types, not just codes
- Revenue/expense scenarios should respect normal balance conventions
- Zero-balance-with-activity is a valid and important state, not an edge case
