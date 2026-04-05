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

  # --- Runtime verification ---

  @FT-DSI-008
  Scenario: Migrations runs successfully against leobloom_dev
    Given the leobloom_dev database is accessible
    And LEOBLOOM_ENV and LEOBLOOM_DB_PASSWORD are set
    When I run the Migrations project
    Then the migration completes with exit code 0
    And the migrondi schema exists in leobloom_dev
