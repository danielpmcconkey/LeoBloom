Feature: Consolidated helper functions

  After refactoring, duplicated helpers (optParam, repoRoot, constraint
  assert helpers) each exist in exactly one shared location. All existing
  behaviors that depend on those helpers continue to work identically.

  # Scope: The behavioral surface of the consolidated helpers. Structural
  # checks (single-definition-site, file placement, namespace, fsproj order)
  # are the QE's responsibility as unit-level assertions, not Gherkin scenarios.

  # --- optParam: type-widened consolidation ---

  # The critical behavioral risk: JournalEntryRepository previously used a
  # string option version of optParam. The consolidated version takes obj option,
  # so string option call sites must go through Option.map box. These scenarios
  # verify that nullable parameters still round-trip correctly through the
  # widened helper.

  @FT-CHL-001
  Scenario: Journal entry with null source persists correctly via consolidated optParam
    Given the ledger schema exists for posting
    And a valid open fiscal period from 2026-03-01 to 2026-03-31
    And an active account 1010 of type asset
    And an active account 4010 of type revenue
    When I post a journal entry dated 2026-03-15 described as "Null source test" with no source and lines:
        | account | amount  | entry_type |
        | 1010    | 1000.00 | debit      |
        | 4010    | 1000.00 | credit     |
    Then the post succeeds with null source

  @FT-CHL-002
  Scenario: Journal entry with non-null source persists correctly via consolidated optParam
    Given the ledger schema exists for posting
    And a valid open fiscal period from 2026-03-01 to 2026-03-31
    And an active account 1010 of type asset
    And an active account 4010 of type revenue
    When I post a journal entry dated 2026-03-15 described as "Source test" with source "manual" and lines:
        | account | amount  | entry_type |
        | 1010    | 1000.00 | debit      |
        | 4010    | 1000.00 | credit     |
    Then the post succeeds
    And the persisted journal entry source is "manual"

  @FT-CHL-003
  Scenario: Journal entry with null memo on lines persists correctly via consolidated optParam
    Given the ledger schema exists for posting
    And a valid open fiscal period from 2026-03-01 to 2026-03-31
    And an active account 1010 of type asset
    And an active account 4010 of type revenue
    When I post a journal entry dated 2026-03-15 described as "Null memo test" with source "manual" and lines:
        | account | amount  | entry_type |
        | 1010    | 1000.00 | debit      |
        | 4010    | 1000.00 | credit     |
    Then the post succeeds with 2 lines and valid id and timestamps

  @FT-CHL-004
  Scenario: Journal entry with non-null memo on lines persists correctly via consolidated optParam
    Given the ledger schema exists for posting
    And a valid open fiscal period from 2026-03-01 to 2026-03-31
    And an active account 1010 of type asset
    And an active account 4010 of type revenue
    When I post a journal entry dated 2026-03-15 described as "Memo test" with source "manual" and lines with memo:
        | account | amount  | entry_type | memo          |
        | 1010    | 1000.00 | debit      | Cash received |
        | 4010    | 1000.00 | credit     | Rent income   |
    Then the post succeeds with memo values on all lines

  # --- repoRoot: CallerFilePath-based resolution ---

  # The consolidated repoRoot uses [<CallerFilePath>] to walk up from the
  # source file to find LeoBloom.sln. This replaces the fragile
  # AppContext.BaseDirectory walkup. The behavioral guarantee: test-time
  # path resolution still finds the repo root correctly.

  @FT-CHL-005
  Scenario: Repo root resolves correctly from test files
    Given a test file that calls RepoPath.repoRoot()
    When the test executes
    Then the returned path contains the LeoBloom.sln file

  @FT-CHL-006
  Scenario: Src directory resolves correctly from test files
    Given a test file that calls RepoPath.srcDir()
    When the test executes
    Then the returned path ends with "Src"
    And the returned path is a valid directory

  # --- Constraint assert helpers: shared from both test contexts ---

  # The consolidated constraint helpers must work from both Ledger and Ops
  # test contexts. Rather than re-specifying every constraint scenario
  # (already covered by FT-LSC-* and FT-OSC-*), we verify the shared
  # helpers correctly detect each SQL error state.

  @FT-CHL-007
  Scenario Outline: Shared constraint helper detects <violation_type> violations
    Given the <schema> schema exists
    When I execute an insert that violates a <violation_type> constraint
    Then the shared <assertion_helper> helper correctly identifies the violation

    Examples:
      | schema | violation_type | assertion_helper |
      | ledger | NOT NULL       | assertNotNull    |
      | ledger | UNIQUE         | assertUnique     |
      | ledger | FK             | assertFk         |
      | ops    | NOT NULL       | assertNotNull    |
      | ops    | UNIQUE         | assertUnique     |
      | ops    | FK             | assertFk         |

  @FT-CHL-008
  Scenario: Shared assertSuccess helper confirms clean inserts
    Given the ops schema exists
    When I execute a valid insert
    Then the shared assertSuccess helper confirms no exception was raised
