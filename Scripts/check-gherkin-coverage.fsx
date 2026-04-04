#!/usr/bin/env dotnet fsi
// Gherkin ↔ Test Coverage Check
// Validates bidirectional mapping between @FT-* tags in .feature files
// and [<Trait("GherkinId", "FT-*")>] in test source files.
//
// Exit codes: 0 = full coverage, 1 = gaps found, 2 = stale references
// Output: JSON for agent consumption

open System
open System.IO
open System.Text.RegularExpressions

let specsDir = Path.Combine(__SOURCE_DIRECTORY__, "..", "Specs")
let testsDir = Path.Combine(__SOURCE_DIRECTORY__, "..", "Src", "LeoBloom.Tests")

// --- Parse feature files for @FT-* tags ---

type ScenarioInfo = { Id: string; Name: string; File: string }

let parseFeatureFiles () =
    let tagPattern = Regex(@"@(FT-[A-Z]+-\d+)")
    let scenarioPattern = Regex(@"^\s*Scenario:\s*(.+)$")
    Directory.GetFiles(specsDir, "*.feature", SearchOption.AllDirectories)
    |> Array.collect (fun filePath ->
        let lines = File.ReadAllLines(filePath)
        let relPath = Path.GetRelativePath(Path.Combine(__SOURCE_DIRECTORY__, ".."), filePath)
        let mutable pendingTag = None
        [| for line in lines do
            let tagMatch = tagPattern.Match(line)
            if tagMatch.Success then
                pendingTag <- Some tagMatch.Groups.[1].Value
            let scenarioMatch = scenarioPattern.Match(line)
            if scenarioMatch.Success then
                match pendingTag with
                | Some tag ->
                    yield { Id = tag; Name = scenarioMatch.Groups.[1].Value.Trim(); File = relPath }
                    pendingTag <- None
                | None -> () |])

// --- Parse test files for Trait("GherkinId", "FT-*") ---

type TestInfo = { Id: string; TestName: string; File: string }

let parseTestFiles () =
    let traitPattern = Regex(@"Trait\(""GherkinId"",\s*""(FT-[A-Z]+-\d+)""\)")
    let testNamePattern = Regex(@"let\s+``(.+?)``")
    Directory.GetFiles(testsDir, "*.fs", SearchOption.TopDirectoryOnly)
    |> Array.collect (fun filePath ->
        let lines = File.ReadAllLines(filePath)
        let relPath = Path.GetRelativePath(Path.Combine(__SOURCE_DIRECTORY__, ".."), filePath)
        let mutable pendingIds = []
        [| for line in lines do
            let traitMatch = traitPattern.Match(line)
            if traitMatch.Success then
                pendingIds <- traitMatch.Groups.[1].Value :: pendingIds
            let nameMatch = testNamePattern.Match(line)
            if nameMatch.Success && not pendingIds.IsEmpty then
                for id in pendingIds do
                    yield { Id = id; TestName = nameMatch.Groups.[1].Value; File = relPath }
                pendingIds <- [] |])

// --- Run analysis ---

let scenarios = parseFeatureFiles()
let tests = parseTestFiles()

let scenarioIds = scenarios |> Array.map (fun s -> s.Id) |> Set.ofArray
let testIds = tests |> Array.map (fun t -> t.Id) |> Set.ofArray

let uncoveredScenarios = Set.difference scenarioIds testIds
let staleTestRefs = Set.difference testIds scenarioIds

// --- Build mapping ---

let scenarioMap = scenarios |> Array.map (fun s -> s.Id, s) |> Map.ofArray
let testMap = tests |> Array.groupBy (fun t -> t.Id) |> Map.ofArray

// --- Output JSON ---

let escapeJson (s: string) = s.Replace("\\", "\\\\").Replace("\"", "\\\"")

printfn "{"
printfn "  \"summary\": {"
printfn "    \"total_scenarios\": %d," scenarios.Length
printfn "    \"total_test_refs\": %d," tests.Length
printfn "    \"covered\": %d," (Set.intersect scenarioIds testIds |> Set.count)
printfn "    \"uncovered\": %d," (Set.count uncoveredScenarios)
printfn "    \"stale\": %d" (Set.count staleTestRefs)
printfn "  },"

// Uncovered scenarios
printfn "  \"uncovered_scenarios\": ["
let uncoveredList = uncoveredScenarios |> Set.toArray
for i in 0 .. uncoveredList.Length - 1 do
    let id = uncoveredList.[i]
    let info = scenarioMap |> Map.tryFind id
    let name = info |> Option.map (fun s -> s.Name) |> Option.defaultValue "?"
    let file = info |> Option.map (fun s -> s.File) |> Option.defaultValue "?"
    let comma = if i < uncoveredList.Length - 1 then "," else ""
    printfn "    { \"id\": \"%s\", \"scenario\": \"%s\", \"file\": \"%s\" }%s" id (escapeJson name) (escapeJson file) comma
printfn "  ],"

// Stale test references
printfn "  \"stale_test_refs\": ["
let staleList = staleTestRefs |> Set.toArray
for i in 0 .. staleList.Length - 1 do
    let id = staleList.[i]
    let info = testMap |> Map.tryFind id |> Option.map Array.head
    let name = info |> Option.map (fun t -> t.TestName) |> Option.defaultValue "?"
    let file = info |> Option.map (fun t -> t.File) |> Option.defaultValue "?"
    let comma = if i < staleList.Length - 1 then "," else ""
    printfn "    { \"id\": \"%s\", \"test\": \"%s\", \"file\": \"%s\" }%s" id (escapeJson name) (escapeJson file) comma
printfn "  ],"

// Full mapping
printfn "  \"mapping\": ["
let allIds = Set.union scenarioIds testIds |> Set.toArray
for i in 0 .. allIds.Length - 1 do
    let id = allIds.[i]
    let scenario = scenarioMap |> Map.tryFind id
    let testList = testMap |> Map.tryFind id |> Option.defaultValue [||]
    let scenarioName = scenario |> Option.map (fun s -> escapeJson s.Name) |> Option.defaultValue ""
    let scenarioFile = scenario |> Option.map (fun s -> escapeJson s.File) |> Option.defaultValue ""
    let testNames = testList |> Array.map (fun t -> sprintf "\"%s\"" (escapeJson t.TestName)) |> String.concat ", "
    let comma = if i < allIds.Length - 1 then "," else ""
    printfn "    { \"id\": \"%s\", \"scenario\": \"%s\", \"scenario_file\": \"%s\", \"tests\": [%s] }%s" id scenarioName scenarioFile testNames comma
printfn "  ]"
printfn "}"

// --- Exit code ---
if not uncoveredScenarios.IsEmpty then
    eprintfn "FAIL: %d scenario(s) have no test coverage" (Set.count uncoveredScenarios)
    for id in uncoveredScenarios do
        let info = scenarioMap |> Map.tryFind id
        eprintfn "  %s: %s" id (info |> Option.map (fun s -> s.Name) |> Option.defaultValue "?")
    exit 1
elif not staleTestRefs.IsEmpty then
    eprintfn "WARN: %d test(s) reference nonexistent scenario IDs" (Set.count staleTestRefs)
    for id in staleTestRefs do
        eprintfn "  %s" id
    exit 2
else
    eprintfn "OK: Full bidirectional coverage (%d scenarios, %d test refs)" scenarios.Length tests.Length
    exit 0
