namespace LeoBloom.Ops

open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Ledger
open LeoBloom.Utilities

/// Wraps FiscalPeriodValidation + FiscalPeriodService.closePeriod.
/// Enforces pre-close validation before allowing a period to close.
/// Force bypass is supported with mandatory note; audit trail records bypass details.
module FiscalPeriodCloseService =

    /// Result of a validated close operation, carrying both the closed period
    /// and the validation result (useful for audit/logging even when force-closed).
    type CloseWithValidationResult =
        { Period: FiscalPeriod
          ValidationResult: PreCloseValidationResult }

    /// Close a fiscal period with pre-close validation.
    ///
    /// - If all validation checks pass: closes normally.
    /// - If force = true and checks fail: closes anyway; records bypass in audit note.
    /// - If force = false and checks fail: returns Error with the failed check messages.
    /// - If force = true but note is None: returns Error (note is required for force).
    let closePeriodWithValidation
        (txn: NpgsqlTransaction)
        (cmd: CloseFiscalPeriodCommand)
        : Result<CloseWithValidationResult, string list> =

        Log.info "Validated close requested for fiscal period {PeriodId} (force={Force})"
            [| cmd.fiscalPeriodId :> obj; cmd.force :> obj |]

        // Validate: force requires a note
        if cmd.force && cmd.note.IsNone then
            Log.warn "Force close rejected: --note is required when using --force" [||]
            Error [ "--note is required when using --force" ]
        else

        // Run pre-close validation (always — even on force, we log what was bypassed)
        match FiscalPeriodValidation.validatePreClose txn cmd.fiscalPeriodId with
        | Error errs -> Error errs
        | Ok validationResult ->

        // Log each check result
        for check in validationResult.Checks do
            let status = if check.Passed then "PASSED" else "FAILED"
            Log.info "Pre-close check {Check}: {Status} — {Message}"
                [| check.Check :> obj; status :> obj; check.Message :> obj |]

        if validationResult.AllPassed then
            // All checks passed — proceed with normal close
            match FiscalPeriodService.closePeriod txn cmd with
            | Error errs -> Error errs
            | Ok period ->
                Ok { Period = period; ValidationResult = validationResult }

        elif cmd.force then
            // Checks failed but force=true — close and append bypass note to audit
            let failedChecks =
                validationResult.Checks
                |> List.filter (fun c -> not c.Passed)
                |> List.map (fun c -> c.Message)
                |> String.concat "; "

            let forceNote = cmd.note.Value
            let auditNote =
                sprintf "[FORCE] %s | Bypassed failures: %s" forceNote failedChecks

            let closeCmd =
                { cmd with note = Some auditNote }

            Log.warn "Force-closing period {PeriodId}. Bypassed: {Failures}"
                [| cmd.fiscalPeriodId :> obj; failedChecks :> obj |]

            match FiscalPeriodService.closePeriod txn closeCmd with
            | Error errs -> Error errs
            | Ok period ->
                Ok { Period = period; ValidationResult = validationResult }

        else
            // Checks failed and no force — block close, return all failure messages
            let failures =
                validationResult.Checks
                |> List.filter (fun c -> not c.Passed)
                |> List.map (fun c -> c.Message)
            Log.warn "Close blocked for period {PeriodId}: {Failures}"
                [| cmd.fiscalPeriodId :> obj; failures :> obj |]
            Error failures
