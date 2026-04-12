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

    /// Close a fiscal period. Idempotent -- closing an already-closed period succeeds
    /// without writing another audit row. On transition, writes an audit row.
    let closePeriod (txn: NpgsqlTransaction) (cmd: CloseFiscalPeriodCommand) : Result<FiscalPeriod, string list> =
        Log.info "Closing fiscal period {PeriodId}" [| cmd.fiscalPeriodId :> obj |]
        try
            match FiscalPeriodRepository.findById txn cmd.fiscalPeriodId with
            | None ->
                Error [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
            | Some existing when not existing.isOpen ->
                // Already closed — idempotent, no audit row
                Log.info "Fiscal period {PeriodId} already closed (idempotent)" [| cmd.fiscalPeriodId :> obj |]
                Ok existing
            | Some _ ->
                match FiscalPeriodRepository.closePeriod txn cmd.fiscalPeriodId cmd.actor with
                | None ->
                    Error [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
                | Some period ->
                    FiscalPeriodAuditRepository.insert txn
                        {| fiscalPeriodId = period.id
                           action = "closed"
                           actor = cmd.actor
                           note = cmd.note |} |> ignore
                    Log.info "Closed fiscal period {PeriodId}" [| period.id :> obj |]
                    Ok period
        with ex ->
            Log.errorExn ex "Failed to close fiscal period {PeriodId}" [| cmd.fiscalPeriodId :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Reopen a fiscal period. Requires a non-empty reason. Idempotent -- reopening an
    /// already-open period succeeds without writing an audit row. On transition, writes
    /// an audit row and increments reopened_count.
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
                | Some existing when existing.isOpen ->
                    // Already open — idempotent, no audit row
                    Log.info "Fiscal period {PeriodId} already open (idempotent)" [| cmd.fiscalPeriodId :> obj |]
                    Ok existing
                | Some _ ->
                    match FiscalPeriodRepository.reopenPeriod txn cmd.fiscalPeriodId with
                    | None ->
                        Error [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
                    | Some period ->
                        FiscalPeriodAuditRepository.insert txn
                            {| fiscalPeriodId = period.id
                               action = "reopened"
                               actor = cmd.actor
                               note = Some cmd.reason |} |> ignore
                        Log.info "Reopened fiscal period {PeriodId}. Reason: {Reason}" [| period.id :> obj; cmd.reason :> obj |]
                        Ok period
            with ex ->
                Log.errorExn ex "Failed to reopen fiscal period {PeriodId}" [| cmd.fiscalPeriodId :> obj |]
                Error [ sprintf "Persistence error: %s" ex.Message ]

    /// List all audit entries for the given fiscal period. Returns error if period does not exist.
    let listAudit (txn: NpgsqlTransaction) (periodId: int) : Result<FiscalPeriodAuditEntry list, string list> =
        Log.info "Listing audit trail for fiscal period {PeriodId}" [| periodId :> obj |]
        try
            match FiscalPeriodRepository.findById txn periodId with
            | None ->
                Error [ sprintf "Fiscal period with id %d does not exist" periodId ]
            | Some _ ->
                let entries = FiscalPeriodAuditRepository.listByPeriod txn periodId
                Ok entries
        with ex ->
            Log.errorExn ex "Failed to list audit for fiscal period {PeriodId}" [| periodId :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]
