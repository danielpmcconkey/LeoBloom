module LeoBloom.CLI.LedgerCommands

open System
open Argu
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Ledger
open LeoBloom.Utilities
open LeoBloom.CLI.OutputFormatter
open LeoBloom.CLI.CliHelpers

// --- Argu DU definitions for ledger subcommands ---

type LedgerPostArgs =
    | [<Mandatory>] Debit of string
    | [<Mandatory>] Credit of string
    | [<Mandatory>] Date of string
    | [<Mandatory>] Description of string
    | Source of string
    | Fiscal_Period_Id of int
    | Adjustment_For_Period of int
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
            | Fiscal_Period_Id _ -> "Fiscal period ID (optional, derived from entry date when omitted)"
            | Adjustment_For_Period _ -> "Tag JE as a post-close adjustment for the given period ID (optional)"
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

type LedgerReverseArgs =
    | [<Mandatory>] Journal_Entry_Id of int
    | Date of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Journal_Entry_Id _ -> "Journal entry ID to reverse"
            | Date _ -> "Entry date for reversal (yyyy-MM-dd, defaults to today)"
            | Json -> "Output in JSON format"

type LedgerArgs =
    | [<CliPrefix(CliPrefix.None)>] Post of ParseResults<LedgerPostArgs>
    | [<CliPrefix(CliPrefix.None)>] Void of ParseResults<LedgerVoidArgs>
    | [<CliPrefix(CliPrefix.None)>] Show of ParseResults<LedgerShowArgs>
    | [<CliPrefix(CliPrefix.None)>] Reverse of ParseResults<LedgerReverseArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Post _ -> "Post a new journal entry"
            | Void _ -> "Void an existing journal entry"
            | Show _ -> "Show a journal entry with lines and references"
            | Reverse _ -> "Post a reversing entry for an existing journal entry"

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

// --- Command handlers ---

let private handlePost (isJson: bool) (args: ParseResults<LedgerPostArgs>) : int =
    let isJson = isJson || args.Contains LedgerPostArgs.Json
    let debitRaws = args.GetResults LedgerPostArgs.Debit
    let creditRaws = args.GetResults LedgerPostArgs.Credit
    let dateRaw = args.GetResult LedgerPostArgs.Date
    let description = args.GetResult LedgerPostArgs.Description
    let source = args.TryGetResult Source
    let fpIdOpt = args.TryGetResult Fiscal_Period_Id
    let adjForPeriodOpt = args.TryGetResult Adjustment_For_Period
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
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
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

            // Resolve fiscal period: explicit override or derive from entry date
            let fpIdResult =
                match fpIdOpt with
                | Some fpId -> Ok fpId
                | None ->
                    match FiscalPeriodRepository.findOpenPeriodForDate txn entryDate with
                    | Some period -> Ok period.id
                    | None -> Error [ sprintf "No open fiscal period covers date %O — provide --fiscal-period-id to override" entryDate ]

            match fpIdResult with
            | Error errs ->
                txn.Rollback()
                write isJson (Error errs)
            | Ok fpId ->
                let cmd : PostJournalEntryCommand =
                    { entryDate = entryDate
                      description = description
                      source = source
                      fiscalPeriodId = fpId
                      lines = debitLines @ creditLines
                      references = refs
                      adjustmentForPeriodId = adjForPeriodOpt }

                let result = JournalEntryService.post txn cmd
                match result with
                | Ok _ -> txn.Commit()
                | Error _ -> txn.Rollback()
                write isJson (result |> Result.map (fun v -> v :> obj))
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleVoid (isJson: bool) (args: ParseResults<LedgerVoidArgs>) : int =
    let isJson = isJson || args.Contains LedgerVoidArgs.Json
    let entryId = args.GetResult LedgerVoidArgs.Entry_Id
    let reason = args.GetResult LedgerVoidArgs.Reason

    let cmd : VoidJournalEntryCommand =
        { journalEntryId = entryId
          voidReason = reason }

    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = JournalEntryService.voidEntry txn cmd
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        write isJson (result |> Result.map (fun v -> v :> obj))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private handleShow (isJson: bool) (args: ParseResults<LedgerShowArgs>) : int =
    let isJson = isJson || args.Contains LedgerShowArgs.Json
    let entryId = args.GetResult LedgerShowArgs.Entry_Id

    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = JournalEntryService.getEntry txn entryId
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        write isJson (result |> Result.map (fun v -> v :> obj))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private handleReverse (isJson: bool) (args: ParseResults<LedgerReverseArgs>) : int =
    let isJson = isJson || args.Contains LedgerReverseArgs.Json
    let entryId = args.GetResult LedgerReverseArgs.Journal_Entry_Id
    let dateRaw = args.TryGetResult LedgerReverseArgs.Date

    let dateResult =
        match dateRaw with
        | None -> Ok None
        | Some raw ->
            match parseDate raw with
            | Ok d -> Ok (Some d)
            | Error e -> Error [e]

    match dateResult with
    | Error errs ->
        write isJson (Error errs)
    | Ok dateOverride ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = JournalEntryService.reverseEntry txn entryId dateOverride
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            write isJson (result |> Result.map (fun v -> v :> obj))
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

// --- Dispatch ---

let dispatch (isJson: bool) (args: ParseResults<LedgerArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (Post postArgs) -> handlePost isJson postArgs
    | Some (Void voidArgs) -> handleVoid isJson voidArgs
    | Some (Show showArgs) -> handleShow isJson showArgs
    | Some (Reverse reverseArgs) -> handleReverse isJson reverseArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
