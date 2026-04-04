# TickSpec Step Definition Patterns — LeoBloom

## The Problem

TickSpec scans the **entire assembly** for step definitions. If two step
functions (in different modules) have regexes that match the same Gherkin line,
TickSpec throws `Ambiguous step definition` at runtime. Unlike SpecFlow, it does
NOT dispatch by context parameter type.

This means every `[<Given>]`, `[<When>]`, `[<Then>]` regex must be globally
unique across all `*StepDefinitions.fs` files in the test project.

## Rules

### 1. Prefix shared setup steps with a feature tag

Every feature has setup steps (create fiscal period, create account, etc.) that
look identical across modules. Prefix them:

```
BAD:  a valid open fiscal period from (.+) to (.+)
GOOD: a balance-test open fiscal period from (.+) to (.+)
GOOD: a void-test open fiscal period from (.+) to (.+)
```

### 2. Never end a step regex with greedy (.+) if a longer variant exists

TickSpec does not do "longest match wins." A step regex ending in `(.+)` will
match the full line even when a longer, more specific regex also matches.

```
BAD:  the balance is (.+)                           ← matches everything
BAD:  the balance is (.+) for a normal-debit account ← also matches, ambiguous

GOOD: the balance result is exactly (.+)             ← unique prefix
GOOD: the balance is (.+) for a normal-debit account ← no shorter competitor
```

### 3. Run the ambiguity checker

```bash
Scripts/check-tickspec-ambiguity.sh
```

Checks for exact duplicates and prefix overlaps. Exit 0 = clean, exit 1 = fix
needed. This also runs automatically via Claude Code hook on Write/Edit of any
`*StepDefinitions.fs` file.

### 4. One context type per feature module

Each feature gets its own context record (`PostContext`, `VoidContext`,
`BalanceContext`). TickSpec caches one value per type — this keeps scenarios
isolated. Don't try to share a context type across features.

### 5. One compound Then per scenario

TickSpec disposes connections in `finally` blocks. Multiple Then steps per
scenario cause the first Then's cleanup to dispose the connection before the
second Then runs. Use one Then that asserts everything.

## How TickSpec Step Resolution Works

1. Gherkin line comes in (e.g., `And a valid open fiscal period from 2026-03-01 to 2026-03-31`)
2. TickSpec regex-matches against ALL `[<Given>]` functions in the assembly
3. If exactly one matches → use it
4. If zero match → `Missing step definition` error
5. If 2+ match → `Ambiguous step definition` error (even if they take different context types)

## References

- TickSpec GitHub: https://github.com/fsprojects/TickSpec
- Research transcript: `.transcripts/2026-04-03T07-43-EDT_6d8d6ac2.md` (lines 3734-3856)
- BDD-style Testing in F# with TickSpec: https://dev.to/deyanp/bdd-like-testing-in-f-with-xunit-gherkin-gherkinprovider-and-tickspec-11d9
