namespace LeoBloom.Ops

open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger

/// Orchestrates initiation and confirmation of transfers between asset accounts.
/// Caller is responsible for connection and transaction lifecycle.
module TransferService =

    let private lookupAccountInfo (txn: NpgsqlTransaction) (accountId: int) : (bool * string) option =
        use sql = new NpgsqlCommand(
            "SELECT a.is_active, at.name \
             FROM ledger.account a \
             JOIN ledger.account_type at ON a.account_type_id = at.id \
             WHERE a.id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", accountId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let result = Some (reader.GetBoolean(0), reader.GetString(1))
            reader.Close()
            result
        else
            reader.Close()
            None

    let initiate (txn: NpgsqlTransaction) (cmd: InitiateTransferCommand) : Result<Transfer, string list> =
        Log.info "Initiating transfer from account {FromId} to account {ToId} for {Amount}"
            [| cmd.fromAccountId :> obj; cmd.toAccountId :> obj; cmd.amount :> obj |]

        // Phase 1: Pure validation
        let pureErrors =
            [ if cmd.amount <= 0m then
                  "Transfer amount must be greater than zero"
              if cmd.fromAccountId = cmd.toAccountId then
                  "Cannot transfer to the same account" ]

        if not pureErrors.IsEmpty then
            Log.warn "Transfer initiation validation failed: {Errors}" [| pureErrors :> obj |]
            Error pureErrors
        else
            try
                let fromInfo = lookupAccountInfo txn cmd.fromAccountId
                let toInfo = lookupAccountInfo txn cmd.toAccountId

                let dbErrors =
                    [ match fromInfo with
                      | None -> sprintf "Account with id %d does not exist" cmd.fromAccountId
                      | Some (false, _) -> sprintf "Account with id %d is not active" cmd.fromAccountId
                      | Some (_, typeName) when typeName <> "asset" ->
                          sprintf "Account with id %d is not an asset account (type: %s)" cmd.fromAccountId typeName
                      | _ -> ()

                      match toInfo with
                      | None -> sprintf "Account with id %d does not exist" cmd.toAccountId
                      | Some (false, _) -> sprintf "Account with id %d is not active" cmd.toAccountId
                      | Some (_, typeName) when typeName <> "asset" ->
                          sprintf "Account with id %d is not an asset account (type: %s)" cmd.toAccountId typeName
                      | _ -> () ]

                if not dbErrors.IsEmpty then
                    Log.warn "Transfer initiation DB validation failed: {Errors}" [| dbErrors :> obj |]
                    Error dbErrors
                else
                    let transfer = TransferRepository.insert txn cmd
                    Log.info "Transfer {TransferId} initiated successfully" [| transfer.id :> obj |]
                    Ok transfer
            with ex ->
                Log.errorExn ex "Failed to initiate transfer" [||]
                Error [ sprintf "Persistence error: %s" ex.Message ]

    let confirm (txn: NpgsqlTransaction) (cmd: ConfirmTransferCommand) : Result<Transfer, string list> =
        Log.info "Confirming transfer {TransferId} on {ConfirmedDate}"
            [| cmd.transferId :> obj; cmd.confirmedDate :> obj |]

        try
            let transfer = TransferRepository.findById txn cmd.transferId
            match transfer with
            | None ->
                Error [ sprintf "Transfer with id %d does not exist" cmd.transferId ]
            | Some t ->
                if not t.isActive then
                    Log.warn "Transfer {TransferId} is inactive" [| cmd.transferId :> obj |]
                    Error [ sprintf "Transfer with id %d is inactive" cmd.transferId ]
                elif t.status <> TransferStatus.Initiated then
                    Log.warn "Transfer {TransferId} is not in initiated status" [| cmd.transferId :> obj |]
                    Error [ sprintf "Transfer must be in initiated status to confirm (current: %s)" (TransferStatus.toString t.status) ]
                else
                    let period = FiscalPeriodRepository.findByDate txn cmd.confirmedDate
                    match period with
                    | None ->
                        Log.warn "No fiscal period found for date {Date}" [| cmd.confirmedDate :> obj |]
                        Error [ sprintf "No fiscal period covers the confirmed_date %O" cmd.confirmedDate ]
                    | Some fp ->
                        // Build and post journal entry
                        let description =
                            t.description
                            |> Option.defaultValue (sprintf "Transfer %d" t.id)

                        let jeCmd : PostJournalEntryCommand =
                            { entryDate = cmd.confirmedDate
                              description = description
                              source = Some "transfer"
                              fiscalPeriodId = fp.id
                              lines =
                                [ { accountId = t.toAccountId
                                    amount = t.amount
                                    entryType = EntryType.Debit
                                    memo = None }
                                  { accountId = t.fromAccountId
                                    amount = t.amount
                                    entryType = EntryType.Credit
                                    memo = None } ]
                              references =
                                [ { referenceType = "transfer"
                                    referenceValue = string t.id } ]
                              adjustmentForPeriodId = None }

                        // Idempotency guard: check for existing non-voided journal entry
                        let existingJeId =
                            JournalEntryRepository.findNonVoidedByReference
                                txn "transfer" (string t.id)

                        match existingJeId with
                        | Some jeId ->
                            Log.info
                                "Idempotency guard: found existing journal entry {JournalEntryId} for transfer {TransferId}, skipping post"
                                [| jeId :> obj; t.id :> obj |]

                            let updated = TransferRepository.updateConfirm txn t.id cmd.confirmedDate jeId
                            Log.info "Transfer {TransferId} confirmed successfully (idempotent)"
                                [| updated.id :> obj |]
                            Ok updated

                        | None ->
                            match JournalEntryService.post txn jeCmd with
                            | Error errs ->
                                Log.warn "Journal entry posting failed for transfer {TransferId}: {Errors}"
                                    [| t.id :> obj; errs :> obj |]
                                Error errs
                            | Ok posted ->
                                Log.info "Created journal entry {JournalEntryId} for transfer {TransferId}"
                                    [| posted.entry.id :> obj; t.id :> obj |]

                                let updated = TransferRepository.updateConfirm txn t.id cmd.confirmedDate posted.entry.id
                                Log.info "Transfer {TransferId} confirmed successfully" [| updated.id :> obj |]
                                Ok updated
        with ex ->
            Log.errorExn ex "Failed to confirm transfer {TransferId}" [| cmd.transferId :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    let show (txn: NpgsqlTransaction) (id: int) : Result<Transfer, string list> =
        Log.info "Showing transfer {TransferId}" [| id :> obj |]
        try
            let transfer = TransferRepository.findById txn id
            match transfer with
            | None ->
                Error [ sprintf "Transfer with id %d does not exist" id ]
            | Some t ->
                Ok t
        with ex ->
            Log.errorExn ex "Failed to show transfer {TransferId}" [| id :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    let list (txn: NpgsqlTransaction) (filter: ListTransfersFilter) : Transfer list =
        Log.info "Listing transfers" [||]
        try
            TransferRepository.list txn filter
        with ex ->
            Log.errorExn ex "Failed to list transfers" [||]
            []
