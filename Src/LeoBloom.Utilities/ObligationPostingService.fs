namespace LeoBloom.Utilities

open LeoBloom.Domain.Ops
open LeoBloom.Domain.Ledger

/// Orchestrates posting a confirmed obligation instance to the ledger.
/// Creates a journal entry and transitions the instance to posted.
module ObligationPostingService =

    let postToLedger (cmd: PostToLedgerCommand) : Result<PostToLedgerResult, string list> =
        Log.info "Posting obligation instance {InstanceId} to ledger" [| cmd.instanceId :> obj |]

        // Phase 1: Read + validate
        let readResult =
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()
            try
                // Load instance
                let instance = ObligationInstanceRepository.findById txn cmd.instanceId
                match instance with
                | None ->
                    txn.Rollback()
                    Error [ sprintf "Obligation instance with id %d does not exist" cmd.instanceId ]
                | Some inst ->
                    if not inst.isActive then
                        txn.Rollback()
                        Log.warn "Instance {InstanceId} is inactive" [| cmd.instanceId :> obj |]
                        Error [ sprintf "Obligation instance with id %d is inactive" cmd.instanceId ]
                    elif inst.status <> InstanceStatus.Confirmed then
                        txn.Rollback()
                        Log.warn "Instance {InstanceId} is not in confirmed status" [| cmd.instanceId :> obj |]
                        Error [ sprintf "Instance must be in confirmed status to post (current: %s)" (InstanceStatus.toString inst.status) ]
                    elif inst.amount.IsNone then
                        txn.Rollback()
                        Log.warn "Instance {InstanceId} has no amount" [| cmd.instanceId :> obj |]
                        Error [ "Instance must have an amount to post" ]
                    elif inst.confirmedDate.IsNone then
                        txn.Rollback()
                        Log.warn "Instance {InstanceId} has no confirmed_date" [| cmd.instanceId :> obj |]
                        Error [ "Instance must have a confirmed_date to post" ]
                    else
                        // Load agreement
                        let agreement = ObligationAgreementRepository.findById txn inst.obligationAgreementId
                        match agreement with
                        | None ->
                            txn.Rollback()
                            Error [ sprintf "Obligation agreement with id %d does not exist" inst.obligationAgreementId ]
                        | Some agr ->
                            if agr.sourceAccountId.IsNone then
                                txn.Rollback()
                                Log.warn "Agreement {AgreementId} has no source_account_id" [| agr.id :> obj |]
                                Error [ "Agreement must have a source_account_id to post" ]
                            elif agr.destAccountId.IsNone then
                                txn.Rollback()
                                Log.warn "Agreement {AgreementId} has no dest_account_id" [| agr.id :> obj |]
                                Error [ "Agreement must have a dest_account_id to post" ]
                            else
                                // Load fiscal period
                                let confirmedDate = inst.confirmedDate.Value
                                let period = FiscalPeriodRepository.findByDate txn confirmedDate
                                match period with
                                | None ->
                                    txn.Rollback()
                                    Log.warn "No fiscal period found for date {Date}" [| confirmedDate :> obj |]
                                    Error [ sprintf "No fiscal period covers the confirmed_date %O" confirmedDate ]
                                | Some fp ->
                                    txn.Commit()
                                    Ok (inst, agr, fp)
            with ex ->
                Log.errorExn ex "Failed during read phase for instance {InstanceId}" [| cmd.instanceId :> obj |]
                try txn.Rollback() with _ -> ()
                Error [ sprintf "Read phase error: %s" ex.Message ]

        match readResult with
        | Error errs -> Error errs
        | Ok (instance, agreement, fiscalPeriod) ->
            let amount = instance.amount.Value
            let confirmedDate = instance.confirmedDate.Value
            let sourceAccountId = agreement.sourceAccountId.Value
            let destAccountId = agreement.destAccountId.Value

            // Build PostJournalEntryCommand
            let jeCmd : PostJournalEntryCommand =
                { entryDate = confirmedDate
                  description = sprintf "%s \u2014 %s" agreement.name instance.name
                  source = Some "obligation"
                  fiscalPeriodId = fiscalPeriod.id
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
                        referenceValue = string instance.id } ] }

            // Phase 2: Post journal entry
            match JournalEntryService.post jeCmd with
            | Error errs ->
                Log.warn "Journal entry posting failed for instance {InstanceId}: {Errors}"
                    [| instance.id :> obj; errs :> obj |]
                Error errs
            | Ok posted ->
                Log.info "Created journal entry {JournalEntryId} for instance {InstanceId}"
                    [| posted.entry.id :> obj; instance.id :> obj |]

                // Phase 3: Transition instance to posted
                let transitionCmd : TransitionCommand =
                    { instanceId = instance.id
                      targetStatus = InstanceStatus.Posted
                      journalEntryId = Some posted.entry.id
                      amount = None
                      confirmedDate = None
                      notes = None }

                match ObligationInstanceService.transition transitionCmd with
                | Error errs ->
                    Log.warn "Transition to posted failed for instance {InstanceId} after journal entry {JournalEntryId} was created"
                        [| instance.id :> obj; posted.entry.id :> obj |]
                    Error errs
                | Ok updated ->
                    Log.info "Transitioned instance {InstanceId} to posted" [| updated.id :> obj |]
                    Ok { journalEntryId = posted.entry.id; instanceId = instance.id }
