Feature: Dal to Utilities rename

  LeoBloom.Dal is renamed to LeoBloom.Utilities. The directory, namespace,
  project references, and solution file all reflect the new name. No trace
  of the old name remains. This is a mechanical refactor with zero behavioral
  change.

  # --- Old name eradication ---

  @FT-DUR-001
  Scenario: No LeoBloom.Dal directory exists under Src
    Given the LeoBloom source tree exists
    When I look for a directory named LeoBloom.Dal under Src
    Then no such directory exists

  @FT-DUR-002
  Scenario: No namespace LeoBloom.Dal in any source file
    Given the LeoBloom source tree exists
    When I search all .fs files for the string "LeoBloom.Dal"
    Then zero matches are found

  @FT-DUR-003
  Scenario: No project reference to LeoBloom.Dal in any fsproj
    Given the LeoBloom source tree exists
    When I search all .fsproj files for the string "LeoBloom.Dal"
    Then zero matches are found

  @FT-DUR-004
  Scenario: Solution file does not reference LeoBloom.Dal
    Given the file LeoBloom.sln exists
    When I search the solution file for the string "LeoBloom.Dal"
    Then zero matches are found

  # --- New project exists and builds ---

  @FT-DUR-005
  Scenario: LeoBloom.Utilities directory exists with all original Dal files
    Given the LeoBloom source tree exists
    When I inspect the Src/LeoBloom.Utilities directory
    Then it contains DataSource.fs
    And it contains JournalEntryRepository.fs
    And it contains JournalEntryService.fs
    And it contains AccountBalanceRepository.fs
    And it contains AccountBalanceService.fs

  @FT-DUR-006
  Scenario: LeoBloom.Utilities.fsproj builds successfully
    Given the LeoBloom source tree exists
    When I run dotnet build on LeoBloom.Utilities
    Then the build succeeds with exit code 0

  # --- Solution integrity ---

  @FT-DUR-007
  Scenario: Full solution builds with zero rename-related warnings
    Given the LeoBloom solution exists
    When I run dotnet build on the solution
    Then the build succeeds with exit code 0
    And the output contains no warnings referencing "Dal"

  @FT-DUR-008
  Scenario: All tests pass after rename
    Given the LeoBloom solution exists
    When I run dotnet test on the solution
    Then all tests pass with exit code 0
