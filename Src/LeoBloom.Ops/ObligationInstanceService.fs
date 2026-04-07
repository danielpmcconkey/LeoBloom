namespace LeoBloom.Ops

open System
open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Utilities

/// Orchestrates spawning obligation instances from an agreement and date range.
/// Opens its own connection + transaction for atomicity.
module ObligationInstanceService =

    let private journalEntryExists (txn: Npgsql.NpgsqlTransaction) (jeId: int) : bool =
        use sql = new Npgsql.NpgsqlCommand(
            "SELECT 1 FROM ledger.journal_entry WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", jeId) |> ignore
        use reader = sql.ExecuteReader()
        let exists = reader.Read()
        reader.Close()
        exists

    let list (filter: ListInstancesFilter) : ObligationInstance list =
        Log.info "Listing obligation instances" [||]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = ObligationInstanceRepository.list txn filter
            txn.Commit()
            result
        with ex ->
            Log.errorExn ex "Failed to list obligation instances" [||]
            try txn.Rollback() with _ -> ()
            []

    let findUpcoming (today: DateOnly) (days: int) : ObligationInstance list =
        Log.info "Finding upcoming obligation instances within {Days} days" [| days :> obj |]
        let horizon = today.AddDays(days)
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = ObligationInstanceRepository.findUpcoming txn today horizon
            txn.Commit()
            result
        with ex ->
            Log.errorExn ex "Failed to find upcoming obligation instances" [||]
            try txn.Rollback() with _ -> ()
            []

    let transition (cmd: TransitionCommand) : Result<ObligationInstance, string list> =
        Log.info "Transitioning instance {InstanceId} to {TargetStatus}"
            [| cmd.instanceId :> obj; InstanceStatus.toString cmd.targetStatus :> obj |]

        match StatusTransition.validateTransitionCommand cmd with
        | Error errs ->
            Log.warn "Transition command validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()
            try
                match ObligationInstanceRepository.findById txn cmd.instanceId with
                | None ->
                    txn.Rollback()
                    let msg = sprintf "Obligation instance with id %d does not exist" cmd.instanceId
                    Log.warn "{Message}" [| msg :> obj |]
                    Error [ msg ]
                | Some instance ->
                    if not instance.isActive then
                        txn.Rollback()
                        let msg = sprintf "Obligation instance with id %d is inactive" instance.id
                        Log.warn "{Message}" [| msg :> obj |]
                        Error [ msg ]
                    else if not (StatusTransition.isValidTransition instance.status cmd.targetStatus) then
                        txn.Rollback()
                        let msg =
                            sprintf "invalid transition from %s to %s"
                                (InstanceStatus.toString instance.status)
                                (InstanceStatus.toString cmd.targetStatus)
                        Log.warn "{Message}" [| msg :> obj |]
                        Error [ msg ]
                    else
                        // Confirmed guard: amount must be available
                        let effectiveAmount =
                            match cmd.targetStatus with
                            | InstanceStatus.Confirmed ->
                                match cmd.amount with
                                | Some _ -> cmd.amount
                                | None -> instance.amount
                            | _ -> cmd.amount

                        if cmd.targetStatus = InstanceStatus.Confirmed && effectiveAmount.IsNone then
                            txn.Rollback()
                            Error [ "amount is required for confirmed transition (not on instance or in command)" ]
                        // Skipped guard: notes must be available
                        elif cmd.targetStatus = InstanceStatus.Skipped && cmd.notes.IsNone && instance.notes.IsNone then
                            txn.Rollback()
                            Error [ "notes are required for skipped transition" ]
                        else
                            // Posted guard: journal entry must exist
                            match cmd.targetStatus, cmd.journalEntryId with
                            | InstanceStatus.Posted, Some jeId when not (journalEntryExists txn jeId) ->
                                txn.Rollback()
                                let msg = sprintf "Journal entry with id %d does not exist" jeId
                                Log.warn "{Message}" [| msg :> obj |]
                                Error [ msg ]
                            | _ ->
                                let amountToSet =
                                    match cmd.targetStatus with
                                    | InstanceStatus.Confirmed -> effectiveAmount
                                    | _ -> None

                                let journalEntryIdToSet =
                                    match cmd.targetStatus with
                                    | InstanceStatus.Posted -> cmd.journalEntryId
                                    | _ -> None

                                let confirmedDateToSet =
                                    match cmd.targetStatus with
                                    | InstanceStatus.Confirmed -> cmd.confirmedDate
                                    | _ -> None

                                let notesToSet =
                                    match cmd.targetStatus with
                                    | InstanceStatus.Skipped -> cmd.notes
                                    | _ -> None

                                let updated =
                                    ObligationInstanceRepository.updateStatus
                                        txn instance.id cmd.targetStatus
                                        amountToSet confirmedDateToSet journalEntryIdToSet notesToSet

                                txn.Commit()

                                Log.info "Transitioned instance {InstanceId} from {From} to {To}"
                                    [| instance.id :> obj
                                       InstanceStatus.toString instance.status :> obj
                                       InstanceStatus.toString cmd.targetStatus :> obj |]

                                Ok updated
            with ex ->
                Log.errorExn ex "Failed to transition instance {InstanceId}" [| cmd.instanceId :> obj |]
                try txn.Rollback() with _ -> ()
                Error [ sprintf "Persistence error: %s" ex.Message ]

    let detectOverdue (referenceDate: DateOnly) : OverdueDetectionResult =
        Log.info "Running overdue detection with reference date {ReferenceDate}" [| referenceDate :> obj |]

        // Find candidates in a read-only transaction
        let candidatesResult =
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()
            try
                let result = ObligationInstanceRepository.findOverdueCandidates txn referenceDate
                txn.Commit()
                Ok result
            with ex ->
                Log.errorExn ex "Failed to query overdue candidates" [||]
                try txn.Rollback() with _ -> ()
                Error ex.Message

        match candidatesResult with
        | Error _ ->
            { transitioned = 0; errors = []; queryFailed = true }
        | Ok candidates when candidates.IsEmpty ->
            Log.info "No overdue candidates found" [||]
            { transitioned = 0; errors = []; queryFailed = false }
        | Ok candidates ->
            Log.info "Found {Count} overdue candidates" [| candidates.Length :> obj |]

            let mutable transitioned = 0
            let mutable errors = []

            for candidate in candidates do
                let cmd =
                    { instanceId = candidate.id
                      targetStatus = Overdue
                      amount = None; confirmedDate = None
                      journalEntryId = None; notes = None }
                match transition cmd with
                | Ok _ -> transitioned <- transitioned + 1
                | Error errs ->
                    let msg = System.String.Join("; ", errs)
                    Log.warn "Failed to transition instance {Id} to overdue: {Error}"
                        [| candidate.id :> obj; msg :> obj |]
                    errors <- (candidate.id, msg) :: errors

            let result = { transitioned = transitioned; errors = errors |> List.rev; queryFailed = false }

            Log.info "Overdue detection complete: {Transitioned} transitioned, {Errors} errors"
                [| result.transitioned :> obj; result.errors.Length :> obj |]

            result

    let spawn (cmd: SpawnObligationInstancesCommand) : Result<SpawnResult, string list> =
        Log.info "Spawning obligation instances for agreement {AgreementId} from {Start} to {End}"
            [| cmd.obligationAgreementId :> obj; cmd.startDate :> obj; cmd.endDate :> obj |]

        match ObligationInstanceSpawning.validateSpawnCommand cmd with
        | Error errs ->
            Log.warn "Spawn command validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()
            try
                match ObligationAgreementRepository.findById txn cmd.obligationAgreementId with
                | None ->
                    txn.Rollback()
                    let msg = sprintf "Obligation agreement with id %d does not exist" cmd.obligationAgreementId
                    Log.warn "{Message}" [| msg :> obj |]
                    Error [ msg ]
                | Some agreement ->
                    if not agreement.isActive then
                        txn.Rollback()
                        let msg = sprintf "Obligation agreement with id %d is inactive" agreement.id
                        Log.warn "{Message}" [| msg :> obj |]
                        Error [ msg ]
                    else
                        let effectiveDay = agreement.expectedDay |> Option.defaultValue 1

                        let allDates =
                            ObligationInstanceSpawning.generateExpectedDates
                                agreement.cadence effectiveDay cmd.startDate cmd.endDate

                        let existingDates =
                            ObligationInstanceRepository.findExistingDates txn agreement.id allDates

                        let newDates =
                            allDates |> List.filter (fun d -> not (existingDates.Contains d))

                        let insertedInstances =
                            newDates
                            |> List.map (fun date ->
                                let name = ObligationInstanceSpawning.generateInstanceName agreement.cadence date
                                ObligationInstanceRepository.insert
                                    txn agreement.id name Expected agreement.amount date)

                        txn.Commit()

                        let skippedCount = allDates.Length - newDates.Length

                        Log.info "Spawned {Created} instances, skipped {Skipped} for agreement {AgreementId}"
                            [| insertedInstances.Length :> obj; skippedCount :> obj; agreement.id :> obj |]

                        Ok { created = insertedInstances; skippedCount = skippedCount }
            with ex ->
                Log.errorExn ex "Failed to spawn obligation instances for agreement {AgreementId}"
                    [| cmd.obligationAgreementId :> obj |]
                try txn.Rollback() with _ -> ()
                Error [ sprintf "Persistence error: %s" ex.Message ]
