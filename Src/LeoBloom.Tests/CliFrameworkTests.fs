module LeoBloom.Tests.CliFrameworkTests

open System
open System.Diagnostics
open System.IO
open Xunit

// =====================================================================
// CliRunner — spawns the CLI process and captures stdout, stderr, exit code
// =====================================================================

module CliRunner =
    open System.Text.RegularExpressions

    type CliResult =
        { ExitCode: int
          Stdout: string
          Stderr: string }

    let private cliDll =
        Path.Combine(TestHelpers.RepoPath.repoRoot, "Src", "LeoBloom.CLI", "bin", "Debug", "net10.0", "LeoBloom.CLI.dll")

    /// Serilog Console sink writes INFO/ERR lines to stdout.
    /// Strip them so we can parse actual CLI output cleanly.
    /// Pattern: lines starting with "[HH:MM:SS " (Serilog compact format)
    let private serilogLinePattern = Regex(@"^\[\d{2}:\d{2}:\d{2}\s", RegexOptions.Compiled)

    let stripLogLines (raw: string) : string =
        raw.Split('\n')
        |> Array.filter (fun line -> not (serilogLinePattern.IsMatch(line)))
        |> String.concat "\n"
        |> fun s -> s.Trim()

    /// Run the CLI with the given arguments string (space-separated).
    let run (argsString: string) : CliResult =
        let psi = ProcessStartInfo()
        psi.FileName <- "dotnet"
        psi.Arguments <- sprintf "%s %s" cliDll argsString
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true
        // Set DOTNET_ENVIRONMENT so appsettings.Development.json is loaded
        psi.Environment.["DOTNET_ENVIRONMENT"] <- "Development"
        // Working directory should be the CLI output dir for config resolution
        psi.WorkingDirectory <- Path.GetDirectoryName(cliDll)

        use proc = Process.Start(psi)
        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit(30000) |> ignore // 30s timeout

        { ExitCode = proc.ExitCode
          Stdout = stdout
          Stderr = stderr }

    /// Run with no arguments
    let runNoArgs () : CliResult =
        run ""

// =====================================================================
// CLI Framework Tests — CliFramework.feature
// =====================================================================

// --- Help Output ---

[<Fact>]
[<Trait("GherkinId", "FT-CLF-001")>]
let ``top-level --help prints usage with available command groups`` () =
    let result = CliRunner.run "--help"
    Assert.Equal(0, result.ExitCode)
    Assert.Contains("ledger", result.Stdout, StringComparison.OrdinalIgnoreCase)

[<Fact>]
[<Trait("GherkinId", "FT-CLF-002")>]
let ``ledger --help prints available subcommands`` () =
    let result = CliRunner.run "ledger --help"
    Assert.Equal(0, result.ExitCode)
    Assert.Contains("post", result.Stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("void", result.Stdout, StringComparison.OrdinalIgnoreCase)
    Assert.Contains("show", result.Stdout, StringComparison.OrdinalIgnoreCase)

// --- Unknown / Invalid Commands ---

[<Fact>]
[<Trait("GherkinId", "FT-CLF-003")>]
let ``unknown top-level command prints error to stderr`` () =
    let result = CliRunner.run "garbage"
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "FT-CLF-004")>]
let ``no arguments prints usage or error to stderr`` () =
    let result = CliRunner.runNoArgs()
    Assert.Equal(2, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// --- Exit Code Mapping (Scenario Outline FT-CLF-005) ---

[<Fact>]
[<Trait("GherkinId", "FT-CLF-005a")>]
let ``exit code 0 for --help`` () =
    let result = CliRunner.run "--help"
    Assert.Equal(0, result.ExitCode)

[<Fact>]
[<Trait("GherkinId", "FT-CLF-005b")>]
let ``exit code 2 for unknown command`` () =
    let result = CliRunner.run "garbage"
    Assert.Equal(2, result.ExitCode)

[<Fact>]
[<Trait("GherkinId", "FT-CLF-005c")>]
let ``exit code 1 for business error (show nonexistent entry)`` () =
    let result = CliRunner.run "ledger show 999999"
    Assert.Equal(1, result.ExitCode)
