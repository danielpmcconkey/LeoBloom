namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates validation and persistence for fiscal period close/reopen.
/// Opens its own connection + transaction for atomicity.
module FiscalPeriodService =

    /// List all fiscal periods.
    let listPeriods () : Result<FiscalPeriod list, string list> =
        Log.info "Listing all fiscal periods" [||]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = FiscalPeriodRepository.listAll txn
            txn.Commit()
            Ok result
        with ex ->
            Log.errorExn ex "Failed to list fiscal periods" [||]
            try txn.Rollback() with _ -> ()
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Create a new fiscal period. Validates key/dates are non-empty,
    /// start <= end. Does NOT check for date overlaps (see F3 resolution).
    let createPeriod
        (periodKey: string)
        (startDate: DateOnly)
        (endDate: DateOnly)
        : Result<FiscalPeriod, string list> =
        let errors = ResizeArray<string>()
        if String.IsNullOrWhiteSpace periodKey then
            errors.Add("Period key is required and cannot be blank")
        if startDate > endDate then
            errors.Add("Start date must be on or before end date")
        if errors.Count > 0 then
            Error (errors |> Seq.toList)
        else
            Log.info "Creating fiscal period {PeriodKey}" [| periodKey :> obj |]
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()
            try
                let period = FiscalPeriodRepository.create txn periodKey startDate endDate
                txn.Commit()
                Log.info "Created fiscal period {PeriodId}" [| period.id :> obj |]
                Ok period
            with
            | :? PostgresException as ex when ex.SqlState = "23505" ->
                try txn.Rollback() with _ -> ()
                Error [ sprintf "A fiscal period with key '%s' already exists" periodKey ]
            | ex ->
                Log.errorExn ex "Failed to create fiscal period {PeriodKey}" [| periodKey :> obj |]
                try txn.Rollback() with _ -> ()
                Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Find a fiscal period by key. Used by CLI for id-or-key resolution.
    let findPeriodByKey (key: string) : Result<FiscalPeriod, string list> =
        Log.info "Finding fiscal period by key {PeriodKey}" [| key :> obj |]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result =
                match FiscalPeriodRepository.findByKey txn key with
                | Some period -> Ok period
                | None -> Error [ sprintf "Fiscal period with key '%s' does not exist" key ]
            txn.Commit()
            result
        with ex ->
            Log.errorExn ex "Failed to find fiscal period by key {PeriodKey}" [| key :> obj |]
            try txn.Rollback() with _ -> ()
            Error [ sprintf "Persistence error: %s" ex.Message ]

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
