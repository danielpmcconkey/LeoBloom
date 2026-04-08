namespace LeoBloom.Ops

open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger

/// Orchestrates posting a confirmed obligation instance to the ledger.
/// Creates a journal entry and transitions the instance to posted.
/// Caller is responsible for connection and transaction lifecycle.
module ObligationPostingService =

    let postToLedger (txn: NpgsqlTransaction) (cmd: PostToLedgerCommand) : Result<PostToLedgerResult, string list> =
        Log.info "Posting obligation instance {InstanceId} to ledger" [| cmd.instanceId :> obj |]

        try
            // Load instance
            let instance = ObligationInstanceRepository.findById txn cmd.instanceId
            match instance with
            | None ->
                Error [ sprintf "Obligation instance with id %d does not exist" cmd.instanceId ]
            | Some inst ->
                if not inst.isActive then
                    Log.warn "Instance {InstanceId} is inactive" [| cmd.instanceId :> obj |]
                    Error [ sprintf "Obligation instance with id %d is inactive" cmd.instanceId ]
                elif inst.status <> InstanceStatus.Confirmed then
                    Log.warn "Instance {InstanceId} is not in confirmed status" [| cmd.instanceId :> obj |]
                    Error [ sprintf "Instance must be in confirmed status to post (current: %s)" (InstanceStatus.toString inst.status) ]
                elif inst.amount.IsNone then
                    Log.warn "Instance {InstanceId} has no amount" [| cmd.instanceId :> obj |]
                    Error [ "Instance must have an amount to post" ]
                elif inst.confirmedDate.IsNone then
                    Log.warn "Instance {InstanceId} has no confirmed_date" [| cmd.instanceId :> obj |]
                    Error [ "Instance must have a confirmed_date to post" ]
                else
                    // Load agreement
                    let agreement = ObligationAgreementRepository.findById txn inst.obligationAgreementId
                    match agreement with
                    | None ->
                        Error [ sprintf "Obligation agreement with id %d does not exist" inst.obligationAgreementId ]
                    | Some agr ->
                        if agr.sourceAccountId.IsNone then
                            Log.warn "Agreement {AgreementId} has no source_account_id" [| agr.id :> obj |]
                            Error [ "Agreement must have a source_account_id to post" ]
                        elif agr.destAccountId.IsNone then
                            Log.warn "Agreement {AgreementId} has no dest_account_id" [| agr.id :> obj |]
                            Error [ "Agreement must have a dest_account_id to post" ]
                        else
                            // Load fiscal period
                            let confirmedDate = inst.confirmedDate.Value
                            let period = FiscalPeriodRepository.findByDate txn confirmedDate
                            match period with
                            | None ->
                                Log.warn "No fiscal period found for date {Date}" [| confirmedDate :> obj |]
                                Error [ sprintf "No fiscal period covers the confirmed_date %O" confirmedDate ]
                            | Some fp ->
                                let amount = inst.amount.Value
                                let sourceAccountId = agr.sourceAccountId.Value
                                let destAccountId = agr.destAccountId.Value

                                // Build PostJournalEntryCommand
                                let jeCmd : PostJournalEntryCommand =
                                    { entryDate = confirmedDate
                                      description = sprintf "%s \u2014 %s" agr.name inst.name
                                      source = Some "obligation"
                                      fiscalPeriodId = fp.id
                                      lines =
                                        [ { accountId = destAccountId
                                            amount = amount
                                            entryType = EntryType.Debit
                                            memo = None }
                                          { accountId = sourceAccountId
                                            amount = amount
                                            entryType = EntryType.Credit
                                            memo = None } ]
                                      references =
                                        [ { referenceType = "obligation"
                                            referenceValue = string inst.id } ] }

                                // Idempotency guard: check for existing non-voided journal entry
                                let existingJeId =
                                    JournalEntryRepository.findNonVoidedByReference
                                        txn "obligation" (string inst.id)

                                match existingJeId with
                                | Some jeId ->
                                    Log.info
                                        "Idempotency guard: found existing journal entry {JournalEntryId} for instance {InstanceId}, skipping post"
                                        [| jeId :> obj; inst.id :> obj |]

                                    // Transition instance to posted (using existing JE)
                                    let transitionCmd : TransitionCommand =
                                        { instanceId = inst.id
                                          targetStatus = InstanceStatus.Posted
                                          journalEntryId = Some jeId
                                          amount = None
                                          confirmedDate = None
                                          notes = None }

                                    match ObligationInstanceService.transition txn transitionCmd with
                                    | Error errs ->
                                        Log.warn
                                            "Transition to posted failed for instance {InstanceId} using existing journal entry {JournalEntryId}"
                                            [| inst.id :> obj; jeId :> obj |]
                                        Error errs
                                    | Ok updated ->
                                        Log.info "Transitioned instance {InstanceId} to posted (idempotent)"
                                            [| updated.id :> obj |]
                                        Ok { journalEntryId = jeId; instanceId = inst.id }

                                | None ->
                                    // Post journal entry
                                    match JournalEntryService.post txn jeCmd with
                                    | Error errs ->
                                        Log.warn "Journal entry posting failed for instance {InstanceId}: {Errors}"
                                            [| inst.id :> obj; errs :> obj |]
                                        Error errs
                                    | Ok posted ->
                                        Log.info "Created journal entry {JournalEntryId} for instance {InstanceId}"
                                            [| posted.entry.id :> obj; inst.id :> obj |]

                                        // Transition instance to posted
                                        let transitionCmd : TransitionCommand =
                                            { instanceId = inst.id
                                              targetStatus = InstanceStatus.Posted
                                              journalEntryId = Some posted.entry.id
                                              amount = None
                                              confirmedDate = None
                                              notes = None }

                                        match ObligationInstanceService.transition txn transitionCmd with
                                        | Error errs ->
                                            Log.warn "Transition to posted failed for instance {InstanceId} after journal entry {JournalEntryId} was created"
                                                [| inst.id :> obj; posted.entry.id :> obj |]
                                            Error errs
                                        | Ok updated ->
                                            Log.info "Transitioned instance {InstanceId} to posted" [| updated.id :> obj |]
                                            Ok { journalEntryId = posted.entry.id; instanceId = inst.id }
        with ex ->
            Log.errorExn ex "Failed during posting for instance {InstanceId}" [| cmd.instanceId :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]
