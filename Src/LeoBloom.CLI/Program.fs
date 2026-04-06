module LeoBloom.CLI.Program

open System
open Argu
open LeoBloom.CLI.LedgerCommands
open LeoBloom.CLI.ReportCommands
open LeoBloom.CLI.InvoiceCommands
open LeoBloom.CLI.ErrorHandler
open LeoBloom.Utilities

// --- Top-level Argu DU ---

type LeoBloomArgs =
    | [<CliPrefix(CliPrefix.None)>] Ledger of ParseResults<LedgerArgs>
    | [<CliPrefix(CliPrefix.None)>] Report of ParseResults<ReportArgs>
    | [<CliPrefix(CliPrefix.None)>] Invoice of ParseResults<InvoiceArgs>
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Ledger _ -> "Ledger commands (post, void, show)"
            | Report _ -> "Report commands (schedule-e, general-ledger, cash-receipts, cash-disbursements)"
            | Invoice _ -> "Invoice commands (record, show, list)"
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
            | Some (Report reportResults) ->
                if isJson then
                    Console.Error.WriteLine("Error: --json is not supported for report commands")
                    ExitCodes.businessError
                else
                    ReportCommands.dispatch reportResults
            | Some (Invoice invoiceResults) ->
                InvoiceCommands.dispatch isJson invoiceResults
            | _ ->
                Console.Error.WriteLine(parser.PrintUsage())
                ExitCodes.systemError
        with
        | ex ->
            Console.Error.WriteLine(sprintf "Unhandled error: %s" ex.Message)
            ExitCodes.systemError

    Log.closeAndFlush()
    exit exitCode
