module LeoBloom.CLI.Program

open System
open Argu
open LeoBloom.CLI.LedgerCommands
open LeoBloom.CLI.ReportCommands
open LeoBloom.CLI.InvoiceCommands
open LeoBloom.CLI.TransferCommands
open LeoBloom.CLI.AccountCommands
open LeoBloom.CLI.PeriodCommands
open LeoBloom.CLI.ErrorHandler
open LeoBloom.Utilities

// --- Top-level Argu DU ---

type LeoBloomArgs =
    | [<CliPrefix(CliPrefix.None)>] Ledger of ParseResults<LedgerArgs>
    | [<CliPrefix(CliPrefix.None)>] Report of ParseResults<ReportArgs>
    | [<CliPrefix(CliPrefix.None)>] Invoice of ParseResults<InvoiceArgs>
    | [<CliPrefix(CliPrefix.None)>] Transfer of ParseResults<TransferArgs>
    | [<CliPrefix(CliPrefix.None)>] Account of ParseResults<AccountArgs>
    | [<CliPrefix(CliPrefix.None)>] Period of ParseResults<PeriodArgs>
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Ledger _ -> "Ledger commands (post, void, show)"
            | Report _ -> "Report commands (schedule-e, general-ledger, cash-receipts, cash-disbursements, trial-balance, balance-sheet, income-statement, pnl-subtree, account-balance)"
            | Invoice _ -> "Invoice commands (record, show, list)"
            | Transfer _ -> "Transfer commands (initiate, confirm, show, list)"
            | Account _ -> "Account commands (list, show, balance)"
            | Period _ -> "Period commands (list, close, reopen, create)"
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
                ReportCommands.dispatch reportResults
            | Some (Invoice invoiceResults) ->
                InvoiceCommands.dispatch isJson invoiceResults
            | Some (Transfer transferResults) ->
                TransferCommands.dispatch isJson transferResults
            | Some (Account accountResults) ->
                AccountCommands.dispatch isJson accountResults
            | Some (Period periodResults) ->
                PeriodCommands.dispatch isJson periodResults
            | _ ->
                Console.Error.WriteLine(parser.PrintUsage())
                ExitCodes.systemError
        with
        | ex ->
            Console.Error.WriteLine(sprintf "Unhandled error: %s" ex.Message)
            ExitCodes.systemError

    Log.closeAndFlush()
    exit exitCode
