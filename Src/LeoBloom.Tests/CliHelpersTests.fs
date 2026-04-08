module LeoBloom.Tests.CliHelpersTests

open System
open System.IO
open Xunit
open LeoBloom.Tests.TestHelpers
open LeoBloom.Tests.CliFrameworkTests

// =====================================================================
// Behavioral: TransferCommands strict date enforcement (AC-2)
// =====================================================================

// --- FT-CLH-001: Transfer initiate rejects lenient date formats ---
// Date parsing happens before any DB access, so no environment setup needed.
// Each example from the Scenario Outline gets its own [<Fact>].

[<Fact>]
[<Trait("GherkinId", "@FT-CLH-001a")>]
let ``transfer initiate rejects US locale M/d/yyyy date format`` () =
    let result = CliRunner.run "transfer initiate --from-account 1 --to-account 2 --amount 500.00 --date 4/1/2026"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-CLH-001b")>]
let ``transfer initiate rejects M-d-yyyy with dash separators`` () =
    let result = CliRunner.run "transfer initiate --from-account 1 --to-account 2 --amount 500.00 --date 4-1-2026"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-CLH-001c")>]
let ``transfer initiate rejects unpadded yyyy-M-d date format`` () =
    let result = CliRunner.run "transfer initiate --from-account 1 --to-account 2 --amount 500.00 --date 2026-4-1"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

[<Fact>]
[<Trait("GherkinId", "@FT-CLH-001d")>]
let ``transfer initiate rejects day-first DD/MM/YYYY date format`` () =
    let result = CliRunner.run "transfer initiate --from-account 1 --to-account 2 --amount 500.00 --date 01/04/2026"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// --- FT-CLH-002: Transfer confirm rejects a non-strict date format ---
// Date parsing happens before the service call, so no real initiated transfer is needed.

[<Fact>]
[<Trait("GherkinId", "@FT-CLH-002")>]
let ``transfer confirm rejects non-strict date format`` () =
    let result = CliRunner.run "transfer confirm 1 --date 4/1/2026"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// --- FT-CLH-003: Transfer list rejects non-strict date in --from filter ---

[<Fact>]
[<Trait("GherkinId", "@FT-CLH-003")>]
let ``transfer list rejects non-strict date in --from filter`` () =
    let result = CliRunner.run "transfer list --from 4/1/2026"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// --- FT-CLH-004: Transfer list rejects non-strict date in --to filter ---

[<Fact>]
[<Trait("GherkinId", "@FT-CLH-004")>]
let ``transfer list rejects non-strict date in --to filter`` () =
    let result = CliRunner.run "transfer list --to 4/1/2026"
    Assert.Equal(1, result.ExitCode)
    Assert.False(String.IsNullOrWhiteSpace(result.Stderr), "Expected error message on stderr")

// =====================================================================
// Structural: AC-1 — parseDate defined exactly once in CLI project
// =====================================================================

[<Fact>]
let ``parseDate is defined exactly once in the CLI project and lives in CliHelpers.fs`` () =
    let cliDir = Path.Combine(RepoPath.srcDir, "LeoBloom.CLI")
    let fsFiles = Directory.GetFiles(cliDir, "*.fs", SearchOption.TopDirectoryOnly)
    let definitions =
        fsFiles
        |> Array.filter (fun f ->
            let content = File.ReadAllText(f)
            // Use space suffix to avoid matching parseDateTimeOffset or other longer names
            content.Contains("let parseDate ") || content.Contains("let private parseDate "))
    Assert.Equal(1, definitions.Length)
    let defFile = definitions.[0]
    Assert.True(defFile.EndsWith("CliHelpers.fs"),
        sprintf "parseDate should be defined in CliHelpers.fs, found in: %s" defFile)

// =====================================================================
// Structural: AC-3 — parsePeriodArg defined exactly once in CLI project
// =====================================================================

[<Fact>]
let ``parsePeriodArg is defined exactly once in the CLI project and lives in CliHelpers.fs`` () =
    let cliDir = Path.Combine(RepoPath.srcDir, "LeoBloom.CLI")
    let fsFiles = Directory.GetFiles(cliDir, "*.fs", SearchOption.TopDirectoryOnly)
    let definitions =
        fsFiles
        |> Array.filter (fun f ->
            let content = File.ReadAllText(f)
            content.Contains("let parsePeriodArg") || content.Contains("let private parsePeriodArg"))
    Assert.Equal(1, definitions.Length)
    let defFile = definitions.[0]
    Assert.True(defFile.EndsWith("CliHelpers.fs"),
        sprintf "parsePeriodArg should be defined in CliHelpers.fs, found in: %s" defFile)

// =====================================================================
// Structural: CliHelpers.fs is first Compile entry in CLI .fsproj
// =====================================================================

[<Fact>]
let ``CliHelpers.fs is the first Compile entry in LeoBloom.CLI.fsproj`` () =
    let fsprojPath = Path.Combine(RepoPath.srcDir, "LeoBloom.CLI", "LeoBloom.CLI.fsproj")
    let content = File.ReadAllText(fsprojPath)
    let cliHelpersIndex = content.IndexOf("CliHelpers.fs")
    let exitCodesIndex = content.IndexOf("ExitCodes.fs")
    Assert.True(cliHelpersIndex >= 0, "CliHelpers.fs should be in the .fsproj")
    Assert.True(exitCodesIndex >= 0, "ExitCodes.fs should be in the .fsproj")
    Assert.True(cliHelpersIndex < exitCodesIndex,
        sprintf "CliHelpers.fs (at %d) should appear before ExitCodes.fs (at %d) in the .fsproj"
            cliHelpersIndex exitCodesIndex)
