module LeoBloom.CLI.LedgerCommands

open System
open Argu
open LeoBloom.Domain.Ledger
open LeoBloom.Ledger
open LeoBloom.CLI.OutputFormatter

// --- Argu DU definitions for ledger subcommands ---

type LedgerPostArgs =
    | [<Mandatory>] Debit of string
    | [<Mandatory>] Credit of string
    | [<Mandatory>] Date of string
    | [<Mandatory>] Description of string
    | Source of string
    | [<Mandatory>] Fiscal_Period_Id of int
    | Ref of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Debit _ -> "Debit line in acct:amount format (repeatable)"
            | Credit _ -> "Credit line in acct:amount format (repeatable)"
            | Date _ -> "Entry date (yyyy-MM-dd)"
            | Description _ -> "Entry description"
            | Source _ -> "Entry source (optional)"
            | Fiscal_Period_Id _ -> "Fiscal period ID"
            | Ref _ -> "Reference in type:value format (optional, repeatable)"
            | Json -> "Output in JSON format"

type LedgerVoidArgs =
    | [<MainCommand; Mandatory>] Entry_Id of int
    | [<Mandatory>] Reason of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Entry_Id _ -> "Journal entry ID to void"
            | Reason _ -> "Reason for voiding"
            | Json -> "Output in JSON format"

type LedgerShowArgs =
    | [<MainCommand; Mandatory>] Entry_Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Entry_Id _ -> "Journal entry ID to display"
            | Json -> "Output in JSON format"

type LedgerArgs =
    | [<CliPrefix(CliPrefix.None)>] Post of ParseResults<LedgerPostArgs>
    | [<CliPrefix(CliPrefix.None)>] Void of ParseResults<LedgerVoidArgs>
    | [<CliPrefix(CliPrefix.None)>] Show of ParseResults<LedgerShowArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Post _ -> "Post a new journal entry"
            | Void _ -> "Void an existing journal entry"
            | Show _ -> "Show a journal entry with lines and references"

// --- Parsing helpers ---

let private parseAcctAmount (raw: string) : Result<int * decimal, string> =
    match raw.IndexOf(':') with
    | -1 -> Error (sprintf "Invalid acct:amount format '%s' -- missing colon" raw)
    | idx ->
        let acctPart = raw.Substring(0, idx)
        let amtPart = raw.Substring(idx + 1)
        match Int32.TryParse(acctPart), Decimal.TryParse(amtPart) with
        | (true, acctId), (true, amount) ->
            if amount <= 0m then
                Error (sprintf "Amount must be positive, got %M in '%s'" amount raw)
            else
                Ok (acctId, amount)
        | (false, _), _ -> Error (sprintf "Invalid account ID '%s' in '%s'" acctPart raw)
        | _, (false, _) -> Error (sprintf "Invalid amount '%s' in '%s'" amtPart raw)

let private parseRef (raw: string) : Result<PostReferenceCommand, string> =
    match raw.IndexOf(':') with
    | -1 -> Error (sprintf "Invalid ref format '%s' -- missing colon" raw)
    | idx ->
        let refType = raw.Substring(0, idx)
        let refValue = raw.Substring(idx + 1)
        if String.IsNullOrWhiteSpace refType then
            Error (sprintf "Reference type cannot be empty in '%s'" raw)
        elif String.IsNullOrWhiteSpace refValue then
            Error (sprintf "Reference value cannot be empty in '%s'" raw)
        else
            Ok { referenceType = refType; referenceValue = refValue }

let private parseDate (raw: string) : Result<DateOnly, string> =
    match DateOnly.TryParseExact(raw, "yyyy-MM-dd") with
    | true, d -> Ok d
    | false, _ -> Error (sprintf "Invalid date format '%s' -- expected yyyy-MM-dd" raw)

// --- Command handlers ---

let private handlePost (isJson: bool) (args: ParseResults<LedgerPostArgs>) : int =
    let isJson = isJson || args.Contains LedgerPostArgs.Json
    let debitRaws = args.GetResults Debit
    let creditRaws = args.GetResults Credit
    let dateRaw = args.GetResult Date
    let description = args.GetResult Description
    let source = args.TryGetResult Source
    let fpId = args.GetResult Fiscal_Period_Id
    let refRaws = args.GetResults Ref

    // Parse all inputs, collect errors
    let mutable errors = []

    let debitParsed = debitRaws |> List.map parseAcctAmount
    let creditParsed = creditRaws |> List.map parseAcctAmount
    let dateParsed = parseDate dateRaw
    let refsParsed = refRaws |> List.map parseRef

    for r in debitParsed do
        match r with Error e -> errors <- errors @ [e] | _ -> ()
    for r in creditParsed do
        match r with Error e -> errors <- errors @ [e] | _ -> ()
    match dateParsed with Error e -> errors <- errors @ [e] | _ -> ()
    for r in refsParsed do
        match r with Error e -> errors <- errors @ [e] | _ -> ()

    if not errors.IsEmpty then
        write isJson (Error errors)
    else
        let debitLines =
            debitParsed |> List.map (fun r ->
                let (acctId, amt) = Result.defaultValue (0, 0m) r
                { accountId = acctId; amount = amt; entryType = EntryType.Debit; memo = None })
        let creditLines =
            creditParsed |> List.map (fun r ->
                let (acctId, amt) = Result.defaultValue (0, 0m) r
                { accountId = acctId; amount = amt; entryType = EntryType.Credit; memo = None })
        let entryDate = match dateParsed with Ok d -> d | _ -> DateOnly.MinValue
        let refs =
            refsParsed |> List.map (fun r -> Result.defaultValue { referenceType = ""; referenceValue = "" } r)

        let cmd : PostJournalEntryCommand =
            { entryDate = entryDate
              description = description
              source = source
              fiscalPeriodId = fpId
              lines = debitLines @ creditLines
              references = refs }

        let result = JournalEntryService.post cmd
        write isJson (result |> Result.map (fun v -> v :> obj))

let private handleVoid (isJson: bool) (args: ParseResults<LedgerVoidArgs>) : int =
    let isJson = isJson || args.Contains LedgerVoidArgs.Json
    let entryId = args.GetResult LedgerVoidArgs.Entry_Id
    let reason = args.GetResult LedgerVoidArgs.Reason

    let cmd : VoidJournalEntryCommand =
        { journalEntryId = entryId
          voidReason = reason }

    let result = JournalEntryService.voidEntry cmd
    write isJson (result |> Result.map (fun v -> v :> obj))

let private handleShow (isJson: bool) (args: ParseResults<LedgerShowArgs>) : int =
    let isJson = isJson || args.Contains LedgerShowArgs.Json
    let entryId = args.GetResult LedgerShowArgs.Entry_Id

    let result = JournalEntryService.getEntry entryId
    write isJson (result |> Result.map (fun v -> v :> obj))

// --- Dispatch ---

let dispatch (isJson: bool) (args: ParseResults<LedgerArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (Post postArgs) -> handlePost isJson postArgs
    | Some (Void voidArgs) -> handleVoid isJson voidArgs
    | Some (Show showArgs) -> handleShow isJson showArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
