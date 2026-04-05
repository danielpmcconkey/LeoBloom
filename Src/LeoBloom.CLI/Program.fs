module LeoBloom.CLI.Program

open System
open Argu
open LeoBloom.CLI.LedgerCommands
open LeoBloom.CLI.ErrorHandler
open LeoBloom.Utilities

// --- Top-level Argu DU ---

type LeoBloomArgs =
    | [<CliPrefix(CliPrefix.None)>] Ledger of ParseResults<LedgerArgs>
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Ledger _ -> "Ledger commands (post, void, show)"
            | Json -> "Output in JSON format"

[<EntryPoint>]
let main (argv: string array) =
    Log.initialize()

    let exitCode =
        try
            let parser = ArgumentParser.Create<LeoBloomArgs>(
                            programName = "leobloom",
                            errorHandler = LeoBloomExiter())
            let results = parser.ParseCommandLine(inputs = argv)
            let isJson = results.Contains Json

            match results.TryGetSubCommand() with
            | Some (Ledger ledgerResults) ->
                LedgerCommands.dispatch isJson ledgerResults
            | _ ->
                Console.Error.WriteLine(parser.PrintUsage())
                ExitCodes.systemError
        with
        | ex ->
            Console.Error.WriteLine(sprintf "Unhandled error: %s" ex.Message)
            ExitCodes.systemError

    Log.closeAndFlush()
    exit exitCode
