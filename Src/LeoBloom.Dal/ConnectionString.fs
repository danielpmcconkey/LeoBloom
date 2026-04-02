namespace LeoBloom.Dal

open System
open System.IO
open Microsoft.Extensions.Configuration

/// Shared connection string resolver.
/// Reads LEOBLOOM_ENV, loads the matching appsettings.{env}.json,
/// substitutes LEOBLOOM_DB_PASSWORD, returns a ready-to-use connection string.
module ConnectionString =

    let resolve (basePath: string) =
        let env =
            Environment.GetEnvironmentVariable "LEOBLOOM_ENV"
            |> Option.ofObj
            |> Option.defaultValue "Development"

        let config =
            ConfigurationBuilder()
                .SetBasePath(basePath)
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
