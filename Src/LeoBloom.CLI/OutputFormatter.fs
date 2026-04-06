module LeoBloom.CLI.OutputFormatter

open System
open System.Text.Json
open System.Text.Json.Serialization
open LeoBloom.Domain.Ledger
open LeoBloom.Domain.Ops
open LeoBloom.Reporting.ReportingTypes

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

// --- Schedule E formatting ---

let private formatScheduleE (report: ScheduleEReport) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Schedule E — Supplemental Income and Loss (%d)" report.year)
    lines.Add(sprintf "  %-6s  %-45s  %12s" "Line" "Description" "Amount")
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 6 "-") (String.replicate 45 "-") (String.replicate 12 "-"))
    for item in report.lineItems do
        lines.Add(sprintf "  %-6d  %-45s  %12s" item.lineNumber item.description (sprintf "%M" item.amount))
        for (subDesc, subAmt) in item.subDetail do
            lines.Add(sprintf "  %6s    %-43s  %12s" "" subDesc (sprintf "%M" subAmt))
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 6 "-") (String.replicate 45 "-") (String.replicate 12 "-"))
    lines.Add(sprintf "  %-6s  %-45s  %12s" "" "Total Expenses" (sprintf "%M" report.totalExpenses))
    lines.Add(sprintf "  %-6s  %-45s  %12s" "" "Net Rental Income (Loss)" (sprintf "%M" report.netRentalIncome))
    String.Join(Environment.NewLine, lines)

// --- General Ledger formatting ---

let private formatGeneralLedger (report: GeneralLedgerReport) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "General Ledger — %s %s" report.accountCode report.accountName)
    lines.Add(sprintf "  Period: %s to %s" (report.fromDate.ToString("yyyy-MM-dd")) (report.toDate.ToString("yyyy-MM-dd")))
    lines.Add("")
    lines.Add(sprintf "  %-12s  %-8s  %-30s  %12s  %12s  %12s" "Date" "Entry" "Description" "Debit" "Credit" "Balance")
    lines.Add(sprintf "  %s  %s  %s  %s  %s  %s"
        (String.replicate 12 "-") (String.replicate 8 "-") (String.replicate 30 "-")
        (String.replicate 12 "-") (String.replicate 12 "-") (String.replicate 12 "-"))
    for entry in report.entries do
        let debit = if entry.debitAmount <> 0m then sprintf "%M" entry.debitAmount else ""
        let credit = if entry.creditAmount <> 0m then sprintf "%M" entry.creditAmount else ""
        let desc = if entry.description.Length > 30 then entry.description.Substring(0, 27) + "..." else entry.description
        lines.Add(sprintf "  %-12s  %-8d  %-30s  %12s  %12s  %12s"
            (entry.date.ToString("yyyy-MM-dd"))
            entry.journalEntryId
            desc
            debit credit
            (sprintf "%M" entry.runningBalance))
    lines.Add(sprintf "  %s  %s  %s  %s  %s  %s"
        (String.replicate 12 "-") (String.replicate 8 "-") (String.replicate 30 "-")
        (String.replicate 12 "-") (String.replicate 12 "-") (String.replicate 12 "-"))
    lines.Add(sprintf "  %-12s  %-8s  %-30s  %12s  %12s  %12s" "" "" "Ending Balance" "" "" (sprintf "%M" report.endingBalance))
    String.Join(Environment.NewLine, lines)

// --- Cash Receipts formatting ---

let private formatCashReceipts (report: CashReceiptsReport) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Cash Receipts — %s to %s" (report.fromDate.ToString("yyyy-MM-dd")) (report.toDate.ToString("yyyy-MM-dd")))
    lines.Add("")
    lines.Add(sprintf "  %-12s  %-8s  %-30s  %-25s  %12s" "Date" "Entry" "Description" "Counterparty" "Amount")
    lines.Add(sprintf "  %s  %s  %s  %s  %s"
        (String.replicate 12 "-") (String.replicate 8 "-") (String.replicate 30 "-")
        (String.replicate 25 "-") (String.replicate 12 "-"))
    for entry in report.entries do
        let desc = if entry.description.Length > 30 then entry.description.Substring(0, 27) + "..." else entry.description
        let counter = if entry.counterpartyAccount.Length > 25 then entry.counterpartyAccount.Substring(0, 22) + "..." else entry.counterpartyAccount
        lines.Add(sprintf "  %-12s  %-8d  %-30s  %-25s  %12s"
            (entry.date.ToString("yyyy-MM-dd"))
            entry.journalEntryId
            desc counter
            (sprintf "%M" entry.amount))
    lines.Add(sprintf "  %s  %s  %s  %s  %s"
        (String.replicate 12 "-") (String.replicate 8 "-") (String.replicate 30 "-")
        (String.replicate 25 "-") (String.replicate 12 "-"))
    lines.Add(sprintf "  %-12s  %-8s  %-30s  %-25s  %12s" "" "" "Total Receipts" "" (sprintf "%M" report.totalReceipts))
    String.Join(Environment.NewLine, lines)

