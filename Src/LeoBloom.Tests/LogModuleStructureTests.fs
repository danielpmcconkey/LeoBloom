module LeoBloom.Tests.LogModuleStructureTests

open System
open System.IO
open Xunit

// =====================================================================
// Helpers
// =====================================================================

let private repoRoot =
    let baseDir = AppContext.BaseDirectory
    let rec walkUp (dir: string) =
        if File.Exists(Path.Combine(dir, "LeoBloom.sln"))
           || Directory.Exists(Path.Combine(dir, "Src")) && Directory.Exists(Path.Combine(dir, "Specs")) then
            dir
        else
            let parent = Directory.GetParent(dir)
            if parent = null then failwith "Could not find repo root"
            walkUp parent.FullName
    walkUp baseDir

let private srcDir = Path.Combine(repoRoot, "Src")

// =====================================================================
// @FT-LMS-008 -- TestHelpers uses Log.errorExn instead of eprintfn
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LMS-008")>]
let ``TestHelpers uses Log.errorExn instead of eprintfn`` () =
    let testHelpers = Path.Combine(srcDir, "LeoBloom.Tests", "TestHelpers.fs")
    Assert.True(File.Exists(testHelpers), $"Expected file: {testHelpers}")

    let content = File.ReadAllText(testHelpers)
    Assert.False(content.Contains("eprintfn"),
        "TestHelpers.fs should not contain eprintfn -- should use Log.errorExn instead")

// =====================================================================
// @FT-LMS-009 -- No printfn or eprintfn in any Src project except Migrations
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LMS-009")>]
let ``No printfn or eprintfn in any Src project except Migrations`` () =
    // Build search targets from parts so this test file doesn't match itself
    let target1 = "print" + "fn"
    let target2 = "eprint" + "fn"

    let fsFiles =
        Directory.GetFiles(srcDir, "*.fs", SearchOption.AllDirectories)
        |> Array.filter (fun f ->
            not (f.Contains("LeoBloom.Migrations", StringComparison.OrdinalIgnoreCase))
            // Exclude test files -- they reference printfn/eprintfn in assertions
            && not (f.Contains("LeoBloom.Tests", StringComparison.OrdinalIgnoreCase)))

    let violations =
        fsFiles
        |> Array.filter (fun f ->
            let content = File.ReadAllText(f)
            content.Contains(target1) || content.Contains(target2))
        |> Array.map (fun f -> f.Replace(repoRoot, ""))

    let msg = sprintf "Found printfn/eprintfn in: %s" (String.Join(", ", violations))
    Assert.True(violations.Length = 0, msg)
