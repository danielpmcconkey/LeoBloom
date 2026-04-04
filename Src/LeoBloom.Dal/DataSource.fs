namespace LeoBloom.Dal

open System
open Microsoft.Extensions.Configuration
open Npgsql

/// Centralized NpgsqlDataSource with eager initialization.
/// One public function: openConnection() — returns a pooled, already-open connection.
/// Fails loud at module load if config is missing, env is wrong, or DB is unreachable.
module DataSource =

    let private connStr =
        let env =
            Environment.GetEnvironmentVariable "LEOBLOOM_ENV"
            |> Option.ofObj
            |> Option.defaultValue "Development"

        let config =
            ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile($"appsettings.{env}.json", optional = false)
                .AddEnvironmentVariables()
                .Build()

        let template = config["ConnectionStrings:LeoBloom"]

        if String.IsNullOrWhiteSpace template then
            failwith $"ConnectionStrings:LeoBloom not found in appsettings.{env}.json"

        let password =
            Environment.GetEnvironmentVariable "LEOBLOOM_DB_PASSWORD"
            |> Option.ofObj
            |> Option.defaultValue ""

        template.Replace("{LEOBLOOM_DB_PASSWORD}", password)

    let private dataSource =
        let builder = NpgsqlDataSourceBuilder(connStr)
        builder.ConnectionStringBuilder.IncludeErrorDetail <- true
        builder.ConnectionStringBuilder.ApplicationName <- "LeoBloom"
        builder.ConnectionStringBuilder.Timeout <- 5
        builder.ConnectionStringBuilder.CommandTimeout <- 5
        let ds = builder.Build()

#if DEBUG
        // Debug-only safety guard: verify we're talking to leobloom_dev.
        // Compiled out of release builds entirely.
        use guardConn = ds.OpenConnection()
        use cmd = guardConn.CreateCommand()
        cmd.CommandText <- "SELECT current_database()"
        let dbName = cmd.ExecuteScalar() :?> string
        if dbName <> "leobloom_dev" then
            failwith $"SAFETY GUARD: Expected database 'leobloom_dev' but connected to '{dbName}'. Aborting."
#endif

        ds

    /// The resolved connection string. Exposed for consumers that need the raw
    /// string (e.g., Migrondi config). Prefer openConnection() for all other use.
    let connectionString = connStr

    /// Returns a pooled, already-open NpgsqlConnection.
    let openConnection () : NpgsqlConnection =
        dataSource.OpenConnection()
