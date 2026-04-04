open System
open System.IO
open Microsoft.Extensions.Configuration
open Migrondi.Core
open Npgsql

/// Migrations builds its own connection string from its own config.
/// This is intentionally not shared with DataSource -- Migrations' DB
/// access is a private implementation detail.
let private connectionString =
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

[<EntryPoint>]
let main _args =
    let env =
        Environment.GetEnvironmentVariable "LEOBLOOM_ENV"
        |> Option.ofObj
        |> Option.defaultValue "Development"

    printfn "Leo Bloom Migrations — Environment: %s" env

    let migrationsDir =
        Path.Combine(AppContext.BaseDirectory, "Migrations")

    if not (Directory.Exists migrationsDir) then
        Directory.CreateDirectory migrationsDir |> ignore

    // Ensure the migrondi schema exists for the journal table.
    // The claude role cannot write to public schema.
    use bootstrapConn = new NpgsqlConnection(connectionString)
    bootstrapConn.Open()
    use cmd = bootstrapConn.CreateCommand()
    cmd.CommandText <- "CREATE SCHEMA IF NOT EXISTS migrondi"
    cmd.ExecuteNonQuery() |> ignore
    bootstrapConn.Close()

    let migrondiConfig = {
        MigrondiConfig.Default with
            connection = connectionString
            driver = MigrondiDriver.Postgresql
            migrations = migrationsDir
            tableName = "__migrondi_migrations"
    }

    let migrondi = Migrondi.MigrondiFactory(migrondiConfig, migrationsDir)

    try
        migrondi.Initialize()

        let statuses = migrondi.MigrationsList()

        let pending =
            statuses
            |> Seq.choose (fun s ->
                match s with
                | MigrationStatus.Pending m -> Some m
                | _ -> None)
            |> Seq.toList

        if pending.IsEmpty then
            printfn "No pending migrations."
            0
        else
            printfn "%d pending migration(s) found." pending.Length
            let applied = migrondi.RunUp()
            for m in applied do
                printfn "  Applied: %s" m.name
            printfn "All migrations applied successfully."
            0
    with ex ->
        eprintfn "ERROR: %s" ex.Message
        1
