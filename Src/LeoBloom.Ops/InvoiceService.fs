namespace LeoBloom.Ops

open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Utilities
open LeoBloom.Ledger

/// Orchestrates recording, showing, and listing invoices.
/// Caller is responsible for connection and transaction lifecycle.
module InvoiceService =

    let recordInvoice (txn: NpgsqlTransaction) (cmd: RecordInvoiceCommand) : Result<Invoice, string list> =
        Log.info "Recording invoice for tenant {Tenant} in fiscal period {FiscalPeriodId}"
            [| cmd.tenant :> obj; cmd.fiscalPeriodId :> obj |]

        // Phase 1: Pure validation
        match InvoiceValidation.validateCommand cmd with
        | Error errs ->
            Log.warn "Invoice validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            // Phase 2: DB validation + insert
            try
                let period = FiscalPeriodRepository.findById txn cmd.fiscalPeriodId
                match period with
                | None ->
                    let msg = sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId
                    Log.warn "{Msg}" [| msg :> obj |]
                    Error [ msg ]
                | Some _ ->
                    let existing = InvoiceRepository.findByTenantAndPeriod txn cmd.tenant cmd.fiscalPeriodId
                    match existing with
                    | Some _ ->
                        let msg = sprintf "An invoice already exists for tenant '%s' in fiscal period %d" cmd.tenant cmd.fiscalPeriodId
                        Log.warn "{Msg}" [| msg :> obj |]
                        Error [ msg ]
                    | None ->
                        try
                            let invoice = InvoiceRepository.insert txn cmd
                            Log.info "Invoice {InvoiceId} recorded successfully" [| invoice.id :> obj |]
                            Ok invoice
                        with
                        | :? PostgresException as pgEx when pgEx.SqlState = "23505" ->
                            let msg = sprintf "An invoice already exists for tenant '%s' in fiscal period %d" cmd.tenant cmd.fiscalPeriodId
                            Log.warn "Duplicate invoice detected via constraint: {Msg}" [| msg :> obj |]
                            Error [ msg ]
                        | ex ->
                            Log.errorExn ex "Failed to insert invoice" [||]
                            Error [ sprintf "Persistence error: %s" ex.Message ]
            with ex ->
                Log.errorExn ex "Failed during invoice DB validation" [||]
                Error [ sprintf "Persistence error: %s" ex.Message ]

    let showInvoice (txn: NpgsqlTransaction) (id: int) : Result<Invoice, string list> =
        Log.info "Showing invoice {InvoiceId}" [| id :> obj |]
        try
            let invoice = InvoiceRepository.findById txn id
            match invoice with
            | None ->
                Error [ sprintf "Invoice with id %d does not exist" id ]
            | Some inv ->
                Ok inv
        with ex ->
            Log.errorExn ex "Failed to show invoice {InvoiceId}" [| id :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    let listInvoices (txn: NpgsqlTransaction) (filter: ListInvoicesFilter) : Invoice list =
        Log.info "Listing invoices" [||]
        try
            InvoiceRepository.list txn filter
        with ex ->
            Log.errorExn ex "Failed to list invoices" [||]
            []
