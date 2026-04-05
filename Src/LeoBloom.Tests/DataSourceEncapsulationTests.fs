module LeoBloom.Tests.DataSourceEncapsulationTests

open System
open System.Reflection
open Xunit
open LeoBloom.Utilities

// =====================================================================
// @FT-DSI-001 — DataSource does not expose a connectionString binding
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DSI-001")>]
let ``DataSource exposes only openConnection as a public binding`` () =
    // Touch DataSource to ensure the Dal assembly is loaded
    use _conn = DataSource.openConnection()
    let dalAsm =
        AppDomain.CurrentDomain.GetAssemblies()
        |> Array.find (fun a -> a.GetName().Name = "LeoBloom.Utilities")

    let dsType =
        dalAsm.GetTypes()
        |> Array.tryFind (fun t -> t.FullName = "LeoBloom.Utilities.DataSource")
        |> Option.defaultWith (fun () -> failwith "Could not find DataSource type in assembly")

    // F# module bindings compile to static methods.
    // Get all public static methods (excluding property getters and compiler artifacts)
    let publicMethods =
        dsType.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
        |> Array.filter (fun m ->
            not m.IsSpecialName) // excludes property getters/setters
        |> Array.map (fun m -> m.Name)
        |> Array.sort

    // Get all public static properties
    let publicProperties =
        dsType.GetProperties(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
        |> Array.map (fun p -> p.Name)
        |> Array.sort

    // The only public binding should be openConnection (compiles to a method)
    Assert.Equal<string[]>([| "openConnection" |], publicMethods)
    Assert.Empty(publicProperties)

// =====================================================================
// @FT-DSI-008 — Migrations runs successfully against leobloom_dev
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-DSI-008")>]
let ``Migrations runs successfully against leobloom_dev`` () =
    // Verify the migrondi schema exists (created by Migrations bootstrap)
    use conn = DataSource.openConnection()
    use cmd = conn.CreateCommand()
    cmd.CommandText <-
        "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'migrondi')"
    let exists = cmd.ExecuteScalar() :?> bool
    Assert.True(exists, "The migrondi schema should exist in leobloom_dev after migrations run")
