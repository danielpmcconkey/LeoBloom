Feature: Log module structure

  The Log module in LeoBloom.Utilities provides a thin wrapper over Serilog.
  It exposes exactly the intended API surface, excludes Debug level by design,
  and the project declares the required Serilog package references. Console
  output calls are eliminated from all Src projects except Migrations.

  # --- Package references ---

  @FT-LMS-001
  Scenario: Serilog core package is referenced
    Given the file Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj exists
    When I inspect the project file for PackageReference elements
    Then a PackageReference to Serilog exists

  @FT-LMS-002
  Scenario: Serilog.Sinks.Console package is referenced
    Given the file Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj exists
    When I inspect the project file for PackageReference elements
    Then a PackageReference to Serilog.Sinks.Console exists

  @FT-LMS-003
  Scenario: Serilog.Sinks.File package is referenced
    Given the file Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj exists
    When I inspect the project file for PackageReference elements
    Then a PackageReference to Serilog.Sinks.File exists

  @FT-LMS-004
  Scenario: Serilog.Settings.Configuration package is referenced
    Given the file Src/LeoBloom.Utilities/LeoBloom.Utilities.fsproj exists
    When I inspect the project file for PackageReference elements
    Then a PackageReference to Serilog.Settings.Configuration exists

  # --- Log module API surface ---

  @FT-LMS-005
  Scenario: Log.fs exists in LeoBloom.Utilities
    Given the LeoBloom source tree exists
    When I look for Log.fs in Src/LeoBloom.Utilities
    Then the file exists

  @FT-LMS-006
  Scenario: Log module exposes the required functions
    Given the file Src/LeoBloom.Utilities/Log.fs exists
    When I inspect the Log module for public function bindings
    Then it contains an initialize function
    And it contains a closeAndFlush function
    And it contains an info function
    And it contains a warn function
    And it contains an error function
    And it contains a fatal function
    And it contains an errorExn function

  @FT-LMS-007
  Scenario: Log module does not expose a debug function
    Given the file Src/LeoBloom.Utilities/Log.fs exists
    When I search Log.fs for any binding named debug or Debug
    Then zero matches are found

  # --- Console output hygiene ---

  @FT-LMS-008
  Scenario: TestHelpers uses Log.errorExn instead of eprintfn
    Given the file Src/LeoBloom.Tests/TestHelpers.fs exists
    When I search TestHelpers.fs for eprintfn
    Then zero matches are found

  @FT-LMS-009
  Scenario: No printfn or eprintfn in any Src project except Migrations
    Given the LeoBloom source tree exists
    When I search all .fs files under Src for printfn or eprintfn excluding LeoBloom.Migrations
    Then zero matches are found

  # --- Migrations isolation ---

  @FT-LMS-010
  Scenario: Migrations has no reference to LeoBloom.Utilities
    Given the file Src/LeoBloom.Migrations/LeoBloom.Migrations.fsproj exists
    When I inspect the project file for ProjectReference elements
    Then no ProjectReference points to LeoBloom.Utilities

  @FT-LMS-011
  Scenario: Migrations source files have no changes from this project
    Given the LeoBloom source tree exists
    When I compare LeoBloom.Migrations files to their state before Project 031
    Then zero files have been modified
