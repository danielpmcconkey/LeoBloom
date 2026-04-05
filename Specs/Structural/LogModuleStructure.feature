Feature: Log module structure

  The Log module in LeoBloom.Utilities provides a thin wrapper over Serilog.
  It exposes exactly the intended API surface, excludes Debug level by design,
  and the project declares the required Serilog package references. Console
  output calls are eliminated from all Src projects except Migrations.

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
