open System
open System.IO
open Microsoft.Extensions.Configuration
open Migrondi.Core
open Npgsql

[<EntryPoint>]
let main args =
    let environment =
        args
        |> Array.tryHead
        |> Option.defaultValue "Development"

    printfn "Leo Bloom Migrations — Environment: %s" environment

    let config =
        ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json", optional = true)
            .AddJsonFile($"appsettings.{environment}.json", optional = true)
            .AddEnvironmentVariables()
            .Build()

    let connTemplate = config["ConnectionStrings:LeoBloom"]

    if String.IsNullOrWhiteSpace connTemplate then
        eprintfn "ERROR: ConnectionStrings:LeoBloom not found in configuration."
        1
    else

    let password =
        Environment.GetEnvironmentVariable "LEOBLOOM_DB_PASSWORD"
        |> Option.ofObj
        |> Option.defaultValue ""

    let connString = connTemplate.Replace("{LEOBLOOM_DB_PASSWORD}", password)

    let migrationsDir =
        Path.Combine(AppContext.BaseDirectory, "Migrations")

    if not (Directory.Exists migrationsDir) then
        Directory.CreateDirectory migrationsDir |> ignore

    // Ensure the migrondi schema exists for the journal table.
    // The claude role cannot write to public schema.
    use bootstrapConn = new NpgsqlConnection(connString)
    bootstrapConn.Open()
    use cmd = bootstrapConn.CreateCommand()
    cmd.CommandText <- "CREATE SCHEMA IF NOT EXISTS migrondi"
    cmd.ExecuteNonQuery() |> ignore
    bootstrapConn.Close()

    let migrondiConfig = {
        MigrondiConfig.Default with
            connection = connString
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