// --- Cash Disbursements formatting ---

let private formatCashDisbursements (report: CashDisbursementsReport) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Cash Disbursements — %s to %s" (report.fromDate.ToString("yyyy-MM-dd")) (report.toDate.ToString("yyyy-MM-dd")))
    lines.Add("")
    lines.Add(sprintf "  %-12s  %-8s  %-30s  %-25s  %12s" "Date" "Entry" "Description" "Counterparty" "Amount")
    lines.Add(sprintf "  %s  %s  %s  %s  %s"
        (String.replicate 12 "-") (String.replicate 8 "-") (String.replicate 30 "-")
        (String.replicate 25 "-") (String.replicate 12 "-"))
    for entry in report.entries do
        let desc = if entry.description.Length > 30 then entry.description.Substring(0, 27) + "..." else entry.description
        let counter = if entry.counterpartyAccount.Length > 25 then entry.counterpartyAccount.Substring(0, 22) + "..." else entry.counterpartyAccount
        lines.Add(sprintf "  %-12s  %-8d  %-30s  %-25s  %12s"
            (entry.date.ToString("yyyy-MM-dd"))
            entry.journalEntryId
            desc counter
            (sprintf "%M" entry.amount))
    lines.Add(sprintf "  %s  %s  %s  %s  %s"
        (String.replicate 12 "-") (String.replicate 8 "-") (String.replicate 30 "-")
        (String.replicate 25 "-") (String.replicate 12 "-"))
    lines.Add(sprintf "  %-12s  %-8s  %-30s  %-25s  %12s" "" "" "Total Disbursements" "" (sprintf "%M" report.totalDisbursements))
    String.Join(Environment.NewLine, lines)

// --- Invoice formatting ---

