module LeoBloom.Tests.SeedRunnerTests

open System
open System.Diagnostics
open System.IO
open Xunit
open Npgsql
open LeoBloom.Utilities

// =====================================================================
// Helpers — run the seed runner shell script and capture output
// =====================================================================

let private seedsDir =
    Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "Src", "LeoBloom.Migrations", "Seeds"))

let private runnerScript = Path.Combine(seedsDir, "run-seeds.sh")

let private runSeeds (env: string) (extraEnv: (string * string) list) =
    let psi = ProcessStartInfo()
    psi.FileName <- "/usr/bin/env"
    psi.Arguments <- sprintf "bash %s %s" runnerScript env
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    psi.CreateNoWindow <- true
    // Inherit LEOBLOOM_DB_PASSWORD from the test environment
    let pw = Environment.GetEnvironmentVariable("LEOBLOOM_DB_PASSWORD")
    if not (String.IsNullOrEmpty pw) then
        psi.Environment.["LEOBLOOM_DB_PASSWORD"] <- pw
    for (k, v) in extraEnv do
        psi.Environment.[k] <- v
    let proc = Process.Start(psi)
    let stdout = proc.StandardOutput.ReadToEnd()
    let stderr = proc.StandardError.ReadToEnd()
    proc.WaitForExit()
    (proc.ExitCode, stdout, stderr)

let private runSeedsDev () = runSeeds "dev" []

/// Run seeds against a custom directory by creating a temp copy of the
/// runner that points to a custom base path.
let private runSeedsCustomDir (seedDir: string) (env: string) =
    let psi = ProcessStartInfo()
    psi.FileName <- "/usr/bin/env"
    // We pass the env arg; the script resolves SCRIPT_DIR from its own location,
    // so we copy the runner into the custom seedDir's parent.
    let parentDir = Path.GetDirectoryName(Path.Combine(seedDir, env))
    // Actually: the runner uses SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
    // So we copy the runner into the custom dir's parent.
    let tempRunner = Path.Combine(seedDir, "run-seeds.sh")
    File.Copy(runnerScript, tempRunner, true)
    psi.Arguments <- sprintf "bash %s %s" tempRunner env
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    psi.CreateNoWindow <- true
    let pw = Environment.GetEnvironmentVariable("LEOBLOOM_DB_PASSWORD")
    if not (String.IsNullOrEmpty pw) then
        psi.Environment.["LEOBLOOM_DB_PASSWORD"] <- pw
    let proc = Process.Start(psi)
    let stdout = proc.StandardOutput.ReadToEnd()
    let stderr = proc.StandardError.ReadToEnd()
    proc.WaitForExit()
    (proc.ExitCode, stdout, stderr)

// =====================================================================
// @FT-SR-001 — Seeds populate fiscal periods on a fresh database
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SR-001")>]
let ``Seeds populate fiscal periods on a fresh database`` () =
    // The dev database has migrations applied. Run seeds and verify the
    // 36 fiscal periods covering 2026-01 through 2028-12 exist.
    let (exitCode, _, stderr) = runSeedsDev ()
    Assert.True((exitCode = 0), sprintf "Seed runner failed: %s" stderr)

    use conn = DataSource.openConnection()
    use cmd = conn.CreateCommand()
    cmd.CommandText <-
        "SELECT COUNT(*) FROM ledger.fiscal_period
         WHERE period_key >= '2026-01' AND period_key <= '2028-12'"
    let count = cmd.ExecuteScalar() :?> int64
    Assert.Equal(36L, count)

    // Verify range boundaries
    cmd.CommandText <-
        "SELECT MIN(period_key), MAX(period_key) FROM ledger.fiscal_period
         WHERE period_key >= '2026-01' AND period_key <= '2028-12'"
    use reader = cmd.ExecuteReader()
    Assert.True(reader.Read())
    Assert.Equal("2026-01", reader.GetString(0))
    Assert.Equal("2028-12", reader.GetString(1))

// =====================================================================
// @FT-SR-002 — Seeds populate chart of accounts on a fresh database
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SR-002")>]
let ``Seeds populate chart of accounts on a fresh database`` () =
    let (exitCode, _, stderr) = runSeedsDev ()
    Assert.True((exitCode = 0), sprintf "Seed runner failed: %s" stderr)

    use conn = DataSource.openConnection()

    // Verify 69 accounts from seed codes
    use cmd = conn.CreateCommand()
    cmd.CommandText <-
        "SELECT COUNT(*) FROM ledger.account
         WHERE code IN (
            '1000','1100','1110','1120','1200','1210','1220',
            '2000','2100','2110','2200','2210','2220',
            '3000','3010','3020','3099',
            '4000','4100','4110','4120','4130','4140','4150','4200','4210','4220',
            '5000','5100','5110','5120','5130','5140','5150','5160','5170','5180','5190','5200','5210',
            '5300','5310','5311','5312','5313','5350','5400','5450','5500','5550','5600','5650','5700','5750','5800','5850','5900','5950',
            '6000','6050','6100','6150','6200',
            '7100','7110','7200','7210','7220',
            '9010')"
    let count = cmd.ExecuteScalar() :?> int64
    Assert.Equal(69L, count)

    // Every account with a parent_id references an existing account
    use cmd2 = conn.CreateCommand()
    cmd2.CommandText <-
        "SELECT a.code, a.parent_id FROM ledger.account a
         WHERE a.parent_id IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM ledger.account p WHERE p.id = a.parent_id)"
    use reader = cmd2.ExecuteReader()
    Assert.False(reader.HasRows, "Found accounts with parent_id referencing non-existent accounts")

