module LeoBloom.CLI.DiagnosticCommands

open System
open Argu
open LeoBloom.Ops
open LeoBloom.CLI.OutputFormatter

// --- Argu DU definitions ---

type OrphanedPostingsArgs =
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Json -> "Output in JSON format"

type DiagnosticArgs =
    | [<CliPrefix(CliPrefix.None)>] Orphaned_Postings of ParseResults<OrphanedPostingsArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Orphaned_Postings _ -> "Detect orphaned journal entry postings"

// --- Command handlers ---

let private handleOrphanedPostings (isJson: bool) : int =
    match OrphanedPostingService.findOrphanedPostings() with
    | Error errs ->
        for err in errs do
            Console.Error.WriteLine(sprintf "Error: %s" err)
        ExitCodes.systemError
    | Ok result ->
        writeOrphanedPostings isJson result

// --- Dispatch ---

let dispatch (isJson: bool) (args: ParseResults<DiagnosticArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (Orphaned_Postings subArgs) ->
        handleOrphanedPostings (isJson || subArgs.Contains OrphanedPostingsArgs.Json)
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
