module LeoBloom.Tests.LogModuleStructureTests

open System
open System.IO
open System.Reflection
open System.Xml.Linq
open Xunit
open LeoBloom.Utilities

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

let private utilitiesFsproj =
    Path.Combine(srcDir, "LeoBloom.Utilities", "LeoBloom.Utilities.fsproj")

let private hasPackageReference (packageName: string) =
    let doc = XDocument.Load(utilitiesFsproj)
    doc.Descendants(XName.Get "PackageReference")
    |> Seq.exists (fun el ->
        let inc = el.Attribute(XName.Get "Include")
        inc <> null && inc.Value.Equals(packageName, StringComparison.OrdinalIgnoreCase))

// =====================================================================
// @FT-LMS-001 -- Serilog core package is referenced
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LMS-001")>]
let ``Serilog core package is referenced`` () =
    Assert.True(hasPackageReference "Serilog",
        "LeoBloom.Utilities.fsproj should reference Serilog")

// =====================================================================
// @FT-LMS-002 -- Serilog.Sinks.Console package is referenced
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LMS-002")>]
let ``Serilog.Sinks.Console package is referenced`` () =
    Assert.True(hasPackageReference "Serilog.Sinks.Console",
        "LeoBloom.Utilities.fsproj should reference Serilog.Sinks.Console")

// =====================================================================
// @FT-LMS-003 -- Serilog.Sinks.File package is referenced
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LMS-003")>]
let ``Serilog.Sinks.File package is referenced`` () =
    Assert.True(hasPackageReference "Serilog.Sinks.File",
        "LeoBloom.Utilities.fsproj should reference Serilog.Sinks.File")

// =====================================================================
// @FT-LMS-004 -- Serilog.Settings.Configuration package is referenced
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LMS-004")>]
let ``Serilog.Settings.Configuration package is referenced`` () =
    Assert.True(hasPackageReference "Serilog.Settings.Configuration",
        "LeoBloom.Utilities.fsproj should reference Serilog.Settings.Configuration")

// =====================================================================
// @FT-LMS-005 -- Log.fs exists in LeoBloom.Utilities
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LMS-005")>]
let ``Log.fs exists in LeoBloom.Utilities`` () =
    let logFs = Path.Combine(srcDir, "LeoBloom.Utilities", "Log.fs")
    Assert.True(File.Exists(logFs), $"Expected file: {logFs}")

// =====================================================================
// @FT-LMS-006 -- Log module exposes the required functions
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LMS-006")>]
let ``Log module exposes the required functions`` () =
    // Touch the module to ensure the assembly is loaded
    Log.initialize()

    let asm =
        AppDomain.CurrentDomain.GetAssemblies()
        |> Array.find (fun a -> a.GetName().Name = "LeoBloom.Utilities")

    let logType =
        asm.GetTypes()
        |> Array.tryFind (fun t -> t.FullName = "LeoBloom.Utilities.Log")
        |> Option.defaultWith (fun () -> failwith "Could not find Log type in assembly")

    let publicMethods =
        logType.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
        |> Array.filter (fun m -> not m.IsSpecialName)
        |> Array.map (fun m -> m.Name)
        |> Set.ofArray

    let required = [ "initialize"; "closeAndFlush"; "info"; "warn"; "error"; "fatal"; "errorExn" ]

    for fn in required do
        Assert.True(Set.contains fn publicMethods,
            $"Log module should expose '{fn}', found: {publicMethods}")

// =====================================================================
// @FT-LMS-007 -- Log module does not expose a debug function
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LMS-007")>]
let ``Log module does not expose a debug function`` () =
    let logFs = Path.Combine(srcDir, "LeoBloom.Utilities", "Log.fs")
    let content = File.ReadAllText(logFs)

    // Check for any let binding named debug (case-insensitive)
    let lines = content.Split('\n')
    let debugBindings =
        lines
        |> Array.filter (fun line ->
            let trimmed = line.Trim()
            trimmed.StartsWith("let debug", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("let ``debug", StringComparison.OrdinalIgnoreCase))

    let msg = sprintf "Log.fs should not contain debug bindings, found: %s" (String.Join("; ", debugBindings))
    Assert.True(debugBindings.Length = 0, msg)

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

// =====================================================================
// @FT-LMS-010 -- Migrations has no reference to LeoBloom.Utilities
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LMS-010")>]
let ``Migrations has no reference to LeoBloom.Utilities`` () =
    let migFsproj = Path.Combine(srcDir, "LeoBloom.Migrations", "LeoBloom.Migrations.fsproj")
    Assert.True(File.Exists(migFsproj), $"Expected file: {migFsproj}")

    let doc = XDocument.Load(migFsproj)
    let projRefs =
        doc.Descendants(XName.Get "ProjectReference")
        |> Seq.map (fun el -> el.Attribute(XName.Get "Include").Value)
        |> Seq.filter (fun r -> r.Contains("LeoBloom.Utilities", StringComparison.OrdinalIgnoreCase))
        |> Seq.toList

    let msg = sprintf "Migrations should not reference LeoBloom.Utilities, found: %s" (String.Join(", ", projRefs))
    Assert.True(projRefs.IsEmpty, msg)

// =====================================================================
// @FT-LMS-011 -- Migrations source files have no changes from this project
// (Verify no Serilog/Log usage in Migrations -- it should remain untouched)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-LMS-011")>]
let ``Migrations source files have no changes from this project`` () =
    let migDir = Path.Combine(srcDir, "LeoBloom.Migrations")
    let fsFiles = Directory.GetFiles(migDir, "*.fs", SearchOption.AllDirectories)

    // Migrations should have no Serilog or Log module references
    let violations =
        fsFiles
        |> Array.filter (fun f ->
            let content = File.ReadAllText(f)
            content.Contains("Serilog") || content.Contains("LeoBloom.Utilities"))
        |> Array.map (fun f -> f.Replace(repoRoot, ""))

    let msg = sprintf "Found Serilog/Utilities references in Migrations: %s" (String.Join(", ", violations))
    Assert.True(violations.Length = 0, msg)

    // Migrations fsproj should have no Serilog packages
    let migFsproj = Path.Combine(migDir, "LeoBloom.Migrations.fsproj")
    let doc = XDocument.Load(migFsproj)
    let serilogPkgs =
        doc.Descendants(XName.Get "PackageReference")
        |> Seq.filter (fun el ->
            let inc = el.Attribute(XName.Get "Include")
            inc <> null && inc.Value.StartsWith("Serilog", StringComparison.OrdinalIgnoreCase))
        |> Seq.toList

    Assert.True(serilogPkgs.IsEmpty,
        "Migrations should have no Serilog packages")