let private formatInvoice (inv: Invoice) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Invoice #%d" inv.id)
    lines.Add(sprintf "  Tenant:         %s" inv.tenant)
    lines.Add(sprintf "  Fiscal Period:  %d" inv.fiscalPeriodId)
    lines.Add(sprintf "  Rent Amount:    %M" inv.rentAmount)
    lines.Add(sprintf "  Utility Share:  %M" inv.utilityShare)
    lines.Add(sprintf "  Total Amount:   %M" inv.totalAmount)
    lines.Add(sprintf "  Generated At:   %s" (inv.generatedAt.ToString("yyyy-MM-dd HH:mm:ss")))
    lines.Add(sprintf "  Document Path:  %s" (inv.documentPath |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Notes:          %s" (inv.notes |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Created:        %s" (inv.createdAt.ToString("yyyy-MM-dd HH:mm:ss")))
    lines.Add(sprintf "  Modified:       %s" (inv.modifiedAt.ToString("yyyy-MM-dd HH:mm:ss")))
    String.Join(Environment.NewLine, lines)

let private formatInvoiceList (invoices: Invoice list) : string =
    if invoices.IsEmpty then ""
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-15s  %-8s  %12s  %12s  %12s" "ID" "Tenant" "Period" "Rent" "Utility" "Total")
        lines.Add(sprintf "  %s  %s  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 15 "-") (String.replicate 8 "-")
            (String.replicate 12 "-") (String.replicate 12 "-") (String.replicate 12 "-"))
        for inv in invoices do
            lines.Add(sprintf "  %-6d  %-15s  %-8d  %12s  %12s  %12s"
                inv.id inv.tenant inv.fiscalPeriodId
                (sprintf "%M" inv.rentAmount) (sprintf "%M" inv.utilityShare) (sprintf "%M" inv.totalAmount))
        String.Join(Environment.NewLine, lines)

// --- Transfer formatting ---

let private formatTransfer (t: Transfer) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Transfer #%d" t.id)
    lines.Add(sprintf "  From Account:   %d" t.fromAccountId)
    lines.Add(sprintf "  To Account:     %d" t.toAccountId)
    lines.Add(sprintf "  Amount:         %M" t.amount)
    lines.Add(sprintf "  Status:         %s" (TransferStatus.toString t.status))
    lines.Add(sprintf "  Initiated:      %s" (t.initiatedDate.ToString("yyyy-MM-dd")))
    lines.Add(sprintf "  Exp. Settle:    %s" (t.expectedSettlement |> Option.map (fun d -> d.ToString("yyyy-MM-dd")) |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Confirmed:      %s" (t.confirmedDate |> Option.map (fun d -> d.ToString("yyyy-MM-dd")) |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Journal Entry:  %s" (t.journalEntryId |> Option.map string |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Description:    %s" (t.description |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Created:        %s" (t.createdAt.ToString("yyyy-MM-dd HH:mm:ss")))
    lines.Add(sprintf "  Modified:       %s" (t.modifiedAt.ToString("yyyy-MM-dd HH:mm:ss")))
    String.Join(Environment.NewLine, lines)

let private formatTransferList (transfers: Transfer list) : string =
    if transfers.IsEmpty then ""
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-10s  %-8s  %-8s  %12s  %-12s" "ID" "Status" "From" "To" "Amount" "Initiated")
        lines.Add(sprintf "  %s  %s  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 10 "-") (String.replicate 8 "-")
            (String.replicate 8 "-") (String.replicate 12 "-") (String.replicate 12 "-"))
        for t in transfers do
            lines.Add(sprintf "  %-6d  %-10s  %-8d  %-8d  %12s  %-12s"
                t.id (TransferStatus.toString t.status) t.fromAccountId t.toAccountId
                (sprintf "%M" t.amount) (t.initiatedDate.ToString("yyyy-MM-dd")))
        String.Join(Environment.NewLine, lines)

// --- Dispatch formatting based on type ---

let formatHuman (value: obj) : string =
    match value with
    | :? PostedJournalEntry as p -> formatPostedEntry p
    | :? JournalEntry as e -> formatJournalEntry e
    | :? ScheduleEReport as r -> formatScheduleE r
    | :? GeneralLedgerReport as r -> formatGeneralLedger r
    | :? CashReceiptsReport as r -> formatCashReceipts r
    | :? CashDisbursementsReport as r -> formatCashDisbursements r
    | :? Invoice as inv -> formatInvoice inv
    | :? Transfer as t -> formatTransfer t
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

// --- Write human-only Result to stdout/stderr (no --json support) ---

let writeHuman (result: Result<'a, string list>) : int =
    match result with
    | Ok value ->
        let output = formatHuman (value :> obj)
        Console.Out.WriteLine(output)
        ExitCodes.success
    | Error errors ->
        for err in errors do
            Console.Error.WriteLine(sprintf "Error: %s" err)
        ExitCodes.businessError

/// Dedicated write function for Invoice list to avoid F# type erasure
/// issues with generic list pattern matching in formatHuman.
let writeInvoiceList (isJson: bool) (invoices: Invoice list) : int =
    if isJson then
        let output = formatJson invoices
        Console.Out.WriteLine(output)
    else
        let output = formatInvoiceList invoices
        if not (String.IsNullOrEmpty output) then
            Console.Out.WriteLine(output)
    ExitCodes.success

/// Dedicated write function for Transfer list to avoid F# type erasure
/// issues with generic list pattern matching in formatHuman.
let writeTransferList (isJson: bool) (transfers: Transfer list) : int =
    if isJson then
        let output = formatJson transfers
        Console.Out.WriteLine(output)
    else
        let output = formatTransferList transfers
        if not (String.IsNullOrEmpty output) then
            Console.Out.WriteLine(output)
    ExitCodes.success

let writeHumanErrors (errors: string list) : int =
    for err in errors do
        Console.Error.WriteLine(sprintf "Error: %s" err)
    ExitCodes.businessError
