namespace LeoBloom.Utilities

open System
open Serilog
open Microsoft.Extensions.Configuration

/// Thin wrapper over Serilog's static Log class.
/// Module-level functions callable from anywhere. No DI, no ILogger<T>.
/// Call Log.initialize() once at process startup before any logging.
module Log =

    let mutable private initialized = false

    /// Initialize Serilog from appsettings.{env}.json.
    /// Creates one log file per process execution. Idempotent — safe to call
    /// multiple times; only the first call configures the logger.
    let initialize () =
        if initialized then () else
        initialized <- true
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

        let timestamp = DateTime.Now.ToString("yyyyMMdd.HH.mm.ss")

        let baseDir =
            config["Logging:FileBasePath"]
            |> Option.ofObj
            |> Option.defaultValue "/workspace/application_logs/leobloom"

        let logPath = System.IO.Path.Combine(baseDir, $"leobloom-{timestamp}.log")

        Serilog.Log.Logger <-
            LoggerConfiguration()
                .ReadFrom.Configuration(config)
                .WriteTo.Console()
                .WriteTo.File(logPath)
                .CreateLogger()

        Serilog.Log.Information("LeoBloom logging initialized. File: {LogPath}", logPath)

    /// Flush and close. Call at process shutdown.
    let closeAndFlush () =
        Serilog.Log.CloseAndFlush()

    // --- Thin wrappers (skip Debug entirely per design decision) ---

    let info (messageTemplate: string) ([<ParamArray>] args: obj array) =
        Serilog.Log.Information(messageTemplate, args)

    let warn (messageTemplate: string) ([<ParamArray>] args: obj array) =
        Serilog.Log.Warning(messageTemplate, args)

    let error (messageTemplate: string) ([<ParamArray>] args: obj array) =
        Serilog.Log.Error(messageTemplate, args)

    let fatal (messageTemplate: string) ([<ParamArray>] args: obj array) =
        Serilog.Log.Fatal(messageTemplate, args)

    let errorExn (ex: exn) (messageTemplate: string) ([<ParamArray>] args: obj array) =
        Serilog.Log.Error(ex, messageTemplate, args)
