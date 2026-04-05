namespace LeoBloom.Utilities

open Npgsql
open LeoBloom.Domain.Ops

/// Orchestrates spawning obligation instances from an agreement and date range.
/// Opens its own connection + transaction for atomicity.
module ObligationInstanceService =

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
