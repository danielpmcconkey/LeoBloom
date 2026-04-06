namespace LeoBloom.Ops

open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Utilities
open LeoBloom.Ledger

/// Orchestrates recording, showing, and listing invoices.
/// Each public function opens its own connection + transaction.
module InvoiceService =

    let recordInvoice (cmd: RecordInvoiceCommand) : Result<Invoice, string list> =
        Log.info "Recording invoice for tenant {Tenant} in fiscal period {FiscalPeriodId}"
            [| cmd.tenant :> obj; cmd.fiscalPeriodId :> obj |]

        // Phase 1: Pure validation
        match InvoiceValidation.validateCommand cmd with
        | Error errs ->
            Log.warn "Invoice validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            // Phase 2: DB validation + insert
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()

            try
                let period = FiscalPeriodRepository.findById txn cmd.fiscalPeriodId
                match period with
                | None ->
                    txn.Rollback()
                    let msg = sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId
                    Log.warn "{Msg}" [| msg :> obj |]
                    Error [ msg ]
                | Some _ ->
                    let existing = InvoiceRepository.findByTenantAndPeriod txn cmd.tenant cmd.fiscalPeriodId
                    match existing with
                    | Some _ ->
                        txn.Rollback()
                        let msg = sprintf "An invoice already exists for tenant '%s' in fiscal period %d" cmd.tenant cmd.fiscalPeriodId
                        Log.warn "{Msg}" [| msg :> obj |]
                        Error [ msg ]
                    | None ->
                        try
                            let invoice = InvoiceRepository.insert txn cmd
                            txn.Commit()
                            Log.info "Invoice {InvoiceId} recorded successfully" [| invoice.id :> obj |]
                            Ok invoice
                        with
                        | :? PostgresException as pgEx when pgEx.SqlState = "23505" ->
                            try txn.Rollback() with _ -> ()
                            let msg = sprintf "An invoice already exists for tenant '%s' in fiscal period %d" cmd.tenant cmd.fiscalPeriodId
                            Log.warn "Duplicate invoice detected via constraint: {Msg}" [| msg :> obj |]
                            Error [ msg ]
                        | ex ->
                            Log.errorExn ex "Failed to insert invoice" [||]
                            try txn.Rollback() with _ -> ()
                            Error [ sprintf "Persistence error: %s" ex.Message ]
            with ex ->
                Log.errorExn ex "Failed during invoice DB validation" [||]
                try txn.Rollback() with _ -> ()
                Error [ sprintf "Persistence error: %s" ex.Message ]

    let showInvoice (id: int) : Result<Invoice, string list> =
        Log.info "Showing invoice {InvoiceId}" [| id :> obj |]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let invoice = InvoiceRepository.findById txn id
            txn.Commit()
            match invoice with
            | None ->
                Error [ sprintf "Invoice with id %d does not exist" id ]
            | Some inv ->
                Ok inv
        with ex ->
            Log.errorExn ex "Failed to show invoice {InvoiceId}" [| id :> obj |]
            try txn.Rollback() with _ -> ()
            Error [ sprintf "Persistence error: %s" ex.Message ]

    let listInvoices (filter: ListInvoicesFilter) : Invoice list =
        Log.info "Listing invoices" [||]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = InvoiceRepository.list txn filter
            txn.Commit()
            result
        with ex ->
            Log.errorExn ex "Failed to list invoices" [||]
            try txn.Rollback() with _ -> ()
            []
