namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates validation and persistence for fiscal period close/reopen.
module FiscalPeriodService =

    /// List all fiscal periods.
    let listPeriods (txn: NpgsqlTransaction) () : Result<FiscalPeriod list, string list> =
        Log.info "Listing all fiscal periods" [||]
        try
            let result = FiscalPeriodRepository.listAll txn
            Ok result
        with ex ->
            Log.errorExn ex "Failed to list fiscal periods" [||]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Create a new fiscal period. Validates key/dates are non-empty, start <= end,
    /// and that the proposed date range does not overlap any existing period.
    let createPeriod
        (txn: NpgsqlTransaction)
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
            try
                match FiscalPeriodRepository.findOverlapping txn startDate endDate with
                | Some conflict ->
                    Error [ sprintf "Date range overlaps existing period '%s' (%s to %s)"
                                conflict.periodKey
                                (conflict.startDate.ToString("yyyy-MM-dd"))
                                (conflict.endDate.ToString("yyyy-MM-dd")) ]
                | None ->
                    let period = FiscalPeriodRepository.create txn periodKey startDate endDate
                    Log.info "Created fiscal period {PeriodId}" [| period.id :> obj |]
                    Ok period
            with
            | :? PostgresException as ex when ex.SqlState = "23505" ->
                Error [ sprintf "A fiscal period with key '%s' already exists" periodKey ]
            | ex ->
                Log.errorExn ex "Failed to create fiscal period {PeriodKey}" [| periodKey :> obj |]
                Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Find a fiscal period by key. Used by CLI for id-or-key resolution.
    let findPeriodByKey (txn: NpgsqlTransaction) (key: string) : Result<FiscalPeriod, string list> =
        Log.info "Finding fiscal period by key {PeriodKey}" [| key :> obj |]
        try
            match FiscalPeriodRepository.findByKey txn key with
            | Some period -> Ok period
            | None -> Error [ sprintf "Fiscal period with key '%s' does not exist" key ]
        with ex ->
            Log.errorExn ex "Failed to find fiscal period by key {PeriodKey}" [| key :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Close a fiscal period. Idempotent -- closing an already-closed period succeeds.
    let closePeriod (txn: NpgsqlTransaction) (cmd: CloseFiscalPeriodCommand) : Result<FiscalPeriod, string list> =
        Log.info "Closing fiscal period {PeriodId}" [| cmd.fiscalPeriodId :> obj |]
        try
            match FiscalPeriodRepository.findById txn cmd.fiscalPeriodId with
            | None ->
                Error [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
            | Some _ ->
                match FiscalPeriodRepository.setIsOpen txn cmd.fiscalPeriodId false with
                | Some period ->
                    Log.info "Closed fiscal period {PeriodId}" [| period.id :> obj |]
                    Ok period
                | None ->
                    Error [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
        with ex ->
            Log.errorExn ex "Failed to close fiscal period {PeriodId}" [| cmd.fiscalPeriodId :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Reopen a fiscal period. Requires a non-empty reason. Idempotent.
    /// Logs the reason via Log.info.
    let reopenPeriod (txn: NpgsqlTransaction) (cmd: ReopenFiscalPeriodCommand) : Result<FiscalPeriod, string list> =
        match validateReopenReason cmd.reason with
        | Error errs ->
            Log.warn "Reopen validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            Log.info "Reopening fiscal period {PeriodId}" [| cmd.fiscalPeriodId :> obj |]
            try
                match FiscalPeriodRepository.findById txn cmd.fiscalPeriodId with
                | None ->
                    Error [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
                | Some _ ->
                    match FiscalPeriodRepository.setIsOpen txn cmd.fiscalPeriodId true with
                    | Some period ->
                        Log.info "Reopened fiscal period {PeriodId}. Reason: {Reason}" [| period.id :> obj; cmd.reason :> obj |]
                        Ok period
                    | None ->
                        Error [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
            with ex ->
                Log.errorExn ex "Failed to reopen fiscal period {PeriodId}" [| cmd.fiscalPeriodId :> obj |]
                Error [ sprintf "Persistence error: %s" ex.Message ]
