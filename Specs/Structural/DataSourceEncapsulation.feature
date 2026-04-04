Feature: DataSource module encapsulation

  DataSource exposes exactly one public binding: openConnection.
  The connectionString escape hatch has been removed. Migrations
  builds its own connection string from its own appsettings and
  has no project reference to LeoBloom.Utilities.

  # Scope: Module API surface, project references, and build integrity.
  # These are code-level structural constraints, not database constraints.

  # --- Public API surface ---

  @FT-DSI-001
  Scenario: DataSource does not expose a connectionString binding
    Given the file Src/LeoBloom.Utilities/DataSource.fs exists
    When I inspect the module for public bindings
    Then the only public binding is openConnection

  @FT-DSI-002
  Scenario: No code outside Migrations references DataSource.connectionString
    Given the LeoBloom source tree exists
    When I search for references to DataSource.connectionString outside LeoBloom.Migrations
    Then zero references are found

  # --- Project decoupling ---

  @FT-DSI-003
  Scenario: Migrations has no project reference to LeoBloom.Utilities
    Given the file Src/LeoBloom.Migrations/LeoBloom.Migrations.fsproj exists
    When I inspect the project file for ProjectReference elements
    Then no ProjectReference points to LeoBloom.Utilities

  # --- Migrations self-sufficiency ---

  @FT-DSI-004
  Scenario: Migrations builds its own connection string from its own appsettings
    Given the file Src/LeoBloom.Migrations/Program.fs exists
    When I inspect Program.fs for connection string construction
    Then it reads from its own appsettings configuration
    And it does not reference any DataSource binding

  @FT-DSI-005
  Scenario: Migrations opens its own NpgsqlConnection for schema bootstrap
    Given the file Src/LeoBloom.Migrations/Program.fs exists
    When I inspect Program.fs for the schema bootstrap connection
    Then it creates an NpgsqlConnection from the locally-built connection string
    And it does not call DataSource.openConnection

  # --- Build integrity ---

  @FT-DSI-006
  Scenario: Full solution builds successfully
    Given the LeoBloom solution exists
    When I run dotnet build on the solution
    Then the build succeeds with exit code 0

  @FT-DSI-007
  Scenario: All existing tests pass
    Given the LeoBloom solution exists
    When I run dotnet test on the solution
    Then all tests pass with exit code 0

  # --- Runtime verification ---

  @FT-DSI-008
  Scenario: Migrations runs successfully against leobloom_dev
    Given the leobloom_dev database is accessible
    And LEOBLOOM_ENV and LEOBLOOM_DB_PASSWORD are set
    When I run the Migrations project
    Then the migration completes with exit code 0
    And the migrondi schema exists in leobloom_dev
