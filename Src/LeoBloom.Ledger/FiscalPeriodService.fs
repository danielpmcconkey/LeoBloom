namespace LeoBloom.Ledger

open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates validation and persistence for fiscal period close/reopen.
/// Opens its own connection + transaction for atomicity.
module FiscalPeriodService =

    /// Close a fiscal period. Idempotent -- closing an already-closed period succeeds.
    let closePeriod (cmd: CloseFiscalPeriodCommand) : Result<FiscalPeriod, string list> =
        Log.info "Closing fiscal period {PeriodId}" [| cmd.fiscalPeriodId :> obj |]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()

        try
            match FiscalPeriodRepository.findById txn cmd.fiscalPeriodId with
            | None ->
                txn.Rollback()
                Error [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
            | Some _ ->
                match FiscalPeriodRepository.setIsOpen txn cmd.fiscalPeriodId false with
                | Some period ->
                    txn.Commit()
                    Log.info "Closed fiscal period {PeriodId}" [| period.id :> obj |]
                    Ok period
                | None ->
                    txn.Rollback()
                    Error [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
        with ex ->
            Log.errorExn ex "Failed to close fiscal period {PeriodId}" [| cmd.fiscalPeriodId :> obj |]
            try txn.Rollback() with _ -> ()
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Reopen a fiscal period. Requires a non-empty reason. Idempotent.
    /// Logs the reason via Log.info.
    let reopenPeriod (cmd: ReopenFiscalPeriodCommand) : Result<FiscalPeriod, string list> =
        match validateReopenReason cmd.reason with
        | Error errs ->
            Log.warn "Reopen validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            Log.info "Reopening fiscal period {PeriodId}" [| cmd.fiscalPeriodId :> obj |]
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()

            try
                match FiscalPeriodRepository.findById txn cmd.fiscalPeriodId with
                | None ->
                    txn.Rollback()
                    Error [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
                | Some _ ->
                    match FiscalPeriodRepository.setIsOpen txn cmd.fiscalPeriodId true with
                    | Some period ->
                        txn.Commit()
                        Log.info "Reopened fiscal period {PeriodId}. Reason: {Reason}" [| period.id :> obj; cmd.reason :> obj |]
                        Ok period
                    | None ->
                        txn.Rollback()
                        Error [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
            with ex ->
                Log.errorExn ex "Failed to reopen fiscal period {PeriodId}" [| cmd.fiscalPeriodId :> obj |]
                try txn.Rollback() with _ -> ()
                Error [ sprintf "Persistence error: %s" ex.Message ]