// =====================================================================
// @FT-SR-003 — Seeds apply account subtypes from the chart of accounts
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SR-003")>]
let ``Seeds apply account subtypes from the chart of accounts`` () =
    let (exitCode, _, stderr) = runSeedsDev ()
    Assert.True((exitCode = 0), sprintf "Seed runner failed: %s" stderr)

    use conn = DataSource.openConnection()
    use cmd = conn.CreateCommand()
    // Leaf accounts: seeded accounts with parent_id that have no children of their own
    cmd.CommandText <-
        "SELECT a.code, a.account_subtype
         FROM ledger.account a
         WHERE a.code IN (
            '1110','1120','1210','1220',
            '2110','2210','2220',
            '4110','4120','4130','4140','4150','4210','4220',
            '5110','5120','5130','5140','5150','5160','5170','5180','5190','5200','5210',
            '5311','5312','5313','5350','5400','5450','5500','5550','5600','5650','5700','5750','5800','5850','5900','5950',
            '6000','6050','6100','6150','6200',
            '7110','7210','7220')
           AND a.account_subtype IS NULL"
    use reader = cmd.ExecuteReader()
    // If any leaf accounts have NULL subtype, that's a failure
    if reader.HasRows then
        let mutable missing = []
        while reader.Read() do
            missing <- reader.GetString(0) :: missing
        Assert.Fail(sprintf "Leaf accounts with NULL account_subtype: %A" missing)

// =====================================================================
// @FT-SR-004 — Running seeds twice produces identical state
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SR-004")>]
let ``Running seeds twice produces identical state`` () =
    // First run
    let (exit1, _, stderr1) = runSeedsDev ()
    Assert.True((exit1 = 0), sprintf "First seed run failed: %s" stderr1)

    // Second run
    let (exit2, _, stderr2) = runSeedsDev ()
    Assert.True((exit2 = 0), sprintf "Second seed run failed: %s" stderr2)

    use conn = DataSource.openConnection()

    // Verify fiscal_period count for seeded range
    use cmd = conn.CreateCommand()
    cmd.CommandText <-
        "SELECT COUNT(*) FROM ledger.fiscal_period
         WHERE period_key >= '2026-01' AND period_key <= '2028-12'"
    let fpCount = cmd.ExecuteScalar() :?> int64
    Assert.Equal(36L, fpCount)

    // Verify account count for seeded codes
    cmd.CommandText <-
        "SELECT COUNT(*) FROM ledger.account
         WHERE code IN (
            '1000','1100','1110','1120','1200','1210','1220',
            '2000','2100','2110','2200','2210','2220',
            '3000','3010','3020','3099',
            '4000','4100','4110','4120','4130','4140','4150','4200','4210','4220',
            '5000','5100','5110','5120','5130','5140','5150','5160','5170','5180','5190','5200','5210',
            '5300','5310','5311','5312','5313','5350','5400','5450','5500','5550','5600','5650','5700','5750','5800','5850','5900','5950',
            '6000','6050','6100','6150','6200',
            '7100','7110','7200','7210','7220',
            '9010')"
    let acctCount = cmd.ExecuteScalar() :?> int64
    Assert.Equal(69L, acctCount)

    // No errors in stderr on the second run (the idempotency guarantee)
    Assert.True(
        String.IsNullOrWhiteSpace(stderr2),
        sprintf "Second run had stderr output: %s" stderr2)

// =====================================================================
// @FT-SR-005 — Runner stops on SQL error with non-zero exit
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SR-005")>]
let ``Runner stops on SQL error with non-zero exit`` () =
    // Create a temp directory with a bad SQL script and a good one after it
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
    let envDir = Path.Combine(tempDir, "broken")
    Directory.CreateDirectory(envDir) |> ignore
    try
        File.WriteAllText(
            Path.Combine(envDir, "010-bad.sql"),
            "SELECT * FROM this_table_does_not_exist_xyz;")
        File.WriteAllText(
            Path.Combine(envDir, "020-good.sql"),
            "SELECT 1;")

        let (exitCode, stdout, _) = runSeedsCustomDir tempDir "broken"
        Assert.True((exitCode <> 0), "Expected non-zero exit code on SQL error")

        // Verify the second script was not executed
        Assert.DoesNotContain("020-good.sql", stdout)
    finally
        if Directory.Exists tempDir then
            Directory.Delete(tempDir, true)

// =====================================================================
// @FT-SR-006 — Runner exits non-zero for nonexistent environment directory
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SR-006")>]
let ``Runner exits non-zero for nonexistent environment directory`` () =
    let (exitCode, _, stderr) = runSeeds "prod" []
    Assert.True((exitCode <> 0), "Expected non-zero exit code for nonexistent env")
    Assert.True(stderr.Contains("not found", StringComparison.OrdinalIgnoreCase),
        sprintf "Expected 'not found' in stderr: %s" stderr)

// =====================================================================
// @FT-SR-007 — Seeds execute in numeric filename order
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-SR-007")>]
let ``Seeds execute in numeric filename order`` () =
    let (exitCode, stdout, stderr) = runSeedsDev ()
    Assert.True((exitCode = 0), sprintf "Seed runner failed: %s" stderr)

    // The runner prints "Running: NNN-name.sql" for each script.
    // Verify 010 appears before 020 in the output.
    let idx010 = stdout.IndexOf("010-fiscal-periods.sql")
    let idx020 = stdout.IndexOf("020-chart-of-accounts.sql")
    Assert.True(idx010 >= 0, "010-fiscal-periods.sql not found in output")
    Assert.True(idx020 >= 0, "020-chart-of-accounts.sql not found in output")
    Assert.True(idx010 < idx020,
        sprintf "010 (at %d) should appear before 020 (at %d) in output" idx010 idx020)
