module LeoBloom.CLI.OutputFormatter

open System
open System.Text.Json
open System.Text.Json.Serialization
open LeoBloom.Domain.Ledger

// --- JSON serialization options ---

let private jsonOptions =
    let opts = JsonSerializerOptions(WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    opts.Converters.Add(JsonFSharpConverter())
    opts

// --- Human-readable formatting ---

let private formatEntryHeader (e: JournalEntry) =
    let status =
        match e.voidedAt with
        | Some dt -> sprintf "VOIDED at %s (%s)" (dt.ToString("yyyy-MM-dd HH:mm:ss")) (e.voidReason |> Option.defaultValue "")
        | None -> "POSTED"
    [ sprintf "Journal Entry #%d  [%s]" e.id status
      sprintf "  Date:           %s" (e.entryDate.ToString("yyyy-MM-dd"))
      sprintf "  Description:    %s" e.description
      sprintf "  Source:         %s" (e.source |> Option.defaultValue "(none)")
      sprintf "  Fiscal Period:  %d" e.fiscalPeriodId
      sprintf "  Created:        %s" (e.createdAt.ToString("yyyy-MM-dd HH:mm:ss"))
      sprintf "  Modified:       %s" (e.modifiedAt.ToString("yyyy-MM-dd HH:mm:ss")) ]

let private formatLines (lines: JournalEntryLine list) =
    if lines.IsEmpty then []
    else
        let header = sprintf "  %-12s  %-10s  %-10s  %s" "Account" "Debit" "Credit" "Memo"
        let separator = sprintf "  %s  %s  %s  %s" (String.replicate 12 "-") (String.replicate 10 "-") (String.replicate 10 "-") (String.replicate 20 "-")
        let rows =
            lines
            |> List.map (fun l ->
                let debit = if l.entryType = EntryType.Debit then sprintf "%M" l.amount else ""
                let credit = if l.entryType = EntryType.Credit then sprintf "%M" l.amount else ""
                let memo = l.memo |> Option.defaultValue ""
                sprintf "  %-12d  %-10s  %-10s  %s" l.accountId debit credit memo)
        [ ""; "  Lines:" ] @ [ header; separator ] @ rows

let private formatReferences (refs: JournalEntryReference list) =
    if refs.IsEmpty then []
    else
        let rows =
            refs
            |> List.map (fun r -> sprintf "    %s: %s" r.referenceType r.referenceValue)
        [ ""; "  References:" ] @ rows

let formatPostedEntry (posted: PostedJournalEntry) : string =
    let parts =
        formatEntryHeader posted.entry
        @ formatLines posted.lines
        @ formatReferences posted.references
    String.Join(Environment.NewLine, parts)

let formatJournalEntry (entry: JournalEntry) : string =
    String.Join(Environment.NewLine, formatEntryHeader entry)

// --- Dispatch formatting based on type ---

let formatHuman (value: obj) : string =
    match value with
    | :? PostedJournalEntry as p -> formatPostedEntry p
    | :? JournalEntry as e -> formatJournalEntry e
    | _ -> sprintf "%A" value

let formatJson (value: obj) : string =
    JsonSerializer.Serialize(value, jsonOptions)

// --- Write Result to stdout/stderr ---

let write (isJson: bool) (result: Result<obj, string list>) : int =
    match result with
    | Ok value ->
        let output = if isJson then formatJson value else formatHuman value
        Console.Out.WriteLine(output)
        ExitCodes.success
    | Error errors ->
        if isJson then
            let json = JsonSerializer.Serialize({| errors = errors |}, jsonOptions)
            Console.Error.WriteLine(json)
        else
            for err in errors do
                Console.Error.WriteLine(sprintf "Error: %s" err)
        ExitCodes.businessError
