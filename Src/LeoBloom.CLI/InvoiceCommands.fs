module LeoBloom.CLI.InvoiceCommands

open System
open Argu
open LeoBloom.Domain.Ops
open LeoBloom.Ops
open LeoBloom.Utilities
open LeoBloom.CLI.OutputFormatter

// --- Argu DU definitions for invoice subcommands ---

type InvoiceRecordArgs =
    | [<Mandatory>] Tenant of string
    | [<Mandatory>] Fiscal_Period_Id of int
    | [<Mandatory>] Rent_Amount of decimal
    | [<Mandatory>] Utility_Share of decimal
    | [<Mandatory>] Total_Amount of decimal
    | [<Mandatory>] Generated_At of string
    | Document_Path of string
    | Notes of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Tenant _ -> "Tenant name"
            | Fiscal_Period_Id _ -> "Fiscal period ID"
            | Rent_Amount _ -> "Rent amount"
            | Utility_Share _ -> "Utility share amount"
            | Total_Amount _ -> "Total invoice amount"
            | Generated_At _ -> "Invoice generation timestamp (ISO 8601)"
            | Document_Path _ -> "Path to invoice document (optional)"
            | Notes _ -> "Invoice notes (optional)"
            | Json -> "Output in JSON format"

type InvoiceShowArgs =
    | [<MainCommand; Mandatory>] Invoice_Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Invoice_Id _ -> "Invoice ID to display"
            | Json -> "Output in JSON format"

type InvoiceListArgs =
    | Tenant of string
    | Fiscal_Period_Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Tenant _ -> "Filter by tenant name (optional)"
            | Fiscal_Period_Id _ -> "Filter by fiscal period ID (optional)"
            | Json -> "Output in JSON format"

type InvoiceArgs =
    | [<CliPrefix(CliPrefix.None)>] Record of ParseResults<InvoiceRecordArgs>
    | [<CliPrefix(CliPrefix.None)>] Show of ParseResults<InvoiceShowArgs>
    | [<CliPrefix(CliPrefix.None)>] List of ParseResults<InvoiceListArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Record _ -> "Record a new invoice"
            | Show _ -> "Show an invoice by ID"
            | List _ -> "List invoices with optional filters"

// --- Parsing helpers ---

let private parseDateTimeOffset (raw: string) : Result<DateTimeOffset, string> =
    match DateTimeOffset.TryParse(raw) with
    | true, dto -> Ok dto
    | false, _ -> Error (sprintf "Invalid date/time format '%s' -- expected ISO 8601 (e.g. 2026-04-01T12:00Z)" raw)

// --- Command handlers ---

let private handleRecord (isJson: bool) (args: ParseResults<InvoiceRecordArgs>) : int =
    let isJson = isJson || args.Contains InvoiceRecordArgs.Json
    let tenant = args.GetResult InvoiceRecordArgs.Tenant
    let fpId = args.GetResult InvoiceRecordArgs.Fiscal_Period_Id
    let rentAmount = args.GetResult InvoiceRecordArgs.Rent_Amount
    let utilityShare = args.GetResult InvoiceRecordArgs.Utility_Share
    let totalAmount = args.GetResult InvoiceRecordArgs.Total_Amount
    let generatedAtRaw = args.GetResult InvoiceRecordArgs.Generated_At
    let documentPath = args.TryGetResult InvoiceRecordArgs.Document_Path
    let notes = args.TryGetResult InvoiceRecordArgs.Notes

    match parseDateTimeOffset generatedAtRaw with
    | Error e ->
        write isJson (Error [e])
    | Ok generatedAt ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let cmd : RecordInvoiceCommand =
                { tenant = tenant
                  fiscalPeriodId = fpId
                  rentAmount = rentAmount
                  utilityShare = utilityShare
                  totalAmount = totalAmount
                  generatedAt = generatedAt
                  documentPath = documentPath
                  notes = notes }

            let result = InvoiceService.recordInvoice txn cmd
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            write isJson (result |> Result.map (fun v -> v :> obj))
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleShow (isJson: bool) (args: ParseResults<InvoiceShowArgs>) : int =
    let isJson = isJson || args.Contains InvoiceShowArgs.Json
    let invoiceId = args.GetResult InvoiceShowArgs.Invoice_Id

    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = InvoiceService.showInvoice txn invoiceId
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        write isJson (result |> Result.map (fun v -> v :> obj))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private handleList (isJson: bool) (args: ParseResults<InvoiceListArgs>) : int =
    let isJson = isJson || args.Contains InvoiceListArgs.Json
    let tenant = args.TryGetResult InvoiceListArgs.Tenant
    let fpId = args.TryGetResult InvoiceListArgs.Fiscal_Period_Id

    let filter : ListInvoicesFilter =
        { tenant = tenant
          fiscalPeriodId = fpId }

    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let invoices = InvoiceService.listInvoices txn filter
        txn.Commit()
        writeInvoiceList isJson invoices
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

// --- Dispatch ---

let dispatch (isJson: bool) (args: ParseResults<InvoiceArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (Record recordArgs) -> handleRecord isJson recordArgs
    | Some (Show showArgs) -> handleShow isJson showArgs
    | Some (List listArgs) -> handleList isJson listArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
